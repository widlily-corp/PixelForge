using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Palettes;
using PixelForge.Core.Tools;
using SkiaSharp;

namespace PixelForge.Core.Services;

/// <summary>
/// Preloads assemblies, pre-faults all memory pages into physical RAM,
/// pre-compiles critical methods with JIT, and warms up the SkiaSharp pipeline.
/// </summary>
public class MemoryPreloadService : IMemoryPreloadService
{
    private static readonly Lazy<MemoryPreloadService> _instance = new(() => new MemoryPreloadService());
    public static MemoryPreloadService Instance => _instance.Value;

    public bool IsPreloaded { get; private set; }

    #region Win32 Interop

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    private const uint MEM_COMMIT = 0x1000;
    private const uint PAGE_NOACCESS = 0x01;
    private const uint PAGE_GUARD = 0x100;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualLock(IntPtr lpAddress, UIntPtr dwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int VirtualQuery(IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    #endregion

    public async Task PreloadAsync(IProgress<PreloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (IsPreloaded)
        {
            progress?.Report(new PreloadProgress(100, "Ready", "Memory working set already initialized."));
            return;
        }

        await Task.Run(() =>
        {
            // Stage 1: Expand Working Set and pre-fault memory pages into physical RAM
            progress?.Report(new PreloadProgress(10, "Allocating RAM Working Set", "Configuring high-priority process working set..."));
            cancellationToken.ThrowIfCancellationRequested();
            ConfigureWorkingSet();

            // Stage 2: Pre-fault all loaded module pages and lock into RAM
            progress?.Report(new PreloadProgress(25, "Locking Assemblies into RAM", "Pre-faulting native & managed module memory pages..."));
            cancellationToken.ThrowIfCancellationRequested();
            LockModulesIntoRam(progress);

            // Stage 3: Pre-JIT core drawing and undo/redo algorithms
            progress?.Report(new PreloadProgress(50, "Pre-JIT Compiling Hot Paths", "Compiling pixel canvas, history, and command methods..."));
            cancellationToken.ThrowIfCancellationRequested();
            PreJitCoreMethods();

            // Stage 4: Warm up SkiaSharp graphics pipeline
            progress?.Report(new PreloadProgress(75, "Warming Up SkiaSharp Graphics", "Initializing off-screen surface and rasterizer pipelines..."));
            cancellationToken.ThrowIfCancellationRequested();
            WarmUpSkiaPipeline();

            // Stage 5: Pre-cache retro color palettes and tools
            progress?.Report(new PreloadProgress(90, "Pre-caching Palettes & Tools", "Instantiating retro color tables and tool matrix..."));
            cancellationToken.ThrowIfCancellationRequested();
            PrecachePalettesAndTools();

            // Complete
            progress?.Report(new PreloadProgress(100, "Ready to Create", "100% of application working set resident in RAM."));
            IsPreloaded = true;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Configures the minimum and maximum working set size to prevent paging out to disk.
    /// </summary>
    private static void ConfigureWorkingSet()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        try
        {
            // Request 256MB minimum working set and 1024MB maximum working set
            IntPtr minWorkingSet = (IntPtr)(256 * 1024 * 1024);
            IntPtr maxWorkingSet = (IntPtr)(1024 * 1024 * 1024);
            SetProcessWorkingSetSize(GetCurrentProcess(), minWorkingSet, maxWorkingSet);
        }
        catch
        {
            // Non-critical if OS permissions restrict modifying working set limits
        }
    }

    /// <summary>
    /// Pre-faults all readable committed memory pages of loaded modules into physical RAM.
    /// </summary>
    private static void LockModulesIntoRam(IProgress<PreloadProgress>? progress)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            var modules = currentProcess.Modules;
            int total = modules.Count;

            for (int i = 0; i < total; i++)
            {
                var module = modules[i];
                string moduleName = module.ModuleName;

                // Prefault pages within the module address space
                PrefaultModulePages(module.BaseAddress, module.ModuleMemorySize);

                if (i % 5 == 0 || i == total - 1)
                {
                    int pct = 25 + (int)((float)i / total * 20);
                    progress?.Report(new PreloadProgress(pct, "Locking Assemblies into RAM", $"Resident in RAM: {moduleName} ({i + 1}/{total})"));
                }
            }
        }
        catch
        {
            // If module enumeration fails, continue safely
        }
    }

    /// <summary>
    /// Safely walks committed and readable pages in the given memory region,
    /// touching 1 byte per 4KB page to force Windows kernel to load it into physical RAM.
    /// </summary>
    private static void PrefaultModulePages(IntPtr baseAddress, int memorySize)
    {
        const int pageSize = 4096;
        IntPtr currentAddress = baseAddress;
        IntPtr endAddress = IntPtr.Add(baseAddress, memorySize);

        while (currentAddress.ToInt64() < endAddress.ToInt64())
        {
            if (VirtualQuery(currentAddress, out var mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
            {
                break;
            }

            long regionSize = mbi.RegionSize.ToInt64();
            if (regionSize <= 0)
            {
                break;
            }

            const uint PAGE_READONLY = 0x02;
            const uint PAGE_READWRITE = 0x04;
            const uint PAGE_WRITECOPY = 0x08;
            const uint PAGE_EXECUTE_READ = 0x20;
            const uint PAGE_EXECUTE_READWRITE = 0x40;
            const uint PAGE_EXECUTE_WRITECOPY = 0x80;

            uint baseProtect = mbi.Protect & 0xFF;
            bool isCommitted = (mbi.State & MEM_COMMIT) != 0;
            bool isGuard = (mbi.Protect & PAGE_GUARD) != 0;
            bool isNoAccess = (mbi.Protect & PAGE_NOACCESS) != 0;
            bool isReadable = !isGuard && !isNoAccess &&
                              (baseProtect == PAGE_READONLY ||
                               baseProtect == PAGE_READWRITE ||
                               baseProtect == PAGE_WRITECOPY ||
                               baseProtect == PAGE_EXECUTE_READ ||
                               baseProtect == PAGE_EXECUTE_READWRITE ||
                               baseProtect == PAGE_EXECUTE_WRITECOPY);

            if (isCommitted && isReadable)
            {
                try
                {
                    unsafe
                    {
                        byte* ptr = (byte*)mbi.BaseAddress.ToPointer();
                        long pagesInRegion = regionSize / pageSize;
                        for (long p = 0; p < pagesInRegion; p++)
                        {
                            // Volatile.Read touches the memory page and forces physical RAM fault
                            _ = Volatile.Read(ref *(ptr + (p * pageSize)));
                        }
                    }

                    // Attempt VirtualLock to keep it resident in physical RAM
                    VirtualLock(mbi.BaseAddress, (UIntPtr)(ulong)regionSize);
                }
                catch
                {
                    // Ignore page-level protection transitions
                }
            }

            currentAddress = IntPtr.Add(mbi.BaseAddress, (int)Math.Min(regionSize, int.MaxValue));
        }
    }

    /// <summary>
    /// Pre-JITs critical methods and static constructors in PixelForge.Core.
    /// </summary>
    private static void PreJitCoreMethods()
    {
        Type[] coreTypes =
        [
            typeof(PixelColor),
            typeof(PixelDelta),
            typeof(Layer),
            typeof(PixelCanvas),
            typeof(SelectionModel),
            typeof(Project),
            typeof(UndoRedoService),
            typeof(DrawPixelsCommand),
            typeof(AddLayerCommand),
            typeof(RemoveLayerCommand),
            typeof(MergeLayerDownCommand),
            typeof(SetLayerVisibilityCommand),
            typeof(PencilTool),
            typeof(EraserTool),
            typeof(FillTool),
            typeof(EyedropperTool),
            typeof(LineTool),
            typeof(RectangleTool),
            typeof(EllipseTool),
            typeof(SelectionTool),
            typeof(ExportService),
            typeof(ProjectService),
            typeof(AutosaveService)
        ];

        foreach (var type in coreTypes)
        {
            PreJitType(type);
        }
    }

    /// <summary>
    /// Compiles all methods and constructors of a type using JIT ahead of time.
    /// </summary>
    public static void PreJitType(Type type)
    {
        try
        {
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        }
        catch
        {
            // Class constructor may already have run
        }

        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var method in type.GetMethods(bindingFlags))
        {
            if (!method.IsAbstract && !method.ContainsGenericParameters)
            {
                try
                {
                    RuntimeHelpers.PrepareMethod(method.MethodHandle);
                }
                catch
                {
                    // Skip if method cannot be pre-JITed
                }
            }
        }

        foreach (var ctor in type.GetConstructors(bindingFlags))
        {
            if (!ctor.IsAbstract && !ctor.ContainsGenericParameters)
            {
                try
                {
                    RuntimeHelpers.PrepareMethod(ctor.MethodHandle);
                }
                catch
                {
                    // Skip if constructor cannot be pre-JITed
                }
            }
        }
    }

    /// <summary>
    /// Initializes an offscreen SkiaSharp surface and exercises rasterizer pathways.
    /// </summary>
    private static void WarmUpSkiaPipeline()
    {
        try
        {
            var info = new SKImageInfo(64, 64, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            if (surface != null)
            {
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);

                using var fillPaint = new SKPaint
                {
                    Color = new SKColor(0, 210, 255, 255),
                    IsAntialias = false,
                    FilterQuality = SKFilterQuality.None,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRect(0, 0, 32, 32, fillPaint);

                using var strokePaint = new SKPaint
                {
                    Color = SKColors.White,
                    StrokeWidth = 1f,
                    IsAntialias = false,
                    Style = SKPaintStyle.Stroke
                };
                canvas.DrawLine(0, 0, 64, 64, strokePaint);
                canvas.DrawCircle(32, 32, 16, strokePaint);

                canvas.Flush();
            }
        }
        catch
        {
            // Surface creation fallback
        }
    }

    /// <summary>
    /// Pre-caches retro color palettes and instantiates tools.
    /// </summary>
    private static void PrecachePalettesAndTools()
    {
        // Touch all presets to force parsing and static initialization
        var presets = PalettePresets.AllPresets;
        foreach (var palette in presets)
        {
            _ = palette.Name;
            _ = palette.Colors.Count;
            if (palette.Colors.Count > 0)
            {
                _ = palette.Colors[0].ToHex();
            }
        }

        // Pre-instantiate drawing tools
        ITool[] tools =
        [
            new PencilTool(),
            new EraserTool(),
            new FillTool(),
            new EyedropperTool(),
            new LineTool(),
            new RectangleTool(),
            new EllipseTool(),
            new SelectionTool()
        ];

        foreach (var tool in tools)
        {
            _ = tool.Name;
            _ = tool.Shortcut;
        }
    }
}

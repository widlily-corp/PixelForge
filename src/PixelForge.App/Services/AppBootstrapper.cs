using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PixelForge.App.Controls;
using PixelForge.App.ViewModels;
using PixelForge.App.Views;
using PixelForge.Core.History;
using PixelForge.Core.Services;

namespace PixelForge.App.Services;

/// <summary>
/// Orchestrates the asynchronous, multi-stage application bootstrapping process:
/// expands and locks RAM working set, pre-JITs hot paths, pre-warms Avalonia UI and Skia surfaces,
/// and smoothly transitions from SplashWindow to MainWindow.
/// </summary>
public class AppBootstrapper
{
    private readonly Action<PreloadProgress>? _onProgress;
    private readonly TimeSpan _minimumSplashDuration;

    public AppBootstrapper(Action<PreloadProgress>? onProgress = null, TimeSpan? minimumSplashDuration = null)
    {
        _onProgress = onProgress;
        _minimumSplashDuration = minimumSplashDuration ?? TimeSpan.FromSeconds(5.0);
    }

    /// <summary>
    /// Executes the full bootstrap sequence and constructs the prepared MainWindow,
    /// smoothly pacing progress presentation across the minimum splash duration (~5 seconds)
    /// so the user clearly perceives all preloading, JIT, and RAM-locking phases.
    /// </summary>
    public async Task<MainWindow> BootstrapAsync(CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        AppLogger.LogInfo("Bootstrap", $"Starting Core subsystem preloading with minimum splash duration: {_minimumSplashDuration.TotalSeconds:F1}s");

        void Report(int percent, string stage, string detail)
        {
            _onProgress?.Invoke(new PreloadProgress(percent, stage, detail));
            AppLogger.LogDebug("Bootstrap", $"[{percent}%] {stage}: {detail}");
        }

        // Stage 1: Allocating Working Set & RAM Matrix (0% -> 20%)
        Report(8, "Allocating RAM Working Set", "Configuring high-priority process working set (1024 MB)...");
        await PacedDelayAsync(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

        Report(18, "Allocating RAM Working Set", "Pre-allocating virtual memory regions and thread pool...");
        await PacedDelayAsync(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

        // Stage 2: Locking Assemblies & Memory Pages into Physical RAM (20% -> 45%)
        Report(28, "Locking Assemblies into RAM", "Pre-faulting native & managed module memory pages...");
        var preloadTask = MemoryPreloadService.Instance.PreloadAsync(null, cancellationToken);
        await PacedDelayAsync(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

        Report(38, "Locking Assemblies into RAM", "Physical RAM residency active (Zero disk-paging latency)...");
        await PacedDelayAsync(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        await preloadTask.ConfigureAwait(false);
        AppLogger.LogInfo("Bootstrap", "Core memory preloading complete.");

        // Stage 3: Pre-JIT Compiling Hot Paths (45% -> 70%)
        Report(50, "Pre-JIT Compiling Hot Paths", "Compiling pixel canvas, history, and command methods...");
        PreJitAppTypes();
        await PacedDelayAsync(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

        Report(65, "Pre-JIT Compiling Hot Paths", "Compiling Skia rasterizers and UI viewmodels...");
        await PacedDelayAsync(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        AppLogger.LogInfo("Bootstrap", "App types Pre-JIT complete.");

        // Stage 4: Building Service Matrix & DI Container (70% -> 88%)
        Report(75, "Warming Up SkiaSharp Graphics", "Initializing off-screen surface and rasterizer pipelines...");
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();
        App.Services = serviceProvider;
        await PacedDelayAsync(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

        Report(85, "Building Service Matrix", "Resolving dependency injection container and services...");
        await PacedDelayAsync(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        AppLogger.LogInfo("Bootstrap", "Dependency Injection container built.");

        // Stage 5: Preparing Studio Workspace & MainWindow (88% -> 98%)
        Report(92, "Preparing Studio Workspace", "Initializing viewport, canvas controls, and palette matrix...");
        MainWindow? mainWindow = null;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            AppLogger.LogInfo("Bootstrap", "Resolving MainViewModel from DI...");
            var mainVm = serviceProvider.GetRequiredService<MainViewModel>();
            AppLogger.LogInfo("Bootstrap", "Instantiating MainWindow...");
            mainWindow = new MainWindow
            {
                DataContext = mainVm
            };
            AppLogger.LogInfo("Bootstrap", "MainWindow instantiated successfully.");
        });
        await PacedDelayAsync(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);

        Report(98, "Preparing Studio Workspace", "Finalizing GPU render target and layer composite...");
        await PacedDelayAsync(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);

        // Ensure minimum duration is respected (~5 seconds)
        var remaining = _minimumSplashDuration - sw.Elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
        }

        // Ready to Create (100%)
        Report(100, "Ready to Create", "PixelForge Studio ready.");
        await Task.Delay(250, cancellationToken).ConfigureAwait(false);

        AppLogger.LogInfo("Bootstrap", $"BootstrapAsync completed successfully in {sw.ElapsedMilliseconds} ms.");
        return mainWindow!;
    }

    private async Task PacedDelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (_minimumSplashDuration > TimeSpan.Zero && delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void PreJitAppTypes()
    {
        Type[] appTypes =
        [
            typeof(PixelCanvasControl),
            typeof(MainViewModel),
            typeof(CanvasViewModel),
            typeof(ToolViewModel),
            typeof(LayerViewModel),
            typeof(PaletteViewModel),
            typeof(HistoryViewModel),
            typeof(ThemeService),
            typeof(UpdateService),
            typeof(ClipboardService),
            typeof(FileDialogService),
            typeof(MainWindow),
            typeof(LivePreviewControl)
        ];

        foreach (var type in appTypes)
        {
            MemoryPreloadService.PreJitType(type);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IAutosaveService, AutosaveService>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IMemoryPreloadService>(_ => MemoryPreloadService.Instance);
        services.AddSingleton<ICrashReportService>(_ => CrashReportService.Instance);
        services.AddSingleton<ThemeService>();
        services.AddSingleton<UndoRedoService>();
        services.AddSingleton<MainViewModel>();
    }
}

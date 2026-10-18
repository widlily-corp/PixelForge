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

    public AppBootstrapper(Action<PreloadProgress>? onProgress = null)
    {
        _onProgress = onProgress;
    }

    /// <summary>
    /// Executes the full bootstrap sequence and constructs the prepared MainWindow.
    /// </summary>
    public async Task<MainWindow> BootstrapAsync(CancellationToken cancellationToken = default)
    {
        AppLogger.LogInfo("Bootstrap", "Starting Core subsystem preloading...");
        var progressReporter = new Progress<PreloadProgress>(p =>
        {
            _onProgress?.Invoke(p);
            AppLogger.LogDebug("Bootstrap", $"[{p.Percentage}%] {p.StageName}: {p.Details}");
        });

        // 1. Run Core memory preloading (Working set expansion, RAM locking, Core pre-JIT, Skia warm-up)
        await MemoryPreloadService.Instance.PreloadAsync(progressReporter, cancellationToken).ConfigureAwait(false);
        AppLogger.LogInfo("Bootstrap", "Core memory preloading complete.");

        // 2. Pre-JIT App layer ViewModels and Controls
        AppLogger.LogInfo("Bootstrap", "Starting Pre-JIT compilation of App types...");
        _onProgress?.Invoke(new PreloadProgress(92, "Pre-compiling UI & Canvas Shaders", "Pre-JITing PixelCanvasControl and ViewModels..."));
        cancellationToken.ThrowIfCancellationRequested();
        PreJitAppTypes();
        AppLogger.LogInfo("Bootstrap", "App types Pre-JIT complete.");

        // 3. Configure DI container
        AppLogger.LogInfo("Bootstrap", "Configuring Dependency Injection container...");
        _onProgress?.Invoke(new PreloadProgress(96, "Building Service Matrix", "Resolving dependency injection container..."));
        cancellationToken.ThrowIfCancellationRequested();
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();
        App.Services = serviceProvider;
        AppLogger.LogInfo("Bootstrap", "Dependency Injection container built.");

        // 4. Instantiate MainWindow and ViewModel on UI thread
        AppLogger.LogInfo("Bootstrap", "Instantiating MainWindow and MainViewModel on UI thread...");
        _onProgress?.Invoke(new PreloadProgress(98, "Preparing Studio Workspace", "Initializing viewport and controls..."));
        cancellationToken.ThrowIfCancellationRequested();

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

        // Small pause for visual smoothness so user sees 100% complete
        _onProgress?.Invoke(new PreloadProgress(100, "Ready to Create", "PixelForge Studio ready."));
        await Task.Delay(150, cancellationToken).ConfigureAwait(false);

        AppLogger.LogInfo("Bootstrap", "BootstrapAsync completed successfully.");
        return mainWindow!;
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
        services.AddSingleton<ThemeService>();
        services.AddSingleton<UndoRedoService>();
        services.AddSingleton<MainViewModel>();
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PixelForge.App.Services;
using PixelForge.App.Views;

namespace PixelForge.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; internal set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppLogger.LogInfo("App", "OnFrameworkInitializationCompleted started.");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Prevent immediate app exit while transitioning from Splash to MainWindow
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Detect command-line argument for file opening (e.g. double-click in Windows Explorer)
            string? initialFilePath = null;
            if (desktop.Args != null && desktop.Args.Length > 0)
            {
                var candidate = desktop.Args[0]?.Trim('\"');
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    initialFilePath = Path.GetFullPath(candidate);
                    AppLogger.LogInfo("App", $"Received startup file argument: {initialFilePath}");
                }
            }

            AppLogger.LogInfo("App", "Displaying SplashWindow...");
            var splash = new SplashWindow();
            splash.Show();

            // Run real multi-stage subsystem preloading and bootstrapping
            _ = Task.Run(async () =>
            {
                try
                {
                    AppLogger.LogInfo("Bootstrapper", "Initializing AppBootstrapper...");
                    var bootstrapper = new AppBootstrapper(progress => splash.UpdateProgress(progress));
                    var mainWindow = await bootstrapper.BootstrapAsync().ConfigureAwait(false);
                    AppLogger.LogInfo("Bootstrapper", "Bootstrap completed successfully. Scheduling UI transition...");

                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        try
                        {
                            AppLogger.LogInfo("UI", "Assigning MainWindow to desktop lifetime...");
                            desktop.MainWindow = mainWindow;
                            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

                            AppLogger.LogInfo("UI", "Calling MainWindow.Show()...");
                            mainWindow.Show();

                            AppLogger.LogInfo("UI", "Closing SplashWindow...");
                            splash.Close();
                            AppLogger.LogInfo("UI", "Workspace ready and active.");

                            // If a file argument was passed from Windows Explorer, open it now
                            if (!string.IsNullOrEmpty(initialFilePath) && mainWindow.DataContext is ViewModels.MainViewModel mainVm)
                            {
                                await mainVm.OpenProjectFromFileAsync(initialFilePath);
                            }

                            // Silently register .pxproj Windows file association in background
                            _ = Task.Run(() =>
                            {
                                try
                                {
                                    FileAssociationService.RegisterAssociation();
                                }
                                catch
                                {
                                    // Suppress on non-Windows/restricted
                                }
                            });
                        }
                        catch (Exception uiEx)
                        {
                            AppLogger.LogFatal("UI", "Critical exception during MainWindow display or Splash close!", uiEx);
                            throw;
                        }
                    });
                }
                catch (Exception ex)
                {
                    AppLogger.LogFatal("Bootstrapper", "Critical bootstrap failure encountered!", ex);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            splash.Close();
                        }
                        catch
                        {
                            // Ignore secondary errors on close
                        }
                        desktop.Shutdown(-1);
                    });
                }
            });
        }

        base.OnFrameworkInitializationCompleted();
    }
}

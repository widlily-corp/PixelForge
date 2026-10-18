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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Prevent immediate app exit while transitioning from Splash to MainWindow
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var splash = new SplashWindow();
            splash.Show();

            // Run real multi-stage subsystem preloading and bootstrapping
            _ = Task.Run(async () =>
            {
                try
                {
                    var bootstrapper = new AppBootstrapper(progress => splash.UpdateProgress(progress));
                    var mainWindow = await bootstrapper.BootstrapAsync().ConfigureAwait(false);

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        desktop.MainWindow = mainWindow;
                        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                        mainWindow.Show();
                        splash.Close();
                    });
                }
                catch (Exception ex)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Console.WriteLine($"Critical bootstrap failure: {ex}");
                        splash.Close();
                        desktop.Shutdown(-1);
                    });
                }
            });
        }

        base.OnFrameworkInitializationCompleted();
    }
}

using Avalonia;
using PixelForge.App.Services;

namespace PixelForge.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppLogger.Initialize();
        AppLogger.LogInfo("Lifecycle", "PixelForge application process started.");

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppLogger.LogFatal("Lifecycle", "Unhandled exception in application lifetime!", ex);
            throw;
        }
        finally
        {
            AppLogger.LogInfo("Lifecycle", "PixelForge application process terminating.");
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseSkia();
}

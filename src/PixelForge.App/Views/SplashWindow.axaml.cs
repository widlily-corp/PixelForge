using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PixelForge.Core.Localization;
using PixelForge.Core.Services;

namespace PixelForge.App.Views;

/// <summary>
/// Professional high-performance splash window that displays real-time subsystem
/// initialization, memory preloading, and component preheating progress with hero pixel art.
/// </summary>
public partial class SplashWindow : Window
{
    private static readonly IBrush BrushGreen = new SolidColorBrush(Color.Parse("#10B981"));
    private static readonly IBrush BrushCyan = new SolidColorBrush(Color.Parse("#00D2FF"));

    public SplashWindow()
    {
        InitializeComponent();
        if (VersionBadgeText != null)
        {
            VersionBadgeText.Text = PixelForge.Core.AppVersionInfo.FullVersionString;
        }

        var loc = LocalizationService.Instance;
        if (SubtitleText != null) SubtitleText.Text = loc.Get("App_Subtitle");
        if (TelemetryRamText != null) TelemetryRamText.Text = loc.Get("Splash_TelemetryRam");
        if (TelemetryJitText != null) TelemetryJitText.Text = loc.Get("Splash_TelemetryJit");
        if (TelemetrySkiaText != null) TelemetrySkiaText.Text = loc.Get("Splash_TelemetrySkia");
        if (TelemetryUndoText != null) TelemetryUndoText.Text = loc.Get("Splash_TelemetryUndo");
        if (SystemTagText != null) SystemTagText.Text = loc.Get("Splash_SystemTag");
    }

    /// <summary>
    /// Thread-safe update of the progress bar and subsystem detail messages with dynamic telemetry module indicators.
    /// </summary>
    public void UpdateProgress(PreloadProgress progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ProgressBar != null)
            {
                ProgressBar.Value = progress.Percentage;
            }

            if (StatusText != null)
            {
                StatusText.Text = progress.StageName;
            }

            if (DetailText != null)
            {
                DetailText.Text = progress.Details;
            }

            if (PercentText != null)
            {
                PercentText.Text = $"{progress.Percentage}%";
            }

            // Dynamically illuminate telemetry modules as initialization stages advance
            if (progress.Percentage >= 20 && DotRam != null)
            {
                DotRam.Foreground = BrushGreen;
            }

            if (progress.Percentage >= 45 && DotJit != null)
            {
                DotJit.Foreground = BrushGreen;
            }

            if (progress.Percentage >= 75 && DotSkia != null)
            {
                DotSkia.Foreground = BrushCyan;
            }

            if (progress.Percentage >= 90 && DotUndo != null)
            {
                DotUndo.Foreground = BrushCyan;
            }
        });
    }
}

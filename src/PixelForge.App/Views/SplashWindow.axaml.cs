using Avalonia.Controls;
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
    public SplashWindow()
    {
        InitializeComponent();
        if (VersionBadgeText != null)
        {
            VersionBadgeText.Text = PixelForge.Core.AppVersionInfo.FullVersionString;
        }

        var loc = LocalizationService.Instance;
        if (SubtitleText != null) SubtitleText.Text = loc.Get("App_Subtitle");
        if (Feature1Text != null) Feature1Text.Text = loc.Get("Splash_Feature1");
        if (Feature2Text != null) Feature2Text.Text = loc.Get("Splash_Feature2");
        if (Feature3Text != null) Feature3Text.Text = loc.Get("Splash_Feature3");
    }

    /// <summary>
    /// Thread-safe update of the progress bar and subsystem detail messages.
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
        });
    }
}

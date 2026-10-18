using Avalonia.Controls;
using Avalonia.Threading;
using PixelForge.Core.Services;

namespace PixelForge.App.Views;

/// <summary>
/// Professional high-performance splash window that displays real-time subsystem
/// initialization, memory preloading, and component preheating progress.
/// </summary>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
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

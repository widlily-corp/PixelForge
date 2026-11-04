using System.ComponentModel;
using PixelForge.App.ViewModels;
using PixelForge.Core.Models;

namespace PixelForge.App.Services;

/// <summary>
/// Defines the contract for high-precision timeline and frame playback management.
/// </summary>
public interface IPlaybackService : INotifyPropertyChanged
{
    /// <summary>
    /// Gets whether animation playback is currently actively ticking.
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// Gets or sets whether playback automatically loops when reaching the end of the timeline or tag.
    /// </summary>
    bool IsLooping { get; set; }

    /// <summary>
    /// Gets or sets the playback speed multiplier (e.g. 0.25x, 0.5x, 1x, 2x, 4x).
    /// </summary>
    double SpeedMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the currently active tag restricting the frame playback range, or null for the full timeline.
    /// </summary>
    AnimationTag? ActiveTag { get; set; }

    /// <summary>
    /// Gets or sets the target canvas being played.
    /// </summary>
    PixelCanvas? Canvas { get; set; }

    /// <summary>
    /// Gets or sets the associated CanvasViewModel.
    /// </summary>
    CanvasViewModel? CanvasViewModel { get; set; }

    /// <summary>
    /// Event raised when the playback state transitions between playing and paused.
    /// </summary>
    event EventHandler? PlaybackStateChanged;

    /// <summary>
    /// Event raised whenever the active frame changes due to playback or manual stepping.
    /// </summary>
    event EventHandler<int>? FrameChanged;

    /// <summary>
    /// Attaches to a <see cref="CanvasViewModel"/>, observing canvas replacements and playback stop requests.
    /// </summary>
    void Attach(CanvasViewModel canvasViewModel);

    /// <summary>
    /// Attaches directly to a <see cref="PixelCanvas"/>.
    /// </summary>
    void Attach(PixelCanvas canvas);

    /// <summary>
    /// Detaches from the current canvas and viewmodel, stopping playback.
    /// </summary>
    void Detach();

    /// <summary>
    /// Starts animation playback.
    /// </summary>
    void Play();

    /// <summary>
    /// Pauses animation playback.
    /// </summary>
    void Pause();

    /// <summary>
    /// Toggles playback between playing and paused.
    /// </summary>
    void TogglePlay();

    /// <summary>
    /// Manually steps one frame forward according to loop and tag constraints.
    /// </summary>
    void StepForward();

    /// <summary>
    /// Manually steps one frame backward according to loop and tag constraints.
    /// </summary>
    void StepBackward();

    /// <summary>
    /// Resets the ping-pong playback direction to forward.
    /// </summary>
    void ResetPingPong();
}

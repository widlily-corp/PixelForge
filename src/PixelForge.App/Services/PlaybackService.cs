using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PixelForge.App.ViewModels;
using PixelForge.Core.Models;

namespace PixelForge.App.Services;

/// <summary>
/// High-precision animation playback controller driving timeline frame advancement,
/// dynamic frame duration intervals, loop constraints, and tag-bound modes (Forward, Reverse, PingPong).
/// </summary>
public partial class PlaybackService : ObservableObject, IPlaybackService, IDisposable
{
    private readonly DispatcherTimer _timer;
    private PixelCanvas? _canvas;
    private CanvasViewModel? _canvasViewModel;
    private bool _pingPongReversing;
    private int _repeatCycle;
    private bool _disposed;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isLooping = true;

    [ObservableProperty]
    private double _speedMultiplier = 1.0;

    [ObservableProperty]
    private AnimationTag? _activeTag;

    public PixelCanvas? Canvas
    {
        get => _canvas;
        set
        {
            if (_canvas != value)
            {
                if (_canvas != null)
                {
                    _canvas.FrameChanged -= OnCanvasFrameChanged;
                }

                _canvas = value;

                if (_canvas != null)
                {
                    _canvas.FrameChanged += OnCanvasFrameChanged;
                }

                OnPropertyChanged(nameof(Canvas));

                if (_canvas == null && IsPlaying)
                {
                    Pause();
                }
            }
        }
    }

    public CanvasViewModel? CanvasViewModel
    {
        get => _canvasViewModel;
        set
        {
            if (_canvasViewModel != value)
            {
                if (_canvasViewModel != null)
                {
                    _canvasViewModel.PlaybackStopRequested -= OnPlaybackStopRequested;
                    _canvasViewModel.PropertyChanged -= OnCanvasViewModelPropertyChanged;
                }

                _canvasViewModel = value;

                if (_canvasViewModel != null)
                {
                    _canvasViewModel.PlaybackStopRequested += OnPlaybackStopRequested;
                    _canvasViewModel.PropertyChanged += OnCanvasViewModelPropertyChanged;
                    Canvas = _canvasViewModel.Canvas;
                    _canvasViewModel.IsPlaying = IsPlaying;
                }

                OnPropertyChanged(nameof(CanvasViewModel));
            }
        }
    }

    public event EventHandler? PlaybackStateChanged;
    public event EventHandler<int>? FrameChanged;

    public PlaybackService()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += OnTimerTick;
    }

    partial void OnSpeedMultiplierChanged(double value)
    {
        if (IsPlaying && Canvas?.CurrentFrame != null)
        {
            _timer.Interval = CalculateInterval(Canvas.CurrentFrame.DurationMs, value);
        }
    }

    partial void OnActiveTagChanged(AnimationTag? value)
    {
        ResetPingPong();
        _repeatCycle = 0;
    }

    partial void OnIsPlayingChanged(bool value)
    {
        if (CanvasViewModel != null && CanvasViewModel.IsPlaying != value)
        {
            CanvasViewModel.IsPlaying = value;
        }

        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Attach(CanvasViewModel canvasViewModel)
    {
        ArgumentNullException.ThrowIfNull(canvasViewModel);
        CanvasViewModel = canvasViewModel;
    }

    public void Attach(PixelCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        Canvas = canvas;
    }

    public void Detach()
    {
        Pause();
        CanvasViewModel = null;
        Canvas = null;
    }

    public void Play()
    {
        if (IsPlaying || Canvas == null)
        {
            return;
        }

        if (Canvas.Frames.Count <= 1 && !IsLooping)
        {
            return;
        }

        _repeatCycle = 0;
        ResetPingPong();

        // If active tag set, ensure current frame is inside tag range before starting
        if (ActiveTag != null && Canvas.Frames.Count > 0)
        {
            int minFrame = Math.Clamp(Math.Min(ActiveTag.FromFrame, ActiveTag.ToFrame), 0, Canvas.Frames.Count - 1);
            int maxFrame = Math.Clamp(Math.Max(ActiveTag.FromFrame, ActiveTag.ToFrame), 0, Canvas.Frames.Count - 1);

            if (Canvas.CurrentFrameIndex < minFrame || Canvas.CurrentFrameIndex > maxFrame)
            {
                int startFrame = ActiveTag.LoopMode == AnimationLoopMode.Reverse ? maxFrame : minFrame;
                Canvas.SwitchToFrame(startFrame);
            }
        }

        int duration = Canvas.CurrentFrame?.DurationMs ?? 100;
        _timer.Interval = CalculateInterval(duration, SpeedMultiplier);
        _timer.Start();

        IsPlaying = true;
    }

    public void Pause()
    {
        if (!IsPlaying)
        {
            return;
        }

        _timer.Stop();
        IsPlaying = false;
    }

    public void TogglePlay()
    {
        if (IsPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    public void StepForward()
    {
        if (Canvas == null || Canvas.Frames.Count == 0)
        {
            return;
        }

        int nextIdx = CalculateNextFrameIndex(
            Canvas.CurrentFrameIndex,
            Canvas.Frames.Count,
            ActiveTag,
            IsLooping,
            ref _pingPongReversing,
            ref _repeatCycle,
            out bool shouldStop);

        Canvas.SwitchToFrame(nextIdx);

        if (shouldStop && IsPlaying)
        {
            Pause();
        }
        else if (IsPlaying && Canvas.CurrentFrame != null)
        {
            _timer.Interval = CalculateInterval(Canvas.CurrentFrame.DurationMs, SpeedMultiplier);
        }
    }

    public void StepBackward()
    {
        if (Canvas == null || Canvas.Frames.Count == 0)
        {
            return;
        }

        int prevIdx;
        int count = Canvas.Frames.Count;

        if (ActiveTag != null)
        {
            int minFrame = Math.Clamp(Math.Min(ActiveTag.FromFrame, ActiveTag.ToFrame), 0, count - 1);
            int maxFrame = Math.Clamp(Math.Max(ActiveTag.FromFrame, ActiveTag.ToFrame), 0, count - 1);

            if (Canvas.CurrentFrameIndex > minFrame && Canvas.CurrentFrameIndex <= maxFrame)
            {
                prevIdx = Canvas.CurrentFrameIndex - 1;
            }
            else if (IsLooping)
            {
                prevIdx = maxFrame;
            }
            else
            {
                prevIdx = minFrame;
            }
        }
        else
        {
            if (Canvas.CurrentFrameIndex > 0)
            {
                prevIdx = Canvas.CurrentFrameIndex - 1;
            }
            else if (IsLooping)
            {
                prevIdx = count - 1;
            }
            else
            {
                prevIdx = 0;
            }
        }

        Canvas.SwitchToFrame(prevIdx);

        if (IsPlaying && Canvas.CurrentFrame != null)
        {
            _timer.Interval = CalculateInterval(Canvas.CurrentFrame.DurationMs, SpeedMultiplier);
        }
    }

    public void ResetPingPong()
    {
        _pingPongReversing = ActiveTag?.LoopMode == AnimationLoopMode.Reverse;
    }

    /// <summary>
    /// Executes a single discrete tick of the playback engine. Used internally by the timer and for deterministic testing.
    /// </summary>
    public void Tick()
    {
        if (!IsPlaying || Canvas == null || Canvas.Frames.Count == 0)
        {
            Pause();
            return;
        }

        int nextIdx = CalculateNextFrameIndex(
            Canvas.CurrentFrameIndex,
            Canvas.Frames.Count,
            ActiveTag,
            IsLooping,
            ref _pingPongReversing,
            ref _repeatCycle,
            out bool shouldStop);

        Canvas.SwitchToFrame(nextIdx);

        if (shouldStop)
        {
            Pause();
            return;
        }

        int newDuration = Canvas.CurrentFrame?.DurationMs ?? 100;
        _timer.Interval = CalculateInterval(newDuration, SpeedMultiplier);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        Tick();
    }

    private void OnPlaybackStopRequested()
    {
        Pause();
    }

    private void OnCanvasViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CanvasViewModel.Canvas))
        {
            Canvas = CanvasViewModel?.Canvas;
        }
        else if (e.PropertyName == nameof(CanvasViewModel.IsPlaying))
        {
            if (CanvasViewModel != null && CanvasViewModel.IsPlaying != IsPlaying)
            {
                if (CanvasViewModel.IsPlaying)
                {
                    Play();
                }
                else
                {
                    Pause();
                }
            }
        }
    }

    private void OnCanvasFrameChanged(int frameIndex)
    {
        FrameChanged?.Invoke(this, frameIndex);
    }

    /// <summary>
    /// Calculates dynamic timer interval based on individual frame duration and speed multiplier.
    /// </summary>
    public static TimeSpan CalculateInterval(int durationMs, double speedMultiplier)
    {
        if (durationMs <= 0)
        {
            durationMs = 100;
        }
        if (speedMultiplier <= 0.001)
        {
            speedMultiplier = 1.0;
        }

        double effectiveMs = durationMs / speedMultiplier;
        int clampedMs = Math.Max(10, (int)Math.Round(effectiveMs));
        return TimeSpan.FromMilliseconds(clampedMs);
    }

    /// <summary>
    /// Computes the subsequent frame index respecting timeline length, active tag bounds, loop modes, and repeat limits.
    /// </summary>
    public static int CalculateNextFrameIndex(
        int currentIndex,
        int frameCount,
        AnimationTag? activeTag,
        bool isLooping,
        ref bool pingPongReverse,
        ref int repeatCycle,
        out bool shouldStop)
    {
        shouldStop = false;

        if (frameCount <= 1)
        {
            shouldStop = !isLooping;
            return 0;
        }

        if (activeTag == null)
        {
            int minFrame = 0;
            int maxFrame = frameCount - 1;

            if (currentIndex < minFrame || currentIndex > maxFrame)
            {
                return minFrame;
            }

            if (currentIndex < maxFrame)
            {
                return currentIndex + 1;
            }

            // At maxFrame
            repeatCycle++;
            if (isLooping)
            {
                return minFrame;
            }

            shouldStop = true;
            return maxFrame;
        }

        // Active tag constrained playback
        int tagMin = Math.Clamp(Math.Min(activeTag.FromFrame, activeTag.ToFrame), 0, frameCount - 1);
        int tagMax = Math.Clamp(Math.Max(activeTag.FromFrame, activeTag.ToFrame), 0, frameCount - 1);

        if (tagMin == tagMax)
        {
            shouldStop = !isLooping;
            return tagMin;
        }

        switch (activeTag.LoopMode)
        {
            case AnimationLoopMode.Reverse:
            {
                if (currentIndex < tagMin || currentIndex > tagMax)
                {
                    return tagMax;
                }

                if (currentIndex > tagMin)
                {
                    return currentIndex - 1;
                }

                // At tagMin
                repeatCycle++;
                bool limitReached = activeTag.RepeatCount > 0 && repeatCycle >= activeTag.RepeatCount;
                if (!isLooping || limitReached)
                {
                    shouldStop = true;
                    return tagMin;
                }

                return tagMax;
            }

            case AnimationLoopMode.PingPong:
            {
                if (currentIndex < tagMin || currentIndex > tagMax)
                {
                    pingPongReverse = false;
                    return tagMin;
                }

                if (!pingPongReverse)
                {
                    // Moving Forward
                    if (currentIndex < tagMax)
                    {
                        int next = currentIndex + 1;
                        if (next >= tagMax)
                        {
                            pingPongReverse = true;
                        }
                        return next;
                    }
                    else
                    {
                        pingPongReverse = true;
                        return Math.Max(tagMin, tagMax - 1);
                    }
                }
                else
                {
                    // Moving Backward
                    if (currentIndex > tagMin)
                    {
                        int next = currentIndex - 1;
                        if (next <= tagMin)
                        {
                            pingPongReverse = false;
                            repeatCycle++;
                            bool limitReached = activeTag.RepeatCount > 0 && repeatCycle >= activeTag.RepeatCount;
                            if (!isLooping || limitReached)
                            {
                                shouldStop = true;
                            }
                        }
                        return next;
                    }
                    else
                    {
                        pingPongReverse = false;
                        repeatCycle++;
                        bool limitReached = activeTag.RepeatCount > 0 && repeatCycle >= activeTag.RepeatCount;
                        if (!isLooping || limitReached)
                        {
                            shouldStop = true;
                            return tagMin;
                        }

                        return Math.Min(tagMax, tagMin + 1);
                    }
                }
            }

            case AnimationLoopMode.Forward:
            default:
            {
                if (currentIndex < tagMin || currentIndex > tagMax)
                {
                    return tagMin;
                }

                if (currentIndex < tagMax)
                {
                    return currentIndex + 1;
                }

                // At tagMax
                repeatCycle++;
                bool limitReached = activeTag.RepeatCount > 0 && repeatCycle >= activeTag.RepeatCount;
                if (!isLooping || limitReached)
                {
                    shouldStop = true;
                    return tagMax;
                }

                return tagMin;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Pause();
        _timer.Tick -= OnTimerTick;

        if (_canvas != null)
        {
            _canvas.FrameChanged -= OnCanvasFrameChanged;
            _canvas = null;
        }

        if (_canvasViewModel != null)
        {
            _canvasViewModel.PlaybackStopRequested -= OnPlaybackStopRequested;
            _canvasViewModel.PropertyChanged -= OnCanvasViewModelPropertyChanged;
            _canvasViewModel = null;
        }

        GC.SuppressFinalize(this);
    }
}

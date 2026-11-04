using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelForge.App.Services;
using PixelForge.Core.Models;

namespace PixelForge.App.ViewModels;

/// <summary>
/// ViewModel managing independent real-time concurrent animation preview playback and viewport scaling.
/// Operates its own frame ticker so animation loops without interfering with the active frame on the main canvas.
/// </summary>
public partial class AnimationPreviewViewModel : ViewModelBase, IDisposable
{
    private readonly DispatcherTimer _ticker;
    private PixelCanvas? _canvas;
    private CanvasViewModel? _canvasViewModel;
    private bool _pingPongReversing;
    private int _repeatCycle;
    private bool _disposed;

    [ObservableProperty]
    private int _currentPreviewFrameIndex;

    [ObservableProperty]
    private int _scale = 2; // Default 2x scale for retro pixel visibility

    [ObservableProperty]
    private bool _isPlaying = true; // Auto-play by default in preview window

    [ObservableProperty]
    private double _speedMultiplier = 1.0;

    [ObservableProperty]
    private bool _isLooping = true;

    [ObservableProperty]
    private AnimationTag? _activeTag;

    [ObservableProperty]
    private string _frameCounterText = "1 / 1 (100ms)";

    public PixelCanvas? Canvas
    {
        get => _canvas;
        set
        {
            if (_canvas != value)
            {
                UnhookCanvasEvents(_canvas);
                _canvas = value;
                HookCanvasEvents(_canvas);

                OnPropertyChanged(nameof(Canvas));
                OnPropertyChanged(nameof(FrameCount));
                OnPropertyChanged(nameof(CanvasWidth));
                OnPropertyChanged(nameof(CanvasHeight));
                OnPropertyChanged(nameof(DisplayWidth));
                OnPropertyChanged(nameof(DisplayHeight));

                CurrentPreviewFrameIndex = Math.Clamp(CurrentPreviewFrameIndex, 0, Math.Max(0, FrameCount - 1));
                UpdateFrameCounterText();
                UpdateTimerInterval();
                RequestRender();
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
                    _canvasViewModel.PropertyChanged -= OnCanvasViewModelPropertyChanged;
                }

                _canvasViewModel = value;

                if (_canvasViewModel != null)
                {
                    _canvasViewModel.PropertyChanged += OnCanvasViewModelPropertyChanged;
                    Canvas = _canvasViewModel.Canvas;
                }

                OnPropertyChanged(nameof(CanvasViewModel));
            }
        }
    }

    public int FrameCount => Canvas?.Frames.Count ?? 0;
    public int CanvasWidth => Canvas?.Width ?? 32;
    public int CanvasHeight => Canvas?.Height ?? 32;
    public double DisplayWidth => CanvasWidth * Scale;
    public double DisplayHeight => CanvasHeight * Scale;

    public bool IsScale1 => Scale == 1;
    public bool IsScale2 => Scale == 2;
    public bool IsScale3 => Scale == 3;
    public bool IsScale4 => Scale == 4;
    public bool IsScale8 => Scale == 8;

    public bool IsSpeed025 => Math.Abs(SpeedMultiplier - 0.25) < 0.01;
    public bool IsSpeed05 => Math.Abs(SpeedMultiplier - 0.5) < 0.01;
    public bool IsSpeed1 => Math.Abs(SpeedMultiplier - 1.0) < 0.01;
    public bool IsSpeed2 => Math.Abs(SpeedMultiplier - 2.0) < 0.01;
    public bool IsSpeed4 => Math.Abs(SpeedMultiplier - 4.0) < 0.01;

    public int CurrentFrameDurationMs =>
        (Canvas != null && (uint)CurrentPreviewFrameIndex < (uint)Canvas.Frames.Count)
            ? Canvas.Frames[CurrentPreviewFrameIndex].DurationMs
            : 100;

    /// <summary>
    /// Event raised when a new preview frame needs to be composited and presented in the view.
    /// </summary>
    public event Action? RenderRequested;

    public AnimationPreviewViewModel(PixelCanvas? canvas = null)
    {
        _ticker = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _ticker.Tick += OnTickerTick;

        if (canvas != null)
        {
            Canvas = canvas;
        }

        if (IsPlaying)
        {
            _ticker.Start();
        }
    }

    public AnimationPreviewViewModel(CanvasViewModel canvasViewModel) : this(canvasViewModel?.Canvas)
    {
        CanvasViewModel = canvasViewModel;
    }

    partial void OnCurrentPreviewFrameIndexChanged(int value)
    {
        UpdateFrameCounterText();
        UpdateTimerInterval();
        RequestRender();
    }

    partial void OnScaleChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayWidth));
        OnPropertyChanged(nameof(DisplayHeight));
        OnPropertyChanged(nameof(IsScale1));
        OnPropertyChanged(nameof(IsScale2));
        OnPropertyChanged(nameof(IsScale3));
        OnPropertyChanged(nameof(IsScale4));
        OnPropertyChanged(nameof(IsScale8));
        RequestRender();
    }

    partial void OnSpeedMultiplierChanged(double value)
    {
        OnPropertyChanged(nameof(IsSpeed025));
        OnPropertyChanged(nameof(IsSpeed05));
        OnPropertyChanged(nameof(IsSpeed1));
        OnPropertyChanged(nameof(IsSpeed2));
        OnPropertyChanged(nameof(IsSpeed4));
        UpdateTimerInterval();
    }

    partial void OnIsPlayingChanged(bool value)
    {
        if (value)
        {
            UpdateTimerInterval();
            _ticker.Start();
        }
        else
        {
            _ticker.Stop();
        }
    }

    partial void OnActiveTagChanged(AnimationTag? value)
    {
        _pingPongReversing = value?.LoopMode == AnimationLoopMode.Reverse;
        _repeatCycle = 0;

        if (value != null && Canvas != null && Canvas.Frames.Count > 0)
        {
            int minFrame = Math.Clamp(Math.Min(value.FromFrame, value.ToFrame), 0, Canvas.Frames.Count - 1);
            int maxFrame = Math.Clamp(Math.Max(value.FromFrame, value.ToFrame), 0, Canvas.Frames.Count - 1);
            if (CurrentPreviewFrameIndex < minFrame || CurrentPreviewFrameIndex > maxFrame)
            {
                CurrentPreviewFrameIndex = value.LoopMode == AnimationLoopMode.Reverse ? maxFrame : minFrame;
            }
        }
    }

    [RelayCommand]
    public void TogglePlay()
    {
        IsPlaying = !IsPlaying;
    }

    [RelayCommand]
    public void StepForward()
    {
        if (Canvas == null || Canvas.Frames.Count == 0)
        {
            return;
        }

        int nextIdx = PlaybackService.CalculateNextFrameIndex(
            CurrentPreviewFrameIndex,
            Canvas.Frames.Count,
            ActiveTag,
            IsLooping,
            ref _pingPongReversing,
            ref _repeatCycle,
            out bool shouldStop);

        CurrentPreviewFrameIndex = nextIdx;

        if (shouldStop && IsPlaying)
        {
            IsPlaying = false;
        }
    }

    [RelayCommand]
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

            if (CurrentPreviewFrameIndex > minFrame && CurrentPreviewFrameIndex <= maxFrame)
            {
                prevIdx = CurrentPreviewFrameIndex - 1;
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
            if (CurrentPreviewFrameIndex > 0)
            {
                prevIdx = CurrentPreviewFrameIndex - 1;
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

        CurrentPreviewFrameIndex = prevIdx;
    }

    [RelayCommand]
    public void SetScale(int scale)
    {
        Scale = scale switch
        {
            <= 1 => 1,
            2 => 2,
            3 => 3,
            4 => 4,
            _ => 8
        };
    }

    [RelayCommand]
    public void SetSpeed(double speed)
    {
        SpeedMultiplier = speed <= 0.001 ? 1.0 : speed;
    }

    public void RequestRender()
    {
        RenderRequested?.Invoke();
    }

    private void OnTickerTick(object? sender, EventArgs e)
    {
        if (!IsPlaying || Canvas == null || Canvas.Frames.Count == 0)
        {
            return;
        }

        int nextIdx = PlaybackService.CalculateNextFrameIndex(
            CurrentPreviewFrameIndex,
            Canvas.Frames.Count,
            ActiveTag,
            IsLooping,
            ref _pingPongReversing,
            ref _repeatCycle,
            out bool shouldStop);

        CurrentPreviewFrameIndex = nextIdx;

        if (shouldStop)
        {
            IsPlaying = false;
        }
    }

    private void UpdateTimerInterval()
    {
        int duration = CurrentFrameDurationMs;
        _ticker.Interval = PlaybackService.CalculateInterval(duration, SpeedMultiplier);
    }

    private void UpdateFrameCounterText()
    {
        int total = Math.Max(1, FrameCount);
        int current = Math.Clamp(CurrentPreviewFrameIndex + 1, 1, total);
        FrameCounterText = $"{current} / {total} ({CurrentFrameDurationMs}ms)";
    }

    private void HookCanvasEvents(PixelCanvas? canvas)
    {
        if (canvas == null)
        {
            return;
        }

        canvas.PixelsChanged += OnCanvasPixelsChanged;
        canvas.Frames.CollectionChanged += OnFramesCollectionChanged;
    }

    private void UnhookCanvasEvents(PixelCanvas? canvas)
    {
        if (canvas == null)
        {
            return;
        }

        canvas.PixelsChanged -= OnCanvasPixelsChanged;
        canvas.Frames.CollectionChanged -= OnFramesCollectionChanged;
    }

    private void OnCanvasPixelsChanged()
    {
        // When user draws on canvas, refresh preview immediately
        RequestRender();
    }

    private void OnFramesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        int count = Canvas?.Frames.Count ?? 0;
        if (count > 0 && CurrentPreviewFrameIndex >= count)
        {
            CurrentPreviewFrameIndex = count - 1;
        }

        OnPropertyChanged(nameof(FrameCount));
        UpdateFrameCounterText();
        UpdateTimerInterval();
        RequestRender();
    }

    private void OnCanvasViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CanvasViewModel.Canvas))
        {
            Canvas = CanvasViewModel?.Canvas;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        IsPlaying = false;
        _ticker.Stop();
        _ticker.Tick -= OnTickerTick;

        UnhookCanvasEvents(_canvas);
        _canvas = null;

        if (_canvasViewModel != null)
        {
            _canvasViewModel.PropertyChanged -= OnCanvasViewModelPropertyChanged;
            _canvasViewModel = null;
        }

        GC.SuppressFinalize(this);
    }
}

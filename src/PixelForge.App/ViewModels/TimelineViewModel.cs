using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelForge.App.Services;
using PixelForge.App.Views;
using PixelForge.Core.History;
using PixelForge.Core.Localization;
using PixelForge.Core.Models;
using PixelForge.Core.Services;

namespace PixelForge.App.ViewModels;

/// <summary>
/// ViewModel coordinating the bottom timeline dock, transport playback controls,
/// frame navigation and manipulation, cel selection, onion skinning, and preview window.
/// </summary>
public partial class TimelineViewModel : ViewModelBase
{
    private readonly CanvasViewModel _canvasViewModel;
    private PixelCanvas _canvas;
    private readonly IPlaybackService _playbackService;
    private readonly UndoRedoService _undoRedoService;
    private readonly IExportService? _exportService;
    private readonly IFileDialogService? _fileDialogService;
    private readonly LocalizationService _loc = LocalizationService.Instance;

    private bool _isCollapsed = false;
    private double _timelineHeight = 160.0;
    private AnimationPreviewWindow? _previewWindow;

    public CanvasViewModel CanvasViewModel => _canvasViewModel;
    public PixelCanvas Canvas => _canvas;
    public IPlaybackService PlaybackService => _playbackService;
    public UndoRedoService UndoRedoService => _undoRedoService;

    public ObservableCollection<AnimationFrame> Frames => Canvas.Frames;
    public ObservableCollection<AnimationTag> Tags => Canvas.Tags;
    public ObservableCollection<Layer> Layers => Canvas.Layers;

    public event EventHandler<string>? StatusRequested;

    public int CurrentFrameIndex
    {
        get => Canvas.CurrentFrameIndex;
        set
        {
            if (Canvas.CurrentFrameIndex != value)
            {
                Canvas.SwitchToFrame(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentFrame));
                OnPropertyChanged(nameof(CurrentFrameDurationMs));
                OnPropertyChanged(nameof(FrameDisplayNumber));
            }
        }
    }

    public AnimationFrame? CurrentFrame => Canvas.CurrentFrame;

    public int CurrentFrameDurationMs
    {
        get => CurrentFrame?.DurationMs ?? 100;
        set
        {
            if (CurrentFrame != null && value > 0 && CurrentFrame.DurationMs != value)
            {
                int oldDuration = CurrentFrame.DurationMs;
                _undoRedoService.ExecuteCommand(new ChangeFrameDurationCommand(CurrentFrame, oldDuration, value));
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentFrame));
            }
        }
    }

    public string FrameDisplayNumber => $"{CurrentFrameIndex + 1} / {Math.Max(1, Frames.Count)}";

    public bool IsPlaying => _playbackService.IsPlaying;

    public bool IsLooping
    {
        get => _playbackService.IsLooping;
        set
        {
            if (_playbackService.IsLooping != value)
            {
                _playbackService.IsLooping = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsOnionSkinEnabled
    {
        get => Canvas.OnionSkin.IsEnabled;
        set
        {
            if (Canvas.OnionSkin.IsEnabled != value)
            {
                Canvas.OnionSkin.IsEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public int OnionPrevFrames
    {
        get => Canvas.OnionSkin.PrevFramesCount;
        set
        {
            int clamped = Math.Clamp(value, 1, 5);
            if (Canvas.OnionSkin.PrevFramesCount != clamped)
            {
                Canvas.OnionSkin.PrevFramesCount = clamped;
                OnPropertyChanged();
            }
        }
    }

    public int OnionNextFrames
    {
        get => Canvas.OnionSkin.NextFramesCount;
        set
        {
            int clamped = Math.Clamp(value, 1, 5);
            if (Canvas.OnionSkin.NextFramesCount != clamped)
            {
                Canvas.OnionSkin.NextFramesCount = clamped;
                OnPropertyChanged();
            }
        }
    }

    public float OnionMaxOpacity
    {
        get => Canvas.OnionSkin.MaxOpacity;
        set
        {
            float clamped = Math.Clamp(value, 0.05f, 1.0f);
            if (Math.Abs(Canvas.OnionSkin.MaxOpacity - clamped) > 0.001f)
            {
                Canvas.OnionSkin.MaxOpacity = clamped;
                OnPropertyChanged();
            }
        }
    }

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set => SetProperty(ref _isCollapsed, value);
    }

    public double TimelineHeight
    {
        get => _timelineHeight;
        set => SetProperty(ref _timelineHeight, Math.Clamp(value, 80.0, 450.0));
    }

    public TimelineViewModel(
        CanvasViewModel canvasViewModel,
        IPlaybackService playbackService,
        UndoRedoService undoRedoService,
        IExportService? exportService = null,
        IFileDialogService? fileDialogService = null)
    {
        _canvasViewModel = canvasViewModel ?? throw new ArgumentNullException(nameof(canvasViewModel));
        _canvas = _canvasViewModel.Canvas;
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _undoRedoService = undoRedoService ?? throw new ArgumentNullException(nameof(undoRedoService));
        _exportService = exportService;
        _fileDialogService = fileDialogService;

        _playbackService.Attach(_canvasViewModel);
        SubscribeToCanvas(_canvas);

        if (_playbackService is ObservableObject obs)
        {
            obs.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IPlaybackService.IsPlaying))
                {
                    OnPropertyChanged(nameof(IsPlaying));
                }
                else if (e.PropertyName == nameof(IPlaybackService.IsLooping))
                {
                    OnPropertyChanged(nameof(IsLooping));
                }
            };
        }
    }

    public void SetCanvas(PixelCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        UnsubscribeFromCanvas();
        _canvas = canvas;
        SubscribeToCanvas(_canvas);

        OnPropertyChanged(nameof(Canvas));
        OnPropertyChanged(nameof(Frames));
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(Layers));
        OnPropertyChanged(nameof(CurrentFrameIndex));
        OnPropertyChanged(nameof(CurrentFrame));
        OnPropertyChanged(nameof(CurrentFrameDurationMs));
        OnPropertyChanged(nameof(FrameDisplayNumber));
        OnPropertyChanged(nameof(IsOnionSkinEnabled));
        OnPropertyChanged(nameof(OnionPrevFrames));
        OnPropertyChanged(nameof(OnionNextFrames));
        OnPropertyChanged(nameof(OnionMaxOpacity));
    }

    private void SubscribeToCanvas(PixelCanvas canvas)
    {
        canvas.FrameChanged += OnCanvasFrameChanged;
        canvas.OnionSkin.PropertyChanged += OnOnionSkinPropertyChanged;
    }

    private void UnsubscribeFromCanvas()
    {
        _canvas.FrameChanged -= OnCanvasFrameChanged;
        _canvas.OnionSkin.PropertyChanged -= OnOnionSkinPropertyChanged;
    }

    private void OnOnionSkinPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsOnionSkinEnabled));
        OnPropertyChanged(nameof(OnionPrevFrames));
        OnPropertyChanged(nameof(OnionNextFrames));
        OnPropertyChanged(nameof(OnionMaxOpacity));
    }

    private void OnCanvasFrameChanged(int newIndex)
    {
        OnPropertyChanged(nameof(CurrentFrameIndex));
        OnPropertyChanged(nameof(CurrentFrame));
        OnPropertyChanged(nameof(CurrentFrameDurationMs));
        OnPropertyChanged(nameof(FrameDisplayNumber));
    }

    [RelayCommand]
    public void TogglePlay()
    {
        _playbackService.TogglePlay();
        OnPropertyChanged(nameof(IsPlaying));
    }

    [RelayCommand]
    public void StepForward()
    {
        _playbackService.StepForward();
    }

    [RelayCommand]
    public void StepBackward()
    {
        _playbackService.StepBackward();
    }

    [RelayCommand]
    public void FirstFrame()
    {
        CurrentFrameIndex = 0;
    }

    [RelayCommand]
    public void LastFrame()
    {
        CurrentFrameIndex = Math.Max(0, Frames.Count - 1);
    }

    [RelayCommand]
    public void ToggleLoop()
    {
        IsLooping = !IsLooping;
    }

    [RelayCommand]
    public void ToggleOnionSkin()
    {
        IsOnionSkinEnabled = !IsOnionSkinEnabled;
        StatusRequested?.Invoke(this, IsOnionSkinEnabled ? "Onion Skinning Enabled" : "Onion Skinning Disabled");
    }

    [RelayCommand]
    public void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }

    [RelayCommand]
    public void AddFrame()
    {
        int insertIndex = CurrentFrameIndex + 1;
        var newFrame = new AnimationFrame(CurrentFrameDurationMs);
        foreach (var layer in Layers)
        {
            newFrame.Cels[layer.Id] = new AnimationCel(layer.Id, Canvas.Width, Canvas.Height);
        }

        _undoRedoService.ExecuteCommand(new AddFrameCommand(Canvas, insertIndex, newFrame));
        CurrentFrameIndex = insertIndex;
        StatusRequested?.Invoke(this, $"Added Frame {insertIndex + 1}");
    }

    [RelayCommand]
    public void DuplicateFrame()
    {
        if (CurrentFrameIndex < 0 || CurrentFrameIndex >= Frames.Count)
        {
            return;
        }

        var source = Frames[CurrentFrameIndex];
        var duplicated = source.Clone(newId: true);
        int newIndex = CurrentFrameIndex + 1;

        _undoRedoService.ExecuteCommand(new DuplicateFrameCommand(Canvas, CurrentFrameIndex, newIndex, duplicated));
        CurrentFrameIndex = newIndex;
        StatusRequested?.Invoke(this, $"Duplicated Frame {newIndex + 1}");
    }

    [RelayCommand]
    public void DeleteFrame()
    {
        if (Frames.Count <= 1)
        {
            StatusRequested?.Invoke(this, "Cannot delete the only frame.");
            return;
        }

        int index = CurrentFrameIndex;
        var frameToRemove = Frames[index];
        _undoRedoService.ExecuteCommand(new RemoveFrameCommand(Canvas, index, frameToRemove));
        StatusRequested?.Invoke(this, $"Deleted Frame {index + 1}");
    }

    [RelayCommand]
    public void SelectFrame(int index)
    {
        if (index >= 0 && index < Frames.Count)
        {
            CurrentFrameIndex = index;
        }
    }

    public void SelectCel(AnimationFrame frame, Layer layer)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(layer);

        int frameIdx = Frames.IndexOf(frame);
        if (frameIdx >= 0)
        {
            CurrentFrameIndex = frameIdx;
        }

        Canvas.ActiveLayer = layer;
    }

    [RelayCommand]
    public void OpenPreviewWindow()
    {
        if (_previewWindow != null)
        {
            _previewWindow.Activate();
            return;
        }

        var previewVm = new AnimationPreviewViewModel(_canvasViewModel);
        _previewWindow = new AnimationPreviewWindow(previewVm);
        _previewWindow.Closed += (_, _) => _previewWindow = null;
        _previewWindow.Show();
    }
}

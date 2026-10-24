using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelForge.Core.History;
using PixelForge.Core.Models;

namespace PixelForge.App.ViewModels;

/// <summary>
/// ViewModel managing the canvas layers stack, active layer selection, and layer operations.
/// </summary>
public partial class LayerViewModel : ViewModelBase
{
    private readonly UndoRedoService _undoRedoService;
    private PixelCanvas _canvas;

    public PixelCanvas Canvas
    {
        get => _canvas;
        private set
        {
            if (SetProperty(ref _canvas, value))
            {
                OnPropertyChanged(nameof(Layers));
                OnPropertyChanged(nameof(ActiveLayer));
                UpdateCommandStates();
            }
        }
    }

    public ObservableCollection<Layer> Layers => _canvas.Layers;

    public Layer? ActiveLayer
    {
        get => _canvas.ActiveLayer;
        set
        {
            if (_canvas.ActiveLayer != value)
            {
                _canvas.ActiveLayer = value;
                OnPropertyChanged();
                UpdateActiveLayerSubscription();
                UpdateCommandStates();
            }
        }
    }

    public bool IsActiveLayerAlphaLocked => ActiveLayer?.IsAlphaLocked == true;

    /// <summary>
    /// Occurs when a layer operation requests a user feedback status message to be displayed.
    /// </summary>
    public event EventHandler<string>? StatusRequested;

    private Layer? _subscribedActiveLayer;

    public bool CanRemoveLayer => _canvas.Layers.Count > 1;

    public bool CanMergeDown
    {
        get
        {
            if (ActiveLayer == null)
            {
                return false;
            }

            int index = _canvas.Layers.IndexOf(ActiveLayer);
            return index > 0;
        }
    }

    public bool CanMoveUp
    {
        get
        {
            if (ActiveLayer == null) return false;
            int index = _canvas.Layers.IndexOf(ActiveLayer);
            return index >= 0 && index < _canvas.Layers.Count - 1;
        }
    }

    public bool CanMoveDown
    {
        get
        {
            if (ActiveLayer == null) return false;
            int index = _canvas.Layers.IndexOf(ActiveLayer);
            return index > 0;
        }
    }

    public LayerViewModel(PixelCanvas canvas, UndoRedoService undoRedoService)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _undoRedoService = undoRedoService ?? throw new ArgumentNullException(nameof(undoRedoService));

        _canvas.PropertyChanged += OnCanvasPropertyChanged;
        _canvas.Layers.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Layers));
            UpdateCommandStates();
        };

        UpdateActiveLayerSubscription();
    }

    public void SetCanvas(PixelCanvas newCanvas)
    {
        ArgumentNullException.ThrowIfNull(newCanvas);
        if (_canvas != null)
        {
            _canvas.PropertyChanged -= OnCanvasPropertyChanged;
        }

        _canvas = newCanvas;
        _canvas.PropertyChanged += OnCanvasPropertyChanged;
        _canvas.Layers.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Layers));
            UpdateCommandStates();
        };

        OnPropertyChanged(nameof(Canvas));
        OnPropertyChanged(nameof(Layers));
        OnPropertyChanged(nameof(ActiveLayer));
        UpdateActiveLayerSubscription();
        UpdateCommandStates();
    }

    private void UpdateActiveLayerSubscription()
    {
        if (_subscribedActiveLayer != null)
        {
            _subscribedActiveLayer.PropertyChanged -= OnActiveLayerPropertyChanged;
            _subscribedActiveLayer = null;
        }

        if (ActiveLayer != null)
        {
            _subscribedActiveLayer = ActiveLayer;
            _subscribedActiveLayer.PropertyChanged += OnActiveLayerPropertyChanged;
        }

        OnPropertyChanged(nameof(IsActiveLayerAlphaLocked));
    }

    private void OnActiveLayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Layer.IsAlphaLocked))
        {
            OnPropertyChanged(nameof(IsActiveLayerAlphaLocked));
        }
    }

    private void OnCanvasPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PixelCanvas.ActiveLayer))
        {
            OnPropertyChanged(nameof(ActiveLayer));
            UpdateActiveLayerSubscription();
            UpdateCommandStates();
        }
    }

    [RelayCommand]
    public void AddLayer()
    {
        string name = $"Layer {_canvas.Layers.Count + 1}";
        var newLayer = new Layer(name, _canvas.Width, _canvas.Height);
        var command = new AddLayerCommand(_canvas, newLayer, _canvas.Layers.Count, $"Add {name}");
        _undoRedoService.ExecuteCommand(command);
        ActiveLayer = newLayer;
        StatusRequested?.Invoke(this, $"Created {name} (Ctrl+Shift+N)");
    }

    [RelayCommand(CanExecute = nameof(CanRemoveLayer))]
    public void RemoveLayer(Layer? layer = null)
    {
        var target = layer ?? ActiveLayer;
        if (target == null || _canvas.Layers.Count <= 1)
        {
            return;
        }

        var command = new RemoveLayerCommand(_canvas, target, $"Remove {target.Name}");
        _undoRedoService.ExecuteCommand(command);
        StatusRequested?.Invoke(this, $"Removed {target.Name}");
    }

    [RelayCommand]
    public void ToggleVisibility(Layer? layer)
    {
        var target = layer ?? ActiveLayer;
        if (target == null)
        {
            return;
        }

        bool nextVisible = !target.IsVisible;
        var command = new SetLayerVisibilityCommand(target, nextVisible, $"Toggle {target.Name} visibility");
        _undoRedoService.ExecuteCommand(command);
        StatusRequested?.Invoke(this, $"{target.Name} is now {(nextVisible ? "visible" : "hidden")} (Shift+L)");
    }

    /// <summary>
    /// Toggles the alpha lock state of the specified layer or active layer.
    /// </summary>
    /// <param name="layer">The target layer, or null for active layer.</param>
    [RelayCommand]
    public void ToggleAlphaLock(Layer? layer = null)
    {
        var target = layer ?? ActiveLayer;
        if (target == null)
        {
            return;
        }

        target.IsAlphaLocked = !target.IsAlphaLocked;
        OnPropertyChanged(nameof(IsActiveLayerAlphaLocked));
        StatusRequested?.Invoke(this, $"{target.Name} Alpha Lock: {(target.IsAlphaLocked ? "enabled" : "disabled")}");
    }

    [RelayCommand(CanExecute = nameof(CanMergeDown))]
    public void MergeDown()
    {
        if (ActiveLayer == null)
        {
            return;
        }

        int index = _canvas.Layers.IndexOf(ActiveLayer);
        if (index <= 0)
        {
            return;
        }

        string layerName = ActiveLayer.Name;
        var command = new MergeLayerDownCommand(_canvas, index, $"Merge {layerName} Down");
        _undoRedoService.ExecuteCommand(command);
        StatusRequested?.Invoke(this, $"Merged {layerName} Down (Ctrl+E)");
    }

    [RelayCommand]
    public void DuplicateLayer(Layer? layer = null)
    {
        var target = layer ?? ActiveLayer;
        if (target == null)
        {
            return;
        }

        var clone = target.Clone(newId: true);
        clone.Name = $"{target.Name} (Copy)";
        int index = _canvas.Layers.IndexOf(target) + 1;
        var command = new AddLayerCommand(_canvas, clone, index, $"Duplicate {target.Name}");
        _undoRedoService.ExecuteCommand(command);
        ActiveLayer = clone;
        StatusRequested?.Invoke(this, $"Duplicated {target.Name} (Ctrl+J)");
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    public void MoveUp()
    {
        if (ActiveLayer == null || !CanMoveUp) return;
        int index = _canvas.Layers.IndexOf(ActiveLayer);
        var command = new ReorderLayerCommand(_canvas, index, index + 1, $"Move {ActiveLayer.Name} Up");
        _undoRedoService.ExecuteCommand(command);
    }

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    public void MoveDown()
    {
        if (ActiveLayer == null || !CanMoveDown) return;
        int index = _canvas.Layers.IndexOf(ActiveLayer);
        var command = new ReorderLayerCommand(_canvas, index, index - 1, $"Move {ActiveLayer.Name} Down");
        _undoRedoService.ExecuteCommand(command);
    }

    private void UpdateCommandStates()
    {
        OnPropertyChanged(nameof(CanRemoveLayer));
        OnPropertyChanged(nameof(CanMergeDown));
        RemoveLayerCommand.NotifyCanExecuteChanged();
        MergeDownCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }
}

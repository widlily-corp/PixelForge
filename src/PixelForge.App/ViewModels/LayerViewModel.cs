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
                UpdateCommandStates();
            }
        }
    }

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
        UpdateCommandStates();
    }

    private void OnCanvasPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PixelCanvas.ActiveLayer))
        {
            OnPropertyChanged(nameof(ActiveLayer));
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
    }

    [RelayCommand]
    public void ToggleVisibility(Layer? layer)
    {
        var target = layer ?? ActiveLayer;
        if (target == null)
        {
            return;
        }

        var command = new SetLayerVisibilityCommand(target, !target.IsVisible, $"Toggle {target.Name} visibility");
        _undoRedoService.ExecuteCommand(command);
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

        var command = new MergeLayerDownCommand(_canvas, index, $"Merge {ActiveLayer.Name} Down");
        _undoRedoService.ExecuteCommand(command);
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
    }

    private void UpdateCommandStates()
    {
        OnPropertyChanged(nameof(CanRemoveLayer));
        OnPropertyChanged(nameof(CanMergeDown));
        RemoveLayerCommand.NotifyCanExecuteChanged();
        MergeDownCommand.NotifyCanExecuteChanged();
    }
}

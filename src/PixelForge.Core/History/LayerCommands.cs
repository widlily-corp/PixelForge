using PixelForge.Core.Models;

namespace PixelForge.Core.History;

/// <summary>
/// Undoable command that adds a layer to a canvas.
/// </summary>
public class AddLayerCommand : IUndoableCommand
{
    private readonly PixelCanvas _canvas;
    private readonly Layer _layer;
    private readonly int _targetIndex;

    public string Description { get; }

    public AddLayerCommand(PixelCanvas canvas, Layer layer, int? targetIndex = null, string description = "Add Layer")
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _targetIndex = targetIndex ?? canvas.Layers.Count;
        Description = description;
    }

    public void Execute()
    {
        _canvas.InsertLayer(_targetIndex, _layer);
        _canvas.ActiveLayer = _layer;
    }

    public void Undo()
    {
        _canvas.RemoveLayer(_layer);
    }
}

/// <summary>
/// Undoable command that removes a layer from a canvas.
/// </summary>
public class RemoveLayerCommand : IUndoableCommand
{
    private readonly PixelCanvas _canvas;
    private readonly Layer _layer;
    private readonly int _originalIndex;

    public string Description { get; }

    public RemoveLayerCommand(PixelCanvas canvas, Layer layer, string description = "Remove Layer")
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _originalIndex = canvas.Layers.IndexOf(layer);
        if (_originalIndex < 0)
        {
            throw new ArgumentException("Layer is not present in the canvas.", nameof(layer));
        }

        Description = description;
    }

    public void Execute()
    {
        _canvas.RemoveLayer(_layer);
    }

    public void Undo()
    {
        _canvas.InsertLayer(_originalIndex, _layer);
    }
}

/// <summary>
/// Undoable command that merges a layer onto the layer below it.
/// </summary>
public class MergeLayerDownCommand : IUndoableCommand
{
    private readonly PixelCanvas _canvas;
    private readonly Layer _topLayer;
    private readonly Layer _bottomLayer;
    private readonly int _topIndex;
    private readonly PixelColor[] _savedBottomPixels;
    private readonly float _savedBottomOpacity;
    private readonly bool _savedBottomVisibility;

    public string Description { get; }

    public MergeLayerDownCommand(PixelCanvas canvas, int topIndex, string description = "Merge Layer Down")
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        if (topIndex <= 0 || topIndex >= canvas.Layers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(topIndex), "Cannot merge bottom layer or invalid index.");
        }

        _topIndex = topIndex;
        _topLayer = canvas.Layers[topIndex];
        _bottomLayer = canvas.Layers[topIndex - 1];

        _savedBottomPixels = new PixelColor[_bottomLayer.Pixels.Length];
        Array.Copy(_bottomLayer.Pixels, _savedBottomPixels, _bottomLayer.Pixels.Length);
        _savedBottomOpacity = _bottomLayer.Opacity;
        _savedBottomVisibility = _bottomLayer.IsVisible;

        Description = description;
    }

    public void Execute()
    {
        _canvas.MergeLayerDown(_topIndex);
    }

    public void Undo()
    {
        // Restore bottom layer state
        Array.Copy(_savedBottomPixels, _bottomLayer.Pixels, _savedBottomPixels.Length);
        _bottomLayer.Opacity = _savedBottomOpacity;
        _bottomLayer.IsVisible = _savedBottomVisibility;

        // Reinsert top layer
        _canvas.InsertLayer(_topIndex, _topLayer);
        _canvas.ActiveLayer = _topLayer;
    }
}

/// <summary>
/// Undoable command that toggles or updates a layer's visibility.
/// </summary>
public class SetLayerVisibilityCommand : IUndoableCommand
{
    private readonly Layer _layer;
    private readonly bool _oldVisibility;
    private readonly bool _newVisibility;

    public string Description { get; }

    public SetLayerVisibilityCommand(Layer layer, bool newVisibility, string description = "Change Layer Visibility")
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _oldVisibility = layer.IsVisible;
        _newVisibility = newVisibility;
        Description = description;
    }

    public void Execute()
    {
        _layer.IsVisible = _newVisibility;
    }

    public void Undo()
    {
        _layer.IsVisible = _oldVisibility;
    }
}

/// <summary>
/// Undoable command that changes the order/index of a layer in the canvas stack.
/// </summary>
public class ReorderLayerCommand : IUndoableCommand
{
    private readonly PixelCanvas _canvas;
    private readonly Layer _layer;
    private readonly int _fromIndex;
    private readonly int _toIndex;

    public string Description { get; }

    public ReorderLayerCommand(PixelCanvas canvas, int fromIndex, int toIndex, string description = "Reorder Layer")
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        if (fromIndex < 0 || fromIndex >= canvas.Layers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(fromIndex));
        }

        if (toIndex < 0 || toIndex >= canvas.Layers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(toIndex));
        }

        _fromIndex = fromIndex;
        _toIndex = toIndex;
        _layer = canvas.Layers[fromIndex];
        Description = description;
    }

    public void Execute()
    {
        _canvas.Layers.Move(_fromIndex, _toIndex);
        _canvas.ActiveLayer = _layer;
        _canvas.InvalidateComposite();
    }

    public void Undo()
    {
        _canvas.Layers.Move(_toIndex, _fromIndex);
        _canvas.ActiveLayer = _layer;
        _canvas.InvalidateComposite();
    }
    }

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
    private readonly Dictionary<Guid, (PixelColor[] Pixels, bool IsEmpty)> _savedCels = new();

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

        // Snapshot cels across all frames for undo
        foreach (var frame in _canvas.Frames)
        {
            if (frame.Cels.TryGetValue(_layer.Id, out var cel))
            {
                var copy = new PixelColor[cel.Pixels.Length];
                Array.Copy(cel.Pixels, copy, cel.Pixels.Length);
                _savedCels[frame.Id] = (copy, cel.IsEmpty);
            }
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

        // Restore cels across all frames
        foreach (var frame in _canvas.Frames)
        {
            if (_savedCels.TryGetValue(frame.Id, out var saved))
            {
                if (!frame.Cels.TryGetValue(_layer.Id, out var cel))
                {
                    cel = new AnimationCel(_layer.Id, _canvas.Width, _canvas.Height);
                    frame.Cels[_layer.Id] = cel;
                }
                Array.Copy(saved.Pixels, cel.Pixels, Math.Min(saved.Pixels.Length, cel.Pixels.Length));
                cel.IsEmpty = saved.IsEmpty;
            }
        }

        if (_canvas.CurrentFrame != null && _canvas.CurrentFrame.Cels.TryGetValue(_layer.Id, out var curCel))
        {
            _layer.SetPixelsDirect(curCel.Pixels);
        }

        _layer.Invalidate();
        _canvas.InvalidateComposite();
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
    private readonly Dictionary<Guid, (PixelColor[]? TopPixels, bool TopEmpty, PixelColor[]? BottomPixels, bool BottomEmpty)> _savedCels = new();

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

        // Snapshot cels across all frames for undo
        foreach (var frame in _canvas.Frames)
        {
            PixelColor[]? topCopy = null;
            bool topEmpty = true;
            if (frame.Cels.TryGetValue(_topLayer.Id, out var topCel))
            {
                topCopy = new PixelColor[topCel.Pixels.Length];
                Array.Copy(topCel.Pixels, topCopy, topCel.Pixels.Length);
                topEmpty = topCel.IsEmpty;
            }

            PixelColor[]? botCopy = null;
            bool botEmpty = true;
            if (frame.Cels.TryGetValue(_bottomLayer.Id, out var botCel))
            {
                botCopy = new PixelColor[botCel.Pixels.Length];
                Array.Copy(botCel.Pixels, botCopy, botCel.Pixels.Length);
                botEmpty = botCel.IsEmpty;
            }

            _savedCels[frame.Id] = (topCopy, topEmpty, botCopy, botEmpty);
        }

        Description = description;
    }

    public void Execute()
    {
        _canvas.MergeLayerDown(_topIndex);
    }

    public void Undo()
    {
        // Reinsert top layer
        _canvas.InsertLayer(_topIndex, _topLayer);

        // Restore bottom and top cels across all frames
        foreach (var frame in _canvas.Frames)
        {
            if (_savedCels.TryGetValue(frame.Id, out var saved))
            {
                if (saved.TopPixels != null)
                {
                    if (!frame.Cels.TryGetValue(_topLayer.Id, out var topCel))
                    {
                        topCel = new AnimationCel(_topLayer.Id, _canvas.Width, _canvas.Height);
                        frame.Cels[_topLayer.Id] = topCel;
                    }
                    Array.Copy(saved.TopPixels, topCel.Pixels, Math.Min(saved.TopPixels.Length, topCel.Pixels.Length));
                    topCel.IsEmpty = saved.TopEmpty;
                }

                if (saved.BottomPixels != null)
                {
                    if (frame.Cels.TryGetValue(_bottomLayer.Id, out var botCel))
                    {
                        Array.Copy(saved.BottomPixels, botCel.Pixels, Math.Min(saved.BottomPixels.Length, botCel.Pixels.Length));
                        botCel.IsEmpty = saved.BottomEmpty;
                    }
                }
            }
        }

        // Restore bottom layer state
        _bottomLayer.Opacity = _savedBottomOpacity;
        _bottomLayer.IsVisible = _savedBottomVisibility;

        if (_canvas.CurrentFrame != null)
        {
            if (_canvas.CurrentFrame.Cels.TryGetValue(_bottomLayer.Id, out var curBotCel))
            {
                _bottomLayer.SetPixelsDirect(curBotCel.Pixels);
            }
            if (_canvas.CurrentFrame.Cels.TryGetValue(_topLayer.Id, out var curTopCel))
            {
                _topLayer.SetPixelsDirect(curTopCel.Pixels);
            }
        }

        _canvas.ActiveLayer = _topLayer;
        _bottomLayer.Invalidate();
        _topLayer.Invalidate();
        _canvas.InvalidateComposite();
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

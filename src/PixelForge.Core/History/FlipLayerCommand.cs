using PixelForge.Core.Models;

namespace PixelForge.Core.History;

public enum FlipDirection
{
    Horizontal,
    Vertical
}

/// <summary>
/// Reversible command that flips an entire layer horizontally or vertically.
/// </summary>
public class FlipLayerCommand : IUndoableCommand
{
    private readonly Layer _layer;
    private readonly FlipDirection _direction;
    private readonly PixelColor[] _oldPixels;
    private readonly PixelColor[] _newPixels;
    private readonly Action? _onFlipped;
    private readonly FlipFloatingSelectionCommand? _floatingCommand;

    public string Description => _floatingCommand != null
        ? _floatingCommand.Description
        : $"Flip {_layer.Name} {(_direction == FlipDirection.Horizontal ? "Horizontal" : "Vertical")}";

    public FlipLayerCommand(Layer layer, FlipDirection direction, Action? onFlipped = null, SkiaSharp.SKRectI? selectionRect = null)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _direction = direction;
        _onFlipped = onFlipped;

        int w = layer.Width;
        int h = layer.Height;
        _oldPixels = new PixelColor[layer.Pixels.Length];
        Array.Copy(layer.Pixels, _oldPixels, layer.Pixels.Length);

        _newPixels = new PixelColor[layer.Pixels.Length];
        Array.Copy(_oldPixels, _newPixels, _oldPixels.Length);

        if (selectionRect.HasValue && !selectionRect.Value.IsEmpty)
        {
            var rect = selectionRect.Value;
            int rx0 = Math.Clamp(rect.Left, 0, w);
            int ry0 = Math.Clamp(rect.Top, 0, h);
            int rx1 = Math.Clamp(rect.Right, 0, w);
            int ry1 = Math.Clamp(rect.Bottom, 0, h);
            int rw = rx1 - rx0;
            int rh = ry1 - ry0;

            if (rw > 0 && rh > 0)
            {
                for (int y = ry0; y < ry1; y++)
                {
                    for (int x = rx0; x < rx1; x++)
                    {
                        int srcX = direction == FlipDirection.Horizontal ? (rx0 + rw - 1 - (x - rx0)) : x;
                        int srcY = direction == FlipDirection.Vertical ? (ry0 + rh - 1 - (y - ry0)) : y;
                        _newPixels[y * w + x] = _oldPixels[srcY * w + srcX];
                    }
                }
            }
        }
        else
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int srcX = direction == FlipDirection.Horizontal ? (w - 1 - x) : x;
                    int srcY = direction == FlipDirection.Vertical ? (h - 1 - y) : y;
                    _newPixels[y * w + x] = _oldPixels[srcY * w + srcX];
                }
            }
        }
    }

    public FlipLayerCommand(Layer layer, FlipDirection direction, Action? onFlipped, SelectionModel? selection)
        : this(layer, direction, onFlipped, selection?.IsActive == true && !selection.IsFloating ? selection.Bounds : (SkiaSharp.SKRectI?)null)
    {
        if (selection?.IsFloating == true)
        {
            _floatingCommand = new FlipFloatingSelectionCommand(selection, direction, onFlipped);
        }
    }

    public void Execute()
    {
        if (_floatingCommand != null)
        {
            _floatingCommand.Execute();
            return;
        }

        Array.Copy(_newPixels, _layer.Pixels, _newPixels.Length);
        _layer.Invalidate();
        _onFlipped?.Invoke();
    }

    public void Undo()
    {
        if (_floatingCommand != null)
        {
            _floatingCommand.Undo();
            return;
        }

        Array.Copy(_oldPixels, _layer.Pixels, _oldPixels.Length);
        _layer.Invalidate();
        _onFlipped?.Invoke();
    }
}

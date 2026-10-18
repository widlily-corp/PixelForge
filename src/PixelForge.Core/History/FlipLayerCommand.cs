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

    public string Description => $"Flip {_layer.Name} {(_direction == FlipDirection.Horizontal ? "Horizontal" : "Vertical")}";

    public FlipLayerCommand(Layer layer, FlipDirection direction, Action? onFlipped = null)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _direction = direction;
        _onFlipped = onFlipped;

        int w = layer.Width;
        int h = layer.Height;
        _oldPixels = new PixelColor[layer.Pixels.Length];
        Array.Copy(layer.Pixels, _oldPixels, layer.Pixels.Length);

        _newPixels = new PixelColor[layer.Pixels.Length];

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

    public void Execute()
    {
        Array.Copy(_newPixels, _layer.Pixels, _newPixels.Length);
        _onFlipped?.Invoke();
    }

    public void Undo()
    {
        Array.Copy(_oldPixels, _layer.Pixels, _oldPixels.Length);
        _onFlipped?.Invoke();
    }
}

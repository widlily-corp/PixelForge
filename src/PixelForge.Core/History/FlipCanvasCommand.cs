using PixelForge.Core.Models;

namespace PixelForge.Core.History;

/// <summary>
/// Reversible command that flips all layers in the canvas horizontally or vertically.
/// </summary>
public class FlipCanvasCommand : IUndoableCommand
{
    private readonly PixelCanvas _canvas;
    private readonly FlipDirection _direction;
    private readonly Action? _onFlipped;
    private readonly List<(Layer Layer, PixelColor[] OldPixels, PixelColor[] NewPixels)> _layerData = new();

    public string Description => $"Flip Canvas {(_direction == FlipDirection.Horizontal ? "Horizontal" : "Vertical")}";

    public FlipCanvasCommand(PixelCanvas canvas, FlipDirection direction, Action? onFlipped = null)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _direction = direction;
        _onFlipped = onFlipped;

        int w = canvas.Width;
        int h = canvas.Height;

        foreach (var layer in canvas.Layers)
        {
            var oldPixels = new PixelColor[layer.Pixels.Length];
            Array.Copy(layer.Pixels, oldPixels, layer.Pixels.Length);

            var newPixels = new PixelColor[layer.Pixels.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int srcX = direction == FlipDirection.Horizontal ? (w - 1 - x) : x;
                    int srcY = direction == FlipDirection.Vertical ? (h - 1 - y) : y;
                    newPixels[y * w + x] = oldPixels[srcY * w + srcX];
                }
            }

            _layerData.Add((layer, oldPixels, newPixels));
        }
    }

    public void Execute()
    {
        foreach (var (layer, _, newPixels) in _layerData)
        {
            Array.Copy(newPixels, layer.Pixels, newPixels.Length);
        }
        _onFlipped?.Invoke();
    }

    public void Undo()
    {
        foreach (var (layer, oldPixels, _) in _layerData)
        {
            Array.Copy(oldPixels, layer.Pixels, oldPixels.Length);
        }
        _onFlipped?.Invoke();
    }
}

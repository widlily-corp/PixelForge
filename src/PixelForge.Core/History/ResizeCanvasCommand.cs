using PixelForge.Core.Models;

namespace PixelForge.Core.History;

/// <summary>
/// Reversible undo/redo command that resizes a multi-layer pixel canvas,
/// preserving existing artwork via custom anchor placement or nearest-neighbor scaling.
/// </summary>
public class ResizeCanvasCommand : IUndoableCommand
{
    private readonly PixelCanvas _canvas;
    private readonly int _oldWidth;
    private readonly int _oldHeight;
    private readonly int _newWidth;
    private readonly int _newHeight;
    private readonly (Layer Layer, PixelColor[] OldPixels, PixelColor[] NewPixels)[] _layerSnapshots;
    private readonly Action? _onDimensionsChanged;

    public string Description => $"Resize Canvas to {_newWidth} × {_newHeight} px";

    public int NewWidth => _newWidth;
    public int NewHeight => _newHeight;
    public int OldWidth => _oldWidth;
    public int OldHeight => _oldHeight;

    public ResizeCanvasCommand(
        PixelCanvas canvas,
        int newWidth,
        int newHeight,
        CanvasAnchor anchor = CanvasAnchor.Center,
        ResizeMode mode = ResizeMode.Anchor,
        Action? onDimensionsChanged = null)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (newWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newWidth), "Width must be greater than zero.");
        }

        if (newHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newHeight), "Height must be greater than zero.");
        }

        _canvas = canvas;
        _oldWidth = canvas.Width;
        _oldHeight = canvas.Height;
        _newWidth = newWidth;
        _newHeight = newHeight;
        _onDimensionsChanged = onDimensionsChanged;

        _layerSnapshots = new (Layer, PixelColor[], PixelColor[])[canvas.Layers.Count];

        var (ox, oy) = CalculateAnchorOffset(_oldWidth, _oldHeight, _newWidth, _newHeight, anchor);

        for (int i = 0; i < canvas.Layers.Count; i++)
        {
            var layer = canvas.Layers[i];
            var oldPixels = new PixelColor[layer.Pixels.Length];
            Array.Copy(layer.Pixels, oldPixels, layer.Pixels.Length);

            var newPixels = new PixelColor[_newWidth * _newHeight];

            if (mode == ResizeMode.ScaleNearestNeighbor)
            {
                ScaleNearestNeighbor(oldPixels, _oldWidth, _oldHeight, newPixels, _newWidth, _newHeight);
            }
            else
            {
                CopyPixelsWithOffset(oldPixels, _oldWidth, _oldHeight, newPixels, _newWidth, _newHeight, ox, oy);
            }

            _layerSnapshots[i] = (layer, oldPixels, newPixels);
        }
    }

    public void Execute()
    {
        foreach (var (layer, _, newPixels) in _layerSnapshots)
        {
            layer.SetDimensionsAndPixels(_newWidth, _newHeight, newPixels);
        }

        _canvas.SetDimensions(_newWidth, _newHeight);
        _onDimensionsChanged?.Invoke();
    }

    public void Undo()
    {
        foreach (var (layer, oldPixels, _) in _layerSnapshots)
        {
            layer.SetDimensionsAndPixels(_oldWidth, _oldHeight, oldPixels);
        }

        _canvas.SetDimensions(_oldWidth, _oldHeight);
        _onDimensionsChanged?.Invoke();
    }

    public static (int OffsetX, int OffsetY) CalculateAnchorOffset(int oldW, int oldH, int newW, int newH, CanvasAnchor anchor)
    {
        int diffX = newW - oldW;
        int diffY = newH - oldH;

        int ox = anchor switch
        {
            CanvasAnchor.TopLeft or CanvasAnchor.MiddleLeft or CanvasAnchor.BottomLeft => 0,
            CanvasAnchor.TopCenter or CanvasAnchor.Center or CanvasAnchor.BottomCenter => diffX / 2,
            CanvasAnchor.TopRight or CanvasAnchor.MiddleRight or CanvasAnchor.BottomRight => diffX,
            _ => diffX / 2
        };

        int oy = anchor switch
        {
            CanvasAnchor.TopLeft or CanvasAnchor.TopCenter or CanvasAnchor.TopRight => 0,
            CanvasAnchor.MiddleLeft or CanvasAnchor.Center or CanvasAnchor.MiddleRight => diffY / 2,
            CanvasAnchor.BottomLeft or CanvasAnchor.BottomCenter or CanvasAnchor.BottomRight => diffY,
            _ => diffY / 2
        };

        return (ox, oy);
    }

    private static void CopyPixelsWithOffset(
        PixelColor[] src, int srcW, int srcH,
        PixelColor[] dst, int dstW, int dstH,
        int ox, int oy)
    {
        Array.Clear(dst, 0, dst.Length);

        for (int y = 0; y < srcH; y++)
        {
            int targetY = y + oy;
            if (targetY < 0 || targetY >= dstH)
            {
                continue;
            }

            for (int x = 0; x < srcW; x++)
            {
                int targetX = x + ox;
                if (targetX < 0 || targetX >= dstW)
                {
                    continue;
                }

                dst[targetY * dstW + targetX] = src[y * srcW + x];
            }
        }
    }

    private static void ScaleNearestNeighbor(
        PixelColor[] src, int srcW, int srcH,
        PixelColor[] dst, int dstW, int dstH)
    {
        for (int y = 0; y < dstH; y++)
        {
            int srcY = Math.Clamp(y * srcH / dstH, 0, srcH - 1);
            for (int x = 0; x < dstW; x++)
            {
                int srcX = Math.Clamp(x * srcW / dstW, 0, srcW - 1);
                dst[y * dstW + x] = src[srcY * srcW + srcX];
            }
        }
    }
}

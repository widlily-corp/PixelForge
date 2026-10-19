using PixelForge.Core.Models;
using SkiaSharp;

namespace PixelForge.Core.History;

/// <summary>
/// Supported rotation angles for layers, selection, and canvas.
/// </summary>
public enum RotationAngle
{
    Rotate90CW,
    Rotate90CCW,
    Rotate180
}

/// <summary>
/// Reversible command that rotates a layer's contents (or active selection) by 90 CW, 90 CCW, or 180 degrees.
/// </summary>
public class RotateLayerCommand : IUndoableCommand
{
    private readonly Layer _layer;
    private readonly RotationAngle _angle;
    private readonly PixelColor[] _oldPixels;
    private readonly PixelColor[] _newPixels;
    private readonly Action? _onRotated;

    public string Description => $"Rotate {_layer.Name} {_angle switch
    {
        RotationAngle.Rotate90CW => "90° CW",
        RotationAngle.Rotate90CCW => "90° CCW",
        RotationAngle.Rotate180 => "180°",
        _ => ""
    }}";

    public RotateLayerCommand(Layer layer, RotationAngle angle, Action? onRotated = null, SKRectI? selectionRect = null)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _angle = angle;
        _onRotated = onRotated;

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
                // Clear bounding box in new pixels first
                for (int y = ry0; y < ry1; y++)
                {
                    for (int x = rx0; x < rx1; x++)
                    {
                        _newPixels[y * w + x] = PixelColor.Transparent;
                    }
                }

                float cx = rx0 + rw / 2f;
                float cy = ry0 + rh / 2f;

                for (int y = ry0; y < ry1; y++)
                {
                    for (int x = rx0; x < rx1; x++)
                    {
                        int dstX, dstY;
                        if (rw == rh)
                        {
                            // Exact square rotation
                            int lx = x - rx0;
                            int ly = y - ry0;
                            (dstX, dstY) = angle switch
                            {
                                RotationAngle.Rotate90CW => (rx0 + (rh - 1 - ly), ry0 + lx),
                                RotationAngle.Rotate90CCW => (rx0 + ly, ry0 + (rw - 1 - lx)),
                                RotationAngle.Rotate180 => (rx0 + (rw - 1 - lx), ry0 + (rh - 1 - ly)),
                                _ => (x, y)
                            };
                        }
                        else
                        {
                            // Centered rectangular rotation
                            float relX = x - cx;
                            float relY = y - cy;
                            float rotX = angle switch
                            {
                                RotationAngle.Rotate90CW => -relY,
                                RotationAngle.Rotate90CCW => relY,
                                RotationAngle.Rotate180 => -relX,
                                _ => relX
                            };
                            float rotY = angle switch
                            {
                                RotationAngle.Rotate90CW => relX,
                                RotationAngle.Rotate90CCW => -relX,
                                RotationAngle.Rotate180 => -relY,
                                _ => relY
                            };
                            dstX = (int)MathF.Round(cx + rotX);
                            dstY = (int)MathF.Round(cy + rotY);
                        }

                        if (dstX >= rx0 && dstX < rx1 && dstY >= ry0 && dstY < ry1)
                        {
                            _newPixels[dstY * w + dstX] = _oldPixels[y * w + x];
                        }
                    }
                }
            }
        }
        else if (w == h)
        {
            // Square layer in-place rotation
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int srcX, srcY;
                    switch (angle)
                    {
                        case RotationAngle.Rotate90CW:
                            srcX = y;
                            srcY = h - 1 - x;
                            break;
                        case RotationAngle.Rotate90CCW:
                            srcX = w - 1 - y;
                            srcY = x;
                            break;
                        case RotationAngle.Rotate180:
                            srcX = w - 1 - x;
                            srcY = h - 1 - y;
                            break;
                        default:
                            srcX = x;
                            srcY = y;
                            break;
                    }
                    _newPixels[y * w + x] = _oldPixels[srcY * w + srcX];
                }
            }
        }
        else
        {
            // Rectangular layer in-place centered rotation
            Array.Fill(_newPixels, PixelColor.Transparent);
            float cx = w / 2f;
            float cy = h / 2f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (angle == RotationAngle.Rotate180)
                    {
                        _newPixels[y * w + x] = _oldPixels[(h - 1 - y) * w + (w - 1 - x)];
                    }
                    else
                    {
                        float relX = x - cx;
                        float relY = y - cy;
                        float rotX = angle == RotationAngle.Rotate90CW ? -relY : relY;
                        float rotY = angle == RotationAngle.Rotate90CW ? relX : -relX;
                        int dstX = (int)MathF.Round(cx + rotX);
                        int dstY = (int)MathF.Round(cy + rotY);
                        if ((uint)dstX < (uint)w && (uint)dstY < (uint)h)
                        {
                            _newPixels[dstY * w + dstX] = _oldPixels[y * w + x];
                        }
                    }
                }
            }
        }
    }

    public void Execute()
    {
        Array.Copy(_newPixels, _layer.Pixels, _newPixels.Length);
        _onRotated?.Invoke();
    }

    public void Undo()
    {
        Array.Copy(_oldPixels, _layer.Pixels, _oldPixels.Length);
        _onRotated?.Invoke();
    }
}

/// <summary>
/// Reversible command that rotates the entire canvas and all its layers by 90 CW, 90 CCW, or 180 degrees.
/// Automatically adjusts canvas dimensions for rectangular aspect ratios.
/// </summary>
public class RotateCanvasCommand : IUndoableCommand
{
    private readonly PixelCanvas _canvas;
    private readonly RotationAngle _angle;
    private readonly Action? _onRotated;
    private readonly int _oldWidth;
    private readonly int _oldHeight;
    private readonly int _newWidth;
    private readonly int _newHeight;
    private readonly List<(Layer Layer, PixelColor[] OldPixels, PixelColor[] NewPixels)> _layerData = new();

    public string Description => $"Rotate Canvas {_angle switch
    {
        RotationAngle.Rotate90CW => "90° CW",
        RotationAngle.Rotate90CCW => "90° CCW",
        RotationAngle.Rotate180 => "180°",
        _ => ""
    }}";

    public RotateCanvasCommand(PixelCanvas canvas, RotationAngle angle, Action? onRotated = null)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _angle = angle;
        _onRotated = onRotated;

        _oldWidth = canvas.Width;
        _oldHeight = canvas.Height;

        if (angle == RotationAngle.Rotate180)
        {
            _newWidth = _oldWidth;
            _newHeight = _oldHeight;
        }
        else
        {
            // 90 CW and 90 CCW swap width and height
            _newWidth = _oldHeight;
            _newHeight = _oldWidth;
        }

        foreach (var layer in canvas.Layers)
        {
            var oldPixels = new PixelColor[layer.Pixels.Length];
            Array.Copy(layer.Pixels, oldPixels, layer.Pixels.Length);

            var newPixels = new PixelColor[_newWidth * _newHeight];

            for (int y = 0; y < _newHeight; y++)
            {
                for (int x = 0; x < _newWidth; x++)
                {
                    int srcX, srcY;
                    switch (angle)
                    {
                        case RotationAngle.Rotate90CW:
                            srcX = y;
                            srcY = _oldHeight - 1 - x;
                            break;
                        case RotationAngle.Rotate90CCW:
                            srcX = _oldWidth - 1 - y;
                            srcY = x;
                            break;
                        case RotationAngle.Rotate180:
                            srcX = _oldWidth - 1 - x;
                            srcY = _oldHeight - 1 - y;
                            break;
                        default:
                            srcX = x;
                            srcY = y;
                            break;
                    }

                    if ((uint)srcX < (uint)_oldWidth && (uint)srcY < (uint)_oldHeight)
                    {
                        newPixels[y * _newWidth + x] = oldPixels[srcY * _oldWidth + srcX];
                    }
                }
            }

            _layerData.Add((layer, oldPixels, newPixels));
        }
    }

    public void Execute()
    {
        _canvas.SetDimensions(_newWidth, _newHeight);
        foreach (var (layer, _, newPixels) in _layerData)
        {
            layer.SetDimensionsAndPixels(_newWidth, _newHeight, newPixels);
        }
        _onRotated?.Invoke();
    }

    public void Undo()
    {
        _canvas.SetDimensions(_oldWidth, _oldHeight);
        foreach (var (layer, oldPixels, _) in _layerData)
        {
            layer.SetDimensionsAndPixels(_oldWidth, _oldHeight, oldPixels);
        }
        _onRotated?.Invoke();
    }
}

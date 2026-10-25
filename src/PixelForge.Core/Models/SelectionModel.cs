using CommunityToolkit.Mvvm.ComponentModel;
using PixelForge.Core.History;
using SkiaSharp;

namespace PixelForge.Core.Models;

/// <summary>
/// Represents a rectangular selection and floating pixel buffer on the canvas.
/// </summary>
public partial class SelectionModel : ObservableObject
{
    private SKRectI _bounds;
    private PixelColor[] _pixels = Array.Empty<PixelColor>();
    private bool _isActive;
    private bool _isFloating;
    private int _offsetX;
    private int _offsetY;

    public SKRectI Bounds
    {
        get => _bounds;
        set => SetProperty(ref _bounds, value);
    }

    public PixelColor[] Pixels
    {
        get => _pixels;
        set => SetProperty(ref _pixels, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public bool IsFloating
    {
        get => _isFloating;
        set => SetProperty(ref _isFloating, value);
    }

    public int OffsetX
    {
        get => _offsetX;
        set => SetProperty(ref _offsetX, value);
    }

    public int OffsetY
    {
        get => _offsetY;
        set => SetProperty(ref _offsetY, value);
    }

    /// <summary>
    /// Sets an active selection rectangle and optionally populates its pixel buffer.
    /// </summary>
    public void Select(SKRectI bounds, PixelColor[]? pixels = null)
    {
        Bounds = bounds;
        Pixels = pixels ?? (bounds.Width > 0 && bounds.Height > 0 ? new PixelColor[bounds.Width * bounds.Height] : Array.Empty<PixelColor>());
        IsActive = bounds.Width > 0 && bounds.Height > 0;
        IsFloating = false;
        OffsetX = 0;
        OffsetY = 0;
    }

    /// <summary>
    /// Lifts selected pixels from the given layer, making the selection floating.
    /// </summary>
    public void Lift(Layer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (!IsActive || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        int w = Bounds.Width;
        int h = Bounds.Height;
        var capturedPixels = new PixelColor[w * h];

        for (int y = 0; y < h; y++)
        {
            int srcY = Bounds.Top + y;
            for (int x = 0; x < w; x++)
            {
                int srcX = Bounds.Left + x;
                capturedPixels[y * w + x] = layer.GetPixel(srcX, srcY);
                layer.SetPixel(srcX, srcY, PixelColor.Transparent);
            }
        }

        Pixels = capturedPixels;
        IsFloating = true;
        OffsetX = 0;
        OffsetY = 0;
    }

    /// <summary>
    /// Stamps floating selection pixels back onto the specified layer.
    /// </summary>
    public void Stamp(Layer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (!IsActive || Pixels.Length == 0)
        {
            return;
        }

        int w = Bounds.Width;
        int h = Bounds.Height;
        int destStartX = Bounds.Left + OffsetX;
        int destStartY = Bounds.Top + OffsetY;

        for (int y = 0; y < h; y++)
        {
            int dstY = destStartY + y;
            for (int x = 0; x < w; x++)
            {
                int dstX = destStartX + x;
                var color = Pixels[y * w + x];
                if (color.A > 0)
                {
                    layer.SetPixel(dstX, dstY, color);
                }
            }
        }

        IsFloating = false;
        OffsetX = 0;
        OffsetY = 0;
    }

    /// <summary>
    /// Clears the selection state.
    /// </summary>
    public void Clear()
    {
        Bounds = SKRectI.Empty;
        Pixels = Array.Empty<PixelColor>();
        IsActive = false;
        IsFloating = false;
        OffsetX = 0;
        OffsetY = 0;
    }

    /// <summary>
    /// Checks whether the point is within the current selection bounds (including floating offset).
    /// </summary>
    public bool Contains(int x, int y)
    {
        if (!IsActive)
        {
            return false;
        }

        var rect = IsFloating
            ? new SKRectI(Bounds.Left + OffsetX, Bounds.Top + OffsetY, Bounds.Right + OffsetX, Bounds.Bottom + OffsetY)
            : Bounds;

        return rect.Contains(x, y);
    }

    /// <summary>
    /// Flips the floating selection pixel buffer horizontally or vertically.
    /// </summary>
    public void Flip(FlipDirection direction)
    {
        if (!IsFloating || Pixels.Length == 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        int w = Bounds.Width;
        int h = Bounds.Height;
        var newPixels = new PixelColor[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int srcX = direction == FlipDirection.Horizontal ? (w - 1 - x) : x;
                int srcY = direction == FlipDirection.Vertical ? (h - 1 - y) : y;
                newPixels[y * w + x] = Pixels[srcY * w + srcX];
            }
        }

        Pixels = newPixels;
    }

    /// <summary>
    /// Rotates the floating selection pixel buffer by 90 CW, 90 CCW, or 180 degrees, updating bounds dimensions.
    /// </summary>
    public void Rotate(RotationAngle angle)
    {
        if (!IsFloating || Pixels.Length == 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        int w = Bounds.Width;
        int h = Bounds.Height;

        if (angle == RotationAngle.Rotate180)
        {
            var newPixels = new PixelColor[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    newPixels[y * w + x] = Pixels[(h - 1 - y) * w + (w - 1 - x)];
                }
            }
            Pixels = newPixels;
            return;
        }

        int newW = h;
        int newH = w;
        var rotatedPixels = new PixelColor[newW * newH];

        for (int ny = 0; ny < newH; ny++)
        {
            for (int nx = 0; nx < newW; nx++)
            {
                int srcX = angle == RotationAngle.Rotate90CW ? ny : (w - 1 - ny);
                int srcY = angle == RotationAngle.Rotate90CW ? (h - 1 - nx) : nx;
                rotatedPixels[ny * newW + nx] = Pixels[srcY * w + srcX];
            }
        }

        Pixels = rotatedPixels;
        Bounds = new SKRectI(Bounds.Left, Bounds.Top, Bounds.Left + newW, Bounds.Top + newH);
    }
}

using PixelForge.Core.Models;
using SkiaSharp;

namespace PixelForge.Core.Transforms;

/// <summary>
/// Specifies the active direct-manipulation handle on the transformation bounding box.
/// </summary>
public enum TransformHandle
{
    None,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
    Rotate,
    Move
}

/// <summary>
/// Specifies the pixel resampling algorithm used when applying transformations.
/// </summary>
public enum ResampleAlgorithm
{
    NearestNeighbor,
    RotSprite
}

/// <summary>
/// Result of a pixel transformation operation containing the new bounds and pixel buffer.
/// </summary>
public readonly record struct TransformedPixelsResult(SKRectI Bounds, int Width, int Height, PixelColor[] Pixels);

/// <summary>
/// Encapsulates the active affine transformation parameters and lifted pixel buffer.
/// </summary>
public sealed class TransformSession
{
    public SKRectI OriginalBounds { get; }
    public PixelColor[] OriginalPixels { get; }
    public SKPoint Pivot { get; set; }
    public float ScaleX { get; set; } = 1.0f;
    public float ScaleY { get; set; } = 1.0f;
    public float RotationDegrees { get; set; } = 0.0f;
    public float TranslationX { get; set; } = 0.0f;
    public float TranslationY { get; set; } = 0.0f;
    public ResampleAlgorithm Algorithm { get; set; } = ResampleAlgorithm.RotSprite;

    public TransformSession(SKRectI bounds, PixelColor[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        OriginalBounds = bounds;
        OriginalPixels = (PixelColor[])pixels.Clone();
        Pivot = new SKPoint(bounds.Left + bounds.Width / 2.0f, bounds.Top + bounds.Height / 2.0f);
    }

    /// <summary>
    /// Computes the forward transformation matrix from original canvas coordinates to transformed coordinates.
    /// </summary>
    public SKMatrix ComputeMatrix()
    {
        var matrix = SKMatrix.CreateTranslation(-Pivot.X, -Pivot.Y);
        matrix = matrix.PostConcat(SKMatrix.CreateScale(ScaleX, ScaleY));
        matrix = matrix.PostConcat(SKMatrix.CreateRotationDegrees(RotationDegrees));
        matrix = matrix.PostConcat(SKMatrix.CreateTranslation(Pivot.X + TranslationX, Pivot.Y + TranslationY));
        return matrix;
    }
}

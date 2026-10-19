namespace PixelForge.Core.Models;

/// <summary>
/// Predefined pixel dithering and hatching patterns for retro texturing and gradients.
/// </summary>
public enum DitherPattern
{
    None,
    Checker50,
    Bayer2x2,
    Bayer4x4,
    Bayer8x8,
    Dots25,
    Dots75,
    HorizontalLines,
    VerticalLines,
    DiagonalLines
}

/// <summary>
/// High-performance zero-allocation dithering pattern evaluation utilities.
/// </summary>
public static class DitherHelper
{
    // Normalized Bayer 2x2 matrix: values 0..3 / 4.0f
    private static readonly float[,] Bayer2 = new float[2, 2]
    {
        { 0f / 4f, 2f / 4f },
        { 3f / 4f, 1f / 4f }
    };

    // Normalized Bayer 4x4 matrix: values 0..15 / 16.0f
    private static readonly float[,] Bayer4 = new float[4, 4]
    {
        {  0f / 16f,  8f / 16f,  2f / 16f, 10f / 16f },
        { 12f / 16f,  4f / 16f, 14f / 16f,  6f / 16f },
        {  3f / 16f, 11f / 16f,  1f / 16f,  9f / 16f },
        { 15f / 16f,  7f / 16f, 13f / 16f,  5f / 16f }
    };

    // Normalized Bayer 8x8 matrix: values 0..63 / 64.0f
    private static readonly float[,] Bayer8 = new float[8, 8]
    {
        {  0f/64f, 32f/64f,  8f/64f, 40f/64f,  2f/64f, 34f/64f, 10f/64f, 42f/64f },
        { 48f/64f, 16f/64f, 56f/64f, 24f/64f, 50f/64f, 18f/64f, 58f/64f, 26f/64f },
        { 12f/64f, 44f/64f,  4f/64f, 36f/64f, 14f/64f, 46f/64f,  6f/64f, 38f/64f },
        { 60f/64f, 28f/64f, 52f/64f, 20f/64f, 62f/64f, 30f/64f, 54f/64f, 22f/64f },
        {  3f/64f, 35f/64f, 11f/64f, 43f/64f,  1f/64f, 33f/64f,  9f/64f, 41f/64f },
        { 51f/64f, 19f/64f, 59f/64f, 27f/64f, 49f/64f, 17f/64f, 57f/64f, 25f/64f },
        { 15f/64f, 47f/64f,  7f/64f, 39f/64f, 13f/64f, 45f/64f,  5f/64f, 37f/64f },
        { 63f/64f, 31f/64f, 55f/64f, 23f/64f, 61f/64f, 29f/64f, 53f/64f, 21f/64f }
    };

    /// <summary>
    /// Determines whether the pixel at (x, y) should be shaded under the given dither pattern and intensity threshold.
    /// </summary>
    /// <param name="x">Pixel X coordinate.</param>
    /// <param name="y">Pixel Y coordinate.</param>
    /// <param name="pattern">Active dither pattern.</param>
    /// <param name="threshold">Intensity threshold between 0.0 (empty) and 1.0 (full).</param>
    /// <returns>True if the pixel should be shaded; false otherwise.</returns>
    public static bool ShouldPlot(int x, int y, DitherPattern pattern, float threshold = 0.5f)
    {
        if (pattern == DitherPattern.None)
        {
            return true;
        }

        if (threshold <= 0f) return false;
        if (threshold >= 1f) return true;

        // Ensure non-negative coordinates for modulo
        int px = (x % 64 + 64) % 64;
        int py = (y % 64 + 64) % 64;

        return pattern switch
        {
            DitherPattern.Checker50 => ((px + py) & 1) == 0,
            DitherPattern.Dots25 => (px & 1) == 0 && (py & 1) == 0,
            DitherPattern.Dots75 => !((px & 1) == 1 && (py & 1) == 1),
            DitherPattern.HorizontalLines => (py & 1) == 0,
            DitherPattern.VerticalLines => (px & 1) == 0,
            DitherPattern.DiagonalLines => ((px + py) % 3) == 0,
            DitherPattern.Bayer2x2 => Bayer2[py % 2, px % 2] < threshold,
            DitherPattern.Bayer4x4 => Bayer4[py % 4, px % 4] < threshold,
            DitherPattern.Bayer8x8 => Bayer8[py % 8, px % 8] < threshold,
            _ => true
        };
    }

    /// <summary>
    /// Selects either primary or secondary color at (x, y) using the specified dither pattern and factor.
    /// </summary>
    public static PixelColor EvaluateDitherColor(int x, int y, PixelColor primary, PixelColor secondary, DitherPattern pattern, float factor = 0.5f)
    {
        if (pattern == DitherPattern.None)
        {
            return primary;
        }

        return ShouldPlot(x, y, pattern, factor) ? primary : secondary;
    }
}

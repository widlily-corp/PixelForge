namespace PixelForge.Core.Patterns;

/// <summary>
/// Predefined classic 8x8 pixel-art and retro pattern presets.
/// Provides precomputed static presets to ensure zero runtime memory allocations.
/// </summary>
public static class PatternPresets
{
    /// <summary>
    /// Gets the classic alternating 1x1 checkerboard dither pattern.
    /// </summary>
    public static PatternPreset Checkerboard { get; } = PatternPreset.FromRows("Checkerboard",
        "10101010",
        "01010101",
        "10101010",
        "01010101",
        "10101010",
        "01010101",
        "10101010",
        "01010101");

    /// <summary>
    /// Gets the running bond retro brick masonry pattern.
    /// </summary>
    public static PatternPreset Bricks { get; } = PatternPreset.FromRows("Bricks",
        "11111110",
        "00000000",
        "11101111",
        "00000000",
        "11111110",
        "00000000",
        "11101111",
        "00000000");

    /// <summary>
    /// Gets the crossed diagonal hatching texture.
    /// </summary>
    public static PatternPreset Crosshatch { get; } = PatternPreset.FromRows("Crosshatch",
        "10001000",
        "01010100",
        "00100010",
        "01010100",
        "10001000",
        "00010001",
        "00100010",
        "00010001");

    /// <summary>
    /// Gets repeating diagonal 45-degree striping.
    /// </summary>
    public static PatternPreset DiagonalStripe { get; } = PatternPreset.FromRows("DiagonalStripe",
        "11000011",
        "11100001",
        "01110000",
        "00111000",
        "00011100",
        "00001110",
        "00000111",
        "10000011");

    /// <summary>
    /// Gets dense stipple dots pattern.
    /// </summary>
    public static PatternPreset DenseDots { get; } = PatternPreset.FromRows("DenseDots",
        "10011001",
        "00000000",
        "01100110",
        "00000000",
        "10011001",
        "00000000",
        "01100110",
        "00000000");

    /// <summary>
    /// Gets sparse stipple dots pattern.
    /// </summary>
    public static PatternPreset SparseDots { get; } = PatternPreset.FromRows("SparseDots",
        "10000000",
        "00000000",
        "00001000",
        "00000000",
        "00000000",
        "00000000",
        "00000000",
        "00000000");

    /// <summary>
    /// Gets fish scale / chainmail rounded texture.
    /// </summary>
    public static PatternPreset Scales { get; } = PatternPreset.FromRows("Scales",
        "00111100",
        "01000010",
        "10000001",
        "10000001",
        "00111100",
        "01000010",
        "10000001",
        "10000001");

    /// <summary>
    /// Gets basketweave alternating 4x4 block texture.
    /// </summary>
    public static PatternPreset Weave { get; } = PatternPreset.FromRows("Weave",
        "11110000",
        "11110000",
        "11110000",
        "11110000",
        "00001111",
        "00001111",
        "00001111",
        "00001111");

    /// <summary>
    /// Gets balanced pseudo-random 50% dither noise texture.
    /// </summary>
    public static PatternPreset Noise { get; } = PatternPreset.FromRows("Noise",
        "10110100",
        "01001011",
        "11010010",
        "00101101",
        "10010110",
        "01101001",
        "11010100",
        "00101011");

    /// <summary>
    /// Gets alternating horizontal scanlines pattern.
    /// </summary>
    public static PatternPreset HorizontalLines { get; } = PatternPreset.FromRows("Horizontal",
        "11111111",
        "00000000",
        "11111111",
        "00000000",
        "11111111",
        "00000000",
        "11111111",
        "00000000");

    /// <summary>
    /// Gets alternating horizontal scanlines pattern (alias for <see cref="HorizontalLines"/>).
    /// </summary>
    public static PatternPreset Horizontal => HorizontalLines;

    /// <summary>
    /// Gets alternating vertical stripes pattern.
    /// </summary>
    public static PatternPreset VerticalLines { get; } = PatternPreset.FromRows("Vertical",
        "10101010",
        "10101010",
        "10101010",
        "10101010",
        "10101010",
        "10101010",
        "10101010",
        "10101010");

    /// <summary>
    /// Gets alternating vertical stripes pattern (alias for <see cref="VerticalLines"/>).
    /// </summary>
    public static PatternPreset Vertical => VerticalLines;

    /// <summary>
    /// Gets all predefined pattern presets in standard catalog order.
    /// </summary>
    public static IReadOnlyList<PatternPreset> AllPresets { get; } =
    [
        Checkerboard,
        Bricks,
        Crosshatch,
        DiagonalStripe,
        DenseDots,
        SparseDots,
        Scales,
        Weave,
        Noise,
        HorizontalLines,
        VerticalLines
    ];

    /// <summary>
    /// Finds a predefined preset by name (case-insensitive).
    /// </summary>
    /// <param name="name">Name of the preset to find.</param>
    /// <returns>Matching preset, or <c>null</c> if not found.</returns>
    public static PatternPreset? GetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        for (int i = 0; i < AllPresets.Count; i++)
        {
            if (string.Equals(AllPresets[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return AllPresets[i];
            }
        }

        if (string.Equals(name, "Horizontal", StringComparison.OrdinalIgnoreCase))
        {
            return HorizontalLines;
        }

        if (string.Equals(name, "Vertical", StringComparison.OrdinalIgnoreCase))
        {
            return VerticalLines;
        }

        if (string.Equals(name, "Diagonal", StringComparison.OrdinalIgnoreCase))
        {
            return DiagonalStripe;
        }

        return null;
    }
}

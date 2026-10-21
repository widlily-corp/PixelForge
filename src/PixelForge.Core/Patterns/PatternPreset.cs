namespace PixelForge.Core.Patterns;

/// <summary>
/// Represents an 8x8 binary pattern mask for texturing, custom brushes, and pixel stamping.
/// Uses a 64-bit ulong bitmask for zero GC allocations and sub-nanosecond lookups.
/// </summary>
public readonly record struct PatternPreset(string Name, ulong Mask)
{
    /// <summary>
    /// Gets a predefined empty (all zeroes) 8x8 pattern preset.
    /// </summary>
    public static readonly PatternPreset Empty = new("Empty", 0UL);

    /// <summary>
    /// Gets a predefined solid (all ones) 8x8 pattern preset.
    /// </summary>
    public static readonly PatternPreset Solid = new("Solid", ~0UL);

    /// <summary>
    /// Evaluates if the pattern has a foreground pixel at coordinate (x, y), wrapping periodically every 8 pixels.
    /// </summary>
    /// <param name="x">The X coordinate.</param>
    /// <param name="y">The Y coordinate.</param>
    /// <returns><c>true</c> if the bit at the periodic 8x8 coordinate is set; otherwise, <c>false</c>.</returns>
    public bool IsPixelSet(int x, int y)
    {
        int px = (x % 8 + 8) % 8;
        int py = (y % 8 + 8) % 8;
        int bitIndex = py * 8 + px;
        return (Mask & (1UL << bitIndex)) != 0UL;
    }

    /// <summary>
    /// Gets the pixel state at coordinate (x, y), wrapping periodically every 8 pixels.
    /// Equivalent to <see cref="IsPixelSet(int, int)"/>.
    /// </summary>
    public bool GetPixel(int x, int y) => IsPixelSet(x, y);

    /// <summary>
    /// Returns a new <see cref="PatternPreset"/> with the specified pixel set or cleared.
    /// </summary>
    /// <param name="x">The local X coordinate (0..7).</param>
    /// <param name="y">The local Y coordinate (0..7).</param>
    /// <param name="value"><c>true</c> to set the bit; <c>false</c> to clear it.</param>
    /// <returns>A new <see cref="PatternPreset"/> with the updated bitmask.</returns>
    public PatternPreset WithPixel(int x, int y, bool value)
    {
        int px = (x % 8 + 8) % 8;
        int py = (y % 8 + 8) % 8;
        int bitIndex = py * 8 + px;

        ulong mask = value
            ? (Mask | (1UL << bitIndex))
            : (Mask & ~(1UL << bitIndex));

        return new PatternPreset(Name, mask);
    }

    /// <summary>
    /// Returns a new <see cref="PatternPreset"/> with all bits inverted.
    /// </summary>
    /// <param name="newName">Optional name for the inverted pattern. Defaults to original name + (Inverted).</param>
    /// <returns>A new inverted <see cref="PatternPreset"/>.</returns>
    public PatternPreset Invert(string? newName = null)
    {
        return new PatternPreset(newName ?? $"{Name} (Inverted)", ~Mask);
    }

    /// <summary>
    /// Helper to construct a pattern from 8 binary row strings (e.g. "10101010").
    /// Characters '1', '#', 'X', 'x', and '*' are treated as foreground (set) pixels.
    /// </summary>
    /// <param name="name">Name of the preset.</param>
    /// <param name="rows">Array of exactly 8 row strings representing the 8x8 pattern.</param>
    /// <returns>A configured <see cref="PatternPreset"/> instance.</returns>
    public static PatternPreset FromRows(string name, params string[] rows)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Length != 8)
        {
            throw new ArgumentException("Pattern must have exactly 8 rows.", nameof(rows));
        }

        ulong mask = 0UL;
        for (int y = 0; y < 8; y++)
        {
            string row = rows[y];
            if (row == null)
            {
                throw new ArgumentException($"Row {y} cannot be null.", nameof(rows));
            }

            for (int x = 0; x < Math.Min(8, row.Length); x++)
            {
                char c = row[x];
                if (c is '1' or '#' or 'X' or 'x' or '*')
                {
                    int bitIndex = y * 8 + x;
                    mask |= (1UL << bitIndex);
                }
            }
        }

        return new PatternPreset(name, mask);
    }

    /// <inheritdoc/>
    public override string ToString() => Name;
}

using PixelForge.Core.Models;

namespace PixelForge.Core.Tools;

/// <summary>
/// Represents a color stop along a multi-stop gradient vector.
/// </summary>
public readonly record struct GradientStop : IComparable<GradientStop>
{
    /// <summary>
    /// Normalized position of the stop along the gradient vector in range [0.0, 1.0].
    /// </summary>
    public float Position { get; init; }

    /// <summary>
    /// Color associated with this gradient stop.
    /// </summary>
    public PixelColor Color { get; init; }

    public GradientStop(float position, PixelColor color)
    {
        Position = Math.Clamp(position, 0f, 1f);
        Color = color;
    }

    public int CompareTo(GradientStop other) => Position.CompareTo(other.Position);
}

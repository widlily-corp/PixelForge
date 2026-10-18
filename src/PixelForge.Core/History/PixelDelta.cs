using PixelForge.Core.Models;

namespace PixelForge.Core.History;

/// <summary>
/// Represents a change in color at specific coordinates on a layer.
/// </summary>
public readonly record struct PixelDelta(int X, int Y, PixelColor OldColor, PixelColor NewColor);

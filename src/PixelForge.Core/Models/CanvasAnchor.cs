namespace PixelForge.Core.Models;

/// <summary>
/// Specifies the anchor position when extending or cropping a canvas during resize.
/// </summary>
public enum CanvasAnchor
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    Center,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

/// <summary>
/// Specifies the algorithm used when resizing a canvas.
/// </summary>
public enum ResizeMode
{
    /// <summary>
    /// Repositions existing artwork relative to an anchor point, padding new area or cropping.
    /// </summary>
    Anchor,

    /// <summary>
    /// Scales pixels proportionally using nearest-neighbor interpolation without blur.
    /// </summary>
    ScaleNearestNeighbor
}

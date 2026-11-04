namespace PixelForge.Core.Services;

/// <summary>
/// Defines the arrangement mode of frames within the exported spritesheet atlas.
/// </summary>
public enum SpritesheetLayoutMode
{
    /// <summary>
    /// Arranges frames in a 2D grid with a specified or auto-calculated number of columns.
    /// </summary>
    Grid,

    /// <summary>
    /// Arranges all frames in a single horizontal strip (1 row of N frames).
    /// </summary>
    HorizontalStrip,

    /// <summary>
    /// Arranges all frames in a single vertical strip (N rows of 1 frame).
    /// </summary>
    VerticalStrip
}

/// <summary>
/// Configuration settings for exporting an animation to a spritesheet texture atlas.
/// </summary>
public class SpritesheetSettings
{
    /// <summary>
    /// Gets or sets the layout arrangement of frames in the atlas.
    /// </summary>
    public SpritesheetLayoutMode LayoutMode { get; set; } = SpritesheetLayoutMode.Grid;

    /// <summary>
    /// Gets or sets the fixed number of columns for grid mode.
    /// If 0, the number of columns is automatically calculated based on the square root of total frames.
    /// </summary>
    public int Columns { get; set; } = 0;

    /// <summary>
    /// Gets or sets the inner padding (gap) in pixels between adjacent frames.
    /// </summary>
    public int Padding { get; set; } = 0;

    /// <summary>
    /// Gets or sets the outer border margin in pixels around the perimeter of the atlas.
    /// </summary>
    public int Border { get; set; } = 0;

    /// <summary>
    /// Gets or sets the integer scaling multiplier (1x, 2x, 4x, etc.) applied to each frame.
    /// </summary>
    public int Scale { get; set; } = 1;
}

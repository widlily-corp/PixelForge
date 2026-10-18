using PixelForge.Core.Models;

namespace PixelForge.Core.Tools;

/// <summary>
/// Defines the contract for interactive pixel art canvas drawing and manipulation tools.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Gets the human-readable display name of the tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the keyboard shortcut key for activating this tool.
    /// </summary>
    string Shortcut { get; }

    /// <summary>
    /// Gets the vector geometry icon resource key for this tool.
    /// </summary>
    string IconKey { get; }

    /// <summary>
    /// Handles pointer down (press) event at the specified pixel coordinates.
    /// </summary>
    void OnPointerDown(ToolContext ctx, int x, int y);

    /// <summary>
    /// Handles pointer move (drag) event at the specified pixel coordinates.
    /// </summary>
    void OnPointerMove(ToolContext ctx, int x, int y);

    /// <summary>
    /// Handles pointer up (release) event at the specified pixel coordinates.
    /// </summary>
    void OnPointerUp(ToolContext ctx, int x, int y);

    /// <summary>
    /// Returns dynamic pixel overlay preview data while dragging or hovering.
    /// </summary>
    IReadOnlyList<(int X, int Y, PixelColor Color)> GetPreviewPixels(ToolContext ctx);
}

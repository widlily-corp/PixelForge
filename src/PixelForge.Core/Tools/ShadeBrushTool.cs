using PixelForge.Core.History;
using PixelForge.Core.Models;

namespace PixelForge.Core.Tools;

/// <summary>
/// Interactive shade / tint brush tool.
/// Left-click shifts the target pixel one step darker along the active palette ramp (or -10% RGB brightness).
/// Right-click (or isPrimary = false) shifts the target pixel one step lighter (or +10% RGB brightness).
/// </summary>
public class ShadeBrushTool : ITool
{
    private readonly HashSet<int> _visited = new();
    private readonly List<PixelDelta> _deltas = new();
    private bool _isDrawing;
    private bool _isDarker = true;
    private int _lastX;
    private int _lastY;

    /// <summary>
    /// Gets the display name of the tool.
    /// </summary>
    public string Name => "Shade Brush";

    /// <summary>
    /// Gets the tooltip explaining tool behavior.
    /// </summary>
    public string Tooltip => "Shade / Tint Brush (Left-click darker, Right-click lighter)";

    /// <summary>
    /// Gets the keyboard shortcut key for activating this tool.
    /// </summary>
    public string Shortcut => "H";

    /// <summary>
    /// Gets the vector icon key for this tool.
    /// </summary>
    public string IconKey => "IconShadeBrush";

    /// <summary>
    /// Gets or sets whether the brush operates in primary mode (darker) or secondary mode (lighter).
    /// Defaults to <c>true</c> (darker).
    /// </summary>
    public bool IsPrimary
    {
        get => _isDarker;
        set => _isDarker = value;
    }

    /// <summary>
    /// Initiates a shading stroke with explicit primary (darker) / secondary (lighter) control.
    /// </summary>
    /// <param name="ctx">The active tool context.</param>
    /// <param name="x">The pointer X coordinate.</param>
    /// <param name="y">The pointer Y coordinate.</param>
    /// <param name="isPrimary"><c>true</c> for darker transition (Left-click); <c>false</c> for lighter transition (Right-click).</param>
    public void OnPointerDown(ToolContext ctx, int x, int y, bool isPrimary)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        _isDarker = isPrimary;
        _visited.Clear();
        _deltas.Clear();
        _isDrawing = true;
        _lastX = x;
        _lastY = y;

        PlotPoint(ctx, x, y);
    }

    /// <inheritdoc/>
    public void OnPointerDown(ToolContext ctx, int x, int y)
    {
        OnPointerDown(ctx, x, y, _isDarker);
    }

    /// <inheritdoc/>
    public void OnPointerMove(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!_isDrawing)
        {
            return;
        }

        if (x == _lastX && y == _lastY)
        {
            return;
        }

        DrawLine(ctx, _lastX, _lastY, x, y);
        _lastX = x;
        _lastY = y;
    }

    /// <inheritdoc/>
    public void OnPointerUp(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!_isDrawing)
        {
            return;
        }

        if (x != _lastX || y != _lastY)
        {
            DrawLine(ctx, _lastX, _lastY, x, y);
        }

        if (_deltas.Count > 0)
        {
            var command = new DrawPixelsCommand(ctx.ActiveLayer, _deltas.ToArray(), "Shade Brush Stroke");
            ctx.History.ExecuteCommand(command);
        }

        _visited.Clear();
        _deltas.Clear();
        _isDrawing = false;
    }

    /// <inheritdoc/>
    public IReadOnlyList<(int X, int Y, PixelColor Color)> GetPreviewPixels(ToolContext ctx)
    {
        return Array.Empty<(int X, int Y, PixelColor Color)>();
    }

    /// <summary>
    /// Computes the shaded/tinted color for the given target pixel color.
    /// If an active color ramp is configured in the context and contains the color,
    /// shifts one step darker or lighter along the ramp; otherwise performs an RGB brightness shift (-10% / +10%).
    /// </summary>
    /// <param name="ctx">The active tool context.</param>
    /// <param name="current">The current target pixel color.</param>
    /// <param name="isDarker"><c>true</c> to shift darker; <c>false</c> to shift lighter.</param>
    /// <returns>The shifted pixel color.</returns>
    public static PixelColor ShiftColor(ToolContext ctx, PixelColor current, bool isDarker)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (ctx.ActiveColorRamp is { Count: > 0 } ramp)
        {
            return ShiftRamp(ramp, current, isDarker);
        }

        return ShiftBrightness(current, isDarker);
    }

    /// <summary>
    /// Shifts the given color one step darker or lighter along the specified color ramp.
    /// Clamps at the boundaries of the ramp.
    /// If the color is not found in the ramp, falls back to <see cref="ShiftBrightness(PixelColor, bool)"/>.
    /// </summary>
    /// <param name="ramp">The active color ramp.</param>
    /// <param name="current">The current pixel color.</param>
    /// <param name="isDarker"><c>true</c> to shift darker; <c>false</c> to shift lighter.</param>
    /// <returns>The shifted pixel color.</returns>
    public static PixelColor ShiftRamp(IReadOnlyList<PixelColor> ramp, PixelColor current, bool isDarker)
    {
        ArgumentNullException.ThrowIfNull(ramp);

        if (ramp.Count == 0)
        {
            return ShiftBrightness(current, isDarker);
        }

        int index = -1;
        for (int i = 0; i < ramp.Count; i++)
        {
            if (ramp[i] == current)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            for (int i = 0; i < ramp.Count; i++)
            {
                if (ramp[i].R == current.R && ramp[i].G == current.G && ramp[i].B == current.B)
                {
                    index = i;
                    break;
                }
            }
        }

        if (index < 0)
        {
            return ShiftBrightness(current, isDarker);
        }

        float lumStart = GetLuminance(ramp[0]);
        float lumEnd = GetLuminance(ramp[^1]);

        int targetIndex;
        if (lumEnd >= lumStart)
        {
            targetIndex = isDarker
                ? Math.Max(0, index - 1)
                : Math.Min(ramp.Count - 1, index + 1);
        }
        else
        {
            targetIndex = isDarker
                ? Math.Min(ramp.Count - 1, index + 1)
                : Math.Max(0, index - 1);
        }

        var target = ramp[targetIndex];
        return new PixelColor(target.R, target.G, target.B, current.A);
    }

    /// <summary>
    /// Applies an RGB brightness shift (-10% for darker, +10% for lighter) to the specified color.
    /// Clamps each channel to [0, 255] and preserves alpha transparency.
    /// </summary>
    /// <param name="color">The source pixel color.</param>
    /// <param name="isDarker"><c>true</c> for -10% darker; <c>false</c> for +10% lighter.</param>
    /// <returns>The shifted pixel color.</returns>
    public static PixelColor ShiftBrightness(PixelColor color, bool isDarker)
    {
        float factor = isDarker ? 0.9f : 1.1f;
        byte r = (byte)Math.Clamp((int)Math.Round(color.R * factor), 0, 255);
        byte g = (byte)Math.Clamp((int)Math.Round(color.G * factor), 0, 255);
        byte b = (byte)Math.Clamp((int)Math.Round(color.B * factor), 0, 255);

        if (isDarker)
        {
            if (r == color.R && color.R > 0) r = (byte)(color.R - 1);
            if (g == color.G && color.G > 0) g = (byte)(color.G - 1);
            if (b == color.B && color.B > 0) b = (byte)(color.B - 1);
        }
        else
        {
            if (r == color.R && color.R < 255) r = (byte)(color.R == 0 ? 26 : color.R + 1);
            if (g == color.G && color.G < 255) g = (byte)(color.G == 0 ? 26 : color.G + 1);
            if (b == color.B && color.B < 255) b = (byte)(color.B == 0 ? 26 : color.B + 1);
        }

        return new PixelColor(r, g, b, color.A);
    }

    private static float GetLuminance(PixelColor c) => 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;

    private void DrawLine(ToolContext ctx, int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            PlotPoint(ctx, x0, y0);

            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private void PlotPoint(ToolContext ctx, int x, int y)
    {
        PlotSingle(ctx, x, y);

        if (!ctx.SymmetryX && !ctx.SymmetryY)
        {
            return;
        }

        int sx = ctx.SymmetryX ? ctx.GetSymmetricX(x) : x;
        int sy = ctx.SymmetryY ? ctx.GetSymmetricY(y) : y;

        bool validSx = ctx.SymmetryX && sx >= 0 && sx < ctx.Canvas.Width && sx != x;
        bool validSy = ctx.SymmetryY && sy >= 0 && sy < ctx.Canvas.Height && sy != y;

        if (validSx)
        {
            PlotSingle(ctx, sx, y);
        }

        if (validSy)
        {
            PlotSingle(ctx, x, sy);
        }

        if (validSx && validSy)
        {
            PlotSingle(ctx, sx, sy);
        }
    }

    private void PlotSingle(ToolContext ctx, int px, int py)
    {
        if ((uint)px >= (uint)ctx.Canvas.Width || (uint)py >= (uint)ctx.Canvas.Height)
        {
            return;
        }

        if (ctx.Selection.IsActive && !ctx.Selection.Contains(px, py))
        {
            return;
        }

        int index = py * ctx.Canvas.Width + px;
        if (_visited.Add(index))
        {
            var oldColor = ctx.ActiveLayer.GetPixel(px, py);
            if (ctx.ActiveLayer.IsAlphaLocked && oldColor.A == 0)
            {
                return;
            }

            var newColor = ShiftColor(ctx, oldColor, _isDarker);
            if (oldColor != newColor)
            {
                _deltas.Add(new PixelDelta(px, py, oldColor, newColor));
                ctx.ActiveLayer.SetPixel(px, py, newColor);
            }
        }
    }
}

using PixelForge.Core.History;
using PixelForge.Core.Models;

namespace PixelForge.Core.Tools;

/// <summary>
/// Continuous erasing tool that resets pixels to PixelColor.Transparent with Bresenham interpolation and symmetry.
/// </summary>
public class EraserTool : ITool
{
    private readonly HashSet<int> _visited = new();
    private readonly List<PixelDelta> _deltas = new();
    private bool _isErasing;
    private int _lastX;
    private int _lastY;

    public string Name => "Eraser";
    public string Shortcut => "E";
    public string IconKey => "IconEraser";

    public void OnPointerDown(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        _visited.Clear();
        _deltas.Clear();
        _isErasing = true;
        _lastX = x;
        _lastY = y;

        ErasePoint(ctx, x, y);
    }

    public void OnPointerMove(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!_isErasing)
        {
            return;
        }

        if (x == _lastX && y == _lastY)
        {
            return;
        }

        EraseLine(ctx, _lastX, _lastY, x, y);
        _lastX = x;
        _lastY = y;
    }

    public void OnPointerUp(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!_isErasing)
        {
            return;
        }

        if (x != _lastX || y != _lastY)
        {
            EraseLine(ctx, _lastX, _lastY, x, y);
        }

        if (_deltas.Count > 0)
        {
            var command = new DrawPixelsCommand(ctx.ActiveLayer, _deltas.ToArray(), "Erase Pixels");
            ctx.History.ExecuteCommand(command);
        }

        _visited.Clear();
        _deltas.Clear();
        _isErasing = false;
    }

    public IReadOnlyList<(int X, int Y, PixelColor Color)> GetPreviewPixels(ToolContext ctx)
    {
        return Array.Empty<(int X, int Y, PixelColor Color)>();
    }

    private void EraseLine(ToolContext ctx, int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            ErasePoint(ctx, x0, y0);

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

    private void ErasePoint(ToolContext ctx, int x, int y)
    {
        ctx.ForEachSymmetricPoint(x, y, (px, py) =>
        {
            int index = py * ctx.Canvas.Width + px;
            if (_visited.Add(index))
            {
                var oldColor = ctx.ActiveLayer.GetPixel(px, py);
                if (oldColor != PixelColor.Transparent)
                {
                    _deltas.Add(new PixelDelta(px, py, oldColor, PixelColor.Transparent));
                    ctx.ActiveLayer.SetPixel(px, py, PixelColor.Transparent);
                }
            }
        });
    }
}

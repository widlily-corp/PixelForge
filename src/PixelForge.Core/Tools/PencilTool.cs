using PixelForge.Core.History;
using PixelForge.Core.Models;

namespace PixelForge.Core.Tools;

/// <summary>
/// Continuous freehand drawing tool with Bresenham interpolation between pointer events and reflection symmetry.
/// </summary>
public class PencilTool : ITool
{
    private readonly HashSet<int> _visited = new();
    private readonly List<PixelDelta> _deltas = new();
    private bool _isDrawing;
    private int _lastX;
    private int _lastY;

    public string Name => "Pencil";
    public string Shortcut => "B";
    public string IconKey => "IconPencil";

    public void OnPointerDown(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        _visited.Clear();
        _deltas.Clear();
        _isDrawing = true;
        _lastX = x;
        _lastY = y;

        PlotPoint(ctx, x, y);
    }

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
            var command = new DrawPixelsCommand(ctx.ActiveLayer, _deltas.ToArray(), "Pencil Stroke");
            ctx.History.ExecuteCommand(command);
        }

        _visited.Clear();
        _deltas.Clear();
        _isDrawing = false;
    }

    public IReadOnlyList<(int X, int Y, PixelColor Color)> GetPreviewPixels(ToolContext ctx)
    {
        return Array.Empty<(int X, int Y, PixelColor Color)>();
    }

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
        ctx.ForEachSymmetricPoint(x, y, (px, py) =>
        {
            int index = py * ctx.Canvas.Width + px;
            if (_visited.Add(index))
            {
                var oldColor = ctx.ActiveLayer.GetPixel(px, py);
                if (oldColor != ctx.PrimaryColor)
                {
                    _deltas.Add(new PixelDelta(px, py, oldColor, ctx.PrimaryColor));
                    ctx.ActiveLayer.SetPixel(px, py, ctx.PrimaryColor);
                }
            }
        });
    }
}

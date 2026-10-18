using PixelForge.Core.History;
using PixelForge.Core.Models;

namespace PixelForge.Core.Tools;

/// <summary>
/// Line tool that renders rasterized line segments using Bresenham's algorithm with live preview and symmetry.
/// </summary>
public class LineTool : ITool
{
    private bool _isDragging;
    private int _startX;
    private int _startY;
    private int _currentX;
    private int _currentY;
    private List<(int X, int Y, PixelColor Color)> _previewPixels = new();

    public string Name => "Line";
    public string Shortcut => "L";
    public string IconKey => "IconLine";

    public void OnPointerDown(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        _isDragging = true;
        _startX = x;
        _startY = y;
        _currentX = x;
        _currentY = y;

        UpdatePreview(ctx);
    }

    public void OnPointerMove(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!_isDragging)
        {
            return;
        }

        _currentX = x;
        _currentY = y;
        UpdatePreview(ctx);
    }

    public void OnPointerUp(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!_isDragging)
        {
            return;
        }

        _currentX = x;
        _currentY = y;

        var deltas = new List<PixelDelta>();
        var visited = new HashSet<int>();

        BresenhamLine(_startX, _startY, _currentX, _currentY, (lx, ly) =>
        {
            ctx.ForEachSymmetricPoint(lx, ly, (px, py) =>
            {
                int index = py * ctx.Canvas.Width + px;
                if (visited.Add(index))
                {
                    var oldColor = ctx.ActiveLayer.GetPixel(px, py);
                    if (oldColor != ctx.PrimaryColor)
                    {
                        deltas.Add(new PixelDelta(px, py, oldColor, ctx.PrimaryColor));
                        ctx.ActiveLayer.SetPixel(px, py, ctx.PrimaryColor);
                    }
                }
            });
        });

        if (deltas.Count > 0)
        {
            var command = new DrawPixelsCommand(ctx.ActiveLayer, deltas.ToArray(), "Draw Line");
            ctx.History.ExecuteCommand(command);
        }

        _isDragging = false;
        _previewPixels.Clear();
    }

    public IReadOnlyList<(int X, int Y, PixelColor Color)> GetPreviewPixels(ToolContext ctx)
    {
        return _isDragging ? _previewPixels : Array.Empty<(int X, int Y, PixelColor Color)>();
    }

    private void UpdatePreview(ToolContext ctx)
    {
        var preview = new List<(int X, int Y, PixelColor Color)>();
        var visited = new HashSet<int>();

        BresenhamLine(_startX, _startY, _currentX, _currentY, (lx, ly) =>
        {
            ctx.ForEachSymmetricPoint(lx, ly, (px, py) =>
            {
                int index = py * ctx.Canvas.Width + px;
                if (visited.Add(index))
                {
                    preview.Add((px, py, ctx.PrimaryColor));
                }
            });
        });

        _previewPixels = preview;
    }

    private static void BresenhamLine(int x0, int y0, int x1, int y1, Action<int, int> plot)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            plot(x0, y0);

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
}

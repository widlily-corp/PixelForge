using PixelForge.Core.History;
using PixelForge.Core.Models;

namespace PixelForge.Core.Tools;

/// <summary>
/// Ellipse tool supporting midpoint rasterization for outline and filled modes, live preview, and symmetry.
/// </summary>
public class EllipseTool : ITool
{
    private bool _isDragging;
    private int _startX;
    private int _startY;
    private int _currentX;
    private int _currentY;
    private List<(int X, int Y, PixelColor Color)> _previewPixels = new();

    public string Name => "Ellipse";
    public string Shortcut => "C";
    public string IconKey => "IconEllipse";

    /// <summary>
    /// Gets or sets whether to render a solid filled ellipse or an outline.
    /// </summary>
    public bool IsFilled { get; set; }

    public EllipseTool(bool isFilled = false)
    {
        IsFilled = isFilled;
    }

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

        GenerateEllipsePoints(_startX, _startY, _currentX, _currentY, IsFilled, (ex, ey) =>
        {
            ctx.ForEachSymmetricPoint(ex, ey, (px, py) =>
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
            var command = new DrawPixelsCommand(ctx.ActiveLayer, deltas.ToArray(), IsFilled ? "Draw Filled Ellipse" : "Draw Ellipse Outline");
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

        GenerateEllipsePoints(_startX, _startY, _currentX, _currentY, IsFilled, (ex, ey) =>
        {
            ctx.ForEachSymmetricPoint(ex, ey, (px, py) =>
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

    private static void GenerateEllipsePoints(int x0, int y0, int x1, int y1, bool isFilled, Action<int, int> plot)
    {
        int minX = Math.Min(x0, x1);
        int maxX = Math.Max(x0, x1);
        int minY = Math.Min(y0, y1);
        int maxY = Math.Max(y0, y1);

        if (minX == maxX)
        {
            for (int y = minY; y <= maxY; y++)
            {
                plot(minX, y);
            }
            return;
        }

        if (minY == maxY)
        {
            for (int x = minX; x <= maxX; x++)
            {
                plot(x, minY);
            }
            return;
        }

        var outlinePoints = new HashSet<(int X, int Y)>();
        var rowExtents = new Dictionary<int, (int MinX, int MaxX)>();

        void AddPoint(int px, int py)
        {
            outlinePoints.Add((px, py));
            if (rowExtents.TryGetValue(py, out var extent))
            {
                rowExtents[py] = (Math.Min(extent.MinX, px), Math.Max(extent.MaxX, px));
            }
            else
            {
                rowExtents[py] = (px, px);
            }
        }

        // Alois Zingl Midpoint Ellipse Algorithm
        long a = Math.Abs(maxX - minX);
        long b = Math.Abs(maxY - minY);
        long b1 = b & 1;
        long dx = 4 * (1 - a) * b * b;
        long dy = 4 * (b1 + 1) * a * a;
        long err = dx + dy + b1 * a * a;
        long e2;

        int curX0 = minX;
        int curY0 = minY + (int)((b + 1) / 2);
        int curX1 = maxX;
        int curY1 = curY0 - (int)b1;

        a *= 8 * a;
        b1 = 8 * b * b;

        do
        {
            AddPoint(curX1, curY0);
            AddPoint(curX0, curY0);
            AddPoint(curX0, curY1);
            AddPoint(curX1, curY1);

            e2 = 2 * err;
            if (e2 <= dy)
            {
                curY0++;
                curY1--;
                err += dy += a;
            }

            if (e2 >= dx || 2 * err > dy)
            {
                curX0++;
                curX1--;
                err += dx += b1;
            }
        } while (curX0 <= curX1);

        while (curY0 - curY1 <= b)
        {
            AddPoint(curX0 - 1, curY0);
            AddPoint(curX1 + 1, curY0++);
            AddPoint(curX0 - 1, curY1);
            AddPoint(curX1 + 1, curY1--);
        }

        if (isFilled)
        {
            foreach (var (rowY, (rowMinX, rowMaxX)) in rowExtents)
            {
                for (int px = rowMinX; px <= rowMaxX; px++)
                {
                    plot(px, rowY);
                }
            }
        }
        else
        {
            foreach (var (px, py) in outlinePoints)
            {
                plot(px, py);
            }
        }
    }
}

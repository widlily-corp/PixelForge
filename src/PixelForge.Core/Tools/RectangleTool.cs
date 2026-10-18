using PixelForge.Core.History;
using PixelForge.Core.Models;

namespace PixelForge.Core.Tools;

/// <summary>
/// Rectangle drawing tool supporting both outline and solid filled modes, with live preview and symmetry.
/// </summary>
public class RectangleTool : ITool
{
    private bool _isDragging;
    private int _startX;
    private int _startY;
    private int _currentX;
    private int _currentY;
    private List<(int X, int Y, PixelColor Color)> _previewPixels = new();

    public string Name => "Rectangle";
    public string Shortcut => "U";
    public string IconKey => "IconRectangle";

    /// <summary>
    /// Gets or sets whether to draw a filled rectangle or an outline.
    /// </summary>
    public bool IsFilled { get; set; }

    public RectangleTool(bool isFilled = false)
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

        GenerateRectanglePoints(_startX, _startY, _currentX, _currentY, IsFilled, (rx, ry) =>
        {
            ctx.ForEachSymmetricPoint(rx, ry, (px, py) =>
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
            var command = new DrawPixelsCommand(ctx.ActiveLayer, deltas.ToArray(), IsFilled ? "Draw Filled Rectangle" : "Draw Rectangle Outline");
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

        GenerateRectanglePoints(_startX, _startY, _currentX, _currentY, IsFilled, (rx, ry) =>
        {
            ctx.ForEachSymmetricPoint(rx, ry, (px, py) =>
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

    private static void GenerateRectanglePoints(int x0, int y0, int x1, int y1, bool isFilled, Action<int, int> plot)
    {
        int minX = Math.Min(x0, x1);
        int maxX = Math.Max(x0, x1);
        int minY = Math.Min(y0, y1);
        int maxY = Math.Max(y0, y1);

        if (isFilled)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    plot(x, y);
                }
            }
        }
        else
        {
            for (int x = minX; x <= maxX; x++)
            {
                plot(x, minY);
                if (minY != maxY)
                {
                    plot(x, maxY);
                }
            }

            for (int y = minY + 1; y < maxY; y++)
            {
                plot(minX, y);
                if (minX != maxX)
                {
                    plot(maxX, y);
                }
            }
        }
    }
}

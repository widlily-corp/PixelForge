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
    private readonly List<(int X, int Y, PixelColor Color)> _previewPixels = new();
    private readonly HashSet<int> _previewVisited = new();
    private readonly List<PixelDelta> _deltas = new();
    private readonly HashSet<int> _drawVisited = new();

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

        _deltas.Clear();
        _drawVisited.Clear();

        DrawRectanglePoints(ctx, _startX, _startY, _currentX, _currentY, IsFilled, isPreview: false);

        if (_deltas.Count > 0)
        {
            var command = new DrawPixelsCommand(ctx.ActiveLayer, _deltas.ToArray(), IsFilled ? "Draw Filled Rectangle" : "Draw Rectangle Outline");
            ctx.History.ExecuteCommand(command);
        }

        _isDragging = false;
        _previewPixels.Clear();
        _previewVisited.Clear();
        _deltas.Clear();
        _drawVisited.Clear();
    }

    public IReadOnlyList<(int X, int Y, PixelColor Color)> GetPreviewPixels(ToolContext ctx)
    {
        return _isDragging ? _previewPixels : Array.Empty<(int X, int Y, PixelColor Color)>();
    }

    private void UpdatePreview(ToolContext ctx)
    {
        _previewPixels.Clear();
        _previewVisited.Clear();

        DrawRectanglePoints(ctx, _startX, _startY, _currentX, _currentY, IsFilled, isPreview: true);
    }

    private void DrawRectanglePoints(ToolContext ctx, int x0, int y0, int x1, int y1, bool isFilled, bool isPreview)
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
                    PlotPoint(ctx, x, y, isPreview);
                }
            }
        }
        else
        {
            for (int x = minX; x <= maxX; x++)
            {
                PlotPoint(ctx, x, minY, isPreview);
                if (minY != maxY)
                {
                    PlotPoint(ctx, x, maxY, isPreview);
                }
            }

            for (int y = minY + 1; y < maxY; y++)
            {
                PlotPoint(ctx, minX, y, isPreview);
                if (minX != maxX)
                {
                    PlotPoint(ctx, maxX, y, isPreview);
                }
            }
        }
    }

    private void PlotPoint(ToolContext ctx, int x, int y, bool isPreview)
    {
        PlotSingle(ctx, x, y, isPreview);

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
            PlotSingle(ctx, sx, y, isPreview);
        }

        if (validSy)
        {
            PlotSingle(ctx, x, sy, isPreview);
        }

        if (validSx && validSy)
        {
            PlotSingle(ctx, sx, sy, isPreview);
        }
    }

    private void PlotSingle(ToolContext ctx, int px, int py, bool isPreview)
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
        if (isPreview)
        {
            if (_previewVisited.Add(index))
            {
                if (ctx.ActiveLayer.IsAlphaLocked && ctx.ActiveLayer.GetPixel(px, py).A == 0)
                {
                    return;
                }

                _previewPixels.Add((px, py, ctx.PrimaryColor));
            }
        }
        else
        {
            if (_drawVisited.Add(index))
            {
                var oldColor = ctx.ActiveLayer.GetPixel(px, py);
                if (ctx.ActiveLayer.IsAlphaLocked && oldColor.A == 0)
                {
                    return;
                }

                if (oldColor != ctx.PrimaryColor)
                {
                    _deltas.Add(new PixelDelta(px, py, oldColor, ctx.PrimaryColor));
                    ctx.ActiveLayer.SetPixel(px, py, ctx.PrimaryColor);
                }
            }
        }
    }
}

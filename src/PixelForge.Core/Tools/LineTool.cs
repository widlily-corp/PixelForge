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
    private readonly List<(int X, int Y, PixelColor Color)> _previewPixels = new();
    private readonly HashSet<int> _previewVisited = new();
    private readonly List<PixelDelta> _deltas = new();
    private readonly HashSet<int> _drawVisited = new();

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

        _deltas.Clear();
        _drawVisited.Clear();

        DrawLinePoints(ctx, _startX, _startY, _currentX, _currentY, isPreview: false);

        if (_deltas.Count > 0)
        {
            var command = new DrawPixelsCommand(ctx.ActiveLayer, _deltas.ToArray(), "Draw Line");
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

        DrawLinePoints(ctx, _startX, _startY, _currentX, _currentY, isPreview: true);
    }

    private void DrawLinePoints(ToolContext ctx, int x0, int y0, int x1, int y1, bool isPreview)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            PlotPoint(ctx, x0, y0, isPreview);

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

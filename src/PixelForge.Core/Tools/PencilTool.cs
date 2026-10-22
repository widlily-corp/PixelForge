using PixelForge.Core.History;
using PixelForge.Core.Models;

namespace PixelForge.Core.Tools;

/// <summary>
/// Continuous freehand drawing tool with Bresenham interpolation between pointer events, reflection symmetry,
/// and optional Pixel-Perfect jaggie elimination.
/// </summary>
public class PencilTool : ITool
{
    private readonly HashSet<int> _visited = new();
    private readonly List<PixelDelta> _deltas = new();
    private readonly List<StrokeStep> _steps = new();
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
        _steps.Clear();
        _isDrawing = true;
        _lastX = x;
        _lastY = y;

        DrawStep(ctx, x, y);
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
        _steps.Clear();
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
            DrawStep(ctx, x0, y0);

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

    private void DrawStep(ToolContext ctx, int x, int y)
    {
        if (ctx.IsPixelPerfect)
        {
            if (_steps.Count > 0 && _steps[^1].X == x && _steps[^1].Y == y)
            {
                return;
            }

            if (_steps.Count >= 2)
            {
                var p0 = _steps[^2];
                var p1 = _steps[^1];

                if (PixelPerfectHelper.IsCorner(p0.X, p0.Y, p1.X, p1.Y, x, y))
                {
                    RollbackStep(ctx, p1);
                    _steps.RemoveAt(_steps.Count - 1);
                }
            }
        }

        int deltaStart = _deltas.Count;
        PlotPoint(ctx, x, y);

        if (ctx.IsPixelPerfect)
        {
            _steps.Add(new StrokeStep(x, y, deltaStart));
        }
    }

    private void RollbackStep(ToolContext ctx, StrokeStep step)
    {
        RollbackSingle(ctx, step.X, step.Y);

        if (ctx.SymmetryX || ctx.SymmetryY)
        {
            int sx = ctx.SymmetryX ? ctx.GetSymmetricX(step.X) : step.X;
            int sy = ctx.SymmetryY ? ctx.GetSymmetricY(step.Y) : step.Y;

            bool validSx = ctx.SymmetryX && sx >= 0 && sx < ctx.Canvas.Width && sx != step.X;
            bool validSy = ctx.SymmetryY && sy >= 0 && sy < ctx.Canvas.Height && sy != step.Y;

            if (validSx)
            {
                RollbackSingle(ctx, sx, step.Y);
            }

            if (validSy)
            {
                RollbackSingle(ctx, step.X, sy);
            }

            if (validSx && validSy)
            {
                RollbackSingle(ctx, sx, sy);
            }
        }

        for (int i = _deltas.Count - 1; i >= step.DeltaStartIndex; i--)
        {
            var delta = _deltas[i];
            ctx.ActiveLayer.SetPixel(delta.X, delta.Y, delta.OldColor);
            _deltas.RemoveAt(i);
        }
    }

    private void RollbackSingle(ToolContext ctx, int px, int py)
    {
        if ((uint)px < (uint)ctx.Canvas.Width && (uint)py < (uint)ctx.Canvas.Height)
        {
            _visited.Remove(py * ctx.Canvas.Width + px);
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
            if (oldColor != ctx.PrimaryColor)
            {
                _deltas.Add(new PixelDelta(px, py, oldColor, ctx.PrimaryColor));
                ctx.ActiveLayer.SetPixel(px, py, ctx.PrimaryColor);
            }
        }
    }

    private readonly struct StrokeStep(int x, int y, int deltaStartIndex)
    {
        public int X { get; } = x;
        public int Y { get; } = y;
        public int DeltaStartIndex { get; } = deltaStartIndex;
    }
}

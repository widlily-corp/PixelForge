using PixelForge.Core.Algorithms;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Patterns;

namespace PixelForge.Core.Tools;

/// <summary>
/// Freehand pattern brush tool that applies 8x8 bitmap pattern textures
/// with support for brush sizing, reflection symmetry, stroke stabilization, and pixel-perfect line cleanup.
/// </summary>
public class PatternBrushTool : ITool
{
    private readonly HashSet<int> _visited = new();
    private readonly List<PixelDelta> _deltas = new();
    private readonly List<StrokeStep> _steps = new();
    private readonly StrokeStabilizer _stabilizer = new();
    private readonly Action<int, int> _symmetricPlotDelegate;

    private ToolContext? _currentCtx;
    private bool _isDrawing;
    private int _lastX;
    private int _lastY;
    private int _brushSize = 1;

    public string Name => "Pattern Brush";
    public string Shortcut => "W";
    public string IconKey => "IconPatternBrush";

    /// <summary>
    /// Gets or sets the 8x8 pattern preset mask used for painting.
    /// </summary>
    public PatternPreset Pattern { get; set; } = PatternPresets.Checkerboard;

    /// <summary>
    /// Gets or sets the brush footprint diameter/extent in pixels (>= 1).
    /// </summary>
    public int BrushSize
    {
        get => _brushSize;
        set => _brushSize = Math.Max(1, value);
    }

    /// <summary>
    /// Gets or sets whether unset (0) pattern bits are painted using the secondary color.
    /// When <c>false</c>, unset bits are transparent/unmodified.
    /// </summary>
    public bool UseSecondaryColor { get; set; }

    /// <summary>
    /// Gets or sets whether pixel-perfect line cleanup is enabled (removes L-corner jaggies for 1px brush).
    /// </summary>
    public bool PixelPerfect { get; set; }

    /// <summary>
    /// Gets the stroke stabilizer providing exponential mouse smoothing.
    /// </summary>
    public StrokeStabilizer Stabilizer => _stabilizer;

    /// <summary>
    /// Gets or sets the stroke stabilizer smoothing strength (0 to 10).
    /// </summary>
    public int StabilizerStrength
    {
        get => _stabilizer.Strength;
        set => _stabilizer.Strength = value;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PatternBrushTool"/> with default settings.
    /// </summary>
    public PatternBrushTool()
    {
        _symmetricPlotDelegate = PlotSymmetricPoint;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PatternBrushTool"/> with the specified pattern and brush size.
    /// </summary>
    /// <param name="pattern">The initial 8x8 pattern preset.</param>
    /// <param name="brushSize">Initial brush size in pixels.</param>
    public PatternBrushTool(PatternPreset pattern, int brushSize = 1) : this()
    {
        Pattern = pattern;
        BrushSize = brushSize;
    }

    /// <inheritdoc/>
    public void OnPointerDown(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        _currentCtx = ctx;
        _visited.Clear();
        _deltas.Clear();
        _steps.Clear();
        _isDrawing = true;

        _stabilizer.Reset();
        if (_stabilizer.Strength > 0)
        {
            (x, y) = _stabilizer.Process(x, y);
        }

        _lastX = x;
        _lastY = y;

        DrawStep(x, y);
    }

    /// <inheritdoc/>
    public void OnPointerMove(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!_isDrawing)
        {
            return;
        }

        _currentCtx = ctx;

        if (_stabilizer.Strength > 0)
        {
            (x, y) = _stabilizer.Process(x, y);
        }

        if (x == _lastX && y == _lastY)
        {
            return;
        }

        DrawLine(_lastX, _lastY, x, y);
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

        _currentCtx = ctx;

        if (_stabilizer.Strength > 0)
        {
            (x, y) = _stabilizer.Process(x, y);
        }

        if (x != _lastX || y != _lastY)
        {
            DrawLine(_lastX, _lastY, x, y);
        }

        if (_deltas.Count > 0)
        {
            var command = new DrawPixelsCommand(ctx.ActiveLayer, _deltas.ToArray(), "Pattern Brush Stroke");
            ctx.History.ExecuteCommand(command);
        }

        _visited.Clear();
        _deltas.Clear();
        _steps.Clear();
        _isDrawing = false;
        _currentCtx = null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<(int X, int Y, PixelColor Color)> GetPreviewPixels(ToolContext ctx)
    {
        return Array.Empty<(int X, int Y, PixelColor Color)>();
    }

    private void DrawLine(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
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

            DrawStep(x0, y0);

            if (x0 == x1 && y0 == y1)
            {
                break;
            }
        }
    }

    private void DrawStep(int x, int y)
    {
        if (PixelPerfect && _brushSize == 1)
        {
            if (_steps.Count >= 2)
            {
                var p0 = _steps[^2];
                var p1 = _steps[^1];

                if (PixelPerfectAlgorithm.IsCornerJaggie(p0.X, p0.Y, p1.X, p1.Y, x, y))
                {
                    RollbackStep(p1);
                    _steps.RemoveAt(_steps.Count - 1);
                }
            }
        }

        int deltaStart = _deltas.Count;
        PlotBrushFootprint(x, y);

        if (PixelPerfect && _brushSize == 1)
        {
            _steps.Add(new StrokeStep(x, y, deltaStart));
        }
    }

    private void RollbackStep(StrokeStep step)
    {
        for (int i = _deltas.Count - 1; i >= step.DeltaStartIndex; i--)
        {
            var delta = _deltas[i];
            _currentCtx!.ActiveLayer.SetPixel(delta.X, delta.Y, delta.OldColor);
            _visited.Remove(delta.Y * _currentCtx.Canvas.Width + delta.X);
            _deltas.RemoveAt(i);
        }
    }

    private void PlotBrushFootprint(int centerX, int centerY)
    {
        if (_brushSize == 1)
        {
            _currentCtx!.ForEachSymmetricPoint(centerX, centerY, _symmetricPlotDelegate);
            return;
        }

        int radius = (_brushSize - 1) / 2;
        int startX = centerX - radius;
        int endX = startX + _brushSize;
        int startY = centerY - radius;
        int endY = startY + _brushSize;

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                _currentCtx!.ForEachSymmetricPoint(x, y, _symmetricPlotDelegate);
            }
        }
    }

    private void PlotSymmetricPoint(int px, int py)
    {
        if (_currentCtx!.Selection.IsActive && !_currentCtx.Selection.Contains(px, py))
        {
            return;
        }

        int index = py * _currentCtx.Canvas.Width + px;
        if (_visited.Contains(index))
        {
            return;
        }

        bool isSet = Pattern.IsPixelSet(px, py);
        if (!isSet && !UseSecondaryColor)
        {
            return;
        }

        _visited.Add(index);
        var newColor = isSet ? _currentCtx.PrimaryColor : _currentCtx.SecondaryColor;
        var oldColor = _currentCtx.ActiveLayer.GetPixel(px, py);

        if (oldColor != newColor)
        {
            _deltas.Add(new PixelDelta(px, py, oldColor, newColor));
            _currentCtx.ActiveLayer.SetPixel(px, py, newColor);
        }
    }

    private readonly struct StrokeStep
    {
        public int X { get; }
        public int Y { get; }
        public int DeltaStartIndex { get; }

        public StrokeStep(int x, int y, int deltaStartIndex)
        {
            X = x;
            Y = y;
            DeltaStartIndex = deltaStartIndex;
        }
    }
}

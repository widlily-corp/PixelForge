using PixelForge.Core.History;
using PixelForge.Core.Models;

namespace PixelForge.Core.Tools;

/// <summary>
/// Freehand noise and texture spray brush tool.
/// Scatters pixels within a circular spray radius according to a density probability threshold.
/// </summary>
public class SprayBrushTool : ITool
{
    private readonly HashSet<int> _visited = new();
    private readonly List<PixelDelta> _deltas = new();
    private readonly Random _random;

    private int _radius = 4;
    private bool _radiusExplicitlySet;
    private float _density = 0.25f;
    private bool _densityExplicitlySet;
    private bool _isDrawing;
    private int _lastX;
    private int _lastY;

    /// <summary>
    /// Gets the display name of the tool.
    /// </summary>
    public string Name => "Spray Brush";

    /// <summary>
    /// Gets the tooltip explaining tool behavior.
    /// </summary>
    public string Tooltip => "Texture Spray / Noise Brush";

    /// <summary>
    /// Gets the keyboard shortcut key for activating this tool.
    /// </summary>
    public string Shortcut => "A";

    /// <summary>
    /// Gets the vector icon key for this tool.
    /// </summary>
    public string IconKey => "IconSprayBrush";

    /// <summary>
    /// Gets or sets the spray scatter radius in pixels (clamped between 1 and 16).
    /// </summary>
    public int Radius
    {
        get => _radius;
        set
        {
            _radius = Math.Clamp(value, 1, 16);
            _radiusExplicitlySet = true;
        }
    }

    /// <summary>
    /// Gets or sets the spray particle density probability (clamped between 0.0 and 1.0).
    /// </summary>
    public float Density
    {
        get => _density;
        set
        {
            _density = Math.Clamp(value, 0f, 1f);
            _densityExplicitlySet = true;
        }
    }

    /// <summary>
    /// Gets or sets whether the brush paints with the primary color (true) or secondary color (false).
    /// </summary>
    public bool IsPrimary { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of <see cref="SprayBrushTool"/> with an optional random seed.
    /// </summary>
    /// <param name="seed">Optional seed for deterministic pseudo-random scattering (useful in tests).</param>
    public SprayBrushTool(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Initiates a spray stroke with explicit primary / secondary color selection.
    /// </summary>
    /// <param name="ctx">The active tool context.</param>
    /// <param name="x">The pointer X coordinate.</param>
    /// <param name="y">The pointer Y coordinate.</param>
    /// <param name="isPrimary"><c>true</c> to use primary color; <c>false</c> for secondary color.</param>
    public void OnPointerDown(ToolContext ctx, int x, int y, bool isPrimary)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        IsPrimary = isPrimary;
        _visited.Clear();
        _deltas.Clear();
        _isDrawing = true;
        _lastX = x;
        _lastY = y;

        SprayAt(ctx, x, y);
    }

    /// <inheritdoc/>
    public void OnPointerDown(ToolContext ctx, int x, int y)
    {
        OnPointerDown(ctx, x, y, IsPrimary);
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

        int dx = Math.Abs(x - _lastX);
        int sx = _lastX < x ? 1 : -1;
        int dy = -Math.Abs(y - _lastY);
        int sy = _lastY < y ? 1 : -1;
        int err = dx + dy;
        int curX = _lastX;
        int curY = _lastY;
        int stepDist = 0;
        int interval = Math.Max(1, GetRadius(ctx) / 2);

        while (true)
        {
            if (curX == x && curY == y)
            {
                SprayAt(ctx, curX, curY);
                break;
            }

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                curX += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                curY += sy;
            }

            stepDist++;
            if (stepDist >= interval)
            {
                SprayAt(ctx, curX, curY);
                stepDist = 0;
            }
        }

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
            SprayAt(ctx, x, y);
        }

        if (_deltas.Count > 0)
        {
            var command = new DrawPixelsCommand(ctx.ActiveLayer, _deltas.ToArray(), "Spray Brush Stroke");
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

    private int GetRadius(ToolContext ctx)
    {
        return _radiusExplicitlySet ? _radius : Math.Clamp(ctx.SprayRadius, 1, 16);
    }

    private float GetDensity(ToolContext ctx)
    {
        return _densityExplicitlySet ? _density : Math.Clamp(ctx.SprayDensity, 0f, 1f);
    }

    private void SprayAt(ToolContext ctx, int cx, int cy)
    {
        int radius = GetRadius(ctx);
        float density = GetDensity(ctx);
        if (density <= 0f)
        {
            return;
        }

        var drawColor = IsPrimary ? ctx.PrimaryColor : ctx.SecondaryColor;
        int r2 = radius * radius;
        int w = ctx.Canvas.Width;
        int h = ctx.Canvas.Height;

        for (int dy = -radius; dy <= radius; dy++)
        {
            int py = cy + dy;
            if ((uint)py >= (uint)h)
            {
                continue;
            }

            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > r2)
                {
                    continue;
                }

                int px = cx + dx;
                if ((uint)px >= (uint)w)
                {
                    continue;
                }

                if (density >= 1.0f || _random.NextSingle() < density)
                {
                    PlotSymmetric(ctx, px, py, drawColor);
                }
            }
        }
    }

    private void PlotSymmetric(ToolContext ctx, int x, int y, PixelColor color)
    {
        PlotSingle(ctx, x, y, color);

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
            PlotSingle(ctx, sx, y, color);
        }

        if (validSy)
        {
            PlotSingle(ctx, x, sy, color);
        }

        if (validSx && validSy)
        {
            PlotSingle(ctx, sx, sy, color);
        }
    }

    private void PlotSingle(ToolContext ctx, int px, int py, PixelColor color)
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

            if (oldColor != color)
            {
                _deltas.Add(new PixelDelta(px, py, oldColor, color));
                ctx.ActiveLayer.SetPixel(px, py, color);
            }
        }
    }
}

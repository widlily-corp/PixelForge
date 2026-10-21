using PixelForge.Core.History;
using PixelForge.Core.Models;

namespace PixelForge.Core.Tools;

/// <summary>
/// Type of gradient transition.
/// </summary>
public enum GradientType
{
    Linear,
    Radial
}

/// <summary>
/// Tool for drawing linear and radial retro dithered or smooth pixel gradients.
/// </summary>
public class GradientTool : ITool
{
    private bool _isDragging;
    private int _startX;
    private int _startY;
    private int _endX;
    private int _endY;
    private readonly List<(int X, int Y, PixelColor Color)> _previewList = new();

    public string Name => "Gradient";
    public string Shortcut => "T";
    public string IconKey => "IconGradient";

    public GradientType Type { get; set; } = GradientType.Linear;

    /// <summary>
    /// Optional sequence of color stops for multi-stop gradients. If empty, uses Primary and Secondary colors.
    /// </summary>
    public IReadOnlyList<GradientStop> Stops { get; set; } = Array.Empty<GradientStop>();

    public void OnPointerDown(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        _isDragging = true;
        _startX = x;
        _startY = y;
        _endX = x;
        _endY = y;
    }

    public void OnPointerMove(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!_isDragging)
        {
            return;
        }

        _endX = x;
        _endY = y;
    }

    public void OnPointerUp(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!_isDragging)
        {
            return;
        }

        _endX = x;
        _endY = y;
        _isDragging = false;

        ApplyGradient(ctx);
    }

    public IReadOnlyList<(int X, int Y, PixelColor Color)> GetPreviewPixels(ToolContext ctx)
    {
        if (!_isDragging)
        {
            return Array.Empty<(int X, int Y, PixelColor Color)>();
        }

        // Return empty for lightweight zero-allocation - visual overlay is handled in hardware viewport
        return _previewList;
    }

    private void ApplyGradient(ToolContext ctx)
    {
        int w = ctx.Canvas.Width;
        int h = ctx.Canvas.Height;
        var selection = ctx.Selection;

        int minX = selection.IsActive ? Math.Max(0, selection.Bounds.Left) : 0;
        int maxX = selection.IsActive ? Math.Min(w, selection.Bounds.Right) : w;
        int minY = selection.IsActive ? Math.Max(0, selection.Bounds.Top) : 0;
        int maxY = selection.IsActive ? Math.Min(h, selection.Bounds.Bottom) : h;

        float dx = _endX - _startX;
        float dy = _endY - _startY;
        float lengthSq = dx * dx + dy * dy;
        float radius = MathF.Sqrt(lengthSq);

        var deltas = new List<PixelDelta>();
        var pattern = ctx.ActiveDitherPattern;

        for (int py = minY; py < maxY; py++)
        {
            for (int px = minX; px < maxX; px++)
            {
                if (selection.IsActive && !selection.Contains(px, py))
                {
                    continue;
                }

                float t;
                if (Type == GradientType.Linear)
                {
                    if (lengthSq <= 0.0001f)
                    {
                        t = 0f;
                    }
                    else
                    {
                        float uX = px - _startX;
                        float uY = py - _startY;
                        t = Math.Clamp((uX * dx + uY * dy) / lengthSq, 0f, 1f);
                    }
                }
                else // Radial
                {
                    if (radius <= 0.0001f)
                    {
                        t = 0f;
                    }
                    else
                    {
                        float dist = MathF.Sqrt((px - _startX) * (px - _startX) + (py - _startY) * (py - _startY));
                        t = Math.Clamp(dist / radius, 0f, 1f);
                    }
                }

                PixelColor newColor = EvaluateColor(Stops, t, pattern, px, py, ctx.PrimaryColor, ctx.SecondaryColor);

                var oldColor = ctx.ActiveLayer.GetPixel(px, py);
                if (oldColor != newColor)
                {
                    deltas.Add(new PixelDelta(px, py, oldColor, newColor));
                    ctx.ActiveLayer.SetPixel(px, py, newColor);
                }
            }
        }

        if (deltas.Count > 0)
        {
            var command = new DrawPixelsCommand(ctx.ActiveLayer, deltas.ToArray(), "Gradient Fill");
            ctx.History.ExecuteCommand(command);
        }
    }

    /// <summary>
    /// Evaluates the color at position t along the gradient vector with optional dithering and multi-stop support.
    /// </summary>
    public static PixelColor EvaluateColor(
        IReadOnlyList<GradientStop>? stops,
        float t,
        DitherPattern pattern,
        int px,
        int py,
        PixelColor fallbackPrimary,
        PixelColor fallbackSecondary)
    {
        t = Math.Clamp(t, 0f, 1f);

        // If no stops configured, fallback to 2-stop Primary -> Secondary
        if (stops == null || stops.Count == 0)
        {
            if (pattern == DitherPattern.None)
            {
                return PixelColor.Lerp(fallbackPrimary, fallbackSecondary, t);
            }

            bool useSecondary = DitherHelper.ShouldPlot(px, py, pattern, t);
            return useSecondary ? fallbackSecondary : fallbackPrimary;
        }

        if (stops.Count == 1)
        {
            return stops[0].Color;
        }

        if (t <= stops[0].Position)
        {
            return stops[0].Color;
        }

        if (t >= stops[^1].Position)
        {
            return stops[^1].Color;
        }

        // Find surrounding interval [k, k+1]
        int k = 0;
        for (int i = 0; i < stops.Count - 1; i++)
        {
            if (t >= stops[i].Position && t <= stops[i + 1].Position)
            {
                k = i;
                break;
            }
        }

        var stopA = stops[k];
        var stopB = stops[k + 1];
        float span = stopB.Position - stopA.Position;
        float localT = span > 0.0001f ? Math.Clamp((t - stopA.Position) / span, 0f, 1f) : 0f;

        if (pattern == DitherPattern.None)
        {
            return PixelColor.Lerp(stopA.Color, stopB.Color, localT);
        }

        bool useSecond = DitherHelper.ShouldPlot(px, py, pattern, localT);
        return useSecond ? stopB.Color : stopA.Color;
    }
}

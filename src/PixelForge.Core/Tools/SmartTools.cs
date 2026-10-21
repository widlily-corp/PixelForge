using PixelForge.Core.History;
using PixelForge.Core.Models;

namespace PixelForge.Core.Tools;

/// <summary>
/// Provides algorithmic pixel operations including smart outlines, shadows, and color replacement.
/// </summary>
public static class SmartTools
{
    /// <summary>
    /// Generates a pixel outline around non-transparent pixels on the layer (or active selection).
    /// </summary>
    /// <param name="layer">The target layer to modify.</param>
    /// <param name="outlineColor">The color of the outline pixels.</param>
    /// <param name="is8Way">If true, uses 8-directional (including diagonal) connectivity; otherwise 4-directional.</param>
    /// <param name="thickness">The thickness of the outline in pixels (>= 1).</param>
    /// <param name="selection">Optional selection model restricting the target shape.</param>
    /// <returns>A list of pixel modifications performed on the layer.</returns>
    public static IReadOnlyList<PixelDelta> GenerateOutline(
        Layer layer,
        PixelColor outlineColor,
        bool is8Way = false,
        int thickness = 1,
        SelectionModel? selection = null)
    {
        var deltas = ComputeOutlineDeltas(layer, outlineColor, is8Way, thickness, selection);
        ApplyDeltas(layer, deltas);
        return deltas;
    }

    /// <summary>
    /// Computes pixel deltas for generating an outline without modifying the layer.
    /// </summary>
    public static IReadOnlyList<PixelDelta> ComputeOutlineDeltas(
        Layer layer,
        PixelColor outlineColor,
        bool is8Way = false,
        int thickness = 1,
        SelectionModel? selection = null)
    {
        ArgumentNullException.ThrowIfNull(layer);

        int w = layer.Width;
        int h = layer.Height;
        int totalPixels = w * h;
        int clampedThickness = Math.Max(1, thickness);
        bool hasSelection = selection != null && selection.IsActive;

        var isShape = new bool[totalPixels];
        bool anyShape = false;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (layer.Pixels[idx].A > 0)
                {
                    if (!hasSelection || selection!.Contains(x, y))
                    {
                        isShape[idx] = true;
                        anyShape = true;
                    }
                }
            }
        }

        if (!anyShape)
        {
            return Array.Empty<PixelDelta>();
        }

        var isOutline = new bool[totalPixels];
        var queue = new Queue<(int X, int Y, int Dist)>();

        ReadOnlySpan<int> dxList = is8Way
            ? [-1, 0, 1, -1, 1, -1, 0, 1]
            : [0, -1, 1, 0];
        ReadOnlySpan<int> dyList = is8Way
            ? [-1, -1, -1, 0, 0, 1, 1, 1]
            : [-1, 0, 0, 1];

        // Seed 1st layer of outline adjacent to shape
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (isShape[idx])
                {
                    continue;
                }

                bool hasShapeNeighbor = false;
                for (int d = 0; d < dxList.Length; d++)
                {
                    int nx = x + dxList[d];
                    int ny = y + dyList[d];
                    if ((uint)nx < (uint)w && (uint)ny < (uint)h)
                    {
                        if (isShape[ny * w + nx])
                        {
                            hasShapeNeighbor = true;
                            break;
                        }
                    }
                }

                if (hasShapeNeighbor)
                {
                    isOutline[idx] = true;
                    if (clampedThickness > 1)
                    {
                        queue.Enqueue((x, y, 1));
                    }
                }
            }
        }

        // Expand for thickness > 1
        while (queue.Count > 0)
        {
            var (cx, cy, dist) = queue.Dequeue();
            if (dist >= clampedThickness)
            {
                continue;
            }

            for (int d = 0; d < dxList.Length; d++)
            {
                int nx = cx + dxList[d];
                int ny = cy + dyList[d];
                if ((uint)nx < (uint)w && (uint)ny < (uint)h)
                {
                    int nIdx = ny * w + nx;
                    if (!isShape[nIdx] && !isOutline[nIdx])
                    {
                        isOutline[nIdx] = true;
                        queue.Enqueue((nx, ny, dist + 1));
                    }
                }
            }
        }

        var deltas = new List<PixelDelta>();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (!isOutline[idx])
                {
                    continue;
                }

                var oldColor = layer.Pixels[idx];
                if (layer.IsAlphaLocked && oldColor.A == 0)
                {
                    continue;
                }

                if (oldColor != outlineColor)
                {
                    deltas.Add(new PixelDelta(x, y, oldColor, outlineColor));
                }
            }
        }

        return deltas;
    }

    /// <summary>
    /// Generates a drop shadow for non-transparent pixels on the layer with offset and opacity.
    /// </summary>
    /// <param name="layer">The target layer to modify.</param>
    /// <param name="shadowColor">The base color of the shadow.</param>
    /// <param name="dx">Horizontal offset in pixels.</param>
    /// <param name="dy">Vertical offset in pixels.</param>
    /// <param name="opacity">Shadow opacity from 0.0 to 1.0 (or 0 to 100).</param>
    /// <param name="selection">Optional selection model restricting the shadow source shape.</param>
    /// <returns>A list of pixel modifications performed on the layer.</returns>
    public static IReadOnlyList<PixelDelta> GenerateShadow(
        Layer layer,
        PixelColor shadowColor,
        int dx = 1,
        int dy = 1,
        float opacity = 0.5f,
        SelectionModel? selection = null)
    {
        var deltas = ComputeShadowDeltas(layer, shadowColor, dx, dy, opacity, selection);
        ApplyDeltas(layer, deltas);
        return deltas;
    }

    /// <summary>
    /// Computes pixel deltas for generating a shadow without modifying the layer.
    /// </summary>
    public static IReadOnlyList<PixelDelta> ComputeShadowDeltas(
        Layer layer,
        PixelColor shadowColor,
        int dx = 1,
        int dy = 1,
        float opacity = 0.5f,
        SelectionModel? selection = null)
    {
        ArgumentNullException.ThrowIfNull(layer);

        int w = layer.Width;
        int h = layer.Height;
        int totalPixels = w * h;
        bool hasSelection = selection != null && selection.IsActive;

        float clampedOpacity = opacity > 1.0f ? Math.Clamp(opacity / 100f, 0f, 1f) : Math.Clamp(opacity, 0f, 1f);
        byte shadowAlpha = (byte)Math.Clamp((int)Math.Round(shadowColor.A * clampedOpacity), 0, 255);
        if (shadowAlpha == 0)
        {
            return Array.Empty<PixelDelta>();
        }

        var baseShadowColor = new PixelColor(shadowColor.R, shadowColor.G, shadowColor.B, shadowAlpha);

        var isShape = new bool[totalPixels];
        bool anyShape = false;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (layer.Pixels[idx].A > 0)
                {
                    if (!hasSelection || selection!.Contains(x, y))
                    {
                        isShape[idx] = true;
                        anyShape = true;
                    }
                }
            }
        }

        if (!anyShape)
        {
            return Array.Empty<PixelDelta>();
        }

        var isShadowTarget = new bool[totalPixels];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (!isShape[idx])
                {
                    continue;
                }

                int tx = x + dx;
                int ty = y + dy;
                if ((uint)tx < (uint)w && (uint)ty < (uint)h)
                {
                    int targetIdx = ty * w + tx;
                    if (!isShape[targetIdx])
                    {
                        isShadowTarget[targetIdx] = true;
                    }
                }
            }
        }

        var deltas = new List<PixelDelta>();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (!isShadowTarget[idx])
                {
                    continue;
                }

                var curColor = layer.Pixels[idx];
                if (layer.IsAlphaLocked && curColor.A == 0)
                {
                    continue;
                }

                var newColor = BlendColors(curColor, baseShadowColor);
                if (curColor != newColor)
                {
                    deltas.Add(new PixelDelta(x, y, curColor, newColor));
                }
            }
        }

        return deltas;
    }

    /// <summary>
    /// Replaces occurrences of a target color with a replacement color within the specified tolerance.
    /// </summary>
    /// <param name="layer">The target layer to modify.</param>
    /// <param name="targetColor">The color to search for and replace.</param>
    /// <param name="replacementColor">The new color to apply.</param>
    /// <param name="tolerance">The maximum color distance threshold for matching (0 to 510).</param>
    /// <param name="selection">Optional selection model restricting the replacement area.</param>
    /// <returns>A list of pixel modifications performed on the layer.</returns>
    public static IReadOnlyList<PixelDelta> ReplaceColor(
        Layer layer,
        PixelColor targetColor,
        PixelColor replacementColor,
        int tolerance = 0,
        SelectionModel? selection = null)
    {
        var deltas = ComputeReplaceColorDeltas(layer, targetColor, replacementColor, tolerance, selection);
        ApplyDeltas(layer, deltas);
        return deltas;
    }

    /// <summary>
    /// Computes pixel deltas for color replacement without modifying the layer.
    /// </summary>
    public static IReadOnlyList<PixelDelta> ComputeReplaceColorDeltas(
        Layer layer,
        PixelColor targetColor,
        PixelColor replacementColor,
        int tolerance = 0,
        SelectionModel? selection = null)
    {
        ArgumentNullException.ThrowIfNull(layer);

        int w = layer.Width;
        int h = layer.Height;
        int clampedTolerance = Math.Clamp(tolerance, 0, 510);

        if (clampedTolerance == 0 && targetColor == replacementColor)
        {
            return Array.Empty<PixelDelta>();
        }

        bool hasSelection = selection != null && selection.IsActive;
        int startX = hasSelection ? Math.Clamp(selection!.Bounds.Left, 0, w) : 0;
        int endX = hasSelection ? Math.Clamp(selection!.Bounds.Right, 0, w) : w;
        int startY = hasSelection ? Math.Clamp(selection!.Bounds.Top, 0, h) : 0;
        int endY = hasSelection ? Math.Clamp(selection!.Bounds.Bottom, 0, h) : h;

        var deltas = new List<PixelDelta>();

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                if (hasSelection && !selection!.Contains(x, y))
                {
                    continue;
                }

                int idx = y * w + x;
                var curColor = layer.Pixels[idx];

                if (layer.IsAlphaLocked && curColor.A == 0)
                {
                    continue;
                }

                if (MatchesColor(curColor, targetColor, clampedTolerance))
                {
                    if (curColor != replacementColor)
                    {
                        deltas.Add(new PixelDelta(x, y, curColor, replacementColor));
                    }
                }
            }
        }

        return deltas;
    }

    /// <summary>
    /// Applies a list of pixel deltas to the layer buffer and triggers an invalidation.
    /// </summary>
    public static void ApplyDeltas(Layer layer, IReadOnlyList<PixelDelta> deltas)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(deltas);

        if (deltas.Count == 0)
        {
            return;
        }

        for (int i = 0; i < deltas.Count; i++)
        {
            var d = deltas[i];
            if ((uint)d.X < (uint)layer.Width && (uint)d.Y < (uint)layer.Height)
            {
                layer.Pixels[d.Y * layer.Width + d.X] = d.NewColor;
            }
        }
        layer.Invalidate();
    }

    /// <summary>
    /// Reverts a list of pixel deltas on the layer buffer and triggers an invalidation.
    /// </summary>
    public static void RevertDeltas(Layer layer, IReadOnlyList<PixelDelta> deltas)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(deltas);

        if (deltas.Count == 0)
        {
            return;
        }

        for (int i = 0; i < deltas.Count; i++)
        {
            var d = deltas[i];
            if ((uint)d.X < (uint)layer.Width && (uint)d.Y < (uint)layer.Height)
            {
                layer.Pixels[d.Y * layer.Width + d.X] = d.OldColor;
            }
        }
        layer.Invalidate();
    }

    /// <summary>
    /// Evaluates if two colors match within the specified Euclidean distance tolerance.
    /// </summary>
    public static bool MatchesColor(PixelColor a, PixelColor b, int tolerance)
    {
        if (tolerance == 0)
        {
            return a == b;
        }

        return PixelColor.Distance(a, b) <= tolerance;
    }

    /// <summary>
    /// Blends a foreground color onto a background color using standard alpha compositing.
    /// </summary>
    public static PixelColor BlendColors(PixelColor background, PixelColor foreground)
    {
        if (foreground.A == 0)
        {
            return background;
        }

        if (foreground.A == 255 || background.A == 0)
        {
            return foreground;
        }

        float fA = foreground.A / 255f;
        float bA = background.A / 255f;
        float outA = fA + bA * (1f - fA);

        if (outA <= 0f)
        {
            return PixelColor.Transparent;
        }

        byte r = (byte)Math.Clamp(Math.Round((foreground.R * fA + background.R * bA * (1f - fA)) / outA), 0, 255);
        byte g = (byte)Math.Clamp(Math.Round((foreground.G * fA + background.G * bA * (1f - fA)) / outA), 0, 255);
        byte b = (byte)Math.Clamp(Math.Round((foreground.B * fA + background.B * bA * (1f - fA)) / outA), 0, 255);
        byte a = (byte)Math.Clamp(Math.Round(outA * 255f), 0, 255);

        return new PixelColor(r, g, b, a);
    }
}

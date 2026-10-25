using PixelForge.Core.History;
using PixelForge.Core.Models;

namespace PixelForge.Core.Tools;

/// <summary>
/// Flood fill tool using breadth-first search (BFS) with bounded memory and symmetry support.
/// </summary>
public class FillTool : ITool
{
    public string Name => "Fill";
    public string Shortcut => "G";
    public string IconKey => "IconFill";

    public void OnPointerDown(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if ((uint)x >= (uint)ctx.Canvas.Width || (uint)y >= (uint)ctx.Canvas.Height)
        {
            return;
        }

        int width = ctx.Canvas.Width;
        int height = ctx.Canvas.Height;
        int totalPixels = width * height;
        int tolerance = Math.Clamp(ctx.FillTolerance, 0, 510);
        bool contiguous = ctx.FillContiguous;
        var selection = ctx.Selection;

        var visited = new bool[totalPixels];
        var deltas = new List<PixelDelta>();
        var seeds = ctx.GetSymmetricPoints(x, y).ToList();

        foreach (var (seedX, seedY) in seeds)
        {
            if ((uint)seedX >= (uint)width || (uint)seedY >= (uint)height)
            {
                continue;
            }

            if (selection.IsActive && !selection.Contains(seedX, seedY))
            {
                continue;
            }

            var targetColor = ctx.ActiveLayer.GetPixel(seedX, seedY);
            if (ctx.ActiveLayer.IsAlphaLocked && targetColor.A == 0)
            {
                continue;
            }

            var replacementColor = ctx.PrimaryColor;

            if (tolerance == 0 && targetColor == replacementColor && ctx.ActiveDitherPattern == DitherPattern.None)
            {
                continue;
            }

            if (!contiguous)
            {
                // Global color replacement across the canvas (or within active selection)
                int startX = selection.IsActive ? Math.Max(0, selection.Bounds.Left) : 0;
                int endX = selection.IsActive ? Math.Min(width, selection.Bounds.Right) : width;
                int startY = selection.IsActive ? Math.Max(0, selection.Bounds.Top) : 0;
                int endY = selection.IsActive ? Math.Min(height, selection.Bounds.Bottom) : height;

                for (int gy = startY; gy < endY; gy++)
                {
                    for (int gx = startX; gx < endX; gx++)
                    {
                        int gIndex = gy * width + gx;
                        if (visited[gIndex]) continue;
                        visited[gIndex] = true;

                        var curColor = ctx.ActiveLayer.GetPixel(gx, gy);
                        if (ctx.ActiveLayer.IsAlphaLocked && curColor.A == 0)
                        {
                            continue;
                        }

                        if (MatchesColor(curColor, targetColor, tolerance))
                        {
                            var finalColor = ctx.ActiveDitherPattern != DitherPattern.None
                                ? DitherHelper.EvaluateDitherColor(gx, gy, replacementColor, ctx.SecondaryColor, ctx.ActiveDitherPattern, ctx.DitherIntensity)
                                : replacementColor;

                            if (curColor != finalColor)
                            {
                                deltas.Add(new PixelDelta(gx, gy, curColor, finalColor));
                                ctx.ActiveLayer.SetPixel(gx, gy, finalColor);
                            }
                        }
                    }
                }
                continue;
            }

            // Contiguous BFS flood fill
            int seedIndex = seedY * width + seedX;
            if (visited[seedIndex])
            {
                continue;
            }

            var queue = new Queue<int>();
            queue.Enqueue(seedIndex);
            visited[seedIndex] = true;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int cx = current % width;
                int cy = current / width;

                var currentColor = ctx.ActiveLayer.GetPixel(cx, cy);
                if (ctx.ActiveLayer.IsAlphaLocked && currentColor.A == 0)
                {
                    continue;
                }

                if (!MatchesColor(currentColor, targetColor, tolerance))
                {
                    continue;
                }

                var finalColor = ctx.ActiveDitherPattern != DitherPattern.None
                    ? DitherHelper.EvaluateDitherColor(cx, cy, replacementColor, ctx.SecondaryColor, ctx.ActiveDitherPattern, ctx.DitherIntensity)
                    : replacementColor;

                if (currentColor != finalColor)
                {
                    deltas.Add(new PixelDelta(cx, cy, currentColor, finalColor));
                    ctx.ActiveLayer.SetPixel(cx, cy, finalColor);
                }

                // 4-directional neighborhood
                if (cx > 0)
                {
                    TryEnqueue(cx - 1, cy, width, targetColor, tolerance, ctx.ActiveLayer, visited, queue, selection);
                }

                if (cx < width - 1)
                {
                    TryEnqueue(cx + 1, cy, width, targetColor, tolerance, ctx.ActiveLayer, visited, queue, selection);
                }

                if (cy > 0)
                {
                    TryEnqueue(cx, cy - 1, width, targetColor, tolerance, ctx.ActiveLayer, visited, queue, selection);
                }

                if (cy < height - 1)
                {
                    TryEnqueue(cx, cy + 1, width, targetColor, tolerance, ctx.ActiveLayer, visited, queue, selection);
                }
            }
        }

        if (deltas.Count > 0)
        {
            string desc = contiguous ? "Flood Fill" : "Replace Color";
            var command = new DrawPixelsCommand(ctx.ActiveLayer, deltas, desc);
            ctx.History.ExecuteCommand(command);
        }
    }

    public void OnPointerMove(ToolContext ctx, int x, int y)
    {
    }

    public void OnPointerUp(ToolContext ctx, int x, int y)
    {
    }

    public IReadOnlyList<(int X, int Y, PixelColor Color)> GetPreviewPixels(ToolContext ctx)
    {
        return Array.Empty<(int X, int Y, PixelColor Color)>();
    }

    private static bool MatchesColor(PixelColor a, PixelColor b, int tolerance)
    {
        if (tolerance == 0)
        {
            return a == b;
        }

        return PixelColor.Distance(a, b) <= tolerance;
    }

    private static void TryEnqueue(
        int x,
        int y,
        int width,
        PixelColor targetColor,
        int tolerance,
        Layer layer,
        bool[] visited,
        Queue<int> queue,
        SelectionModel selection)
    {
        if (selection.IsActive && !selection.Contains(x, y))
        {
            return;
        }

        if (layer.IsAlphaLocked && layer.GetPixel(x, y).A == 0)
        {
            return;
        }

        int index = y * width + x;
        if (!visited[index] && MatchesColor(layer.GetPixel(x, y), targetColor, tolerance))
        {
            visited[index] = true;
            queue.Enqueue(index);
        }
    }
}

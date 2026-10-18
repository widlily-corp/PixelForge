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

        var visited = new bool[totalPixels];
        var deltas = new List<PixelDelta>();
        var seeds = ctx.GetSymmetricPoints(x, y).ToList();

        foreach (var (seedX, seedY) in seeds)
        {
            if ((uint)seedX >= (uint)width || (uint)seedY >= (uint)height)
            {
                continue;
            }

            int seedIndex = seedY * width + seedX;
            if (visited[seedIndex])
            {
                continue;
            }

            var targetColor = ctx.ActiveLayer.GetPixel(seedX, seedY);
            var replacementColor = ctx.PrimaryColor;

            if (targetColor == replacementColor)
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
                if (currentColor != targetColor)
                {
                    continue;
                }

                deltas.Add(new PixelDelta(cx, cy, targetColor, replacementColor));
                ctx.ActiveLayer.SetPixel(cx, cy, replacementColor);

                // 4-directional neighborhood
                if (cx > 0)
                {
                    TryEnqueue(cx - 1, cy, width, targetColor, ctx.ActiveLayer, visited, queue);
                }

                if (cx < width - 1)
                {
                    TryEnqueue(cx + 1, cy, width, targetColor, ctx.ActiveLayer, visited, queue);
                }

                if (cy > 0)
                {
                    TryEnqueue(cx, cy - 1, width, targetColor, ctx.ActiveLayer, visited, queue);
                }

                if (cy < height - 1)
                {
                    TryEnqueue(cx, cy + 1, width, targetColor, ctx.ActiveLayer, visited, queue);
                }
            }
        }

        if (deltas.Count > 0)
        {
            var command = new DrawPixelsCommand(ctx.ActiveLayer, deltas, "Flood Fill");
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

    private static void TryEnqueue(
        int x,
        int y,
        int width,
        PixelColor targetColor,
        Layer layer,
        bool[] visited,
        Queue<int> queue)
    {
        int index = y * width + x;
        if (!visited[index] && layer.GetPixel(x, y) == targetColor)
        {
            visited[index] = true;
            queue.Enqueue(index);
        }
    }
}

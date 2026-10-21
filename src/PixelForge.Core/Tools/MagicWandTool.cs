using PixelForge.Core.Models;
using SkiaSharp;

namespace PixelForge.Core.Tools;

/// <summary>
/// Magic wand selection tool that selects pixels based on color similarity using either contiguous BFS or global matching.
/// </summary>
public class MagicWandTool : ITool
{
    private int? _tolerance;
    private bool? _contiguous;

    public string Name => "Magic Wand";
    public string Shortcut => "W";
    public string IconKey => "IconMagicWand";

    /// <summary>
    /// Gets or sets the color distance threshold for matching pixels (0 to 510).
    /// </summary>
    public int Tolerance
    {
        get => _tolerance ?? 0;
        set => _tolerance = value;
    }

    /// <summary>
    /// Gets or sets whether selection operates contiguously via BFS or globally matches across the entire layer.
    /// </summary>
    public bool Contiguous
    {
        get => _contiguous ?? true;
        set => _contiguous = value;
    }

    /// <summary>
    /// Gets the list of pixel coordinates selected in the most recent operation.
    /// </summary>
    public IReadOnlyList<(int X, int Y)> SelectedPixels { get; private set; } = Array.Empty<(int X, int Y)>();

    public MagicWandTool()
    {
    }

    public MagicWandTool(int tolerance, bool contiguous = true)
    {
        _tolerance = tolerance;
        _contiguous = contiguous;
    }

    public void OnPointerDown(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if ((uint)x >= (uint)ctx.Canvas.Width || (uint)y >= (uint)ctx.Canvas.Height)
        {
            ctx.Selection.Clear();
            SelectedPixels = Array.Empty<(int X, int Y)>();
            return;
        }

        if (ctx.Selection.IsFloating)
        {
            ctx.Selection.Stamp(ctx.ActiveLayer);
        }

        int tolerance = _tolerance ?? ctx.FillTolerance;
        bool contiguous = _contiguous ?? ctx.FillContiguous;

        var seeds = ctx.GetSymmetricPoints(x, y);
        Select(ctx.ActiveLayer, seeds, tolerance, contiguous, ctx.Selection);
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

    /// <summary>
    /// Selects matching pixels from a layer starting at the specified coordinate.
    /// </summary>
    public IReadOnlyList<(int X, int Y)> Select(
        Layer layer,
        int startX,
        int startY,
        int tolerance = 0,
        bool contiguous = true,
        SelectionModel? selection = null)
    {
        ArgumentNullException.ThrowIfNull(layer);
        return Select(layer, new[] { (startX, startY) }, tolerance, contiguous, selection);
    }

    /// <summary>
    /// Selects matching pixels from a layer using multiple seed coordinates (e.g., symmetry points).
    /// </summary>
    public IReadOnlyList<(int X, int Y)> Select(
        Layer layer,
        IEnumerable<(int X, int Y)> seeds,
        int tolerance = 0,
        bool contiguous = true,
        SelectionModel? selection = null)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(seeds);

        int w = layer.Width;
        int h = layer.Height;
        int totalPixels = w * h;
        int clampedTolerance = Math.Clamp(tolerance, 0, 510);

        var seedList = new List<(int X, int Y)>();
        foreach (var s in seeds)
        {
            if ((uint)s.X < (uint)w && (uint)s.Y < (uint)h)
            {
                seedList.Add(s);
            }
        }

        if (seedList.Count == 0)
        {
            selection?.Clear();
            SelectedPixels = Array.Empty<(int X, int Y)>();
            return SelectedPixels;
        }

        var targetColor = layer.GetPixel(seedList[0].X, seedList[0].Y);
        var matched = new List<(int X, int Y)>();

        if (contiguous)
        {
            var visited = new bool[totalPixels];
            var queue = new Queue<int>();

            for (int i = 0; i < seedList.Count; i++)
            {
                var (sx, sy) = seedList[i];
                int sIdx = sy * w + sx;
                if (!visited[sIdx] && MatchesColor(layer.GetPixel(sx, sy), targetColor, clampedTolerance))
                {
                    visited[sIdx] = true;
                    queue.Enqueue(sIdx);
                    matched.Add((sx, sy));
                }
            }

            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                int cx = cur % w;
                int cy = cur / w;

                // 4-way neighborhood
                if (cx > 0) TryEnqueue(cx - 1, cy);
                if (cx < w - 1) TryEnqueue(cx + 1, cy);
                if (cy > 0) TryEnqueue(cx, cy - 1);
                if (cy < h - 1) TryEnqueue(cx, cy + 1);
            }

            void TryEnqueue(int nx, int ny)
            {
                int nIdx = ny * w + nx;
                if (!visited[nIdx])
                {
                    visited[nIdx] = true;
                    if (MatchesColor(layer.GetPixel(nx, ny), targetColor, clampedTolerance))
                    {
                        queue.Enqueue(nIdx);
                        matched.Add((nx, ny));
                    }
                }
            }
        }
        else
        {
            for (int gy = 0; gy < h; gy++)
            {
                for (int gx = 0; gx < w; gx++)
                {
                    var color = layer.GetPixel(gx, gy);
                    if (MatchesColor(color, targetColor, clampedTolerance))
                    {
                        matched.Add((gx, gy));
                    }
                }
            }
        }

        SelectedPixels = matched;

        if (selection != null)
        {
            if (matched.Count == 0)
            {
                selection.Clear();
            }
            else
            {
                int minX = int.MaxValue;
                int minY = int.MaxValue;
                int maxX = int.MinValue;
                int maxY = int.MinValue;

                for (int i = 0; i < matched.Count; i++)
                {
                    var pt = matched[i];
                    if (pt.X < minX) minX = pt.X;
                    if (pt.X > maxX) maxX = pt.X;
                    if (pt.Y < minY) minY = pt.Y;
                    if (pt.Y > maxY) maxY = pt.Y;
                }

                int rw = maxX - minX + 1;
                int rh = maxY - minY + 1;
                var bounds = new SKRectI(minX, minY, minX + rw, minY + rh);

                var selectionPixels = new PixelColor[rw * rh];
                for (int i = 0; i < matched.Count; i++)
                {
                    var pt = matched[i];
                    int localIdx = (pt.Y - minY) * rw + (pt.X - minX);
                    selectionPixels[localIdx] = layer.GetPixel(pt.X, pt.Y);
                }

                selection.Select(bounds, selectionPixels);
            }
        }

        return matched;
    }

    private static bool MatchesColor(PixelColor a, PixelColor b, int tolerance)
    {
        if (tolerance == 0)
        {
            return a == b;
        }

        return PixelColor.Distance(a, b) <= tolerance;
    }
}

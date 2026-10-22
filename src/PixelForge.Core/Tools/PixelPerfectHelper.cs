namespace PixelForge.Core.Tools;

/// <summary>
/// Provides utility methods for pixel-perfect line drawing and filtering.
/// Detects and eliminates redundant diagonal L-corner elbow pixels (jaggies)
/// in freehand drawing strokes to produce clean, crisp 1px lines while preserving 8-connectivity.
/// </summary>
public static class PixelPerfectHelper
{
    /// <summary>
    /// Evaluates whether the middle point (x1, y1) in a 3-point stroke segment forms an unwanted L-corner jaggie.
    /// An L-corner occurs when the first and third points are diagonally adjacent (|dx| == 1 and |dy| == 1)
    /// and the middle point shares an orthogonal axis with both endpoints.
    /// </summary>
    /// <param name="x0">First point X coordinate.</param>
    /// <param name="y0">First point Y coordinate.</param>
    /// <param name="x1">Middle point X coordinate.</param>
    /// <param name="y1">Middle point Y coordinate.</param>
    /// <param name="x2">Third point X coordinate.</param>
    /// <param name="y2">Third point Y coordinate.</param>
    /// <returns><c>true</c> if the middle point forms an unwanted L-corner jaggie; otherwise, <c>false</c>.</returns>
    public static bool IsCorner(int x0, int y0, int x1, int y1, int x2, int y2)
    {
        int dx = Math.Abs(x2 - x0);
        int dy = Math.Abs(y2 - y0);

        if (dx == 1 && dy == 1)
        {
            return (x1 == x0 && y1 == y2) || (x1 == x2 && y1 == y0);
        }

        return false;
    }

    /// <summary>
    /// Evaluates whether the middle point in a 3-point stroke segment forms an unwanted L-corner jaggie.
    /// </summary>
    /// <param name="p0">First point coordinate tuple.</param>
    /// <param name="p1">Middle point coordinate tuple.</param>
    /// <param name="p2">Third point coordinate tuple.</param>
    /// <returns><c>true</c> if the middle point forms an unwanted L-corner jaggie; otherwise, <c>false</c>.</returns>
    public static bool IsCorner((int X, int Y) p0, (int X, int Y) p1, (int X, int Y) p2)
    {
        return IsCorner(p0.X, p0.Y, p1.X, p1.Y, p2.X, p2.Y);
    }

    /// <summary>
    /// Determines whether the previous point (p1) should be removed given the preceding point (p0)
    /// and the newly added point (p2). Alias for <see cref="IsCorner(int, int, int, int, int, int)"/>.
    /// </summary>
    public static bool ShouldRemovePrevious(int p0X, int p0Y, int p1X, int p1Y, int p2X, int p2Y)
    {
        return IsCorner(p0X, p0Y, p1X, p1Y, p2X, p2Y);
    }

    /// <summary>
    /// Determines whether two coordinates are diagonally adjacent (Chebyshev distance 1 with diagonal step).
    /// </summary>
    public static bool AreDiagonallyAdjacent(int x0, int y0, int x1, int y1)
    {
        return Math.Abs(x1 - x0) == 1 && Math.Abs(y1 - y0) == 1;
    }

    /// <summary>
    /// Filters a list of consecutive stroke points upon release, removing unnecessary corner pixels.
    /// </summary>
    /// <param name="stroke">List of consecutive stroke points.</param>
    /// <returns>A new list of points with L-corner jaggies removed.</returns>
    public static List<(int X, int Y)> FilterStroke(IReadOnlyList<(int X, int Y)> stroke)
    {
        ArgumentNullException.ThrowIfNull(stroke);

        var result = new List<(int X, int Y)>(stroke.Count);
        FilterStroke(stroke, result);
        return result;
    }

    /// <summary>
    /// Filters a list of consecutive stroke points into a provided destination list, avoiding allocations.
    /// </summary>
    /// <param name="stroke">List of consecutive stroke points.</param>
    /// <param name="destination">Destination list to receive filtered stroke points.</param>
    public static void FilterStroke(IReadOnlyList<(int X, int Y)> stroke, List<(int X, int Y)> destination)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        ArgumentNullException.ThrowIfNull(destination);

        destination.Clear();

        if (stroke.Count == 0)
        {
            return;
        }

        if (stroke.Count < 3)
        {
            for (int i = 0; i < stroke.Count; i++)
            {
                destination.Add(stroke[i]);
            }
            return;
        }

        destination.Add(stroke[0]);

        for (int i = 1; i < stroke.Count - 1; i++)
        {
            var p0 = destination[^1];
            var p1 = stroke[i];

            if (p1.X == p0.X && p1.Y == p0.Y)
            {
                continue;
            }

            int nextIdx = i + 1;
            while (nextIdx < stroke.Count && stroke[nextIdx].X == p1.X && stroke[nextIdx].Y == p1.Y)
            {
                nextIdx++;
            }

            if (nextIdx < stroke.Count)
            {
                var p2 = stroke[nextIdx];
                if (IsCorner(p0.X, p0.Y, p1.X, p1.Y, p2.X, p2.Y))
                {
                    // Skip p1 corner pixel
                    continue;
                }
            }

            destination.Add(p1);
        }

        var last = stroke[^1];
        if (destination.Count == 0 || destination[^1].X != last.X || destination[^1].Y != last.Y)
        {
            destination.Add(last);
        }
    }

    /// <summary>
    /// Appends a new point to an active stroke buffer in real-time, replacing the preceding
    /// point if it forms an unwanted L-corner jaggie.
    /// </summary>
    /// <param name="stroke">The mutable stroke buffer.</param>
    /// <param name="x">The new X coordinate.</param>
    /// <param name="y">The new Y coordinate.</param>
    /// <returns><c>true</c> if a corner jaggie was removed; otherwise, <c>false</c>.</returns>
    public static bool AddPoint(List<(int X, int Y)> stroke, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(stroke);

        if (stroke.Count == 0)
        {
            stroke.Add((x, y));
            return false;
        }

        if (stroke[^1].X == x && stroke[^1].Y == y)
        {
            return false;
        }

        if (stroke.Count >= 2)
        {
            var p0 = stroke[^2];
            var p1 = stroke[^1];

            if (IsCorner(p0.X, p0.Y, p1.X, p1.Y, x, y))
            {
                stroke[^1] = (x, y);
                return true;
            }
        }

        stroke.Add((x, y));
        return false;
    }
}

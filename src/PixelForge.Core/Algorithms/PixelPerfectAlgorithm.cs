namespace PixelForge.Core.Algorithms;

/// <summary>
/// Implements standard pixel-art Pixel-Perfect line algorithm.
/// Detects and eliminates redundant diagonal L-corner elbow pixels (jaggies)
/// in freehand drawing strokes to produce clean, crisp 1px lines.
/// </summary>
public static class PixelPerfectAlgorithm
{
    /// <summary>
    /// Evaluates whether the middle point in a 3-point stroke segment forms an unwanted L-corner jaggie.
    /// </summary>
    /// <param name="x0">First point X coordinate.</param>
    /// <param name="y0">First point Y coordinate.</param>
    /// <param name="x1">Middle point X coordinate.</param>
    /// <param name="y1">Middle point Y coordinate.</param>
    /// <param name="x2">Third point X coordinate.</param>
    /// <param name="y2">Third point Y coordinate.</param>
    /// <returns><c>true</c> if the middle point forms an unwanted L-corner jaggie; otherwise, <c>false</c>.</returns>
    public static bool IsCornerJaggie(int x0, int y0, int x1, int y1, int x2, int y2)
    {
        // Check if endpoints are diagonal neighbors (|dx| == 1 and |dy| == 1)
        int dx = Math.Abs(x2 - x0);
        int dy = Math.Abs(y2 - y0);

        if (dx == 1 && dy == 1)
        {
            // The middle pixel forms an unwanted L-shaped corner if it shares an axis with both endpoints
            bool sharesAxis = (x1 == x0 && y1 == y2) || (x1 == x2 && y1 == y0);
            return sharesAxis;
        }

        return false;
    }

    /// <summary>
    /// Filters a list of consecutive stroke points, removing unnecessary corner pixels.
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
            var p2 = stroke[i + 1];

            if (p1.X == p0.X && p1.Y == p0.Y)
            {
                continue;
            }

            if (IsCornerJaggie(p0.X, p0.Y, p1.X, p1.Y, p2.X, p2.Y))
            {
                // Skip p1 corner pixel
                continue;
            }

            destination.Add(p1);
        }

        if (stroke[^1].X != destination[^1].X || stroke[^1].Y != destination[^1].Y)
        {
            destination.Add(stroke[^1]);
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

            if (IsCornerJaggie(p0.X, p0.Y, p1.X, p1.Y, x, y))
            {
                stroke[^1] = (x, y);
                return true;
            }
        }

        stroke.Add((x, y));
        return false;
    }
}

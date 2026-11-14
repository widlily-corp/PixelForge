using PixelForge.Core.Models;
using SkiaSharp;

namespace PixelForge.Core.Transforms;

/// <summary>
/// High-performance zero-blur pixel art resampling and transformation algorithms.
/// </summary>
public static class PixelTransform
{
    /// <summary>
    /// Transforms the pixel buffer using nearest-neighbor backward coordinate sampling.
    /// </summary>
    public static TransformedPixelsResult TransformNearestNeighbor(TransformSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        int srcW = session.OriginalBounds.Width;
        int srcH = session.OriginalBounds.Height;
        if (srcW <= 0 || srcH <= 0 || session.OriginalPixels.Length == 0)
        {
            return new TransformedPixelsResult(SKRectI.Empty, 0, 0, Array.Empty<PixelColor>());
        }

        // Fast-path: Identity transform
        if (session.ScaleX == 1.0f && session.ScaleY == 1.0f &&
            session.RotationDegrees == 0.0f &&
            session.TranslationX == 0.0f && session.TranslationY == 0.0f)
        {
            return new TransformedPixelsResult(
                session.OriginalBounds,
                srcW,
                srcH,
                (PixelColor[])session.OriginalPixels.Clone());
        }

        var forward = session.ComputeMatrix();
        if (!forward.TryInvert(out var inv))
        {
            return new TransformedPixelsResult(
                session.OriginalBounds,
                srcW,
                srcH,
                (PixelColor[])session.OriginalPixels.Clone());
        }

        // Map four corners of original bounds
        float left = session.OriginalBounds.Left;
        float top = session.OriginalBounds.Top;
        float right = session.OriginalBounds.Right;
        float bottom = session.OriginalBounds.Bottom;

        var p0 = forward.MapPoint(left, top);
        var p1 = forward.MapPoint(right, top);
        var p2 = forward.MapPoint(right, bottom);
        var p3 = forward.MapPoint(left, bottom);

        float minX = MathF.Min(MathF.Min(p0.X, p1.X), MathF.Min(p2.X, p3.X));
        float maxX = MathF.Max(MathF.Max(p0.X, p1.X), MathF.Max(p2.X, p3.X));
        float minY = MathF.Min(MathF.Min(p0.Y, p1.Y), MathF.Min(p2.Y, p3.Y));
        float maxY = MathF.Max(MathF.Max(p0.Y, p1.Y), MathF.Max(p2.Y, p3.Y));

        const float Epsilon = 0.0005f;
        int dstMinX = (int)MathF.Floor(minX + Epsilon);
        int dstMaxX = (int)MathF.Ceiling(maxX - Epsilon);
        int dstMinY = (int)MathF.Floor(minY + Epsilon);
        int dstMaxY = (int)MathF.Ceiling(maxY - Epsilon);

        int dstW = Math.Max(1, dstMaxX - dstMinX);
        int dstH = Math.Max(1, dstMaxY - dstMinY);

        var dstBounds = new SKRectI(dstMinX, dstMinY, dstMinX + dstW, dstMinY + dstH);
        var dstPixels = new PixelColor[dstW * dstH];

        var srcPixels = session.OriginalPixels;
        float origLeft = session.OriginalBounds.Left;
        float origTop = session.OriginalBounds.Top;

        for (int dy = 0; dy < dstH; dy++)
        {
            float worldY = dstMinY + dy + 0.5f;
            int rowOffset = dy * dstW;

            for (int dx = 0; dx < dstW; dx++)
            {
                float worldX = dstMinX + dx + 0.5f;
                var srcPt = inv.MapPoint(worldX, worldY);

                int sx = (int)MathF.Floor(srcPt.X - origLeft);
                int sy = (int)MathF.Floor(srcPt.Y - origTop);

                if (sx >= 0 && sx < srcW && sy >= 0 && sy < srcH)
                {
                    dstPixels[rowOffset + dx] = srcPixels[sy * srcW + sx];
                }
                else
                {
                    dstPixels[rowOffset + dx] = PixelColor.Transparent;
                }
            }
        }

        return new TransformedPixelsResult(dstBounds, dstW, dstH, dstPixels);
    }

    /// <summary>
    /// Upscales a pixel buffer by 2x using the EPX / Scale2x algorithm to preserve pixel art curves without blur.
    /// </summary>
    public static (int Width, int Height, PixelColor[] Pixels) Scale2x(PixelColor[] src, int w, int h)
    {
        ArgumentNullException.ThrowIfNull(src);
        if (w <= 0 || h <= 0 || src.Length < w * h)
        {
            return (0, 0, Array.Empty<PixelColor>());
        }

        int newW = w * 2;
        int newH = h * 2;
        var dst = new PixelColor[newW * newH];

        for (int y = 0; y < h; y++)
        {
            int rowOffset = y * w;
            for (int x = 0; x < w; x++)
            {
                var p = src[rowOffset + x];
                var a = (y > 0) ? src[(y - 1) * w + x] : p;
                var b = (x < w - 1) ? src[rowOffset + (x + 1)] : p;
                var c = (x > 0) ? src[rowOffset + (x - 1)] : p;
                var d = (y < h - 1) ? src[(y + 1) * w + x] : p;

                var p1 = p;
                var p2 = p;
                var p3 = p;
                var p4 = p;

                if (c == a && c != d && a != b) p1 = a;
                if (a == b && a != c && b != d) p2 = b;
                if (d == c && d != b && c != a) p3 = c;
                if (b == d && b != a && d != c) p4 = d;

                int outY = y * 2;
                int outX = x * 2;
                dst[outY * newW + outX] = p1;
                dst[outY * newW + (outX + 1)] = p2;
                dst[(outY + 1) * newW + outX] = p3;
                dst[(outY + 1) * newW + (outX + 1)] = p4;
            }
        }

        return (newW, newH, dst);
    }

    /// <summary>
    /// Transforms the pixel buffer using the high-fidelity RotSprite algorithm.
    /// </summary>
    public static TransformedPixelsResult TransformRotSprite(TransformSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        int srcW = session.OriginalBounds.Width;
        int srcH = session.OriginalBounds.Height;
        if (srcW <= 0 || srcH <= 0 || session.OriginalPixels.Length == 0)
        {
            return new TransformedPixelsResult(SKRectI.Empty, 0, 0, Array.Empty<PixelColor>());
        }

        // Automatic fallback to nearest-neighbor if dimensions exceed safety budget or rotation is zero
        if (srcW > 256 || srcH > 256 || MathF.Abs(session.RotationDegrees % 360f) < 0.001f)
        {
            return TransformNearestNeighbor(session);
        }

        // Pass 1: 8x Scale2x upscale
        var (w2, h2, pass1) = Scale2x(session.OriginalPixels, srcW, srcH);
        var (w4, h4, pass2) = Scale2x(pass1, w2, h2);
        var (upW, upH, up8) = Scale2x(pass2, w4, h4);

        // Rotation matrix about Pivot (scale = 1.0f, translation = 0)
        var rotMatrix = SKMatrix.CreateTranslation(-session.Pivot.X, -session.Pivot.Y);
        rotMatrix = rotMatrix.PostConcat(SKMatrix.CreateRotationDegrees(session.RotationDegrees));
        rotMatrix = rotMatrix.PostConcat(SKMatrix.CreateTranslation(session.Pivot.X, session.Pivot.Y));

        if (!rotMatrix.TryInvert(out var invRot))
        {
            return TransformNearestNeighbor(session);
        }

        // Calculate 1:1 rotated bounding box
        float left = session.OriginalBounds.Left;
        float top = session.OriginalBounds.Top;
        float right = session.OriginalBounds.Right;
        float bottom = session.OriginalBounds.Bottom;

        var p0 = rotMatrix.MapPoint(left, top);
        var p1 = rotMatrix.MapPoint(right, top);
        var p2 = rotMatrix.MapPoint(right, bottom);
        var p3 = rotMatrix.MapPoint(left, bottom);

        float minX = MathF.Min(MathF.Min(p0.X, p1.X), MathF.Min(p2.X, p3.X));
        float maxX = MathF.Max(MathF.Max(p0.X, p1.X), MathF.Max(p2.X, p3.X));
        float minY = MathF.Min(MathF.Min(p0.Y, p1.Y), MathF.Min(p2.Y, p3.Y));
        float maxY = MathF.Max(MathF.Max(p0.Y, p1.Y), MathF.Max(p2.Y, p3.Y));

        const float Epsilon = 0.0005f;
        int dstMinX = (int)MathF.Floor(minX + Epsilon);
        int dstMaxX = (int)MathF.Ceiling(maxX - Epsilon);
        int dstMinY = (int)MathF.Floor(minY + Epsilon);
        int dstMaxY = (int)MathF.Ceiling(maxY - Epsilon);

        int dstW = Math.Max(1, dstMaxX - dstMinX);
        int dstH = Math.Max(1, dstMaxY - dstMinY);

        var dstBounds = new SKRectI(dstMinX, dstMinY, dstMinX + dstW, dstMinY + dstH);
        var rotatedPixels = new PixelColor[dstW * dstH];

        float origLeft = session.OriginalBounds.Left;
        float origTop = session.OriginalBounds.Top;

        // Sample 8x8 subpixels with majority voting
        for (int dy = 0; dy < dstH; dy++)
        {
            int rowOffset = dy * dstW;
            for (int dx = 0; dx < dstW; dx++)
            {
                // Count frequencies of sampled colors
                var sampleCounts = new Dictionary<PixelColor, int>(8);
                PixelColor centerSample = PixelColor.Transparent;

                for (int subY = 0; subY < 8; subY++)
                {
                    float worldY = dstMinY + dy + (subY + 0.5f) / 8.0f;
                    for (int subX = 0; subX < 8; subX++)
                    {
                        float worldX = dstMinX + dx + (subX + 0.5f) / 8.0f;
                        var pt = invRot.MapPoint(worldX, worldY);

                        int upX = (int)MathF.Floor((pt.X - origLeft) * 8.0f);
                        int upY = (int)MathF.Floor((pt.Y - origTop) * 8.0f);

                        PixelColor sample = PixelColor.Transparent;
                        if (upX >= 0 && upX < upW && upY >= 0 && upY < upH)
                        {
                            sample = up8[upY * upW + upX];
                        }

                        if (subX == 4 && subY == 4)
                        {
                            centerSample = sample;
                        }

                        if (sampleCounts.TryGetValue(sample, out int count))
                        {
                            sampleCounts[sample] = count + 1;
                        }
                        else
                        {
                            sampleCounts[sample] = 1;
                        }
                    }
                }

                // Majority voting
                PixelColor majorityColor = centerSample;
                int maxVotes = -1;
                foreach (var (color, count) in sampleCounts)
                {
                    // Transparent counts only if it has a strict majority or no opaque color exists
                    if (count > maxVotes)
                    {
                        maxVotes = count;
                        majorityColor = color;
                    }
                }

                rotatedPixels[rowOffset + dx] = majorityColor;
            }
        }

        // If scale or translation is also modified, apply nearest neighbor scale to rotated result
        if (session.ScaleX != 1.0f || session.ScaleY != 1.0f ||
            session.TranslationX != 0.0f || session.TranslationY != 0.0f)
        {
            var intermediateSession = new TransformSession(dstBounds, rotatedPixels)
            {
                ScaleX = session.ScaleX,
                ScaleY = session.ScaleY,
                TranslationX = session.TranslationX,
                TranslationY = session.TranslationY,
                Pivot = session.Pivot
            };

            return TransformNearestNeighbor(intermediateSession);
        }

        return new TransformedPixelsResult(dstBounds, dstW, dstH, rotatedPixels);
    }

    /// <summary>
    /// Executes the transformation according to the session's chosen algorithm.
    /// </summary>
    public static TransformedPixelsResult Transform(TransformSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Algorithm == ResampleAlgorithm.RotSprite)
        {
            return TransformRotSprite(session);
        }

        return TransformNearestNeighbor(session);
    }
}

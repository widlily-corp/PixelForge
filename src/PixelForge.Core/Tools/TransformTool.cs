using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Transforms;
using SkiaSharp;

namespace PixelForge.Core.Tools;

/// <summary>
/// Dedicated free transform tool providing direct manipulation of bounding box scale, rotation, translation, and pixel resampling.
/// </summary>
public class TransformTool : ITool
{
    private TransformSession? _activeSession;
    private TransformHandle _activeHandle = TransformHandle.None;
    private TransformHandle _hoveredHandle = TransformHandle.None;

    private float _dragStartX;
    private float _dragStartY;
    private float _initScaleX = 1.0f;
    private float _initScaleY = 1.0f;
    private float _initRotation = 0.0f;
    private float _initTransX = 0.0f;
    private float _initTransY = 0.0f;
    private float _initAngleRad = 0.0f;

    public string Name => "Transform";
    public string Shortcut => "T";
    public string IconKey => "IconTransform";

    public TransformSession? ActiveSession => _activeSession;
    public TransformHandle ActiveHandle => _activeHandle;
    public TransformHandle HoveredHandle => _hoveredHandle;

    public bool IsShiftDown { get; set; }
    public bool IsCtrlDown { get; set; }

    /// <summary>
    /// Initializes an active transform session for the current selection or active layer content.
    /// </summary>
    public void InitializeSession(ToolContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (ctx.ActiveLayer == null) return;

        SKRectI bounds;
        PixelColor[] pixels;

        if (ctx.Selection != null && ctx.Selection.IsActive && ctx.Selection.Bounds.Width > 0 && ctx.Selection.Bounds.Height > 0)
        {
            bounds = ctx.Selection.Bounds;
            int w = bounds.Width;
            int h = bounds.Height;
            pixels = new PixelColor[w * h];

            for (int y = 0; y < h; y++)
            {
                int srcY = bounds.Top + y;
                for (int x = 0; x < w; x++)
                {
                    int srcX = bounds.Left + x;
                    pixels[y * w + x] = ctx.ActiveLayer.GetPixel(srcX, srcY);
                    ctx.ActiveLayer.SetPixel(srcX, srcY, PixelColor.Transparent);
                }
            }
        }
        else
        {
            // Find content bounds on the active layer
            int minX = ctx.ActiveLayer.Width;
            int minY = ctx.ActiveLayer.Height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < ctx.ActiveLayer.Height; y++)
            {
                for (int x = 0; x < ctx.ActiveLayer.Width; x++)
                {
                    if (ctx.ActiveLayer.GetPixel(x, y).A > 0)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < minX || maxY < minY)
            {
                bounds = new SKRectI(0, 0, Math.Min(16, ctx.ActiveLayer.Width), Math.Min(16, ctx.ActiveLayer.Height));
            }
            else
            {
                bounds = new SKRectI(minX, minY, maxX + 1, maxY + 1);
            }

            int w = bounds.Width;
            int h = bounds.Height;
            pixels = new PixelColor[w * h];

            for (int y = 0; y < h; y++)
            {
                int srcY = bounds.Top + y;
                for (int x = 0; x < w; x++)
                {
                    int srcX = bounds.Left + x;
                    pixels[y * w + x] = ctx.ActiveLayer.GetPixel(srcX, srcY);
                    ctx.ActiveLayer.SetPixel(srcX, srcY, PixelColor.Transparent);
                }
            }
        }

        ctx.ActiveLayer.Invalidate();
        _activeSession = new TransformSession(bounds, pixels);
    }

    /// <summary>
    /// Performs hit-testing against transform handles and bounding quad.
    /// </summary>
    public TransformHandle HitTest(float x, float y, float handleHitRadius = 8.0f)
    {
        if (_activeSession == null) return TransformHandle.None;

        var matrix = _activeSession.ComputeMatrix();
        float left = _activeSession.OriginalBounds.Left;
        float top = _activeSession.OriginalBounds.Top;
        float right = _activeSession.OriginalBounds.Right;
        float bottom = _activeSession.OriginalBounds.Bottom;
        float midX = left + _activeSession.OriginalBounds.Width / 2.0f;
        float midY = top + _activeSession.OriginalBounds.Height / 2.0f;

        // Rotation handle point: 20 pixels above top midpoint
        var rotPt = matrix.MapPoint(midX, top - 20.0f);
        if (DistanceSquared(x, y, rotPt.X, rotPt.Y) <= handleHitRadius * handleHitRadius)
        {
            return TransformHandle.Rotate;
        }

        // Corner handles
        var tl = matrix.MapPoint(left, top);
        if (DistanceSquared(x, y, tl.X, tl.Y) <= handleHitRadius * handleHitRadius) return TransformHandle.TopLeft;

        var tr = matrix.MapPoint(right, top);
        if (DistanceSquared(x, y, tr.X, tr.Y) <= handleHitRadius * handleHitRadius) return TransformHandle.TopRight;

        var br = matrix.MapPoint(right, bottom);
        if (DistanceSquared(x, y, br.X, br.Y) <= handleHitRadius * handleHitRadius) return TransformHandle.BottomRight;

        var bl = matrix.MapPoint(left, bottom);
        if (DistanceSquared(x, y, bl.X, bl.Y) <= handleHitRadius * handleHitRadius) return TransformHandle.BottomLeft;

        // Edge midpoint handles
        var tm = matrix.MapPoint(midX, top);
        if (DistanceSquared(x, y, tm.X, tm.Y) <= handleHitRadius * handleHitRadius) return TransformHandle.Top;

        var rm = matrix.MapPoint(right, midY);
        if (DistanceSquared(x, y, rm.X, rm.Y) <= handleHitRadius * handleHitRadius) return TransformHandle.Right;

        var bm = matrix.MapPoint(midX, bottom);
        if (DistanceSquared(x, y, bm.X, bm.Y) <= handleHitRadius * handleHitRadius) return TransformHandle.Bottom;

        var lm = matrix.MapPoint(left, midY);
        if (DistanceSquared(x, y, lm.X, lm.Y) <= handleHitRadius * handleHitRadius) return TransformHandle.Left;

        // Check inside quad via inverse transform
        if (matrix.TryInvert(out var inv))
        {
            var localPt = inv.MapPoint(x, y);
            if (localPt.X >= left && localPt.X <= right && localPt.Y >= top && localPt.Y <= bottom)
            {
                return TransformHandle.Move;
            }
        }

        return TransformHandle.None;
    }

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        float dx = x1 - x2;
        float dy = y1 - y2;
        return dx * dx + dy * dy;
    }

    public void OnPointerDown(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (_activeSession == null)
        {
            InitializeSession(ctx);
        }

        _activeHandle = HitTest(x, y);

        if (_activeHandle == TransformHandle.None)
        {
            Commit(ctx);
            return;
        }

        _dragStartX = x;
        _dragStartY = y;
        _initScaleX = _activeSession!.ScaleX;
        _initScaleY = _activeSession.ScaleY;
        _initRotation = _activeSession.RotationDegrees;
        _initTransX = _activeSession.TranslationX;
        _initTransY = _activeSession.TranslationY;

        var pivotWorld = new SKPoint(
            _activeSession.Pivot.X + _activeSession.TranslationX,
            _activeSession.Pivot.Y + _activeSession.TranslationY);
        _initAngleRad = MathF.Atan2(y - pivotWorld.Y, x - pivotWorld.X);
    }

    public void OnPointerMove(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (_activeSession == null)
        {
            return;
        }

        if (_activeHandle == TransformHandle.None)
        {
            _hoveredHandle = HitTest(x, y);
            return;
        }

        float dx = x - _dragStartX;
        float dy = y - _dragStartY;

        switch (_activeHandle)
        {
            case TransformHandle.Move:
                _activeSession.TranslationX = _initTransX + dx;
                _activeSession.TranslationY = _initTransY + dy;
                break;

            case TransformHandle.Rotate:
                var pivotWorld = new SKPoint(
                    _activeSession.Pivot.X + _activeSession.TranslationX,
                    _activeSession.Pivot.Y + _activeSession.TranslationY);
                float curAngleRad = MathF.Atan2(y - pivotWorld.Y, x - pivotWorld.X);
                float deltaDeg = (curAngleRad - _initAngleRad) * (180.0f / MathF.PI);
                float totalAngle = _initRotation + deltaDeg;

                if (IsCtrlDown)
                {
                    totalAngle = MathF.Round(totalAngle / 15.0f) * 15.0f;
                }
                _activeSession.RotationDegrees = totalAngle;
                break;

            case TransformHandle.BottomRight:
            case TransformHandle.TopLeft:
            case TransformHandle.TopRight:
            case TransformHandle.BottomLeft:
            case TransformHandle.Right:
            case TransformHandle.Bottom:
            case TransformHandle.Left:
            case TransformHandle.Top:
                ApplyScaleDelta(_activeHandle, dx, dy);
                break;
        }
    }

    private void ApplyScaleDelta(TransformHandle handle, float dx, float dy)
    {
        if (_activeSession == null) return;

        float origW = Math.Max(1.0f, _activeSession.OriginalBounds.Width);
        float origH = Math.Max(1.0f, _activeSession.OriginalBounds.Height);

        float factorX = 1.0f;
        float factorY = 1.0f;

        switch (handle)
        {
            case TransformHandle.BottomRight:
                factorX = (_initScaleX * origW + dx) / origW;
                factorY = (_initScaleY * origH + dy) / origH;
                break;
            case TransformHandle.Right:
                factorX = (_initScaleX * origW + dx) / origW;
                factorY = _initScaleY;
                break;
            case TransformHandle.Bottom:
                factorX = _initScaleX;
                factorY = (_initScaleY * origH + dy) / origH;
                break;
            case TransformHandle.TopLeft:
                factorX = (_initScaleX * origW - dx) / origW;
                factorY = (_initScaleY * origH - dy) / origH;
                break;
            case TransformHandle.Top:
                factorX = _initScaleX;
                factorY = (_initScaleY * origH - dy) / origH;
                break;
            case TransformHandle.Left:
                factorX = (_initScaleX * origW - dx) / origW;
                factorY = _initScaleY;
                break;
            case TransformHandle.TopRight:
                factorX = (_initScaleX * origW + dx) / origW;
                factorY = (_initScaleY * origH - dy) / origH;
                break;
            case TransformHandle.BottomLeft:
                factorX = (_initScaleX * origW - dx) / origW;
                factorY = (_initScaleY * origH + dy) / origH;
                break;
        }

        if (IsShiftDown)
        {
            float maxFactor = MathF.Max(MathF.Abs(factorX), MathF.Abs(factorY));
            factorX = MathF.Sign(factorX == 0 ? 1 : factorX) * maxFactor;
            factorY = MathF.Sign(factorY == 0 ? 1 : factorY) * maxFactor;
        }

        if (MathF.Abs(factorX) > 0.01f) _activeSession.ScaleX = factorX;
        if (MathF.Abs(factorY) > 0.01f) _activeSession.ScaleY = factorY;
    }

    public void OnPointerUp(ToolContext ctx, int x, int y)
    {
        _activeHandle = TransformHandle.None;
    }

    public void Commit(ToolContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (_activeSession == null || ctx.ActiveLayer == null) return;

        var result = PixelTransform.Transform(_activeSession);

        // Calculate sparse pixel deltas by comparing original pixels with transformed result
        var deltas = new List<PixelDelta>();

        // 1. Clear original bounds deltas
        int origW = _activeSession.OriginalBounds.Width;
        int origH = _activeSession.OriginalBounds.Height;
        for (int y = 0; y < origH; y++)
        {
            int srcY = _activeSession.OriginalBounds.Top + y;
            for (int x = 0; x < origW; x++)
            {
                int srcX = _activeSession.OriginalBounds.Left + x;
                var oldC = _activeSession.OriginalPixels[y * origW + x];
                if (oldC.A > 0)
                {
                    deltas.Add(new PixelDelta(srcX, srcY, oldC, PixelColor.Transparent));
                }
            }
        }

        // 2. Stamp new pixels and record deltas
        for (int dy = 0; dy < result.Height; dy++)
        {
            int dstY = result.Bounds.Top + dy;
            for (int dx = 0; dx < result.Width; dx++)
            {
                int dstX = result.Bounds.Left + dx;
                var newC = result.Pixels[dy * result.Width + dx];
                if (newC.A > 0)
                {
                    var existingC = ctx.ActiveLayer.GetPixel(dstX, dstY);
                    ctx.ActiveLayer.SetPixel(dstX, dstY, newC);

                    // If already cleared in step 1, update new color; otherwise add delta
                    int existingIdx = deltas.FindIndex(d => d.X == dstX && d.Y == dstY);
                    if (existingIdx >= 0)
                    {
                        deltas[existingIdx] = new PixelDelta(dstX, dstY, deltas[existingIdx].OldColor, newC);
                    }
                    else
                    {
                        deltas.Add(new PixelDelta(dstX, dstY, existingC, newC));
                    }
                }
            }
        }

        var cmd = new ApplyTransformCommand(
            ctx.ActiveLayer,
            deltas,
            _activeSession.OriginalBounds,
            result.Bounds,
            ctx.Selection,
            ctx.Canvas);

        ctx.History?.ExecuteCommand(cmd);
        ctx.ActiveLayer.Invalidate();

        if (ctx.Selection != null && ctx.Selection.IsActive)
        {
            ctx.Selection.Select(result.Bounds);
        }

        _activeSession = null;
        _activeHandle = TransformHandle.None;
    }

    public void Cancel(ToolContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (_activeSession == null || ctx.ActiveLayer == null) return;

        int w = _activeSession.OriginalBounds.Width;
        int h = _activeSession.OriginalBounds.Height;

        for (int y = 0; y < h; y++)
        {
            int dstY = _activeSession.OriginalBounds.Top + y;
            for (int x = 0; x < w; x++)
            {
                int dstX = _activeSession.OriginalBounds.Left + x;
                ctx.ActiveLayer.SetPixel(dstX, dstY, _activeSession.OriginalPixels[y * w + x]);
            }
        }

        ctx.ActiveLayer.Invalidate();
        _activeSession = null;
        _activeHandle = TransformHandle.None;
    }

    public IReadOnlyList<(int X, int Y, PixelColor Color)> GetPreviewPixels(ToolContext ctx)
    {
        return Array.Empty<(int X, int Y, PixelColor Color)>();
    }
}

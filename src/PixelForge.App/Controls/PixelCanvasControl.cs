using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using PixelForge.App.Services;
using PixelForge.App.ViewModels;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Tools;
using SkiaSharp;

namespace PixelForge.App.Controls;

/// <summary>
/// Hardware-accelerated SkiaSharp canvas control for pixel art creation with 60+ FPS rendering,
/// zero-allocation draw cycles, nearest-neighbor scaling, adaptive pixel grid, symmetry axes, and tool overlays.
/// </summary>
public class PixelCanvasControl : Control, ICustomDrawOperation
{
    private CanvasViewModel? _canvasViewModel;
    private ToolViewModel? _toolViewModel;
    private PaletteViewModel? _paletteViewModel;
    private HistoryViewModel? _historyViewModel;

    public static readonly DirectProperty<PixelCanvasControl, CanvasViewModel?> CanvasViewModelProperty =
        AvaloniaProperty.RegisterDirect<PixelCanvasControl, CanvasViewModel?>(
            nameof(CanvasViewModel),
            o => o.CanvasViewModel,
            (o, v) => o.CanvasViewModel = v);

    public static readonly DirectProperty<PixelCanvasControl, ToolViewModel?> ToolViewModelProperty =
        AvaloniaProperty.RegisterDirect<PixelCanvasControl, ToolViewModel?>(
            nameof(ToolViewModel),
            o => o.ToolViewModel,
            (o, v) => o.ToolViewModel = v);

    public static readonly DirectProperty<PixelCanvasControl, PaletteViewModel?> PaletteViewModelProperty =
        AvaloniaProperty.RegisterDirect<PixelCanvasControl, PaletteViewModel?>(
            nameof(PaletteViewModel),
            o => o.PaletteViewModel,
            (o, v) => o.PaletteViewModel = v);

    public static readonly DirectProperty<PixelCanvasControl, HistoryViewModel?> HistoryViewModelProperty =
        AvaloniaProperty.RegisterDirect<PixelCanvasControl, HistoryViewModel?>(
            nameof(HistoryViewModel),
            o => o.HistoryViewModel,
            (o, v) => o.HistoryViewModel = v);

    public CanvasViewModel? CanvasViewModel
    {
        get => _canvasViewModel;
        set
        {
            if (SetAndRaise(CanvasViewModelProperty, ref _canvasViewModel, value))
            {
                OnCanvasViewModelChanged();
            }
        }
    }

    public ToolViewModel? ToolViewModel
    {
        get => _toolViewModel;
        set => SetAndRaise(ToolViewModelProperty, ref _toolViewModel, value);
    }

    public PaletteViewModel? PaletteViewModel
    {
        get => _paletteViewModel;
        set => SetAndRaise(PaletteViewModelProperty, ref _paletteViewModel, value);
    }

    public HistoryViewModel? HistoryViewModel
    {
        get => _historyViewModel;
        set => SetAndRaise(HistoryViewModelProperty, ref _historyViewModel, value);
    }

    // Viewport transform
    private float _panX;
    private float _panY;
    private bool _hasInitializedViewport;

    // Pointer interaction states
    private bool _isPanning;
    private Point _lastPanPointerPos;
    private bool _isSpacePressed;
    private bool _isDrawing;
    private int _lastDrawnPixelX = -1;
    private int _lastDrawnPixelY = -1;
    private int _strokeStartPixelX = -1;
    private int _strokeStartPixelY = -1;
    private int _currentHoverPixelX = -1;
    private int _currentHoverPixelY = -1;
    private bool _isHovering;

    // Active stroke deltas for Undo/Redo
    private readonly List<PixelDelta> _activeStrokeDeltas = new(512);
    private readonly HashSet<long> _touchedPixelKeys = new(512);

    // Pre-allocated Skia resources for Zero-Allocation rendering
    private readonly SKPaint _checkerboardPaint = new() { IsAntialias = false };
    private readonly SKPaint _bitmapPaint = new() { FilterQuality = SKFilterQuality.None, IsAntialias = false };
    private readonly SKPaint _gridPaint = new()
    {
        Color = new SKColor(255, 255, 255, 40),
        StrokeWidth = 1f,
        IsAntialias = false,
        Style = SKPaintStyle.Stroke
    };
    private readonly SKPaint _gridDarkPaint = new()
    {
        Color = new SKColor(0, 0, 0, 30),
        StrokeWidth = 1f,
        IsAntialias = false,
        Style = SKPaintStyle.Stroke
    };
    private readonly SKPaint _symmetryPaint = new()
    {
        Color = new SKColor(0, 210, 255, 180),
        StrokeWidth = 1.5f,
        IsAntialias = false,
        PathEffect = SKPathEffect.CreateDash([6f, 4f], 0f),
        Style = SKPaintStyle.Stroke
    };
    private readonly SKPaint _previewStrokePaint = new()
    {
        StrokeWidth = 1f,
        IsAntialias = false,
        Style = SKPaintStyle.Stroke
    };
    private readonly SKPaint _previewFillPaint = new()
    {
        IsAntialias = false,
        Style = SKPaintStyle.Fill
    };
    private readonly SKPaint _selectionPaint = new()
    {
        Color = new SKColor(255, 255, 255, 220),
        StrokeWidth = 1.5f,
        IsAntialias = false,
        PathEffect = SKPathEffect.CreateDash([4f, 4f], 0f),
        Style = SKPaintStyle.Stroke
    };
    private readonly SKPaint _cursorHoverPaint = new()
    {
        Color = new SKColor(255, 255, 255, 120),
        StrokeWidth = 1f,
        IsAntialias = false,
        Style = SKPaintStyle.Stroke
    };

    // Offscreen layer composite buffer
    private SKBitmap? _compositeBitmap;
    private PixelColor[]? _compositePixels;
    private bool _isDirtyComposite = true;

    // ICustomDrawOperation bounds
    Rect ICustomDrawOperation.Bounds => new(0, 0, Bounds.Width, Bounds.Height);

    public PixelCanvasControl()
    {
        ClipToBounds = true;
        Focusable = true;

        InitializeCheckerboardShader();
    }

    private void InitializeCheckerboardShader()
    {
        // 16x16 2x2 tile checkerboard pattern (8x8 squares)
        using var tileBitmap = new SKBitmap(16, 16, SKColorType.Rgba8888, SKAlphaType.Premul);
        var lightColor = new SKColor(42, 46, 64);
        var darkColor = new SKColor(31, 35, 50);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                bool isLight = ((x / 8) + (y / 8)) % 2 == 0;
                tileBitmap.SetPixel(x, y, isLight ? lightColor : darkColor);
            }
        }

        var shader = SKShader.CreateBitmap(tileBitmap, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
        _checkerboardPaint.Shader = shader;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainViewModel mainVm)
        {
            CanvasViewModel = mainVm.CanvasViewModel;
            ToolViewModel = mainVm.ToolViewModel;
            PaletteViewModel = mainVm.PaletteViewModel;
            HistoryViewModel = mainVm.HistoryViewModel;
        }
    }

    private void OnCanvasViewModelChanged()
    {
        _isDirtyComposite = true;
        _hasInitializedViewport = false;

        if (CanvasViewModel?.Canvas != null)
        {
            CanvasViewModel.Canvas.PropertyChanged += (_, _) => MarkCompositeDirty();
            CanvasViewModel.Canvas.Layers.CollectionChanged += (_, _) => MarkCompositeDirty();
            foreach (var layer in CanvasViewModel.Canvas.Layers)
            {
                layer.PropertyChanged += (_, _) => MarkCompositeDirty();
            }
        }

        InvalidateVisual();
    }

    public void MarkCompositeDirty()
    {
        _isDirtyComposite = true;
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (!_hasInitializedViewport && CanvasViewModel != null && Bounds.Width > 0 && Bounds.Height > 0)
        {
            CenterCanvasInViewport();
            _hasInitializedViewport = true;
        }
    }

    private void CenterCanvasInViewport()
    {
        if (CanvasViewModel == null) return;
        float cw = CanvasViewModel.CanvasWidth * CanvasViewModel.Zoom;
        float ch = CanvasViewModel.CanvasHeight * CanvasViewModel.Zoom;
        _panX = (float)(Bounds.Width - cw) / 2f;
        _panY = (float)(Bounds.Height - ch) / 2f;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        context.Custom(this);
    }

    bool ICustomDrawOperation.HitTest(Point p) => Bounds.Contains(p);

    bool IEquatable<ICustomDrawOperation>.Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);

    void IDisposable.Dispose()
    {
        // Reusable Skia paints and composite buffers are retained across frames for the lifetime
        // of PixelCanvasControl. Avalonia calls ICustomDrawOperation.Dispose() at the conclusion
        // of every frame; disposing them here causes an AccessViolationException on frame 2.
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _compositeBitmap?.Dispose();
        _compositeBitmap = null;
        _checkerboardPaint.Dispose();
        _bitmapPaint.Dispose();
        _gridPaint.Dispose();
        _gridDarkPaint.Dispose();
        _symmetryPaint.Dispose();
        _previewStrokePaint.Dispose();
        _previewFillPaint.Dispose();
        _selectionPaint.Dispose();
        _cursorHoverPaint.Dispose();
    }

    void ICustomDrawOperation.Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as ISkiaSharpApiLeaseFeature;
        if (leaseFeature == null) return;

        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;
        var vm = CanvasViewModel;
        if (vm == null || vm.Canvas == null) return;

        float zoom = vm.Zoom;
        int canvasW = vm.CanvasWidth;
        int canvasH = vm.CanvasHeight;
        float destW = canvasW * zoom;
        float destH = canvasH * zoom;
        var destRect = new SKRect(_panX, _panY, _panX + destW, _panY + destH);

        int saveCount = canvas.Save();
        try
        {
            // 1. Checkerboard transparency background
            canvas.DrawRect(destRect, _checkerboardPaint);

            // 2. Offscreen composite of layers with SKFilterQuality.None (Nearest Neighbor)
            EnsureCompositeUpdated(vm.Canvas);
            if (_compositeBitmap != null)
            {
                canvas.DrawBitmap(_compositeBitmap, destRect, _bitmapPaint);
            }

            // 3. Tool live preview overlay (e.g. current shape, line, selection)
            RenderToolPreview(canvas, destRect, zoom);

            // 4. Adaptive pixel grid when zoom >= 4x
            if (vm.ShowGrid && zoom >= 4f)
            {
                RenderPixelGrid(canvas, destRect, canvasW, canvasH, zoom);
            }

            // 5. Symmetry axes X / Y
            if (vm.ShowSymmetryX || vm.ShowSymmetryY)
            {
                RenderSymmetryAxes(canvas, destRect, canvasW, canvasH, zoom, vm.ShowSymmetryX, vm.ShowSymmetryY);
            }

            // 6. Selection outline (marching ants)
            if (vm.Selection.IsActive)
            {
                RenderSelectionMarquee(canvas, destRect, zoom, vm.Selection);
            }

            // 7. Hover cursor pixel outline
            if (_isHovering && _currentHoverPixelX >= 0 && _currentHoverPixelX < canvasW &&
                _currentHoverPixelY >= 0 && _currentHoverPixelY < canvasH && !_isDrawing)
            {
                RenderCursorHover(canvas, destRect, zoom);
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("PixelCanvasControl", "Exception occurred during Skia canvas render pass", ex);
            CrashReportService.Instance.CaptureCrash(ex, "PixelCanvasControl.Render", isTerminating: false);
        }
        finally
        {
            canvas.RestoreToCount(saveCount);
        }
    }

    private void EnsureCompositeUpdated(PixelCanvas pixelCanvas)
    {
        int w = pixelCanvas.Width;
        int h = pixelCanvas.Height;
        int total = w * h;

        if (_compositeBitmap == null || _compositeBitmap.Width != w || _compositeBitmap.Height != h)
        {
            _compositeBitmap?.Dispose();
            _compositeBitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            _compositePixels = new PixelColor[total];
            _isDirtyComposite = true;
        }

        if (_isDirtyComposite && _compositePixels != null)
        {
            pixelCanvas.GetCompositePixels(_compositePixels);

            unsafe
            {
                fixed (PixelColor* src = _compositePixels)
                {
                    Buffer.MemoryCopy(src, (void*)_compositeBitmap.GetPixels(), (long)total * 4, (long)total * 4);
                }
            }
            _compositeBitmap.NotifyPixelsChanged();

            _isDirtyComposite = false;
        }
    }

    private void RenderPixelGrid(SKCanvas canvas, SKRect destRect, int canvasW, int canvasH, float zoom)
    {
        float left = destRect.Left;
        float top = destRect.Top;
        float right = destRect.Right;
        float bottom = destRect.Bottom;

        // Vertical lines
        for (int x = 0; x <= canvasW; x++)
        {
            float posX = left + (x * zoom);
            canvas.DrawLine(posX, top, posX, bottom, _gridPaint);
        }

        // Horizontal lines
        for (int y = 0; y <= canvasH; y++)
        {
            float posY = top + (y * zoom);
            canvas.DrawLine(left, posY, right, posY, _gridPaint);
        }
    }

    private void RenderSymmetryAxes(SKCanvas canvas, SKRect destRect, int canvasW, int canvasH, float zoom, bool symX, bool symY)
    {
        if (symX)
        {
            float axisX = destRect.Left + ((float)canvasW / 2f * zoom);
            canvas.DrawLine(axisX, destRect.Top, axisX, destRect.Bottom, _symmetryPaint);
        }

        if (symY)
        {
            float axisY = destRect.Top + ((float)canvasH / 2f * zoom);
            canvas.DrawLine(destRect.Left, axisY, destRect.Right, axisY, _symmetryPaint);
        }
    }

    private void RenderSelectionMarquee(SKCanvas canvas, SKRect destRect, float zoom, SelectionModel selection)
    {
        var b = selection.Bounds;
        int ox = selection.IsFloating ? selection.OffsetX : 0;
        int oy = selection.IsFloating ? selection.OffsetY : 0;

        float selLeft = destRect.Left + ((b.Left + ox) * zoom);
        float selTop = destRect.Top + ((b.Top + oy) * zoom);
        float selRight = destRect.Left + ((b.Right + ox) * zoom);
        float selBottom = destRect.Top + ((b.Bottom + oy) * zoom);

        canvas.DrawRect(new SKRect(selLeft, selTop, selRight, selBottom), _selectionPaint);
    }

    private void RenderCursorHover(SKCanvas canvas, SKRect destRect, float zoom)
    {
        int brushSize = ToolViewModel?.BrushSize ?? 1;
        int half = brushSize / 2;
        int startX = _currentHoverPixelX - half;
        int startY = _currentHoverPixelY - half;

        float hoverLeft = destRect.Left + (startX * zoom);
        float hoverTop = destRect.Top + (startY * zoom);
        float hoverW = brushSize * zoom;
        float hoverH = brushSize * zoom;

        canvas.DrawRect(new SKRect(hoverLeft, hoverTop, hoverLeft + hoverW, hoverTop + hoverH), _cursorHoverPaint);
    }

    private void RenderToolPreview(SKCanvas canvas, SKRect destRect, float zoom)
    {
        if (!_isDrawing || ToolViewModel == null || PaletteViewModel == null) return;

        var color = ToolViewModel.ActiveTool == CanvasToolType.Eraser
            ? SKColors.Transparent
            : PaletteViewModel.PrimaryColor.ToSKColor();

        _previewStrokePaint.Color = new SKColor(color.Red, color.Green, color.Blue, 180);
        _previewFillPaint.Color = new SKColor(color.Red, color.Green, color.Blue, 120);

        switch (ToolViewModel.ActiveTool)
        {
            case CanvasToolType.Line:
                float x1 = destRect.Left + ((_strokeStartPixelX + 0.5f) * zoom);
                float y1 = destRect.Top + ((_strokeStartPixelY + 0.5f) * zoom);
                float x2 = destRect.Left + ((_currentHoverPixelX + 0.5f) * zoom);
                float y2 = destRect.Top + ((_currentHoverPixelY + 0.5f) * zoom);
                _previewStrokePaint.StrokeWidth = Math.Max(1f, ToolViewModel.BrushSize * zoom);
                canvas.DrawLine(x1, y1, x2, y2, _previewStrokePaint);
                break;

            case CanvasToolType.Rectangle:
                int rx0 = Math.Min(_strokeStartPixelX, _currentHoverPixelX);
                int ry0 = Math.Min(_strokeStartPixelY, _currentHoverPixelY);
                int rx1 = Math.Max(_strokeStartPixelX, _currentHoverPixelX) + 1;
                int ry1 = Math.Max(_strokeStartPixelY, _currentHoverPixelY) + 1;
                var rect = new SKRect(
                    destRect.Left + (rx0 * zoom),
                    destRect.Top + (ry0 * zoom),
                    destRect.Left + (rx1 * zoom),
                    destRect.Top + (ry1 * zoom));

                if (ToolViewModel.IsShapeFilled)
                {
                    canvas.DrawRect(rect, _previewFillPaint);
                }
                canvas.DrawRect(rect, _previewStrokePaint);
                break;

            case CanvasToolType.Ellipse:
                int ex0 = Math.Min(_strokeStartPixelX, _currentHoverPixelX);
                int ey0 = Math.Min(_strokeStartPixelY, _currentHoverPixelY);
                int ex1 = Math.Max(_strokeStartPixelX, _currentHoverPixelX) + 1;
                int ey1 = Math.Max(_strokeStartPixelY, _currentHoverPixelY) + 1;
                var oval = new SKRect(
                    destRect.Left + (ex0 * zoom),
                    destRect.Top + (ey0 * zoom),
                    destRect.Left + (ex1 * zoom),
                    destRect.Top + (ey1 * zoom));

                if (ToolViewModel.IsShapeFilled)
                {
                    canvas.DrawOval(oval, _previewFillPaint);
                }
                canvas.DrawOval(oval, _previewStrokePaint);
                break;

            case CanvasToolType.Gradient:
                float gx1 = destRect.Left + ((_strokeStartPixelX + 0.5f) * zoom);
                float gy1 = destRect.Top + ((_strokeStartPixelY + 0.5f) * zoom);
                float gx2 = destRect.Left + ((_currentHoverPixelX + 0.5f) * zoom);
                float gy2 = destRect.Top + ((_currentHoverPixelY + 0.5f) * zoom);
                _previewStrokePaint.StrokeWidth = 2f;
                canvas.DrawLine(gx1, gy1, gx2, gy2, _previewStrokePaint);
                if (ToolViewModel.GradientType == GradientType.Radial)
                {
                    float rad = MathF.Sqrt((gx2 - gx1) * (gx2 - gx1) + (gy2 - gy1) * (gy2 - gy1));
                    canvas.DrawCircle(gx1, gy1, rad, _previewStrokePaint);
                }
                break;

            case CanvasToolType.Selection:
                int sx0 = Math.Min(_strokeStartPixelX, _currentHoverPixelX);
                int sy0 = Math.Min(_strokeStartPixelY, _currentHoverPixelY);
                int sx1 = Math.Max(_strokeStartPixelX, _currentHoverPixelX) + 1;
                int sy1 = Math.Max(_strokeStartPixelY, _currentHoverPixelY) + 1;
                var selRect = new SKRect(
                    destRect.Left + (sx0 * zoom),
                    destRect.Top + (sy0 * zoom),
                    destRect.Left + (sx1 * zoom),
                    destRect.Top + (sy1 * zoom));
                canvas.DrawRect(selRect, _selectionPaint);
                break;
        }
    }

    #region Input Event Handling

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (CanvasViewModel == null) return;

        var pos = e.GetPosition(this);
        float oldZoom = CanvasViewModel.Zoom;
        float factor = e.Delta.Y > 0 ? 1.25f : (1f / 1.25f);
        float newZoom = Math.Clamp(oldZoom * factor, CanvasViewModel.MinZoom, CanvasViewModel.MaxZoom);

        if (Math.Abs(newZoom - oldZoom) > 0.001f)
        {
            // Zoom focused on cursor point
            float mouseX = (float)pos.X;
            float mouseY = (float)pos.Y;

            float canvasX = (mouseX - _panX) / oldZoom;
            float canvasY = (mouseY - _panY) / oldZoom;

            _panX = mouseX - (canvasX * newZoom);
            _panY = mouseY - (canvasY * newZoom);

            CanvasViewModel.Zoom = newZoom;
            InvalidateVisual();
        }

        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        _lastPanPointerPos = point.Position;

        // Middle button or Space+LeftDrag = Pan
        if (point.Properties.IsMiddleButtonPressed || (_isSpacePressed && point.Properties.IsLeftButtonPressed))
        {
            _isPanning = true;
            Cursor = new Cursor(StandardCursorType.Hand);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (point.Properties.IsLeftButtonPressed || point.Properties.IsRightButtonPressed)
        {
            var (px, py, isInside) = ScreenToCanvasPixel(point.Position);
            if (!isInside || CanvasViewModel == null || ToolViewModel == null || PaletteViewModel == null)
            {
                return;
            }

            e.Pointer.Capture(this);
            _isDrawing = true;
            _strokeStartPixelX = px;
            _strokeStartPixelY = py;
            _lastDrawnPixelX = px;
            _lastDrawnPixelY = py;
            _activeStrokeDeltas.Clear();
            _touchedPixelKeys.Clear();

            var drawColor = point.Properties.IsRightButtonPressed
                ? PaletteViewModel.SecondaryColor
                : PaletteViewModel.PrimaryColor;

            ExecuteToolPointerDown(px, py, drawColor);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);

        if (_isPanning)
        {
            float dx = (float)(pos.X - _lastPanPointerPos.X);
            float dy = (float)(pos.Y - _lastPanPointerPos.Y);
            _panX += dx;
            _panY += dy;
            _lastPanPointerPos = pos;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        var (px, py, isInside) = ScreenToCanvasPixel(pos);
        _currentHoverPixelX = px;
        _currentHoverPixelY = py;
        _isHovering = isInside;

        CanvasViewModel?.UpdateCursor(px, py, isInside);

        if (_isDrawing && isInside && ToolViewModel != null && PaletteViewModel != null)
        {
            var pointerProps = e.GetCurrentPoint(this).Properties;
            var drawColor = pointerProps.IsRightButtonPressed
                ? PaletteViewModel.SecondaryColor
                : PaletteViewModel.PrimaryColor;

            ExecuteToolPointerMove(px, py, drawColor);
            InvalidateVisual();
        }
        else
        {
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isPanning)
        {
            _isPanning = false;
            Cursor = _isSpacePressed ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (_isDrawing)
        {
            _isDrawing = false;
            e.Pointer.Capture(null);

            var (px, py, _) = ScreenToCanvasPixel(e.GetPosition(this));
            ExecuteToolPointerUp(px, py);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Space)
        {
            _isSpacePressed = true;
            if (!_isDrawing)
            {
                Cursor = new Cursor(StandardCursorType.Hand);
            }
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space)
        {
            _isSpacePressed = false;
            if (!_isPanning)
            {
                Cursor = Cursor.Default;
            }
        }
    }

    #endregion

    #region Tool Execution Logic

    private void ExecuteToolPointerDown(int px, int py, PixelColor drawColor)
    {
        if (ToolViewModel == null || CanvasViewModel?.Canvas.ActiveLayer == null) return;
        var layer = CanvasViewModel.Canvas.ActiveLayer;

        switch (ToolViewModel.ActiveTool)
        {
            case CanvasToolType.Pencil:
            case CanvasToolType.DitherBrush:
                PlotBrushWithSymmetry(layer, px, py, drawColor);
                MarkCompositeDirty();
                break;

            case CanvasToolType.Eraser:
                PlotBrushWithSymmetry(layer, px, py, PixelColor.Transparent);
                MarkCompositeDirty();
                break;

            case CanvasToolType.Fill:
                var targetColor = layer.GetPixel(px, py);
                FloodFill(layer, px, py, targetColor, drawColor);
                if (_activeStrokeDeltas.Count > 0 && HistoryViewModel != null)
                {
                    string desc = ToolViewModel.FillContiguous ? "Flood Fill" : "Replace Color";
                    var cmd = new DrawPixelsCommand(layer, _activeStrokeDeltas.ToArray(), desc);
                    HistoryViewModel.Service.ExecuteCommand(cmd);
                    _activeStrokeDeltas.Clear();
                }
                MarkCompositeDirty();
                break;

            case CanvasToolType.Eyedropper:
                var picked = layer.GetPixel(px, py);
                if (PaletteViewModel != null)
                {
                    PaletteViewModel.PrimaryColor = picked;
                }
                break;

            case CanvasToolType.Selection:
                if (CanvasViewModel.Selection.IsActive && CanvasViewModel.Selection.Contains(px, py))
                {
                    // Drag floating selection
                }
                break;
        }
    }

    private void ExecuteToolPointerMove(int px, int py, PixelColor drawColor)
    {
        if (ToolViewModel == null || CanvasViewModel?.Canvas.ActiveLayer == null) return;
        var layer = CanvasViewModel.Canvas.ActiveLayer;

        switch (ToolViewModel.ActiveTool)
        {
            case CanvasToolType.Pencil:
            case CanvasToolType.DitherBrush:
                BresenhamLine(_lastDrawnPixelX, _lastDrawnPixelY, px, py, (x, y) =>
                {
                    PlotBrushWithSymmetry(layer, x, y, drawColor);
                });
                _lastDrawnPixelX = px;
                _lastDrawnPixelY = py;
                MarkCompositeDirty();
                break;

            case CanvasToolType.Eraser:
                BresenhamLine(_lastDrawnPixelX, _lastDrawnPixelY, px, py, (x, y) =>
                {
                    PlotBrushWithSymmetry(layer, x, y, PixelColor.Transparent);
                });
                _lastDrawnPixelX = px;
                _lastDrawnPixelY = py;
                MarkCompositeDirty();
                break;
        }
    }

    private void ExecuteToolPointerUp(int px, int py)
    {
        if (ToolViewModel == null || CanvasViewModel?.Canvas.ActiveLayer == null) return;
        var layer = CanvasViewModel.Canvas.ActiveLayer;
        var drawColor = PaletteViewModel?.PrimaryColor ?? PixelColor.Black;

        switch (ToolViewModel.ActiveTool)
        {
            case CanvasToolType.Pencil:
            case CanvasToolType.Eraser:
            case CanvasToolType.DitherBrush:
                if (_activeStrokeDeltas.Count > 0 && HistoryViewModel != null)
                {
                    string desc = ToolViewModel.ActiveTool switch
                    {
                        CanvasToolType.Pencil => "Pencil Stroke",
                        CanvasToolType.Eraser => "Eraser Stroke",
                        CanvasToolType.DitherBrush => "Dither Stroke",
                        _ => "Stroke"
                    };
                    var cmd = new DrawPixelsCommand(layer, _activeStrokeDeltas.ToArray(), desc);
                    HistoryViewModel.Service.ExecuteCommand(cmd);
                    _activeStrokeDeltas.Clear();
                    _touchedPixelKeys.Clear();
                }
                break;

            case CanvasToolType.Line:
                BresenhamLine(_strokeStartPixelX, _strokeStartPixelY, px, py, (x, y) =>
                {
                    PlotBrushWithSymmetry(layer, x, y, drawColor);
                });
                CommitActiveDeltas(layer, "Draw Line");
                break;

            case CanvasToolType.Rectangle:
                PlotRectangle(layer, _strokeStartPixelX, _strokeStartPixelY, px, py, drawColor, ToolViewModel.IsShapeFilled);
                CommitActiveDeltas(layer, "Draw Rectangle");
                break;

            case CanvasToolType.Ellipse:
                PlotEllipse(layer, _strokeStartPixelX, _strokeStartPixelY, px, py, drawColor, ToolViewModel.IsShapeFilled);
                CommitActiveDeltas(layer, "Draw Ellipse");
                break;

            case CanvasToolType.Gradient:
                ApplyInteractiveGradient(layer, _strokeStartPixelX, _strokeStartPixelY, px, py);
                CommitActiveDeltas(layer, "Gradient Fill");
                break;

            case CanvasToolType.Selection:
                int rx0 = Math.Min(_strokeStartPixelX, px);
                int ry0 = Math.Min(_strokeStartPixelY, py);
                int rx1 = Math.Max(_strokeStartPixelX, px) + 1;
                int ry1 = Math.Max(_strokeStartPixelY, py) + 1;
                CanvasViewModel.Selection.Select(new SKRectI(rx0, ry0, rx1, ry1));
                break;
        }

        MarkCompositeDirty();
    }

    private void CommitActiveDeltas(Layer layer, string description)
    {
        if (_activeStrokeDeltas.Count > 0 && HistoryViewModel != null)
        {
            var cmd = new DrawPixelsCommand(layer, _activeStrokeDeltas.ToArray(), description);
            HistoryViewModel.Service.ExecuteCommand(cmd);
            _activeStrokeDeltas.Clear();
            _touchedPixelKeys.Clear();
        }
    }

    private void PlotBrushWithSymmetry(Layer layer, int cx, int cy, PixelColor color)
    {
        int brushSize = ToolViewModel?.BrushSize ?? 1;
        int half = brushSize / 2;
        bool symX = CanvasViewModel?.ShowSymmetryX ?? false;
        bool symY = CanvasViewModel?.ShowSymmetryY ?? false;
        int w = layer.Width;
        int h = layer.Height;
        var pattern = ToolViewModel?.SelectedDitherPattern ?? DitherPattern.None;
        var secondaryColor = PaletteViewModel?.SecondaryColor ?? PixelColor.White;

        for (int dy = -half; dy < brushSize - half; dy++)
        {
            for (int dx = -half; dx < brushSize - half; dx++)
            {
                int px = cx + dx;
                int py = cy + dy;

                PlotSinglePixel(layer, px, py, color, secondaryColor, pattern);

                if (symX)
                {
                    int mx = (w - 1) - px;
                    PlotSinglePixel(layer, mx, py, color, secondaryColor, pattern);
                }

                if (symY)
                {
                    int my = (h - 1) - py;
                    PlotSinglePixel(layer, px, my, color, secondaryColor, pattern);
                }

                if (symX && symY)
                {
                    int mx = (w - 1) - px;
                    int my = (h - 1) - py;
                    PlotSinglePixel(layer, mx, my, color, secondaryColor, pattern);
                }
            }
        }
    }

    private void PlotSinglePixel(Layer layer, int x, int y, PixelColor primaryColor, PixelColor secondaryColor, DitherPattern pattern)
    {
        var selection = CanvasViewModel?.Selection;
        if (selection != null && selection.IsActive && !selection.Contains(x, y))
        {
            return;
        }

        PixelColor finalColor = primaryColor;
        if (ToolViewModel?.ActiveTool == CanvasToolType.Eraser)
        {
            finalColor = PixelColor.Transparent;
        }
        else if (pattern != DitherPattern.None)
        {
            finalColor = DitherHelper.EvaluateDitherColor(x, y, primaryColor, secondaryColor, pattern);
        }

        ApplyPixel(layer, x, y, finalColor);
    }

    private void ApplyPixel(Layer layer, int x, int y, PixelColor newColor)
    {
        if ((uint)x >= (uint)layer.Width || (uint)y >= (uint)layer.Height) return;

        long key = ((long)y << 32) | (uint)x;
        var oldColor = layer.GetPixel(x, y);

        if (_touchedPixelKeys.Add(key))
        {
            _activeStrokeDeltas.Add(new PixelDelta(x, y, oldColor, newColor));
        }

        layer.SetPixel(x, y, newColor);
    }

    private void FloodFill(Layer layer, int startX, int startY, PixelColor targetColor, PixelColor replacementColor)
    {
        int tolerance = ToolViewModel != null ? (int)(ToolViewModel.FillTolerance * 2.55f) : 0;
        bool contiguous = ToolViewModel?.FillContiguous ?? true;
        var pattern = ToolViewModel?.SelectedDitherPattern ?? DitherPattern.None;
        var secondaryColor = PaletteViewModel?.SecondaryColor ?? PixelColor.White;
        var selection = CanvasViewModel?.Selection;

        if (tolerance == 0 && targetColor == replacementColor && pattern == DitherPattern.None) return;
        int w = layer.Width;
        int h = layer.Height;
        if ((uint)startX >= (uint)w || (uint)startY >= (uint)h) return;

        bool Matches(PixelColor c) => tolerance == 0 ? c == targetColor : PixelColor.Distance(c, targetColor) <= tolerance;

        if (!contiguous)
        {
            int minX = selection != null && selection.IsActive ? Math.Max(0, selection.Bounds.Left) : 0;
            int maxX = selection != null && selection.IsActive ? Math.Min(w, selection.Bounds.Right) : w;
            int minY = selection != null && selection.IsActive ? Math.Max(0, selection.Bounds.Top) : 0;
            int maxY = selection != null && selection.IsActive ? Math.Min(h, selection.Bounds.Bottom) : h;

            for (int y = minY; y < maxY; y++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    if (selection != null && selection.IsActive && !selection.Contains(x, y)) continue;

                    var current = layer.GetPixel(x, y);
                    if (Matches(current))
                    {
                        var finalColor = pattern != DitherPattern.None
                            ? DitherHelper.EvaluateDitherColor(x, y, replacementColor, secondaryColor, pattern)
                            : replacementColor;

                        if (current != finalColor)
                        {
                            ApplyPixel(layer, x, y, finalColor);
                        }
                    }
                }
            }
            return;
        }

        var visited = new bool[w * h];
        var queue = new Queue<(int X, int Y)>(128);
        queue.Enqueue((startX, startY));
        visited[startY * w + startX] = true;

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            var current = layer.GetPixel(x, y);
            if (!Matches(current)) continue;

            var finalColor = pattern != DitherPattern.None
                ? DitherHelper.EvaluateDitherColor(x, y, replacementColor, secondaryColor, pattern)
                : replacementColor;

            if (current != finalColor)
            {
                ApplyPixel(layer, x, y, finalColor);
            }

            if (x > 0 && !visited[y * w + (x - 1)] && Matches(layer.GetPixel(x - 1, y)))
            {
                if (selection == null || !selection.IsActive || selection.Contains(x - 1, y))
                {
                    visited[y * w + (x - 1)] = true;
                    queue.Enqueue((x - 1, y));
                }
            }
            if (x + 1 < w && !visited[y * w + (x + 1)] && Matches(layer.GetPixel(x + 1, y)))
            {
                if (selection == null || !selection.IsActive || selection.Contains(x + 1, y))
                {
                    visited[y * w + (x + 1)] = true;
                    queue.Enqueue((x + 1, y));
                }
            }
            if (y > 0 && !visited[(y - 1) * w + x] && Matches(layer.GetPixel(x, y - 1)))
            {
                if (selection == null || !selection.IsActive || selection.Contains(x, y - 1))
                {
                    visited[(y - 1) * w + x] = true;
                    queue.Enqueue((x, y - 1));
                }
            }
            if (y + 1 < h && !visited[(y + 1) * w + x] && Matches(layer.GetPixel(x, y + 1)))
            {
                if (selection == null || !selection.IsActive || selection.Contains(x, y + 1))
                {
                    visited[(y + 1) * w + x] = true;
                    queue.Enqueue((x, y + 1));
                }
            }
        }
    }

    private void ApplyInteractiveGradient(Layer layer, int x0, int y0, int x1, int y1)
    {
        int w = layer.Width;
        int h = layer.Height;
        var selection = CanvasViewModel?.Selection;
        int minX = selection != null && selection.IsActive ? Math.Max(0, selection.Bounds.Left) : 0;
        int maxX = selection != null && selection.IsActive ? Math.Min(w, selection.Bounds.Right) : w;
        int minY = selection != null && selection.IsActive ? Math.Max(0, selection.Bounds.Top) : 0;
        int maxY = selection != null && selection.IsActive ? Math.Min(h, selection.Bounds.Bottom) : h;

        float dx = x1 - x0;
        float dy = y1 - y0;
        float lengthSq = dx * dx + dy * dy;
        float radius = MathF.Sqrt(lengthSq);
        bool isRadial = ToolViewModel?.GradientType == GradientType.Radial;
        var pattern = ToolViewModel?.SelectedDitherPattern ?? DitherPattern.None;
        var primary = PaletteViewModel?.PrimaryColor ?? PixelColor.Black;
        var secondary = PaletteViewModel?.SecondaryColor ?? PixelColor.White;

        for (int y = minY; y < maxY; y++)
        {
            for (int x = minX; x < maxX; x++)
            {
                if (selection != null && selection.IsActive && !selection.Contains(x, y)) continue;

                float t;
                if (!isRadial)
                {
                    if (lengthSq <= 0.0001f) t = 0f;
                    else
                    {
                        float ux = x - x0;
                        float uy = y - y0;
                        t = Math.Clamp((ux * dx + uy * dy) / lengthSq, 0f, 1f);
                    }
                }
                else
                {
                    if (radius <= 0.0001f) t = 0f;
                    else
                    {
                        float dist = MathF.Sqrt((x - x0) * (x - x0) + (y - y0) * (y - y0));
                        t = Math.Clamp(dist / radius, 0f, 1f);
                    }
                }

                PixelColor newColor;
                if (pattern == DitherPattern.None)
                {
                    newColor = PixelColor.Lerp(primary, secondary, t);
                }
                else
                {
                    bool useSec = DitherHelper.ShouldPlot(x, y, pattern, t);
                    newColor = useSec ? secondary : primary;
                }

                ApplyPixel(layer, x, y, newColor);
            }
        }
    }

    private void PlotRectangle(Layer layer, int x0, int y0, int x1, int y1, PixelColor color, bool fill)
    {
        int rx0 = Math.Min(x0, x1);
        int ry0 = Math.Min(y0, y1);
        int rx1 = Math.Max(x0, x1);
        int ry1 = Math.Max(y0, y1);

        if (fill)
        {
            for (int y = ry0; y <= ry1; y++)
            {
                for (int x = rx0; x <= rx1; x++)
                {
                    PlotBrushWithSymmetry(layer, x, y, color);
                }
            }
        }
        else
        {
            for (int x = rx0; x <= rx1; x++)
            {
                PlotBrushWithSymmetry(layer, x, ry0, color);
                PlotBrushWithSymmetry(layer, x, ry1, color);
            }
            for (int y = ry0; y <= ry1; y++)
            {
                PlotBrushWithSymmetry(layer, rx0, y, color);
                PlotBrushWithSymmetry(layer, rx1, y, color);
            }
        }
    }

    private void PlotEllipse(Layer layer, int x0, int y0, int x1, int y1, PixelColor color, bool fill)
    {
        int minX = Math.Min(x0, x1);
        int maxX = Math.Max(x0, x1);
        int minY = Math.Min(y0, y1);
        int maxY = Math.Max(y0, y1);

        float a = (maxX - minX) / 2f;
        float b = (maxY - minY) / 2f;
        float centerX = minX + a;
        float centerY = minY + b;

        if (a <= 0.001f || b <= 0.001f)
        {
            PlotBrushWithSymmetry(layer, minX, minY, color);
            return;
        }

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = (x - centerX) / a;
                float dy = (y - centerY) / b;
                float distSq = dx * dx + dy * dy;

                if (fill && distSq <= 1.0f)
                {
                    PlotBrushWithSymmetry(layer, x, y, color);
                }
                else if (!fill && Math.Abs(distSq - 1.0f) <= (1f / Math.Min(a, b)))
                {
                    PlotBrushWithSymmetry(layer, x, y, color);
                }
            }
        }
    }

    private static void BresenhamLine(int x0, int y0, int x1, int y1, Action<int, int> plot)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            plot(x0, y0);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private (int X, int Y, bool IsInside) ScreenToCanvasPixel(Point screenPos)
    {
        if (CanvasViewModel == null) return (-1, -1, false);

        float zoom = CanvasViewModel.Zoom;
        int px = (int)Math.Floor((screenPos.X - _panX) / zoom);
        int py = (int)Math.Floor((screenPos.Y - _panY) / zoom);

        bool isInside = px >= 0 && px < CanvasViewModel.CanvasWidth &&
                        py >= 0 && py < CanvasViewModel.CanvasHeight;

        return (px, py, isInside);
    }

    #endregion
}

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
using PixelForge.Core.Algorithms;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Patterns;
using PixelForge.Core.Tools;
using PixelForge.Core.Transforms;
using SkiaSharp;

namespace PixelForge.App.Controls;

/// <summary>
/// Hardware-accelerated SkiaSharp canvas control for pixel art creation with 60+ FPS rendering,
/// zero-allocation draw cycles, nearest-neighbor scaling, adaptive pixel grid, symmetry axes, tool overlays,
/// and seamless 3x3 tiled mode.
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

    public static readonly DirectProperty<PixelCanvasControl, bool> IsTiledModeProperty =
        AvaloniaProperty.RegisterDirect<PixelCanvasControl, bool>(
            nameof(IsTiledMode),
            o => o.IsTiledMode,
            (o, v) => o.IsTiledMode = v);

    private bool _isTiledMode;

    /// <summary>
    /// Gets or sets whether seamless 3x3 tiled view mode is enabled.
    /// When active, 3x3 repetitions of the composite are rendered and draw operations wrap periodically.
    /// </summary>
    public bool IsTiledMode
    {
        get => _isTiledMode;
        set
        {
            if (SetAndRaise(IsTiledModeProperty, ref _isTiledMode, value))
            {
                InvalidateVisual();
            }
        }
    }

    public static readonly DirectProperty<PixelCanvasControl, bool> IsPlayingProperty =
        AvaloniaProperty.RegisterDirect<PixelCanvasControl, bool>(
            nameof(IsPlaying),
            o => o.IsPlaying,
            (o, v) => o.IsPlaying = v);

    private bool _isPlaying;

    /// <summary>
    /// Gets or sets whether animation playback is currently running on the canvas.
    /// When playing, pointer interactions pause playback rather than drawing strokes.
    /// </summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetAndRaise(IsPlayingProperty, ref _isPlaying, value);
    }

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
        set
        {
            if (_historyViewModel != value)
            {
                if (_historyViewModel != null)
                {
                    _historyViewModel.Service.HistoryChanged -= MarkCompositeDirty;
                }
                if (SetAndRaise(HistoryViewModelProperty, ref _historyViewModel, value))
                {
                    if (_historyViewModel != null)
                    {
                        _historyViewModel.Service.HistoryChanged += MarkCompositeDirty;
                    }
                }
            }
        }
    }

    // Cached cursors to prevent allocations during mouse interactions
    private static Cursor? _panHandCursor;
    private static Cursor PanHandCursor => _panHandCursor ??= new Cursor(StandardCursorType.Hand);

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

    // Stroke stabilizer & Pixel-Perfect line smoothing
    private readonly StrokeStabilizer _strokeStabilizer = new();
    private readonly List<StrokeStep> _strokeSteps = new(512);

    // Pre-allocated Skia resources for Zero-Allocation rendering
    private readonly SKPaint _checkerboardPaint = new() { IsAntialias = false };
    private readonly SKPaint _bitmapPaint = new() { FilterQuality = SKFilterQuality.None, IsAntialias = false };
    private readonly SKPaint _tiledNeighborPaint = new()
    {
        FilterQuality = SKFilterQuality.None,
        IsAntialias = false,
        Color = new SKColor(255, 255, 255, 102) // ~40% opacity
    };
    private readonly SKPaint _tiledBorderPaint = new()
    {
        Color = new SKColor(0, 210, 255, 75), // subtle cyan border between tiles
        StrokeWidth = 1f,
        IsAntialias = false,
        Style = SKPaintStyle.Stroke
    };
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
    private readonly SKPaint _tileGridPaint = new()
    {
        Color = new SKColor(0, 210, 255, 65), // subtle cyan alpha ~65 (40-70 range)
        StrokeWidth = 1.2f,
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
    private readonly SKPaint _referenceOverlayPaint = new()
    {
        IsAntialias = true,
        FilterQuality = SKFilterQuality.Low
    };

    // Zero-Alloc Free Transform Skia resources
    private readonly SKPaint _transformOutlinePaint = new()
    {
        Color = new SKColor(0, 200, 255, 230),
        StrokeWidth = 1.5f,
        IsAntialias = false,
        PathEffect = SKPathEffect.CreateDash([4f, 4f], 0f),
        Style = SKPaintStyle.Stroke
    };
    private readonly SKPaint _transformHandleFillPaint = new()
    {
        Color = new SKColor(255, 255, 255, 255),
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };
    private readonly SKPaint _transformHandleStrokePaint = new()
    {
        Color = new SKColor(0, 0, 0, 220),
        StrokeWidth = 1.5f,
        IsAntialias = true,
        Style = SKPaintStyle.Stroke
    };
    private readonly SKPaint _transformStemPaint = new()
    {
        Color = new SKColor(0, 200, 255, 200),
        StrokeWidth = 1f,
        IsAntialias = false,
        Style = SKPaintStyle.Stroke
    };
    private readonly SKPaint _transformPivotPaint = new()
    {
        Color = new SKColor(255, 100, 0, 230),
        StrokeWidth = 1.5f,
        IsAntialias = true,
        Style = SKPaintStyle.Stroke
    };
    private SKBitmap? _transformPreviewBitmap;

    // Zero-Alloc Onion Skinning Skia resources
    private readonly SKPaint _prevOnionPaint = new()
    {
        FilterQuality = SKFilterQuality.None,
        IsAntialias = false
    };
    private readonly SKPaint _nextOnionPaint = new()
    {
        FilterQuality = SKFilterQuality.None,
        IsAntialias = false
    };
    private SKColorFilter? _prevColorFilter;
    private SKColorFilter? _nextColorFilter;
    private PixelColor _cachedPrevTint;
    private PixelColor _cachedNextTint;

    private const int MaxOnionBitmaps = 10;
    private readonly SKBitmap?[] _onionBitmaps = new SKBitmap?[MaxOnionBitmaps];
    private PixelColor[]? _onionPixelBuffer;
    private int _onionBufferWidth;
    private int _onionBufferHeight;

    // Offscreen layer composite buffer
    private SKBitmap? _compositeBitmap;
    private PixelColor[]? _compositePixels;
    private bool _isDirtyComposite = true;
    private PixelCanvas? _subscribedCanvas;
    private CanvasViewModel? _subscribedCanvasViewModel;

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
        int checkerSize = CanvasViewModel?.CheckerSize ?? 16;
        if (checkerSize < 2) checkerSize = 16;
        int halfSize = Math.Max(1, checkerSize / 2);

        SKColor lightColor = SKColor.TryParse(CanvasViewModel?.ColorCheckerLight, out var cl) ? cl : new SKColor(42, 46, 64);
        SKColor darkColor = SKColor.TryParse(CanvasViewModel?.ColorCheckerDark, out var cd) ? cd : new SKColor(31, 35, 50);

        using var tileBitmap = new SKBitmap(checkerSize, checkerSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (int y = 0; y < checkerSize; y++)
        {
            for (int x = 0; x < checkerSize; x++)
            {
                bool isLight = ((x / halfSize) + (y / halfSize)) % 2 == 0;
                tileBitmap.SetPixel(x, y, isLight ? lightColor : darkColor);
            }
        }

        var shader = SKShader.CreateBitmap(tileBitmap, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
        _checkerboardPaint.Shader?.Dispose();
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
            IsTiledMode = mainVm.IsTiledMode;
            mainVm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.IsTiledMode))
                {
                    IsTiledMode = mainVm.IsTiledMode;
                }
            };
        }
    }

    private void OnCanvasViewModelChanged()
    {
        if (_subscribedCanvasViewModel != null)
        {
            _subscribedCanvasViewModel.PropertyChanged -= OnCanvasViewModelPropertyChanged;
            _subscribedCanvasViewModel = null;
        }

        if (_subscribedCanvas != null)
        {
            _subscribedCanvas.PixelsChanged -= MarkCompositeDirty;
            _subscribedCanvas.FrameChanged -= OnFrameChanged;
            _subscribedCanvas.OnionSkin.PropertyChanged -= OnOnionSkinPropertyChanged;
            _subscribedCanvas = null;
        }

        _isDirtyComposite = true;
        _hasInitializedViewport = false;

        if (CanvasViewModel != null)
        {
            _subscribedCanvasViewModel = CanvasViewModel;
            _subscribedCanvasViewModel.PropertyChanged += OnCanvasViewModelPropertyChanged;

            if (CanvasViewModel.Canvas != null)
            {
                _subscribedCanvas = CanvasViewModel.Canvas;
                _subscribedCanvas.PixelsChanged += MarkCompositeDirty;
                _subscribedCanvas.FrameChanged += OnFrameChanged;
                _subscribedCanvas.OnionSkin.PropertyChanged += OnOnionSkinPropertyChanged;
                CanvasViewModel.Canvas.PropertyChanged += (_, _) => MarkCompositeDirty();
                CanvasViewModel.Canvas.Layers.CollectionChanged += (_, _) => MarkCompositeDirty();
                foreach (var layer in CanvasViewModel.Canvas.Layers)
                {
                    layer.PropertyChanged += (_, _) => MarkCompositeDirty();
                }
            }

            if (CanvasViewModel.ReferenceOverlay != null)
            {
                CanvasViewModel.ReferenceOverlay.PropertyChanged += (_, _) => InvalidateVisual();
            }
        }

        InvalidateVisual();
    }

    private void OnCanvasViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CanvasViewModel.CheckerSize) or nameof(CanvasViewModel.ColorCheckerLight) or nameof(CanvasViewModel.ColorCheckerDark))
        {
            InitializeCheckerboardShader();
        }
        if (e.PropertyName == nameof(CanvasViewModel.IsPlaying))
        {
            if (CanvasViewModel != null)
            {
                IsPlaying = CanvasViewModel.IsPlaying;
            }
        }
        InvalidateVisual();
    }

    private void OnFrameChanged(int newFrameIndex)
    {
        MarkCompositeDirty();
    }

    private void OnOnionSkinPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        MarkCompositeDirty();
    }

    public void MarkCompositeDirty()
    {
        _isDirtyComposite = true;
        if (Dispatcher.UIThread.CheckAccess())
        {
            InvalidateVisual();
        }
        else
        {
            Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
        }
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
        if (_subscribedCanvasViewModel != null)
        {
            _subscribedCanvasViewModel.PropertyChanged -= OnCanvasViewModelPropertyChanged;
            _subscribedCanvasViewModel = null;
        }
        if (_subscribedCanvas != null)
        {
            _subscribedCanvas.PixelsChanged -= MarkCompositeDirty;
            _subscribedCanvas.FrameChanged -= OnFrameChanged;
            _subscribedCanvas.OnionSkin.PropertyChanged -= OnOnionSkinPropertyChanged;
            _subscribedCanvas = null;
        }
        _compositeBitmap?.Dispose();
        _compositeBitmap = null;
        _checkerboardPaint.Dispose();
        _bitmapPaint.Dispose();
        _tiledNeighborPaint.Dispose();
        _tiledBorderPaint.Dispose();
        _gridPaint.Dispose();
        _gridDarkPaint.Dispose();
        _tileGridPaint.Dispose();
        _symmetryPaint.Dispose();
        _previewStrokePaint.Dispose();
        _previewFillPaint.Dispose();
        _selectionPaint.Dispose();
        _cursorHoverPaint.Dispose();
        _referenceOverlayPaint.Dispose();
        _transformOutlinePaint.Dispose();
        _transformHandleFillPaint.Dispose();
        _transformHandleStrokePaint.Dispose();
        _transformStemPaint.Dispose();
        _transformPivotPaint.Dispose();
        _transformPreviewBitmap?.Dispose();
        DisposeOnionSkinResources();
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
            EnsureCompositeUpdated(vm.Canvas);

            // 1 & 2. Background and Composite Layers (Seamless 3x3 Tiled Mode or Single View)
            if (IsTiledMode)
            {
                // 3x3 repetition of composite
                // Central tile (tx=0, ty=0) at 100%, 8 neighboring tiles at ~40% opacity with subtle separator border
                for (int ty = -1; ty <= 1; ty++)
                {
                    for (int tx = -1; tx <= 1; tx++)
                    {
                        float offsetX = _panX + (tx * destW);
                        float offsetY = _panY + (ty * destH);
                        var tileRect = new SKRect(offsetX, offsetY, offsetX + destW, offsetY + destH);

                        // Checkerboard
                        canvas.DrawRect(tileRect, _checkerboardPaint);

                        // Onion skinning on central tile
                        if (tx == 0 && ty == 0 && vm.Canvas.OnionSkin.IsEnabled && vm.Canvas.Frames.Count > 1)
                        {
                            RenderOnionSkinPass(canvas, tileRect, vm.Canvas);
                        }

                        if (_compositeBitmap != null)
                        {
                            if (tx == 0 && ty == 0)
                            {
                                canvas.DrawBitmap(_compositeBitmap, tileRect, _bitmapPaint);
                            }
                            else
                            {
                                canvas.DrawBitmap(_compositeBitmap, tileRect, _tiledNeighborPaint);
                            }
                        }

                        // Boundary separator grid between tiles
                        canvas.DrawRect(tileRect, _tiledBorderPaint);
                    }
                }
            }
            else
            {
                // 1. Checkerboard transparency background
                canvas.DrawRect(destRect, _checkerboardPaint);

                // Onion skinning pass (Pass 1: Prev frames, Pass 2: Next frames)
                if (vm.Canvas.OnionSkin.IsEnabled && vm.Canvas.Frames.Count > 1)
                {
                    RenderOnionSkinPass(canvas, destRect, vm.Canvas);
                }

                // 2. Offscreen composite of layers with SKFilterQuality.None (Nearest Neighbor)
                if (_compositeBitmap != null)
                {
                    canvas.DrawBitmap(_compositeBitmap, destRect, _bitmapPaint);
                }
            }

            // 2.5. Reference Image Overlay
            RenderReferenceOverlay(canvas, destRect, vm.ReferenceOverlay);

            // 3. Tool live preview overlay (e.g. current shape, line, selection)
            RenderToolPreview(canvas, destRect, zoom);

            // 3.5. Free Transform Overlay
            if (ToolViewModel?.ActiveTool == CanvasToolType.Transform && ToolViewModel.TransformTool.ActiveSession != null)
            {
                RenderTransformOverlay(canvas, destRect, zoom, ToolViewModel.TransformTool.ActiveSession);
            }

            // 4. Adaptive pixel grid when zoom >= 4x
            if (vm.ShowGrid && zoom >= vm.PixelGridMinZoom)
            {
                RenderPixelGrid(canvas, destRect, canvasW, canvasH, zoom);
            }

            // 4.5. Tile grid overlay
            if (vm.ShowTileGrid && vm.TileGridSize > 1)
            {
                RenderTileGrid(canvas, destRect, canvasW, canvasH, zoom, vm.TileGridSize);
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

    private void RenderOnionSkinPass(SKCanvas canvas, SKRect destRect, PixelCanvas canvasModel)
    {
        var onion = canvasModel.OnionSkin;
        if (!onion.IsEnabled || canvasModel.Frames.Count <= 1)
        {
            return;
        }

        UpdateOnionSkinFilters(onion);

        int w = canvasModel.Width;
        int h = canvasModel.Height;
        EnsureOnionPixelBuffer(w, h);
        int total = w * h;

        int currentFrame = canvasModel.CurrentFrameIndex;
        int totalFrames = canvasModel.Frames.Count;
        Guid? singleLayerId = onion.ActiveLayerOnly ? canvasModel.ActiveLayer?.Id : null;
        int slotIndex = 0;

        // Pass 1: Previous Frames
        int prevCount = onion.PrevFramesCount;
        for (int k = 1; k <= prevCount; k++)
        {
            int frameIdx = currentFrame - k;
            if (frameIdx >= 0)
            {
                var bitmap = GetOrCreateOnionBitmap(slotIndex++, w, h);
                canvasModel.GetFrameCompositePixels(frameIdx, _onionPixelBuffer, singleLayerId);

                unsafe
                {
                    fixed (PixelColor* src = _onionPixelBuffer)
                    {
                        Buffer.MemoryCopy(src, (void*)bitmap.GetPixels(), (long)total * 4, (long)total * 4);
                    }
                }
                bitmap.NotifyPixelsChanged();

                float fade = 1.0f - ((float)(k - 1) / prevCount);
                float alpha = Math.Clamp(onion.MaxOpacity * fade, 0.0f, 1.0f);
                byte byteAlpha = (byte)(alpha * 255f);

                _prevOnionPaint.Color = new SKColor(255, 255, 255, byteAlpha);
                canvas.DrawBitmap(bitmap, destRect, _prevOnionPaint);
            }
        }

        // Pass 2: Next Frames
        int nextCount = onion.NextFramesCount;
        for (int k = 1; k <= nextCount; k++)
        {
            int frameIdx = currentFrame + k;
            if (frameIdx < totalFrames)
            {
                var bitmap = GetOrCreateOnionBitmap(slotIndex++, w, h);
                canvasModel.GetFrameCompositePixels(frameIdx, _onionPixelBuffer, singleLayerId);

                unsafe
                {
                    fixed (PixelColor* src = _onionPixelBuffer)
                    {
                        Buffer.MemoryCopy(src, (void*)bitmap.GetPixels(), (long)total * 4, (long)total * 4);
                    }
                }
                bitmap.NotifyPixelsChanged();

                float fade = 1.0f - ((float)(k - 1) / nextCount);
                float alpha = Math.Clamp(onion.MaxOpacity * fade, 0.0f, 1.0f);
                byte byteAlpha = (byte)(alpha * 255f);

                _nextOnionPaint.Color = new SKColor(255, 255, 255, byteAlpha);
                canvas.DrawBitmap(bitmap, destRect, _nextOnionPaint);
            }
        }
    }

    private void UpdateOnionSkinFilters(OnionSkinSettings settings)
    {
        if (_prevColorFilter == null || settings.PrevFrameTint != _cachedPrevTint)
        {
            _prevColorFilter?.Dispose();
            _cachedPrevTint = settings.PrevFrameTint;
            var skColor = new SKColor(_cachedPrevTint.R, _cachedPrevTint.G, _cachedPrevTint.B, _cachedPrevTint.A);
            _prevColorFilter = SKColorFilter.CreateBlendMode(skColor, SKBlendMode.SrcATop);
            _prevOnionPaint.ColorFilter = _prevColorFilter;
        }

        if (_nextColorFilter == null || settings.NextFrameTint != _cachedNextTint)
        {
            _nextColorFilter?.Dispose();
            _cachedNextTint = settings.NextFrameTint;
            var skColor = new SKColor(_cachedNextTint.R, _cachedNextTint.G, _cachedNextTint.B, _cachedNextTint.A);
            _nextColorFilter = SKColorFilter.CreateBlendMode(skColor, SKBlendMode.SrcATop);
            _nextOnionPaint.ColorFilter = _nextColorFilter;
        }
    }

    private SKBitmap GetOrCreateOnionBitmap(int slotIndex, int width, int height)
    {
        if (slotIndex < 0 || slotIndex >= MaxOnionBitmaps)
        {
            slotIndex = 0;
        }

        var existing = _onionBitmaps[slotIndex];
        if (existing == null || existing.Width != width || existing.Height != height)
        {
            existing?.Dispose();
            existing = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            _onionBitmaps[slotIndex] = existing;
        }

        return existing;
    }

    private void EnsureOnionPixelBuffer(int width, int height)
    {
        int total = width * height;
        if (_onionPixelBuffer == null || _onionPixelBuffer.Length != total || _onionBufferWidth != width || _onionBufferHeight != height)
        {
            _onionPixelBuffer = new PixelColor[total];
            _onionBufferWidth = width;
            _onionBufferHeight = height;
        }
    }

    private void DisposeOnionSkinResources()
    {
        _prevColorFilter?.Dispose();
        _prevColorFilter = null;
        _nextColorFilter?.Dispose();
        _nextColorFilter = null;
        _prevOnionPaint.Dispose();
        _nextOnionPaint.Dispose();

        for (int i = 0; i < _onionBitmaps.Length; i++)
        {
            _onionBitmaps[i]?.Dispose();
            _onionBitmaps[i] = null;
        }
    }

    private void RenderTransformOverlay(SKCanvas canvas, SKRect destRect, float zoom, TransformSession session)
    {
        int origW = session.OriginalBounds.Width;
        int origH = session.OriginalBounds.Height;
        if (origW <= 0 || origH <= 0 || session.OriginalPixels.Length < origW * origH) return;

        // 1. Draw transformed preview bitmap
        if (_transformPreviewBitmap == null || _transformPreviewBitmap.Width != origW || _transformPreviewBitmap.Height != origH)
        {
            _transformPreviewBitmap?.Dispose();
            _transformPreviewBitmap = new SKBitmap(origW, origH, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        }

        unsafe
        {
            fixed (PixelColor* ptr = session.OriginalPixels)
            {
                _transformPreviewBitmap.SetPixels((IntPtr)ptr);
            }
        }

        canvas.Save();
        canvas.Translate(destRect.Left, destRect.Top);
        canvas.Scale(zoom);

        var matrix = session.ComputeMatrix();
        canvas.Concat(ref matrix);

        var srcRect = new SKRect(session.OriginalBounds.Left, session.OriginalBounds.Top, session.OriginalBounds.Right, session.OriginalBounds.Bottom);
        canvas.DrawBitmap(_transformPreviewBitmap, srcRect, _bitmapPaint);
        canvas.Restore();

        // 2. Draw transformed bounding box outline & handles in screen coordinates
        float left = session.OriginalBounds.Left;
        float top = session.OriginalBounds.Top;
        float right = session.OriginalBounds.Right;
        float bottom = session.OriginalBounds.Bottom;
        float midX = left + origW / 2.0f;
        float midY = top + origH / 2.0f;

        SKPoint ToScreen(SKPoint pt)
        {
            var mapped = matrix.MapPoint(pt);
            return new SKPoint(destRect.Left + mapped.X * zoom, destRect.Top + mapped.Y * zoom);
        }

        var scrTL = ToScreen(new SKPoint(left, top));
        var scrTR = ToScreen(new SKPoint(right, top));
        var scrBR = ToScreen(new SKPoint(right, bottom));
        var scrBL = ToScreen(new SKPoint(left, bottom));

        var scrTM = ToScreen(new SKPoint(midX, top));
        var scrRM = ToScreen(new SKPoint(right, midY));
        var scrBM = ToScreen(new SKPoint(midX, bottom));
        var scrLM = ToScreen(new SKPoint(left, midY));

        var scrRot = ToScreen(new SKPoint(midX, top - 20.0f));
        var scrPivot = ToScreen(session.Pivot);

        using var path = new SKPath();
        path.MoveTo(scrTL);
        path.LineTo(scrTR);
        path.LineTo(scrBR);
        path.LineTo(scrBL);
        path.Close();
        canvas.DrawPath(path, _transformOutlinePaint);

        // Rotation stem line & circle handle
        canvas.DrawLine(scrTM, scrRot, _transformStemPaint);
        canvas.DrawCircle(scrRot, 6.0f, _transformHandleFillPaint);
        canvas.DrawCircle(scrRot, 6.0f, _transformHandleStrokePaint);

        // 8 handles (square 8x8)
        DrawTransformHandle(canvas, scrTL);
        DrawTransformHandle(canvas, scrTR);
        DrawTransformHandle(canvas, scrBR);
        DrawTransformHandle(canvas, scrBL);
        DrawTransformHandle(canvas, scrTM);
        DrawTransformHandle(canvas, scrRM);
        DrawTransformHandle(canvas, scrBM);
        DrawTransformHandle(canvas, scrLM);

        // Pivot crosshair
        canvas.DrawLine(scrPivot.X - 5, scrPivot.Y, scrPivot.X + 5, scrPivot.Y, _transformPivotPaint);
        canvas.DrawLine(scrPivot.X, scrPivot.Y - 5, scrPivot.X, scrPivot.Y + 5, _transformPivotPaint);
    }

    private void DrawTransformHandle(SKCanvas canvas, SKPoint center, float size = 8.0f)
    {
        float half = size / 2.0f;
        var rect = new SKRect(center.X - half, center.Y - half, center.X + half, center.Y + half);
        canvas.DrawRect(rect, _transformHandleFillPaint);
        canvas.DrawRect(rect, _transformHandleStrokePaint);
    }

    private void UpdateTransformCursor(TransformHandle handle)
    {
        Cursor = handle switch
        {
            TransformHandle.Rotate => new Cursor(StandardCursorType.Cross),
            TransformHandle.TopLeft or TransformHandle.BottomRight => new Cursor(StandardCursorType.TopLeftCorner),
            TransformHandle.TopRight or TransformHandle.BottomLeft => new Cursor(StandardCursorType.TopRightCorner),
            TransformHandle.Top or TransformHandle.Bottom => new Cursor(StandardCursorType.SizeNorthSouth),
            TransformHandle.Left or TransformHandle.Right => new Cursor(StandardCursorType.SizeWestEast),
            TransformHandle.Move => new Cursor(StandardCursorType.SizeAll),
            _ => Cursor.Default
        };
    }

    private void RenderReferenceOverlay(SKCanvas canvas, SKRect destRect, ReferenceImageOverlay? overlay)
    {
        if (overlay == null || !overlay.IsVisible || !overlay.HasImage || overlay.Bitmap == null) return;

        byte alpha = (byte)Math.Clamp((int)(overlay.Opacity * 255f), 0, 255);
        if (alpha == 0) return;

        _referenceOverlayPaint.Color = new SKColor(255, 255, 255, alpha);
        canvas.DrawBitmap(overlay.Bitmap, destRect, _referenceOverlayPaint);
    }

    private void RenderPixelGrid(SKCanvas canvas, SKRect destRect, int canvasW, int canvasH, float zoom)
    {
        float left = destRect.Left;
        float top = destRect.Top;
        float right = destRect.Right;
        float bottom = destRect.Bottom;

        // Viewport culling: only draw grid lines within the visible clip bounds to guarantee 60+ FPS on large canvases (e.g. 500x1000, 4096)
        var clip = canvas.LocalClipBounds;
        float visibleLeft = Math.Max(left, clip.Left);
        float visibleRight = Math.Min(right, clip.Right);
        float visibleTop = Math.Max(top, clip.Top);
        float visibleBottom = Math.Min(bottom, clip.Bottom);

        if (visibleLeft >= visibleRight || visibleTop >= visibleBottom)
        {
            return;
        }

        int startX = Math.Max(0, (int)Math.Floor((visibleLeft - left) / zoom));
        int endX = Math.Min(canvasW, (int)Math.Ceiling((visibleRight - left) / zoom));

        // Vertical lines
        for (int x = startX; x <= endX; x++)
        {
            float posX = left + (x * zoom);
            canvas.DrawLine(posX, visibleTop, posX, visibleBottom, _gridPaint);
        }

        int startY = Math.Max(0, (int)Math.Floor((visibleTop - top) / zoom));
        int endY = Math.Min(canvasH, (int)Math.Ceiling((visibleBottom - top) / zoom));

        // Horizontal lines
        for (int y = startY; y <= endY; y++)
        {
            float posY = top + (y * zoom);
            canvas.DrawLine(visibleLeft, posY, visibleRight, posY, _gridPaint);
        }
    }

    /// <summary>
    /// Renders the tile grid overlay with viewport clip culling to guarantee 60+ FPS performance on canvases of any size.
    /// </summary>
    public void RenderTileGrid(SKCanvas canvas, SKRect destRect, int canvasW, int canvasH, float zoom, int tileSize)
    {
        if (tileSize <= 0) return;

        float tileSpan = tileSize * zoom;
        if (tileSpan < 1f) return;

        float left = destRect.Left;
        float top = destRect.Top;
        float right = destRect.Right;
        float bottom = destRect.Bottom;

        // Viewport clipping via canvas.LocalClipBounds to guarantee 60+ FPS on any canvas size
        var clip = canvas.LocalClipBounds;
        float visibleLeft = Math.Max(left, clip.Left);
        float visibleRight = Math.Min(right, clip.Right);
        float visibleTop = Math.Max(top, clip.Top);
        float visibleBottom = Math.Min(bottom, clip.Bottom);

        if (visibleLeft >= visibleRight || visibleTop >= visibleBottom)
        {
            return;
        }

        if (CanvasViewModel?.TileGridSettings != null)
        {
            _tileGridPaint.Color = CanvasViewModel.TileGridSettings.GridColor;
        }

        int startTileX = Math.Max(0, (int)Math.Floor((visibleLeft - left) / tileSpan));
        int endTileX = (int)Math.Ceiling((visibleRight - left) / tileSpan);

        // Vertical tile boundary lines
        for (int t = startTileX; t <= endTileX; t++)
        {
            int px = t * tileSize;
            if (px > canvasW) break;

            float posX = left + (px * zoom);
            canvas.DrawLine(posX, visibleTop, posX, visibleBottom, _tileGridPaint);
        }

        int startTileY = Math.Max(0, (int)Math.Floor((visibleTop - top) / tileSpan));
        int endTileY = (int)Math.Ceiling((visibleBottom - top) / tileSpan);

        // Horizontal tile boundary lines
        for (int t = startTileY; t <= endTileY; t++)
        {
            int py = t * tileSize;
            if (py > canvasH) break;

            float posY = top + (py * zoom);
            canvas.DrawLine(visibleLeft, posY, visibleRight, posY, _tileGridPaint);
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
        Focus();

        // Concurrency / Playback guard: if playback is running on the main canvas,
        // any pointer press immediately stops playback and selects the clicked frame.
        if (IsPlaying || (CanvasViewModel != null && CanvasViewModel.IsPlaying))
        {
            IsPlaying = false;
            if (CanvasViewModel != null)
            {
                CanvasViewModel.StopPlayback();
                if (CanvasViewModel.Canvas != null)
                {
                    CanvasViewModel.Canvas.SwitchToFrame(CanvasViewModel.Canvas.CurrentFrameIndex);
                }
            }
            e.Handled = true;
            return;
        }

        var point = e.GetCurrentPoint(this);
        _lastPanPointerPos = point.Position;

        // Middle button or Space+LeftDrag = Pan
        if (point.Properties.IsMiddleButtonPressed || (_isSpacePressed && point.Properties.IsLeftButtonPressed))
        {
            _isPanning = true;
            Cursor = PanHandCursor;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (ToolViewModel?.ActiveTool == CanvasToolType.Transform && CanvasViewModel != null && ToolViewModel.ToolContext != null)
        {
            if (point.Properties.IsLeftButtonPressed)
            {
                float canvasX = (float)((point.Position.X - _panX) / CanvasViewModel.Zoom);
                float canvasY = (float)((point.Position.Y - _panY) / CanvasViewModel.Zoom);

                e.Pointer.Capture(this);
                _isDrawing = true;
                ToolViewModel.TransformTool.OnPointerDown(ToolViewModel.ToolContext, (int)MathF.Round(canvasX), (int)MathF.Round(canvasY));
                InvalidateVisual();
                e.Handled = true;
                return;
            }
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
            _strokeSteps.Clear();

            _strokeStabilizer.Strength = ToolViewModel.StabilizerStrength;
            _strokeStabilizer.Reset();
            if (_strokeStabilizer.Strength > 0)
            {
                (px, py) = _strokeStabilizer.Process(px, py);
                if (IsTiledMode)
                {
                    int cw = CanvasViewModel.CanvasWidth;
                    int ch = CanvasViewModel.CanvasHeight;
                    px = (px % cw + cw) % cw;
                    py = (py % ch + ch) % ch;
                }
            }

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

        if (ToolViewModel?.ActiveTool == CanvasToolType.Transform && CanvasViewModel != null && ToolViewModel.ToolContext != null)
        {
            float canvasX = (float)((pos.X - _panX) / CanvasViewModel.Zoom);
            float canvasY = (float)((pos.Y - _panY) / CanvasViewModel.Zoom);

            ToolViewModel.TransformTool.OnPointerMove(ToolViewModel.ToolContext, (int)MathF.Round(canvasX), (int)MathF.Round(canvasY));
            UpdateTransformCursor(ToolViewModel.TransformTool.HoveredHandle);
            InvalidateVisual();
            if (_isDrawing)
            {
                e.Handled = true;
                return;
            }
        }

        var (px, py, isInside) = ScreenToCanvasPixel(pos);
        _currentHoverPixelX = px;
        _currentHoverPixelY = py;
        _isHovering = isInside;

        PixelColor hoveredColor = (isInside && CanvasViewModel?.Canvas.ActiveLayer != null)
            ? CanvasViewModel.Canvas.ActiveLayer.GetPixel(px, py)
            : PixelColor.Transparent;

        CanvasViewModel?.UpdateCursor(px, py, isInside, hoveredColor);

        if (_isDrawing && isInside && ToolViewModel != null && PaletteViewModel != null)
        {
            if (_strokeStabilizer.Strength > 0)
            {
                (px, py) = _strokeStabilizer.Process(px, py);
                if (IsTiledMode && CanvasViewModel != null)
                {
                    int cw = CanvasViewModel.CanvasWidth;
                    int ch = CanvasViewModel.CanvasHeight;
                    px = (px % cw + cw) % cw;
                    py = (py % ch + ch) % ch;
                }
            }

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
            Cursor = _isSpacePressed ? PanHandCursor : Cursor.Default;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (ToolViewModel?.ActiveTool == CanvasToolType.Transform && CanvasViewModel != null && ToolViewModel.ToolContext != null)
        {
            if (_isDrawing)
            {
                _isDrawing = false;
                e.Pointer.Capture(null);

                var pos = e.GetPosition(this);
                float canvasX = (float)((pos.X - _panX) / CanvasViewModel.Zoom);
                float canvasY = (float)((pos.Y - _panY) / CanvasViewModel.Zoom);

                ToolViewModel.TransformTool.OnPointerUp(ToolViewModel.ToolContext, (int)MathF.Round(canvasX), (int)MathF.Round(canvasY));
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }

        if (_isDrawing)
        {
            _isDrawing = false;
            e.Pointer.Capture(null);

            var (px, py, _) = ScreenToCanvasPixel(e.GetPosition(this));
            if (_strokeStabilizer.Strength > 0)
            {
                (px, py) = _strokeStabilizer.Process(px, py);
                if (IsTiledMode && CanvasViewModel != null)
                {
                    int cw = CanvasViewModel.CanvasWidth;
                    int ch = CanvasViewModel.CanvasHeight;
                    px = (px % cw + cw) % cw;
                    py = (py % ch + ch) % ch;
                }
            }

            ExecuteToolPointerUp(px, py);
            _strokeSteps.Clear();
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.LeftShift or Key.RightShift)
        {
            if (ToolViewModel?.TransformTool != null) ToolViewModel.TransformTool.IsShiftDown = true;
        }
        else if (e.Key is Key.LeftCtrl or Key.RightCtrl)
        {
            if (ToolViewModel?.TransformTool != null) ToolViewModel.TransformTool.IsCtrlDown = true;
        }

        if (e.Key == Key.Space || e.PhysicalKey == PhysicalKey.Space)
        {
            _isSpacePressed = true;
            if (!_isDrawing)
            {
                Cursor = PanHandCursor;
            }
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key is Key.LeftShift or Key.RightShift)
        {
            if (ToolViewModel?.TransformTool != null) ToolViewModel.TransformTool.IsShiftDown = false;
        }
        else if (e.Key is Key.LeftCtrl or Key.RightCtrl)
        {
            if (ToolViewModel?.TransformTool != null) ToolViewModel.TransformTool.IsCtrlDown = false;
        }

        if (e.Key == Key.Space || e.PhysicalKey == PhysicalKey.Space)
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
            case CanvasToolType.PatternBrush:
                DrawFreehandStep(layer, px, py, drawColor);
                MarkCompositeDirty();
                break;

            case CanvasToolType.Eraser:
                DrawFreehandStep(layer, px, py, PixelColor.Transparent);
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

            case CanvasToolType.MagicWand:
                ExecuteMagicWand(layer, px, py);
                MarkCompositeDirty();
                break;

            case CanvasToolType.Eyedropper:
                var picked = layer.GetPixel(px, py);
                if (PaletteViewModel != null)
                {
                    PaletteViewModel.PrimaryColor = picked;
                }
                if (DataContext is MainViewModel mainVm)
                {
                    mainVm.StatusMessage = mainVm.Loc.Format("Msg_EyedropperPicked", picked.ToHex(picked.A < 255));
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
            case CanvasToolType.PatternBrush:
                DrawFreehandLine(layer, _lastDrawnPixelX, _lastDrawnPixelY, px, py, drawColor);
                _lastDrawnPixelX = px;
                _lastDrawnPixelY = py;
                MarkCompositeDirty();
                break;

            case CanvasToolType.Eraser:
                DrawFreehandLine(layer, _lastDrawnPixelX, _lastDrawnPixelY, px, py, PixelColor.Transparent);
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
            case CanvasToolType.PatternBrush:
                if (_activeStrokeDeltas.Count > 0 && HistoryViewModel != null)
                {
                    string desc = ToolViewModel.ActiveTool switch
                    {
                        CanvasToolType.Pencil => "Pencil Stroke",
                        CanvasToolType.Eraser => "Eraser Stroke",
                        CanvasToolType.DitherBrush => "Dither Stroke",
                        CanvasToolType.PatternBrush => "Pattern Brush Stroke",
                        _ => "Stroke"
                    };
                    var cmd = new DrawPixelsCommand(layer, _activeStrokeDeltas.ToArray(), desc);
                    HistoryViewModel.Service.ExecuteCommand(cmd);
                    _activeStrokeDeltas.Clear();
                    _touchedPixelKeys.Clear();
                    _strokeSteps.Clear();
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

    private void DrawFreehandLine(Layer layer, int x0, int y0, int x1, int y1, PixelColor color)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            DrawFreehandStep(layer, x0, y0, color);
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

    private void DrawFreehandStep(Layer layer, int x, int y, PixelColor color)
    {
        bool pixelPerfect = (ToolViewModel?.IsPixelPerfect ?? false) && (ToolViewModel?.BrushSize == 1);
        if (pixelPerfect)
        {
            if (_strokeSteps.Count >= 2)
            {
                var p0 = _strokeSteps[^2];
                var p1 = _strokeSteps[^1];

                if (PixelPerfectAlgorithm.IsCornerJaggie(p0.X, p0.Y, p1.X, p1.Y, x, y))
                {
                    RollbackStep(layer, p1);
                    _strokeSteps.RemoveAt(_strokeSteps.Count - 1);
                }
            }
        }

        int deltaStart = _activeStrokeDeltas.Count;
        PlotBrushWithSymmetry(layer, x, y, color);

        if (pixelPerfect)
        {
            _strokeSteps.Add(new StrokeStep(x, y, deltaStart));
        }
    }

    private void RollbackStep(Layer layer, StrokeStep step)
    {
        for (int i = _activeStrokeDeltas.Count - 1; i >= step.DeltaStartIndex; i--)
        {
            var delta = _activeStrokeDeltas[i];
            layer.SetPixel(delta.X, delta.Y, delta.OldColor);
            long key = ((long)delta.Y << 32) | (uint)delta.X;
            _touchedPixelKeys.Remove(key);
            _activeStrokeDeltas.RemoveAt(i);
        }
    }

    private void CommitActiveDeltas(Layer layer, string description)
    {
        if (_activeStrokeDeltas.Count > 0 && HistoryViewModel != null)
        {
            var cmd = new DrawPixelsCommand(layer, _activeStrokeDeltas.ToArray(), description);
            HistoryViewModel.Service.ExecuteCommand(cmd);
            _activeStrokeDeltas.Clear();
            _touchedPixelKeys.Clear();
            _strokeSteps.Clear();
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

                if (IsTiledMode)
                {
                    px = (px % w + w) % w;
                    py = (py % h + h) % h;
                }

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
        if (IsTiledMode)
        {
            x = (x % layer.Width + layer.Width) % layer.Width;
            y = (y % layer.Height + layer.Height) % layer.Height;
        }

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
        else if (ToolViewModel?.ActiveTool == CanvasToolType.PatternBrush)
        {
            var patternPreset = ToolViewModel.SelectedPatternPreset;
            if (!patternPreset.IsPixelSet(x, y))
            {
                return;
            }
            finalColor = primaryColor;
        }
        else if (pattern != DitherPattern.None)
        {
            finalColor = DitherHelper.EvaluateDitherColor(x, y, primaryColor, secondaryColor, pattern);
        }

        ApplyPixel(layer, x, y, finalColor);
    }

    private void ApplyPixel(Layer layer, int x, int y, PixelColor newColor)
    {
        int targetX = x;
        int targetY = y;
        if (IsTiledMode)
        {
            targetX = (x % layer.Width + layer.Width) % layer.Width;
            targetY = (y % layer.Height + layer.Height) % layer.Height;
        }
        else if ((uint)x >= (uint)layer.Width || (uint)y >= (uint)layer.Height)
        {
            return;
        }

        long key = ((long)targetY << 32) | (uint)targetX;
        var oldColor = layer.GetPixel(targetX, targetY);

        if (_touchedPixelKeys.Add(key))
        {
            _activeStrokeDeltas.Add(new PixelDelta(targetX, targetY, oldColor, newColor));
        }

        layer.SetPixel(targetX, targetY, newColor);
    }

    private void ExecuteMagicWand(Layer layer, int px, int py)
    {
        if (CanvasViewModel == null || ToolViewModel == null) return;

        int tolerance = (int)(ToolViewModel.FillTolerance * 2.55f);
        bool contiguous = ToolViewModel.FillContiguous;
        bool symX = CanvasViewModel.ShowSymmetryX;
        bool symY = CanvasViewModel.ShowSymmetryY;

        var seeds = new List<(int X, int Y)>(4) { (px, py) };
        if (symX)
        {
            seeds.Add(((layer.Width - 1) - px, py));
        }
        if (symY)
        {
            seeds.Add((px, (layer.Height - 1) - py));
        }
        if (symX && symY)
        {
            seeds.Add(((layer.Width - 1) - px, (layer.Height - 1) - py));
        }

        var wand = ToolViewModel.MagicWandTool;
        wand.Select(layer, seeds, tolerance, contiguous, CanvasViewModel.Selection);
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
        int rawX = (int)Math.Floor((screenPos.X - _panX) / zoom);
        int rawY = (int)Math.Floor((screenPos.Y - _panY) / zoom);

        int cw = CanvasViewModel.CanvasWidth;
        int ch = CanvasViewModel.CanvasHeight;

        if (IsTiledMode)
        {
            bool isInside = rawX >= -cw && rawX < 2 * cw && rawY >= -ch && rawY < 2 * ch;
            int wrappedX = (rawX % cw + cw) % cw;
            int wrappedY = (rawY % ch + ch) % ch;
            return (wrappedX, wrappedY, isInside);
        }
        else
        {
            bool isInside = rawX >= 0 && rawX < cw && rawY >= 0 && rawY < ch;
            return (rawX, rawY, isInside);
        }
    }

    private readonly struct StrokeStep
    {
        public int X { get; }
        public int Y { get; }
        public int DeltaStartIndex { get; }

        public StrokeStep(int x, int y, int deltaStartIndex)
        {
            X = x;
            Y = y;
            DeltaStartIndex = deltaStartIndex;
        }
    }

    #endregion
}

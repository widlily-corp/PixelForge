using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PixelForge.Core.Models;

namespace PixelForge.App.Views;

public partial class LivePreviewControl : UserControl
{
    private WriteableBitmap? _previewBitmap;
    private PixelColor[]? _compositeBuffer;
    private int _scale = 1;
    private PixelCanvas? _currentCanvas;
    private readonly DispatcherTimer _refreshTimer;
    private bool _needsRefresh;

    public static readonly StyledProperty<PixelCanvas?> CanvasSourceProperty =
        AvaloniaProperty.Register<LivePreviewControl, PixelCanvas?>(nameof(CanvasSource));

    public PixelCanvas? CanvasSource
    {
        get => GetValue(CanvasSourceProperty);
        set => SetValue(CanvasSourceProperty, value);
    }

    public LivePreviewControl()
    {
        InitializeComponent();

        Scale1xButton.Click += (_, _) => SetScale(1);
        Scale2xButton.Click += (_, _) => SetScale(2);
        Scale4xButton.Click += (_, _) => SetScale(4);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 fps live preview refresh
        };
        _refreshTimer.Tick += OnRefreshTimerTick;
        _refreshTimer.Start();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CanvasSourceProperty)
        {
            AttachCanvas(change.GetNewValue<PixelCanvas?>());
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (CanvasSource == null && DataContext != null)
        {
            // Try to resolve Canvas from DataContext if possible
            var prop = DataContext.GetType().GetProperty("Canvas");
            if (prop != null && prop.GetValue(DataContext) is PixelCanvas canvas)
            {
                AttachCanvas(canvas);
            }
        }
    }

    public void AttachCanvas(PixelCanvas? canvas)
    {
        if (_currentCanvas == canvas)
        {
            return;
        }

        if (_currentCanvas != null)
        {
            _currentCanvas.PropertyChanged -= OnCanvasPropertyChanged;
        }

        _currentCanvas = canvas;

        if (_currentCanvas != null)
        {
            _currentCanvas.PropertyChanged += OnCanvasPropertyChanged;
            ReinitializeBitmap(_currentCanvas.Width, _currentCanvas.Height);
            RequestRefresh();
        }
    }

    private void OnCanvasPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        RequestRefresh();
    }

    public void RequestRefresh()
    {
        _needsRefresh = true;
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        // Also check if DataContext provides a canvas if not yet attached
        if (_currentCanvas == null && DataContext != null)
        {
            var prop = DataContext.GetType().GetProperty("Canvas");
            if (prop != null && prop.GetValue(DataContext) is PixelCanvas canvas)
            {
                AttachCanvas(canvas);
            }
        }

        if (_needsRefresh && _currentCanvas != null)
        {
            _needsRefresh = false;
            RenderPreview();
        }
    }

    private void ReinitializeBitmap(int width, int height)
    {
        DimensionBadge.Text = $"{width} × {height}";

        if (_previewBitmap == null || _previewBitmap.PixelSize.Width != width || _previewBitmap.PixelSize.Height != height)
        {
            _previewBitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            PreviewImage.Source = _previewBitmap;
            _compositeBuffer = new PixelColor[width * height];
        }

        DrawCheckerboardPattern(width, height);
        UpdateDisplaySize();
    }

    private IBrush GetBrush(string resourceKey, IBrush fallback)
    {
        if (this.TryFindResource(resourceKey, out var res) && res is IBrush b)
        {
            return b;
        }

        if (Application.Current != null && Application.Current.TryFindResource(resourceKey, out var appRes) && appRes is IBrush ab)
        {
            return ab;
        }

        return fallback;
    }

    private void SetScale(int scale)
    {
        _scale = scale;

        var accent = GetBrush("AccentPrimaryBrush", Brushes.DodgerBlue);
        var panel = GetBrush("SurfacePanelBrush", new SolidColorBrush(Color.Parse("#202330")));
        var textSec = GetBrush("TextSecondaryBrush", Brushes.LightGray);

        Scale1xButton.Background = scale == 1 ? accent : panel;
        Scale1xButton.Foreground = scale == 1 ? Brushes.Black : textSec;

        Scale2xButton.Background = scale == 2 ? accent : panel;
        Scale2xButton.Foreground = scale == 2 ? Brushes.Black : textSec;

        Scale4xButton.Background = scale == 4 ? accent : panel;
        Scale4xButton.Foreground = scale == 4 ? Brushes.Black : textSec;

        UpdateDisplaySize();
    }

    private void UpdateDisplaySize()
    {
        if (_currentCanvas == null)
        {
            return;
        }

        double displayWidth = _currentCanvas.Width * _scale;
        double displayHeight = _currentCanvas.Height * _scale;

        CheckerContainer.Width = displayWidth;
        CheckerContainer.Height = displayHeight;

        PreviewImage.Width = displayWidth;
        PreviewImage.Height = displayHeight;
    }

    private void DrawCheckerboardPattern(int width, int height)
    {
        CheckerCanvas.Children.Clear();
        int cellSize = 6;
        int cols = (int)Math.Ceiling((double)width * _scale / cellSize);
        int rows = (int)Math.Ceiling((double)height * _scale / cellSize);

        IBrush lightBrush = this.TryFindResource("CheckerLightBrush", out var lb) && lb is IBrush b1
            ? b1 : new SolidColorBrush(Color.Parse("#2A2E40"));
        IBrush darkBrush = this.TryFindResource("CheckerDarkBrush", out var db) && db is IBrush b2
            ? b2 : new SolidColorBrush(Color.Parse("#1F2332"));

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var rect = new Rectangle
                {
                    Width = cellSize,
                    Height = cellSize,
                    Fill = (r + c) % 2 == 0 ? lightBrush : darkBrush
                };
                Canvas.SetLeft(rect, c * cellSize);
                Canvas.SetTop(rect, r * cellSize);
                CheckerCanvas.Children.Add(rect);
            }
        }
    }

    private unsafe void RenderPreview()
    {
        if (_currentCanvas == null || _previewBitmap == null || _compositeBuffer == null)
        {
            return;
        }

        int width = _currentCanvas.Width;
        int height = _currentCanvas.Height;

        if (_previewBitmap.PixelSize.Width != width || _previewBitmap.PixelSize.Height != height)
        {
            ReinitializeBitmap(width, height);
        }

        _compositeBuffer = _currentCanvas.GetCompositePixels(_compositeBuffer);

        using (var locked = _previewBitmap.Lock())
        {
            byte* dst = (byte*)locked.Address.ToPointer();
            int total = width * height;

            for (int i = 0; i < total; i++)
            {
                var c = _compositeBuffer[i];
                float aNorm = c.A / 255.0f;
                // Premultiplied BGRA
                dst[i * 4 + 0] = (byte)(c.B * aNorm); // B
                dst[i * 4 + 1] = (byte)(c.G * aNorm); // G
                dst[i * 4 + 2] = (byte)(c.R * aNorm); // R
                dst[i * 4 + 3] = c.A;                 // A
            }
        }

        PreviewImage.InvalidateVisual();
    }
}

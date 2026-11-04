using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PixelForge.App.ViewModels;
using PixelForge.Core.Models;

namespace PixelForge.App.Views;

/// <summary>
/// Floating tool window providing concurrent real-time animated preview of the timeline.
/// Supports zooming from 1x to 8x with nearest-neighbor interpolation, playback speed controls, and frame stepping.
/// </summary>
public partial class AnimationPreviewWindow : Window
{
    private WriteableBitmap? _bitmap;
    private PixelColor[]? _compositeBuffer;
    private AnimationPreviewViewModel? _viewModel;

    public AnimationPreviewViewModel? ViewModel => DataContext as AnimationPreviewViewModel;

    public AnimationPreviewWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public AnimationPreviewWindow(AnimationPreviewViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.RenderRequested -= OnRenderRequested;
        }

        _viewModel = DataContext as AnimationPreviewViewModel;

        if (_viewModel != null)
        {
            _viewModel.RenderRequested += OnRenderRequested;
            RenderPreview();
        }
    }

    private void OnRenderRequested()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            RenderPreview();
        }
        else
        {
            Dispatcher.UIThread.Post(RenderPreview);
        }
    }

    private unsafe void RenderPreview()
    {
        var vm = ViewModel;
        if (vm?.Canvas == null)
        {
            return;
        }

        var canvas = vm.Canvas;
        int width = canvas.Width;
        int height = canvas.Height;
        int frameIndex = vm.CurrentPreviewFrameIndex;

        if (frameIndex < 0 || frameIndex >= canvas.Frames.Count)
        {
            return;
        }

        int scale = vm.Scale;

        if (_bitmap == null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
        {
            _bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            PreviewImage.Source = _bitmap;
            _compositeBuffer = new PixelColor[width * height];
            DrawCheckerboardPattern(width, height, scale);
        }

        if (CheckerContainer.Width != width * scale || CheckerContainer.Height != height * scale)
        {
            DrawCheckerboardPattern(width, height, scale);
            UpdateDisplaySize(width, height, scale);
        }

        _compositeBuffer = canvas.GetFrameCompositePixels(frameIndex, _compositeBuffer);

        using (var locked = _bitmap.Lock())
        {
            byte* dst = (byte*)locked.Address.ToPointer();
            int total = width * height;

            for (int i = 0; i < total; i++)
            {
                var c = _compositeBuffer[i];
                float aNorm = c.A / 255.0f;
                // Premultiplied BGRA for Skia / Avalonia WriteableBitmap
                dst[i * 4 + 0] = (byte)(c.B * aNorm); // B
                dst[i * 4 + 1] = (byte)(c.G * aNorm); // G
                dst[i * 4 + 2] = (byte)(c.R * aNorm); // R
                dst[i * 4 + 3] = c.A;                 // A
            }
        }

        PreviewImage.InvalidateVisual();
    }

    private void DrawCheckerboardPattern(int width, int height, int scale)
    {
        CheckerCanvas.Children.Clear();
        int cellSize = 8;
        int displayWidth = width * scale;
        int displayHeight = height * scale;
        int cols = (int)Math.Ceiling((double)displayWidth / cellSize);
        int rows = (int)Math.Ceiling((double)displayHeight / cellSize);

        IBrush lightBrush = this.TryFindResource("CheckerLightBrush", out var lb) && lb is IBrush b1
            ? b1 : new SolidColorBrush(Color.Parse("#2A2E40"));
        IBrush darkBrush = this.TryFindResource("CheckerDarkBrush", out var db) && db is IBrush b2
            ? b2 : new SolidColorBrush(Color.Parse("#1F2332"));

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var rect = new Avalonia.Controls.Shapes.Rectangle
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

    private void UpdateDisplaySize(int width, int height, int scale)
    {
        double displayWidth = width * scale;
        double displayHeight = height * scale;

        CheckerContainer.Width = displayWidth;
        CheckerContainer.Height = displayHeight;

        PreviewImage.Width = displayWidth;
        PreviewImage.Height = displayHeight;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (ViewModel == null)
        {
            return;
        }

        if (e.Key == Key.Space)
        {
            ViewModel.TogglePlay();
            e.Handled = true;
        }
        else if (e.Key is Key.OemComma or Key.Left)
        {
            ViewModel.StepBackward();
            e.Handled = true;
        }
        else if (e.Key is Key.OemPeriod or Key.Right)
        {
            ViewModel.StepForward();
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (_viewModel != null)
        {
            _viewModel.RenderRequested -= OnRenderRequested;
            _viewModel.Dispose();
            _viewModel = null;
        }
    }
}

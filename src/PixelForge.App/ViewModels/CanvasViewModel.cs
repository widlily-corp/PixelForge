using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelForge.Core.Models;

namespace PixelForge.App.ViewModels;

/// <summary>
/// ViewModel managing canvas dimensions, viewport zoom, overlays (grid, symmetry), and cursor tracking.
/// </summary>
public partial class CanvasViewModel : ViewModelBase
{
    public const float MinZoom = 1.0f;
    public const float MaxZoom = 32.0f;

    [ObservableProperty]
    private PixelCanvas _canvas;

    [ObservableProperty]
    private float _zoom = 12.0f;

    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private bool _showSymmetryX = false;

    [ObservableProperty]
    private bool _showSymmetryY = false;

    [ObservableProperty]
    private int _symmetryAxisX;

    [ObservableProperty]
    private int _symmetryAxisY;

    [ObservableProperty]
    private int _cursorX = -1;

    [ObservableProperty]
    private int _cursorY = -1;

    [ObservableProperty]
    private bool _isCursorInside = false;

    [ObservableProperty]
    private SelectionModel _selection = new();

    public int CanvasWidth => Canvas.Width;

    public int CanvasHeight => Canvas.Height;

    public string CursorCoordinatesText => IsCursorInside && CursorX >= 0 && CursorY >= 0
        ? $"X: {CursorX}, Y: {CursorY}"
        : "—";

    public string ZoomText => $"{MathF.Round(Zoom * 100f)}%";

    public string DimensionsText => $"{CanvasWidth} × {CanvasHeight} px";

    public CanvasViewModel(PixelCanvas canvas)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _symmetryAxisX = canvas.Width / 2;
        _symmetryAxisY = canvas.Height / 2;
    }

    public void SetCanvas(PixelCanvas newCanvas)
    {
        ArgumentNullException.ThrowIfNull(newCanvas);
        Canvas = newCanvas;
        SymmetryAxisX = newCanvas.Width / 2;
        SymmetryAxisY = newCanvas.Height / 2;
        Selection.Clear();

        OnPropertyChanged(nameof(CanvasWidth));
        OnPropertyChanged(nameof(CanvasHeight));
        OnPropertyChanged(nameof(DimensionsText));
    }

    public void UpdateCursor(int x, int y, bool isInside)
    {
        CursorX = x;
        CursorY = y;
        IsCursorInside = isInside;
        OnPropertyChanged(nameof(CursorCoordinatesText));
    }

    partial void OnZoomChanged(float value)
    {
        OnPropertyChanged(nameof(ZoomText));
    }

    [RelayCommand]
    public void ZoomIn()
    {
        Zoom = Math.Clamp(Zoom * 1.25f, MinZoom, MaxZoom);
    }

    [RelayCommand]
    public void ZoomOut()
    {
        Zoom = Math.Clamp(Zoom / 1.25f, MinZoom, MaxZoom);
    }

    [RelayCommand]
    public void ResetZoom()
    {
        Zoom = 1.0f;
    }

    [RelayCommand]
    public void ToggleGrid()
    {
        ShowGrid = !ShowGrid;
    }

    [RelayCommand]
    public void ToggleSymmetryX()
    {
        ShowSymmetryX = !ShowSymmetryX;
    }

    [RelayCommand]
    public void ToggleSymmetryY()
    {
        ShowSymmetryY = !ShowSymmetryY;
    }
}

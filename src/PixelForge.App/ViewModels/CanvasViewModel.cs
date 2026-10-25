using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelForge.Core.Models;

namespace PixelForge.App.ViewModels;

/// <summary>
/// ViewModel managing canvas dimensions, viewport zoom, overlays (grid, symmetry), and cursor tracking.
/// </summary>
public partial class CanvasViewModel : ViewModelBase
{
    public const float MinZoom = 0.05f;
    public const float MaxZoom = 64.0f;

    [ObservableProperty]
    private PixelCanvas _canvas;

    [ObservableProperty]
    private float _zoom = 12.0f;

    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private float _pixelGridMinZoom = 4.0f;

    [ObservableProperty]
    private int _checkerSize = 16;

    [ObservableProperty]
    private string _colorCheckerLight = "#2A2E40";

    [ObservableProperty]
    private string _colorCheckerDark = "#1F2332";

    [ObservableProperty]
    private bool _showTileGrid = false;

    [ObservableProperty]
    private int _tileGridSize = 16;

    [ObservableProperty]
    private TileGridSettings _tileGridSettings = new();

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
    private PixelColor _hoveredColor = PixelColor.Transparent;

    [ObservableProperty]
    private bool _hasHoveredColor = false;

    public string HoveredColorHex => HasHoveredColor ? HoveredColor.ToHex(HoveredColor.A < 255) : string.Empty;

    [ObservableProperty]
    private SelectionModel _selection = new();

    [ObservableProperty]
    private ReferenceImageOverlay _referenceOverlay = new();

    public int CanvasWidth => Canvas.Width;

    public int CanvasHeight => Canvas.Height;

    private string _cursorCoordinatesText = "—";
    public string CursorCoordinatesText => _cursorCoordinatesText;

    public string ZoomText => $"{MathF.Round(Zoom * 100f)}%";

    public string DimensionsText => $"{CanvasWidth} × {CanvasHeight} px";

    public string TileGridText => $"{TileGridSize} × {TileGridSize} px";

    public bool HasActiveSelection => Selection.IsActive && Selection.Bounds.Width > 0 && Selection.Bounds.Height > 0;

    public string SelectionText => HasActiveSelection
        ? $"{Selection.Bounds.Width} × {Selection.Bounds.Height} px ({Selection.Bounds.Left}, {Selection.Bounds.Top})"
        : string.Empty;

    public string SelectionShortText => HasActiveSelection
        ? $"{Selection.Bounds.Width} × {Selection.Bounds.Height}"
        : string.Empty;

    public bool HasActiveSymmetry => ShowSymmetryX || ShowSymmetryY;

    public string SymmetryStatusText => (ShowSymmetryX && ShowSymmetryY) ? "SYM XY" : (ShowSymmetryX ? "SYM X" : "SYM Y");

    public CanvasViewModel(PixelCanvas canvas)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _symmetryAxisX = canvas.Width / 2;
        _symmetryAxisY = canvas.Height / 2;
        TileGridSettings.TileSize = TileGridSize;
        TileGridSettings.IsVisible = ShowTileGrid;
        TileGridSettings.PropertyChanged += OnTileGridSettingsPropertyChanged;
        _selection.PropertyChanged += OnSelectionPropertyChanged;
    }

    private void OnSelectionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasActiveSelection));
        OnPropertyChanged(nameof(SelectionText));
        OnPropertyChanged(nameof(SelectionShortText));
    }

    partial void OnSelectionChanged(SelectionModel? oldValue, SelectionModel newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnSelectionPropertyChanged;
        }
        if (newValue != null)
        {
            newValue.PropertyChanged += OnSelectionPropertyChanged;
        }
        OnPropertyChanged(nameof(HasActiveSelection));
        OnPropertyChanged(nameof(SelectionText));
        OnPropertyChanged(nameof(SelectionShortText));
    }

    private void OnTileGridSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TileGridSettings.TileSize) && TileGridSize != TileGridSettings.TileSize)
        {
            TileGridSize = TileGridSettings.TileSize;
        }
        else if (e.PropertyName == nameof(TileGridSettings.IsVisible) && ShowTileGrid != TileGridSettings.IsVisible)
        {
            ShowTileGrid = TileGridSettings.IsVisible;
        }
        else if (e.PropertyName != nameof(TileGridSettings.TileSize) && e.PropertyName != nameof(TileGridSettings.IsVisible))
        {
            OnPropertyChanged(nameof(TileGridSettings));
        }
    }

    partial void OnTileGridSettingsChanged(TileGridSettings? oldValue, TileGridSettings newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnTileGridSettingsPropertyChanged;
        }
        if (newValue != null)
        {
            newValue.PropertyChanged += OnTileGridSettingsPropertyChanged;
            TileGridSize = newValue.TileSize;
            ShowTileGrid = newValue.IsVisible;
        }
        OnPropertyChanged(nameof(TileGridText));
    }

    public void SetCanvas(PixelCanvas newCanvas)
    {
        ArgumentNullException.ThrowIfNull(newCanvas);
        Canvas = newCanvas;
        SymmetryAxisX = newCanvas.Width / 2;
        SymmetryAxisY = newCanvas.Height / 2;
        Selection.Clear();

        if (newCanvas.Width >= 512 || newCanvas.Height >= 512)
        {
            Zoom = 1.0f;
        }
        else if (newCanvas.Width >= 128 || newCanvas.Height >= 128)
        {
            Zoom = 4.0f;
        }

        OnPropertyChanged(nameof(CanvasWidth));
        OnPropertyChanged(nameof(CanvasHeight));
        OnPropertyChanged(nameof(DimensionsText));
    }

    /// <summary>
    /// Notifies listeners that canvas dimensions have changed (e.g. after a resize command).
    /// </summary>
    public void NotifyDimensionsChanged()
    {
        SymmetryAxisX = Canvas.Width / 2;
        SymmetryAxisY = Canvas.Height / 2;
        Selection.Clear();

        OnPropertyChanged(nameof(CanvasWidth));
        OnPropertyChanged(nameof(CanvasHeight));
        OnPropertyChanged(nameof(DimensionsText));
    }

    /// <summary>
    /// Updates the cursor position and hovered pixel color with zero allocation if the pixel hasn't changed.
    /// </summary>
    public void UpdateCursor(int x, int y, bool isInside, PixelColor hoveredColor = default)
    {
        if (CursorX == x && CursorY == y && IsCursorInside == isInside && HoveredColor == hoveredColor)
        {
            return;
        }

        CursorX = x;
        CursorY = y;
        IsCursorInside = isInside;
        HoveredColor = hoveredColor;
        HasHoveredColor = isInside && x >= 0 && y >= 0 && hoveredColor.A > 0;

        _cursorCoordinatesText = isInside && x >= 0 && y >= 0
            ? $"X: {x}, Y: {y}"
            : "—";

        OnPropertyChanged(nameof(CursorCoordinatesText));
        OnPropertyChanged(nameof(HoveredColorHex));
    }

    partial void OnZoomChanged(float value)
    {
        OnPropertyChanged(nameof(ZoomText));
    }

    partial void OnShowSymmetryXChanged(bool value)
    {
        OnPropertyChanged(nameof(HasActiveSymmetry));
        OnPropertyChanged(nameof(SymmetryStatusText));
    }

    partial void OnShowSymmetryYChanged(bool value)
    {
        OnPropertyChanged(nameof(HasActiveSymmetry));
        OnPropertyChanged(nameof(SymmetryStatusText));
    }

    partial void OnTileGridSizeChanged(int value)
    {
        TileGridSettings.TileSize = value;
        OnPropertyChanged(nameof(TileGridText));
    }

    partial void OnShowTileGridChanged(bool value)
    {
        TileGridSettings.IsVisible = value;
    }

    private static readonly float[] ZoomPresets = [1.0f, 2.0f, 4.0f, 8.0f, 16.0f];

    [RelayCommand]
    public void CycleZoomPreset()
    {
        int currentIndex = -1;
        for (int i = 0; i < ZoomPresets.Length; i++)
        {
            if (MathF.Abs(Zoom - ZoomPresets[i]) < 0.05f)
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = (currentIndex + 1) % ZoomPresets.Length;
        Zoom = ZoomPresets[nextIndex];
    }

    [RelayCommand]
    public void SetZoom(float value)
    {
        Zoom = Math.Clamp(value, MinZoom, MaxZoom);
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
    public void ToggleTileGrid()
    {
        ShowTileGrid = !ShowTileGrid;
    }

    [RelayCommand]
    public void CycleTileGridSize()
    {
        TileGridSize = TileGridSettings.GetNextSize(TileGridSize);
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

    [RelayCommand]
    public void ToggleReferenceVisibility()
    {
        ReferenceOverlay.IsVisible = !ReferenceOverlay.IsVisible;
    }

    [RelayCommand]
    public void ClearReferenceOverlay()
    {
        ReferenceOverlay.Clear();
    }
}

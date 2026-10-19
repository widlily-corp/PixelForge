using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelForge.Core.Models;
using PixelForge.Core.Tools;

namespace PixelForge.App.ViewModels;

/// <summary>
/// Supported tools for pixel drawing and manipulation.
/// </summary>
public enum CanvasToolType
{
    Pencil,
    Eraser,
    Fill,
    Eyedropper,
    Line,
    Rectangle,
    Ellipse,
    Selection,
    DitherBrush,
    Gradient
}

/// <summary>
/// Metadata for a tool item displayed in the tool strip.
/// </summary>
public record ToolItem(CanvasToolType Type, string Name, string Shortcut, string IconKey);

/// <summary>
/// ViewModel managing current drawing tool selection, brush size, dithering, and tool configurations.
/// </summary>
public partial class ToolViewModel : ViewModelBase
{
    [ObservableProperty]
    private CanvasToolType _activeTool = CanvasToolType.Pencil;

    [ObservableProperty]
    private int _brushSize = 1;

    [ObservableProperty]
    private bool _isShapeFilled = false;

    [ObservableProperty]
    private DitherPattern _selectedDitherPattern = DitherPattern.None;

    [ObservableProperty]
    private int _fillTolerance = 0;

    [ObservableProperty]
    private bool _fillContiguous = true;

    [ObservableProperty]
    private GradientType _gradientType = GradientType.Linear;

    public IReadOnlyList<DitherPattern> AvailablePatterns { get; } = Enum.GetValues<DitherPattern>();

    public IReadOnlyList<GradientType> AvailableGradientTypes { get; } = Enum.GetValues<GradientType>();

    public IReadOnlyList<ToolItem> AvailableTools { get; } = new List<ToolItem>
    {
        new(CanvasToolType.Pencil, "Pencil", "B", "IconPencil"),
        new(CanvasToolType.Eraser, "Eraser", "E", "IconEraser"),
        new(CanvasToolType.DitherBrush, "Dither Brush", "D", "IconDither"),
        new(CanvasToolType.Fill, "Paint Bucket", "G", "IconFill"),
        new(CanvasToolType.Gradient, "Gradient", "T", "IconGradient"),
        new(CanvasToolType.Eyedropper, "Eyedropper", "I", "IconEyedropper"),
        new(CanvasToolType.Line, "Line", "L", "IconLine"),
        new(CanvasToolType.Rectangle, "Rectangle", "U", "IconRectangle"),
        new(CanvasToolType.Ellipse, "Ellipse", "C", "IconEllipse"),
        new(CanvasToolType.Selection, "Selection", "M", "IconSelection")
    };

    public ToolItem ActiveToolItem => AvailableTools.First(t => t.Type == ActiveTool);

    public string ActiveToolDisplayName => ActiveTool switch
    {
        CanvasToolType.Pencil => "Карандаш",
        CanvasToolType.Eraser => "Ластик",
        CanvasToolType.DitherBrush => "Паттерн кисть",
        CanvasToolType.Fill => "Заливка",
        CanvasToolType.Gradient => "Градиент",
        CanvasToolType.Eyedropper => "Пипетка",
        CanvasToolType.Line => "Линия",
        CanvasToolType.Rectangle => "Прямоугольник",
        CanvasToolType.Ellipse => "Эллипс",
        CanvasToolType.Selection => "Выделение",
        _ => "Инструмент"
    };

    public bool SupportsBrushSize => ActiveTool switch
    {
        CanvasToolType.Pencil or CanvasToolType.Eraser or CanvasToolType.Line or CanvasToolType.DitherBrush => true,
        _ => false
    };

    public bool SupportsShapeFill => ActiveTool switch
    {
        CanvasToolType.Rectangle or CanvasToolType.Ellipse => true,
        _ => false
    };

    public bool SupportsDitherPattern => ActiveTool switch
    {
        CanvasToolType.Pencil or CanvasToolType.DitherBrush or CanvasToolType.Gradient or CanvasToolType.Fill or CanvasToolType.Rectangle or CanvasToolType.Ellipse => true,
        _ => false
    };

    public bool SupportsFillSettings => ActiveTool == CanvasToolType.Fill;

    public bool SupportsGradientSettings => ActiveTool == CanvasToolType.Gradient;

    partial void OnActiveToolChanged(CanvasToolType value)
    {
        if (value == CanvasToolType.DitherBrush && SelectedDitherPattern == DitherPattern.None)
        {
            SelectedDitherPattern = DitherPattern.Bayer4x4;
        }

        OnPropertyChanged(nameof(ActiveToolItem));
        OnPropertyChanged(nameof(ActiveToolDisplayName));
        OnPropertyChanged(nameof(SupportsBrushSize));
        OnPropertyChanged(nameof(SupportsShapeFill));
        OnPropertyChanged(nameof(SupportsDitherPattern));
        OnPropertyChanged(nameof(SupportsFillSettings));
        OnPropertyChanged(nameof(SupportsGradientSettings));
    }

    [RelayCommand]
    public void SelectTool(CanvasToolType tool)
    {
        ActiveTool = tool;
    }

    [RelayCommand]
    public void SetBrushSize(int size)
    {
        BrushSize = Math.Clamp(size, 1, 32);
    }

    [RelayCommand]
    public void IncreaseBrushSize()
    {
        if (BrushSize < 32)
        {
            BrushSize++;
        }
    }

    [RelayCommand]
    public void DecreaseBrushSize()
    {
        if (BrushSize > 1)
        {
            BrushSize--;
        }
    }
}

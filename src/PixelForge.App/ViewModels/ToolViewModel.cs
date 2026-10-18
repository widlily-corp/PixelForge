using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    Selection
}

/// <summary>
/// Metadata for a tool item displayed in the tool strip.
/// </summary>
public record ToolItem(CanvasToolType Type, string Name, string Shortcut, string IconKey);

/// <summary>
/// ViewModel managing current drawing tool selection, brush size, and tool configurations.
/// </summary>
public partial class ToolViewModel : ViewModelBase
{
    [ObservableProperty]
    private CanvasToolType _activeTool = CanvasToolType.Pencil;

    [ObservableProperty]
    private int _brushSize = 1;

    [ObservableProperty]
    private bool _isShapeFilled = false;

    public IReadOnlyList<ToolItem> AvailableTools { get; } = new List<ToolItem>
    {
        new(CanvasToolType.Pencil, "Pencil", "B", "IconPencil"),
        new(CanvasToolType.Eraser, "Eraser", "E", "IconEraser"),
        new(CanvasToolType.Fill, "Paint Bucket", "G", "IconFill"),
        new(CanvasToolType.Eyedropper, "Eyedropper", "I", "IconEyedropper"),
        new(CanvasToolType.Line, "Line", "L", "IconLine"),
        new(CanvasToolType.Rectangle, "Rectangle", "U", "IconRectangle"),
        new(CanvasToolType.Ellipse, "Ellipse", "C", "IconEllipse"),
        new(CanvasToolType.Selection, "Selection", "M", "IconSelection")
    };

    public ToolItem ActiveToolItem => AvailableTools.First(t => t.Type == ActiveTool);

    partial void OnActiveToolChanged(CanvasToolType value)
    {
        OnPropertyChanged(nameof(ActiveToolItem));
    }

    [RelayCommand]
    public void SelectTool(CanvasToolType tool)
    {
        ActiveTool = tool;
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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelForge.Core.Models;
using PixelForge.Core.Patterns;
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
    Gradient,
    PatternBrush,
    MagicWand
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
    private readonly Dictionary<CanvasToolType, ITool> _tools;

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

    [ObservableProperty]
    private bool _isPixelPerfect = false;

    [ObservableProperty]
    private int _stabilizerStrength = 0;

    [ObservableProperty]
    private PatternPreset _selectedPatternPreset = PatternPresets.Checkerboard;

    /// <summary>
    /// Gets all predefined 8x8 pattern presets.
    /// </summary>
    public IReadOnlyList<PatternPreset> Available8x8Patterns { get; } = PatternPresets.AllPresets;

    /// <summary>
    /// Gets the registered Magic Wand tool instance.
    /// </summary>
    public MagicWandTool MagicWandTool { get; } = new();

    /// <summary>
    /// Gets the registered Pattern Brush tool instance.
    /// </summary>
    public PatternBrushTool PatternBrushTool { get; } = new();

    /// <summary>
    /// Gets the dictionary of all registered tool instances.
    /// </summary>
    public IReadOnlyDictionary<CanvasToolType, ITool> Tools => _tools;

    public IReadOnlyList<DitherPattern> AvailablePatterns { get; } = Enum.GetValues<DitherPattern>();

    public IReadOnlyList<GradientType> AvailableGradientTypes { get; } = Enum.GetValues<GradientType>();

    public IReadOnlyList<ToolItem> AvailableTools { get; } = new List<ToolItem>
    {
        new(CanvasToolType.Pencil, "Pencil", "B", "IconPencil"),
        new(CanvasToolType.Eraser, "Eraser", "E", "IconEraser"),
        new(CanvasToolType.DitherBrush, "Dither Brush", "D", "IconDither"),
        new(CanvasToolType.PatternBrush, "Pattern Brush", "P", "IconPatternBrush"),
        new(CanvasToolType.Fill, "Paint Bucket", "G", "IconFill"),
        new(CanvasToolType.MagicWand, "Magic Wand", "W", "IconMagicWand"),
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
        CanvasToolType.PatternBrush => "Текстурная кисть",
        CanvasToolType.Fill => "Заливка",
        CanvasToolType.MagicWand => "Волшебная палочка",
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
        CanvasToolType.Pencil or CanvasToolType.Eraser or CanvasToolType.Line or CanvasToolType.DitherBrush or CanvasToolType.PatternBrush => true,
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

    public bool SupportsFillSettings => ActiveTool is CanvasToolType.Fill or CanvasToolType.MagicWand;

    public bool SupportsGradientSettings => ActiveTool == CanvasToolType.Gradient;

    public bool Supports8x8Pattern => ActiveTool == CanvasToolType.PatternBrush;

    public bool SupportsPixelPerfect => ActiveTool is CanvasToolType.Pencil or CanvasToolType.Eraser or CanvasToolType.DitherBrush or CanvasToolType.PatternBrush;

    public bool SupportsStabilizer => ActiveTool is CanvasToolType.Pencil or CanvasToolType.Eraser or CanvasToolType.DitherBrush or CanvasToolType.PatternBrush;

    public ToolViewModel()
    {
        _tools = new Dictionary<CanvasToolType, ITool>
        {
            [CanvasToolType.Pencil] = new PencilTool(),
            [CanvasToolType.Eraser] = new EraserTool(),
            [CanvasToolType.DitherBrush] = new DitherBrushTool(),
            [CanvasToolType.PatternBrush] = PatternBrushTool,
            [CanvasToolType.Fill] = new FillTool(),
            [CanvasToolType.MagicWand] = MagicWandTool,
            [CanvasToolType.Gradient] = new GradientTool(),
            [CanvasToolType.Eyedropper] = new EyedropperTool(),
            [CanvasToolType.Line] = new LineTool(),
            [CanvasToolType.Rectangle] = new RectangleTool(),
            [CanvasToolType.Ellipse] = new EllipseTool(),
            [CanvasToolType.Selection] = new SelectionTool()
        };
    }

    partial void OnStabilizerStrengthChanged(int value)
    {
        PatternBrushTool.StabilizerStrength = Math.Clamp(value, 0, 10);
    }

    partial void OnIsPixelPerfectChanged(bool value)
    {
        PatternBrushTool.PixelPerfect = value;
    }

    partial void OnSelectedPatternPresetChanged(PatternPreset value)
    {
        PatternBrushTool.Pattern = value;
    }

    partial void OnBrushSizeChanged(int value)
    {
        PatternBrushTool.BrushSize = value;
    }

    partial void OnFillToleranceChanged(int value)
    {
        MagicWandTool.Tolerance = (int)(value * 2.55f);
    }

    partial void OnFillContiguousChanged(bool value)
    {
        MagicWandTool.Contiguous = value;
    }

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
        OnPropertyChanged(nameof(Supports8x8Pattern));
        OnPropertyChanged(nameof(SupportsPixelPerfect));
        OnPropertyChanged(nameof(SupportsStabilizer));
    }

    [RelayCommand]
    public void SelectTool(CanvasToolType tool)
    {
        ActiveTool = tool;
    }

    [RelayCommand]
    public void TogglePixelPerfect()
    {
        IsPixelPerfect = !IsPixelPerfect;
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

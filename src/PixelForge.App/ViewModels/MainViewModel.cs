using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelForge.App.Services;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Services;
using PixelForge.Core.Tools;

namespace PixelForge.App.ViewModels;

/// <summary>
/// Root ViewModel orchestrating canvas, tools, layers, palette, history, autosave, file operations, and OTA updates.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IExportService _exportService;
    private readonly IAutosaveService _autosaveService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IClipboardService _clipboardService;
    private readonly IUpdateService _updateService;
    private readonly UndoRedoService _undoRedoService;

    [ObservableProperty]
    private ToolViewModel _toolViewModel;

    [ObservableProperty]
    private CanvasViewModel _canvasViewModel;

    [ObservableProperty]
    private LayerViewModel _layerViewModel;

    [ObservableProperty]
    private PaletteViewModel _paletteViewModel;

    [ObservableProperty]
    private HistoryViewModel _historyViewModel;

    [ObservableProperty]
    private ThemeService _themeService;

    [ObservableProperty]
    private string? _currentFilePath;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isUpdateBannerVisible = false;

    [ObservableProperty]
    private string? _updateBannerMessage;

    [ObservableProperty]
    private string? _updateDownloadUrl;

    [ObservableProperty]
    private bool _isUpdating = false;

    [ObservableProperty]
    private double _updateProgress = 0.0;

    [ObservableProperty]
    private string _updateStatusText = string.Empty;

    [ObservableProperty]
    private bool _isTiledMode = false;

    public string Title
    {
        get
        {
            string fileName = string.IsNullOrEmpty(CurrentFilePath) ? "Untitled" : Path.GetFileName(CurrentFilePath);
            string dirtyMarker = IsDirty ? "*" : "";
            return $"PixelForge v{_updateService.CurrentVersion} — {fileName}{dirtyMarker}";
        }
    }

    public MainViewModel(
        IProjectService projectService,
        IExportService exportService,
        IAutosaveService autosaveService,
        IFileDialogService fileDialogService,
        IClipboardService clipboardService,
        IUpdateService updateService,
        ThemeService themeService,
        UndoRedoService undoRedoService)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _autosaveService = autosaveService ?? throw new ArgumentNullException(nameof(autosaveService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _undoRedoService = undoRedoService ?? throw new ArgumentNullException(nameof(undoRedoService));

        // Create default 32x32 pixel canvas
        var canvas = new PixelCanvas(32, 32);

        _toolViewModel = new ToolViewModel();
        _canvasViewModel = new CanvasViewModel(canvas);
        _layerViewModel = new LayerViewModel(canvas, _undoRedoService);
        _paletteViewModel = new PaletteViewModel();
        _historyViewModel = new HistoryViewModel(_undoRedoService);

        // Mark dirty when undo/redo changes
        _undoRedoService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UndoRedoService.UndoStack))
            {
                IsDirty = true;
                OnPropertyChanged(nameof(Title));
            }
        };

        // Wire autosave service
        _autosaveService.Autosaved += (_, timestamp) =>
        {
            StatusMessage = $"Autosaved at {timestamp:HH:mm:ss}";
        };
        _autosaveService.AutosaveFailed += (_, ex) =>
        {
            StatusMessage = $"Autosave error: {ex.Message}";
        };
        _autosaveService.Start(GetProjectSnapshot, TimeSpan.FromMinutes(2));

        // Run non-blocking background update check
        _ = RunInitialUpdateCheckAsync();
    }

    public Project GetProjectSnapshot()
    {
        return Project.FromCanvas(CanvasViewModel.Canvas, PaletteViewModel.Colors);
    }

    public void LoadProject(Project project, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        var canvas = project.ToCanvas();
        CanvasViewModel.SetCanvas(canvas);
        LayerViewModel.SetCanvas(canvas);

        if (project.Palette != null && project.Palette.Count > 0)
        {
            var colors = new List<PixelColor>();
            foreach (var hex in project.Palette)
            {
                if (PixelColor.TryParse(hex, out var color))
                {
                    colors.Add(color);
                }
            }

            if (colors.Count > 0)
            {
                PaletteViewModel.LoadPreset(new Core.Palettes.Palette("Project Palette", colors));
            }
        }

        _undoRedoService.Clear();
        CurrentFilePath = filePath;
        IsDirty = false;
        OnPropertyChanged(nameof(Title));
        StatusMessage = $"Loaded {Path.GetFileName(filePath ?? "project")}";
    }

    [RelayCommand]
    public void CreateNewProject()
    {
        NewProject(32, 32);
    }

    public void NewProject(int width = 32, int height = 32)
    {
        var canvas = new PixelCanvas(width, height);
        CanvasViewModel.SetCanvas(canvas);
        LayerViewModel.SetCanvas(canvas);
        _undoRedoService.Clear();
        CurrentFilePath = null;
        IsDirty = false;
        OnPropertyChanged(nameof(Title));
        StatusMessage = $"New canvas ({width}x{height})";
    }

    public void ResizeCanvas(int newWidth, int newHeight, CanvasAnchor anchor = CanvasAnchor.Center, ResizeMode mode = ResizeMode.Anchor)
    {
        if (newWidth <= 0 || newHeight <= 0) return;
        if (newWidth == CanvasViewModel.CanvasWidth && newHeight == CanvasViewModel.CanvasHeight) return;

        var cmd = new ResizeCanvasCommand(
            CanvasViewModel.Canvas,
            newWidth,
            newHeight,
            anchor,
            mode,
            () =>
            {
                CanvasViewModel.NotifyDimensionsChanged();
                IsDirty = true;
                OnPropertyChanged(nameof(Title));
            });

        _undoRedoService.ExecuteCommand(cmd);
        StatusMessage = $"Размер холста изменён на {newWidth} × {newHeight} px";
    }

    [RelayCommand]
    public void FlipActiveLayerHorizontal()
    {
        FlipActiveLayer(FlipDirection.Horizontal);
    }

    [RelayCommand]
    public void FlipActiveLayerVertical()
    {
        FlipActiveLayer(FlipDirection.Vertical);
    }

    public void FlipActiveLayer(FlipDirection direction)
    {
        var activeLayer = CanvasViewModel.Canvas.ActiveLayer;
        if (activeLayer == null) return;

        var selectionRect = CanvasViewModel.Selection.IsActive ? CanvasViewModel.Selection.Bounds : (SkiaSharp.SKRectI?)null;

        var cmd = new FlipLayerCommand(
            activeLayer,
            direction,
            () =>
            {
                IsDirty = true;
                OnPropertyChanged(nameof(Title));
            },
            selectionRect);

        _undoRedoService.ExecuteCommand(cmd);
        string target = selectionRect.HasValue ? "Выделение" : $"Слой '{activeLayer.Name}'";
        StatusMessage = $"{target} отражён {(direction == FlipDirection.Horizontal ? "по горизонтали" : "по вертикали")}";
    }

    [RelayCommand]
    public void RotateActiveLayerCW() => RotateActiveLayer(RotationAngle.Rotate90CW);

    [RelayCommand]
    public void RotateActiveLayerCCW() => RotateActiveLayer(RotationAngle.Rotate90CCW);

    [RelayCommand]
    public void RotateActiveLayer180() => RotateActiveLayer(RotationAngle.Rotate180);

    public void RotateActiveLayer(RotationAngle angle)
    {
        var activeLayer = CanvasViewModel.Canvas.ActiveLayer;
        if (activeLayer == null) return;

        var selectionRect = CanvasViewModel.Selection.IsActive ? CanvasViewModel.Selection.Bounds : (SkiaSharp.SKRectI?)null;

        var cmd = new RotateLayerCommand(
            activeLayer,
            angle,
            () =>
            {
                IsDirty = true;
                OnPropertyChanged(nameof(Title));
            },
            selectionRect);

        _undoRedoService.ExecuteCommand(cmd);
        string angleStr = angle switch
        {
            RotationAngle.Rotate90CW => "на 90° по часовой",
            RotationAngle.Rotate90CCW => "на 90° против часовой",
            RotationAngle.Rotate180 => "на 180°",
            _ => ""
        };
        string target = selectionRect.HasValue ? "Выделение" : $"Слой '{activeLayer.Name}'";
        StatusMessage = $"{target} повёрнут {angleStr}";
    }

    [RelayCommand]
    public void FlipCanvasHorizontal() => FlipCanvas(FlipDirection.Horizontal);

    [RelayCommand]
    public void FlipCanvasVertical() => FlipCanvas(FlipDirection.Vertical);

    public void FlipCanvas(FlipDirection direction)
    {
        var cmd = new FlipCanvasCommand(
            CanvasViewModel.Canvas,
            direction,
            () =>
            {
                IsDirty = true;
                OnPropertyChanged(nameof(Title));
            });

        _undoRedoService.ExecuteCommand(cmd);
        StatusMessage = $"Холст отражён {(direction == FlipDirection.Horizontal ? "по горизонтали" : "по вертикали")}";
    }

    [RelayCommand]
    public void RotateCanvasCW() => RotateCanvas(RotationAngle.Rotate90CW);

    [RelayCommand]
    public void RotateCanvasCCW() => RotateCanvas(RotationAngle.Rotate90CCW);

    [RelayCommand]
    public void RotateCanvas180() => RotateCanvas(RotationAngle.Rotate180);

    public void RotateCanvas(RotationAngle angle)
    {
        var cmd = new RotateCanvasCommand(
            CanvasViewModel.Canvas,
            angle,
            () =>
            {
                IsDirty = true;
                OnPropertyChanged(nameof(Title));
            });

        _undoRedoService.ExecuteCommand(cmd);
        string angleStr = angle switch
        {
            RotationAngle.Rotate90CW => "на 90° по часовой",
            RotationAngle.Rotate90CCW => "на 90° против часовой",
            RotationAngle.Rotate180 => "на 180°",
            _ => ""
        };
        StatusMessage = $"Холст повёрнут {angleStr}";
    }

    [RelayCommand]
    public void ClearActiveLayer()
    {
        var layer = CanvasViewModel.Canvas.ActiveLayer;
        if (layer == null) return;

        var deltas = new List<PixelDelta>(layer.Width * layer.Height);
        for (int y = 0; y < layer.Height; y++)
        {
            for (int x = 0; x < layer.Width; x++)
            {
                var oldCol = layer.GetPixel(x, y);
                if (oldCol.A > 0)
                {
                    deltas.Add(new PixelDelta(x, y, oldCol, PixelColor.Transparent));
                }
            }
        }

        if (deltas.Count > 0)
        {
            var cmd = new DrawPixelsCommand(layer, deltas.ToArray(), $"Clear {layer.Name}");
            _undoRedoService.ExecuteCommand(cmd);
            StatusMessage = $"Слой '{layer.Name}' очищен";
        }
    }

    [RelayCommand]
    public async Task OpenProjectAsync()
    {
        try
        {
            string? filePath = await _fileDialogService.ShowOpenProjectDialogAsync();
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var project = await _projectService.LoadProjectAsync(filePath);
            LoadProject(project, filePath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open project: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SaveProjectAsync()
    {
        if (string.IsNullOrEmpty(CurrentFilePath))
        {
            await SaveProjectAsAsync();
            return;
        }

        try
        {
            var project = GetProjectSnapshot();
            await _projectService.SaveProjectAsync(project, CurrentFilePath);
            IsDirty = false;
            OnPropertyChanged(nameof(Title));
            StatusMessage = $"Saved to {Path.GetFileName(CurrentFilePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SaveProjectAsAsync()
    {
        try
        {
            string defaultName = string.IsNullOrEmpty(CurrentFilePath) ? "untitled.pxproj" : Path.GetFileName(CurrentFilePath);
            string? filePath = await _fileDialogService.ShowSaveProjectDialogAsync(defaultName);
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            CurrentFilePath = filePath;
            var project = GetProjectSnapshot();
            await _projectService.SaveProjectAsync(project, filePath);
            IsDirty = false;
            OnPropertyChanged(nameof(Title));
            StatusMessage = $"Saved as {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save As error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ExportPngAsync()
    {
        try
        {
            string? filePath = await _fileDialogService.ShowExportImageDialogAsync("png", "art.png");
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            await _exportService.ExportPngAsync(CanvasViewModel.Canvas, filePath, scale: 1);
            StatusMessage = $"Exported PNG to {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ExportBmpAsync()
    {
        try
        {
            string? filePath = await _fileDialogService.ShowExportImageDialogAsync("bmp", "art.bmp");
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            await _exportService.ExportBmpAsync(CanvasViewModel.Canvas, filePath, scale: 1);
            StatusMessage = $"Exported BMP to {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CopyAsync()
    {
        var selection = CanvasViewModel.Selection;
        var layer = LayerViewModel.ActiveLayer;
        if (layer == null)
        {
            return;
        }

        if (selection.IsActive && selection.Bounds.Width > 0 && selection.Bounds.Height > 0)
        {
            int w = selection.Bounds.Width;
            int h = selection.Bounds.Height;
            var pixels = new PixelColor[w * h];

            if (selection.IsFloating && selection.Pixels.Length == w * h)
            {
                Array.Copy(selection.Pixels, pixels, pixels.Length);
            }
            else
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        pixels[y * w + x] = layer.GetPixel(selection.Bounds.Left + x, selection.Bounds.Top + y);
                    }
                }
            }

            await _clipboardService.SetPixelsAsync(pixels, w, h);
            StatusMessage = $"Copied {w}x{h} selection";
        }
        else
        {
            // Copy entire active layer
            await _clipboardService.SetPixelsAsync(layer.Pixels, layer.Width, layer.Height);
            StatusMessage = $"Copied entire layer ({layer.Name})";
        }
    }

    [RelayCommand]
    public async Task CutAsync()
    {
        var selection = CanvasViewModel.Selection;
        var layer = LayerViewModel.ActiveLayer;
        if (layer == null)
        {
            return;
        }

        await CopyAsync();

        if (selection.IsActive && selection.Bounds.Width > 0 && selection.Bounds.Height > 0)
        {
            int w = selection.Bounds.Width;
            int h = selection.Bounds.Height;
            var deltas = new List<PixelDelta>(w * h);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int px = selection.Bounds.Left + x;
                    int py = selection.Bounds.Top + y;
                    var oldColor = layer.GetPixel(px, py);
                    if (oldColor.A > 0)
                    {
                        deltas.Add(new PixelDelta(px, py, oldColor, PixelColor.Transparent));
                    }
                }
            }

            if (deltas.Count > 0)
            {
                var command = new DrawPixelsCommand(layer, deltas, "Cut Selection");
                _undoRedoService.ExecuteCommand(command);
            }

            selection.Clear();
            StatusMessage = "Cut selection to clipboard";
        }
    }

    [RelayCommand]
    public async Task PasteAsync()
    {
        var result = await _clipboardService.GetPixelsAsync();
        if (result == null)
        {
            StatusMessage = "Clipboard is empty";
            return;
        }

        var (pixels, w, h) = result.Value;
        var selection = CanvasViewModel.Selection;

        // Place floating selection in center of viewport or canvas
        int startX = Math.Max(0, (CanvasViewModel.CanvasWidth - w) / 2);
        int startY = Math.Max(0, (CanvasViewModel.CanvasHeight - h) / 2);

        selection.Select(new SkiaSharp.SKRectI(startX, startY, startX + w, startY + h), pixels);
        selection.IsFloating = true;

        StatusMessage = $"Pasted {w}x{h} pixels as floating selection";
    }

    [RelayCommand]
    public void SelectAll()
    {
        var layer = CanvasViewModel.Canvas.ActiveLayer;
        if (layer == null) return;
        CanvasViewModel.Selection.Select(new SkiaSharp.SKRectI(0, 0, layer.Width, layer.Height));
        StatusMessage = "Выделен весь холст (Ctrl+A)";
    }

    [RelayCommand]
    public void Deselect()
    {
        CanvasViewModel.Selection.Clear();
        StatusMessage = "Выделение снято (Ctrl+D)";
    }

    [RelayCommand]
    public void DeleteSelectionOrClearLayer()
    {
        var selection = CanvasViewModel.Selection;
        var layer = CanvasViewModel.Canvas.ActiveLayer;
        if (layer == null) return;

        if (selection.IsActive && selection.Bounds.Width > 0 && selection.Bounds.Height > 0)
        {
            int w = selection.Bounds.Width;
            int h = selection.Bounds.Height;
            var deltas = new List<PixelDelta>(w * h);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int px = selection.Bounds.Left + x;
                    int py = selection.Bounds.Top + y;
                    var oldColor = layer.GetPixel(px, py);
                    if (oldColor.A > 0)
                    {
                        deltas.Add(new PixelDelta(px, py, oldColor, PixelColor.Transparent));
                    }
                }
            }

            if (deltas.Count > 0)
            {
                var command = new DrawPixelsCommand(layer, deltas.ToArray(), "Delete Selection");
                _undoRedoService.ExecuteCommand(command);
            }

            selection.Clear();
            StatusMessage = "Выделенная область удалена";
        }
        else
        {
            ClearActiveLayer();
        }
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        StatusMessage = "Проверка наличия обновлений...";
        var result = await _updateService.CheckForUpdatesAsync(silent: false);

        if (!result.Success)
        {
            StatusMessage = "Не удалось связаться с сервером обновлений. Проверьте подключение к сети.";
            return;
        }

        if (result.IsUpdateAvailable)
        {
            UpdateDownloadUrl = result.DownloadUrl;
            UpdateBannerMessage = $"Доступна новая версия v{result.LatestVersion}! {result.ReleaseNotes}";
            IsUpdateBannerVisible = true;
            StatusMessage = $"Доступно обновление: v{result.LatestVersion}";
        }
        else
        {
            StatusMessage = $"У вас установлена последняя версия (v{_updateService.CurrentVersion}). Обновления не требуются.";
        }
    }

    [RelayCommand]
    public async Task ApplyUpdateAsync()
    {
        if (string.IsNullOrEmpty(UpdateDownloadUrl))
        {
            return;
        }

        IsUpdating = true;
        UpdateProgress = 0.0;
        UpdateStatusText = "Подготовка к автоматической установке...";

        var progress = new Progress<double>(p =>
        {
            UpdateProgress = p * 100.0;
            UpdateStatusText = $"Загрузка обновления: {(int)(p * 100)}%";
        });

        bool success = await _updateService.DownloadAndInstallUpdateAsync(UpdateDownloadUrl, progress).ConfigureAwait(false);
        if (!success)
        {
            IsUpdating = false;
            StatusMessage = "Не удалось автоматически установить обновление. Попробуйте скачать вручную.";
        }
    }

    [RelayCommand]
    public void OpenUpdateDownloadPage()
    {
        if (!string.IsNullOrEmpty(UpdateDownloadUrl))
        {
            _updateService.OpenDownloadPage(UpdateDownloadUrl);
        }
        IsUpdateBannerVisible = false;
    }

    [RelayCommand]
    public void DismissUpdateBanner()
    {
        IsUpdateBannerVisible = false;
    }

    /// <summary>
    /// Toggles the seamless 3x3 tiled mode.
    /// </summary>
    [RelayCommand]
    public void ToggleTiledMode()
    {
        IsTiledMode = !IsTiledMode;
        StatusMessage = IsTiledMode ? "Бесшовный режим (Tiled Mode) включен" : "Бесшовный режим выключен";
    }

    /// <summary>
    /// Generates an outline around non-transparent pixels on the active layer or selection.
    /// </summary>
    [RelayCommand]
    public void AddOutline()
    {
        var layer = CanvasViewModel.Canvas.ActiveLayer;
        if (layer == null)
        {
            return;
        }

        var selection = CanvasViewModel.Selection.IsActive ? CanvasViewModel.Selection : null;
        var color = PaletteViewModel.PrimaryColor;
        int thickness = Math.Max(1, ToolViewModel.BrushSize);

        var cmd = new OutlineCommand(layer, color, is8Way: false, thickness: thickness, selection: selection);
        _undoRedoService.ExecuteCommand(cmd);
        IsDirty = true;
        OnPropertyChanged(nameof(Title));
        StatusMessage = $"Добавлен контур толщиной {thickness} px";
    }

    /// <summary>
    /// Generates a drop shadow for non-transparent pixels on the active layer or selection.
    /// </summary>
    [RelayCommand]
    public void AddShadow()
    {
        var layer = CanvasViewModel.Canvas.ActiveLayer;
        if (layer == null)
        {
            return;
        }

        var selection = CanvasViewModel.Selection.IsActive ? CanvasViewModel.Selection : null;
        var shadowColor = new PixelColor(0, 0, 0, 180);

        var cmd = new ShadowCommand(layer, shadowColor, dx: 1, dy: 1, opacity: 0.5f, selection: selection);
        _undoRedoService.ExecuteCommand(cmd);
        IsDirty = true;
        OnPropertyChanged(nameof(Title));
        StatusMessage = "Добавлена тень (смещение 1, 1)";
    }

    /// <summary>
    /// Replaces occurrences of the secondary color with the primary color on the active layer or selection.
    /// </summary>
    [RelayCommand]
    public void ReplaceColor()
    {
        var layer = CanvasViewModel.Canvas.ActiveLayer;
        if (layer == null)
        {
            return;
        }

        var selection = CanvasViewModel.Selection.IsActive ? CanvasViewModel.Selection : null;
        var targetColor = PaletteViewModel.SecondaryColor;
        var replacementColor = PaletteViewModel.PrimaryColor;
        int tolerance = (int)(ToolViewModel.FillTolerance * 2.55f);

        var cmd = new ReplaceColorCommand(layer, targetColor, replacementColor, tolerance, selection);
        _undoRedoService.ExecuteCommand(cmd);
        IsDirty = true;
        OnPropertyChanged(nameof(Title));
        StatusMessage = $"Заменен цвет на слое '{layer.Name}'";
    }

    /// <summary>
    /// Event triggered when opening the hotkeys cheat sheet dialog is requested.
    /// </summary>
    public event EventHandler? ShowHotkeysRequested;

    /// <summary>
    /// Requests display of the hotkeys cheat sheet dialog.
    /// </summary>
    [RelayCommand]
    public void OpenHotkeysDialog()
    {
        ShowHotkeysRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Event triggered when opening the color ramp generator dialog is requested.
    /// </summary>
    public event EventHandler? ShowColorRampRequested;

    /// <summary>
    /// Requests display of the color ramp generator dialog.
    /// </summary>
    [RelayCommand]
    public void OpenColorRampDialog()
    {
        ShowColorRampRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task RunInitialUpdateCheckAsync()
    {
        try
        {
            var result = await _updateService.CheckForUpdatesAsync(silent: true);
            if (result.Success && result.IsUpdateAvailable)
            {
                UpdateDownloadUrl = result.DownloadUrl;
                UpdateBannerMessage = $"Доступна новая версия v{result.LatestVersion}!";
                IsUpdateBannerVisible = true;
            }
        }
        catch
        {
            // Silent check must never crash or block
        }
    }
}

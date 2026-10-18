using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelForge.App.Services;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Services;

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
    public async Task CheckForUpdatesAsync()
    {
        StatusMessage = "Checking for updates...";
        var result = await _updateService.CheckForUpdatesAsync(silent: false);

        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "Failed to check for updates";
            return;
        }

        if (result.IsUpdateAvailable)
        {
            UpdateDownloadUrl = result.DownloadUrl;
            UpdateBannerMessage = $"New version v{result.LatestVersion} is available! {result.ReleaseNotes}";
            IsUpdateBannerVisible = true;
            StatusMessage = $"Update available: v{result.LatestVersion}";
        }
        else
        {
            StatusMessage = $"PixelForge is up to date (v{_updateService.CurrentVersion})";
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

    private async Task RunInitialUpdateCheckAsync()
    {
        try
        {
            var result = await _updateService.CheckForUpdatesAsync(silent: true);
            if (result.Success && result.IsUpdateAvailable)
            {
                UpdateDownloadUrl = result.DownloadUrl;
                UpdateBannerMessage = $"New version v{result.LatestVersion} is available!";
                IsUpdateBannerVisible = true;
            }
        }
        catch
        {
            // Silent check must never crash or block
        }
    }
}

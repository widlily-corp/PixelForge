using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelForge.Core.Localization;
using PixelForge.Core.Services;

namespace PixelForge.App.ViewModels;

/// <summary>
/// Preset definition for quick default canvas sizing.
/// </summary>
public record CanvasPresetItem(int Width, int Height, string Label);

/// <summary>
/// ViewModel managing all application settings categories, real-time validation, and persistence.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly AppSettingsService _appSettingsService;

    public event EventHandler? SettingsApplied;
    public event EventHandler? RequestClose;

    [ObservableProperty]
    private int _selectedTabIndex = 0;

    public bool IsTab0 => SelectedTabIndex == 0;
    public bool IsTab1 => SelectedTabIndex == 1;
    public bool IsTab2 => SelectedTabIndex == 2;
    public bool IsTab3 => SelectedTabIndex == 3;
    public bool IsTab4 => SelectedTabIndex == 4;
    public bool IsTab5 => SelectedTabIndex == 5;

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsTab0));
        OnPropertyChanged(nameof(IsTab1));
        OnPropertyChanged(nameof(IsTab2));
        OnPropertyChanged(nameof(IsTab3));
        OnPropertyChanged(nameof(IsTab4));
        OnPropertyChanged(nameof(IsTab5));
    }

    // --- Category 0: General ---
    [ObservableProperty]
    private AppLanguage _selectedLanguage;

    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private bool _checkUpdatesOnStartup;

    // --- Category 1: Canvas & Grid ---
    [ObservableProperty]
    private int _defaultCanvasWidth;

    [ObservableProperty]
    private int _defaultCanvasHeight;

    [ObservableProperty]
    private bool _showPixelGrid;

    [ObservableProperty]
    private double _pixelGridMinZoom;

    [ObservableProperty]
    private int _tileGridWidth;

    [ObservableProperty]
    private int _tileGridHeight;

    [ObservableProperty]
    private int _checkerSize;

    [ObservableProperty]
    private string _colorCheckerLight = "#2A2E40";

    [ObservableProperty]
    private string _colorCheckerDark = "#1F2332";

    // --- Category 2: Tools Defaults ---
    [ObservableProperty]
    private int _defaultStabilizerStrength;

    [ObservableProperty]
    private bool _defaultPixelPerfect;

    [ObservableProperty]
    private int _defaultBrushSize;

    // --- Category 3: Autosave & History ---
    [ObservableProperty]
    private bool _autosaveEnabled;

    [ObservableProperty]
    private int _autosaveIntervalMinutes;

    [ObservableProperty]
    private int _maxUndoSteps;

    // --- Category 4: Performance ---
    [ObservableProperty]
    private bool _preloadMemory;

    [ObservableProperty]
    private bool _hardwareAcceleration;

    public IReadOnlyList<AppLanguage> AvailableLanguages { get; } = [AppLanguage.Russian, AppLanguage.English];

    public IReadOnlyList<int> AvailableCheckerSizes { get; } = [8, 16, 32, 64];

    public IReadOnlyList<CanvasPresetItem> CanvasPresets { get; } =
    [
        new(16, 16, "16 × 16"),
        new(24, 24, "24 × 24"),
        new(32, 32, "32 × 32"),
        new(48, 48, "48 × 48"),
        new(64, 64, "64 × 64"),
        new(128, 128, "128 × 128")
    ];

    public SettingsViewModel(AppSettingsService? appSettingsService = null)
    {
        _appSettingsService = appSettingsService ?? new AppSettingsService();
        LoadFromSettings(_appSettingsService.Data);
    }

    /// <summary>
    /// Loads ViewModel state from an AppSettingsData snapshot.
    /// </summary>
    public void LoadFromSettings(AppSettingsData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        SelectedLanguage = Enum.TryParse<AppLanguage>(data.Language, true, out var lang) ? lang : AppLanguage.Russian;
        IsDarkTheme = data.IsDarkTheme;
        CheckUpdatesOnStartup = data.CheckUpdatesOnStartup;

        DefaultCanvasWidth = data.DefaultCanvasWidth;
        DefaultCanvasHeight = data.DefaultCanvasHeight;
        ShowPixelGrid = data.ShowPixelGrid;
        PixelGridMinZoom = data.PixelGridMinZoom;
        TileGridWidth = data.TileGridWidth;
        TileGridHeight = data.TileGridHeight;
        CheckerSize = data.CheckerSize;
        ColorCheckerLight = data.ColorCheckerLight;
        ColorCheckerDark = data.ColorCheckerDark;

        DefaultStabilizerStrength = data.DefaultStabilizerStrength;
        DefaultPixelPerfect = data.DefaultPixelPerfect;
        DefaultBrushSize = data.DefaultBrushSize;

        AutosaveEnabled = data.AutosaveEnabled;
        AutosaveIntervalMinutes = data.AutosaveIntervalMinutes;
        MaxUndoSteps = data.MaxUndoSteps;

        PreloadMemory = data.PreloadMemory;
        HardwareAcceleration = data.HardwareAcceleration;
    }

    /// <summary>
    /// Generates an AppSettingsData snapshot reflecting the current ViewModel state.
    /// </summary>
    public AppSettingsData CreateSnapshot()
    {
        return new AppSettingsData
        {
            Language = SelectedLanguage.ToString(),
            IsDarkTheme = IsDarkTheme,
            CheckUpdatesOnStartup = CheckUpdatesOnStartup,

            DefaultCanvasWidth = Math.Clamp(DefaultCanvasWidth, 1, 4096),
            DefaultCanvasHeight = Math.Clamp(DefaultCanvasHeight, 1, 4096),
            ShowPixelGrid = ShowPixelGrid,
            PixelGridMinZoom = Math.Clamp(PixelGridMinZoom, 1.0, 32.0),
            TileGridWidth = Math.Clamp(TileGridWidth, 1, 1024),
            TileGridHeight = Math.Clamp(TileGridHeight, 1, 1024),
            CheckerSize = Math.Clamp(CheckerSize, 2, 256),
            ColorCheckerLight = string.IsNullOrWhiteSpace(ColorCheckerLight) ? "#2A2E40" : ColorCheckerLight.Trim(),
            ColorCheckerDark = string.IsNullOrWhiteSpace(ColorCheckerDark) ? "#1F2332" : ColorCheckerDark.Trim(),

            DefaultStabilizerStrength = Math.Clamp(DefaultStabilizerStrength, 0, 10),
            DefaultPixelPerfect = DefaultPixelPerfect,
            DefaultBrushSize = Math.Clamp(DefaultBrushSize, 1, 64),

            AutosaveEnabled = AutosaveEnabled,
            AutosaveIntervalMinutes = Math.Clamp(AutosaveIntervalMinutes, 1, 60),
            MaxUndoSteps = Math.Clamp(MaxUndoSteps, 10, 500),

            PreloadMemory = PreloadMemory,
            HardwareAcceleration = HardwareAcceleration,
            RecentProjects = new List<string>(_appSettingsService.RecentProjects)
        };
    }

    [RelayCommand]
    public void SelectTab(int index)
    {
        SelectedTabIndex = Math.Clamp(index, 0, 5);
    }

    [RelayCommand]
    public void ApplyCanvasPreset(CanvasPresetItem preset)
    {
        if (preset == null) return;
        DefaultCanvasWidth = preset.Width;
        DefaultCanvasHeight = preset.Height;
    }

    [RelayCommand]
    public void SaveAndApply()
    {
        var snapshot = CreateSnapshot();
        _appSettingsService.ApplySettings(snapshot);
        SettingsApplied?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void ResetToDefaults()
    {
        var defaultData = new AppSettingsData();
        LoadFromSettings(defaultData);
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}

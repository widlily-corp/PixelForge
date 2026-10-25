using System.Text.Json;
using System.Text.Json.Serialization;
using PixelForge.Core.Localization;

namespace PixelForge.Core.Services;

/// <summary>
/// Data contract for serializable persistent application settings.
/// </summary>
public class AppSettingsData
{
    // General
    [JsonPropertyName("language")]
    public string Language { get; set; } = "Russian";

    [JsonPropertyName("isDarkTheme")]
    public bool IsDarkTheme { get; set; } = true;

    [JsonPropertyName("checkUpdatesOnStartup")]
    public bool CheckUpdatesOnStartup { get; set; } = true;

    // Canvas & Grid
    [JsonPropertyName("defaultCanvasWidth")]
    public int DefaultCanvasWidth { get; set; } = 32;

    [JsonPropertyName("defaultCanvasHeight")]
    public int DefaultCanvasHeight { get; set; } = 32;

    [JsonPropertyName("showPixelGrid")]
    public bool ShowPixelGrid { get; set; } = true;

    [JsonPropertyName("pixelGridMinZoom")]
    public double PixelGridMinZoom { get; set; } = 4.0;

    [JsonPropertyName("tileGridWidth")]
    public int TileGridWidth { get; set; } = 16;

    [JsonPropertyName("tileGridHeight")]
    public int TileGridHeight { get; set; } = 16;

    [JsonPropertyName("checkerSize")]
    public int CheckerSize { get; set; } = 16;

    [JsonPropertyName("colorCheckerLight")]
    public string ColorCheckerLight { get; set; } = "#2A2E40";

    [JsonPropertyName("colorCheckerDark")]
    public string ColorCheckerDark { get; set; } = "#1F2332";

    // Tools
    [JsonPropertyName("defaultStabilizerStrength")]
    public int DefaultStabilizerStrength { get; set; } = 0;

    [JsonPropertyName("defaultPixelPerfect")]
    public bool DefaultPixelPerfect { get; set; } = false;

    [JsonPropertyName("defaultBrushSize")]
    public int DefaultBrushSize { get; set; } = 1;

    // Autosave & History
    [JsonPropertyName("autosaveEnabled")]
    public bool AutosaveEnabled { get; set; } = true;

    [JsonPropertyName("autosaveIntervalMinutes")]
    public int AutosaveIntervalMinutes { get; set; } = 5;

    [JsonPropertyName("maxUndoSteps")]
    public int MaxUndoSteps { get; set; } = 100;

    // Performance
    [JsonPropertyName("preloadMemory")]
    public bool PreloadMemory { get; set; } = true;

    [JsonPropertyName("hardwareAcceleration")]
    public bool HardwareAcceleration { get; set; } = true;

    // Recent Projects
    [JsonPropertyName("recentProjects")]
    public List<string> RecentProjects { get; set; } = [];

    /// <summary>
    /// Creates a deep clone of the settings data.
    /// </summary>
    public AppSettingsData Clone()
    {
        return new AppSettingsData
        {
            Language = Language,
            IsDarkTheme = IsDarkTheme,
            CheckUpdatesOnStartup = CheckUpdatesOnStartup,
            DefaultCanvasWidth = DefaultCanvasWidth,
            DefaultCanvasHeight = DefaultCanvasHeight,
            ShowPixelGrid = ShowPixelGrid,
            PixelGridMinZoom = PixelGridMinZoom,
            TileGridWidth = TileGridWidth,
            TileGridHeight = TileGridHeight,
            CheckerSize = CheckerSize,
            ColorCheckerLight = ColorCheckerLight,
            ColorCheckerDark = ColorCheckerDark,
            DefaultStabilizerStrength = DefaultStabilizerStrength,
            DefaultPixelPerfect = DefaultPixelPerfect,
            DefaultBrushSize = DefaultBrushSize,
            AutosaveEnabled = AutosaveEnabled,
            AutosaveIntervalMinutes = AutosaveIntervalMinutes,
            MaxUndoSteps = MaxUndoSteps,
            PreloadMemory = PreloadMemory,
            HardwareAcceleration = HardwareAcceleration,
            RecentProjects = new List<string>(RecentProjects)
        };
    }
}

/// <summary>
/// Manages user configuration, persistent recent project history, and application preferences.
/// Adheres to transactional, atomic disk writes.
/// </summary>
public class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsFilePath;
    private AppSettingsData _data = new();

    /// <summary>
    /// Event raised whenever settings are saved or updated.
    /// </summary>
    public event EventHandler? SettingsChanged;

    /// <summary>
    /// Current raw settings snapshot.
    /// </summary>
    public AppSettingsData Data => _data;

    public AppLanguage Language
    {
        get => Enum.TryParse<AppLanguage>(_data.Language, true, out var lang) ? lang : AppLanguage.Russian;
        set
        {
            _data.Language = value.ToString();
            Save();
        }
    }

    public bool IsDarkTheme
    {
        get => _data.IsDarkTheme;
        set
        {
            _data.IsDarkTheme = value;
            Save();
        }
    }

    public bool CheckUpdatesOnStartup
    {
        get => _data.CheckUpdatesOnStartup;
        set
        {
            _data.CheckUpdatesOnStartup = value;
            Save();
        }
    }

    public int DefaultCanvasWidth
    {
        get => _data.DefaultCanvasWidth;
        set
        {
            _data.DefaultCanvasWidth = Math.Clamp(value, 1, 4096);
            Save();
        }
    }

    public int DefaultCanvasHeight
    {
        get => _data.DefaultCanvasHeight;
        set
        {
            _data.DefaultCanvasHeight = Math.Clamp(value, 1, 4096);
            Save();
        }
    }

    public bool ShowPixelGrid
    {
        get => _data.ShowPixelGrid;
        set
        {
            _data.ShowPixelGrid = value;
            Save();
        }
    }

    public double PixelGridMinZoom
    {
        get => _data.PixelGridMinZoom;
        set
        {
            _data.PixelGridMinZoom = Math.Clamp(value, 1.0, 32.0);
            Save();
        }
    }

    public int TileGridWidth
    {
        get => _data.TileGridWidth;
        set
        {
            _data.TileGridWidth = Math.Clamp(value, 1, 1024);
            Save();
        }
    }

    public int TileGridHeight
    {
        get => _data.TileGridHeight;
        set
        {
            _data.TileGridHeight = Math.Clamp(value, 1, 1024);
            Save();
        }
    }

    public int CheckerSize
    {
        get => _data.CheckerSize;
        set
        {
            _data.CheckerSize = Math.Clamp(value, 2, 256);
            Save();
        }
    }

    public string ColorCheckerLight
    {
        get => _data.ColorCheckerLight;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _data.ColorCheckerLight = value;
                Save();
            }
        }
    }

    public string ColorCheckerDark
    {
        get => _data.ColorCheckerDark;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _data.ColorCheckerDark = value;
                Save();
            }
        }
    }

    public int DefaultStabilizerStrength
    {
        get => _data.DefaultStabilizerStrength;
        set
        {
            _data.DefaultStabilizerStrength = Math.Clamp(value, 0, 10);
            Save();
        }
    }

    public bool DefaultPixelPerfect
    {
        get => _data.DefaultPixelPerfect;
        set
        {
            _data.DefaultPixelPerfect = value;
            Save();
        }
    }

    public int DefaultBrushSize
    {
        get => _data.DefaultBrushSize;
        set
        {
            _data.DefaultBrushSize = Math.Clamp(value, 1, 64);
            Save();
        }
    }

    public bool AutosaveEnabled
    {
        get => _data.AutosaveEnabled;
        set
        {
            _data.AutosaveEnabled = value;
            Save();
        }
    }

    public int AutosaveIntervalMinutes
    {
        get => _data.AutosaveIntervalMinutes;
        set
        {
            _data.AutosaveIntervalMinutes = Math.Clamp(value, 1, 60);
            Save();
        }
    }

    public int MaxUndoSteps
    {
        get => _data.MaxUndoSteps;
        set
        {
            _data.MaxUndoSteps = Math.Clamp(value, 10, 500);
            Save();
        }
    }

    public bool PreloadMemory
    {
        get => _data.PreloadMemory;
        set
        {
            _data.PreloadMemory = value;
            Save();
        }
    }

    public bool HardwareAcceleration
    {
        get => _data.HardwareAcceleration;
        set
        {
            _data.HardwareAcceleration = value;
            Save();
        }
    }

    public IReadOnlyList<string> RecentProjects => _data.RecentProjects.AsReadOnly();

    public AppSettingsService(string? customSettingsFilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customSettingsFilePath))
        {
            _settingsFilePath = customSettingsFilePath;
        }
        else
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PixelForge");
            _settingsFilePath = Path.Combine(appDataDir, "settings.json");
        }

        Load();
    }

    /// <summary>
    /// Applies a complete settings snapshot and persists to disk.
    /// </summary>
    public void ApplySettings(AppSettingsData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data.Clone();
        Save();
    }

    /// <summary>
    /// Resets all settings to default factory values, optionally preserving recent projects list.
    /// </summary>
    public void ResetToDefaults(bool preserveRecentProjects = true)
    {
        var recents = preserveRecentProjects ? new List<string>(_data.RecentProjects) : new List<string>();
        _data = new AppSettingsData
        {
            RecentProjects = recents
        };
        Save();
    }

    /// <summary>
    /// Adds a project file path to the top of the recent projects list, pruning duplicates and limiting to 10 entries.
    /// </summary>
    public void AddRecentProject(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        var fullPath = Path.GetFullPath(filePath);
        _data.RecentProjects.RemoveAll(p => string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase));
        _data.RecentProjects.Insert(0, fullPath);

        if (_data.RecentProjects.Count > 10)
        {
            _data.RecentProjects.RemoveRange(10, _data.RecentProjects.Count - 10);
        }

        Save();
    }

    /// <summary>
    /// Removes a specified project path from the recent list.
    /// </summary>
    public void RemoveRecentProject(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        var fullPath = Path.GetFullPath(filePath);
        if (_data.RecentProjects.RemoveAll(p => string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            Save();
        }
    }

    /// <summary>
    /// Clears all stored recent projects.
    /// </summary>
    public void ClearRecentProjects()
    {
        if (_data.RecentProjects.Count > 0)
        {
            _data.RecentProjects.Clear();
            Save();
        }
    }

    /// <summary>
    /// Reads settings from disk or initializes defaults if not present.
    /// </summary>
    public void Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettingsData>(json, JsonOptions);
                if (loaded != null)
                {
                    // Sanitize values in case of corrupted or out-of-range config
                    if (loaded.DefaultCanvasWidth <= 0) loaded.DefaultCanvasWidth = 32;
                    if (loaded.DefaultCanvasHeight <= 0) loaded.DefaultCanvasHeight = 32;
                    if (loaded.TileGridWidth <= 0) loaded.TileGridWidth = 16;
                    if (loaded.TileGridHeight <= 0) loaded.TileGridHeight = 16;
                    if (loaded.CheckerSize <= 0) loaded.CheckerSize = 16;
                    if (string.IsNullOrWhiteSpace(loaded.ColorCheckerLight)) loaded.ColorCheckerLight = "#2A2E40";
                    if (string.IsNullOrWhiteSpace(loaded.ColorCheckerDark)) loaded.ColorCheckerDark = "#1F2332";
                    if (loaded.PixelGridMinZoom < 1.0) loaded.PixelGridMinZoom = 4.0;
                    if (loaded.DefaultBrushSize <= 0) loaded.DefaultBrushSize = 1;
                    if (loaded.AutosaveIntervalMinutes <= 0) loaded.AutosaveIntervalMinutes = 5;
                    if (loaded.MaxUndoSteps <= 0) loaded.MaxUndoSteps = 100;
                    loaded.RecentProjects ??= [];

                    _data = loaded;
                    return;
                }
            }
        }
        catch
        {
            // Fall back to clean defaults if corrupted
        }

        _data = new AppSettingsData();
    }

    /// <summary>
    /// Performs an atomic, transactional save to disk using a temporary file.
    /// </summary>
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tempPath = _settingsFilePath + ".tmp";
            var json = JsonSerializer.Serialize(_data, JsonOptions);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _settingsFilePath, overwrite: true);

            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Suppress non-critical file I/O exceptions on shutdown/portable runs
        }
    }
}

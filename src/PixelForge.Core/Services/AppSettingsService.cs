using System.Text.Json;
using System.Text.Json.Serialization;
using PixelForge.Core.Localization;

namespace PixelForge.Core.Services;

/// <summary>
/// Data contract for serializable persistent application settings.
/// </summary>
public class AppSettingsData
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "Russian";

    [JsonPropertyName("isDarkTheme")]
    public bool IsDarkTheme { get; set; } = true;

    [JsonPropertyName("recentProjects")]
    public List<string> RecentProjects { get; set; } = [];
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
        }
        catch
        {
            // Suppress non-critical file I/O exceptions on shutdown/portable runs
        }
    }
}

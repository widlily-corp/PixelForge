using System.Text.Json.Serialization;

namespace PixelForge.App.Services;

/// <summary>
/// Model representing the manifest downloaded from latest.json.
/// </summary>
public class UpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("releaseDate")]
    public DateTimeOffset? ReleaseDate { get; set; }

    [JsonPropertyName("releaseNotes")]
    public string ReleaseNotes { get; set; } = string.Empty;

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;
}

/// <summary>
/// Result of an update check operation.
/// </summary>
public class UpdateCheckResult
{
    public bool Success { get; init; }
    public bool IsUpdateAvailable { get; init; }
    public string CurrentVersion { get; init; } = "0.0.1";
    public string? LatestVersion { get; init; }
    public string? ReleaseNotes { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Defines OTA update checking and navigation service.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Current running application version.
    /// </summary>
    string CurrentVersion { get; }

    /// <summary>
    /// Checks GitHub Releases / latest.json manifest for newer versions.
    /// </summary>
    /// <param name="silent">If true, suppress non-critical network errors.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<UpdateCheckResult> CheckForUpdatesAsync(bool silent = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the release download page or URL in the default browser.
    /// </summary>
    void OpenDownloadPage(string url);

    /// <summary>
    /// Downloads the latest installer executable with progress reporting and initiates the automated silent upgrade.
    /// </summary>
    Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}

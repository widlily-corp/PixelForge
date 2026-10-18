using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace PixelForge.App.Services;

/// <summary>
/// Implements update checking against GitHub Releases latest.json manifest.
/// </summary>
public class UpdateService : IUpdateService
{
    public const string DefaultManifestUrl = "https://raw.githubusercontent.com/Widlily/PixelForge/main/latest.json";

    private readonly HttpClient _httpClient;
    private readonly string _manifestUrl;

    public string CurrentVersion => "0.0.1";

    public UpdateService(HttpClient? httpClient = null, string? manifestUrl = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _manifestUrl = manifestUrl ?? DefaultManifestUrl;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"PixelForge/{CurrentVersion}");
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(bool silent = true, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var manifest = await _httpClient.GetFromJsonAsync<UpdateManifest>(_manifestUrl, timeoutCts.Token).ConfigureAwait(false);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                return new UpdateCheckResult
                {
                    Success = false,
                    CurrentVersion = CurrentVersion,
                    ErrorMessage = "Invalid update manifest received."
                };
            }

            bool isNewer = IsRemoteVersionNewer(manifest.Version, CurrentVersion);

            return new UpdateCheckResult
            {
                Success = true,
                IsUpdateAvailable = isNewer,
                CurrentVersion = CurrentVersion,
                LatestVersion = manifest.Version,
                ReleaseNotes = manifest.ReleaseNotes,
                DownloadUrl = manifest.DownloadUrl
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                Success = false,
                CurrentVersion = CurrentVersion,
                ErrorMessage = silent ? null : $"Failed to check for updates: {ex.Message}"
            };
        }
    }

    public void OpenDownloadPage(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch
        {
            // Silently ignore browser launch failures
        }
    }

    private static bool IsRemoteVersionNewer(string remoteVersion, string localVersion)
    {
        string cleanRemote = remoteVersion.TrimStart('v', 'V');
        string cleanLocal = localVersion.TrimStart('v', 'V');

        if (Version.TryParse(cleanRemote, out var remoteParsed) && Version.TryParse(cleanLocal, out var localParsed))
        {
            return remoteParsed > localParsed;
        }

        return string.Compare(cleanRemote, cleanLocal, StringComparison.OrdinalIgnoreCase) > 0;
    }
}

using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace PixelForge.App.Services;

/// <summary>
/// Implements update checking against GitHub Releases latest.json manifest.
/// </summary>
public class UpdateService : IUpdateService
{
    public const string DefaultManifestUrl = "https://raw.githubusercontent.com/widlily-corp/PixelForge/main/latest.json";

    private readonly HttpClient _httpClient;
    private readonly string _manifestUrl;

    public string CurrentVersion => "0.1.3";

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

    /// <summary>
    /// Downloads the latest installer executable with progress reporting and initiates the automated silent upgrade.
    /// </summary>
    public async Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return false;
        }

        try
        {
            string tempDir = Path.GetTempPath();
            string fileName = $"PixelForge_Update_{Guid.NewGuid():N}.exe";
            string installerPath = Path.Combine(tempDir, fileName);

            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;

                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var fileStream = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

                byte[] buffer = new byte[81920];
                long totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                    totalBytesRead += bytesRead;

                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        double percentage = (double)totalBytesRead / totalBytes.Value;
                        progress?.Report(Math.Clamp(percentage, 0.0, 1.0));
                    }
                }
            }

            progress?.Report(1.0);

            // Execute Inno Setup installer silently and restart application
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true
            };

            Process.Start(psi);

            // Exit this process so Inno Setup can update files cleanly
            Environment.Exit(0);
            return true;
        }
        catch (Exception)
        {
            return false;
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

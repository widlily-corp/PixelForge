using PixelForge.Core.Models;

namespace PixelForge.Core.Services;

/// <summary>
/// Background service that periodically saves project snapshots to AppData/PixelForge/autosave.pxproj.
/// </summary>
public class AutosaveService : IAutosaveService
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(2);

    private readonly IProjectService _projectService;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private Func<Project?>? _projectProvider;
    private CancellationTokenSource? _cts;
    private Task? _timerTask;
    private bool _disposed;

    public string AutosaveFilePath { get; }

    public bool IsRunning => _timerTask != null && !_timerTask.IsCompleted;

    public bool HasEmergencyCopy => File.Exists(AutosaveFilePath);

    public event EventHandler<DateTime>? Autosaved;
    public event EventHandler<Exception>? AutosaveFailed;

    public AutosaveService(IProjectService projectService, string? customAutosavePath = null)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));

        if (!string.IsNullOrWhiteSpace(customAutosavePath))
        {
            AutosaveFilePath = customAutosavePath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            AutosaveFilePath = Path.Combine(appData, "PixelForge", "autosave.pxproj");
        }
    }

    public void Start(Func<Project?> projectProvider, TimeSpan? interval = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(projectProvider);

        Stop();

        _projectProvider = projectProvider;
        var period = interval ?? DefaultInterval;
        if (period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero.");
        }

        _cts = new CancellationTokenSource();
        _timerTask = RunPeriodicLoopAsync(period, _cts.Token);
    }

    public void Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            try
            {
                _timerTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException) { /* Expected when cancelled */ }
            catch (OperationCanceledException) { /* Expected */ }

            _cts.Dispose();
            _cts = null;
            _timerTask = null;
        }

        _projectProvider = null;
    }

    public async Task TriggerSaveAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await SaveSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Project?> LoadEmergencyCopyAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!HasEmergencyCopy)
        {
            return null;
        }

        return await _projectService.LoadProjectAsync(AutosaveFilePath, cancellationToken).ConfigureAwait(false);
    }

    public void DeleteEmergencyCopy()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (File.Exists(AutosaveFilePath))
        {
            try
            {
                File.Delete(AutosaveFilePath);
            }
            catch (Exception ex)
            {
                AutosaveFailed?.Invoke(this, ex);
            }
        }
    }

    private async Task RunPeriodicLoopAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                {
                    break;
                }

                await SaveSnapshotAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                AutosaveFailed?.Invoke(this, ex);
            }
        }
    }

    private async Task SaveSnapshotAsync(CancellationToken cancellationToken)
    {
        if (_projectProvider == null)
        {
            return;
        }

        Project? project = null;
        try
        {
            project = _projectProvider();
        }
        catch (Exception ex)
        {
            AutosaveFailed?.Invoke(this, ex);
            return;
        }

        if (project == null)
        {
            return;
        }

        bool entered = await _saveLock.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        if (!entered)
        {
            return;
        }

        try
        {
            await _projectService.SaveProjectAsync(project, AutosaveFilePath, cancellationToken).ConfigureAwait(false);
            Autosaved?.Invoke(this, DateTime.UtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            AutosaveFailed?.Invoke(this, ex);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _saveLock.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _saveLock.Dispose();
        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }
}

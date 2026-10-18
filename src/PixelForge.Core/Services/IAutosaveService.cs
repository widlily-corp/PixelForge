using PixelForge.Core.Models;

namespace PixelForge.Core.Services;

/// <summary>
/// Provides background periodic autosave functionality and crash recovery management.
/// </summary>
public interface IAutosaveService : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the full path to the autosave file in AppData.
    /// </summary>
    string AutosaveFilePath { get; }

    /// <summary>
    /// Indicates whether the autosave service is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Indicates whether an emergency recovery copy exists on disk.
    /// </summary>
    bool HasEmergencyCopy { get; }

    /// <summary>
    /// Raised whenever an autosave succeeds.
    /// </summary>
    event EventHandler<DateTime>? Autosaved;

    /// <summary>
    /// Raised whenever an autosave fails.
    /// </summary>
    event EventHandler<Exception>? AutosaveFailed;

    /// <summary>
    /// Starts the background autosave timer.
    /// </summary>
    void Start(Func<Project?> projectProvider, TimeSpan? interval = null);

    /// <summary>
    /// Stops the background autosave timer.
    /// </summary>
    void Stop();

    /// <summary>
    /// Manually triggers an autosave cycle immediately.
    /// </summary>
    Task TriggerSaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the emergency copy if one exists.
    /// </summary>
    Task<Project?> LoadEmergencyCopyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the emergency copy file.
    /// </summary>
    void DeleteEmergencyCopy();
}

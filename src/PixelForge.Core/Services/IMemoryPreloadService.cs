namespace PixelForge.Core.Services;

/// <summary>
/// Status and percentage update during application preloading.
/// </summary>
/// <param name="Percentage">Current overall percentage (0 to 100).</param>
/// <param name="StageName">High-level stage description (e.g. "Preloading RAM Working Set").</param>
/// <param name="Details">Fine-grained details (e.g. specific assembly or resource being touched).</param>
public readonly record struct PreloadProgress(int Percentage, string StageName, string Details);

/// <summary>
/// Service responsible for preloading assemblies, locking working set into physical RAM,
/// pre-JITing hot path methods, and warming up the SkiaSharp graphics pipeline.
/// </summary>
public interface IMemoryPreloadService
{
    /// <summary>
    /// Gets whether the memory preload and warm-up has already completed.
    /// </summary>
    bool IsPreloaded { get; }

    /// <summary>
    /// Asynchronously preloads runtime assemblies, faults all code/data pages into physical RAM,
    /// compiles hot-path methods via JIT, and warms up graphical surfaces.
    /// </summary>
    /// <param name="progress">Optional progress reporter for splash screen updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PreloadAsync(IProgress<PreloadProgress>? progress = null, CancellationToken cancellationToken = default);
}

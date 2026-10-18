namespace PixelForge.App.Services;

/// <summary>
/// Summary details for a recorded crash dump.
/// </summary>
public record CrashReportInfo(
    string DumpFilePath,
    string ReportFilePath,
    DateTime Timestamp,
    string ExceptionType,
    string Message);

/// <summary>
/// Contract for creating native Windows minidumps and structured diagnostic crash reports.
/// </summary>
public interface ICrashReportService
{
    /// <summary>
    /// Gets the absolute directory where crash dumps and reports are stored.
    /// </summary>
    string CrashDirectory { get; }

    /// <summary>
    /// Captures a native process minidump (.dmp) and a structured diagnostic report (.json).
    /// </summary>
    /// <param name="exception">Optional exception that caused the crash.</param>
    /// <param name="context">Context description (e.g. subsystem or operation name).</param>
    /// <param name="isTerminating">Whether the application is about to terminate.</param>
    /// <returns>Information about the captured crash report, or null if capture failed.</returns>
    CrashReportInfo? CaptureCrash(Exception? exception = null, string? context = null, bool isTerminating = false);

    /// <summary>
    /// Enumerates all saved crash reports ordered by date descending.
    /// </summary>
    IReadOnlyList<CrashReportInfo> GetCrashReports();

    /// <summary>
    /// Removes older crash dumps, retaining only the most recent entries.
    /// </summary>
    /// <param name="maxRetained">Maximum number of recent dumps to keep (default: 10).</param>
    void PruneOldReports(int maxRetained = 10);
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace PixelForge.App.Services;

/// <summary>
/// Industrial crash diagnostic service that captures Windows native minidumps (.dmp)
/// via DbgHelp.dll and generates structured JSON telemetry upon critical failures.
/// </summary>
public class CrashReportService : ICrashReportService
{
    private static readonly Lazy<CrashReportService> _instance = new(() => new CrashReportService());
    public static CrashReportService Instance => _instance.Value;

    private readonly object _syncRoot = new();
    private readonly string _crashDirectory;

    public string CrashDirectory => _crashDirectory;

    #region Win32 DbgHelp Interop

    [Flags]
    private enum MINIDUMP_TYPE : uint
    {
        MiniDumpNormal = 0x00000000,
        MiniDumpWithDataSegs = 0x00000001,
        MiniDumpWithFullMemory = 0x00000002,
        MiniDumpWithHandleData = 0x00000004,
        MiniDumpFilterMemory = 0x00000008,
        MiniDumpScanMemory = 0x00000010,
        MiniDumpWithUnloadedModules = 0x00000020,
        MiniDumpWithIndirectlyReferencedMemory = 0x00000040,
        MiniDumpFilterModulePaths = 0x00000080,
        MiniDumpWithProcessThreadData = 0x00000100,
        MiniDumpWithPrivateReadWriteMemory = 0x00000200,
        MiniDumpWithoutOptionalData = 0x00000400,
        MiniDumpWithFullMemoryInfo = 0x00000800,
        MiniDumpWithThreadInfo = 0x00001000,
        MiniDumpWithCodeSegs = 0x00002000
    }

    [DllImport("dbghelp.dll", EntryPoint = "MiniDumpWriteDump", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern bool MiniDumpWriteDump(
        IntPtr hProcess,
        uint processId,
        SafeFileHandle hFile,
        uint dumpType,
        IntPtr expParam,
        IntPtr userStreamParam,
        IntPtr callbackParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    #endregion

    public CrashReportService(string? customCrashDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(customCrashDirectory))
        {
            _crashDirectory = customCrashDirectory;
        }
        else
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _crashDirectory = Path.Combine(localAppData, "PixelForge", "Crashes");
        }

        try
        {
            Directory.CreateDirectory(_crashDirectory);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("CrashReport", $"Failed to create crash directory at '{_crashDirectory}'", ex);
        }
    }

    public CrashReportInfo? CaptureCrash(Exception? exception = null, string? context = null, bool isTerminating = false)
    {
        lock (_syncRoot)
        {
            try
            {
                DateTime now = DateTime.Now;
                string timestampStr = now.ToString("yyyyMMdd_HHmmss");
                string filePrefix = $"crash_{timestampStr}_{Guid.NewGuid():N}";
                string dumpPath = Path.Combine(_crashDirectory, $"{filePrefix}.dmp");
                string reportPath = Path.Combine(_crashDirectory, $"{filePrefix}.json");

                // 1. Generate Native MiniDump if running on Windows
                bool dumpSucceeded = false;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    dumpSucceeded = WriteNativeMiniDump(dumpPath);
                }

                // 2. Generate Structured Diagnostic JSON Telemetry
                var telemetry = BuildCrashTelemetry(now, exception, context, isTerminating, dumpSucceeded ? dumpPath : null);
                string json = JsonSerializer.Serialize(telemetry, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(reportPath, json);

                AppLogger.LogFatal("CrashReport", $"Crash captured! Dump: {dumpPath}, Report: {reportPath}, Terminating={isTerminating}", exception);

                // 3. Keep directory bounded
                PruneOldReports(10);

                return new CrashReportInfo(
                    DumpFilePath: dumpSucceeded ? dumpPath : string.Empty,
                    ReportFilePath: reportPath,
                    Timestamp: now,
                    ExceptionType: exception?.GetType().FullName ?? "NativeOrUnspecified",
                    Message: exception?.Message ?? context ?? "No exception details available");
            }
            catch (Exception ex)
            {
                AppLogger.LogFatal("CrashReport", "Fatal error during crash capture routine", ex);
                return null;
            }
        }
    }

    private static bool WriteNativeMiniDump(string dumpFilePath)
    {
        try
        {
            using var fileStream = new FileStream(dumpFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            uint dumpType = (uint)(MINIDUMP_TYPE.MiniDumpNormal |
                                  MINIDUMP_TYPE.MiniDumpWithIndirectlyReferencedMemory |
                                  MINIDUMP_TYPE.MiniDumpWithThreadInfo |
                                  MINIDUMP_TYPE.MiniDumpWithUnloadedModules);

            bool success = MiniDumpWriteDump(
                GetCurrentProcess(),
                GetCurrentProcessId(),
                fileStream.SafeFileHandle,
                dumpType,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (!success)
            {
                int error = Marshal.GetLastWin32Error();
                AppLogger.LogError("CrashReport", $"MiniDumpWriteDump failed with Win32 error code: {error}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("CrashReport", "Failed to write native minidump", ex);
            return false;
        }
    }

    private static CrashTelemetry BuildCrashTelemetry(
        DateTime timestamp,
        Exception? exception,
        string? context,
        bool isTerminating,
        string? dumpFilePath)
    {
        using var process = Process.GetCurrentProcess();

        return new CrashTelemetry
        {
            Timestamp = timestamp.ToString("o"),
            Context = context ?? "Application Execution",
            IsTerminating = isTerminating,
            DumpFile = dumpFilePath,
            OperatingSystem = Environment.OSVersion.ToString(),
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            DotNetRuntime = RuntimeInformation.FrameworkDescription,
            ProcessId = process.Id,
            WorkingSetMb = process.WorkingSet64 / (1024 * 1024),
            PeakWorkingSetMb = process.PeakWorkingSet64 / (1024 * 1024),
            TotalAllocatedBytes = GC.GetTotalAllocatedBytes(),
            ThreadCount = process.Threads.Count,
            Exception = exception != null ? new ExceptionTelemetry
            {
                Type = exception.GetType().FullName ?? "Unknown",
                Message = exception.Message,
                HResult = $"0x{exception.HResult:X8}",
                StackTrace = exception.StackTrace,
                InnerException = exception.InnerException != null ? new ExceptionTelemetry
                {
                    Type = exception.InnerException.GetType().FullName ?? "Unknown",
                    Message = exception.InnerException.Message,
                    HResult = $"0x{exception.InnerException.HResult:X8}",
                    StackTrace = exception.InnerException.StackTrace
                } : null
            } : null
        };
    }

    public IReadOnlyList<CrashReportInfo> GetCrashReports()
    {
        lock (_syncRoot)
        {
            try
            {
                if (!Directory.Exists(_crashDirectory))
                {
                    return Array.Empty<CrashReportInfo>();
                }

                var files = Directory.GetFiles(_crashDirectory, "*.json");
                var list = new List<CrashReportInfo>(files.Length);

                foreach (var file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        string dumpPath = root.TryGetProperty("DumpFile", out var d) ? d.GetString() ?? "" : "";
                        DateTime time = root.TryGetProperty("Timestamp", out var t) && DateTime.TryParse(t.GetString(), out var dt) ? dt : File.GetCreationTime(file);
                        string exType = "Unknown";
                        string msg = "";

                        if (root.TryGetProperty("Exception", out var exObj) && exObj.ValueKind == JsonValueKind.Object)
                        {
                            if (exObj.TryGetProperty("Type", out var tProp)) exType = tProp.GetString() ?? "Unknown";
                            if (exObj.TryGetProperty("Message", out var mProp)) msg = mProp.GetString() ?? "";
                        }

                        list.Add(new CrashReportInfo(dumpPath, file, time, exType, msg));
                    }
                    catch
                    {
                        // Ignore corrupt individual report files
                    }
                }

                list.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
                return list;
            }
            catch
            {
                return Array.Empty<CrashReportInfo>();
            }
        }
    }

    public void PruneOldReports(int maxRetained = 10)
    {
        try
        {
            if (!Directory.Exists(_crashDirectory))
            {
                return;
            }

            var dirInfo = new DirectoryInfo(_crashDirectory);
            var files = dirInfo.GetFiles("crash_*")
                               .OrderByDescending(f => f.CreationTimeUtc)
                               .ToList();

            // Each report has up to 2 files (.dmp and .json)
            int fileLimit = Math.Max(2, maxRetained * 2);
            if (files.Count > fileLimit)
            {
                foreach (var oldFile in files.Skip(fileLimit))
                {
                    try
                    {
                        oldFile.Delete();
                    }
                    catch
                    {
                        // Ignore deletion lock
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("CrashReport", "Failed to prune old crash reports", ex);
        }
    }

    private class CrashTelemetry
    {
        public string Timestamp { get; set; } = "";
        public string Context { get; set; } = "";
        public bool IsTerminating { get; set; }
        public string? DumpFile { get; set; }
        public string OperatingSystem { get; set; } = "";
        public string Architecture { get; set; } = "";
        public string DotNetRuntime { get; set; } = "";
        public int ProcessId { get; set; }
        public long WorkingSetMb { get; set; }
        public long PeakWorkingSetMb { get; set; }
        public long TotalAllocatedBytes { get; set; }
        public int ThreadCount { get; set; }
        public ExceptionTelemetry? Exception { get; set; }
    }

    private class ExceptionTelemetry
    {
        public string Type { get; set; } = "";
        public string Message { get; set; } = "";
        public string HResult { get; set; } = "";
        public string? StackTrace { get; set; }
        public ExceptionTelemetry? InnerException { get; set; }
    }
}

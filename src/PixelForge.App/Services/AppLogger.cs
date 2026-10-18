using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace PixelForge.App.Services;

public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error,
    Fatal
}

/// <summary>
/// High-reliability, thread-safe file logger that writes diagnostics and crash traces
/// to 'debug.log' adjacent to the application executable with fallback to LocalAppData.
/// </summary>
public static class AppLogger
{
    private static readonly object _syncRoot = new();
    private static StreamWriter? _writer;
    private static string? _logFilePath;
    private static bool _isInitialized;

    public static string LogFilePath => _logFilePath ?? "debug.log";

    /// <summary>
    /// Initializes the logging subsystem, hooks global unhandled exceptions, and writes system diagnostic banner.
    /// </summary>
    public static void Initialize()
    {
        lock (_syncRoot)
        {
            if (_isInitialized)
            {
                return;
            }

            try
            {
                string baseDir = AppContext.BaseDirectory;
                string primaryPath = Path.Combine(baseDir, "debug.log");

                // Check if directory is writable (e.g. not read-only Program Files)
                if (IsDirectoryWritable(baseDir))
                {
                    _logFilePath = primaryPath;
                }
                else
                {
                    string fallbackDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelForge");
                    Directory.CreateDirectory(fallbackDir);
                    _logFilePath = Path.Combine(fallbackDir, "debug.log");
                }

                // Append or create new debug.log with immediate autoflush
                var stream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(stream) { AutoFlush = true };

                _isInitialized = true;

                // Write session startup banner
                LogBanner();

                // Wire global unhandled exception hooks
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                // Wire Trace listener for Avalonia internal diagnostics
                Trace.Listeners.Add(new DebugLogTraceListener());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppLogger] Failed to initialize logger: {ex}");
            }
        }
    }

    private static bool IsDirectoryWritable(string path)
    {
        try
        {
            string testFile = Path.Combine(path, $".test_write_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void LogBanner()
    {
        string sep = new('=', 80);
        WriteLine(sep);
        WriteLine($" PixelForge Diagnostics Log Session Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        WriteLine(sep);
        WriteLine($"  Process ID:       {Process.GetCurrentProcess().Id}");
        WriteLine($"  App Base Dir:     {AppContext.BaseDirectory}");
        WriteLine($"  Log File:         {_logFilePath}");
        WriteLine($"  OS Version:       {Environment.OSVersion} ({RuntimeInformation.OSArchitecture})");
        WriteLine($"  .NET Runtime:     {RuntimeInformation.FrameworkDescription}");
        WriteLine($"  Working Set:      {Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024)} MB");
        WriteLine(sep);
    }

    public static void LogDebug(string category, string message) => Log(LogLevel.Debug, category, message);

    public static void LogInfo(string category, string message) => Log(LogLevel.Info, category, message);

    public static void LogWarn(string category, string message) => Log(LogLevel.Warn, category, message);

    public static void LogError(string category, string message, Exception? ex = null) => Log(LogLevel.Error, category, message, ex);

    public static void LogFatal(string category, string message, Exception? ex = null) => Log(LogLevel.Fatal, category, message, ex);

    public static void Log(LogLevel level, string category, string message, Exception? ex = null)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string levelStr = level.ToString().ToUpperInvariant().PadRight(5);
        int threadId = Environment.CurrentManagedThreadId;

        string formatted = $"[{timestamp}] [{levelStr}] [T:{threadId:D2}] [{category}] {message}";

        lock (_syncRoot)
        {
            WriteLine(formatted);
            if (ex != null)
            {
                WriteLine($"Exception Type:    {ex.GetType().FullName}");
                WriteLine($"Exception Message: {ex.Message}");
                WriteLine($"StackTrace:\n{ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    WriteLine($"---> Inner Exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                    WriteLine($"StackTrace:\n{ex.InnerException.StackTrace}");
                }
            }
        }

        // Also echo to console/debug output
        Console.WriteLine(formatted);
        if (ex != null)
        {
            Console.WriteLine(ex);
        }
    }

    private static void WriteLine(string text)
    {
        try
        {
            _writer?.WriteLine(text);
        }
        catch
        {
            // Silently survive write failures
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        LogFatal("CrashHandler", $"Unhandled AppDomain Exception encountered! IsTerminating={e.IsTerminating}", ex);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogError("CrashHandler", "Unobserved Task Exception encountered!", e.Exception);
        e.SetObserved();
    }

    private class DebugLogTraceListener : TraceListener
    {
        public override void Write(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                LogDebug("Trace", message.TrimEnd());
            }
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                LogDebug("Trace", message.TrimEnd());
            }
        }
    }
}

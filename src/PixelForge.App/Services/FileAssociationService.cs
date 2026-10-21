using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace PixelForge.App.Services;

/// <summary>
/// Manages Windows Shell file associations and 'Open With...' registration for .pxproj files
/// in the user's registry hive (HKEY_CURRENT_USER) without requiring administrator elevation.
/// </summary>
public static class FileAssociationService
{
    private const string Extension = ".pxproj";
    private const string ProgId = "PixelForge.Project";
    private const string FriendlyDocName = "PixelForge Project";

    /// <summary>
    /// Gets whether the current operating system is Windows and supports shell registration.
    /// </summary>
    public static bool IsWindows => OperatingSystem.IsWindows();

    /// <summary>
    /// Registers .pxproj file association and 'Open With...' shell handler for the current user.
    /// </summary>
    /// <returns>True if registration succeeded; otherwise, false.</returns>
    public static bool RegisterAssociation()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                return false;
            }

            var exeName = Path.GetFileName(exePath);
            var commandValue = $"\"{exePath}\" \"%1\"";
            var iconValue = $"\"{exePath}\",0";

            // 1. HKCU\Software\Classes\.pxproj
            using (var extKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Extension))
            {
                extKey.SetValue(string.Empty, ProgId);
                extKey.SetValue("Content Type", "application/x-pixelforge");

                using var openWithKey = extKey.CreateSubKey("OpenWithProgids");
                openWithKey.SetValue(ProgId, string.Empty);
            }

            // 2. HKCU\Software\Classes\PixelForge.Project
            using (var progKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId))
            {
                progKey.SetValue(string.Empty, FriendlyDocName);

                using var iconKey = progKey.CreateSubKey("DefaultIcon");
                iconKey.SetValue(string.Empty, iconValue);

                using var shellKey = progKey.CreateSubKey(@"shell\open\command");
                shellKey.SetValue(string.Empty, commandValue);
            }

            // 3. HKCU\Software\Classes\Applications\{exeName}
            using (var appKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\" + exeName))
            {
                appKey.SetValue("FriendlyAppName", "PixelForge");

                using var suppKey = appKey.CreateSubKey("SupportedTypes");
                suppKey.SetValue(Extension, string.Empty);

                using var appCmdKey = appKey.CreateSubKey(@"shell\open\command");
                appCmdKey.SetValue(string.Empty, commandValue);
            }

            // 4. Notify Windows Shell of file association changes
            NotifyShell();

            AppLogger.LogInfo("FileAssociation", $"Successfully registered {Extension} association for {exePath}.");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("FileAssociation", "Failed to register Windows file association.", ex);
            return false;
        }
    }

    /// <summary>
    /// Checks whether .pxproj file association is currently registered for PixelForge in HKCU.
    /// </summary>
    public static bool IsAssociationRegistered()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            using var extKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + Extension);
            if (extKey == null) return false;

            var val = extKey.GetValue(string.Empty) as string;
            return string.Equals(val, ProgId, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void NotifyShell()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            // SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero)
            NativeMethods.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
            // Best effort notification
        }
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}

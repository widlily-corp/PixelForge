using System.Reflection;

namespace PixelForge.Core;

/// <summary>
/// Provides unified access to application version information compiled into assemblies.
/// </summary>
public static class AppVersionInfo
{
    private static readonly Lazy<string> _currentVersion = new(() =>
    {
        var assembly = typeof(AppVersionInfo).Assembly;
        var version = assembly.GetName().Version;
        if (version != null && version.Major >= 0)
        {
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            var clean = infoVersion.Split('+')[0].TrimStart('v', 'V');
            if (!string.IsNullOrWhiteSpace(clean))
            {
                return clean;
            }
        }

        return "0.1.0";
    });

    /// <summary>
    /// Gets the current semantic application version (e.g., "0.1.3").
    /// </summary>
    public static string CurrentVersion => _currentVersion.Value;

    /// <summary>
    /// Gets the formatted version string with leading 'v' (e.g., "v0.1.3").
    /// </summary>
    public static string FullVersionString => $"v{CurrentVersion}";
}

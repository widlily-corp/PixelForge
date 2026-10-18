using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PixelForge.App.Services;

/// <summary>
/// Service managing runtime theme switching between Dark and Light themes.
/// </summary>
public partial class ThemeService : ObservableObject
{
    private bool _isDarkTheme = true;

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        private set => SetProperty(ref _isDarkTheme, value);
    }

    public event EventHandler<bool>? ThemeChanged;

    public ThemeService()
    {
        // Default to Dark theme
        ApplyTheme(true);
    }

    public void SetDarkTheme() => ApplyTheme(true);

    public void SetLightTheme() => ApplyTheme(false);

    public void ToggleTheme() => ApplyTheme(!_isDarkTheme);

    private void ApplyTheme(bool isDark)
    {
        IsDarkTheme = isDark;
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        ThemeChanged?.Invoke(this, isDark);
    }
}

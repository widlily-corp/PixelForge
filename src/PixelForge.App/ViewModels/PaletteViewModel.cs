using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelForge.Core.Models;
using PixelForge.Core.Palettes;

namespace PixelForge.App.ViewModels;

/// <summary>
/// ViewModel managing primary/secondary active colors, current color palette, and palette presets.
/// </summary>
public partial class PaletteViewModel : ViewModelBase
{
    [ObservableProperty]
    private PixelColor _primaryColor = PixelColor.Black;

    [ObservableProperty]
    private PixelColor _secondaryColor = PixelColor.White;

    [ObservableProperty]
    private Palette _currentPalette;

    public ObservableCollection<PixelColor> Colors { get; } = new();

    public IReadOnlyList<Palette> Presets => PalettePresets.AllPresets;

    /// <summary>
    /// Occurs when a palette operation requests a status feedback message.
    /// </summary>
    public event EventHandler<string>? StatusRequested;

    public PaletteViewModel()
    {
        _currentPalette = PalettePresets.Default32.Clone();
        SyncColorsFromPalette();

        if (Colors.Count > 0)
        {
            PrimaryColor = Colors[0];
            SecondaryColor = Colors.Count > 1 ? Colors[1] : PixelColor.White;
        }
    }

    [RelayCommand]
    public void SwapColors()
    {
        (PrimaryColor, SecondaryColor) = (SecondaryColor, PrimaryColor);
        StatusRequested?.Invoke(this, "Colors swapped (X)");
    }

    [RelayCommand]
    public void SelectNextColor()
    {
        if (Colors.Count == 0) return;
        int index = Colors.IndexOf(PrimaryColor);
        int nextIndex = index >= 0 ? (index + 1) % Colors.Count : 0;
        SelectPrimaryColor(Colors[nextIndex]);
        StatusRequested?.Invoke(this, $"Palette: {Colors[nextIndex].ToHex(Colors[nextIndex].A < 255)} (key .)");
    }

    [RelayCommand]
    public void SelectPreviousColor()
    {
        if (Colors.Count == 0) return;
        int index = Colors.IndexOf(PrimaryColor);
        int prevIndex = index > 0 ? index - 1 : Colors.Count - 1;
        SelectPrimaryColor(Colors[prevIndex]);
        StatusRequested?.Invoke(this, $"Palette: {Colors[prevIndex].ToHex(Colors[prevIndex].A < 255)} (key ,)");
    }

    [RelayCommand]
    public void SelectPrimaryColor(PixelColor color)
    {
        PrimaryColor = color;
    }

    [RelayCommand]
    public void SelectSecondaryColor(PixelColor color)
    {
        SecondaryColor = color;
    }

    [RelayCommand]
    public void LoadPreset(Palette preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        CurrentPalette = preset.Clone();
        SyncColorsFromPalette();
    }

    [RelayCommand]
    public void LoadPresetByName(string name)
    {
        var preset = PalettePresets.GetPresetByName(name);
        LoadPreset(preset);
    }

    [RelayCommand]
    public void AddColor(PixelColor color)
    {
        if (!Colors.Contains(color))
        {
            Colors.Add(color);
            CurrentPalette.Add(color);
        }
    }

    [RelayCommand]
    public void AddRampColors(IEnumerable<PixelColor> rampColors)
    {
        ArgumentNullException.ThrowIfNull(rampColors);
        foreach (var color in rampColors)
        {
            AddColor(color);
        }
    }

    [RelayCommand]
    public void RemoveColor(PixelColor color)
    {
        Colors.Remove(color);
        CurrentPalette.Remove(color);
    }

    private void SyncColorsFromPalette()
    {
        Colors.Clear();
        foreach (var color in CurrentPalette.Colors)
        {
            Colors.Add(color);
        }
    }
}

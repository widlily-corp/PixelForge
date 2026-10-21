using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using PixelForge.Core.Localization;
using PixelForge.Core.Models;
using PixelForge.Core.Palettes;

namespace PixelForge.App.Views;

/// <summary>
/// Active editing target for sliders and hex input.
/// </summary>
public enum ActiveColorTarget
{
    /// <summary>
    /// Editing the primary (LMB) color.
    /// </summary>
    Primary,

    /// <summary>
    /// Editing the secondary (RMB) color.
    /// </summary>
    Secondary
}

/// <summary>
/// Display mode for the color picker content area.
/// </summary>
public enum ColorPickerViewMode
{
    /// <summary>
    /// Displays palette swatches grid.
    /// </summary>
    Palette,

    /// <summary>
    /// Displays RGBA sliders.
    /// </summary>
    Sliders
}

/// <summary>
/// UserControl providing color picking, RGBA sliders, hex input, and palette preset management.
/// </summary>
public partial class ColorPickerView : UserControl
{
    public LocalizationService Loc => LocalizationService.Instance;

    private bool _isUpdating;
    private PixelColor _primaryColor = PixelColor.Black;
    private PixelColor _secondaryColor = PixelColor.White;
    private ActiveColorTarget _activeTarget = ActiveColorTarget.Primary;
    private ColorPickerViewMode _viewMode = ColorPickerViewMode.Palette;
    private readonly List<PixelColor> _currentPalette = new();
    private System.ComponentModel.INotifyPropertyChanged? _subscribedInpc;

    public static readonly StyledProperty<PixelColor> PrimaryColorProperty =
        AvaloniaProperty.Register<ColorPickerView, PixelColor>(nameof(PrimaryColor), PixelColor.Black);

    public static readonly StyledProperty<PixelColor> SecondaryColorProperty =
        AvaloniaProperty.Register<ColorPickerView, PixelColor>(nameof(SecondaryColor), PixelColor.White);

    /// <summary>
    /// Gets or sets the primary (LMB) drawing color.
    /// </summary>
    public PixelColor PrimaryColor
    {
        get => GetValue(PrimaryColorProperty);
        set => SetValue(PrimaryColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the secondary (RMB) drawing color.
    /// </summary>
    public PixelColor SecondaryColor
    {
        get => GetValue(SecondaryColorProperty);
        set => SetValue(SecondaryColorProperty, value);
    }

    /// <summary>
    /// Gets the current active color editing target (Primary or Secondary).
    /// </summary>
    public ActiveColorTarget ActiveTarget => _activeTarget;

    /// <summary>
    /// Gets the current view mode (Palette swatches or RGB sliders).
    /// </summary>
    public ColorPickerViewMode ViewMode => _viewMode;

    /// <summary>
    /// Occurs when the primary color has changed.
    /// </summary>
    public event EventHandler<PixelColor>? PrimaryColorChanged;

    /// <summary>
    /// Occurs when the secondary color has changed.
    /// </summary>
    public event EventHandler<PixelColor>? SecondaryColorChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorPickerView"/> class.
    /// </summary>
    public ColorPickerView()
    {
        InitializeComponent();

        RedSlider.ValueChanged += (_, _) => OnSliderChanged();
        GreenSlider.ValueChanged += (_, _) => OnSliderChanged();
        BlueSlider.ValueChanged += (_, _) => OnSliderChanged();
        AlphaSlider.ValueChanged += (_, _) => OnSliderChanged();

        HexTextBox.LostFocus += (_, _) => OnHexCommitted();
        HexTextBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                OnHexCommitted();
            }
        };

        SwapColorsButton.Click += (_, _) => SwapColors();
        AddColorButton.Click += (_, _) => AddCurrentColorToPalette();
        PresetComboBox.SelectionChanged += OnPresetSelectionChanged;

        // View mode tabs
        PaletteTabButton.Click += (_, _) => SetViewMode(ColorPickerViewMode.Palette);
        SlidersTabButton.Click += (_, _) => SetViewMode(ColorPickerViewMode.Sliders);

        // Color plates selection
        PrimaryPlate.PointerPressed += (_, _) => SetActiveTarget(ActiveColorTarget.Primary);
        PrimaryColorBox.PointerPressed += (_, _) => SetActiveTarget(ActiveColorTarget.Primary);

        SecondaryPlate.PointerPressed += (_, _) => SetActiveTarget(ActiveColorTarget.Secondary);
        SecondaryColorBox.PointerPressed += (_, _) => SetActiveTarget(ActiveColorTarget.Secondary);

        // Load default palette
        LoadPalette(PalettePresets.Default32);
        SetPrimaryColor(_primaryColor);
        SetSecondaryColor(_secondaryColor);
        SetActiveTarget(ActiveColorTarget.Primary);
    }

    /// <summary>
    /// Switches the content view between Palette swatches and RGB sliders.
    /// </summary>
    public void SetViewMode(ColorPickerViewMode mode)
    {
        _viewMode = mode;
        if (mode == ColorPickerViewMode.Palette)
        {
            PaletteTabButton.Classes.Set("active", true);
            SlidersTabButton.Classes.Set("active", false);
            PaletteContainer.IsVisible = true;
            SlidersContainer.IsVisible = false;
        }
        else
        {
            SlidersTabButton.Classes.Set("active", true);
            PaletteTabButton.Classes.Set("active", false);
            SlidersContainer.IsVisible = true;
            PaletteContainer.IsVisible = false;
        }
    }

    /// <summary>
    /// Sets whether sliders and hex inputs currently target the primary or secondary color.
    /// </summary>
    public void SetActiveTarget(ActiveColorTarget target)
    {
        _activeTarget = target;
        var accentBrush = GetBrush("AccentPrimaryBrush", Brushes.DodgerBlue);
        var secondaryBrush = GetBrush("AccentSecondaryBrush", Brushes.MediumPurple);
        var subtleBrush = GetBrush("BorderSubtleBrush", Brushes.DimGray);

        if (target == ActiveColorTarget.Primary)
        {
            PrimaryPlate.BorderBrush = accentBrush;
            PrimaryPlate.BorderThickness = new Thickness(1.5);
            SecondaryPlate.BorderBrush = subtleBrush;
            SecondaryPlate.BorderThickness = new Thickness(1);

            UpdateSlidersFromColor(_primaryColor);
            UpdateHexTextFromColor(_primaryColor);
        }
        else
        {
            SecondaryPlate.BorderBrush = secondaryBrush;
            SecondaryPlate.BorderThickness = new Thickness(1.5);
            PrimaryPlate.BorderBrush = subtleBrush;
            PrimaryPlate.BorderThickness = new Thickness(1);

            UpdateSlidersFromColor(_secondaryColor);
            UpdateHexTextFromColor(_secondaryColor);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PrimaryColorProperty && !_isUpdating)
        {
            SetPrimaryColor(change.GetNewValue<PixelColor>());
        }
        else if (change.Property == SecondaryColorProperty && !_isUpdating)
        {
            SetSecondaryColor(change.GetNewValue<PixelColor>());
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_subscribedInpc != null)
        {
            _subscribedInpc.PropertyChanged -= OnDataContextPropertyChanged;
            _subscribedInpc = null;
        }

        if (DataContext is System.ComponentModel.INotifyPropertyChanged inpc)
        {
            _subscribedInpc = inpc;
            _subscribedInpc.PropertyChanged += OnDataContextPropertyChanged;
        }
        SyncFromDataContext();
    }

    private void OnDataContextPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "PrimaryColor")
        {
            var primaryProp = DataContext?.GetType().GetProperty("PrimaryColor");
            if (primaryProp != null && primaryProp.GetValue(DataContext) is PixelColor pc && pc != _primaryColor)
            {
                SetPrimaryColor(pc);
            }
        }
        else if (e.PropertyName == "SecondaryColor")
        {
            var secondaryProp = DataContext?.GetType().GetProperty("SecondaryColor");
            if (secondaryProp != null && secondaryProp.GetValue(DataContext) is PixelColor sc && sc != _secondaryColor)
            {
                SetSecondaryColor(sc);
            }
        }
        else if (e.PropertyName == "CurrentPalette")
        {
            var paletteProp = DataContext?.GetType().GetProperty("CurrentPalette");
            if (paletteProp != null && paletteProp.GetValue(DataContext) is Palette pal)
            {
                _currentPalette.Clear();
                _currentPalette.AddRange(pal.Colors);
                RebuildSwatches();
            }
        }
    }

    private void SyncFromDataContext()
    {
        if (DataContext == null)
        {
            return;
        }

        var primaryProp = DataContext.GetType().GetProperty("PrimaryColor");
        if (primaryProp != null && primaryProp.GetValue(DataContext) is PixelColor pc)
        {
            SetPrimaryColor(pc);
        }

        var secondaryProp = DataContext.GetType().GetProperty("SecondaryColor");
        if (secondaryProp != null && secondaryProp.GetValue(DataContext) is PixelColor sc)
        {
            SetSecondaryColor(sc);
        }

        var paletteProp = DataContext.GetType().GetProperty("CurrentPalette");
        if (paletteProp != null && paletteProp.GetValue(DataContext) is Palette pal)
        {
            _currentPalette.Clear();
            _currentPalette.AddRange(pal.Colors);
            RebuildSwatches();
        }
    }

    private void SyncToDataContext()
    {
        if (DataContext == null)
        {
            return;
        }

        var primaryProp = DataContext.GetType().GetProperty("PrimaryColor");
        if (primaryProp != null && primaryProp.CanWrite)
        {
            primaryProp.SetValue(DataContext, _primaryColor);
        }

        var secondaryProp = DataContext.GetType().GetProperty("SecondaryColor");
        if (secondaryProp != null && secondaryProp.CanWrite)
        {
            secondaryProp.SetValue(DataContext, _secondaryColor);
        }
    }

    private void OnSliderChanged()
    {
        if (_isUpdating)
        {
            return;
        }

        byte r = (byte)Math.Clamp((int)Math.Round(RedSlider.Value), 0, 255);
        byte g = (byte)Math.Clamp((int)Math.Round(GreenSlider.Value), 0, 255);
        byte b = (byte)Math.Clamp((int)Math.Round(BlueSlider.Value), 0, 255);
        byte a = (byte)Math.Clamp((int)Math.Round(AlphaSlider.Value), 0, 255);

        RedValueText.Text = r.ToString();
        GreenValueText.Text = g.ToString();
        BlueValueText.Text = b.ToString();
        AlphaValueText.Text = a.ToString();

        var newColor = new PixelColor(r, g, b, a);
        if (_activeTarget == ActiveColorTarget.Primary)
        {
            SetPrimaryColor(newColor, updateSliders: false);
        }
        else
        {
            SetSecondaryColor(newColor, updateSliders: false);
        }
    }

    private void OnHexCommitted()
    {
        if (_isUpdating)
        {
            return;
        }

        string text = HexTextBox.Text?.Trim() ?? string.Empty;
        if (PixelColor.TryParse(text, out var color))
        {
            if (_activeTarget == ActiveColorTarget.Primary)
            {
                SetPrimaryColor(color);
            }
            else
            {
                SetSecondaryColor(color);
            }
        }
        else
        {
            var activeColor = _activeTarget == ActiveColorTarget.Primary ? _primaryColor : _secondaryColor;
            HexTextBox.Text = activeColor.ToHex(activeColor.A < 255);
        }
    }

    private void UpdateHexTextFromColor(PixelColor color)
    {
        if (_isUpdating)
        {
            return;
        }

        _isUpdating = true;
        HexTextBox.Text = color.ToHex(color.A < 255);
        _isUpdating = false;
    }

    /// <summary>
    /// Sets the primary color and updates visual representations.
    /// </summary>
    public void SetPrimaryColor(PixelColor color, bool updateSliders = true)
    {
        _primaryColor = color;
        PrimaryColor = color;

        PrimaryColorBox.Background = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));

        if (updateSliders && _activeTarget == ActiveColorTarget.Primary)
        {
            UpdateSlidersFromColor(color);
        }

        if (_activeTarget == ActiveColorTarget.Primary)
        {
            UpdateHexTextFromColor(color);
        }

        UpdateActiveSwatchHighlight();

        PrimaryColorChanged?.Invoke(this, color);
        SyncToDataContext();
    }

    /// <summary>
    /// Sets the secondary color and updates visual representations.
    /// </summary>
    public void SetSecondaryColor(PixelColor color, bool updateSliders = true)
    {
        _secondaryColor = color;
        SecondaryColor = color;

        SecondaryColorBox.Background = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));

        if (updateSliders && _activeTarget == ActiveColorTarget.Secondary)
        {
            UpdateSlidersFromColor(color);
        }

        if (_activeTarget == ActiveColorTarget.Secondary)
        {
            UpdateHexTextFromColor(color);
        }

        SecondaryColorChanged?.Invoke(this, color);
        SyncToDataContext();
    }

    private void UpdateSlidersFromColor(PixelColor color)
    {
        _isUpdating = true;
        RedSlider.Value = color.R;
        GreenSlider.Value = color.G;
        BlueSlider.Value = color.B;
        AlphaSlider.Value = color.A;

        RedValueText.Text = color.R.ToString();
        GreenValueText.Text = color.G.ToString();
        BlueValueText.Text = color.B.ToString();
        AlphaValueText.Text = color.A.ToString();
        _isUpdating = false;
    }

    /// <summary>
    /// Swaps the primary and secondary colors.
    /// </summary>
    public void SwapColors()
    {
        var temp = _primaryColor;
        SetPrimaryColor(_secondaryColor);
        SetSecondaryColor(temp);
        SetActiveTarget(_activeTarget);
    }

    private void OnPresetSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PresetComboBox.SelectedItem is ComboBoxItem item)
        {
            string? header = item.Content?.ToString();
            Palette palette = header switch
            {
                "DawnBringer 16" => PalettePresets.DawnBringer16,
                "Endesga 32" => PalettePresets.Endesga32,
                "PICO-8" => PalettePresets.Pico8,
                "GameBoy" => PalettePresets.GameBoy,
                "NES 54" or "NES" => PalettePresets.Nes,
                "Cyberpunk Neo" => PalettePresets.CyberpunkNeo,
                "Solarized" => PalettePresets.Solarized,
                _ => PalettePresets.Default32
            };
            LoadPalette(palette);
        }
    }

    /// <summary>
    /// Loads a palette and rebuilds the swatches grid.
    /// </summary>
    public void LoadPalette(Palette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        _currentPalette.Clear();
        _currentPalette.AddRange(palette.Colors);
        RebuildSwatches();

        if (DataContext is ViewModels.PaletteViewModel pvm && pvm.CurrentPalette != palette)
        {
            pvm.LoadPreset(palette);
        }
    }

    /// <summary>
    /// Adds the currently active primary color to the palette swatches.
    /// </summary>
    public void AddCurrentColorToPalette()
    {
        if (!_currentPalette.Contains(_primaryColor))
        {
            _currentPalette.Add(_primaryColor);
            RebuildSwatches();

            if (DataContext is ViewModels.PaletteViewModel pvm)
            {
                pvm.AddColor(_primaryColor);
            }
        }
    }

    private void UpdateActiveSwatchHighlight()
    {
        foreach (var child in PaletteWrapPanel.Children)
        {
            if (child is Border swatch && swatch.Tag is PixelColor color)
            {
                swatch.Classes.Set("active", color == _primaryColor);
            }
        }
    }

    private IBrush GetBrush(string resourceKey, IBrush fallback)
    {
        if (this.TryFindResource(resourceKey, out var res) && res is IBrush b)
        {
            return b;
        }

        if (Application.Current != null && Application.Current.TryFindResource(resourceKey, out var appRes) && appRes is IBrush ab)
        {
            return ab;
        }

        return fallback;
    }

    private void RebuildSwatches()
    {
        PaletteWrapPanel.Children.Clear();
        ColorCountText.Text = Loc.Format("Color_Count", _currentPalette.Count);

        for (int i = 0; i < _currentPalette.Count; i++)
        {
            var color = _currentPalette[i];
            var swatch = new Border
            {
                Width = 22,
                Height = 22,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B)),
                Cursor = new Cursor(StandardCursorType.Hand),
                Tag = color
            };
            swatch.Classes.Add("palette-swatch");
            if (color == _primaryColor)
            {
                swatch.Classes.Add("active");
            }

            ToolTip.SetTip(swatch, color.ToHex(color.A < 255));

            swatch.PointerPressed += (s, e) =>
            {
                var point = e.GetCurrentPoint(swatch);
                if (point.Properties.IsLeftButtonPressed)
                {
                    SetPrimaryColor(color);
                    SetActiveTarget(ActiveColorTarget.Primary);
                    e.Handled = true;
                }
                else if (point.Properties.IsRightButtonPressed)
                {
                    SetSecondaryColor(color);
                    SetActiveTarget(ActiveColorTarget.Secondary);
                    e.Handled = true;
                }
            };

            PaletteWrapPanel.Children.Add(swatch);
        }
    }
}

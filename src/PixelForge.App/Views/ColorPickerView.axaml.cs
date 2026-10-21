using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using PixelForge.Core.Models;
using PixelForge.Core.Palettes;

namespace PixelForge.App.Views;

public partial class ColorPickerView : UserControl
{
    private bool _isUpdating;
    private PixelColor _primaryColor = PixelColor.Black;
    private PixelColor _secondaryColor = PixelColor.White;
    private readonly List<PixelColor> _currentPalette = new();
    private System.ComponentModel.INotifyPropertyChanged? _subscribedInpc;

    public static readonly StyledProperty<PixelColor> PrimaryColorProperty =
        AvaloniaProperty.Register<ColorPickerView, PixelColor>(nameof(PrimaryColor), PixelColor.Black);

    public static readonly StyledProperty<PixelColor> SecondaryColorProperty =
        AvaloniaProperty.Register<ColorPickerView, PixelColor>(nameof(SecondaryColor), PixelColor.White);

    public PixelColor PrimaryColor
    {
        get => GetValue(PrimaryColorProperty);
        set => SetValue(PrimaryColorProperty, value);
    }

    public PixelColor SecondaryColor
    {
        get => GetValue(SecondaryColorProperty);
        set => SetValue(SecondaryColorProperty, value);
    }

    public event EventHandler<PixelColor>? PrimaryColorChanged;
    public event EventHandler<PixelColor>? SecondaryColorChanged;

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

        PrimaryColorBox.PointerPressed += (_, _) =>
        {
            // Activate primary editing
            UpdateSlidersFromColor(_primaryColor);
        };

        SecondaryColorBox.PointerPressed += (_, _) =>
        {
            // Activate secondary editing
            UpdateSlidersFromColor(_secondaryColor);
        };

        // Load default palette
        LoadPalette(PalettePresets.Default32);
        SetPrimaryColor(_primaryColor);
        SetSecondaryColor(_secondaryColor);
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
    }

    private void SyncFromDataContext()
    {
        if (DataContext == null)
        {
            return;
        }

        // Check if DataContext has Palette property or ViewModel
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
        SetPrimaryColor(newColor, updateSliders: false);
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
            SetPrimaryColor(color);
        }
        else
        {
            // Revert invalid hex text
            HexTextBox.Text = _primaryColor.ToHex(_primaryColor.A < 255);
        }
    }

    public void SetPrimaryColor(PixelColor color, bool updateSliders = true)
    {
        _primaryColor = color;
        PrimaryColor = color;

        PrimaryColorBox.Background = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));

        if (updateSliders)
        {
            UpdateSlidersFromColor(color);
        }

        if (!_isUpdating)
        {
            _isUpdating = true;
            HexTextBox.Text = color.ToHex(color.A < 255);
            _isUpdating = false;
        }

        PrimaryColorChanged?.Invoke(this, color);
        SyncToDataContext();
    }

    public void SetSecondaryColor(PixelColor color)
    {
        _secondaryColor = color;
        SecondaryColor = color;

        SecondaryColorBox.Background = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));

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

    public void SwapColors()
    {
        var temp = _primaryColor;
        SetPrimaryColor(_secondaryColor);
        SetSecondaryColor(temp);
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
                "NES 54" => PalettePresets.Nes,
                "Cyberpunk Neo" => PalettePresets.CyberpunkNeo,
                "Solarized" => PalettePresets.Solarized,
                _ => PalettePresets.Default32
            };
            LoadPalette(palette);
        }
    }

    public void LoadPalette(Palette palette)
    {
        _currentPalette.Clear();
        _currentPalette.AddRange(palette.Colors);
        RebuildSwatches();
    }

    public void AddCurrentColorToPalette()
    {
        if (!_currentPalette.Contains(_primaryColor))
        {
            _currentPalette.Add(_primaryColor);
            RebuildSwatches();
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
        ColorCountText.Text = $"{_currentPalette.Count} цветов";

        var borderBrush = GetBrush("BorderSubtleBrush", new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)));

        foreach (var color in _currentPalette)
        {
            var swatch = new Border
            {
                Width = 20,
                Height = 20,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B)),
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            ToolTip.SetTip(swatch, color.ToHex(color.A < 255));

            swatch.PointerPressed += (s, e) =>
            {
                var point = e.GetCurrentPoint(swatch);
                if (point.Properties.IsLeftButtonPressed)
                {
                    SetPrimaryColor(color);
                    e.Handled = true;
                }
                else if (point.Properties.IsRightButtonPressed)
                {
                    SetSecondaryColor(color);
                    e.Handled = true;
                }
            };

            PaletteWrapPanel.Children.Add(swatch);
        }
    }
}

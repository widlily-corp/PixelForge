using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using PixelForge.App.ViewModels;
using PixelForge.Core.Models;
using PixelForge.Core.Palettes;

namespace PixelForge.App.Views;

/// <summary>
/// Interactive modal dialog for designing artistic color ramps and multi-stop gradients with Hue Shifting.
/// </summary>
public partial class ColorRampDialog : Window
{
    private readonly PaletteViewModel? _paletteViewModel;
    private readonly ToolViewModel? _toolViewModel;
    private PixelColor _baseColor = new(220, 80, 50);
    private PixelColor[] _currentRamp = Array.Empty<PixelColor>();
    private bool _isUpdatingUi;

    public ColorRampDialog()
    {
        InitializeComponent();
        AttachEvents();
        RegenerateRamp();
    }

    public ColorRampDialog(PaletteViewModel paletteViewModel, ToolViewModel toolViewModel) : this()
    {
        _paletteViewModel = paletteViewModel;
        _toolViewModel = toolViewModel;

        if (_paletteViewModel != null)
        {
            _baseColor = _paletteViewModel.PrimaryColor;
        }

        RegenerateRamp();
    }

    private void AttachEvents()
    {
        KeyDown += OnWindowKeyDown;

        if (PresetComboBox != null)
        {
            PresetComboBox.SelectionChanged += (_, _) =>
            {
                if (_isUpdatingUi) return;
                SyncPresetSettings();
                RegenerateRamp();
            };
        }

        if (StepsSlider != null)
        {
            StepsSlider.PropertyChanged += (_, e) =>
            {
                if (e.Property.Name == nameof(Slider.Value))
                {
                    if (StepsValueText != null)
                    {
                        StepsValueText.Text = $"{(int)StepsSlider.Value}";
                    }
                    RegenerateRamp();
                }
            };
        }

        if (HueShiftSlider != null)
        {
            HueShiftSlider.PropertyChanged += (_, e) =>
            {
                if (e.Property.Name == nameof(Slider.Value))
                {
                    int val = (int)HueShiftSlider.Value;
                    if (HueShiftValueText != null)
                    {
                        HueShiftValueText.Text = val >= 0 ? $"+{val}°" : $"{val}°";
                    }
                    RegenerateRamp();
                }
            };
        }

        if (UsePrimaryColorButton != null)
        {
            UsePrimaryColorButton.Click += (_, _) =>
            {
                if (_paletteViewModel != null)
                {
                    _baseColor = _paletteViewModel.PrimaryColor;
                    RegenerateRamp();
                }
            };
        }

        if (AddToPaletteButton != null)
        {
            AddToPaletteButton.Click += OnAddToPaletteClick;
        }

        if (ApplyToGradientButton != null)
        {
            ApplyToGradientButton.Click += OnApplyToGradientClick;
        }

        if (CloseButton != null)
        {
            CloseButton.Click += (_, _) => Close();
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void SyncPresetSettings()
    {
        if (PresetComboBox == null || StepsSlider == null || HueShiftSlider == null) return;

        _isUpdatingUi = true;
        try
        {
            int index = PresetComboBox.SelectedIndex;
            switch (index)
            {
                case 0: // Sunlight
                    HueShiftSlider.Value = 35;
                    break;
                case 1: // Fire
                    HueShiftSlider.Value = 28;
                    break;
                case 2: // Ice
                    HueShiftSlider.Value = -25;
                    break;
                case 3: // Toxic
                    HueShiftSlider.Value = 30;
                    break;
                case 4: // Gold
                    HueShiftSlider.Value = 20;
                    break;
                default: // Custom
                    break;
            }
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private void RegenerateRamp()
    {
        if (BaseColorSwatch != null)
        {
            BaseColorSwatch.Background = new SolidColorBrush(Color.FromArgb(_baseColor.A, _baseColor.R, _baseColor.G, _baseColor.B));
        }

        int steps = StepsSlider != null ? (int)StepsSlider.Value : 5;
        float hueShift = HueShiftSlider != null ? (float)HueShiftSlider.Value : 30f;
        int presetIndex = PresetComboBox?.SelectedIndex ?? 0;

        if (presetIndex >= 0 && presetIndex <= 4)
        {
            var presetType = (RampPresetType)presetIndex;
            _currentRamp = ColorRampGenerator.GeneratePreset(presetType, _baseColor, steps);
        }
        else
        {
            _currentRamp = ColorRampGenerator.GenerateRamp(new ColorRampSettings(_baseColor, steps, hueShift));
        }

        UpdateSwatchesDisplay();
    }

    private void UpdateSwatchesDisplay()
    {
        if (SwatchesContainer == null) return;

        SwatchesContainer.Children.Clear();
        if (_currentRamp.Length == 0) return;

        int count = _currentRamp.Length;
        int swatchWidth = Math.Clamp(460 / count, 36, 76);

        IBrush borderBrush = GetBrush("BorderStrongBrush", new SolidColorBrush(Color.Parse("#384260")));
        IBrush textBrush = GetBrush("TextSecondaryBrush", new SolidColorBrush(Color.Parse("#94A3B8")));

        for (int i = 0; i < count; i++)
        {
            var color = _currentRamp[i];
            var itemPanel = new StackPanel
            {
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var swatchBorder = new Border
            {
                Width = swatchWidth,
                Height = 52,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B)),
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1.5),
                BoxShadow = BoxShadows.Parse("0 2 6 0 #20000000")
            };

            var hexText = new TextBlock
            {
                Text = color.ToHex(false),
                FontSize = 10,
                FontFamily = new FontFamily("Consolas, monospace"),
                Foreground = textBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            itemPanel.Children.Add(swatchBorder);
            itemPanel.Children.Add(hexText);
            SwatchesContainer.Children.Add(itemPanel);
        }

        if (RampStatusText != null)
        {
            RampStatusText.Text = $"{count} цветов (Тень ➔ Свет)";
        }
    }

    private void OnAddToPaletteClick(object? sender, RoutedEventArgs e)
    {
        if (_paletteViewModel != null && _currentRamp.Length > 0)
        {
            _paletteViewModel.AddRampColors(_currentRamp);
            if (RampStatusText != null)
            {
                RampStatusText.Text = "Добавлено в палитру!";
            }
        }
    }

    private void OnApplyToGradientClick(object? sender, RoutedEventArgs e)
    {
        if (_toolViewModel != null && _currentRamp.Length > 0)
        {
            string name = PresetComboBox?.SelectedItem is ComboBoxItem item
                ? item.Content?.ToString() ?? "Рампа"
                : "Рампа";

            _toolViewModel.ApplyRampAsGradient(_currentRamp, name);
            Close();
        }
    }

    private IBrush GetBrush(string resourceKey, IBrush fallback)
    {
        if (this.TryFindResource(resourceKey, out var res) && res is IBrush brush)
        {
            return brush;
        }

        if (Application.Current != null && Application.Current.TryFindResource(resourceKey, out var appRes) && appRes is IBrush ab)
        {
            return ab;
        }

        return fallback;
    }
}

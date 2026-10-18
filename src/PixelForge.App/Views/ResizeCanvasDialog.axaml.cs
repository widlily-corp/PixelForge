using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using PixelForge.Core.Models;

namespace PixelForge.App.Views;

/// <summary>
/// Result data returned when the user applies canvas resizing.
/// </summary>
public sealed record ResizeCanvasResult(int Width, int Height, CanvasAnchor Anchor, ResizeMode Mode);

/// <summary>
/// Interactive dialog for resizing canvas dimensions with 3x3 anchor positioning,
/// nearest-neighbor pixel scaling, and quick presets.
/// </summary>
public partial class ResizeCanvasDialog : Window
{
    private readonly int _initialWidth;
    private readonly int _initialHeight;
    private CanvasAnchor _selectedAnchor = CanvasAnchor.Center;
    private bool _isUpdatingAspect;

    public ResizeCanvasResult? Result { get; private set; }

    public ResizeCanvasDialog() : this(32, 32)
    {
    }

    public ResizeCanvasDialog(int initialWidth, int initialHeight)
    {
        _initialWidth = Math.Clamp(initialWidth, 8, 1024);
        _initialHeight = Math.Clamp(initialHeight, 8, 1024);

        InitializeComponent();

        CurrentDimensionsText.Text = $"Текущий размер: {_initialWidth} × {_initialHeight} px";
        WidthInput.Value = _initialWidth;
        HeightInput.Value = _initialHeight;

        // Wire Presets
        Preset16Button.Click += (_, _) => SetDimensions(16, 16);
        Preset32Button.Click += (_, _) => SetDimensions(32, 32);
        Preset48Button.Click += (_, _) => SetDimensions(48, 48);
        Preset64Button.Click += (_, _) => SetDimensions(64, 64);
        Preset128Button.Click += (_, _) => SetDimensions(128, 128);
        Preset256Button.Click += (_, _) => SetDimensions(256, 256);

        ScaleHalfButton.Click += (_, _) => SetDimensions(Math.Max(8, _initialWidth / 2), Math.Max(8, _initialHeight / 2));
        ScaleDoubleButton.Click += (_, _) => SetDimensions(Math.Min(1024, _initialWidth * 2), Math.Min(1024, _initialHeight * 2));

        // Aspect ratio tracking
        WidthInput.ValueChanged += OnWidthChanged;
        HeightInput.ValueChanged += OnHeightChanged;

        // Mode radios
        AnchorModeRadio.IsCheckedChanged += OnModeChanged;
        ScaleModeRadio.IsCheckedChanged += OnModeChanged;

        // Anchor buttons
        AnchorTopLeft.Click += (_, _) => SelectAnchor(CanvasAnchor.TopLeft);
        AnchorTopCenter.Click += (_, _) => SelectAnchor(CanvasAnchor.TopCenter);
        AnchorTopRight.Click += (_, _) => SelectAnchor(CanvasAnchor.TopRight);

        AnchorMiddleLeft.Click += (_, _) => SelectAnchor(CanvasAnchor.MiddleLeft);
        AnchorCenter.Click += (_, _) => SelectAnchor(CanvasAnchor.Center);
        AnchorMiddleRight.Click += (_, _) => SelectAnchor(CanvasAnchor.MiddleRight);

        AnchorBottomLeft.Click += (_, _) => SelectAnchor(CanvasAnchor.BottomLeft);
        AnchorBottomCenter.Click += (_, _) => SelectAnchor(CanvasAnchor.BottomCenter);
        AnchorBottomRight.Click += (_, _) => SelectAnchor(CanvasAnchor.BottomRight);

        // Action buttons
        CancelButton.Click += (_, _) => Close(null);
        ApplyButton.Click += OnApplyClick;

        UpdateAnchorVisuals();
    }

    private void SetDimensions(int w, int h)
    {
        _isUpdatingAspect = true;
        try
        {
            WidthInput.Value = Math.Clamp(w, 8, 1024);
            HeightInput.Value = Math.Clamp(h, 8, 1024);
        }
        finally
        {
            _isUpdatingAspect = false;
        }
    }

    private void OnWidthChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isUpdatingAspect || KeepAspectCheckBox?.IsChecked != true || WidthInput.Value == null)
        {
            return;
        }

        _isUpdatingAspect = true;
        try
        {
            float ratio = (float)_initialHeight / _initialWidth;
            HeightInput.Value = Math.Clamp((int)Math.Round((double)WidthInput.Value.Value * ratio), 8, 1024);
        }
        finally
        {
            _isUpdatingAspect = false;
        }
    }

    private void OnHeightChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isUpdatingAspect || KeepAspectCheckBox?.IsChecked != true || HeightInput.Value == null)
        {
            return;
        }

        _isUpdatingAspect = true;
        try
        {
            float ratio = (float)_initialWidth / _initialHeight;
            WidthInput.Value = Math.Clamp((int)Math.Round((double)HeightInput.Value.Value * ratio), 8, 1024);
        }
        finally
        {
            _isUpdatingAspect = false;
        }
    }

    private void OnModeChanged(object? sender, RoutedEventArgs e)
    {
        bool isAnchorMode = AnchorModeRadio.IsChecked == true;
        AnchorPanel.Opacity = isAnchorMode ? 1.0 : 0.4;
        AnchorPanel.IsEnabled = isAnchorMode;
    }

    private void SelectAnchor(CanvasAnchor anchor)
    {
        _selectedAnchor = anchor;
        UpdateAnchorVisuals();
    }

    private void UpdateAnchorVisuals()
    {
        AnchorDescriptionText.Text = _selectedAnchor switch
        {
            CanvasAnchor.TopLeft => "Сверху-слева (расширение вправо и вниз)",
            CanvasAnchor.TopCenter => "Сверху по центру (расширение по бокам и вниз)",
            CanvasAnchor.TopRight => "Сверху-справа (расширение влево и вниз)",
            CanvasAnchor.MiddleLeft => "По центру слева (расширение вправо)",
            CanvasAnchor.Center => "По центру (равномерное расширение во все стороны)",
            CanvasAnchor.MiddleRight => "По центру справа (расширение влево)",
            CanvasAnchor.BottomLeft => "Снизу-слева (расширение вправо и вверх)",
            CanvasAnchor.BottomCenter => "Снизу по центру (расширение по бокам и вверх)",
            CanvasAnchor.BottomRight => "Снизу-справа (расширение влево и вверх)",
            _ => "По центру"
        };

        var highlightBrush = GetResourceBrush("AccentPrimaryBrush", Brushes.DodgerBlue);
        var cardBrush = GetResourceBrush("SurfaceCardBrush", Brushes.DarkSlateGray);
        var primaryTextBrush = GetResourceBrush("TextPrimaryBrush", Brushes.White);
        var activeTextBrush = new SolidColorBrush(Color.FromRgb(10, 14, 23));

        UpdateButtonHighlight(AnchorTopLeft, _selectedAnchor == CanvasAnchor.TopLeft, highlightBrush, cardBrush, activeTextBrush, primaryTextBrush);
        UpdateButtonHighlight(AnchorTopCenter, _selectedAnchor == CanvasAnchor.TopCenter, highlightBrush, cardBrush, activeTextBrush, primaryTextBrush);
        UpdateButtonHighlight(AnchorTopRight, _selectedAnchor == CanvasAnchor.TopRight, highlightBrush, cardBrush, activeTextBrush, primaryTextBrush);

        UpdateButtonHighlight(AnchorMiddleLeft, _selectedAnchor == CanvasAnchor.MiddleLeft, highlightBrush, cardBrush, activeTextBrush, primaryTextBrush);
        UpdateButtonHighlight(AnchorCenter, _selectedAnchor == CanvasAnchor.Center, highlightBrush, cardBrush, activeTextBrush, primaryTextBrush);
        UpdateButtonHighlight(AnchorMiddleRight, _selectedAnchor == CanvasAnchor.MiddleRight, highlightBrush, cardBrush, activeTextBrush, primaryTextBrush);

        UpdateButtonHighlight(AnchorBottomLeft, _selectedAnchor == CanvasAnchor.BottomLeft, highlightBrush, cardBrush, activeTextBrush, primaryTextBrush);
        UpdateButtonHighlight(AnchorBottomCenter, _selectedAnchor == CanvasAnchor.BottomCenter, highlightBrush, cardBrush, activeTextBrush, primaryTextBrush);
        UpdateButtonHighlight(AnchorBottomRight, _selectedAnchor == CanvasAnchor.BottomRight, highlightBrush, cardBrush, activeTextBrush, primaryTextBrush);
    }

    private static void UpdateButtonHighlight(Button btn, bool isSelected, IBrush activeBg, IBrush defaultBg, IBrush activeFg, IBrush defaultFg)
    {
        if (isSelected)
        {
            btn.Background = activeBg;
            btn.Foreground = activeFg;
            btn.BorderBrush = activeBg;
        }
        else
        {
            btn.Background = defaultBg;
            btn.Foreground = defaultFg;
            btn.BorderBrush = Brushes.Transparent;
        }
    }

    private IBrush GetResourceBrush(string key, IBrush fallback)
    {
        if (this.TryFindResource(key, out var res) && res is IBrush b)
        {
            return b;
        }

        if (Application.Current != null && Application.Current.TryFindResource(key, out var appRes) && appRes is IBrush ab)
        {
            return ab;
        }

        return fallback;
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        int width = (int)(WidthInput.Value ?? _initialWidth);
        int height = (int)(HeightInput.Value ?? _initialHeight);

        if (width < 8 || width > 1024 || height < 8 || height > 1024)
        {
            ErrorText.Text = "Размеры холста должны быть от 8 до 1024 пикселей.";
            return;
        }

        var mode = ScaleModeRadio.IsChecked == true ? ResizeMode.ScaleNearestNeighbor : ResizeMode.Anchor;
        Result = new ResizeCanvasResult(width, height, _selectedAnchor, mode);
        Close(Result);
    }
}

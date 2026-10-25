using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PixelForge.Core.Models;
using PixelForge.Core.Utils;

namespace PixelForge.App.Controls;

/// <summary>
/// Interaction drag mode for the HSV color wheel control.
/// </summary>
public enum WheelDragMode
{
    /// <summary>
    /// No active interaction.
    /// </summary>
    None,

    /// <summary>
    /// Dragging the outer Hue ring indicator.
    /// </summary>
    Hue,

    /// <summary>
    /// Dragging the inner Saturation-Value box reticle handle.
    /// </summary>
    SaturationValue
}

/// <summary>
/// Hardware-accelerated HSV Color Wheel control with an outer continuous Hue sweep ring
/// and an inner Saturation-Value square. Provides zero-allocation rendering and smooth pointer tracking.
/// </summary>
public class ColorWheelControl : Control
{
    private bool _isInternalUpdate;
    private WheelDragMode _dragMode = WheelDragMode.None;

    // Pre-allocated, reusable rendering primitives (Zero GC allocations per frame)
    private readonly ConicGradientBrush _hueBrush;
    private readonly Pen _hueRingPen;
    private readonly LinearGradientBrush _svHorizontalBrush;
    private readonly GradientStop _svHueStop;
    private readonly LinearGradientBrush _svVerticalBrush;
    private readonly Pen _svBorderPen;
    private readonly Pen _handleOuterPen;
    private readonly Pen _handleInnerPen;
    private readonly SolidColorBrush _hueHandleFillBrush;

    public static readonly StyledProperty<PixelColor> SelectedColorProperty =
        AvaloniaProperty.Register<ColorWheelControl, PixelColor>(
            nameof(SelectedColor),
            PixelColor.Red,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<float> HueProperty =
        AvaloniaProperty.Register<ColorWheelControl, float>(
            nameof(Hue),
            0f,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<float> SaturationProperty =
        AvaloniaProperty.Register<ColorWheelControl, float>(
            nameof(Saturation),
            1f,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<float> ValueProperty =
        AvaloniaProperty.Register<ColorWheelControl, float>(
            nameof(Value),
            1f,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<ColorWheelControl, IBrush?>(
            nameof(Background),
            Brushes.Transparent);

    static ColorWheelControl()
    {
        AffectsRender<ColorWheelControl>(
            SelectedColorProperty,
            HueProperty,
            SaturationProperty,
            ValueProperty,
            BackgroundProperty);
    }

    /// <summary>
    /// Gets or sets the selected 32-bit RGBA color.
    /// </summary>
    public PixelColor SelectedColor
    {
        get => GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the current Hue in degrees [0, 360).
    /// </summary>
    public float Hue
    {
        get => GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    /// <summary>
    /// Gets or sets the current Saturation in range [0, 1].
    /// </summary>
    public float Saturation
    {
        get => GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    /// <summary>
    /// Gets or sets the current Value/Brightness in range [0, 1].
    /// </summary>
    public float Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Gets or sets the background brush behind the wheel.
    /// </summary>
    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>
    /// Gets the current drag interaction mode.
    /// </summary>
    public WheelDragMode DragMode => _dragMode;

    /// <summary>
    /// Occurs when the selected color changes via user interaction or property assignment.
    /// </summary>
    public event EventHandler<PixelColor>? ColorChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorWheelControl"/> class.
    /// </summary>
    public ColorWheelControl()
    {
        ClipToBounds = false;
        try
        {
            Cursor = new Cursor(StandardCursorType.Cross);
        }
        catch
        {
            // Fallback for headless testing environments without ICursorFactory
        }

        // 1. Hue sweep gradient brush & pen
        _hueBrush = new ConicGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            Angle = 0,
            GradientStops = new GradientStops
            {
                new GradientStop(Color.FromRgb(255, 0, 0), 0.0),
                new GradientStop(Color.FromRgb(255, 255, 0), 1.0 / 6.0),
                new GradientStop(Color.FromRgb(0, 255, 0), 2.0 / 6.0),
                new GradientStop(Color.FromRgb(0, 255, 255), 3.0 / 6.0),
                new GradientStop(Color.FromRgb(0, 0, 255), 4.0 / 6.0),
                new GradientStop(Color.FromRgb(255, 0, 255), 5.0 / 6.0),
                new GradientStop(Color.FromRgb(255, 0, 0), 1.0)
            }
        };
        _hueRingPen = new Pen(_hueBrush, 16);

        // 2. Inner SV box horizontal gradient (White -> Pure Hue)
        _svHueStop = new GradientStop(Color.FromRgb(255, 0, 0), 1.0);
        _svHorizontalBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Colors.White, 0.0),
                _svHueStop
            }
        };

        // 3. Inner SV box vertical gradient (Transparent -> Black)
        _svVerticalBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Colors.Transparent, 0.0),
                new GradientStop(Colors.Black, 1.0)
            }
        };

        // 4. Cached pens and brushes for crisp reticle and indicators
        _svBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), 1);
        _handleOuterPen = new Pen(Brushes.Black, 1.5);
        _handleInnerPen = new Pen(Brushes.White, 1.5);
        _hueHandleFillBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0));

        OnExternalColorChanged(SelectedColor);
    }

    /// <summary>
    /// Calculates the layout geometry of the Hue ring and inner SV square.
    /// </summary>
    public static void CalculateGeometry(
        Rect bounds,
        out Point center,
        out double rOuter,
        out double ringThickness,
        out double rInner,
        out double rMid,
        out Rect svRect)
    {
        center = new Point(bounds.Width / 2.0, bounds.Height / 2.0);
        double minDim = Math.Min(bounds.Width, bounds.Height);
        const double padding = 6.0;
        rOuter = Math.Max(0.0, (minDim / 2.0) - padding);
        ringThickness = Math.Clamp(rOuter * 0.22, 12.0, 20.0);
        rInner = Math.Max(0.0, rOuter - ringThickness);
        rMid = (rOuter + rInner) / 2.0;

        const double gap = 4.0;
        double rBox = Math.Max(0.0, rInner - gap);
        double side = rBox * Math.Sqrt(2.0);
        double halfSide = side / 2.0;
        svRect = new Rect(center.X - halfSide, center.Y - halfSide, side, side);
    }

    /// <summary>
    /// Calculates Hue in degrees [0, 360) corresponding to a screen point relative to center.
    /// </summary>
    public static float CalculateHueFromPoint(Point point, Point center)
    {
        double dx = point.X - center.X;
        double dy = point.Y - center.Y;
        double angleRad = Math.Atan2(dy, dx);
        double angleDeg = angleRad * (180.0 / Math.PI);
        if (angleDeg < 0.0)
        {
            angleDeg += 360.0;
        }

        float hue = (float)(angleDeg % 360.0);
        return Math.Clamp(hue, 0f, 359.999f);
    }

    /// <summary>
    /// Calculates Saturation [0, 1] and Value [0, 1] corresponding to a point in the SV rectangle.
    /// </summary>
    public static void CalculateSaturationValueFromPoint(
        Point point,
        Rect svRect,
        out float saturation,
        out float value)
    {
        if (svRect.Width <= 0 || svRect.Height <= 0)
        {
            saturation = 1f;
            value = 1f;
            return;
        }

        double normX = (point.X - svRect.Left) / svRect.Width;
        double normY = (point.Y - svRect.Top) / svRect.Height;

        saturation = Math.Clamp((float)normX, 0f, 1f);
        value = Math.Clamp((float)(1.0 - normY), 0f, 1f);
    }

    /// <summary>
    /// Calculates the position on the Hue ring for a given hue angle.
    /// </summary>
    public static Point GetPointFromHue(float hue, Point center, double rMid)
    {
        double rad = (hue % 360f) * (Math.PI / 180.0);
        double x = center.X + rMid * Math.Cos(rad);
        double y = center.Y + rMid * Math.Sin(rad);
        return new Point(x, y);
    }

    /// <summary>
    /// Calculates the position within the SV rectangle for given saturation and value.
    /// </summary>
    public static Point GetPointFromSaturationValue(float saturation, float value, Rect svRect)
    {
        double x = svRect.Left + Math.Clamp(saturation, 0f, 1f) * svRect.Width;
        double y = svRect.Top + (1.0 - Math.Clamp(value, 0f, 1f)) * svRect.Height;
        return new Point(x, y);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedColorProperty && !_isInternalUpdate)
        {
            OnExternalColorChanged(change.GetNewValue<PixelColor>());
        }
        else if (change.Property == HueProperty && !_isInternalUpdate)
        {
            _isInternalUpdate = true;
            UpdateColorFromHsv();
            _isInternalUpdate = false;
        }
        else if ((change.Property == SaturationProperty || change.Property == ValueProperty) && !_isInternalUpdate)
        {
            _isInternalUpdate = true;
            UpdateColorFromHsv();
            _isInternalUpdate = false;
        }
    }

    private void OnExternalColorChanged(PixelColor color)
    {
        ColorSpaceHelper.ToHsv(color, out float h, out float s, out float v);

        _isInternalUpdate = true;
        // Keep existing hue if incoming color is grayscale or pure black
        if (s > 0.001f && v > 0.001f)
        {
            Hue = h;
        }

        Saturation = s;
        Value = v;

        PixelColor pureHue = ColorSpaceHelper.FromHsv(Hue, 1f, 1f);
        _svHueStop.Color = Color.FromRgb(pureHue.R, pureHue.G, pureHue.B);
        _hueHandleFillBrush.Color = Color.FromRgb(pureHue.R, pureHue.G, pureHue.B);
        _isInternalUpdate = false;

        InvalidateVisual();
    }

    private void UpdateColorFromHsv()
    {
        PixelColor pureHue = ColorSpaceHelper.FromHsv(Hue, 1f, 1f);
        _svHueStop.Color = Color.FromRgb(pureHue.R, pureHue.G, pureHue.B);
        _hueHandleFillBrush.Color = Color.FromRgb(pureHue.R, pureHue.G, pureHue.B);

        byte alpha = SelectedColor.A;
        if (alpha == 0 && SelectedColor == PixelColor.Transparent)
        {
            alpha = 255;
        }

        PixelColor newColor = ColorSpaceHelper.FromHsv(Hue, Saturation, Value, alpha);
        if (newColor != SelectedColor)
        {
            SelectedColor = newColor;
            ColorChanged?.Invoke(this, newColor);
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        if (bounds.Width < 20 || bounds.Height < 20)
        {
            return;
        }

        CalculateGeometry(
            bounds,
            out Point center,
            out _,
            out double ringThickness,
            out _,
            out double rMid,
            out Rect svRect);

        // Background hit-test fill
        if (Background != null)
        {
            context.DrawRectangle(Background, null, bounds);
        }

        // 1. Update Hue brush center & pen thickness (zero allocations)
        _hueBrush.Center = new RelativePoint(center.X, center.Y, RelativeUnit.Absolute);
        _hueRingPen.Thickness = ringThickness;

        // 2. Draw outer continuous Hue sweep ring
        context.DrawEllipse(null, _hueRingPen, center, rMid, rMid);

        // 3. Draw inner SV square (horizontal pure hue gradient, overlaid by vertical black gradient)
        context.DrawRectangle(_svHorizontalBrush, null, svRect);
        context.DrawRectangle(_svVerticalBrush, null, svRect);
        context.DrawRectangle(null, _svBorderPen, svRect);

        // 4. Draw Hue indicator handle on the ring
        Point huePoint = GetPointFromHue(Hue, center, rMid);
        context.DrawEllipse(Brushes.White, _handleOuterPen, huePoint, 6.0, 6.0);
        context.DrawEllipse(_hueHandleFillBrush, null, huePoint, 3.5, 3.5);

        // 5. Draw SV reticle handle inside the square
        Point svPoint = GetPointFromSaturationValue(Saturation, Value, svRect);
        context.DrawEllipse(null, _handleOuterPen, svPoint, 5.0, 5.0);
        context.DrawEllipse(null, _handleInnerPen, svPoint, 3.5, 3.5);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var p = e.GetCurrentPoint(this);
        if (!p.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var point = p.Position;
        CalculateGeometry(
            Bounds,
            out Point center,
            out double rOuter,
            out _,
            out double rInner,
            out _,
            out Rect svRect);

        if (svRect.Contains(point))
        {
            _dragMode = WheelDragMode.SaturationValue;
            e.Pointer.Capture(this);
            UpdateSaturationValueFromPosition(point, svRect);
            e.Handled = true;
        }
        else
        {
            double dx = point.X - center.X;
            double dy = point.Y - center.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            // Ring hit-test with generous tolerance for comfortable interaction
            if ((dist >= rInner - 4.0 && dist <= rOuter + 6.0) || dist > rInner)
            {
                _dragMode = WheelDragMode.Hue;
                e.Pointer.Capture(this);
                UpdateHueFromPosition(point, center);
                e.Handled = true;
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragMode == WheelDragMode.None)
        {
            return;
        }

        var point = e.GetPosition(this);
        CalculateGeometry(
            Bounds,
            out Point center,
            out _,
            out _,
            out _,
            out _,
            out Rect svRect);

        if (_dragMode == WheelDragMode.Hue)
        {
            UpdateHueFromPosition(point, center);
            e.Handled = true;
        }
        else if (_dragMode == WheelDragMode.SaturationValue)
        {
            UpdateSaturationValueFromPosition(point, svRect);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragMode != WheelDragMode.None)
        {
            _dragMode = WheelDragMode.None;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragMode = WheelDragMode.None;
    }

    private void UpdateHueFromPosition(Point point, Point center)
    {
        float newHue = CalculateHueFromPoint(point, center);
        if (Math.Abs(Hue - newHue) > 0.001f)
        {
            _isInternalUpdate = true;
            Hue = newHue;

            // If color was at zero saturation & brightness (pure black), promote to full color on hue ring click
            if (Value <= 0.001f && Saturation <= 0.001f)
            {
                Saturation = 1f;
                Value = 1f;
            }

            UpdateColorFromHsv();
            _isInternalUpdate = false;
        }
    }

    private void UpdateSaturationValueFromPosition(Point point, Rect svRect)
    {
        CalculateSaturationValueFromPoint(point, svRect, out float s, out float v);
        if (Math.Abs(Saturation - s) > 0.001f || Math.Abs(Value - v) > 0.001f)
        {
            _isInternalUpdate = true;
            Saturation = s;
            Value = v;
            UpdateColorFromHsv();
            _isInternalUpdate = false;
        }
    }
}

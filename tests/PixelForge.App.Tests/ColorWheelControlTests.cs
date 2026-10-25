using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Avalonia;
using FluentAssertions;
using PixelForge.App.Controls;
using PixelForge.App.Views;
using PixelForge.Core.Localization;
using PixelForge.Core.Models;
using Xunit;

namespace PixelForge.App.Tests;

[Collection("LocalizationSharedState")]
public class ColorWheelControlTests
{
    private static string GetSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PixelForge.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new DirectoryNotFoundException("PixelForge.sln directory not found from " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Arrange & Act
        var wheel = new ColorWheelControl();

        // Assert
        wheel.SelectedColor.Should().Be(PixelColor.Red);
        wheel.Hue.Should().BeInRange(0f, 0.001f);
        wheel.Saturation.Should().Be(1f);
        wheel.Value.Should().Be(1f);
        wheel.DragMode.Should().Be(WheelDragMode.None);
    }

    [Theory]
    [InlineData(255, 0, 0, 0f, 1f, 1f)]       // Pure Red
    [InlineData(0, 255, 0, 120f, 1f, 1f)]     // Pure Green
    [InlineData(0, 0, 255, 240f, 1f, 1f)]     // Pure Blue
    [InlineData(255, 255, 0, 60f, 1f, 1f)]    // Yellow
    [InlineData(0, 255, 255, 180f, 1f, 1f)]   // Cyan
    [InlineData(255, 0, 255, 300f, 1f, 1f)]   // Magenta
    public void SettingSelectedColor_UpdatesHueSaturationValue(
        byte r, byte g, byte b, float expectedH, float expectedS, float expectedV)
    {
        // Arrange
        var wheel = new ColorWheelControl();
        var color = new PixelColor(r, g, b, 255);

        // Act
        wheel.SelectedColor = color;

        // Assert
        wheel.Hue.Should().BeApproximately(expectedH, 0.5f);
        wheel.Saturation.Should().BeApproximately(expectedS, 0.01f);
        wheel.Value.Should().BeApproximately(expectedV, 0.01f);
    }

    [Fact]
    public void SettingHue_UpdatesSelectedColorAndFiresColorChanged()
    {
        // Arrange
        var wheel = new ColorWheelControl();
        PixelColor receivedColor = default;
        int callCount = 0;
        wheel.ColorChanged += (s, c) =>
        {
            receivedColor = c;
            callCount++;
        };

        // Act - Switch to pure Green (Hue = 120)
        wheel.Hue = 120f;

        // Assert
        callCount.Should().BeGreaterThan(0);
        wheel.SelectedColor.R.Should().Be(0);
        wheel.SelectedColor.G.Should().Be(255);
        wheel.SelectedColor.B.Should().Be(0);
        receivedColor.Should().Be(wheel.SelectedColor);
    }

    [Fact]
    public void SettingSaturationAndValue_UpdatesSelectedColor()
    {
        // Arrange
        var wheel = new ColorWheelControl { Hue = 0f }; // Red hue

        // Act - Reduce saturation to 0 (White)
        wheel.Saturation = 0f;
        wheel.Value = 1f;

        // Assert
        wheel.SelectedColor.R.Should().Be(255);
        wheel.SelectedColor.G.Should().Be(255);
        wheel.SelectedColor.B.Should().Be(255);

        // Act - Reduce value to 0 (Black)
        wheel.Value = 0f;

        // Assert
        wheel.SelectedColor.R.Should().Be(0);
        wheel.SelectedColor.G.Should().Be(0);
        wheel.SelectedColor.B.Should().Be(0);
    }

    [Fact]
    public void ExternalBlackColor_PreservesExistingHue()
    {
        // Arrange
        var wheel = new ColorWheelControl { SelectedColor = new PixelColor(0, 0, 255, 255) }; // Blue (240°)
        wheel.Hue.Should().BeApproximately(240f, 0.5f);

        // Act - Set to Black
        wheel.SelectedColor = PixelColor.Black;

        // Assert - Hue remains Blue (240°), allowing user to brighten back without losing hue
        wheel.Hue.Should().BeApproximately(240f, 0.5f);
        wheel.Saturation.Should().Be(0f);
        wheel.Value.Should().Be(0f);

        // Act - Brighten back up
        wheel.Value = 1f;
        wheel.Saturation = 1f;

        // Assert - Restores Blue, not Red
        wheel.SelectedColor.R.Should().Be(0);
        wheel.SelectedColor.G.Should().Be(0);
        wheel.SelectedColor.B.Should().Be(255);
    }

    [Theory]
    [InlineData(150, 100, 0f)]      // East / 3 o'clock -> 0° (Red)
    [InlineData(150, 150, 45f)]     // South-East -> 45°
    [InlineData(100, 150, 90f)]     // South / 6 o'clock -> 90°
    [InlineData(50, 100, 180f)]     // West / 9 o'clock -> 180° (Cyan)
    [InlineData(100, 50, 270f)]     // North / 12 o'clock -> 270°
    public void CalculateHueFromPoint_ReturnsExpectedDegrees(double px, double py, float expectedDegrees)
    {
        // Arrange
        var center = new Point(100, 100);
        var point = new Point(px, py);

        // Act
        float hue = ColorWheelControl.CalculateHueFromPoint(point, center);

        // Assert
        hue.Should().BeApproximately(expectedDegrees, 0.5f);
    }

    [Fact]
    public void CalculateSaturationValueFromPoint_MapsCorrectly()
    {
        // Arrange
        var svRect = new Rect(50, 50, 100, 100);

        // Top-Left: S=0, V=1 (White)
        ColorWheelControl.CalculateSaturationValueFromPoint(new Point(50, 50), svRect, out float s1, out float v1);
        s1.Should().Be(0f);
        v1.Should().Be(1f);

        // Top-Right: S=1, V=1 (Pure Hue)
        ColorWheelControl.CalculateSaturationValueFromPoint(new Point(150, 50), svRect, out float s2, out float v2);
        s2.Should().Be(1f);
        v2.Should().Be(1f);

        // Bottom-Left: S=0, V=0 (Black)
        ColorWheelControl.CalculateSaturationValueFromPoint(new Point(50, 150), svRect, out float s3, out float v3);
        s3.Should().Be(0f);
        v3.Should().Be(0f);

        // Bottom-Right: S=1, V=0 (Black)
        ColorWheelControl.CalculateSaturationValueFromPoint(new Point(150, 150), svRect, out float s4, out float v4);
        s4.Should().Be(1f);
        v4.Should().Be(0f);

        // Center: S=0.5, V=0.5
        ColorWheelControl.CalculateSaturationValueFromPoint(new Point(100, 100), svRect, out float s5, out float v5);
        s5.Should().BeApproximately(0.5f, 0.001f);
        v5.Should().BeApproximately(0.5f, 0.001f);
    }

    [Fact]
    public void CalculateSaturationValueFromPoint_ClampsOutOfRangePoints()
    {
        // Arrange
        var svRect = new Rect(50, 50, 100, 100);

        // Far outside top-left
        ColorWheelControl.CalculateSaturationValueFromPoint(new Point(-20, -10), svRect, out float s1, out float v1);
        s1.Should().Be(0f);
        v1.Should().Be(1f);

        // Far outside bottom-right
        ColorWheelControl.CalculateSaturationValueFromPoint(new Point(300, 250), svRect, out float s2, out float v2);
        s2.Should().Be(1f);
        v2.Should().Be(0f);
    }

    [Fact]
    public void GetPointFromHue_ReturnsCoordinateMatchingGeometry()
    {
        // Arrange
        var center = new Point(100, 100);
        double rMid = 70.0;

        // Act & Assert
        // 0° should be at (170, 100)
        var p0 = ColorWheelControl.GetPointFromHue(0f, center, rMid);
        p0.X.Should().BeApproximately(170.0, 0.01);
        p0.Y.Should().BeApproximately(100.0, 0.01);

        // 90° should be at (100, 170)
        var p90 = ColorWheelControl.GetPointFromHue(90f, center, rMid);
        p90.X.Should().BeApproximately(100.0, 0.01);
        p90.Y.Should().BeApproximately(170.0, 0.01);

        // 180° should be at (30, 100)
        var p180 = ColorWheelControl.GetPointFromHue(180f, center, rMid);
        p180.X.Should().BeApproximately(30.0, 0.01);
        p180.Y.Should().BeApproximately(100.0, 0.01);

        // 270° should be at (100, 30)
        var p270 = ColorWheelControl.GetPointFromHue(270f, center, rMid);
        p270.X.Should().BeApproximately(100.0, 0.01);
        p270.Y.Should().BeApproximately(30.0, 0.01);
    }

    [Fact]
    public void GetPointFromSaturationValue_ReturnsCoordinatesInsideRect()
    {
        // Arrange
        var svRect = new Rect(20, 30, 80, 60);

        // Act & Assert
        var pTopLeft = ColorWheelControl.GetPointFromSaturationValue(0f, 1f, svRect);
        pTopLeft.X.Should().BeApproximately(20.0, 0.01);
        pTopLeft.Y.Should().BeApproximately(30.0, 0.01);

        var pBottomRight = ColorWheelControl.GetPointFromSaturationValue(1f, 0f, svRect);
        pBottomRight.X.Should().BeApproximately(100.0, 0.01);
        pBottomRight.Y.Should().BeApproximately(90.0, 0.01);
    }

    [Fact]
    public void CalculateGeometry_CreatesInscribedSquareWithinInnerRing()
    {
        // Arrange
        var bounds = new Rect(0, 0, 200, 200);

        // Act
        ColorWheelControl.CalculateGeometry(
            bounds,
            out Point center,
            out double rOuter,
            out double ringThickness,
            out double rInner,
            out double rMid,
            out Rect svRect);

        // Assert
        center.X.Should().Be(100.0);
        center.Y.Should().Be(100.0);
        rOuter.Should().BeGreaterThan(0.0);
        ringThickness.Should().BeInRange(12.0, 20.0);
        rInner.Should().BeLessThan(rOuter);
        rMid.Should().BeApproximately((rOuter + rInner) / 2.0, 0.001);

        // Inner SV square must fit completely inside inner ring radius
        double halfW = svRect.Width / 2.0;
        double halfH = svRect.Height / 2.0;
        double cornerDist = Math.Sqrt(halfW * halfW + halfH * halfH);

        cornerDist.Should().BeLessThan(rInner, "SV square corners must never clip or overlap the Hue ring");
    }

    [Fact]
    public void ColorPickerViewMode_Enum_DefinesWheelAsFirstTab()
    {
        // Assert: Wheel is first and default mode
        ((int)ColorPickerViewMode.Wheel).Should().Be(0);
        ColorPickerViewMode.Wheel.ToString().Should().Be("Wheel");
        ColorPickerViewMode.Palette.ToString().Should().Be("Palette");
        ColorPickerViewMode.Sliders.ToString().Should().Be("Sliders");
    }

    [Fact]
    public void ColorPickerView_Axaml_DeclaresWheelTabAndContainer()
    {
        // Arrange
        var root = GetSolutionRoot();
        var axamlPath = Path.Combine(root, "src", "PixelForge.App", "Views", "ColorPickerView.axaml");
        File.Exists(axamlPath).Should().BeTrue();

        var doc = XDocument.Load(axamlPath);
        var xmlString = doc.ToString();

        // Assert: WheelTabButton is declared
        xmlString.Should().Contain("WheelTabButton");
        xmlString.Should().Contain("Color_TabWheel");

        // Assert: WheelContainer is declared with ColorWheelControl of height 170
        xmlString.Should().Contain("WheelContainer");
        xmlString.Should().Contain("ColorWheelControl");
        xmlString.Should().Contain("Height=\"170\"");

        // Assert: 3 tabs in grid
        xmlString.Should().Contain("ColumnDefinitions=\"*,*,*\"");
    }

    [Fact]
    public void LocalizationService_ContainsColorWheelTabKeys()
    {
        // Arrange
        var loc = LocalizationService.Instance;

        // Russian localization
        loc.SetLanguage(AppLanguage.Russian);
        loc["Color_TabWheel"].Should().Be("Круг");

        // English localization
        loc.SetLanguage(AppLanguage.English);
        loc["Color_TabWheel"].Should().Be("Wheel");

        // Restore default
        loc.SetLanguage(AppLanguage.Russian);
    }
}

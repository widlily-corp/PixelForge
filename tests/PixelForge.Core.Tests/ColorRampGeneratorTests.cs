using FluentAssertions;
using PixelForge.Core.Models;
using PixelForge.Core.Palettes;
using PixelForge.Core.Utils;
using Xunit;

namespace PixelForge.Core.Tests;

public class ColorRampGeneratorTests
{
    [Fact]
    public void GenerateRamp_ShouldReturnCorrectNumberOfSteps()
    {
        // Arrange
        var baseColor = new PixelColor(200, 50, 50);

        // Act
        var ramp = ColorRampGenerator.GenerateRamp(baseColor, stepCount: 6);

        // Assert
        ramp.Should().HaveCount(6);
        ramp.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GenerateRamp_LightnessShouldIncreaseMonotonically()
    {
        // Arrange
        var baseColor = new PixelColor(100, 150, 240);

        // Act
        var ramp = ColorRampGenerator.GenerateRamp(baseColor, stepCount: 5);

        // Assert
        for (int i = 0; i < ramp.Length - 1; i++)
        {
            ColorSpaceHelper.ToHsv(ramp[i], out _, out _, out float v1);
            ColorSpaceHelper.ToHsv(ramp[i + 1], out _, out _, out float v2);

            v2.Should().BeGreaterThan(v1, "Highlight steps must be brighter than shadows");
        }
    }

    [Fact]
    public void GenerateRamp_HueShiftingShouldShiftHueAngle()
    {
        // Arrange
        var baseColor = new PixelColor(220, 100, 50);
        float hueShift = 30f;

        // Act
        var ramp = ColorRampGenerator.GenerateRamp(new ColorRampSettings(baseColor, stepCount: 5, hueShiftDegrees: hueShift));

        // Assert
        ColorSpaceHelper.ToHsv(ramp[0], out float shadowH, out _, out _);
        ColorSpaceHelper.ToHsv(ramp[^1], out float lightH, out _, out _);

        // Total hue delta should be approximately 2 * hueShift
        float diff = Math.Abs(lightH - shadowH);
        if (diff > 180f) diff = 360f - diff;
        diff.Should().BeGreaterThan(15f, "Hue shifting must differentiate shadows and highlights");
    }

    [Theory]
    [InlineData(RampPresetType.Sunlight)]
    [InlineData(RampPresetType.Fire)]
    [InlineData(RampPresetType.Ice)]
    [InlineData(RampPresetType.Toxic)]
    [InlineData(RampPresetType.MetallicGold)]
    public void GeneratePreset_ShouldReturnValidRamps(RampPresetType preset)
    {
        // Act
        var ramp = ColorRampGenerator.GeneratePreset(preset, stepCount: 5);

        // Assert
        ramp.Should().HaveCount(5);
        ramp[0].A.Should().Be(255);
    }
}

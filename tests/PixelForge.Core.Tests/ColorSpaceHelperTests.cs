using FluentAssertions;
using PixelForge.Core.Models;
using PixelForge.Core.Utils;
using Xunit;

namespace PixelForge.Core.Tests;

public class ColorSpaceHelperTests
{
    [Theory]
    [InlineData(255, 0, 0, 0f, 1f, 1f)]         // Pure Red
    [InlineData(0, 255, 0, 120f, 1f, 1f)]       // Pure Green
    [InlineData(0, 0, 255, 240f, 1f, 1f)]       // Pure Blue
    [InlineData(255, 255, 255, 0f, 0f, 1f)]     // White
    [InlineData(0, 0, 0, 0f, 0f, 0f)]           // Black
    public void ToHsv_ShouldMatchExpectedValues(byte r, byte g, byte b, float expectedH, float expectedS, float expectedV)
    {
        // Arrange
        var color = new PixelColor(r, g, b);

        // Act
        ColorSpaceHelper.ToHsv(color, out float h, out float s, out float v);

        // Assert
        h.Should().BeApproximately(expectedH, 1.0f);
        s.Should().BeApproximately(expectedS, 0.05f);
        v.Should().BeApproximately(expectedV, 0.05f);
    }

    [Fact]
    public void RoundTrip_HsvToRgbAndBack_ShouldPreserveColor()
    {
        // Arrange
        var original = new PixelColor(210, 85, 45, 255);

        // Act
        ColorSpaceHelper.ToHsv(original, out float h, out float s, out float v);
        var reconstructed = ColorSpaceHelper.FromHsv(h, s, v, original.A);

        // Assert
        reconstructed.R.Should().BeCloseTo(original.R, 2);
        reconstructed.G.Should().BeCloseTo(original.G, 2);
        reconstructed.B.Should().BeCloseTo(original.B, 2);
        reconstructed.A.Should().Be(original.A);
    }
}

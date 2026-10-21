using FluentAssertions;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Tools;
using Xunit;

namespace PixelForge.Core.Tests;

public class MultiStopGradientTests
{
    [Fact]
    public void EvaluateColor_WithEmptyStops_ShouldFallbackToPrimaryAndSecondary()
    {
        // Arrange
        var primary = PixelColor.Red;
        var secondary = PixelColor.Blue;

        // Act
        var atStart = GradientTool.EvaluateColor(null, 0f, DitherPattern.None, 0, 0, primary, secondary);
        var atEnd = GradientTool.EvaluateColor(null, 1f, DitherPattern.None, 0, 0, primary, secondary);
        var atMid = GradientTool.EvaluateColor(null, 0.5f, DitherPattern.None, 0, 0, primary, secondary);

        // Assert
        atStart.Should().Be(primary);
        atEnd.Should().Be(secondary);
        atMid.R.Should().BeCloseTo(128, 2);
        atMid.B.Should().BeCloseTo(128, 2);
    }

    [Fact]
    public void EvaluateColor_WithThreeStops_ShouldInterpolateBetweenSegments()
    {
        // Arrange
        var stops = new[]
        {
            new GradientStop(0.0f, PixelColor.Black),
            new GradientStop(0.5f, PixelColor.Red),
            new GradientStop(1.0f, PixelColor.White)
        };

        // Act & Assert
        // At 0.0 -> Black
        GradientTool.EvaluateColor(stops, 0f, DitherPattern.None, 0, 0, PixelColor.Green, PixelColor.Green)
            .Should().Be(PixelColor.Black);

        // At 0.5 -> Red
        GradientTool.EvaluateColor(stops, 0.5f, DitherPattern.None, 0, 0, PixelColor.Green, PixelColor.Green)
            .Should().Be(PixelColor.Red);

        // At 1.0 -> White
        GradientTool.EvaluateColor(stops, 1.0f, DitherPattern.None, 0, 0, PixelColor.Green, PixelColor.Green)
            .Should().Be(PixelColor.White);

        // At 0.25 -> halfway between Black and Red
        var mid1 = GradientTool.EvaluateColor(stops, 0.25f, DitherPattern.None, 0, 0, PixelColor.Green, PixelColor.Green);
        mid1.R.Should().BeCloseTo(128, 2);
        mid1.G.Should().Be(0);

        // At 0.75 -> halfway between Red and White
        var mid2 = GradientTool.EvaluateColor(stops, 0.75f, DitherPattern.None, 0, 0, PixelColor.Green, PixelColor.Green);
        mid2.R.Should().Be(255);
        mid2.G.Should().BeCloseTo(128, 2);
    }

    [Fact]
    public void ApplyGradient_WithMultiStopPreset_ShouldUpdateLayerPixels()
    {
        // Arrange
        var canvas = new PixelCanvas(10, 1);
        var layer = canvas.Layers[0];
        var history = new UndoRedoService();
        var ctx = new ToolContext(canvas, layer, history, PixelColor.Black, PixelColor.White);

        var tool = new GradientTool
        {
            Type = GradientType.Linear,
            Stops = new[]
            {
                new GradientStop(0.0f, PixelColor.Red),
                new GradientStop(0.5f, PixelColor.Green),
                new GradientStop(1.0f, PixelColor.Blue)
            }
        };

        // Act - draw horizontal gradient from x=0 to x=9
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerUp(ctx, 9, 0);

        // Assert
        layer.GetPixel(0, 0).Should().Be(PixelColor.Red);
        layer.GetPixel(9, 0).Should().Be(PixelColor.Blue);
        layer.GetPixel(4, 0).G.Should().BeGreaterThan(150); // Near green midstop
        history.CanUndo.Should().BeTrue();
    }
}

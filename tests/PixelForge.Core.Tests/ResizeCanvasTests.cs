using FluentAssertions;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using Xunit;

namespace PixelForge.Core.Tests;

public class ResizeCanvasTests
{
    [Fact]
    public void ResizeCanvasCommand_ExecutesAndUndoes_PreservesOriginalData()
    {
        // Arrange
        var canvas = new PixelCanvas(4, 4);
        var layer = canvas.ActiveLayer!;
        var red = new PixelColor(255, 0, 0, 255);
        layer.SetPixel(1, 1, red);

        bool callbackFired = false;
        var cmd = new ResizeCanvasCommand(canvas, 8, 8, CanvasAnchor.Center, ResizeMode.Anchor, () => callbackFired = true);

        // Act - Execute
        cmd.Execute();

        // Assert Execute
        canvas.Width.Should().Be(8);
        canvas.Height.Should().Be(8);
        layer.Width.Should().Be(8);
        layer.Height.Should().Be(8);
        callbackFired.Should().BeTrue();

        // In 4x4 to 8x8 with Center anchor: ox = (8 - 4) / 2 = 2, oy = (8 - 4) / 2 = 2
        // Pixel at (1, 1) should now be at (1 + 2, 1 + 2) = (3, 3)
        layer.GetPixel(3, 3).Should().Be(red);
        layer.GetPixel(0, 0).Should().Be(PixelColor.Transparent);

        // Act - Undo
        callbackFired = false;
        cmd.Undo();

        // Assert Undo
        canvas.Width.Should().Be(4);
        canvas.Height.Should().Be(4);
        layer.Width.Should().Be(4);
        layer.Height.Should().Be(4);
        layer.GetPixel(1, 1).Should().Be(red);
        callbackFired.Should().BeTrue();
    }

    [Fact]
    public void ResizeCanvasCommand_WithAnchorTopLeft_PlacesArtworkAtTopLeft()
    {
        // Arrange
        var canvas = new PixelCanvas(2, 2);
        var layer = canvas.ActiveLayer!;
        var blue = new PixelColor(0, 0, 255, 255);
        layer.SetPixel(0, 0, blue);

        var cmd = new ResizeCanvasCommand(canvas, 6, 6, CanvasAnchor.TopLeft, ResizeMode.Anchor);

        // Act
        cmd.Execute();

        // Assert: offset is (0, 0)
        layer.GetPixel(0, 0).Should().Be(blue);
        layer.GetPixel(5, 5).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void ResizeCanvasCommand_WithScaleNearestNeighbor_ScalesPixelsAccurately()
    {
        // Arrange
        var canvas = new PixelCanvas(2, 2);
        var layer = canvas.ActiveLayer!;
        var yellow = new PixelColor(255, 255, 0, 255);
        layer.SetPixel(0, 0, yellow);

        var cmd = new ResizeCanvasCommand(canvas, 4, 4, CanvasAnchor.Center, ResizeMode.ScaleNearestNeighbor);

        // Act
        cmd.Execute();

        // Assert: 2x2 scaled 2x to 4x4 means (0, 0), (0, 1), (1, 0), (1, 1) are yellow
        layer.GetPixel(0, 0).Should().Be(yellow);
        layer.GetPixel(1, 0).Should().Be(yellow);
        layer.GetPixel(0, 1).Should().Be(yellow);
        layer.GetPixel(1, 1).Should().Be(yellow);
        layer.GetPixel(2, 2).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void ResizeCanvasCommand_WhenCropping_ClipsPixelsCorrectlyAndRestoresOnUndo()
    {
        // Arrange
        var canvas = new PixelCanvas(8, 8);
        var layer = canvas.ActiveLayer!;
        var white = new PixelColor(255, 255, 255, 255);
        layer.SetPixel(0, 0, white); // will be clipped when cropped with center anchor
        layer.SetPixel(4, 4, white); // center pixel, should remain

        // Act: crop from 8x8 to 4x4 with Center anchor (ox = -2, oy = -2)
        var cmd = new ResizeCanvasCommand(canvas, 4, 4, CanvasAnchor.Center, ResizeMode.Anchor);
        cmd.Execute();

        // Assert
        canvas.Width.Should().Be(4);
        canvas.Height.Should().Be(4);
        layer.GetPixel(2, 2).Should().Be(white); // old (4, 4) + (-2, -2) = (2, 2)

        // Act - Undo
        cmd.Undo();

        // Assert
        canvas.Width.Should().Be(8);
        canvas.Height.Should().Be(8);
        layer.GetPixel(0, 0).Should().Be(white);
        layer.GetPixel(4, 4).Should().Be(white);
    }

    [Fact]
    public void FlipLayerCommand_HorizontalAndVertical_FlipsAccuratelyAndRevertsOnUndo()
    {
        // Arrange
        var layer = new Layer("Test Layer", 4, 4);
        var green = new PixelColor(0, 255, 0, 255);
        layer.SetPixel(0, 0, green);

        var flipH = new FlipLayerCommand(layer, FlipDirection.Horizontal);

        // Act H
        flipH.Execute();
        layer.GetPixel(3, 0).Should().Be(green);
        layer.GetPixel(0, 0).Should().Be(PixelColor.Transparent);

        // Act V
        var flipV = new FlipLayerCommand(layer, FlipDirection.Vertical);
        flipV.Execute();
        layer.GetPixel(3, 3).Should().Be(green);

        // Undo V
        flipV.Undo();
        layer.GetPixel(3, 0).Should().Be(green);

        // Undo H
        flipH.Undo();
        layer.GetPixel(0, 0).Should().Be(green);
    }

    [Fact]
    public void ResizeCanvasCommand_ThrowsOnInvalidDimensions()
    {
        var canvas = new PixelCanvas(16, 16);

        var actZero = () => new ResizeCanvasCommand(canvas, 0, 16);
        actZero.Should().Throw<ArgumentOutOfRangeException>();

        var actNeg = () => new ResizeCanvasCommand(canvas, 16, -5);
        actNeg.Should().Throw<ArgumentOutOfRangeException>();
    }
}

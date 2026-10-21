using FluentAssertions;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Tools;
using Xunit;

namespace PixelForge.Core.Tests;

public class MagicWandTests
{
    private static ToolContext CreateContext(int width = 16, int height = 16)
    {
        var canvas = new PixelCanvas(width, height);
        var history = new UndoRedoService();
        return new ToolContext(canvas, canvas.ActiveLayer!, history, PixelColor.Red, PixelColor.Black);
    }

    [Fact]
    public void MagicWandTool_Metadata_ReturnsExpectedDefaults()
    {
        // Arrange
        var tool = new MagicWandTool();

        // Act & Assert
        tool.Name.Should().Be("Magic Wand");
        tool.Shortcut.Should().Be("W");
        tool.IconKey.Should().Be("IconMagicWand");
        tool.Tolerance.Should().Be(0);
        tool.Contiguous.Should().BeTrue();
    }

    [Fact]
    public void MagicWandTool_GetPreviewPixels_ReturnsEmptyList()
    {
        // Arrange
        var ctx = CreateContext();
        var tool = new MagicWandTool();

        // Act
        var preview = tool.GetPreviewPixels(ctx);

        // Assert
        preview.Should().BeEmpty();
    }

    [Fact]
    public void MagicWandTool_ContiguousMode_SelectsConnectedAreaOnly()
    {
        // Arrange
        var ctx = CreateContext(10, 10);
        var layer = ctx.ActiveLayer;

        // Draw a 3x3 blue square at (2, 2)
        for (int y = 2; y <= 4; y++)
        {
            for (int x = 2; x <= 4; x++)
            {
                layer.SetPixel(x, y, PixelColor.Blue);
            }
        }

        // Draw a separate disconnected blue pixel at (8, 8)
        layer.SetPixel(8, 8, PixelColor.Blue);

        var tool = new MagicWandTool(tolerance: 0, contiguous: true);

        // Act - Click inside the 3x3 square
        tool.OnPointerDown(ctx, 3, 3);

        // Assert
        ctx.Selection.IsActive.Should().BeTrue();
        ctx.Selection.Bounds.Left.Should().Be(2);
        ctx.Selection.Bounds.Top.Should().Be(2);
        ctx.Selection.Bounds.Right.Should().Be(5);
        ctx.Selection.Bounds.Bottom.Should().Be(5);
        ctx.Selection.Bounds.Width.Should().Be(3);
        ctx.Selection.Bounds.Height.Should().Be(3);

        // 9 pixels in the square, the isolated pixel at (8, 8) must NOT be selected
        tool.SelectedPixels.Should().HaveCount(9);
        tool.SelectedPixels.Should().NotContain((8, 8));
        tool.SelectedPixels.Should().Contain((2, 2));
        tool.SelectedPixels.Should().Contain((4, 4));
    }

    [Fact]
    public void MagicWandTool_GlobalMode_SelectsAllMatchingPixelsAcrossLayer()
    {
        // Arrange
        var ctx = CreateContext(10, 10);
        var layer = ctx.ActiveLayer;

        // Draw first island of blue pixels
        layer.SetPixel(1, 1, PixelColor.Blue);
        layer.SetPixel(1, 2, PixelColor.Blue);

        // Draw second disconnected island of blue pixels
        layer.SetPixel(7, 7, PixelColor.Blue);
        layer.SetPixel(8, 7, PixelColor.Blue);

        var tool = new MagicWandTool(tolerance: 0, contiguous: false);

        // Act
        tool.OnPointerDown(ctx, 1, 1);

        // Assert
        ctx.Selection.IsActive.Should().BeTrue();
        tool.SelectedPixels.Should().HaveCount(4);
        tool.SelectedPixels.Should().Contain((1, 1));
        tool.SelectedPixels.Should().Contain((1, 2));
        tool.SelectedPixels.Should().Contain((7, 7));
        tool.SelectedPixels.Should().Contain((8, 7));

        // Bounds encompass both islands: (1, 1) to (8, 7)
        ctx.Selection.Bounds.Left.Should().Be(1);
        ctx.Selection.Bounds.Top.Should().Be(1);
        ctx.Selection.Bounds.Right.Should().Be(9);
        ctx.Selection.Bounds.Bottom.Should().Be(8);
    }

    [Fact]
    public void MagicWandTool_Tolerance_SelectsPixelsWithinColorDistance()
    {
        // Arrange
        var ctx = CreateContext(10, 10);
        var layer = ctx.ActiveLayer;

        var baseRed = new PixelColor(255, 0, 0, 255);
        var nearRed = new PixelColor(245, 5, 5, 255); // distance ~ 12
        var farGreen = new PixelColor(0, 255, 0, 255);

        layer.SetPixel(2, 2, baseRed);
        layer.SetPixel(2, 3, nearRed);
        layer.SetPixel(2, 4, farGreen);

        var strictTool = new MagicWandTool(tolerance: 0, contiguous: true);
        var tolerantTool = new MagicWandTool(tolerance: 20, contiguous: true);

        // Act 1: Strict tolerance (0) -> should only select (2, 2)
        strictTool.OnPointerDown(ctx, 2, 2);
        var strictSelected = strictTool.SelectedPixels.ToList();

        // Act 2: Tolerant tool (20) -> should select (2, 2) and (2, 3) but not (2, 4)
        tolerantTool.OnPointerDown(ctx, 2, 2);
        var tolerantSelected = tolerantTool.SelectedPixels.ToList();

        // Assert
        strictSelected.Should().ContainSingle().Which.Should().Be((2, 2));
        tolerantSelected.Should().HaveCount(2);
        tolerantSelected.Should().Contain((2, 2));
        tolerantSelected.Should().Contain((2, 3));
        tolerantSelected.Should().NotContain((2, 4));
    }

    [Fact]
    public void MagicWandTool_OutOfBounds_ClearsSelectionWithoutThrowing()
    {
        // Arrange
        var ctx = CreateContext(10, 10);
        var tool = new MagicWandTool();

        // Act
        tool.OnPointerDown(ctx, -1, 5);

        // Assert
        ctx.Selection.IsActive.Should().BeFalse();
        tool.SelectedPixels.Should().BeEmpty();

        // Act 2
        tool.OnPointerDown(ctx, 20, 20);

        // Assert
        ctx.Selection.IsActive.Should().BeFalse();
        tool.SelectedPixels.Should().BeEmpty();
    }

    [Fact]
    public void MagicWandTool_Select_DirectMethodCall_ReturnsPointsAndUpdatesSelection()
    {
        // Arrange
        var layer = new Layer("Test", 8, 8);
        layer.SetPixel(3, 3, PixelColor.Green);
        layer.SetPixel(3, 4, PixelColor.Green);
        var selection = new SelectionModel();
        var tool = new MagicWandTool();

        // Act
        var points = tool.Select(layer, 3, 3, tolerance: 0, contiguous: true, selection);

        // Assert
        points.Should().HaveCount(2);
        selection.IsActive.Should().BeTrue();
        selection.Bounds.Width.Should().Be(1);
        selection.Bounds.Height.Should().Be(2);
    }

    [Fact]
    public void MagicWandTool_SelectionPixelsBuffer_PopulatedWithLayerColorsAndTransparency()
    {
        // Arrange
        var ctx = CreateContext(6, 6);
        var layer = ctx.ActiveLayer;

        // Place red pixels at (1, 1) and (1, 3) - disconnected vertically
        layer.SetPixel(1, 1, PixelColor.Red);
        layer.SetPixel(1, 2, PixelColor.Yellow); // different color in between
        layer.SetPixel(1, 3, PixelColor.Red);

        var tool = new MagicWandTool(tolerance: 0, contiguous: false);

        // Act
        tool.OnPointerDown(ctx, 1, 1);

        // Assert
        ctx.Selection.IsActive.Should().BeTrue();
        // Bounds: Left=1, Top=1, Right=2, Bottom=4 (width=1, height=3)
        ctx.Selection.Bounds.Width.Should().Be(1);
        ctx.Selection.Bounds.Height.Should().Be(3);

        // (1, 1) -> index 0: Red
        // (1, 2) -> index 1: Transparent (not selected!)
        // (1, 3) -> index 2: Red
        ctx.Selection.Pixels[0].Should().Be(PixelColor.Red);
        ctx.Selection.Pixels[1].Should().Be(PixelColor.Transparent);
        ctx.Selection.Pixels[2].Should().Be(PixelColor.Red);
    }

    [Fact]
    public void MagicWandTool_WithSymmetry_SeedsFromSymmetricPoints()
    {
        // Arrange
        var ctx = CreateContext(8, 8);
        ctx.SymmetryX = true;
        ctx.SymmetryAxisX = 4; // center vertical mirror

        var layer = ctx.ActiveLayer;
        // Symmetric pair: (2, 2) and (5, 2)
        layer.SetPixel(2, 2, PixelColor.Purple);
        layer.SetPixel(5, 2, PixelColor.Purple);

        var tool = new MagicWandTool(tolerance: 0, contiguous: true);

        // Act - click at (2, 2)
        tool.OnPointerDown(ctx, 2, 2);

        // Assert
        tool.SelectedPixels.Should().HaveCount(2);
        tool.SelectedPixels.Should().Contain((2, 2));
        tool.SelectedPixels.Should().Contain((5, 2));
    }

    [Fact]
    public void MagicWandTool_StampsFloatingSelectionBeforeNewSelection()
    {
        // Arrange
        var ctx = CreateContext(10, 10);
        var layer = ctx.ActiveLayer;

        // Create a floating selection with Blue pixel at offset (5, 5)
        ctx.Selection.Select(new SkiaSharp.SKRectI(0, 0, 1, 1), new[] { PixelColor.Blue });
        ctx.Selection.IsFloating = true;
        ctx.Selection.OffsetX = 5;
        ctx.Selection.OffsetY = 5;

        // Active layer has red at (1, 1)
        layer.SetPixel(1, 1, PixelColor.Red);

        var tool = new MagicWandTool(tolerance: 0, contiguous: true);

        // Act - Click at (1, 1)
        tool.OnPointerDown(ctx, 1, 1);

        // Assert
        // The floating blue pixel should have been stamped at (5, 5) on the layer
        layer.GetPixel(5, 5).Should().Be(PixelColor.Blue);
        // And now (1, 1) is selected
        tool.SelectedPixels.Should().ContainSingle().Which.Should().Be((1, 1));
        ctx.Selection.IsFloating.Should().BeFalse();
    }
}

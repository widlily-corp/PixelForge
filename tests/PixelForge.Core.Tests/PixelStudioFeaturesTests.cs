using FluentAssertions;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Tools;
using SkiaSharp;
using Xunit;

namespace PixelForge.Core.Tests;

public class PixelStudioFeaturesTests
{
    private static ToolContext CreateContext(int width = 16, int height = 16)
    {
        var canvas = new PixelCanvas(width, height);
        var history = new UndoRedoService();
        return new ToolContext(canvas, canvas.ActiveLayer!, history, PixelColor.Black, PixelColor.White);
    }

    #region 1. Dithering & Patterns Tests

    [Fact]
    public void DitherHelper_Checker50_AlternatesPixels()
    {
        // Arrange & Act & Assert
        DitherHelper.ShouldPlot(0, 0, DitherPattern.Checker50).Should().BeTrue();
        DitherHelper.ShouldPlot(1, 0, DitherPattern.Checker50).Should().BeFalse();
        DitherHelper.ShouldPlot(0, 1, DitherPattern.Checker50).Should().BeFalse();
        DitherHelper.ShouldPlot(1, 1, DitherPattern.Checker50).Should().BeTrue();
    }

    [Fact]
    public void DitherHelper_Bayer4x4_RespectsThreshold()
    {
        // At threshold 0.0f, no pixel should plot
        DitherHelper.ShouldPlot(0, 0, DitherPattern.Bayer4x4, 0f).Should().BeFalse();

        // At threshold 1.0f, all pixels should plot
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                DitherHelper.ShouldPlot(x, y, DitherPattern.Bayer4x4, 1.0f).Should().BeTrue();
            }
        }

        // At threshold 0.5f, exactly 8 of 16 pixels in the 4x4 block should plot
        int count = 0;
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                if (DitherHelper.ShouldPlot(x, y, DitherPattern.Bayer4x4, 0.5f)) count++;
            }
        }
        count.Should().Be(8);
    }

    [Fact]
    public void DitherBrushTool_DrawsDitheredStroke_AndRestoresOnUndo()
    {
        // Arrange
        var ctx = CreateContext(8, 8);
        ctx.ActiveDitherPattern = DitherPattern.Checker50;
        var tool = new DitherBrushTool();

        // Act
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerMove(ctx, 1, 0);
        tool.OnPointerUp(ctx, 1, 0);

        // Assert: (0, 0) should be Primary (Black), (1, 0) should be Secondary (White)
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Black);
        ctx.ActiveLayer.GetPixel(1, 0).Should().Be(PixelColor.White);

        // Undo
        ctx.History.Undo();
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(1, 0).Should().Be(PixelColor.Transparent);
    }

    #endregion

    #region 2. Pixel Gradients Tests

    [Fact]
    public void GradientTool_LinearSmoothGradient_FillsLinearly()
    {
        // Arrange
        var ctx = CreateContext(8, 1);
        ctx.PrimaryColor = PixelColor.Black;
        ctx.SecondaryColor = PixelColor.White;
        ctx.ActiveDitherPattern = DitherPattern.None;

        var tool = new GradientTool { Type = GradientType.Linear };

        // Act: drag across the 8-pixel width
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerMove(ctx, 7, 0);
        tool.OnPointerUp(ctx, 7, 0);

        // Assert: start should be Black, end should be White, middle should be Gray
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Black);
        ctx.ActiveLayer.GetPixel(7, 0).Should().Be(PixelColor.White);
        ctx.ActiveLayer.GetPixel(3, 0).R.Should().BeInRange(90, 140);

        // Undo
        ctx.History.Undo();
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void GradientTool_LinearDitheredGradient_UsesBayerPattern()
    {
        // Arrange
        var ctx = CreateContext(16, 16);
        ctx.PrimaryColor = PixelColor.Black;
        ctx.SecondaryColor = PixelColor.White;
        ctx.ActiveDitherPattern = DitherPattern.Bayer4x4;

        var tool = new GradientTool { Type = GradientType.Linear };

        // Act
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerUp(ctx, 15, 0);

        // Assert: all pixels should be strictly Black or White (retro 1-bit dithering)
        for (int x = 0; x < 16; x++)
        {
            var c = ctx.ActiveLayer.GetPixel(x, 0);
            (c == PixelColor.Black || c == PixelColor.White).Should().BeTrue();
        }
    }

    [Fact]
    public void GradientTool_RadialGradient_FillsFromCenterOutward()
    {
        // Arrange
        var ctx = CreateContext(9, 9);
        ctx.PrimaryColor = PixelColor.Black;
        ctx.SecondaryColor = PixelColor.White;
        ctx.ActiveDitherPattern = DitherPattern.None;

        var tool = new GradientTool { Type = GradientType.Radial };

        // Act: Center at (4, 4), outer edge at (4, 8)
        tool.OnPointerDown(ctx, 4, 4);
        tool.OnPointerUp(ctx, 4, 8);

        // Assert: Center is Black, distance >= 4 is White
        ctx.ActiveLayer.GetPixel(4, 4).Should().Be(PixelColor.Black);
        ctx.ActiveLayer.GetPixel(4, 8).Should().Be(PixelColor.White);
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.White);
    }

    [Fact]
    public void GradientTool_WithSelection_OnlyFillsInsideSelection()
    {
        // Arrange
        var ctx = CreateContext(16, 16);
        ctx.Selection.Select(new SKRectI(4, 4, 12, 12));
        var tool = new GradientTool { Type = GradientType.Linear };

        // Act
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerUp(ctx, 15, 15);

        // Assert: Outside selection must remain Transparent
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(15, 15).Should().Be(PixelColor.Transparent);

        // Inside selection must be filled
        ctx.ActiveLayer.GetPixel(4, 4).Should().NotBe(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(10, 10).Should().NotBe(PixelColor.Transparent);
    }

    #endregion

    #region 3. Flood Fill Threshold & Contiguous Tests

    [Fact]
    public void FillTool_WithThreshold_FillsSimilarShades()
    {
        // Arrange: Dark gray (30) next to black (0), and white (255) further away
        var ctx = CreateContext(4, 4);
        var black = new PixelColor(0, 0, 0, 255);
        var nearBlack = new PixelColor(25, 25, 25, 255);
        var white = new PixelColor(255, 255, 255, 255);

        ctx.ActiveLayer.SetPixel(0, 0, black);
        ctx.ActiveLayer.SetPixel(1, 0, nearBlack);
        ctx.ActiveLayer.SetPixel(2, 0, white);

        ctx.PrimaryColor = PixelColor.Red;
        ctx.FillTolerance = 50; // Tolerance allows nearBlack (distance = ~43) but not white (distance = ~441)
        ctx.FillContiguous = true;

        var tool = new FillTool();

        // Act
        tool.OnPointerDown(ctx, 0, 0);

        // Assert
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(1, 0).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(2, 0).Should().Be(white); // Too distant, remained white
    }

    [Fact]
    public void FillTool_GlobalColorReplace_ReplacesNonContiguousPixels()
    {
        // Arrange: Separate islands of blue
        var ctx = CreateContext(8, 8);
        var blue = PixelColor.Blue;
        ctx.ActiveLayer.SetPixel(0, 0, blue);
        ctx.ActiveLayer.SetPixel(7, 7, blue);
        ctx.ActiveLayer.SetPixel(3, 3, PixelColor.Green);

        ctx.PrimaryColor = PixelColor.Yellow;
        ctx.FillTolerance = 0;
        ctx.FillContiguous = false; // Global replace

        var tool = new FillTool();

        // Act: click at (0, 0)
        tool.OnPointerDown(ctx, 0, 0);

        // Assert: both islands of blue should be replaced with Yellow
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Yellow);
        ctx.ActiveLayer.GetPixel(7, 7).Should().Be(PixelColor.Yellow);
        ctx.ActiveLayer.GetPixel(3, 3).Should().Be(PixelColor.Green); // Unrelated color preserved
    }

    #endregion

    #region 4. Flip & Rotate Tests

    [Fact]
    public void FlipLayerCommand_WithSelection_OnlyFlipsInsideSelection()
    {
        // Arrange: 8x8 layer with red at (2, 2) and green at (6, 6)
        var layer = new Layer("Layer", 8, 8);
        layer.SetPixel(2, 2, PixelColor.Red);
        layer.SetPixel(6, 6, PixelColor.Green);

        // Selection covering (1, 1) to (4, 4)
        var selRect = new SKRectI(1, 1, 4, 4); // width 3, height 3: cols 1, 2, 3
        var cmd = new FlipLayerCommand(layer, FlipDirection.Horizontal, null, selRect);

        // Act
        cmd.Execute();

        // Assert: Inside selection (cols 1..3), col 2 mirrored in [1, 2, 3] is col 2
        // Let's test with rect (1, 1) to (5, 5): cols 1, 2, 3, 4 (width 4)
        var selRect4 = new SKRectI(1, 1, 5, 5);
        var cmd4 = new FlipLayerCommand(layer, FlipDirection.Horizontal, null, selRect4);
        cmd4.Execute();

        // Pixel outside selection (6, 6) must remain green
        layer.GetPixel(6, 6).Should().Be(PixelColor.Green);

        // Undo
        cmd4.Undo();
        layer.GetPixel(6, 6).Should().Be(PixelColor.Green);
    }

    [Fact]
    public void FlipCanvasCommand_FlipsAllLayers_AndRevertsOnUndo()
    {
        // Arrange
        var canvas = new PixelCanvas(4, 4);
        var l1 = canvas.ActiveLayer!;
        var l2 = canvas.AddLayer("Layer 2");

        l1.SetPixel(0, 0, PixelColor.Red);
        l2.SetPixel(0, 0, PixelColor.Blue);

        var cmd = new FlipCanvasCommand(canvas, FlipDirection.Horizontal);

        // Act
        cmd.Execute();

        // Assert: (0, 0) should now be at (3, 0) on both layers
        l1.GetPixel(3, 0).Should().Be(PixelColor.Red);
        l1.GetPixel(0, 0).Should().Be(PixelColor.Transparent);
        l2.GetPixel(3, 0).Should().Be(PixelColor.Blue);
        l2.GetPixel(0, 0).Should().Be(PixelColor.Transparent);

        // Undo
        cmd.Undo();
        l1.GetPixel(0, 0).Should().Be(PixelColor.Red);
        l2.GetPixel(0, 0).Should().Be(PixelColor.Blue);
    }

    [Fact]
    public void RotateLayerCommand_Square_Rotates90CW_CCW_And180()
    {
        // Arrange: 4x4 square layer with a point at top-left (0, 0)
        var layer = new Layer("Square Layer", 4, 4);
        layer.SetPixel(0, 0, PixelColor.Red);

        // Act & Assert 90 CW: (0, 0) -> (3, 0)
        var rotCW = new RotateLayerCommand(layer, RotationAngle.Rotate90CW);
        rotCW.Execute();
        layer.GetPixel(3, 0).Should().Be(PixelColor.Red);
        layer.GetPixel(0, 0).Should().Be(PixelColor.Transparent);

        // Act & Assert 180: (3, 0) -> (0, 3)
        var rot180 = new RotateLayerCommand(layer, RotationAngle.Rotate180);
        rot180.Execute();
        layer.GetPixel(0, 3).Should().Be(PixelColor.Red);

        // Undo 180
        rot180.Undo();
        layer.GetPixel(3, 0).Should().Be(PixelColor.Red);

        // Undo 90 CW
        rotCW.Undo();
        layer.GetPixel(0, 0).Should().Be(PixelColor.Red);
    }

    [Fact]
    public void RotateCanvasCommand_Rectangular_SwapsDimensionsAndRotatesLayers()
    {
        // Arrange: 8 wide x 4 high canvas
        var canvas = new PixelCanvas(8, 4);
        var layer = canvas.ActiveLayer!;
        layer.SetPixel(0, 0, PixelColor.Red); // Top-left of 8x4

        var cmdCW = new RotateCanvasCommand(canvas, RotationAngle.Rotate90CW);

        // Act
        cmdCW.Execute();

        // Assert: canvas dimensions should now be 4 wide x 8 high!
        canvas.Width.Should().Be(4);
        canvas.Height.Should().Be(8);
        layer.Width.Should().Be(4);
        layer.Height.Should().Be(8);

        // In 90 CW: (0, 0) of 8x4 maps to (3, 0) of 4x8 (new width 4 - 1 - 0 = 3)
        layer.GetPixel(3, 0).Should().Be(PixelColor.Red);
        layer.GetPixel(0, 0).Should().Be(PixelColor.Transparent);

        // Act: Undo
        cmdCW.Undo();

        // Assert: restored to 8x4
        canvas.Width.Should().Be(8);
        canvas.Height.Should().Be(4);
        layer.Width.Should().Be(8);
        layer.Height.Should().Be(4);
        layer.GetPixel(0, 0).Should().Be(PixelColor.Red);
    }

    #endregion
}

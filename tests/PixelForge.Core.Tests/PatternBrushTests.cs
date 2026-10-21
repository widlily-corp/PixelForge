using FluentAssertions;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Patterns;
using PixelForge.Core.Tools;
using Xunit;

namespace PixelForge.Core.Tests;

public class PatternBrushTests
{
    private static ToolContext CreateContext(int width = 16, int height = 16)
    {
        var canvas = new PixelCanvas(width, height);
        var history = new UndoRedoService();
        return new ToolContext(canvas, canvas.ActiveLayer!, history, PixelColor.Red, PixelColor.Blue);
    }

    #region 1. PatternPreset Tests

    [Fact]
    public void PatternPreset_IsPixelSet_HandlesPeriodicWrappingAndNegativeCoordinates()
    {
        // Arrange: Checkerboard has (0, 0) = 1 and (1, 0) = 0
        var preset = PatternPresets.Checkerboard;

        // Act & Assert positive wrapping (mod 8)
        preset.IsPixelSet(0, 0).Should().BeTrue();
        preset.IsPixelSet(8, 0).Should().BeTrue();
        preset.IsPixelSet(16, 8).Should().BeTrue();

        preset.IsPixelSet(1, 0).Should().BeFalse();
        preset.IsPixelSet(9, 0).Should().BeFalse();

        // Act & Assert negative wrapping
        preset.IsPixelSet(-8, 0).Should().BeTrue();
        preset.IsPixelSet(-1, -1).Should().BeTrue();  // (-1, -1) -> (7, 7) which in checkerboard row 7 col 7 is '1'
        preset.IsPixelSet(-2, -1).Should().BeFalse(); // (-2, -1) -> (6, 7) which in checkerboard row 7 col 6 is '0'
    }

    [Fact]
    public void PatternPreset_FromRows_ParsesMultipleForegroundCharacters()
    {
        // Arrange
        var preset = PatternPreset.FromRows("Custom",
            "10000000",
            "#0000000",
            "X0000000",
            "x0000000",
            "*0000000",
            "00000000",
            "00000000",
            "00000000");

        // Act & Assert
        preset.IsPixelSet(0, 0).Should().BeTrue();
        preset.IsPixelSet(0, 1).Should().BeTrue();
        preset.IsPixelSet(0, 2).Should().BeTrue();
        preset.IsPixelSet(0, 3).Should().BeTrue();
        preset.IsPixelSet(0, 4).Should().BeTrue();
        preset.IsPixelSet(0, 5).Should().BeFalse();
        preset.IsPixelSet(1, 0).Should().BeFalse();
    }

    [Fact]
    public void PatternPreset_FromRows_ThrowsOnInvalidInput()
    {
        // Less than 8 rows
        Action actShort = () => PatternPreset.FromRows("Test", "10101010");
        actShort.Should().Throw<ArgumentException>();

        // Null rows
        Action actNull = () => PatternPreset.FromRows("Test", null!);
        actNull.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PatternPreset_WithPixel_SetsAndClearsBits()
    {
        // Arrange
        var empty = PatternPreset.Empty;
        empty.IsPixelSet(3, 4).Should().BeFalse();

        // Act: set bit
        var modified = empty.WithPixel(3, 4, true);
        modified.IsPixelSet(3, 4).Should().BeTrue();

        // Act: clear bit
        var reverted = modified.WithPixel(3, 4, false);
        reverted.IsPixelSet(3, 4).Should().BeFalse();
    }

    [Fact]
    public void PatternPreset_Invert_FlipsAllBits()
    {
        // Arrange
        var empty = PatternPreset.Empty;
        var solid = PatternPreset.Solid;

        // Act & Assert
        empty.Invert().Mask.Should().Be(~0UL);
        solid.Invert().Mask.Should().Be(0UL);
    }

    #endregion

    #region 2. PatternPresets Catalog Tests

    [Fact]
    public void PatternPresets_All11RequiredPresets_AreDefinedAndNonZero()
    {
        // Assert all 11 required presets exist and have valid masks
        PatternPresets.Checkerboard.Mask.Should().NotBe(0UL);
        PatternPresets.Bricks.Mask.Should().NotBe(0UL);
        PatternPresets.Crosshatch.Mask.Should().NotBe(0UL);
        PatternPresets.DiagonalStripe.Mask.Should().NotBe(0UL);
        PatternPresets.DenseDots.Mask.Should().NotBe(0UL);
        PatternPresets.SparseDots.Mask.Should().NotBe(0UL);
        PatternPresets.Scales.Mask.Should().NotBe(0UL);
        PatternPresets.Weave.Mask.Should().NotBe(0UL);
        PatternPresets.Noise.Mask.Should().NotBe(0UL);
        PatternPresets.HorizontalLines.Mask.Should().NotBe(0UL);
        PatternPresets.VerticalLines.Mask.Should().NotBe(0UL);

        // Aliases
        PatternPresets.Horizontal.Should().Be(PatternPresets.HorizontalLines);
        PatternPresets.Vertical.Should().Be(PatternPresets.VerticalLines);

        PatternPresets.AllPresets.Should().HaveCount(11);
    }

    [Fact]
    public void PatternPresets_GetByName_FindsPresetsCaseInsensitively()
    {
        // Act & Assert
        PatternPresets.GetByName("checkerboard")?.Name.Should().Be("Checkerboard");
        PatternPresets.GetByName("BRICKS")?.Name.Should().Be("Bricks");
        PatternPresets.GetByName("horizontal")?.Name.Should().Be("Horizontal");
        PatternPresets.GetByName("Vertical")?.Name.Should().Be("Vertical");
        PatternPresets.GetByName("diagonal")?.Name.Should().Be("DiagonalStripe");
        PatternPresets.GetByName("nonexistent").Should().BeNull();
        PatternPresets.GetByName("").Should().BeNull();
    }

    #endregion

    #region 3. PatternBrushTool Core Painting Tests

    [Fact]
    public void PatternBrushTool_DefaultProperties_AreSetCorrectly()
    {
        // Arrange & Act
        var tool = new PatternBrushTool();

        // Assert
        tool.Name.Should().Be("Pattern Brush");
        tool.Shortcut.Should().Be("W");
        tool.IconKey.Should().Be("IconPatternBrush");
        tool.BrushSize.Should().Be(1);
        tool.Pattern.Should().Be(PatternPresets.Checkerboard);
        tool.UseSecondaryColor.Should().BeFalse();
        tool.PixelPerfect.Should().BeFalse();
        tool.StabilizerStrength.Should().Be(0);
        tool.GetPreviewPixels(CreateContext()).Should().BeEmpty();
    }

    [Fact]
    public void PatternBrushTool_SingleClick_StampsPatternPixel()
    {
        // Arrange: (0, 0) is foreground in Checkerboard, (1, 0) is background
        var ctx = CreateContext(8, 8);
        ctx.PrimaryColor = PixelColor.Red;
        var tool = new PatternBrushTool(PatternPresets.Checkerboard, brushSize: 1);

        // Act
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerUp(ctx, 0, 0);

        // Assert
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Red);
        ctx.History.CanUndo.Should().BeTrue();
    }

    [Fact]
    public void PatternBrushTool_WhenBitIsUnsetAndUseSecondaryColorFalse_LeavesPixelUntouched()
    {
        // Arrange: (1, 0) has unset bit in Checkerboard
        var ctx = CreateContext(8, 8);
        ctx.PrimaryColor = PixelColor.Red;
        var tool = new PatternBrushTool(PatternPresets.Checkerboard, brushSize: 1)
        {
            UseSecondaryColor = false
        };

        // Act: click on (1, 0)
        tool.OnPointerDown(ctx, 1, 0);
        tool.OnPointerUp(ctx, 1, 0);

        // Assert: pixel remains transparent
        ctx.ActiveLayer.GetPixel(1, 0).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void PatternBrushTool_WhenUseSecondaryColorTrue_PaintsBothColors()
    {
        // Arrange
        var ctx = CreateContext(8, 8);
        ctx.PrimaryColor = PixelColor.Red;
        ctx.SecondaryColor = PixelColor.Blue;
        var tool = new PatternBrushTool(PatternPresets.Checkerboard, brushSize: 1)
        {
            UseSecondaryColor = true
        };

        // Act: click at (0, 0) and (1, 0)
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerMove(ctx, 1, 0);
        tool.OnPointerUp(ctx, 1, 0);

        // Assert: (0, 0) is Red, (1, 0) is Blue
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(1, 0).Should().Be(PixelColor.Blue);
    }

    [Fact]
    public void PatternBrushTool_PointerMove_InterpolatesLineWithBresenham()
    {
        // Arrange: Horizontal line pattern
        var ctx = CreateContext(8, 8);
        ctx.PrimaryColor = PixelColor.Red;
        var tool = new PatternBrushTool(PatternPresets.HorizontalLines, brushSize: 1);

        // Act: draw vertical stroke from (0, 0) to (0, 3)
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerMove(ctx, 0, 3);
        tool.OnPointerUp(ctx, 0, 3);

        // Assert: HorizontalLines alternates rows: row 0 is 1, row 1 is 0, row 2 is 1, row 3 is 0
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(0, 1).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(0, 2).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(0, 3).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void PatternBrushTool_BrushSizeGreaterThanOne_StampsLargerFootprint()
    {
        // Arrange: Solid pattern to easily test footprint bounds
        var ctx = CreateContext(8, 8);
        ctx.PrimaryColor = PixelColor.Red;
        var tool = new PatternBrushTool(PatternPreset.Solid, brushSize: 3);

        // Act: click at center (3, 3) -> 3x3 stamp covers [2..4, 2..4]
        tool.OnPointerDown(ctx, 3, 3);
        tool.OnPointerUp(ctx, 3, 3);

        // Assert: center 3x3 block is Red
        for (int y = 2; y <= 4; y++)
        {
            for (int x = 2; x <= 4; x++)
            {
                ctx.ActiveLayer.GetPixel(x, y).Should().Be(PixelColor.Red);
            }
        }

        // Out of bounds is untouched
        ctx.ActiveLayer.GetPixel(1, 1).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(5, 5).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void PatternBrushTool_WithSymmetryX_MirrorsHorizontally()
    {
        // Arrange: 16x16 canvas, center axis at 8 -> symmetric X of 2 is 16 - 1 - 2 = 13
        var ctx = CreateContext(16, 16);
        ctx.SymmetryX = true;
        ctx.PrimaryColor = PixelColor.Red;
        var tool = new PatternBrushTool(PatternPreset.Solid, brushSize: 1);

        // Act
        tool.OnPointerDown(ctx, 2, 5);
        tool.OnPointerUp(ctx, 2, 5);

        // Assert: both (2, 5) and (13, 5) are painted
        ctx.ActiveLayer.GetPixel(2, 5).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(13, 5).Should().Be(PixelColor.Red);
    }

    [Fact]
    public void PatternBrushTool_WithSymmetryXY_MirrorsInFourQuadrants()
    {
        // Arrange
        var ctx = CreateContext(16, 16);
        ctx.SymmetryX = true;
        ctx.SymmetryY = true;
        ctx.PrimaryColor = PixelColor.Red;
        var tool = new PatternBrushTool(PatternPreset.Solid, brushSize: 1);

        // Act
        tool.OnPointerDown(ctx, 2, 3);
        tool.OnPointerUp(ctx, 2, 3);

        // Assert: 4 symmetric corners
        ctx.ActiveLayer.GetPixel(2, 3).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(13, 3).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(2, 12).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(13, 12).Should().Be(PixelColor.Red);
    }

    [Fact]
    public void PatternBrushTool_WithSelection_RestrictsPaintingInsideSelection()
    {
        // Arrange: selection box from (2, 2) to (4, 4)
        var ctx = CreateContext(8, 8);
        ctx.Selection.Select(new SkiaSharp.SKRectI(2, 2, 5, 5)); // contains (2,2) to (4,4)
        ctx.PrimaryColor = PixelColor.Red;
        var tool = new PatternBrushTool(PatternPreset.Solid, brushSize: 1);

        // Act: stroke from (0, 3) to (7, 3)
        tool.OnPointerDown(ctx, 0, 3);
        tool.OnPointerMove(ctx, 7, 3);
        tool.OnPointerUp(ctx, 7, 3);

        // Assert: only x in [2..4] is painted
        ctx.ActiveLayer.GetPixel(0, 3).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(1, 3).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(2, 3).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(3, 3).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(4, 3).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(5, 3).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(6, 3).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void PatternBrushTool_PixelPerfectMode_RemovesLCornerJaggies()
    {
        // Arrange: PixelPerfect enabled, Solid pattern, 1px brush
        var ctx = CreateContext(8, 8);
        ctx.PrimaryColor = PixelColor.Red;
        var tool = new PatternBrushTool(PatternPreset.Solid, brushSize: 1)
        {
            PixelPerfect = true
        };

        // Act: Draw L-corner sequence (0, 0) -> (1, 0) -> (1, 1)
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerMove(ctx, 1, 0);
        tool.OnPointerMove(ctx, 1, 1);
        tool.OnPointerUp(ctx, 1, 1);

        // Assert: (1, 0) was removed by pixel-perfect algorithm!
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(1, 0).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(1, 1).Should().Be(PixelColor.Red);
    }

    [Fact]
    public void PatternBrushTool_Stabilizer_SmoothsStrokeTrajectory()
    {
        // Arrange
        var ctx = CreateContext(32, 32);
        ctx.PrimaryColor = PixelColor.Red;
        var tool = new PatternBrushTool(PatternPreset.Solid, brushSize: 1)
        {
            StabilizerStrength = 8
        };

        // Act: pointer down at (0, 0), move to (20, 20)
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerMove(ctx, 20, 20);
        tool.OnPointerUp(ctx, 20, 20);

        // Assert: because of stabilizer smoothing lag, the stroke does not overshoot,
        // and initial (0, 0) is painted
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Red);
    }

    [Fact]
    public void PatternBrushTool_UndoRedo_ProperlyRestoresState()
    {
        // Arrange
        var ctx = CreateContext(8, 8);
        ctx.PrimaryColor = PixelColor.Red;
        var tool = new PatternBrushTool(PatternPreset.Solid, brushSize: 1);

        // Act
        tool.OnPointerDown(ctx, 3, 3);
        tool.OnPointerUp(ctx, 3, 3);

        ctx.ActiveLayer.GetPixel(3, 3).Should().Be(PixelColor.Red);

        // Undo
        ctx.History.Undo();
        ctx.ActiveLayer.GetPixel(3, 3).Should().Be(PixelColor.Transparent);

        // Redo
        ctx.History.Redo();
        ctx.ActiveLayer.GetPixel(3, 3).Should().Be(PixelColor.Red);
    }

    #endregion
}

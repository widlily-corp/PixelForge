using FluentAssertions;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Tools;
using Xunit;

namespace PixelForge.Core.Tests;

public class ToolTests
{
    private static ToolContext CreateContext(int width = 32, int height = 32)
    {
        var canvas = new PixelCanvas(width, height);
        var history = new UndoRedoService();
        return new ToolContext(canvas, canvas.ActiveLayer!, history, PixelColor.Red, PixelColor.Black);
    }

    [Fact]
    public void ToolContext_PropertiesAndEvents_WorkCorrectly()
    {
        // Arrange
        var ctx = CreateContext();
        PixelColor? pickedColor = null;
        ctx.PrimaryColorChanged += c => pickedColor = c;

        // Act
        ctx.PrimaryColor = PixelColor.Yellow;
        ctx.SecondaryColor = PixelColor.Green;
        var symPoints = ctx.GetSymmetricPoints(5, 5).ToList();

        // Assert
        pickedColor.Should().Be(PixelColor.Yellow);
        ctx.SecondaryColor.Should().Be(PixelColor.Green);
        symPoints.Should().ContainSingle();
    }

    [Fact]
    public void PencilTool_SingleClick_DrawsPixelAndCommitsToHistory()
    {
        // Arrange
        var ctx = CreateContext();
        var tool = new PencilTool();

        // Act
        tool.OnPointerDown(ctx, 5, 5);
        tool.OnPointerUp(ctx, 5, 5);

        // Assert
        ctx.ActiveLayer.GetPixel(5, 5).Should().Be(PixelColor.Red);
        ctx.History.CanUndo.Should().BeTrue();
    }

    [Fact]
    public void PencilTool_PointerMove_InterpolatesLineWithBresenham()
    {
        // Arrange
        var ctx = CreateContext();
        var tool = new PencilTool();

        // Act
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerMove(ctx, 2, 2);
        tool.OnPointerUp(ctx, 2, 2);

        // Assert
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(1, 1).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(2, 2).Should().Be(PixelColor.Red);
        tool.GetPreviewPixels(ctx).Should().BeEmpty();
    }

    [Fact]
    public void PencilTool_WithSymmetryX_DrawsMirroredPixels()
    {
        // Arrange
        var ctx = CreateContext(32, 32);
        ctx.SymmetryX = true;
        var tool = new PencilTool();

        // Act
        tool.OnPointerDown(ctx, 4, 10);
        tool.OnPointerUp(ctx, 4, 10);

        // Assert
        int expectedSymmetricX = 32 - 1 - 4; // 27
        ctx.ActiveLayer.GetPixel(4, 10).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(expectedSymmetricX, 10).Should().Be(PixelColor.Red);
    }

    [Fact]
    public void PencilTool_WithSymmetryXY_DrawsFourMirroredPixels()
    {
        // Arrange
        var ctx = CreateContext(32, 32);
        ctx.SymmetryX = true;
        ctx.SymmetryY = true;
        var tool = new PencilTool();

        // Act
        tool.OnPointerDown(ctx, 2, 3);
        tool.OnPointerUp(ctx, 2, 3);

        // Assert
        int sx = 31 - 2;
        int sy = 31 - 3;
        ctx.ActiveLayer.GetPixel(2, 3).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(sx, 3).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(2, sy).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(sx, sy).Should().Be(PixelColor.Red);
    }

    [Fact]
    public void EraserTool_ClearsPixelsToTransparent_WithUndo()
    {
        // Arrange
        var ctx = CreateContext();
        ctx.ActiveLayer.SetPixel(10, 10, PixelColor.Blue);
        var tool = new EraserTool();

        // Act
        tool.OnPointerDown(ctx, 10, 10);
        tool.OnPointerUp(ctx, 10, 10);

        // Assert
        ctx.ActiveLayer.GetPixel(10, 10).Should().Be(PixelColor.Transparent);
        ctx.History.CanUndo.Should().BeTrue();

        ctx.History.Undo();
        ctx.ActiveLayer.GetPixel(10, 10).Should().Be(PixelColor.Blue);
    }

    [Fact]
    public void EraserTool_PointerMove_InterpolatesAndErasesLine()
    {
        // Arrange
        var ctx = CreateContext();
        ctx.ActiveLayer.SetPixel(0, 0, PixelColor.Blue);
        ctx.ActiveLayer.SetPixel(1, 1, PixelColor.Blue);
        ctx.ActiveLayer.SetPixel(2, 2, PixelColor.Blue);
        var tool = new EraserTool();

        // Act
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerMove(ctx, 2, 2);
        tool.OnPointerUp(ctx, 2, 2);

        // Assert
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(1, 1).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(2, 2).Should().Be(PixelColor.Transparent);
        tool.GetPreviewPixels(ctx).Should().BeEmpty();
    }

    [Fact]
    public void EraserTool_WithSymmetry_ClearsMirroredPixels()
    {
        // Arrange
        var ctx = CreateContext(32, 32);
        ctx.SymmetryX = true;
        ctx.ActiveLayer.SetPixel(5, 5, PixelColor.Blue);
        ctx.ActiveLayer.SetPixel(26, 5, PixelColor.Blue);
        var tool = new EraserTool();

        // Act
        tool.OnPointerDown(ctx, 5, 5);
        tool.OnPointerUp(ctx, 5, 5);

        // Assert
        ctx.ActiveLayer.GetPixel(5, 5).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(26, 5).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void FillTool_FillsBoundedArea_UsingBFS()
    {
        // Arrange
        var ctx = CreateContext(16, 16);
        for (int i = 0; i < 4; i++)
        {
            ctx.ActiveLayer.SetPixel(i, 0, PixelColor.Blue);
            ctx.ActiveLayer.SetPixel(i, 3, PixelColor.Blue);
            ctx.ActiveLayer.SetPixel(0, i, PixelColor.Blue);
            ctx.ActiveLayer.SetPixel(3, i, PixelColor.Blue);
        }

        ctx.PrimaryColor = PixelColor.Yellow;
        var tool = new FillTool();

        // Act
        tool.OnPointerDown(ctx, 1, 1);
        tool.OnPointerMove(ctx, 1, 1);
        tool.OnPointerUp(ctx, 1, 1);

        // Assert
        ctx.ActiveLayer.GetPixel(1, 1).Should().Be(PixelColor.Yellow);
        ctx.ActiveLayer.GetPixel(2, 2).Should().Be(PixelColor.Yellow);
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Blue);
        ctx.ActiveLayer.GetPixel(3, 3).Should().Be(PixelColor.Blue);
        ctx.ActiveLayer.GetPixel(5, 5).Should().Be(PixelColor.Transparent);
        tool.GetPreviewPixels(ctx).Should().BeEmpty();
    }

    [Fact]
    public void FillTool_ClickingSameColor_IsNoOp()
    {
        // Arrange
        var ctx = CreateContext(16, 16);
        ctx.PrimaryColor = PixelColor.Transparent;
        var tool = new FillTool();

        // Act
        tool.OnPointerDown(ctx, 0, 0);

        // Assert
        ctx.History.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void FillTool_WithSymmetry_FillsSymmetricRegions()
    {
        // Arrange
        var ctx = CreateContext(32, 32);
        ctx.SymmetryX = true;
        ctx.ActiveLayer.Clear(PixelColor.Black);
        ctx.PrimaryColor = PixelColor.Green;
        var tool = new FillTool();

        // Act
        tool.OnPointerDown(ctx, 0, 0);

        // Assert
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Green);
        ctx.ActiveLayer.GetPixel(31, 0).Should().Be(PixelColor.Green);
        ctx.ActiveLayer.GetPixel(15, 15).Should().Be(PixelColor.Green);
    }

    [Fact]
    public void EyedropperTool_PicksColorFromActiveLayerAndComposite()
    {
        // Arrange
        var ctx = CreateContext();
        ctx.ActiveLayer.SetPixel(7, 7, PixelColor.Magenta);
        var tool = new EyedropperTool();

        // Act
        tool.OnPointerDown(ctx, 7, 7);
        tool.OnPointerMove(ctx, 7, 7);
        tool.OnPointerUp(ctx, 7, 7);

        // Assert
        ctx.PrimaryColor.Should().Be(PixelColor.Magenta);
        tool.GetPreviewPixels(ctx).Should().BeEmpty();
    }

    [Fact]
    public void EyedropperTool_WhenActiveLayerTransparent_PicksFromUnderlyingLayer()
    {
        // Arrange
        var canvas = new PixelCanvas(16, 16);
        var bottom = canvas.ActiveLayer!;
        bottom.SetPixel(3, 3, PixelColor.Orange);

        var top = canvas.AddLayer("Top");
        var history = new UndoRedoService();
        var ctx = new ToolContext(canvas, top, history);
        var tool = new EyedropperTool();

        // Act
        tool.OnPointerDown(ctx, 3, 3);
        tool.OnPointerUp(ctx, 3, 3);

        // Assert
        ctx.PrimaryColor.Should().Be(PixelColor.Orange);
    }

    [Fact]
    public void LineTool_ShowsPreviewDuringDrag_AndCommitsOnPointerUp()
    {
        // Arrange
        var ctx = CreateContext();
        ctx.PrimaryColor = PixelColor.Green;
        var tool = new LineTool();

        // Act
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerMove(ctx, 4, 0);
        var preview = tool.GetPreviewPixels(ctx);

        // Assert preview
        preview.Should().HaveCount(5);
        ctx.ActiveLayer.GetPixel(2, 0).Should().Be(PixelColor.Transparent);

        // Act pointer up
        tool.OnPointerUp(ctx, 4, 0);

        // Assert commit
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Green);
        ctx.ActiveLayer.GetPixel(1, 0).Should().Be(PixelColor.Green);
        ctx.ActiveLayer.GetPixel(2, 0).Should().Be(PixelColor.Green);
        ctx.ActiveLayer.GetPixel(3, 0).Should().Be(PixelColor.Green);
        ctx.ActiveLayer.GetPixel(4, 0).Should().Be(PixelColor.Green);
        tool.GetPreviewPixels(ctx).Should().BeEmpty();
    }

    [Fact]
    public void RectangleTool_OutlineMode_DrawsOnlyBorders()
    {
        // Arrange
        var ctx = CreateContext(16, 16);
        ctx.PrimaryColor = PixelColor.White;
        var tool = new RectangleTool(isFilled: false);

        // Act
        tool.OnPointerDown(ctx, 2, 2);
        tool.OnPointerMove(ctx, 5, 5);
        var preview = tool.GetPreviewPixels(ctx);
        preview.Should().NotBeEmpty();

        tool.OnPointerUp(ctx, 5, 5);

        // Assert borders
        ctx.ActiveLayer.GetPixel(2, 2).Should().Be(PixelColor.White);
        ctx.ActiveLayer.GetPixel(5, 5).Should().Be(PixelColor.White);
        ctx.ActiveLayer.GetPixel(2, 5).Should().Be(PixelColor.White);
        ctx.ActiveLayer.GetPixel(5, 2).Should().Be(PixelColor.White);
        // Interior untouched
        ctx.ActiveLayer.GetPixel(3, 3).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(4, 4).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void RectangleTool_FilledMode_DrawsBordersAndInterior()
    {
        // Arrange
        var ctx = CreateContext(16, 16);
        ctx.PrimaryColor = PixelColor.White;
        var tool = new RectangleTool(isFilled: true);

        // Act
        tool.OnPointerDown(ctx, 2, 2);
        tool.OnPointerUp(ctx, 4, 4);

        // Assert
        for (int y = 2; y <= 4; y++)
        {
            for (int x = 2; x <= 4; x++)
            {
                ctx.ActiveLayer.GetPixel(x, y).Should().Be(PixelColor.White);
            }
        }
    }

    [Fact]
    public void EllipseTool_OutlineMode_DrawsSymmetricBoundary()
    {
        // Arrange
        var ctx = CreateContext(16, 16);
        ctx.PrimaryColor = PixelColor.Yellow;
        var tool = new EllipseTool(isFilled: false);

        // Act
        tool.OnPointerDown(ctx, 4, 4);
        tool.OnPointerMove(ctx, 10, 10);
        var preview = tool.GetPreviewPixels(ctx);
        preview.Should().NotBeEmpty();

        tool.OnPointerUp(ctx, 10, 10);

        // Assert tips
        ctx.ActiveLayer.GetPixel(7, 4).Should().Be(PixelColor.Yellow);
        ctx.ActiveLayer.GetPixel(7, 10).Should().Be(PixelColor.Yellow);
        ctx.ActiveLayer.GetPixel(4, 7).Should().Be(PixelColor.Yellow);
        ctx.ActiveLayer.GetPixel(10, 7).Should().Be(PixelColor.Yellow);
        ctx.ActiveLayer.GetPixel(7, 7).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void EllipseTool_FilledMode_DrawsInteriorPixels()
    {
        // Arrange
        var ctx = CreateContext(16, 16);
        ctx.PrimaryColor = PixelColor.Yellow;
        var tool = new EllipseTool(isFilled: true);

        // Act
        tool.OnPointerDown(ctx, 4, 4);
        tool.OnPointerUp(ctx, 10, 10);

        // Assert center is filled
        ctx.ActiveLayer.GetPixel(7, 7).Should().Be(PixelColor.Yellow);
    }

    [Fact]
    public void EllipseTool_DegenerateLines_DrawsCorrectly()
    {
        // Arrange
        var ctx = CreateContext(16, 16);
        ctx.PrimaryColor = PixelColor.Red;
        var tool = new EllipseTool(isFilled: false);

        // Act 1xN line
        tool.OnPointerDown(ctx, 5, 2);
        tool.OnPointerUp(ctx, 5, 6);

        // Assert
        for (int y = 2; y <= 6; y++)
        {
            ctx.ActiveLayer.GetPixel(5, y).Should().Be(PixelColor.Red);
        }

        // Act Nx1 line
        tool.OnPointerDown(ctx, 1, 8);
        tool.OnPointerUp(ctx, 4, 8);

        // Assert
        for (int x = 1; x <= 4; x++)
        {
            ctx.ActiveLayer.GetPixel(x, 8).Should().Be(PixelColor.Red);
        }
    }

    [Fact]
    public void SelectionTool_SelectCutCopyPasteAndMove_Workflow()
    {
        // Arrange
        var ctx = CreateContext(16, 16);
        ctx.ActiveLayer.SetPixel(2, 2, PixelColor.Red);
        ctx.ActiveLayer.SetPixel(3, 2, PixelColor.Green);
        var tool = new SelectionTool();

        // 1. Select region (2,2) to (3,3)
        tool.OnPointerDown(ctx, 2, 2);
        var selectingPreview = tool.GetPreviewPixels(ctx);
        selectingPreview.Should().NotBeEmpty();

        tool.OnPointerMove(ctx, 3, 3);
        tool.OnPointerUp(ctx, 3, 3);

        ctx.Selection.IsActive.Should().BeTrue();
        ctx.Selection.Bounds.Width.Should().Be(2);
        ctx.Selection.Bounds.Height.Should().Be(2);

        // 2. Copy
        tool.Copy(ctx);
        SelectionTool.Clipboard.Should().NotBeNull();
        SelectionTool.Clipboard!.Value.Width.Should().Be(2);
        SelectionTool.Clipboard!.Value.Height.Should().Be(2);

        // 3. Delete
        tool.Delete(ctx);
        ctx.ActiveLayer.GetPixel(2, 2).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(3, 2).Should().Be(PixelColor.Transparent);

        // 4. Paste
        tool.Paste(ctx);
        ctx.Selection.IsActive.Should().BeTrue();
        ctx.Selection.IsFloating.Should().BeTrue();

        var floatingPreview = tool.GetPreviewPixels(ctx);
        floatingPreview.Should().NotBeEmpty();

        // 5. Move floating selection
        tool.OnPointerDown(ctx, ctx.Selection.Bounds.Left, ctx.Selection.Bounds.Top);
        tool.OnPointerMove(ctx, ctx.Selection.Bounds.Left + 2, ctx.Selection.Bounds.Top + 2);
        tool.OnPointerUp(ctx, ctx.Selection.Bounds.Left + 2, ctx.Selection.Bounds.Top + 2);

        // 6. Stamp
        tool.Stamp(ctx);
        ctx.Selection.IsFloating.Should().BeFalse();

        // 7. Cut workflow
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerUp(ctx, 1, 1);
        tool.Cut(ctx);
        ctx.Selection.IsActive.Should().BeFalse();

        // 8. Deselect
        tool.Deselect(ctx);
    }
}

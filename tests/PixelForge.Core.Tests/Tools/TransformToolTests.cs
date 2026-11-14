using FluentAssertions;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Tools;
using PixelForge.Core.Transforms;
using SkiaSharp;
using Xunit;

namespace PixelForge.Core.Tests.Tools;

public class TransformToolTests
{
    private (ToolContext ctx, PixelCanvas canvas, Layer layer) CreateContext(int width = 10, int height = 10)
    {
        var canvas = new PixelCanvas(width, height);
        var layer = new Layer("TestLayer", width, height);
        canvas.Layers.Add(layer);
        canvas.ActiveLayer = layer;

        var history = new UndoRedoService();
        var selection = new SelectionModel();

        var ctx = new ToolContext(canvas, layer, history, primaryColor: PixelColor.Red, selection: selection);

        return (ctx, canvas, layer);
    }

    [Fact]
    public void Properties_ShouldMatchSpecification()
    {
        var tool = new TransformTool();
        tool.Name.Should().Be("Transform");
        tool.Shortcut.Should().Be("T");
        tool.IconKey.Should().Be("IconTransform");
    }

    [Fact]
    public void InitializeSession_WithActiveSelection_ShouldLiftSelectedPixels()
    {
        var (ctx, _, layer) = CreateContext(10, 10);
        layer.SetPixel(2, 2, PixelColor.Red);
        layer.SetPixel(3, 3, PixelColor.Green);

        ctx.Selection.Select(new SKRectI(2, 2, 4, 4));

        var tool = new TransformTool();
        tool.InitializeSession(ctx);

        tool.ActiveSession.Should().NotBeNull();
        tool.ActiveSession!.OriginalBounds.Should().Be(new SKRectI(2, 2, 4, 4));

        // Lifted pixels should be erased from layer
        layer.GetPixel(2, 2).Should().Be(PixelColor.Transparent);
        layer.GetPixel(3, 3).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void Cancel_ShouldRestoreOriginalPixelsWithoutHistory()
    {
        var (ctx, _, layer) = CreateContext(10, 10);
        layer.SetPixel(2, 2, PixelColor.Red);

        ctx.Selection.Select(new SKRectI(2, 2, 4, 4));

        var tool = new TransformTool();
        tool.InitializeSession(ctx);
        tool.Cancel(ctx);

        tool.ActiveSession.Should().BeNull();
        layer.GetPixel(2, 2).Should().Be(PixelColor.Red);
        ctx.History.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void Commit_ShouldStampPixelsAndPushHistory()
    {
        var (ctx, _, layer) = CreateContext(10, 10);
        layer.SetPixel(2, 2, PixelColor.Red);

        ctx.Selection.Select(new SKRectI(2, 2, 3, 3));

        var tool = new TransformTool();
        tool.InitializeSession(ctx);
        tool.ActiveSession!.TranslationX = 2;
        tool.ActiveSession!.TranslationY = 2;

        tool.Commit(ctx);

        tool.ActiveSession.Should().BeNull();
        layer.GetPixel(2, 2).Should().Be(PixelColor.Transparent);
        layer.GetPixel(4, 4).Should().Be(PixelColor.Red);
        ctx.History.CanUndo.Should().BeTrue();

        // Test Undo
        ctx.History.Undo();
        layer.GetPixel(2, 2).Should().Be(PixelColor.Red);
        layer.GetPixel(4, 4).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void PointerDownOutside_ShouldCommitActiveSession()
    {
        var (ctx, _, layer) = CreateContext(10, 10);
        layer.SetPixel(2, 2, PixelColor.Red);
        ctx.Selection.Select(new SKRectI(2, 2, 3, 3));

        var tool = new TransformTool();
        tool.InitializeSession(ctx);

        // Click outside bounding box (e.g. at 9, 9)
        tool.OnPointerDown(ctx, 9, 9);

        tool.ActiveSession.Should().BeNull();
        ctx.History.CanUndo.Should().BeTrue();
    }

    [Fact]
    public void HitTest_ShouldIdentifyRotateAndHandles()
    {
        var (ctx, _, layer) = CreateContext(100, 100);
        ctx.Selection.Select(new SKRectI(20, 20, 80, 80));

        var tool = new TransformTool();
        tool.InitializeSession(ctx);

        // Center should be Move (50, 50)
        tool.HitTest(50, 50).Should().Be(TransformHandle.Move);

        // TopLeft corner at (20, 20)
        tool.HitTest(20, 20).Should().Be(TransformHandle.TopLeft);

        // BottomRight corner at (80, 80)
        tool.HitTest(80, 80).Should().Be(TransformHandle.BottomRight);

        // Rotate handle 20 units above top-mid (50, 0)
        tool.HitTest(50, 0).Should().Be(TransformHandle.Rotate);

        // Far away should be None
        tool.HitTest(500, 500).Should().Be(TransformHandle.None);
    }

    [Fact]
    public void RotationWithCtrl_ShouldSnapTo15Degrees()
    {
        var (ctx, _, layer) = CreateContext(100, 100);
        ctx.Selection.Select(new SKRectI(20, 20, 80, 80));

        var tool = new TransformTool();
        tool.InitializeSession(ctx);

        // Drag rotate handle at (50, 0) with Ctrl
        tool.IsCtrlDown = true;
        tool.OnPointerDown(ctx, 50, 0);

        // Move to angle ~16 degrees
        tool.OnPointerMove(ctx, 65, 3);

        // Rotation angle should be snapped to exactly 15 degrees
        tool.ActiveSession!.RotationDegrees.Should().Be(15.0f);
    }
}

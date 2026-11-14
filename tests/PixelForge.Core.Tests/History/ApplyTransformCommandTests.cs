using FluentAssertions;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using SkiaSharp;
using Xunit;

namespace PixelForge.Core.Tests.History;

public class ApplyTransformCommandTests
{
    [Fact]
    public void Execute_ShouldApplyNewColors()
    {
        var layer = new Layer("Test", 10, 10);
        layer.SetPixel(2, 2, PixelColor.Red);

        var deltas = new List<PixelDelta>
        {
            new(2, 2, PixelColor.Red, PixelColor.Transparent),
            new(5, 5, PixelColor.Transparent, PixelColor.Blue)
        };

        var cmd = new ApplyTransformCommand(layer, deltas, new SKRectI(0, 0, 4, 4), new SKRectI(4, 4, 8, 8));
        cmd.Execute();

        layer.GetPixel(2, 2).Should().Be(PixelColor.Transparent);
        layer.GetPixel(5, 5).Should().Be(PixelColor.Blue);
    }

    [Fact]
    public void Undo_ShouldRestoreOldColors()
    {
        var layer = new Layer("Test", 10, 10);
        layer.SetPixel(2, 2, PixelColor.Red);

        var deltas = new List<PixelDelta>
        {
            new(2, 2, PixelColor.Red, PixelColor.Transparent),
            new(5, 5, PixelColor.Transparent, PixelColor.Blue)
        };

        var cmd = new ApplyTransformCommand(layer, deltas, new SKRectI(0, 0, 4, 4), new SKRectI(4, 4, 8, 8));
        cmd.Execute();
        cmd.Undo();

        layer.GetPixel(2, 2).Should().Be(PixelColor.Red);
        layer.GetPixel(5, 5).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void UndoRedo_WithSelectionModel_ShouldUpdateSelectionBounds()
    {
        var layer = new Layer("Test", 10, 10);
        var selection = new SelectionModel();
        var origBounds = new SKRectI(1, 1, 3, 3);
        var transformedBounds = new SKRectI(4, 4, 8, 8);
        selection.Select(origBounds);

        var deltas = new List<PixelDelta>
        {
            new(1, 1, PixelColor.Red, PixelColor.Transparent),
            new(4, 4, PixelColor.Transparent, PixelColor.Red)
        };

        var cmd = new ApplyTransformCommand(layer, deltas, origBounds, transformedBounds, selection);
        cmd.Execute();
        selection.Bounds.Should().Be(transformedBounds);

        cmd.Undo();
        selection.Bounds.Should().Be(origBounds);

        cmd.Execute();
        selection.Bounds.Should().Be(transformedBounds);
    }
}

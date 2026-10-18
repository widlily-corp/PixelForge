using FluentAssertions;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using Xunit;

namespace PixelForge.Core.Tests;

public class UndoRedoTests
{
    [Fact]
    public void UndoRedo_Over100Steps_UndoesAndRedoesCorrectly()
    {
        // Arrange
        const int steps = 120;
        var service = new UndoRedoService(maxSteps: 150);
        var layer = new Layer("Test", 128, 128);

        // Act - Apply 120 unique pixel commands
        for (int i = 0; i < steps; i++)
        {
            var oldColor = layer.GetPixel(i, 0);
            var newColor = new PixelColor((byte)(i % 256), 0, 0, 255);
            var delta = new PixelDelta(i, 0, oldColor, newColor);
            var cmd = new DrawPixelsCommand(layer, new[] { delta }, $"Step {i}");
            service.ExecuteCommand(cmd);
        }

        // Assert all applied
        service.UndoStack.Should().HaveCount(steps);
        service.CanUndo.Should().BeTrue();
        service.CanRedo.Should().BeFalse();

        for (int i = 0; i < steps; i++)
        {
            layer.GetPixel(i, 0).R.Should().Be((byte)(i % 256));
        }

        // Act - Undo all 120 steps
        for (int i = steps - 1; i >= 0; i--)
        {
            bool undone = service.Undo();
            undone.Should().BeTrue();
            layer.GetPixel(i, 0).Should().Be(PixelColor.Transparent);
        }

        service.CanUndo.Should().BeFalse();
        service.CanRedo.Should().BeTrue();
        service.RedoStack.Should().HaveCount(steps);

        // Act - Redo all 120 steps
        for (int i = 0; i < steps; i++)
        {
            bool redone = service.Redo();
            redone.Should().BeTrue();
            layer.GetPixel(i, 0).R.Should().Be((byte)(i % 256));
        }

        service.CanUndo.Should().BeTrue();
        service.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void MaxSteps_WhenExceeded_DiscardsOldestCommands()
    {
        // Arrange
        const int maxSteps = 10;
        var service = new UndoRedoService(maxSteps);
        var layer = new Layer("Test", 32, 32);

        // Act - Execute 15 commands
        for (int i = 0; i < 15; i++)
        {
            var delta = new PixelDelta(i, 0, PixelColor.Transparent, PixelColor.Red);
            var cmd = new DrawPixelsCommand(layer, new[] { delta }, $"Step {i}");
            service.ExecuteCommand(cmd);
        }

        // Assert
        service.UndoStack.Should().HaveCount(maxSteps);
        service.UndoStack[0].Description.Should().Be("Step 5");
        service.UndoStack[^1].Description.Should().Be("Step 14");
    }

    [Fact]
    public void ExecuteCommand_ClearsRedoStack()
    {
        // Arrange
        var service = new UndoRedoService();
        var layer = new Layer("Test", 16, 16);

        var cmd1 = new DrawPixelsCommand(layer, new[] { new PixelDelta(0, 0, PixelColor.Transparent, PixelColor.Red) });
        var cmd2 = new DrawPixelsCommand(layer, new[] { new PixelDelta(1, 0, PixelColor.Transparent, PixelColor.Blue) });
        var cmd3 = new DrawPixelsCommand(layer, new[] { new PixelDelta(2, 0, PixelColor.Transparent, PixelColor.Green) });

        service.ExecuteCommand(cmd1);
        service.ExecuteCommand(cmd2);

        service.Undo();
        service.CanRedo.Should().BeTrue();

        // Act - New command while redo is available
        service.ExecuteCommand(cmd3);

        // Assert
        service.CanRedo.Should().BeFalse();
        service.RedoStack.Should().BeEmpty();
    }

    [Fact]
    public void Clear_EmptiesBothUndoAndRedoStacks()
    {
        // Arrange
        var service = new UndoRedoService();
        var layer = new Layer("Test", 16, 16);
        service.ExecuteCommand(new DrawPixelsCommand(layer, new[] { new PixelDelta(0, 0, PixelColor.Transparent, PixelColor.Red) }));
        service.ExecuteCommand(new DrawPixelsCommand(layer, new[] { new PixelDelta(1, 0, PixelColor.Transparent, PixelColor.Blue) }));
        service.Undo();

        // Act
        service.Clear();

        // Assert
        service.CanUndo.Should().BeFalse();
        service.CanRedo.Should().BeFalse();
        service.UndoStack.Should().BeEmpty();
        service.RedoStack.Should().BeEmpty();
    }

    [Fact]
    public void UndoAndRedo_WhenEmpty_ReturnFalse()
    {
        // Arrange
        var service = new UndoRedoService();

        // Act & Assert
        service.Undo().Should().BeFalse();
        service.Redo().Should().BeFalse();
    }

    [Fact]
    public void Constructor_InvalidMaxSteps_ThrowsArgumentOutOfRangeException()
    {
        // Arrange & Act
        Action act = () => _ = new UndoRedoService(0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AddLayerCommand_ExecuteAndUndo_ManagesCanvasLayers()
    {
        // Arrange
        var canvas = new PixelCanvas(16, 16);
        var newLayer = new Layer("Layer 2", 16, 16);
        var cmd = new AddLayerCommand(canvas, newLayer);

        // Act - Execute
        cmd.Execute();

        // Assert
        canvas.Layers.Should().HaveCount(2);
        canvas.ActiveLayer.Should().BeSameAs(newLayer);

        // Act - Undo
        cmd.Undo();

        // Assert
        canvas.Layers.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveLayerCommand_ExecuteAndUndo_RestoresLayerAtOriginalIndex()
    {
        // Arrange
        var canvas = new PixelCanvas(16, 16);
        var layer2 = canvas.AddLayer("Layer 2");
        var cmd = new RemoveLayerCommand(canvas, layer2);

        // Act - Execute
        cmd.Execute();

        // Assert
        canvas.Layers.Should().HaveCount(1);

        // Act - Undo
        cmd.Undo();

        // Assert
        canvas.Layers.Should().HaveCount(2);
        canvas.Layers[1].Should().BeSameAs(layer2);
    }

    [Fact]
    public void MergeLayerDownCommand_ExecuteAndUndo_RestoresLayerState()
    {
        // Arrange
        var canvas = new PixelCanvas(16, 16);
        var bottom = canvas.ActiveLayer!;
        bottom.SetPixel(0, 0, PixelColor.Blue);

        var top = canvas.AddLayer("Top");
        top.SetPixel(0, 0, PixelColor.Red);

        var cmd = new MergeLayerDownCommand(canvas, 1);

        // Act - Execute
        cmd.Execute();

        // Assert
        canvas.Layers.Should().HaveCount(1);
        bottom.GetPixel(0, 0).Should().Be(PixelColor.Red);

        // Act - Undo
        cmd.Undo();

        // Assert
        canvas.Layers.Should().HaveCount(2);
        bottom.GetPixel(0, 0).Should().Be(PixelColor.Blue);
        canvas.Layers[1].Should().BeSameAs(top);
    }

    [Fact]
    public void SetLayerVisibilityCommand_ExecuteAndUndo_TogglesVisibility()
    {
        // Arrange
        var layer = new Layer("Test", 16, 16);
        layer.IsVisible.Should().BeTrue();

        var cmd = new SetLayerVisibilityCommand(layer, false);

        // Act - Execute
        cmd.Execute();
        layer.IsVisible.Should().BeFalse();

        // Act - Undo
        cmd.Undo();
        layer.IsVisible.Should().BeTrue();
    }
}

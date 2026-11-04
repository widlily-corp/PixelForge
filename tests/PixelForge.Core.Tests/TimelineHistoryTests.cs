using FluentAssertions;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using Xunit;

namespace PixelForge.Core.Tests;

public class TimelineHistoryTests
{
    #region DrawPixelsCommand Timeline Auto-Navigation Tests

    [Fact]
    public void DrawPixelsCommand_UndoAndRedo_AutoNavigatesToTargetFrame()
    {
        // Arrange
        var canvas = new PixelCanvas(8, 8);
        canvas.AddFrame(durationMs: 100); // Frame 1
        canvas.AddFrame(durationMs: 100); // Frame 2
        var layer = canvas.Layers[0];

        // Switch to frame 1 and prepare stroke deltas
        canvas.SwitchToFrame(1);
        var red = new PixelColor(255, 0, 0, 255);
        var oldColor = layer.GetPixel(3, 3);
        layer.SetPixel(3, 3, red);
        var deltas = new[] { new PixelDelta(3, 3, oldColor, red) };

        var command = new DrawPixelsCommand(layer, deltas, canvas, frameIndex: 1, "Draw on Frame 1");

        // Act: Navigate to Frame 0 and invoke Undo
        canvas.SwitchToFrame(0);
        canvas.CurrentFrameIndex.Should().Be(0);

        command.Undo();

        // Assert: Auto-navigated to Frame 1 and reverted the pixel
        canvas.CurrentFrameIndex.Should().Be(1);
        layer.GetPixel(3, 3).Should().Be(oldColor);

        // Act: Navigate to Frame 2 and invoke Execute (Redo)
        canvas.SwitchToFrame(2);
        canvas.CurrentFrameIndex.Should().Be(2);

        command.Execute();

        // Assert: Auto-navigated to Frame 1 and reapplied the pixel
        canvas.CurrentFrameIndex.Should().Be(1);
        layer.GetPixel(3, 3).Should().Be(red);
    }

    [Fact]
    public void DrawPixelsCommand_WithoutCanvas_OperatesOnLayerDirectly()
    {
        // Arrange
        var layer = new Layer("Test", 4, 4);
        var blue = new PixelColor(0, 0, 255, 255);
        var deltas = new[] { new PixelDelta(1, 1, PixelColor.Transparent, blue) };

        // Act
        var cmd = new DrawPixelsCommand(layer, deltas);
        cmd.Execute();

        // Assert
        layer.GetPixel(1, 1).Should().Be(blue);
        cmd.Canvas.Should().BeNull();
        cmd.FrameIndex.Should().BeNull();

        cmd.Undo();
        layer.GetPixel(1, 1).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void DrawPixelsCommand_CapturesCurrentFrameIndex_WhenNotExplicitlyProvided()
    {
        // Arrange
        var canvas = new PixelCanvas(4, 4);
        canvas.AddFrame(durationMs: 100); // Frame 1
        canvas.SwitchToFrame(1);

        var layer = canvas.Layers[0];
        var deltas = new[] { new PixelDelta(0, 0, PixelColor.Transparent, PixelColor.Red) };

        // Act
        var cmd = new DrawPixelsCommand(layer, deltas, canvas);

        // Assert
        cmd.FrameIndex.Should().Be(1);
        cmd.Canvas.Should().Be(canvas);
    }

    #endregion

    #region Frame Mutation Commands Tests

    [Fact]
    public void AddFrameCommand_ExecuteAndUndo_ManagesFrameCollectionAndNavigation()
    {
        // Arrange
        var canvas = new PixelCanvas(8, 8);
        canvas.Frames.Count.Should().Be(1);

        var newFrame = new AnimationFrame(150);
        foreach (var layer in canvas.Layers)
        {
            newFrame.Cels[layer.Id] = new AnimationCel(layer.Id, 8, 8);
        }

        var cmd = new AddFrameCommand(canvas, insertIndex: 1, newFrame);

        // Act: Execute
        cmd.Execute();

        // Assert
        canvas.Frames.Count.Should().Be(2);
        canvas.Frames[1].Should().Be(newFrame);
        canvas.CurrentFrameIndex.Should().Be(1);

        // Act: Undo
        cmd.Undo();

        // Assert
        canvas.Frames.Count.Should().Be(1);
        canvas.Frames.Contains(newFrame).Should().BeFalse();
        canvas.CurrentFrameIndex.Should().Be(0);

        // Act: Redo
        cmd.Execute();
        canvas.Frames.Count.Should().Be(2);
        canvas.CurrentFrameIndex.Should().Be(1);
    }

    [Fact]
    public void RemoveFrameCommand_ExecuteAndUndo_RestoresFrameWithIntactCels()
    {
        // Arrange
        var canvas = new PixelCanvas(8, 8);
        var frame2 = canvas.AddFrame(durationMs: 200);
        var layer = canvas.Layers[0];
        layer.SetPixel(2, 2, PixelColor.Red);

        canvas.Frames.Count.Should().Be(2);
        var cmd = new RemoveFrameCommand(canvas, 1, frame2);

        // Act: Execute
        cmd.Execute();

        // Assert
        canvas.Frames.Count.Should().Be(1);
        canvas.Frames.Contains(frame2).Should().BeFalse();

        // Act: Undo
        cmd.Undo();

        // Assert
        canvas.Frames.Count.Should().Be(2);
        canvas.Frames[1].Should().Be(frame2);
        canvas.CurrentFrameIndex.Should().Be(1);
        canvas.Layers[0].GetPixel(2, 2).Should().Be(PixelColor.Red);
    }

    [Fact]
    public void DuplicateFrameCommand_ExecuteAndUndo_ClonesAndRemovesFrame()
    {
        // Arrange
        var canvas = new PixelCanvas(8, 8);
        var layer = canvas.Layers[0];
        layer.SetPixel(1, 1, PixelColor.Green);

        var duplicatedFrame = canvas.Frames[0].Clone();
        var cmd = new DuplicateFrameCommand(canvas, sourceIndex: 0, newIndex: 1, duplicatedFrame);

        // Act: Execute
        cmd.Execute();

        // Assert
        canvas.Frames.Count.Should().Be(2);
        canvas.CurrentFrameIndex.Should().Be(1);
        canvas.Layers[0].GetPixel(1, 1).Should().Be(PixelColor.Green);

        // Act: Undo
        cmd.Undo();

        // Assert
        canvas.Frames.Count.Should().Be(1);
        canvas.CurrentFrameIndex.Should().Be(0);

        // Act: Redo
        cmd.Execute();
        canvas.Frames.Count.Should().Be(2);
        canvas.CurrentFrameIndex.Should().Be(1);
    }

    [Fact]
    public void ReorderFramesCommand_ExecuteAndUndo_ReordersTimeline()
    {
        // Arrange
        var canvas = new PixelCanvas(4, 4);
        var frame0 = canvas.Frames[0];
        frame0.DurationMs = 100;

        var frame1 = canvas.AddFrame(durationMs: 200);
        var frame2 = canvas.AddFrame(durationMs: 300);

        canvas.Frames.Should().ContainInOrder(frame0, frame1, frame2);

        var cmd = new ReorderFramesCommand(canvas, oldIndex: 0, newIndex: 2);

        // Act: Move frame0 from index 0 to 2
        cmd.Execute();

        // Assert
        canvas.Frames.Should().ContainInOrder(frame1, frame2, frame0);
        canvas.CurrentFrameIndex.Should().Be(2);

        // Act: Undo
        cmd.Undo();

        // Assert
        canvas.Frames.Should().ContainInOrder(frame0, frame1, frame2);
        canvas.CurrentFrameIndex.Should().Be(0);

        // Act: Redo
        cmd.Execute();
        canvas.Frames.Should().ContainInOrder(frame1, frame2, frame0);
        canvas.CurrentFrameIndex.Should().Be(2);
    }

    [Fact]
    public void ChangeFrameDurationCommand_ExecuteAndUndo_UpdatesDuration()
    {
        // Arrange
        var frame = new AnimationFrame(100);
        var cmd = new ChangeFrameDurationCommand(frame, oldDuration: 100, newDuration: 250);

        // Act: Execute
        cmd.Execute();
        frame.DurationMs.Should().Be(250);

        // Act: Undo
        cmd.Undo();
        frame.DurationMs.Should().Be(100);

        // Act: Redo
        cmd.Execute();
        frame.DurationMs.Should().Be(250);
    }

    #endregion

    #region Tag Commands Tests

    [Fact]
    public void TagCommands_AddAndRemove_SupportsUndoRedo()
    {
        // Arrange
        var canvas = new PixelCanvas(8, 8);
        canvas.AddFrame(durationMs: 100);
        canvas.AddFrame(durationMs: 100);

        var tag = new AnimationTag
        {
            Name = "Idle",
            FromFrame = 0,
            ToFrame = 1,
            Color = PixelColor.Red
        };

        var addCmd = new AddTagCommand(canvas, tag);

        // Act: Add Tag
        addCmd.Execute();
        canvas.Tags.Should().Contain(tag);

        // Act: Undo Add
        addCmd.Undo();
        canvas.Tags.Should().NotContain(tag);

        // Act: Redo Add
        addCmd.Execute();
        canvas.Tags.Should().Contain(tag);

        // Act: Remove Tag
        var removeCmd = new RemoveTagCommand(canvas, tag);
        removeCmd.Execute();
        canvas.Tags.Should().NotContain(tag);

        // Act: Undo Remove
        removeCmd.Undo();
        canvas.Tags.Should().Contain(tag);
        canvas.Tags.IndexOf(tag).Should().Be(0);
    }

    #endregion

    #region Global Layer Operations across Timeline Tests

    [Fact]
    public void MergeLayerDown_BlendsCelsAcrossAllFrames()
    {
        // Arrange: 8x8 canvas with 3 frames and 2 layers
        var canvas = new PixelCanvas(8, 8);
        var topLayer = canvas.AddLayer("Top");
        var botLayer = canvas.Layers[0];

        // Frame 0 setup
        canvas.SwitchToFrame(0);
        botLayer.SetPixel(0, 0, PixelColor.Blue);
        topLayer.SetPixel(0, 0, PixelColor.Red);

        // Frame 1 setup
        canvas.AddFrame(durationMs: 100);
        canvas.SwitchToFrame(1);
        botLayer.SetPixel(1, 1, PixelColor.Green);
        topLayer.SetPixel(1, 1, PixelColor.Yellow);

        // Frame 2 setup
        canvas.AddFrame(durationMs: 100);
        canvas.SwitchToFrame(2);
        botLayer.SetPixel(2, 2, PixelColor.White);
        // top layer empty on frame 2

        // Act: Merge Top layer down into Bottom layer
        canvas.SwitchToFrame(0);
        canvas.MergeLayerDown(topLayer);

        // Assert: 1 layer remaining
        canvas.Layers.Count.Should().Be(1);
        var mergedLayer = canvas.Layers[0];

        // Frame 0 check
        canvas.SwitchToFrame(0);
        mergedLayer.GetPixel(0, 0).Should().Be(PixelColor.Red);

        // Frame 1 check
        canvas.SwitchToFrame(1);
        mergedLayer.GetPixel(1, 1).Should().Be(PixelColor.Yellow);

        // Frame 2 check
        canvas.SwitchToFrame(2);
        mergedLayer.GetPixel(2, 2).Should().Be(PixelColor.White);
    }

    [Fact]
    public void MergeLayerDownCommand_UndoAndRedo_RestoresAllFramesCels()
    {
        // Arrange
        var canvas = new PixelCanvas(8, 8);
        var topLayer = canvas.AddLayer("Top");
        var botLayer = canvas.Layers[0];

        canvas.SwitchToFrame(0);
        botLayer.SetPixel(0, 0, PixelColor.Blue);
        topLayer.SetPixel(0, 0, PixelColor.Red);

        canvas.AddFrame(durationMs: 100);
        canvas.SwitchToFrame(1);
        botLayer.SetPixel(1, 1, PixelColor.Green);
        topLayer.SetPixel(1, 1, PixelColor.Yellow);

        var cmd = new MergeLayerDownCommand(canvas, 1);

        // Act: Execute
        cmd.Execute();
        canvas.Layers.Count.Should().Be(1);

        // Act: Undo
        cmd.Undo();

        // Assert: Both layers restored
        canvas.Layers.Count.Should().Be(2);

        // Frame 0 pixels restored
        canvas.SwitchToFrame(0);
        canvas.Layers[0].GetPixel(0, 0).Should().Be(PixelColor.Blue);
        canvas.Layers[1].GetPixel(0, 0).Should().Be(PixelColor.Red);

        // Frame 1 pixels restored
        canvas.SwitchToFrame(1);
        canvas.Layers[0].GetPixel(1, 1).Should().Be(PixelColor.Green);
        canvas.Layers[1].GetPixel(1, 1).Should().Be(PixelColor.Yellow);

        // Act: Redo
        cmd.Execute();
        canvas.Layers.Count.Should().Be(1);
        canvas.SwitchToFrame(0);
        canvas.Layers[0].GetPixel(0, 0).Should().Be(PixelColor.Red);
        canvas.SwitchToFrame(1);
        canvas.Layers[0].GetPixel(1, 1).Should().Be(PixelColor.Yellow);
    }

    [Fact]
    public void SetDimensions_ResizesCelsAcrossAllFrames()
    {
        // Arrange
        var canvas = new PixelCanvas(4, 4);
        var layer = canvas.Layers[0];

        // Frame 0 has pixel at (0, 0)
        canvas.SwitchToFrame(0);
        layer.SetPixel(0, 0, PixelColor.Red);

        // Frame 1 has pixel at (2, 2)
        canvas.AddFrame(durationMs: 100);
        canvas.SwitchToFrame(1);
        layer.SetPixel(2, 2, PixelColor.Blue);

        // Frame 2 has pixel at (3, 3)
        canvas.AddFrame(durationMs: 100);
        canvas.SwitchToFrame(2);
        layer.SetPixel(3, 3, PixelColor.Green);

        // Act: Resize canvas from 4x4 to 8x8
        canvas.SetDimensions(8, 8);

        // Assert
        canvas.Width.Should().Be(8);
        canvas.Height.Should().Be(8);

        foreach (var frame in canvas.Frames)
        {
            frame.Cels[layer.Id].Pixels.Length.Should().Be(64);
        }

        // Verify Frame 0 content
        canvas.SwitchToFrame(0);
        layer.Width.Should().Be(8);
        layer.Height.Should().Be(8);
        layer.GetPixel(0, 0).Should().Be(PixelColor.Red);

        // Verify Frame 1 content
        canvas.SwitchToFrame(1);
        layer.Width.Should().Be(8);
        layer.Height.Should().Be(8);
        layer.GetPixel(2, 2).Should().Be(PixelColor.Blue);

        // Verify Frame 2 content
        canvas.SwitchToFrame(2);
        layer.Width.Should().Be(8);
        layer.Height.Should().Be(8);
        layer.GetPixel(3, 3).Should().Be(PixelColor.Green);
    }

    [Fact]
    public void RemoveLayerCommand_MultiFrame_RestoresCelsAcrossAllFrames()
    {
        // Arrange
        var canvas = new PixelCanvas(4, 4);
        var extraLayer = canvas.AddLayer("Extra");

        // Paint on Frame 0
        canvas.SwitchToFrame(0);
        extraLayer.SetPixel(0, 0, PixelColor.Red);

        // Paint on Frame 1
        canvas.AddFrame(durationMs: 100);
        canvas.SwitchToFrame(1);
        extraLayer.SetPixel(1, 1, PixelColor.Blue);

        var cmd = new RemoveLayerCommand(canvas, extraLayer);

        // Act: Execute Remove
        cmd.Execute();

        // Assert: Extra layer removed and cels purged
        canvas.Layers.Count.Should().Be(1);
        canvas.Frames[0].Cels.ContainsKey(extraLayer.Id).Should().BeFalse();
        canvas.Frames[1].Cels.ContainsKey(extraLayer.Id).Should().BeFalse();

        // Act: Undo
        cmd.Undo();

        // Assert: Extra layer restored along with artwork on both frames
        canvas.Layers.Count.Should().Be(2);
        canvas.SwitchToFrame(0);
        extraLayer.GetPixel(0, 0).Should().Be(PixelColor.Red);

        canvas.SwitchToFrame(1);
        extraLayer.GetPixel(1, 1).Should().Be(PixelColor.Blue);
    }

    [Fact]
    public void DrawPixelsCommand_ConsecutiveStrokesAcrossFrames_UndoesInExactReverseOrder()
    {
        // Arrange
        var canvas = new PixelCanvas(4, 4);
        canvas.AddFrame(durationMs: 100); // Frame 1
        canvas.AddFrame(durationMs: 100); // Frame 2
        var layer = canvas.Layers[0];
        var history = new UndoRedoService();

        // Stroke 1 on Frame 0
        canvas.SwitchToFrame(0);
        var old0 = layer.GetPixel(0, 0);
        layer.SetPixel(0, 0, PixelColor.Red);
        history.ExecuteCommand(new DrawPixelsCommand(layer, [new PixelDelta(0, 0, old0, PixelColor.Red)], canvas, 0));

        // Stroke 2 on Frame 1
        canvas.SwitchToFrame(1);
        var old1 = layer.GetPixel(1, 1);
        layer.SetPixel(1, 1, PixelColor.Green);
        history.ExecuteCommand(new DrawPixelsCommand(layer, [new PixelDelta(1, 1, old1, PixelColor.Green)], canvas, 1));

        // Stroke 3 on Frame 2
        canvas.SwitchToFrame(2);
        var old2 = layer.GetPixel(2, 2);
        layer.SetPixel(2, 2, PixelColor.Blue);
        history.ExecuteCommand(new DrawPixelsCommand(layer, [new PixelDelta(2, 2, old2, PixelColor.Blue)], canvas, 2));

        // Act & Assert: Undo step 3
        history.Undo();
        canvas.CurrentFrameIndex.Should().Be(2);
        layer.GetPixel(2, 2).Should().Be(old2);

        // Undo step 2
        history.Undo();
        canvas.CurrentFrameIndex.Should().Be(1);
        layer.GetPixel(1, 1).Should().Be(old1);

        // Undo step 1
        history.Undo();
        canvas.CurrentFrameIndex.Should().Be(0);
        layer.GetPixel(0, 0).Should().Be(old0);

        // Redo step 1
        history.Redo();
        canvas.CurrentFrameIndex.Should().Be(0);
        layer.GetPixel(0, 0).Should().Be(PixelColor.Red);

        // Redo step 2
        history.Redo();
        canvas.CurrentFrameIndex.Should().Be(1);
        layer.GetPixel(1, 1).Should().Be(PixelColor.Green);

        // Redo step 3
        history.Redo();
        canvas.CurrentFrameIndex.Should().Be(2);
        layer.GetPixel(2, 2).Should().Be(PixelColor.Blue);
    }

    [Fact]
    public void ReorderFramesCommand_InvalidIndices_ThrowsArgumentOutOfRangeException()
    {
        var canvas = new PixelCanvas(4, 4);
        var actNeg = () => new ReorderFramesCommand(canvas, -1, 0);
        var actHigh = () => new ReorderFramesCommand(canvas, 0, 5);

        actNeg.Should().Throw<ArgumentOutOfRangeException>();
        actHigh.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion
}

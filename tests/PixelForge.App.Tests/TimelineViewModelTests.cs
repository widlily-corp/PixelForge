using FluentAssertions;
using PixelForge.App.Services;
using PixelForge.App.ViewModels;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using Xunit;

namespace PixelForge.App.Tests;

public class TimelineViewModelTests
{
    [Fact]
    public void TimelineViewModel_Initialization_ReflectsCanvasDefaults()
    {
        var canvas = new PixelCanvas(16, 16);
        var canvasVm = new CanvasViewModel(canvas);
        var playback = new PlaybackService();
        var undoRedo = new UndoRedoService();

        var timelineVm = new TimelineViewModel(canvasVm, playback, undoRedo);

        timelineVm.CurrentFrameIndex.Should().Be(0);
        timelineVm.Frames.Count.Should().Be(1);
        timelineVm.CurrentFrameDurationMs.Should().Be(100);
        timelineVm.IsPlaying.Should().BeFalse();
        timelineVm.IsCollapsed.Should().BeFalse();
        timelineVm.IsOnionSkinEnabled.Should().BeFalse();
        timelineVm.FrameDisplayNumber.Should().Be("1 / 1");
    }

    [Fact]
    public void TimelineViewModel_AddFrameCommand_InsertsFrameAndSupportsUndo()
    {
        var canvas = new PixelCanvas(16, 16);
        var canvasVm = new CanvasViewModel(canvas);
        var playback = new PlaybackService();
        var undoRedo = new UndoRedoService();
        var timelineVm = new TimelineViewModel(canvasVm, playback, undoRedo);

        timelineVm.AddFrameCommand.Execute(null);

        timelineVm.Frames.Count.Should().Be(2);
        timelineVm.CurrentFrameIndex.Should().Be(1);
        timelineVm.FrameDisplayNumber.Should().Be("2 / 2");

        // Undo
        undoRedo.Undo();
        timelineVm.Frames.Count.Should().Be(1);

        // Redo
        undoRedo.Redo();
        timelineVm.Frames.Count.Should().Be(2);
    }

    [Fact]
    public void TimelineViewModel_DuplicateFrameCommand_DuplicatesCurrentFrame()
    {
        var canvas = new PixelCanvas(16, 16);
        canvas.ActiveLayer!.SetPixel(0, 0, PixelColor.Red);

        var canvasVm = new CanvasViewModel(canvas);
        var playback = new PlaybackService();
        var undoRedo = new UndoRedoService();
        var timelineVm = new TimelineViewModel(canvasVm, playback, undoRedo);

        timelineVm.DuplicateFrameCommand.Execute(null);

        timelineVm.Frames.Count.Should().Be(2);
        timelineVm.CurrentFrameIndex.Should().Be(1);
        canvas.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Red);
    }

    [Fact]
    public void TimelineViewModel_DeleteFrameCommand_DeletesFrameWhenMultipleExist()
    {
        var canvas = new PixelCanvas(16, 16);
        var canvasVm = new CanvasViewModel(canvas);
        var playback = new PlaybackService();
        var undoRedo = new UndoRedoService();
        var timelineVm = new TimelineViewModel(canvasVm, playback, undoRedo);

        // Cannot delete when only 1 frame
        timelineVm.DeleteFrameCommand.Execute(null);
        timelineVm.Frames.Count.Should().Be(1);

        // Add second frame and delete
        timelineVm.AddFrameCommand.Execute(null);
        timelineVm.Frames.Count.Should().Be(2);

        timelineVm.DeleteFrameCommand.Execute(null);
        timelineVm.Frames.Count.Should().Be(1);
    }

    [Fact]
    public void TimelineViewModel_CurrentFrameDurationMs_AppliesUndoableCommand()
    {
        var canvas = new PixelCanvas(16, 16);
        var canvasVm = new CanvasViewModel(canvas);
        var playback = new PlaybackService();
        var undoRedo = new UndoRedoService();
        var timelineVm = new TimelineViewModel(canvasVm, playback, undoRedo);

        timelineVm.CurrentFrameDurationMs = 250;
        canvas.CurrentFrame!.DurationMs.Should().Be(250);

        undoRedo.Undo();
        canvas.CurrentFrame.DurationMs.Should().Be(100);

        undoRedo.Redo();
        canvas.CurrentFrame.DurationMs.Should().Be(250);
    }

    [Fact]
    public void TimelineViewModel_ToggleOnionSkin_TogglesSettings()
    {
        var canvas = new PixelCanvas(16, 16);
        var canvasVm = new CanvasViewModel(canvas);
        var playback = new PlaybackService();
        var undoRedo = new UndoRedoService();
        var timelineVm = new TimelineViewModel(canvasVm, playback, undoRedo);

        timelineVm.IsOnionSkinEnabled.Should().BeFalse();
        timelineVm.ToggleOnionSkinCommand.Execute(null);
        timelineVm.IsOnionSkinEnabled.Should().BeTrue();
        canvas.OnionSkin.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void TimelineViewModel_SetCanvas_RebindsToNewCanvas()
    {
        var canvas1 = new PixelCanvas(16, 16);
        var canvasVm = new CanvasViewModel(canvas1);
        var playback = new PlaybackService();
        var undoRedo = new UndoRedoService();
        var timelineVm = new TimelineViewModel(canvasVm, playback, undoRedo);

        var canvas2 = new PixelCanvas(32, 32);
        canvas2.AddFrame(durationMs: 300);

        timelineVm.SetCanvas(canvas2);

        timelineVm.Frames.Count.Should().Be(2);
        timelineVm.Canvas.Width.Should().Be(32);
    }

    [Fact]
    public void TimelineViewModel_FrameNumbers_TrackSequential1BasedIndices()
    {
        var canvas = new PixelCanvas(16, 16);
        var canvasVm = new CanvasViewModel(canvas);
        var playback = new PlaybackService();
        var undoRedo = new UndoRedoService();
        var timelineVm = new TimelineViewModel(canvasVm, playback, undoRedo);

        // Initial frame has FrameNumber == 1
        timelineVm.Frames[0].FrameNumber.Should().Be(1);

        // Add 2 frames -> 1, 2, 3
        timelineVm.AddFrameCommand.Execute(null);
        timelineVm.AddFrameCommand.Execute(null);
        timelineVm.Frames.Count.Should().Be(3);
        timelineVm.Frames[0].FrameNumber.Should().Be(1);
        timelineVm.Frames[1].FrameNumber.Should().Be(2);
        timelineVm.Frames[2].FrameNumber.Should().Be(3);

        // Delete frame at index 1 -> remaining should be 1, 2
        timelineVm.SelectFrame(1);
        timelineVm.DeleteFrameCommand.Execute(null);
        timelineVm.Frames.Count.Should().Be(2);
        timelineVm.Frames[0].FrameNumber.Should().Be(1);
        timelineVm.Frames[1].FrameNumber.Should().Be(2);
    }

    [Fact]
    public void TimelineViewModel_ToggleCollapseCommand_TogglesCollapsedState()
    {
        var canvas = new PixelCanvas(16, 16);
        var canvasVm = new CanvasViewModel(canvas);
        var playback = new PlaybackService();
        var undoRedo = new UndoRedoService();
        var timelineVm = new TimelineViewModel(canvasVm, playback, undoRedo);

        timelineVm.IsCollapsed.Should().BeFalse();
        timelineVm.ToggleCollapseCommand.Execute(null);
        timelineVm.IsCollapsed.Should().BeTrue();
        timelineVm.ToggleCollapseCommand.Execute(null);
        timelineVm.IsCollapsed.Should().BeFalse();
    }
}

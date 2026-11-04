using System;
using FluentAssertions;
using PixelForge.App.Services;
using PixelForge.App.ViewModels;
using PixelForge.Core.Models;
using Xunit;

namespace PixelForge.App.Tests;

public class PlaybackServiceTests
{
    [Fact]
    public void CalculateInterval_CalculatesCorrectDurations_AndClampsTo10Ms()
    {
        // 100ms at normal speed
        PlaybackService.CalculateInterval(100, 1.0).TotalMilliseconds.Should().Be(100);

        // 100ms at 2x speed -> 50ms
        PlaybackService.CalculateInterval(100, 2.0).TotalMilliseconds.Should().Be(50);

        // 100ms at 0.5x speed -> 200ms
        PlaybackService.CalculateInterval(100, 0.5).TotalMilliseconds.Should().Be(200);

        // Very short duration or very high speed clamps to 10ms minimum
        PlaybackService.CalculateInterval(4, 1.0).TotalMilliseconds.Should().Be(10);
        PlaybackService.CalculateInterval(100, 100.0).TotalMilliseconds.Should().Be(10);

        // Invalid inputs default safely
        PlaybackService.CalculateInterval(0, 0.0).TotalMilliseconds.Should().Be(100);
        PlaybackService.CalculateInterval(-50, -1.0).TotalMilliseconds.Should().Be(100);
    }

    [Fact]
    public void StepForward_WithoutTag_LoopsOrStopsAtEnd()
    {
        var canvas = new PixelCanvas(8, 8);
        canvas.AddFrame(durationMs: 100);
        canvas.AddFrame(durationMs: 100);
        // Canvas now has 3 frames: [0, 1, 2]
        canvas.SwitchToFrame(0);

        using var service = new PlaybackService();
        service.Attach(canvas);
        service.IsLooping = true;
        canvas.CurrentFrameIndex.Should().Be(0);

        // Step through frames
        service.StepForward();
        canvas.CurrentFrameIndex.Should().Be(1);

        service.StepForward();
        canvas.CurrentFrameIndex.Should().Be(2);

        // Wrap around when looping
        service.StepForward();
        canvas.CurrentFrameIndex.Should().Be(0);

        // When looping is disabled
        service.IsLooping = false;
        canvas.SwitchToFrame(2);
        service.StepForward();
        canvas.CurrentFrameIndex.Should().Be(2);
    }

    [Fact]
    public void StepBackward_WithoutTag_LoopsOrStopsAtBeginning()
    {
        var canvas = new PixelCanvas(8, 8);
        canvas.AddFrame(durationMs: 100);
        canvas.AddFrame(durationMs: 100);
        // Canvas has 3 frames: [0, 1, 2]
        canvas.SwitchToFrame(0);

        using var service = new PlaybackService();
        service.Attach(canvas);
        service.IsLooping = true;
        canvas.CurrentFrameIndex.Should().Be(0);

        // Wrap backward to end
        service.StepBackward();
        canvas.CurrentFrameIndex.Should().Be(2);

        service.StepBackward();
        canvas.CurrentFrameIndex.Should().Be(1);

        service.StepBackward();
        canvas.CurrentFrameIndex.Should().Be(0);

        // When looping is disabled
        service.IsLooping = false;
        service.StepBackward();
        canvas.CurrentFrameIndex.Should().Be(0);
    }

    [Fact]
    public void CalculateNextFrameIndex_TagForward_LoopsWithinBounds()
    {
        var tag = new AnimationTag
        {
            FromFrame = 2,
            ToFrame = 4,
            LoopMode = AnimationLoopMode.Forward,
            RepeatCount = 0
        };

        bool pingPong = false;
        int repeatCycle = 0;

        // Step within tag
        int next = PlaybackService.CalculateNextFrameIndex(2, 6, tag, isLooping: true, ref pingPong, ref repeatCycle, out bool stop);
        next.Should().Be(3);
        stop.Should().BeFalse();

        next = PlaybackService.CalculateNextFrameIndex(3, 6, tag, isLooping: true, ref pingPong, ref repeatCycle, out stop);
        next.Should().Be(4);
        stop.Should().BeFalse();

        // Loop back to start of tag (2)
        next = PlaybackService.CalculateNextFrameIndex(4, 6, tag, isLooping: true, ref pingPong, ref repeatCycle, out stop);
        next.Should().Be(2);
        stop.Should().BeFalse();
        repeatCycle.Should().Be(1);

        // Outside tag jumps into tag min
        next = PlaybackService.CalculateNextFrameIndex(0, 6, tag, isLooping: true, ref pingPong, ref repeatCycle, out stop);
        next.Should().Be(2);
    }

    [Fact]
    public void CalculateNextFrameIndex_TagForward_StopsAfterRepeatCount()
    {
        var tag = new AnimationTag
        {
            FromFrame = 1,
            ToFrame = 2,
            LoopMode = AnimationLoopMode.Forward,
            RepeatCount = 2 // Stop after 2 repetitions
        };

        bool pingPong = false;
        int repeatCycle = 0;

        // Cycle 1: 1 -> 2
        int next = PlaybackService.CalculateNextFrameIndex(1, 4, tag, isLooping: true, ref pingPong, ref repeatCycle, out bool stop);
        next.Should().Be(2);
        stop.Should().BeFalse();

        // End of Cycle 1: loops to 1
        next = PlaybackService.CalculateNextFrameIndex(2, 4, tag, isLooping: true, ref pingPong, ref repeatCycle, out stop);
        next.Should().Be(1);
        stop.Should().BeFalse();
        repeatCycle.Should().Be(1);

        // Cycle 2: 1 -> 2
        next = PlaybackService.CalculateNextFrameIndex(1, 4, tag, isLooping: true, ref pingPong, ref repeatCycle, out stop);
        next.Should().Be(2);
        stop.Should().BeFalse();

        // End of Cycle 2: RepeatCount reached! Should stop at 2.
        next = PlaybackService.CalculateNextFrameIndex(2, 4, tag, isLooping: true, ref pingPong, ref repeatCycle, out stop);
        next.Should().Be(2);
        stop.Should().BeTrue();
        repeatCycle.Should().Be(2);
    }

    [Fact]
    public void CalculateNextFrameIndex_TagReverse_StepsBackwardAndLoops()
    {
        var tag = new AnimationTag
        {
            FromFrame = 1,
            ToFrame = 3,
            LoopMode = AnimationLoopMode.Reverse,
            RepeatCount = 0
        };

        bool pingPong = false;
        int repeatCycle = 0;

        // Steps: 3 -> 2 -> 1 -> 3
        int next = PlaybackService.CalculateNextFrameIndex(3, 5, tag, isLooping: true, ref pingPong, ref repeatCycle, out bool stop);
        next.Should().Be(2);
        stop.Should().BeFalse();

        next = PlaybackService.CalculateNextFrameIndex(2, 5, tag, isLooping: true, ref pingPong, ref repeatCycle, out stop);
        next.Should().Be(1);
        stop.Should().BeFalse();

        next = PlaybackService.CalculateNextFrameIndex(1, 5, tag, isLooping: true, ref pingPong, ref repeatCycle, out stop);
        next.Should().Be(3);
        stop.Should().BeFalse();
        repeatCycle.Should().Be(1);
    }

    [Fact]
    public void CalculateNextFrameIndex_TagPingPong_ReversesDirectionAtBoundaries()
    {
        var tag = new AnimationTag
        {
            FromFrame = 0,
            ToFrame = 2,
            LoopMode = AnimationLoopMode.PingPong,
            RepeatCount = 0
        };

        bool pingPong = false; // Moving forward
        int repeatCycle = 0;

        // 0 -> 1
        int next = PlaybackService.CalculateNextFrameIndex(0, 3, tag, isLooping: true, ref pingPong, ref repeatCycle, out bool stop);
        next.Should().Be(1);
        pingPong.Should().BeFalse();

        // 1 -> 2 (reaches boundary -> flips to reverse)
        next = PlaybackService.CalculateNextFrameIndex(1, 3, tag, isLooping: true, ref pingPong, ref repeatCycle, out stop);
        next.Should().Be(2);
        pingPong.Should().BeTrue();

        // 2 -> 1 (moving reverse)
        next = PlaybackService.CalculateNextFrameIndex(2, 3, tag, isLooping: true, ref pingPong, ref repeatCycle, out stop);
        next.Should().Be(1);
        pingPong.Should().BeTrue();

        // 1 -> 0 (reaches start boundary -> flips to forward, increments cycle)
        next = PlaybackService.CalculateNextFrameIndex(1, 3, tag, isLooping: true, ref pingPong, ref repeatCycle, out stop);
        next.Should().Be(0);
        pingPong.Should().BeFalse();
        repeatCycle.Should().Be(1);
    }

    [Fact]
    public void PlaybackService_PlaybackStopRequested_AutomaticallyPausesPlayback()
    {
        var canvas = new PixelCanvas(4, 4);
        canvas.AddFrame(durationMs: 100);
        var canvasVm = new CanvasViewModel(canvas);

        using var service = new PlaybackService();
        service.Attach(canvasVm);

        service.Play();
        service.IsPlaying.Should().BeTrue();
        canvasVm.IsPlaying.Should().BeTrue();

        // Simulate user clicking on canvas or tool interaction triggering PlaybackStopRequested
        canvasVm.StopPlayback();

        service.IsPlaying.Should().BeFalse();
        canvasVm.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public void AnimationPreviewViewModel_TicksConcurrently_WithoutAffectingMainCanvasCurrentFrameIndex()
    {
        var canvas = new PixelCanvas(8, 8);
        canvas.AddFrame(durationMs: 100);
        canvas.AddFrame(durationMs: 100);
        canvas.AddFrame(durationMs: 100);
        // Canvas has 4 frames: [0, 1, 2, 3]

        canvas.SwitchToFrame(1);
        canvas.CurrentFrameIndex.Should().Be(1);

        using var previewVm = new AnimationPreviewViewModel(canvas);
        previewVm.CurrentPreviewFrameIndex.Should().Be(0);

        // Step preview forward
        previewVm.StepForward();
        previewVm.CurrentPreviewFrameIndex.Should().Be(1);
        // Main canvas must remain untouched on frame 1
        canvas.CurrentFrameIndex.Should().Be(1);

        previewVm.StepForward();
        previewVm.CurrentPreviewFrameIndex.Should().Be(2);
        canvas.CurrentFrameIndex.Should().Be(1);

        previewVm.StepForward();
        previewVm.CurrentPreviewFrameIndex.Should().Be(3);
        canvas.CurrentFrameIndex.Should().Be(1);

        // Wrap around in preview
        previewVm.StepForward();
        previewVm.CurrentPreviewFrameIndex.Should().Be(0);
        canvas.CurrentFrameIndex.Should().Be(1);
    }

    [Fact]
    public void AnimationPreviewViewModel_ScaleAndSpeedMultipliers_UpdateCalculatedProperties()
    {
        var canvas = new PixelCanvas(16, 24);
        using var previewVm = new AnimationPreviewViewModel(canvas);

        previewVm.SetScale(4);
        previewVm.Scale.Should().Be(4);
        previewVm.DisplayWidth.Should().Be(16 * 4);
        previewVm.DisplayHeight.Should().Be(24 * 4);
        previewVm.IsScale4.Should().BeTrue();
        previewVm.IsScale1.Should().BeFalse();

        previewVm.SetSpeed(2.0);
        previewVm.SpeedMultiplier.Should().Be(2.0);
        previewVm.IsSpeed2.Should().BeTrue();
        previewVm.IsSpeed1.Should().BeFalse();
    }
}

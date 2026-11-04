using System;
using FluentAssertions;
using PixelForge.App.Controls;
using PixelForge.App.ViewModels;
using PixelForge.Core.Models;
using Xunit;

namespace PixelForge.App.Tests;

public class OnionSkinningAndPlaybackGuardTests
{
    [Fact]
    public void CanvasViewModel_PlaybackState_TogglesAndSignalsStopRequested()
    {
        var canvas = new PixelCanvas(16, 16);
        var vm = new CanvasViewModel(canvas);

        vm.IsPlaying.Should().BeFalse();

        bool stopRequested = false;
        vm.PlaybackStopRequested += () => stopRequested = true;

        vm.IsPlaying = true;
        vm.IsPlaying.Should().BeTrue();

        vm.StopPlayback();
        vm.IsPlaying.Should().BeFalse();
        stopRequested.Should().BeTrue();
    }

    [Fact]
    public void PixelCanvas_GetFrameCompositePixels_WithSingleLayerId_CompositesOnlyTargetLayer()
    {
        var canvas = new PixelCanvas(2, 2);
        // Canvas starts with 1 layer and 1 frame
        var layer1 = canvas.Layers[0];
        layer1.Name = "Base";
        layer1.SetPixel(0, 0, new PixelColor(255, 0, 0, 255)); // Red

        var layer2 = canvas.AddLayer("Overlay");
        layer2.SetPixel(0, 0, new PixelColor(0, 255, 0, 255)); // Green

        var buffer = new PixelColor[4];

        // Without layer filter: Green on top of Red = Green
        canvas.GetFrameCompositePixels(0, buffer);
        buffer[0].Should().Be(new PixelColor(0, 255, 0, 255));

        // Filter to layer1 only: Should composite only Red
        canvas.GetFrameCompositePixels(0, buffer, layer1.Id);
        buffer[0].Should().Be(new PixelColor(255, 0, 0, 255));

        // Filter to layer2 only: Should composite only Green
        canvas.GetFrameCompositePixels(0, buffer, layer2.Id);
        buffer[0].Should().Be(new PixelColor(0, 255, 0, 255));
    }

    [Fact]
    public void PixelCanvas_FrameChanged_FiresOnSwitchToFrame()
    {
        var canvas = new PixelCanvas(4, 4);
        var frame2 = new AnimationFrame(120);
        foreach (var layer in canvas.Layers)
        {
            frame2.Cels[layer.Id] = new AnimationCel(layer.Id, canvas.Width, canvas.Height);
        }
        canvas.Frames.Add(frame2);

        int recordedFrame = -1;
        canvas.FrameChanged += idx => recordedFrame = idx;

        canvas.SwitchToFrame(1);
        recordedFrame.Should().Be(1);
        canvas.CurrentFrameIndex.Should().Be(1);
    }

    [Fact]
    public void OnionSkinSettings_ClampingAndPropertyChangeNotifications()
    {
        var settings = new OnionSkinSettings();

        string? changedProperty = null;
        settings.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

        settings.IsEnabled = true;
        changedProperty.Should().Be(nameof(OnionSkinSettings.IsEnabled));

        settings.PrevFramesCount = 10;
        settings.PrevFramesCount.Should().Be(5); // Clamped to 5

        settings.NextFramesCount = 0;
        settings.NextFramesCount.Should().Be(1); // Clamped to 1

        settings.MaxOpacity = 0.75f;
        settings.MaxOpacity.Should().Be(0.75f);
    }

    [Fact]
    public void PixelCanvasControl_IsPlayingProperty_DirectBindingSupportsToggle()
    {
        var control = new PixelCanvasControl();
        control.IsPlaying.Should().BeFalse();

        control.IsPlaying = true;
        control.IsPlaying.Should().BeTrue();

        control.IsPlaying = false;
        control.IsPlaying.Should().BeFalse();
    }
}

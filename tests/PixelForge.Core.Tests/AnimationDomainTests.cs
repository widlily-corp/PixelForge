using FluentAssertions;
using PixelForge.Core.Models;
using Xunit;

namespace PixelForge.Core.Tests;

public class AnimationDomainTests
{
    #region AnimationCel Tests

    [Fact]
    public void AnimationCel_DimensionConstructor_InitializesEmptyBuffer()
    {
        // Arrange
        var layerId = Guid.NewGuid();

        // Act
        var cel = new AnimationCel(layerId, 16, 16);

        // Assert
        cel.LayerId.Should().Be(layerId);
        cel.Pixels.Should().NotBeNull();
        cel.Pixels.Length.Should().Be(256);
        cel.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void AnimationCel_InvalidDimensions_ThrowsArgumentOutOfRangeException()
    {
        var id = Guid.NewGuid();
        Action actZeroWidth = () => _ = new AnimationCel(id, 0, 16);
        Action actZeroHeight = () => _ = new AnimationCel(id, 16, 0);

        actZeroWidth.Should().Throw<ArgumentOutOfRangeException>();
        actZeroHeight.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AnimationCel_PixelsConstructor_SetsBufferAndNotEmpty()
    {
        // Arrange
        var layerId = Guid.NewGuid();
        var pixels = new PixelColor[64];
        pixels[0] = new PixelColor(255, 0, 0, 255);

        // Act
        var cel = new AnimationCel(layerId, pixels);

        // Assert
        cel.LayerId.Should().Be(layerId);
        cel.Pixels.Should().BeSameAs(pixels);
        cel.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void AnimationCel_SetPixels_UpdatesBuffer()
    {
        // Arrange
        var cel = new AnimationCel(Guid.NewGuid(), 8, 8);
        var newPixels = new PixelColor[64];
        newPixels[5] = new PixelColor(0, 255, 0, 255);

        // Act
        cel.SetPixels(newPixels);

        // Assert
        cel.Pixels.Should().BeSameAs(newPixels);
        cel.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void AnimationCel_UpdateIsEmpty_CorrectlyDetectsAlpha()
    {
        // Arrange
        var cel = new AnimationCel(Guid.NewGuid(), 4, 4);
        cel.IsEmpty = false;

        // Act & Assert 1: All transparent
        cel.UpdateIsEmpty();
        cel.IsEmpty.Should().BeTrue();

        // Act & Assert 2: Has non-transparent pixel
        cel.Pixels[0] = new PixelColor(255, 255, 255, 1);
        cel.UpdateIsEmpty();
        cel.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void AnimationCel_Clone_CreatesDeepCopy()
    {
        // Arrange
        var layerId = Guid.NewGuid();
        var cel = new AnimationCel(layerId, 4, 4);
        cel.Pixels[0] = new PixelColor(255, 128, 0, 255);
        cel.IsEmpty = false;

        // Act
        var clone = cel.Clone();

        // Assert
        clone.LayerId.Should().Be(layerId);
        clone.Pixels.Should().NotBeSameAs(cel.Pixels);
        clone.Pixels[0].Should().Be(cel.Pixels[0]);
        clone.IsEmpty.Should().Be(cel.IsEmpty);

        // Mutating clone does not affect original
        clone.Pixels[0] = PixelColor.Transparent;
        cel.Pixels[0].Should().Be(new PixelColor(255, 128, 0, 255));
    }

    #endregion

    #region AnimationFrame Tests

    [Fact]
    public void AnimationFrame_Defaults_InitializedProperly()
    {
        // Act
        var frame = new AnimationFrame();

        // Assert
        frame.Id.Should().NotBeEmpty();
        frame.DurationMs.Should().Be(100);
        frame.Cels.Should().BeEmpty();
    }

    [Fact]
    public void AnimationFrame_Duration_ClampedToMinimumOneMs()
    {
        // Arrange
        var frame = new AnimationFrame();

        // Act
        frame.DurationMs = -50;

        // Assert
        frame.DurationMs.Should().Be(1);
    }

    [Fact]
    public void AnimationFrame_Clone_DeepCopiesAllCels()
    {
        // Arrange
        var frame = new AnimationFrame(120);
        var layerId = Guid.NewGuid();
        var cel = new AnimationCel(layerId, 4, 4);
        cel.Pixels[0] = new PixelColor(0, 0, 255, 255);
        frame.Cels[layerId] = cel;

        // Act
        var clone = frame.Clone();

        // Assert
        clone.Id.Should().NotBe(frame.Id);
        clone.DurationMs.Should().Be(120);
        clone.Cels.Should().ContainKey(layerId);
        clone.Cels[layerId].Should().NotBeSameAs(cel);
        clone.Cels[layerId].Pixels.Should().NotBeSameAs(cel.Pixels);
        clone.Cels[layerId].Pixels[0].Should().Be(new PixelColor(0, 0, 255, 255));
    }

    #endregion

    #region AnimationTag & OnionSkinSettings Tests

    [Fact]
    public void AnimationTag_DefaultsAndProperties_WorkProperly()
    {
        // Act
        var tag = new AnimationTag
        {
            Name = "RunCycle",
            FromFrame = 2,
            ToFrame = 6,
            LoopMode = AnimationLoopMode.PingPong,
            RepeatCount = 3
        };

        // Assert
        tag.Id.Should().NotBeEmpty();
        tag.Name.Should().Be("RunCycle");
        tag.FromFrame.Should().Be(2);
        tag.ToFrame.Should().Be(6);
        tag.LoopMode.Should().Be(AnimationLoopMode.PingPong);
        tag.RepeatCount.Should().Be(3);
    }

    [Fact]
    public void OnionSkinSettings_PropertyClamping_EnforcesLimits()
    {
        // Arrange
        var settings = new OnionSkinSettings();

        // Act
        settings.PrevFramesCount = 10;
        settings.NextFramesCount = -5;
        settings.MaxOpacity = 1.5f;

        // Assert
        settings.PrevFramesCount.Should().Be(5);
        settings.NextFramesCount.Should().Be(1);
        settings.MaxOpacity.Should().Be(1.0f);
    }

    #endregion

    #region PixelCanvas Timeline Tests

    [Fact]
    public void PixelCanvas_Constructor_InitializesFrameZeroWithCels()
    {
        // Arrange & Act
        var canvas = new PixelCanvas(16, 16);

        // Assert
        canvas.Frames.Should().HaveCount(1);
        canvas.CurrentFrameIndex.Should().Be(0);
        canvas.CurrentFrame.Should().NotBeNull();
        canvas.CurrentFrame.Should().BeSameAs(canvas.Frames[0]);

        // Frame 0 contains cel for default layer
        var layer = canvas.Layers[0];
        canvas.CurrentFrame!.Cels.Should().ContainKey(layer.Id);
        var cel = canvas.CurrentFrame.Cels[layer.Id];

        // Zero-allocation: layer.Pixels must be reference-identical to cel.Pixels
        layer.Pixels.Should().BeSameAs(cel.Pixels);
    }

    [Fact]
    public void PixelCanvas_AddFrame_AppendsFrameAndSwitchesToIt()
    {
        // Arrange
        var canvas = new PixelCanvas(16, 16);
        var initialLayer = canvas.Layers[0];

        // Act
        var newFrame = canvas.AddFrame(durationMs: 150);

        // Assert
        canvas.Frames.Should().HaveCount(2);
        canvas.CurrentFrameIndex.Should().Be(1);
        canvas.CurrentFrame.Should().BeSameAs(newFrame);
        newFrame.DurationMs.Should().Be(150);

        // Layer pixels should now reference new frame's cel
        var newCel = newFrame.Cels[initialLayer.Id];
        initialLayer.Pixels.Should().BeSameAs(newCel.Pixels);
        initialLayer.Pixels.Should().NotBeSameAs(canvas.Frames[0].Cels[initialLayer.Id].Pixels);
    }

    [Fact]
    public void PixelCanvas_AddFrame_WithInsertIndex_InsertsAtCorrectPosition()
    {
        // Arrange
        var canvas = new PixelCanvas(16, 16);
        var f0 = canvas.Frames[0];

        // Act
        var inserted = canvas.AddFrame(insertIndex: 0, durationMs: 200);

        // Assert
        canvas.Frames.Should().HaveCount(2);
        canvas.Frames[0].Should().BeSameAs(inserted);
        canvas.Frames[1].Should().BeSameAs(f0);
        canvas.CurrentFrameIndex.Should().Be(0);
    }

    [Fact]
    public void PixelCanvas_SwitchToFrame_ZeroAllocationCelSwapping_PreservesIndependentDrawings()
    {
        // Arrange
        var canvas = new PixelCanvas(8, 8);
        var layer = canvas.Layers[0];
        canvas.AddFrame(); // Creates Frame 1, canvas now at frame 1

        var red = new PixelColor(255, 0, 0, 255);
        var blue = new PixelColor(0, 0, 255, 255);

        bool frameChangedFired = false;
        int reportedIndex = -1;
        canvas.FrameChanged += idx =>
        {
            frameChangedFired = true;
            reportedIndex = idx;
        };

        // Act 1: Draw blue on Frame 1
        layer.SetPixel(2, 2, blue);
        layer.GetPixel(2, 2).Should().Be(blue);

        // Act 2: Switch to Frame 0 and draw red
        canvas.SwitchToFrame(0);
        frameChangedFired.Should().BeTrue();
        reportedIndex.Should().Be(0);
        canvas.CurrentFrameIndex.Should().Be(0);

        // Layer should now reference Frame 0's cel (which is blank at 2, 2)
        layer.GetPixel(2, 2).Should().Be(PixelColor.Transparent);
        layer.SetPixel(2, 2, red);
        layer.GetPixel(2, 2).Should().Be(red);

        // Act 3: Switch back to Frame 1
        canvas.SwitchToFrame(1);
        canvas.CurrentFrameIndex.Should().Be(1);
        layer.GetPixel(2, 2).Should().Be(blue);

        // Act 4: Switch back to Frame 0
        canvas.SwitchToFrame(0);
        layer.GetPixel(2, 2).Should().Be(red);

        // Verify zero-alloc buffer reference identities
        layer.Pixels.Should().BeSameAs(canvas.Frames[0].Cels[layer.Id].Pixels);
        canvas.SwitchToFrame(1);
        layer.Pixels.Should().BeSameAs(canvas.Frames[1].Cels[layer.Id].Pixels);
    }

    [Fact]
    public void PixelCanvas_DuplicateFrame_ClonesPixelsAndDuration()
    {
        // Arrange
        var canvas = new PixelCanvas(8, 8);
        var layer = canvas.Layers[0];
        var yellow = new PixelColor(255, 255, 0, 255);
        layer.SetPixel(3, 3, yellow);
        canvas.Frames[0].DurationMs = 250;

        // Act
        var duplicate = canvas.DuplicateFrame(0);

        // Assert
        canvas.Frames.Should().HaveCount(2);
        canvas.CurrentFrameIndex.Should().Be(1);
        canvas.CurrentFrame.Should().BeSameAs(duplicate);
        duplicate.DurationMs.Should().Be(250);

        // Pixel data was cloned
        layer.GetPixel(3, 3).Should().Be(yellow);

        // Cloned buffer is independent: editing duplicate does not alter original
        layer.SetPixel(3, 3, PixelColor.Transparent);
        layer.GetPixel(3, 3).Should().Be(PixelColor.Transparent);

        canvas.SwitchToFrame(0);
        layer.GetPixel(3, 3).Should().Be(yellow);
    }

    [Fact]
    public void PixelCanvas_DuplicateFrame_InvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        var canvas = new PixelCanvas(8, 8);
        Action actNegative = () => canvas.DuplicateFrame(-1);
        Action actTooLarge = () => canvas.DuplicateFrame(10);

        actNegative.Should().Throw<ArgumentOutOfRangeException>();
        actTooLarge.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PixelCanvas_RemoveFrame_EnsuresAtLeastOneRemainingFrame()
    {
        // Arrange
        var canvas = new PixelCanvas(8, 8);
        canvas.AddFrame(); // Frames: 2
        canvas.Frames.Should().HaveCount(2);

        // Act 1: Remove frame 1 -> should succeed
        bool removed = canvas.RemoveFrame(1);
        removed.Should().BeTrue();
        canvas.Frames.Should().HaveCount(1);
        canvas.CurrentFrameIndex.Should().Be(0);

        // Act 2: Attempt to remove the only remaining frame -> should fail
        bool removedLast = canvas.RemoveFrame(0);
        removedLast.Should().BeFalse();
        canvas.Frames.Should().HaveCount(1);
    }

    [Fact]
    public void PixelCanvas_RemoveFrame_AdjustsCurrentFrameIndexProperly()
    {
        // Arrange: 3 frames [0, 1, 2]
        var canvas = new PixelCanvas(8, 8);
        canvas.AddFrame();
        canvas.AddFrame();
        canvas.Frames.Should().HaveCount(3);

        // Frame 0: red, Frame 1: green, Frame 2: blue
        var layer = canvas.Layers[0];
        canvas.SwitchToFrame(0);
        layer.SetPixel(0, 0, new PixelColor(255, 0, 0, 255));
        canvas.SwitchToFrame(1);
        layer.SetPixel(0, 0, new PixelColor(0, 255, 0, 255));
        canvas.SwitchToFrame(2);
        layer.SetPixel(0, 0, new PixelColor(0, 0, 255, 255));

        // When removing active frame 1 from [0, 1, 2], new active frame should be the new index 1 (former frame 2: blue)
        canvas.SwitchToFrame(1);
        canvas.RemoveFrame(1);

        canvas.Frames.Should().HaveCount(2);
        canvas.CurrentFrameIndex.Should().Be(1);
        layer.GetPixel(0, 0).Should().Be(new PixelColor(0, 0, 255, 255));
    }

    [Fact]
    public void PixelCanvas_LayerSynchronization_MultiFrameLayerOperations()
    {
        // Arrange: create canvas with 2 frames
        var canvas = new PixelCanvas(8, 8);
        canvas.AddFrame();
        canvas.Frames.Should().HaveCount(2);

        // Act: add a second layer
        var layer2 = canvas.AddLayer("Overlay");

        // Assert: both frames should have cels for the new layer
        canvas.Frames[0].Cels.Should().ContainKey(layer2.Id);
        canvas.Frames[1].Cels.Should().ContainKey(layer2.Id);

        // Current active frame is frame 1
        canvas.CurrentFrameIndex.Should().Be(1);
        layer2.Pixels.Should().BeSameAs(canvas.Frames[1].Cels[layer2.Id].Pixels);

        // Switch to frame 0
        canvas.SwitchToFrame(0);
        layer2.Pixels.Should().BeSameAs(canvas.Frames[0].Cels[layer2.Id].Pixels);

        // Act 2: remove layer2
        canvas.RemoveLayer(layer2);
        canvas.Frames[0].Cels.Should().NotContainKey(layer2.Id);
        canvas.Frames[1].Cels.Should().NotContainKey(layer2.Id);
    }

    [Fact]
    public void PixelCanvas_Tags_AddAndClampingOnFrameRemoval()
    {
        // Arrange: 4 frames
        var canvas = new PixelCanvas(8, 8);
        canvas.AddFrame();
        canvas.AddFrame();
        canvas.AddFrame();
        canvas.Frames.Should().HaveCount(4);

        // Act: Add tag covering frames 1 to 3
        var tag = canvas.AddTag("Attack", 1, 3, new PixelColor(255, 0, 0, 255), AnimationLoopMode.PingPong, 2);

        // Assert
        canvas.Tags.Should().ContainSingle();
        tag.Name.Should().Be("Attack");
        tag.FromFrame.Should().Be(1);
        tag.ToFrame.Should().Be(3);

        // Act 2: Remove frames until only 2 remain
        canvas.RemoveFrame(3);
        canvas.RemoveFrame(2);

        // Assert: tag boundaries clamped
        tag.ToFrame.Should().Be(1);
    }

    [Fact]
    public void PixelCanvas_GetFrameCompositePixels_ComputesWithoutChangingCurrentFrame()
    {
        // Arrange
        var canvas = new PixelCanvas(4, 4);
        var layer = canvas.Layers[0];
        var red = new PixelColor(255, 0, 0, 255);
        var blue = new PixelColor(0, 0, 255, 255);

        layer.SetPixel(0, 0, red);

        canvas.AddFrame(); // Switch to frame 1
        layer.SetPixel(0, 0, blue);

        canvas.CurrentFrameIndex.Should().Be(1);

        // Act: request composite of frame 0 while remaining on frame 1
        var compositeF0 = canvas.GetFrameCompositePixels(0);

        // Assert
        canvas.CurrentFrameIndex.Should().Be(1);
        layer.GetPixel(0, 0).Should().Be(blue); // Main canvas unaffected
        compositeF0[0].Should().Be(red);        // Frame 0 composite has red pixel
    }

    [Fact]
    public void Layer_SetPixelsDirect_Validation()
    {
        // Arrange
        var layer = new Layer("Test", 4, 4);
        bool propertyChangedFired = false;
        layer.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Layer.Pixels))
            {
                propertyChangedFired = true;
            }
        };

        // Act & Assert 1: Null throws
        Action actNull = () => layer.SetPixelsDirect(null!);
        actNull.Should().Throw<ArgumentNullException>();

        // Act & Assert 2: Wrong size throws
        Action actWrongSize = () => layer.SetPixelsDirect(new PixelColor[8]);
        actWrongSize.Should().Throw<ArgumentException>();

        // Act & Assert 3: Valid size replaces buffer and raises event
        var validPixels = new PixelColor[16];
        layer.SetPixelsDirect(validPixels);

        layer.Pixels.Should().BeSameAs(validPixels);
        propertyChangedFired.Should().BeTrue();
    }

    #endregion
}

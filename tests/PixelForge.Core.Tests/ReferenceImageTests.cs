using FluentAssertions;
using PixelForge.Core.Models;
using SkiaSharp;
using Xunit;

namespace PixelForge.Core.Tests;

public class ReferenceImageTests
{
    [Fact]
    public void InitialState_ShouldHaveNoImageAndDefaultOpacity()
    {
        using var overlay = new ReferenceImageOverlay();
        overlay.HasImage.Should().BeFalse();
        overlay.Bitmap.Should().BeNull();
        overlay.Opacity.Should().Be(0.5f);
        overlay.IsVisible.Should().BeTrue();
        overlay.Width.Should().Be(0);
        overlay.Height.Should().Be(0);
        overlay.FilePath.Should().BeNull();
    }

    [Fact]
    public void Opacity_ShouldClampBetweenZeroAndOne()
    {
        using var overlay = new ReferenceImageOverlay();

        overlay.Opacity = 1.5f;
        overlay.Opacity.Should().Be(1.0f);

        overlay.Opacity = -0.5f;
        overlay.Opacity.Should().Be(0.0f);

        overlay.Opacity = 0.75f;
        overlay.Opacity.Should().Be(0.75f);
    }

    [Fact]
    public void LoadFromStream_ValidPng_ShouldPopulateDimensionsAndBitmap()
    {
        using var overlay = new ReferenceImageOverlay();

        // Create a 16x16 test bitmap and encode to PNG stream
        using var testBitmap = new SKBitmap(16, 16);
        testBitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(testBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = data.AsStream();

        overlay.LoadFromStream(stream, "test_ref.png");

        overlay.HasImage.Should().BeTrue();
        overlay.Bitmap.Should().NotBeNull();
        overlay.Width.Should().Be(16);
        overlay.Height.Should().Be(16);
        overlay.FilePath.Should().Be("test_ref.png");

        overlay.Clear();
        overlay.HasImage.Should().BeFalse();
        overlay.Bitmap.Should().BeNull();
    }

    [Fact]
    public void LoadFromStream_InvalidData_ShouldThrowInvalidOperationException()
    {
        using var overlay = new ReferenceImageOverlay();
        using var badStream = new MemoryStream([1, 2, 3, 4, 5]);

        var act = () => overlay.LoadFromStream(badStream);
        act.Should().Throw<InvalidOperationException>();
    }
}

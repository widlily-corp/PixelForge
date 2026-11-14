using FluentAssertions;
using PixelForge.Core.Models;
using PixelForge.Core.Transforms;
using SkiaSharp;
using Xunit;

namespace PixelForge.Core.Tests.Transforms;

public class PixelTransformTests
{
    [Fact]
    public void ComputeMatrix_Identity_ShouldMapPointsUnchanged()
    {
        var bounds = new SKRectI(10, 10, 20, 20);
        var pixels = new PixelColor[100];
        var session = new TransformSession(bounds, pixels);

        var matrix = session.ComputeMatrix();

        var pt = new SKPoint(15, 15);
        var mapped = matrix.MapPoint(pt);

        mapped.X.Should().BeApproximately(15f, 0.001f);
        mapped.Y.Should().BeApproximately(15f, 0.001f);
    }

    [Fact]
    public void ComputeMatrix_Translation_ShouldOffsetPivotAndPoints()
    {
        var bounds = new SKRectI(0, 0, 10, 10);
        var pixels = new PixelColor[100];
        var session = new TransformSession(bounds, pixels)
        {
            TranslationX = 5,
            TranslationY = -3
        };

        var matrix = session.ComputeMatrix();
        var mapped = matrix.MapPoint(new SKPoint(0, 0));

        mapped.X.Should().BeApproximately(5f, 0.001f);
        mapped.Y.Should().BeApproximately(-3f, 0.001f);
    }

    [Fact]
    public void TransformNearestNeighbor_Identity_ShouldReturnExactPixels()
    {
        // 2x2 test pattern:
        // [Red,   Green]
        // [Blue,  Yellow]
        var bounds = new SKRectI(0, 0, 2, 2);
        var pixels = new PixelColor[]
        {
            PixelColor.Red,
            PixelColor.Green,
            PixelColor.Blue,
            PixelColor.Yellow
        };

        var session = new TransformSession(bounds, pixels);

        var result = PixelTransform.TransformNearestNeighbor(session);

        result.Width.Should().Be(2);
        result.Height.Should().Be(2);
        result.Bounds.Left.Should().Be(0);
        result.Bounds.Top.Should().Be(0);
        result.Pixels[0].Should().Be(pixels[0]);
        result.Pixels[1].Should().Be(pixels[1]);
        result.Pixels[2].Should().Be(pixels[2]);
        result.Pixels[3].Should().Be(pixels[3]);
    }

    [Fact]
    public void TransformNearestNeighbor_IntegerScale2x_ShouldDoubleDimensions()
    {
        var bounds = new SKRectI(0, 0, 2, 2);
        var red = PixelColor.Red;
        var blue = PixelColor.Blue;
        var pixels = new PixelColor[] { red, red, blue, blue };

        var session = new TransformSession(bounds, pixels)
        {
            ScaleX = 2.0f,
            ScaleY = 2.0f
        };

        var result = PixelTransform.TransformNearestNeighbor(session);

        result.Width.Should().Be(4);
        result.Height.Should().Be(4);
    }

    [Fact]
    public void TransformNearestNeighbor_Rotate90CW_ShouldRotatePattern()
    {
        // 2x2:
        // [R, G]  Rotate 90 CW -> [B, R]
        // [B, Y]                  [Y, G]
        var bounds = new SKRectI(0, 0, 2, 2);
        var r = PixelColor.Red;
        var g = PixelColor.Green;
        var b = PixelColor.Blue;
        var y = PixelColor.Yellow;

        var pixels = new PixelColor[] { r, g, b, y };
        var session = new TransformSession(bounds, pixels)
        {
            RotationDegrees = 90.0f
        };

        var result = PixelTransform.TransformNearestNeighbor(session);

        result.Width.Should().Be(2);
        result.Height.Should().Be(2);
        result.Pixels[0].Should().Be(b);
        result.Pixels[1].Should().Be(r);
        result.Pixels[2].Should().Be(y);
        result.Pixels[3].Should().Be(g);
    }

    [Fact]
    public void Scale2x_SinglePixel_ShouldProduce2x2Block()
    {
        var src = new PixelColor[] { PixelColor.Red };
        var (w, h, upscaled) = PixelTransform.Scale2x(src, 1, 1);

        w.Should().Be(2);
        h.Should().Be(2);
        upscaled.Length.Should().Be(4);
        upscaled.Should().AllSatisfy(p => p.Should().Be(PixelColor.Red));
    }

    [Fact]
    public void Scale2x_DiagonalLine_ShouldRoundCorners()
    {
        // 3x3 pattern with diagonal line:
        // [X, _, _]
        // [_, X, _]
        // [_, _, X]
        var x = PixelColor.Red;
        var o = PixelColor.Transparent;
        var src = new PixelColor[]
        {
            x, o, o,
            o, x, o,
            o, o, x
        };

        var (w, h, upscaled) = PixelTransform.Scale2x(src, 3, 3);
        w.Should().Be(6);
        h.Should().Be(6);
        upscaled.Length.Should().Be(36);
        // EPX rule creates stepped connections
        upscaled.Count(p => p == x).Should().BeGreaterThan(3);
    }

    [Fact]
    public void TransformRotSprite_Identity_ShouldMatchNearestNeighbor()
    {
        var bounds = new SKRectI(0, 0, 2, 2);
        var pixels = new PixelColor[] { PixelColor.Red, PixelColor.Green, PixelColor.Blue, PixelColor.Yellow };
        var session = new TransformSession(bounds, pixels)
        {
            Algorithm = ResampleAlgorithm.RotSprite
        };

        var result = PixelTransform.Transform(session);
        result.Width.Should().Be(2);
        result.Height.Should().Be(2);
        result.Pixels[0].Should().Be(PixelColor.Red);
    }

    [Fact]
    public void TransformRotSprite_Rotate45Deg_ShouldReturnRotatedBoundsAndPixels()
    {
        // 4x4 filled box
        var bounds = new SKRectI(0, 0, 4, 4);
        var pixels = new PixelColor[16];
        Array.Fill(pixels, PixelColor.Cyan);

        var session = new TransformSession(bounds, pixels)
        {
            RotationDegrees = 45f,
            Algorithm = ResampleAlgorithm.RotSprite
        };

        var result = PixelTransform.Transform(session);

        // 45 deg rotation of 4x4 produces bounding box of approx sqrt(2)*4 = 5.6 -> 6x6
        result.Width.Should().BeGreaterThanOrEqualTo(4);
        result.Height.Should().BeGreaterThanOrEqualTo(4);
        result.Pixels.Should().Contain(PixelColor.Cyan);
    }

    [Fact]
    public void TransformRotSprite_Over256Pixels_ShouldFallbackSafely()
    {
        // 300x2 sprite (width > 256)
        var bounds = new SKRectI(0, 0, 300, 2);
        var pixels = new PixelColor[600];
        Array.Fill(pixels, PixelColor.Red);

        var session = new TransformSession(bounds, pixels)
        {
            RotationDegrees = 90f,
            Algorithm = ResampleAlgorithm.RotSprite
        };

        var result = PixelTransform.Transform(session);
        result.Width.Should().Be(2);
        result.Height.Should().Be(300);
        result.Pixels.Should().AllSatisfy(p => p.Should().Be(PixelColor.Red));
    }
}

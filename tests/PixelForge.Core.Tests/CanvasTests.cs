using FluentAssertions;
using PixelForge.Core.Models;
using PixelForge.Core.Palettes;
using SkiaSharp;
using Xunit;

namespace PixelForge.Core.Tests;

public class CanvasTests
{
    [Theory]
    [InlineData(32, 32)]
    [InlineData(64, 64)]
    [InlineData(128, 128)]
    public void Constructor_WithValidDimensions_InitializesCanvasCorrectly(int width, int height)
    {
        // Arrange & Act
        var canvas = new PixelCanvas(width, height);

        // Assert
        canvas.Width.Should().Be(width);
        canvas.Height.Should().Be(height);
        canvas.Layers.Should().HaveCount(1);
        canvas.ActiveLayer.Should().NotBeNull();
        canvas.ActiveLayer!.Width.Should().Be(width);
        canvas.ActiveLayer.Height.Should().Be(height);
        canvas.ActiveLayer.Pixels.Length.Should().Be(width * height);
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(32, 0)]
    [InlineData(-1, 32)]
    [InlineData(32, -1)]
    public void Constructor_WithInvalidDimensions_ThrowsArgumentOutOfRangeException(int width, int height)
    {
        // Arrange & Act
        Action act = () => _ = new PixelCanvas(width, height);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithLayerList_InitializesLayersAndActiveLayer()
    {
        // Arrange
        var l1 = new Layer("L1", 16, 16);
        var l2 = new Layer("L2", 16, 16);

        // Act
        var canvas = new PixelCanvas(16, 16, new[] { l1, l2 }, l2);

        // Assert
        canvas.Layers.Should().HaveCount(2);
        canvas.ActiveLayer.Should().BeSameAs(l2);
    }

    [Fact]
    public void Constructor_WithMismatchedLayerDimensions_ThrowsArgumentException()
    {
        // Arrange
        var mismatched = new Layer("Bad", 32, 32);

        // Act
        Action act = () => _ = new PixelCanvas(16, 16, new[] { mismatched });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddLayer_AddsLayerAtTop_AndSetsItAsActive()
    {
        // Arrange
        var canvas = new PixelCanvas(32, 32);
        var initialLayer = canvas.ActiveLayer;

        // Act
        var newLayer = canvas.AddLayer("Foreground");

        // Assert
        canvas.Layers.Should().HaveCount(2);
        canvas.Layers[1].Should().BeSameAs(newLayer);
        canvas.ActiveLayer.Should().BeSameAs(newLayer);
        newLayer.Name.Should().Be("Foreground");
        newLayer.Width.Should().Be(32);
        newLayer.Height.Should().Be(32);
        newLayer.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void InsertLayer_InsertsAtIndex_AndSetsActive()
    {
        // Arrange
        var canvas = new PixelCanvas(32, 32);
        var customLayer = new Layer("Custom", 32, 32);

        // Act
        canvas.InsertLayer(0, customLayer);

        // Assert
        canvas.Layers.Should().HaveCount(2);
        canvas.Layers[0].Should().BeSameAs(customLayer);
        canvas.ActiveLayer.Should().BeSameAs(customLayer);
    }

    [Fact]
    public void RemoveLayer_WhenMultipleLayers_RemovesLayerAndAdjustsActive()
    {
        // Arrange
        var canvas = new PixelCanvas(32, 32);
        var layer2 = canvas.AddLayer("Layer 2");

        // Act
        bool removed = canvas.RemoveLayer(layer2);

        // Assert
        removed.Should().BeTrue();
        canvas.Layers.Should().HaveCount(1);
        canvas.ActiveLayer.Should().BeSameAs(canvas.Layers[0]);
    }

    [Fact]
    public void RemoveLayer_WhenSingleLayerRemaining_ReturnsFalseAndPreservesLayer()
    {
        // Arrange
        var canvas = new PixelCanvas(32, 32);
        var onlyLayer = canvas.ActiveLayer!;

        // Act
        bool removed = canvas.RemoveLayer(onlyLayer);

        // Assert
        removed.Should().BeFalse();
        canvas.Layers.Should().HaveCount(1);
        canvas.ActiveLayer.Should().BeSameAs(onlyLayer);
    }

    [Fact]
    public void RemoveLayerAt_RemovesAtIndex()
    {
        // Arrange
        var canvas = new PixelCanvas(32, 32);
        var layer2 = canvas.AddLayer("Layer 2");

        // Act
        bool removed = canvas.RemoveLayerAt(1);

        // Assert
        removed.Should().BeTrue();
        canvas.Layers.Should().HaveCount(1);
    }

    [Fact]
    public void MergeLayerDown_BlendsTopLayerIntoBottomLayer_AndRemovesTop()
    {
        // Arrange
        var canvas = new PixelCanvas(32, 32);
        var bottom = canvas.ActiveLayer!;
        bottom.SetPixel(5, 5, PixelColor.Blue);

        var top = canvas.AddLayer("Top");
        top.SetPixel(5, 5, PixelColor.Red);
        top.SetPixel(6, 6, PixelColor.Green);

        // Act
        var merged = canvas.MergeLayerDown(1);

        // Assert
        canvas.Layers.Should().HaveCount(1);
        canvas.ActiveLayer.Should().BeSameAs(merged);
        merged.GetPixel(5, 5).Should().Be(PixelColor.Red);
        merged.GetPixel(6, 6).Should().Be(PixelColor.Green);
    }

    [Fact]
    public void GetCompositePixel_BlendsLayersBottomToTop()
    {
        // Arrange
        var canvas = new PixelCanvas(32, 32);
        var bottom = canvas.ActiveLayer!;
        bottom.SetPixel(10, 10, PixelColor.Blue);

        var top = canvas.AddLayer("Top");
        top.SetPixel(10, 10, new PixelColor(255, 0, 0, 128)); // Semi-transparent Red

        // Act
        var composite = canvas.GetCompositePixel(10, 10);

        // Assert
        composite.A.Should().Be(255);
        composite.R.Should().BeGreaterThan(0);
        composite.B.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetCompositePixel_WhenLayerIsHidden_IgnoresHiddenLayer()
    {
        // Arrange
        var canvas = new PixelCanvas(32, 32);
        var bottom = canvas.ActiveLayer!;
        bottom.SetPixel(10, 10, PixelColor.Blue);

        var top = canvas.AddLayer("Top");
        top.SetPixel(10, 10, PixelColor.Red);
        top.IsVisible = false;

        // Act
        var composite = canvas.GetCompositePixel(10, 10);

        // Assert
        composite.Should().Be(PixelColor.Blue);
    }

    [Fact]
    public void GetCompositePixels_FlattensAllLayersCorrectly()
    {
        // Arrange
        var canvas = new PixelCanvas(16, 16);
        var bottom = canvas.ActiveLayer!;
        bottom.SetPixel(0, 0, PixelColor.Red);

        var top = canvas.AddLayer("Top");
        top.SetPixel(1, 1, PixelColor.Green);

        // Act
        var flattened = canvas.GetCompositePixels();

        // Assert
        flattened.Length.Should().Be(256);
        flattened[0].Should().Be(PixelColor.Red);
        flattened[1 * 16 + 1].Should().Be(PixelColor.Green);
        flattened[2].Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void Layer_Clear_FillsAllPixelsWithSpecifiedColor()
    {
        // Arrange
        var layer = new Layer("Test", 16, 16);
        layer.SetPixel(0, 0, PixelColor.Red);

        // Act
        layer.Clear(PixelColor.Yellow);

        // Assert
        layer.GetPixel(0, 0).Should().Be(PixelColor.Yellow);
        layer.GetPixel(15, 15).Should().Be(PixelColor.Yellow);
    }

    [Fact]
    public void Layer_Clone_CreatesDeepIndependentCopy()
    {
        // Arrange
        var original = new Layer("Original", 16, 16);
        original.SetPixel(2, 2, PixelColor.Magenta);

        // Act
        var clone = original.Clone(newId: true);
        clone.SetPixel(2, 2, PixelColor.Cyan);

        // Assert
        clone.Id.Should().NotBe(original.Id);
        clone.GetPixel(2, 2).Should().Be(PixelColor.Cyan);
        original.GetPixel(2, 2).Should().Be(PixelColor.Magenta);
    }

    [Fact]
    public void Layer_OutOfBounds_ReturnsTransparentAndIgnoresSet()
    {
        // Arrange
        var layer = new Layer("Test", 10, 10);

        // Act
        var pixel = layer.GetPixel(-1, 5);
        var pixel2 = layer.GetPixel(10, 5);
        layer.SetPixel(-1, 5, PixelColor.Red);
        layer.SetPixel(15, 15, PixelColor.Red);

        // Assert
        pixel.Should().Be(PixelColor.Transparent);
        pixel2.Should().Be(PixelColor.Transparent);
    }

    [Theory]
    [InlineData("#F00", 255, 0, 0, 255)]
    [InlineData("#8F00", 255, 0, 0, 136)]
    [InlineData("#00FF00", 0, 255, 0, 255)]
    [InlineData("#800000FF", 0, 0, 255, 128)]
    public void PixelColor_FromHex_ParsesVariousFormats(string hex, byte r, byte g, byte b, byte a)
    {
        // Arrange & Act
        var color = PixelColor.FromHex(hex);

        // Assert
        color.R.Should().Be(r);
        color.G.Should().Be(g);
        color.B.Should().Be(b);
        color.A.Should().Be(a);
    }

    [Fact]
    public void PixelColor_ConversionsAndEquality()
    {
        // Arrange
        var c1 = new PixelColor(10, 20, 30, 200);
        var c2 = new PixelColor(10, 20, 30, 200);
        var c3 = new PixelColor(10, 20, 31, 200);

        // Act
        uint argb = c1.ToArgb();
        var fromArgb = PixelColor.FromArgb(argb);
        SKColor sk = c1;
        PixelColor fromSk = sk;

        // Assert
        (c1 == c2).Should().BeTrue();
        (c1 != c3).Should().BeTrue();
        c1.Equals(c2).Should().BeTrue();
        fromArgb.Should().Be(c1);
        fromSk.Should().Be(c1);
        c1.ToHex(true).Should().StartWith("#");
    }

    [Fact]
    public void PalettePresets_ProvidesStandardPalettes()
    {
        // Arrange & Act
        var db32 = PalettePresets.Default32;
        var pico8 = PalettePresets.Pico8;
        var gb = PalettePresets.GameBoy;
        var nes = PalettePresets.Nes;
        var byName = PalettePresets.GetPresetByName("pico-8");

        // Assert
        db32.Colors.Should().HaveCount(32);
        pico8.Colors.Should().HaveCount(16);
        gb.Colors.Should().HaveCount(4);
        nes.Colors.Should().HaveCount(54);
        byName.Name.Should().Be(pico8.Name);
        byName.Colors.Should().BeEquivalentTo(pico8.Colors);
        PalettePresets.AllPresets.Should().HaveCount(4);
    }
}

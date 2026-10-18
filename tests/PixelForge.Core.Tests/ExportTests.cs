using FluentAssertions;
using PixelForge.Core.Models;
using PixelForge.Core.Services;
using SkiaSharp;
using Xunit;

namespace PixelForge.Core.Tests;

public class ExportTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ExportService _service = new();

    public ExportTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PixelForge_ExportTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, true); } catch { }
        }
    }

    [Theory]
    [InlineData(1, 16, 16)]
    [InlineData(2, 32, 32)]
    [InlineData(4, 64, 64)]
    public async Task ExportPngAsync_ToFile_ProducesValidPngWithCorrectScale(int scale, int expectedWidth, int expectedHeight)
    {
        // Arrange
        var canvas = new PixelCanvas(16, 16);
        canvas.ActiveLayer!.SetPixel(0, 0, PixelColor.Red);
        canvas.ActiveLayer.SetPixel(15, 15, PixelColor.Blue);
        var outputPath = Path.Combine(_tempDirectory, $"export_scaled_{scale}.png");

        // Act
        await _service.ExportPngAsync(canvas, outputPath, scale);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        using var bitmap = SKBitmap.Decode(outputPath);
        bitmap.Should().NotBeNull();
        bitmap.Width.Should().Be(expectedWidth);
        bitmap.Height.Should().Be(expectedHeight);
    }

    [Fact]
    public async Task ExportPngAsync_ToStream_WritesValidPngStream()
    {
        // Arrange
        var canvas = new PixelCanvas(8, 8);
        canvas.ActiveLayer!.SetPixel(4, 4, PixelColor.Green);
        using var memoryStream = new MemoryStream();

        // Act
        await _service.ExportPngAsync(canvas, memoryStream);

        // Assert
        memoryStream.Length.Should().BeGreaterThan(0);
        memoryStream.Position = 0;
        using var bitmap = SKBitmap.Decode(memoryStream);
        bitmap.Should().NotBeNull();
        bitmap.Width.Should().Be(8);
        bitmap.Height.Should().Be(8);
    }

    [Theory]
    [InlineData(1, 16, 16)]
    [InlineData(2, 32, 32)]
    public async Task ExportBmpAsync_ToFile_ProducesValidBmpHeaderAndDimensions(int scale, int expectedWidth, int expectedHeight)
    {
        // Arrange
        var canvas = new PixelCanvas(16, 16);
        canvas.ActiveLayer!.SetPixel(1, 1, PixelColor.Yellow);
        var outputPath = Path.Combine(_tempDirectory, $"export_{scale}.bmp");

        // Act
        await _service.ExportBmpAsync(canvas, outputPath, scale);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var bytes = await File.ReadAllBytesAsync(outputPath);
        bytes.Length.Should().BeGreaterThan(54);
        // BMP magic header: 'B' 'M'
        bytes[0].Should().Be(0x42);
        bytes[1].Should().Be(0x4D);

        using var bitmap = SKBitmap.Decode(outputPath);
        bitmap.Should().NotBeNull();
        bitmap.Width.Should().Be(expectedWidth);
        bitmap.Height.Should().Be(expectedHeight);
    }

    [Fact]
    public async Task ExportBmpAsync_ToStream_WritesLosslessBmp()
    {
        // Arrange
        var canvas = new PixelCanvas(10, 10);
        canvas.ActiveLayer!.SetPixel(5, 5, PixelColor.Cyan);
        using var memoryStream = new MemoryStream();

        // Act
        await _service.ExportBmpAsync(canvas, memoryStream);

        // Assert
        memoryStream.Length.Should().BeGreaterThan(0);
        var bytes = memoryStream.ToArray();
        bytes[0].Should().Be(0x42);
        bytes[1].Should().Be(0x4D);

        memoryStream.Position = 0;
        using var bitmap = SKBitmap.Decode(memoryStream);
        bitmap.Should().NotBeNull();
        bitmap.Width.Should().Be(10);
        bitmap.Height.Should().Be(10);
    }

    [Fact]
    public async Task ExportPngAsync_InvalidScale_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var canvas = new PixelCanvas(16, 16);
        using var ms = new MemoryStream();

        // Act
        Func<Task> act = async () => await _service.ExportPngAsync(canvas, ms, scale: 0);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}

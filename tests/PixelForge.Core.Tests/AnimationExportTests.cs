using System.Text.Json;
using FluentAssertions;
using PixelForge.Core.Models;
using PixelForge.Core.Services;
using SkiaSharp;
using Xunit;

namespace PixelForge.Core.Tests;

public class AnimationExportTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ExportService _exportService = new();

    public AnimationExportTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PixelForge_AnimExportTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, true); } catch { }
        }
    }

    private static PixelCanvas CreateTestAnimatedCanvas(int width = 8, int height = 8, int frameCount = 3)
    {
        var canvas = new PixelCanvas(width, height);
        // Frame 0 is already created by default
        canvas.ActiveLayer!.SetPixel(0, 0, PixelColor.Red);

        for (int i = 1; i < frameCount; i++)
        {
            canvas.AddFrame(durationMs: 100 + (i * 50));
            canvas.SwitchToFrame(i);
            var color = i % 2 == 1 ? PixelColor.Green : PixelColor.Blue;
            canvas.ActiveLayer!.SetPixel(i % width, i % height, color);
        }

        canvas.SwitchToFrame(0);
        return canvas;
    }

    #region GIF Export Tests

    [Fact]
    public async Task ExportGifAsync_ToFile_ProducesValidGifHeaderAndTrailer()
    {
        // Arrange
        var canvas = CreateTestAnimatedCanvas(16, 16, frameCount: 3);
        var outputPath = Path.Combine(_tempDirectory, "test_animation.gif");

        // Act
        await _exportService.ExportGifAsync(canvas, outputPath, scale: 1);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var bytes = await File.ReadAllBytesAsync(outputPath);
        bytes.Length.Should().BeGreaterThan(50);

        // GIF89a Header magic
        bytes[0].Should().Be(0x47); // G
        bytes[1].Should().Be(0x49); // I
        bytes[2].Should().Be(0x46); // F
        bytes[3].Should().Be(0x38); // 8
        bytes[4].Should().Be(0x39); // 9
        bytes[5].Should().Be(0x61); // a

        // Screen dimensions (little-endian)
        ushort screenW = BitConverter.ToUInt16(bytes, 6);
        ushort screenH = BitConverter.ToUInt16(bytes, 8);
        screenW.Should().Be(16);
        screenH.Should().Be(16);

        // Netscape 2.0 loop extension block presence: "NETSCAPE2.0"
        var contentString = System.Text.Encoding.ASCII.GetString(bytes);
        contentString.Should().Contain("NETSCAPE2.0");

        // GIF Trailer at end: 0x3B
        bytes[^1].Should().Be(0x3B);
    }

    [Fact]
    public async Task ExportGifAsync_WithScale_MultipliesScreenDimensions()
    {
        // Arrange
        var canvas = CreateTestAnimatedCanvas(10, 12, frameCount: 2);
        var outputPath = Path.Combine(_tempDirectory, "scaled_animation.gif");

        // Act
        await _exportService.ExportGifAsync(canvas, outputPath, scale: 3);

        // Assert
        var bytes = await File.ReadAllBytesAsync(outputPath);
        ushort screenW = BitConverter.ToUInt16(bytes, 6);
        ushort screenH = BitConverter.ToUInt16(bytes, 8);
        screenW.Should().Be(30);
        screenH.Should().Be(36);
    }

    [Fact]
    public async Task ExportGifAsync_DecodableBySkiaCodec_PreservesFrameCountAndDimensions()
    {
        // Arrange
        var canvas = CreateTestAnimatedCanvas(16, 16, frameCount: 4);
        using var stream = new MemoryStream();

        // Act
        await _exportService.ExportGifAsync(canvas, stream, scale: 1);
        stream.Position = 0;

        // Assert using SkiaSharp's native GIF decoder
        using var codec = SKCodec.Create(stream);
        codec.Should().NotBeNull();
        codec.Info.Width.Should().Be(16);
        codec.Info.Height.Should().Be(16);
        codec.FrameCount.Should().Be(4);
    }

    [Fact]
    public async Task ExportGifAsync_HeavyColorCount_QuantizesCleanly()
    {
        // Arrange: Generate canvas with > 256 unique colors across the frame
        var canvas = new PixelCanvas(32, 32);
        var layer = canvas.ActiveLayer!;
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                byte r = (byte)(x * 8);
                byte g = (byte)(y * 8);
                byte b = (byte)((x + y) * 4);
                layer.SetPixel(x, y, new PixelColor(r, g, b, 255));
            }
        }

        var outputPath = Path.Combine(_tempDirectory, "quantized_heavy.gif");

        // Act
        await _exportService.ExportGifAsync(canvas, outputPath, scale: 1);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        using var stream = File.OpenRead(outputPath);
        using var codec = SKCodec.Create(stream);
        codec.Should().NotBeNull();
        codec.Info.Width.Should().Be(32);
        codec.Info.Height.Should().Be(32);
        codec.FrameCount.Should().Be(1);
    }

    #endregion

    #region Spritesheet Export Tests

    [Theory]
    [InlineData(SpritesheetLayoutMode.HorizontalStrip, 4, 16, 16, 0, 0, 1, 64, 16)]
    [InlineData(SpritesheetLayoutMode.VerticalStrip, 4, 16, 16, 0, 0, 1, 16, 64)]
    [InlineData(SpritesheetLayoutMode.Grid, 4, 16, 16, 0, 0, 1, 32, 32)] // 2 cols, 2 rows
    [InlineData(SpritesheetLayoutMode.Grid, 5, 10, 10, 2, 5, 1, 44, 32)] // 3 cols, 2 rows; W: 2*5 + 3*10 + 2*2 = 44, H: 2*5 + 2*10 + 1*2 = 32
    public async Task ExportSpritesheetAsync_LayoutCalculations_ProduceExpectedDimensions(
        SpritesheetLayoutMode layoutMode,
        int frameCount,
        int frameW,
        int frameH,
        int padding,
        int border,
        int scale,
        int expectedW,
        int expectedH)
    {
        // Arrange
        var canvas = CreateTestAnimatedCanvas(frameW, frameH, frameCount);
        var settings = new SpritesheetSettings
        {
            LayoutMode = layoutMode,
            Padding = padding,
            Border = border,
            Scale = scale
        };

        var imagePath = Path.Combine(_tempDirectory, $"spritesheet_{layoutMode}.png");

        // Act
        await _exportService.ExportSpritesheetAsync(canvas, imagePath, jsonPath: null, settings);

        // Assert
        File.Exists(imagePath).Should().BeTrue();
        using var bitmap = SKBitmap.Decode(imagePath);
        bitmap.Should().NotBeNull();
        bitmap.Width.Should().Be(expectedW);
        bitmap.Height.Should().Be(expectedH);
    }

    [Fact]
    public async Task ExportSpritesheetAsync_WithJsonMetadata_ConformsToAsepriteSchema()
    {
        // Arrange
        var canvas = CreateTestAnimatedCanvas(16, 16, frameCount: 3);
        canvas.AddTag("Walk", 0, 1, new PixelColor(0xFF, 0x00, 0x00, 0xFF), AnimationLoopMode.Forward);
        canvas.AddTag("Attack", 2, 2, new PixelColor(0x00, 0xFF, 0x00, 0xFF), AnimationLoopMode.PingPong);

        var settings = new SpritesheetSettings
        {
            LayoutMode = SpritesheetLayoutMode.HorizontalStrip,
            Padding = 2,
            Border = 4,
            Scale = 2
        };

        var imagePath = Path.Combine(_tempDirectory, "character_sheet.png");
        var jsonPath = Path.Combine(_tempDirectory, "character_sheet.json");

        // Act
        await _exportService.ExportSpritesheetAsync(canvas, imagePath, jsonPath, settings);

        // Assert image
        File.Exists(imagePath).Should().BeTrue();
        File.Exists(jsonPath).Should().BeTrue();

        // Assert JSON content
        var jsonContent = await File.ReadAllTextAsync(jsonPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        // frames array check
        root.TryGetProperty("frames", out var framesElem).Should().BeTrue();
        framesElem.GetArrayLength().Should().Be(3);

        var firstFrame = framesElem[0];
        firstFrame.GetProperty("filename").GetString().Should().Be("character_sheet_0");
        firstFrame.GetProperty("duration").GetInt32().Should().Be(canvas.Frames[0].DurationMs);

        var frameRect = firstFrame.GetProperty("frame");
        frameRect.GetProperty("x").GetInt32().Should().Be(4); // Border = 4
        frameRect.GetProperty("y").GetInt32().Should().Be(4); // Border = 4
        frameRect.GetProperty("w").GetInt32().Should().Be(32); // 16 * 2 scale = 32
        frameRect.GetProperty("h").GetInt32().Should().Be(32);

        firstFrame.GetProperty("rotated").GetBoolean().Should().BeFalse();
        firstFrame.GetProperty("trimmed").GetBoolean().Should().BeFalse();

        // meta object check
        root.TryGetProperty("meta", out var metaElem).Should().BeTrue();
        metaElem.GetProperty("app").GetString().Should().Be("PixelForge");
        metaElem.GetProperty("version").GetString().Should().Be(AppVersionInfo.CurrentVersion);
        metaElem.GetProperty("image").GetString().Should().Be("character_sheet.png");
        metaElem.GetProperty("format").GetString().Should().Be("RGBA8888");
        metaElem.GetProperty("scale").GetString().Should().Be("2");

        var frameTags = metaElem.GetProperty("frameTags");
        frameTags.GetArrayLength().Should().Be(2);

        var tag0 = frameTags[0];
        tag0.GetProperty("name").GetString().Should().Be("Walk");
        tag0.GetProperty("from").GetInt32().Should().Be(0);
        tag0.GetProperty("to").GetInt32().Should().Be(1);
        tag0.GetProperty("direction").GetString().Should().Be("forward");

        var tag1 = frameTags[1];
        tag1.GetProperty("name").GetString().Should().Be("Attack");
        tag1.GetProperty("from").GetInt32().Should().Be(2);
        tag1.GetProperty("to").GetInt32().Should().Be(2);
        tag1.GetProperty("direction").GetString().Should().Be("pingpong");
    }

    #endregion

    #region PNG Sequence Export Tests

    [Fact]
    public async Task ExportPngSequenceAsync_ExportsCorrectFileCountAndNames()
    {
        // Arrange
        var canvas = CreateTestAnimatedCanvas(16, 16, frameCount: 4);
        var outputDir = Path.Combine(_tempDirectory, "png_seq");

        // Act
        await _exportService.ExportPngSequenceAsync(canvas, outputDir, "hero_run", scale: 2);

        // Assert
        Directory.Exists(outputDir).Should().BeTrue();
        var files = Directory.GetFiles(outputDir, "*.png").OrderBy(f => f).ToArray();
        files.Length.Should().Be(4);

        Path.GetFileName(files[0]).Should().Be("hero_run_000.png");
        Path.GetFileName(files[1]).Should().Be("hero_run_001.png");
        Path.GetFileName(files[2]).Should().Be("hero_run_002.png");
        Path.GetFileName(files[3]).Should().Be("hero_run_003.png");

        foreach (var file in files)
        {
            using var bitmap = SKBitmap.Decode(file);
            bitmap.Should().NotBeNull();
            bitmap.Width.Should().Be(32); // 16 * 2 scale
            bitmap.Height.Should().Be(32);
        }
    }

    #endregion

    #region Parameter Validation & Cancellation Tests

    [Fact]
    public async Task ExportMethods_ValidateParametersCorrectly()
    {
        var canvas = CreateTestAnimatedCanvas(8, 8, frameCount: 1);
        var settings = new SpritesheetSettings();

        // Null canvas
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _exportService.ExportGifAsync(null!, "test.gif"));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _exportService.ExportSpritesheetAsync(null!, "sheet.png", null, settings));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _exportService.ExportPngSequenceAsync(null!, _tempDirectory, "frame"));

        // Invalid scale
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _exportService.ExportGifAsync(canvas, "test.gif", scale: 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _exportService.ExportSpritesheetAsync(canvas, "sheet.png", null, new SpritesheetSettings { Scale = -1 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _exportService.ExportPngSequenceAsync(canvas, _tempDirectory, "frame", scale: 0));

        // Invalid padding / border
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _exportService.ExportSpritesheetAsync(canvas, "sheet.png", null, new SpritesheetSettings { Padding = -5 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _exportService.ExportSpritesheetAsync(canvas, "sheet.png", null, new SpritesheetSettings { Border = -2 }));
    }

    [Fact]
    public async Task ExportMethods_RespectCancellationToken()
    {
        var canvas = CreateTestAnimatedCanvas(16, 16, frameCount: 5);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _exportService.ExportGifAsync(canvas, Path.Combine(_tempDirectory, "cancel.gif"), cancellationToken: cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _exportService.ExportSpritesheetAsync(canvas, Path.Combine(_tempDirectory, "cancel.png"), null, new SpritesheetSettings(), cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _exportService.ExportPngSequenceAsync(canvas, _tempDirectory, "cancel", cancellationToken: cts.Token));
    }

    #endregion
}

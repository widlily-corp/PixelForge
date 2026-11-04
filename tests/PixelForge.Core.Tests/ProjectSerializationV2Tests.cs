using System.Text.Json;
using FluentAssertions;
using PixelForge.Core.Models;
using PixelForge.Core.Services;
using Xunit;

namespace PixelForge.Core.Tests;

public class ProjectSerializationV2Tests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ProjectService _service = new();

    public ProjectSerializationV2Tests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PixelForge_V2Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, true); } catch { }
        }
    }

    [Fact]
    public async Task SaveAndLoad_MultiFrameProjectV2_PreservesAllFramesCelsTagsAndPixels()
    {
        // Arrange: 8x8 canvas with 2 layers and 3 frames
        var filePath = Path.Combine(_tempDirectory, "multiframe.pxproj");
        var canvas = new PixelCanvas(8, 8);
        canvas.ActiveLayer!.Name = "Background";
        var layer2 = canvas.AddLayer("Foreground");

        // Add 2 additional frames (total 3 frames)
        canvas.AddFrame(durationMs: 120);
        canvas.AddFrame(durationMs: 150);

        // Frame 0 (100ms default): Background has Red at (0,0), Foreground has Green at (1,1)
        canvas.SwitchToFrame(0);
        canvas.Layers[0].SetPixel(0, 0, PixelColor.Red);
        canvas.Layers[1].SetPixel(1, 1, PixelColor.Green);

        // Frame 1 (120ms): Background has Blue at (2,2), Foreground has Yellow at (3,3)
        canvas.SwitchToFrame(1);
        canvas.Layers[0].SetPixel(2, 2, PixelColor.Blue);
        canvas.Layers[1].SetPixel(3, 3, PixelColor.Yellow);

        // Frame 2 (150ms): Background has Magenta at (4,4), Foreground has Cyan at (5,5)
        canvas.SwitchToFrame(2);
        canvas.Layers[0].SetPixel(4, 4, PixelColor.Magenta);
        canvas.Layers[1].SetPixel(5, 5, PixelColor.Cyan);

        // Add tag covering frames 0..2
        var tagColor = new PixelColor(0x12, 0x34, 0x56, 0xFF);
        canvas.AddTag("WalkCycle", 0, 2, tagColor, AnimationLoopMode.PingPong, 3);

        // Set active frame to 1, and active layer to layer2
        canvas.CurrentFrameIndex = 1;
        canvas.ActiveLayer = layer2;

        var palette = new[] { PixelColor.Red, PixelColor.Green, PixelColor.Blue, PixelColor.Yellow, PixelColor.Magenta, PixelColor.Cyan };

        // Act: Canvas -> Project -> File -> Project -> Canvas
        var project = Project.FromCanvas(canvas, palette);
        await _service.SaveProjectAsync(project, filePath);

        var loadedProject = await _service.LoadProjectAsync(filePath);
        var loadedCanvas = loadedProject.ToCanvas();

        // Assert: Project DTO structure
        loadedProject.FormatVersion.Should().Be(2);
        loadedProject.Width.Should().Be(8);
        loadedProject.Height.Should().Be(8);
        loadedProject.ActiveLayerIndex.Should().Be(1);
        loadedProject.ActiveFrameIndex.Should().Be(1);
        loadedProject.Layers.Should().HaveCount(2);
        loadedProject.Frames.Should().HaveCount(3);
        loadedProject.Tags.Should().HaveCount(1);
        loadedProject.Palette.Should().HaveCount(6);

        // Assert: LayerData.Pixels should have active frame pixels (Frame 1)
        // Background has Blue at (2,2), Foreground has Yellow at (3,3)
        var bgFromData = loadedProject.Layers[0].ToLayer(8, 8);
        bgFromData.GetPixel(2, 2).Should().Be(PixelColor.Blue);
        var fgFromData = loadedProject.Layers[1].ToLayer(8, 8);
        fgFromData.GetPixel(3, 3).Should().Be(PixelColor.Yellow);

        // Assert: Reconstructed Canvas
        loadedCanvas.Width.Should().Be(8);
        loadedCanvas.Height.Should().Be(8);
        loadedCanvas.Layers.Should().HaveCount(2);
        loadedCanvas.Layers[0].Name.Should().Be("Background");
        loadedCanvas.Layers[1].Name.Should().Be("Foreground");
        loadedCanvas.ActiveLayer.Should().Be(loadedCanvas.Layers[1]);
        loadedCanvas.CurrentFrameIndex.Should().Be(1);
        loadedCanvas.Frames.Should().HaveCount(3);

        // Assert: Frame durations
        loadedCanvas.Frames[0].DurationMs.Should().Be(100);
        loadedCanvas.Frames[1].DurationMs.Should().Be(120);
        loadedCanvas.Frames[2].DurationMs.Should().Be(150);

        // Assert: Tags
        loadedCanvas.Tags.Should().HaveCount(1);
        loadedCanvas.Tags[0].Name.Should().Be("WalkCycle");
        loadedCanvas.Tags[0].FromFrame.Should().Be(0);
        loadedCanvas.Tags[0].ToFrame.Should().Be(2);
        loadedCanvas.Tags[0].Color.Should().Be(tagColor);
        loadedCanvas.Tags[0].LoopMode.Should().Be(AnimationLoopMode.PingPong);
        loadedCanvas.Tags[0].RepeatCount.Should().Be(3);

        // Assert: Frame 0 pixel buffers
        loadedCanvas.SwitchToFrame(0);
        loadedCanvas.Layers[0].GetPixel(0, 0).Should().Be(PixelColor.Red);
        loadedCanvas.Layers[1].GetPixel(1, 1).Should().Be(PixelColor.Green);
        loadedCanvas.Layers[0].GetPixel(2, 2).Should().Be(PixelColor.Transparent);

        // Assert: Frame 1 pixel buffers
        loadedCanvas.SwitchToFrame(1);
        loadedCanvas.Layers[0].GetPixel(2, 2).Should().Be(PixelColor.Blue);
        loadedCanvas.Layers[1].GetPixel(3, 3).Should().Be(PixelColor.Yellow);
        loadedCanvas.Layers[0].GetPixel(0, 0).Should().Be(PixelColor.Transparent);

        // Assert: Frame 2 pixel buffers
        loadedCanvas.SwitchToFrame(2);
        loadedCanvas.Layers[0].GetPixel(4, 4).Should().Be(PixelColor.Magenta);
        loadedCanvas.Layers[1].GetPixel(5, 5).Should().Be(PixelColor.Cyan);
        loadedCanvas.Layers[0].GetPixel(2, 2).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void ToCanvas_LegacyV1Json_SynthesizesSingleFrameWith100msDurationAndPreservesPixels()
    {
        // Arrange: 4x4 canvas legacy v1 JSON (no frames array, no tags, formatVersion = 1)
        var redBytes = new byte[4 * 4 * 4];
        // Pixel (0, 0) is Red (RGBA = 255, 0, 0, 255)
        redBytes[0] = 255;
        redBytes[1] = 0;
        redBytes[2] = 0;
        redBytes[3] = 255;
        // Pixel (3, 3) is Green (RGBA = 0, 255, 0, 255)
        int p33 = (3 * 4 + 3) * 4;
        redBytes[p33] = 0;
        redBytes[p33 + 1] = 255;
        redBytes[p33 + 2] = 0;
        redBytes[p33 + 3] = 255;

        string base64Pixels = Convert.ToBase64String(redBytes);
        var layerId = Guid.NewGuid();

        string v1Json = $$"""
        {
            "formatVersion": 1,
            "appVersion": "0.1.13",
            "width": 4,
            "height": 4,
            "activeLayerIndex": 0,
            "layers": [
                {
                    "id": "{{layerId}}",
                    "name": "Classic Layer",
                    "isVisible": true,
                    "opacity": 0.85,
                    "pixels": "{{base64Pixels}}"
                }
            ],
            "palette": ["#FF0000FF", "#00FF00FF"]
        }
        """;

        // Act: Deserialize v1 JSON and convert to canvas
        var project = JsonSerializer.Deserialize<Project>(v1Json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        project.Should().NotBeNull();
        project.FormatVersion.Should().Be(1);
        project.Frames.Should().BeEmpty();

        var canvas = project.ToCanvas();

        // Assert: 1 frame synthesized, 100ms duration, 0th index
        canvas.Width.Should().Be(4);
        canvas.Height.Should().Be(4);
        canvas.Layers.Should().HaveCount(1);
        canvas.Layers[0].Id.Should().Be(layerId);
        canvas.Layers[0].Name.Should().Be("Classic Layer");
        canvas.Layers[0].Opacity.Should().BeApproximately(0.85f, 0.001f);
        canvas.Layers[0].GetPixel(0, 0).Should().Be(PixelColor.Red);
        canvas.Layers[0].GetPixel(3, 3).Should().Be(PixelColor.Green);

        canvas.Frames.Should().HaveCount(1);
        canvas.Frames[0].DurationMs.Should().Be(100);
        canvas.CurrentFrameIndex.Should().Be(0);
        canvas.Frames[0].Cels.Should().ContainKey(layerId);
        canvas.Frames[0].Cels[layerId].Pixels[0].Should().Be(PixelColor.Red);
        canvas.Tags.Should().BeEmpty();
    }

    [Fact]
    public void ToCanvas_V2ProjectWithEmptyFrames_SynthesizesSingleFrameFromLayers()
    {
        // Arrange: v2 project where frames list is empty
        var project = new Project(4, 4)
        {
            FormatVersion = 2,
            Frames = new List<FrameData>()
        };

        var layer = new Layer("Layer 1", 4, 4);
        layer.SetPixel(1, 1, PixelColor.Blue);
        project.Layers.Add(LayerData.FromLayer(layer));

        // Act
        var canvas = project.ToCanvas();

        // Assert
        canvas.Frames.Should().HaveCount(1);
        canvas.Frames[0].DurationMs.Should().Be(100);
        canvas.CurrentFrameIndex.Should().Be(0);
        canvas.Layers[0].GetPixel(1, 1).Should().Be(PixelColor.Blue);
    }

    [Fact]
    public void ToCanvas_ActiveFrameIndexOutOfBounds_ClampsToValidBounds()
    {
        // Arrange
        var canvas = new PixelCanvas(4, 4);
        canvas.AddFrame(100); // Frame 1
        canvas.AddFrame(100); // Frame 2

        var project = Project.FromCanvas(canvas);
        project.ActiveFrameIndex = 999; // Intentionally out of bounds

        // Act
        var loadedCanvas = project.ToCanvas();

        // Assert
        loadedCanvas.CurrentFrameIndex.Should().Be(2); // Clamped to 2 (Frames.Count - 1)

        project.ActiveFrameIndex = -5;
        var loadedCanvas2 = project.ToCanvas();
        loadedCanvas2.CurrentFrameIndex.Should().Be(0); // Clamped to 0
    }

    [Fact]
    public void ToCanvas_MissingOrNullTags_InitializesEmptyTagsCollection()
    {
        // Arrange
        var canvas = new PixelCanvas(4, 4);
        var project = Project.FromCanvas(canvas);
        project.Tags = null!; // Intentionally null

        // Act
        var loadedCanvas = project.ToCanvas();

        // Assert
        loadedCanvas.Tags.Should().NotBeNull();
        loadedCanvas.Tags.Should().BeEmpty();
    }

    [Fact]
    public void ValidateProject_UnsupportedFormatVersions_ThrowsInvalidDataException()
    {
        // Arrange
        var projectV0 = new Project(4, 4) { FormatVersion = 0 };
        projectV0.Layers.Add(LayerData.FromLayer(new Layer("L1", 4, 4)));

        var projectV3 = new Project(4, 4) { FormatVersion = 3 };
        projectV3.Layers.Add(LayerData.FromLayer(new Layer("L1", 4, 4)));

        // Act & Assert
        Action actV0 = () => _service.ValidateProject(projectV0);
        actV0.Should().Throw<InvalidDataException>()
            .WithMessage("*Unsupported project format version*");

        Action actV3 = () => _service.ValidateProject(projectV3);
        actV3.Should().Throw<InvalidDataException>()
            .WithMessage("*Unsupported project format version*");
    }

    [Fact]
    public void ValidateProject_CelBufferMismatch_ThrowsInvalidDataException()
    {
        // Arrange
        var project = new Project(4, 4) { FormatVersion = 2 };
        var layer = new Layer("L1", 4, 4);
        project.Layers.Add(LayerData.FromLayer(layer));

        var frame = new FrameData
        {
            DurationMs = 100,
            Cels = new List<CelData>
            {
                new CelData
                {
                    LayerId = layer.Id,
                    Pixels = new byte[10] // Invalid size (expected 4 * 4 * 4 = 64)
                }
            }
        };
        project.Frames.Add(frame);

        // Act & Assert
        Action act = () => _service.ValidateProject(project);
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*has invalid pixel buffer size*");
    }

    [Fact]
    public void TagData_InvalidHexAndLoopMode_FallsBackToDefaults()
    {
        // Arrange
        var tagData = new TagData
        {
            Id = Guid.NewGuid(),
            Name = "FallbackTag",
            FromFrame = 0,
            ToFrame = 1,
            Color = "NotAColor",
            LoopMode = "UnknownLoopMode",
            RepeatCount = -1
        };

        // Act
        var tag = tagData.ToTag();

        // Assert
        tag.Name.Should().Be("FallbackTag");
        tag.Color.Should().Be(new PixelColor(0x4A, 0x90, 0xE2, 0xFF)); // Default color
        tag.LoopMode.Should().Be(AnimationLoopMode.Forward); // Default loop mode
        tag.RepeatCount.Should().Be(0); // Clamped to >= 0
    }
}

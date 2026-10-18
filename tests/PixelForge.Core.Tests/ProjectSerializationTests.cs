using FluentAssertions;
using PixelForge.Core.Models;
using PixelForge.Core.Services;
using Xunit;

namespace PixelForge.Core.Tests;

public class ProjectSerializationTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ProjectService _service = new();

    public ProjectSerializationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PixelForge_Tests_" + Guid.NewGuid().ToString("N"));
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
    public async Task SaveAndLoad_ValidProject_PreservesAllDataAccurately()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "test_project.pxproj");
        var canvas = new PixelCanvas(16, 16);
        canvas.ActiveLayer!.Name = "Background";
        canvas.ActiveLayer.SetPixel(0, 0, PixelColor.Red);
        canvas.ActiveLayer.SetPixel(15, 15, PixelColor.Blue);

        var layer2 = canvas.AddLayer("Foreground");
        layer2.Opacity = 0.75f;
        layer2.SetPixel(5, 5, PixelColor.Green);

        var palette = new[] { PixelColor.Red, PixelColor.Green, PixelColor.Blue };
        var project = Project.FromCanvas(canvas, palette);

        // Act
        await _service.SaveProjectAsync(project, filePath);
        var loadedProject = await _service.LoadProjectAsync(filePath);
        var loadedCanvas = loadedProject.ToCanvas();

        // Assert
        loadedProject.Width.Should().Be(16);
        loadedProject.Height.Should().Be(16);
        loadedProject.Layers.Should().HaveCount(2);
        loadedProject.Palette.Should().HaveCount(3);

        loadedCanvas.Width.Should().Be(16);
        loadedCanvas.Height.Should().Be(16);
        loadedCanvas.Layers.Should().HaveCount(2);
        loadedCanvas.Layers[0].Name.Should().Be("Background");
        loadedCanvas.Layers[0].GetPixel(0, 0).Should().Be(PixelColor.Red);
        loadedCanvas.Layers[0].GetPixel(15, 15).Should().Be(PixelColor.Blue);
        loadedCanvas.Layers[1].Name.Should().Be("Foreground");
        loadedCanvas.Layers[1].Opacity.Should().BeApproximately(0.75f, 0.01f);
        loadedCanvas.Layers[1].GetPixel(5, 5).Should().Be(PixelColor.Green);
    }

    [Fact]
    public async Task LoadProject_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var missingPath = Path.Combine(_tempDirectory, "does_not_exist.pxproj");

        // Act
        Func<Task> act = async () => await _service.LoadProjectAsync(missingPath);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task LoadProject_MalformedJson_ThrowsInvalidDataException()
    {
        // Arrange
        var corruptPath = Path.Combine(_tempDirectory, "corrupt.pxproj");
        await File.WriteAllTextAsync(corruptPath, "{ this is not valid json! }");

        // Act
        Func<Task> act = async () => await _service.LoadProjectAsync(corruptPath);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task LoadProject_UnsupportedFormatVersion_ThrowsInvalidDataException()
    {
        // Arrange
        var invalidVersionPath = Path.Combine(_tempDirectory, "invalid_ver.pxproj");
        string json = """
        {
            "formatVersion": 999,
            "width": 16,
            "height": 16,
            "layers": [
                {
                    "name": "Layer 1",
                    "pixels": ""
                }
            ]
        }
        """;
        await File.WriteAllTextAsync(invalidVersionPath, json);

        // Act
        Func<Task> act = async () => await _service.LoadProjectAsync(invalidVersionPath);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*Unsupported project format version*");
    }

    [Fact]
    public async Task LoadProject_MismatchedPixelBuffer_ThrowsInvalidDataException()
    {
        // Arrange
        var corruptPath = Path.Combine(_tempDirectory, "mismatched_pixels.pxproj");
        string json = """
        {
            "formatVersion": 1,
            "width": 16,
            "height": 16,
            "layers": [
                {
                    "name": "Layer 1",
                    "pixels": "AAAAAA=="
                }
            ]
        }
        """;
        await File.WriteAllTextAsync(corruptPath, json);

        // Act
        Func<Task> act = async () => await _service.LoadProjectAsync(corruptPath);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*invalid pixel buffer size*");
    }

    [Fact]
    public async Task LoadProject_NoLayers_ThrowsInvalidDataException()
    {
        // Arrange
        var emptyLayersPath = Path.Combine(_tempDirectory, "no_layers.pxproj");
        string json = """
        {
            "formatVersion": 1,
            "width": 16,
            "height": 16,
            "layers": []
        }
        """;
        await File.WriteAllTextAsync(emptyLayersPath, json);

        // Act
        Func<Task> act = async () => await _service.LoadProjectAsync(emptyLayersPath);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*at least one layer*");
    }

    [Fact]
    public async Task AutosaveService_SaveAndEmergencyRecovery_WorksAsExpected()
    {
        // Arrange
        var customPath = Path.Combine(_tempDirectory, "autosave_test.pxproj");
        var autosaveService = new AutosaveService(_service, customPath);
        var canvas = new PixelCanvas(8, 8);
        canvas.ActiveLayer!.SetPixel(0, 0, PixelColor.Magenta);
        var project = Project.FromCanvas(canvas);

        // Act
        autosaveService.Start(() => project, TimeSpan.FromMinutes(5));
        await autosaveService.TriggerSaveAsync();

        // Assert
        autosaveService.HasEmergencyCopy.Should().BeTrue();
        var emergencyCopy = await autosaveService.LoadEmergencyCopyAsync();
        emergencyCopy.Should().NotBeNull();
        emergencyCopy!.Width.Should().Be(8);
        emergencyCopy.Height.Should().Be(8);

        // Act
        autosaveService.DeleteEmergencyCopy();
        autosaveService.HasEmergencyCopy.Should().BeFalse();

        autosaveService.Stop();
        await autosaveService.DisposeAsync();
    }
}

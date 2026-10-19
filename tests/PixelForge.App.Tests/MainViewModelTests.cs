using FluentAssertions;
using PixelForge.App.Services;
using PixelForge.App.ViewModels;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Services;
using Xunit;

namespace PixelForge.App.Tests;

public class MainViewModelTests
{
    private class DummyFileDialogService : IFileDialogService
    {
        public Task<string?> ShowOpenProjectDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveProjectDialogAsync(string? defaultFileName = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowExportImageDialogAsync(string defaultExtension = "png", string? defaultFileName = null) => Task.FromResult<string?>(null);
    }

    private class DummyClipboardService : IClipboardService
    {
        public Task SetTextAsync(string text) => Task.CompletedTask;
        public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);
        public Task SetPixelsAsync(PixelColor[] pixels, int width, int height) => Task.CompletedTask;
        public Task<(PixelColor[] Pixels, int Width, int Height)?> GetPixelsAsync() => Task.FromResult<(PixelColor[], int, int)?>(null);
        public Task<bool> HasPixelsAsync() => Task.FromResult(false);
    }

    private class DummyUpdateService : IUpdateService
    {
        public string CurrentVersion => "0.1.1";
        public Task<UpdateCheckResult> CheckForUpdatesAsync(bool silent = true, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UpdateCheckResult { Success = true, CurrentVersion = "0.1.1" });
        }
        public void OpenDownloadPage(string url) { }
    }

    private static MainViewModel CreateMainViewModel()
    {
        return new MainViewModel(
            new ProjectService(),
            new ExportService(),
            new AutosaveService(new ProjectService()),
            new DummyFileDialogService(),
            new DummyClipboardService(),
            new DummyUpdateService(),
            new ThemeService(),
            new UndoRedoService());
    }

    [Fact]
    public void ResizeCanvas_UpdatesCanvasDimensions_AndSupportsUndoRedo()
    {
        // Arrange
        var vm = CreateMainViewModel();
        vm.CanvasViewModel.CanvasWidth.Should().Be(32);
        vm.CanvasViewModel.CanvasHeight.Should().Be(32);

        // Act - Resize to 64x64
        vm.ResizeCanvas(64, 64, CanvasAnchor.Center, ResizeMode.Anchor);

        // Assert
        vm.CanvasViewModel.CanvasWidth.Should().Be(64);
        vm.CanvasViewModel.CanvasHeight.Should().Be(64);
        vm.CanvasViewModel.DimensionsText.Should().Be("64 × 64 px");
        vm.IsDirty.Should().BeTrue();

        // Undo
        vm.HistoryViewModel.Undo();
        vm.CanvasViewModel.CanvasWidth.Should().Be(32);
        vm.CanvasViewModel.CanvasHeight.Should().Be(32);
        vm.CanvasViewModel.DimensionsText.Should().Be("32 × 32 px");

        // Redo
        vm.HistoryViewModel.Redo();
        vm.CanvasViewModel.CanvasWidth.Should().Be(64);
        vm.CanvasViewModel.CanvasHeight.Should().Be(64);
    }

    [Fact]
    public void FlipActiveLayer_FlipsHorizontallyAndVertically()
    {
        // Arrange
        var vm = CreateMainViewModel();
        var layer = vm.CanvasViewModel.Canvas.ActiveLayer!;
        var color = new PixelColor(255, 128, 0, 255);
        layer.SetPixel(0, 0, color);

        // Act - Horizontal
        vm.FlipActiveLayerHorizontal();
        layer.GetPixel(layer.Width - 1, 0).Should().Be(color);
        layer.GetPixel(0, 0).Should().Be(PixelColor.Transparent);

        // Act - Vertical
        vm.FlipActiveLayerVertical();
        layer.GetPixel(layer.Width - 1, layer.Height - 1).Should().Be(color);

        // Undo
        vm.HistoryViewModel.Undo();
        layer.GetPixel(layer.Width - 1, 0).Should().Be(color);
    }

    [Fact]
    public void ClearActiveLayer_ClearsPixels_AndCanBeUndone()
    {
        // Arrange
        var vm = CreateMainViewModel();
        var layer = vm.CanvasViewModel.Canvas.ActiveLayer!;
        var color = new PixelColor(0, 255, 255, 255);
        layer.SetPixel(5, 5, color);

        // Act
        vm.ClearActiveLayer();
        layer.GetPixel(5, 5).Should().Be(PixelColor.Transparent);

        // Undo
        vm.HistoryViewModel.Undo();
        layer.GetPixel(5, 5).Should().Be(color);
    }

    [Fact]
    public void SelectAll_And_Deselect_ShouldManageSelectionState()
    {
        // Arrange
        var vm = CreateMainViewModel();
        var selection = vm.CanvasViewModel.Selection;

        // Act - Select All
        vm.SelectAll();
        selection.IsActive.Should().BeTrue();
        selection.Bounds.Width.Should().Be(vm.CanvasViewModel.CanvasWidth);
        selection.Bounds.Height.Should().Be(vm.CanvasViewModel.CanvasHeight);

        // Act - Deselect
        vm.Deselect();
        selection.IsActive.Should().BeFalse();
    }

    [Fact]
    public void DeleteSelectionOrClearLayer_WithActiveSelection_DeletesOnlySelectedPixels()
    {
        // Arrange
        var vm = CreateMainViewModel();
        var layer = vm.CanvasViewModel.Canvas.ActiveLayer!;
        var color = new PixelColor(255, 0, 0, 255);
        layer.SetPixel(2, 2, color);
        layer.SetPixel(10, 10, color);

        vm.CanvasViewModel.Selection.Select(new SkiaSharp.SKRectI(0, 0, 5, 5));

        // Act
        vm.DeleteSelectionOrClearLayer();

        // Assert: pixel inside selection deleted, pixel outside preserved
        layer.GetPixel(2, 2).Should().Be(PixelColor.Transparent);
        layer.GetPixel(10, 10).Should().Be(color);
        vm.CanvasViewModel.Selection.IsActive.Should().BeFalse();

        // Undo should restore deleted pixels
        vm.HistoryViewModel.Undo();
        layer.GetPixel(2, 2).Should().Be(color);
    }
}

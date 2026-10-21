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
        public string CurrentVersion => "0.1.3";
        public Task<UpdateCheckResult> CheckForUpdatesAsync(bool silent = true, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UpdateCheckResult { Success = true, CurrentVersion = "0.1.3" });
        }
        public void OpenDownloadPage(string url) { }
        public Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
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

    [Fact]
    public void LayerViewModel_MoveUpAndMoveDown_ChangesLayerOrderAndSupportsUndoRedo()
    {
        // Arrange
        var vm = CreateMainViewModel();
        var layer1 = vm.CanvasViewModel.Canvas.Layers[0];
        vm.LayerViewModel.AddLayer();
        var layer2 = vm.LayerViewModel.ActiveLayer!;
        vm.LayerViewModel.AddLayer();
        var layer3 = vm.LayerViewModel.ActiveLayer!;

        vm.LayerViewModel.ActiveLayer = layer2; // Index 1
        vm.LayerViewModel.CanMoveUp.Should().BeTrue();
        vm.LayerViewModel.CanMoveDown.Should().BeTrue();

        // Act - Move layer 2 up to index 2
        vm.LayerViewModel.MoveUp();

        // Assert
        vm.CanvasViewModel.Canvas.Layers[2].Should().BeSameAs(layer2);
        vm.CanvasViewModel.Canvas.Layers[1].Should().BeSameAs(layer3);

        // Undo
        vm.HistoryViewModel.Undo();
        vm.CanvasViewModel.Canvas.Layers[1].Should().BeSameAs(layer2);
        vm.CanvasViewModel.Canvas.Layers[2].Should().BeSameAs(layer3);

        // Act - Move layer 2 down to index 0
        vm.LayerViewModel.MoveDown();
        vm.CanvasViewModel.Canvas.Layers[0].Should().BeSameAs(layer2);
        vm.CanvasViewModel.Canvas.Layers[1].Should().BeSameAs(layer1);

        // Undo
        vm.HistoryViewModel.Undo();
        vm.CanvasViewModel.Canvas.Layers[1].Should().BeSameAs(layer2);
    }

    [Fact]
    public void ToggleTiledMode_TogglesStateAndUpdatesStatus()
    {
        // Arrange
        var vm = CreateMainViewModel();
        vm.IsTiledMode.Should().BeFalse();

        // Act
        vm.ToggleTiledMode();

        // Assert
        vm.IsTiledMode.Should().BeTrue();
        vm.StatusMessage.Should().Contain("Tiled Mode");

        // Act again
        vm.ToggleTiledMode();
        vm.IsTiledMode.Should().BeFalse();
        vm.StatusMessage.Should().Contain("выключен");
    }

    [Fact]
    public void AddOutlineCommand_CreatesOutlineOnActiveLayer_AndSupportsUndoRedo()
    {
        // Arrange
        var vm = CreateMainViewModel();
        var layer = vm.CanvasViewModel.Canvas.ActiveLayer!;
        var shapeColor = new PixelColor(255, 255, 255, 255);
        layer.SetPixel(10, 10, shapeColor);

        var outlineColor = new PixelColor(255, 0, 0, 255);
        vm.PaletteViewModel.PrimaryColor = outlineColor;
        vm.ToolViewModel.SetBrushSize(1);

        // Act
        vm.AddOutline();

        // Assert: 4-way neighbors around (10, 10) are now outlineColor
        layer.GetPixel(10, 9).Should().Be(outlineColor);
        layer.GetPixel(10, 11).Should().Be(outlineColor);
        layer.GetPixel(9, 10).Should().Be(outlineColor);
        layer.GetPixel(11, 10).Should().Be(outlineColor);
        layer.GetPixel(10, 10).Should().Be(shapeColor); // Shape preserved
        vm.IsDirty.Should().BeTrue();

        // Undo
        vm.HistoryViewModel.Undo();
        layer.GetPixel(10, 9).Should().Be(PixelColor.Transparent);
        layer.GetPixel(10, 10).Should().Be(shapeColor);
    }

    [Fact]
    public void AddShadowCommand_CreatesShadow_AndSupportsUndoRedo()
    {
        // Arrange
        var vm = CreateMainViewModel();
        var layer = vm.CanvasViewModel.Canvas.ActiveLayer!;
        var shapeColor = new PixelColor(200, 200, 200, 255);
        layer.SetPixel(15, 15, shapeColor);

        // Act
        vm.AddShadow();

        // Assert: offset pixel at (16, 16) has shadow applied
        var shadowPixel = layer.GetPixel(16, 16);
        shadowPixel.A.Should().BeGreaterThan(0);
        layer.GetPixel(15, 15).Should().Be(shapeColor);

        // Undo
        vm.HistoryViewModel.Undo();
        layer.GetPixel(16, 16).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void ReplaceColorCommand_ReplacesTargetColorWithPrimaryColor_AndSupportsUndoRedo()
    {
        // Arrange
        var vm = CreateMainViewModel();
        var layer = vm.CanvasViewModel.Canvas.ActiveLayer!;
        var oldColor = new PixelColor(0, 0, 255, 255);
        var newColor = new PixelColor(255, 255, 0, 255);
        layer.SetPixel(5, 5, oldColor);
        layer.SetPixel(6, 6, oldColor);

        vm.PaletteViewModel.SecondaryColor = oldColor;
        vm.PaletteViewModel.PrimaryColor = newColor;
        vm.ToolViewModel.FillTolerance = 0;

        // Act
        vm.ReplaceColor();

        // Assert
        layer.GetPixel(5, 5).Should().Be(newColor);
        layer.GetPixel(6, 6).Should().Be(newColor);

        // Undo
        vm.HistoryViewModel.Undo();
        layer.GetPixel(5, 5).Should().Be(oldColor);
        layer.GetPixel(6, 6).Should().Be(oldColor);
    }

    [Fact]
    public void LayerViewModel_ToggleAlphaLock_TogglesIsAlphaLocked()
    {
        // Arrange
        var vm = CreateMainViewModel();
        var layer = vm.CanvasViewModel.Canvas.ActiveLayer!;
        layer.IsAlphaLocked.Should().BeFalse();

        // Act
        vm.LayerViewModel.ToggleAlphaLock(layer);

        // Assert
        layer.IsAlphaLocked.Should().BeTrue();

        // Act again without argument (defaults to ActiveLayer)
        vm.LayerViewModel.ToggleAlphaLock();
        layer.IsAlphaLocked.Should().BeFalse();
    }

    [Fact]
    public void OpenHotkeysDialog_InvokesShowHotkeysRequestedEvent()
    {
        // Arrange
        var vm = CreateMainViewModel();
        bool eventFired = false;
        object? senderReceived = null;
        vm.ShowHotkeysRequested += (sender, _) =>
        {
            eventFired = true;
            senderReceived = sender;
        };

        // Act
        vm.OpenHotkeysDialog();

        // Assert
        eventFired.Should().BeTrue();
        senderReceived.Should().BeSameAs(vm);
    }

    [Fact]
    public void OpenHotkeysDialogCommand_Execution_InvokesShowHotkeysRequestedEvent()
    {
        // Arrange
        var vm = CreateMainViewModel();
        bool eventFired = false;
        vm.ShowHotkeysRequested += (_, _) => eventFired = true;

        // Act
        vm.OpenHotkeysDialogCommand.Execute(null);

        // Assert
        eventFired.Should().BeTrue();
    }

    [Fact]
    public void PaletteViewModel_SwapColors_ExchangesPrimaryAndSecondaryColors()
    {
        // Arrange
        var pvm = new PaletteViewModel();
        var red = new PixelColor(255, 0, 0, 255);
        var blue = new PixelColor(0, 0, 255, 255);
        pvm.PrimaryColor = red;
        pvm.SecondaryColor = blue;

        // Act
        pvm.SwapColors();

        // Assert
        pvm.PrimaryColor.Should().Be(blue);
        pvm.SecondaryColor.Should().Be(red);
    }

    [Fact]
    public void PaletteViewModel_SelectPrimaryAndSecondaryColor_UpdatesProperties()
    {
        // Arrange
        var pvm = new PaletteViewModel();
        var magenta = new PixelColor(255, 0, 255, 255);
        var cyan = new PixelColor(0, 255, 255, 255);

        // Act
        pvm.SelectPrimaryColor(magenta);
        pvm.SelectSecondaryColor(cyan);

        // Assert
        pvm.PrimaryColor.Should().Be(magenta);
        pvm.SecondaryColor.Should().Be(cyan);
    }

    [Fact]
    public void PaletteViewModel_LoadPreset_UpdatesCurrentPaletteAndColorsList()
    {
        // Arrange
        var pvm = new PaletteViewModel();
        var dawnBringer = PixelForge.Core.Palettes.PalettePresets.DawnBringer16;

        // Act
        pvm.LoadPreset(dawnBringer);

        // Assert
        pvm.CurrentPalette.Name.Should().Be("DawnBringer 16");
        pvm.Colors.Should().HaveCount(16);
        pvm.Colors[0].Should().Be(dawnBringer.Colors[0]);
    }

    [Fact]
    public void PaletteViewModel_LoadPresetByName_FindsAndLoadsCorrectPreset()
    {
        // Arrange
        var pvm = new PaletteViewModel();

        // Act
        pvm.LoadPresetByName("PICO-8");

        // Assert
        pvm.CurrentPalette.Name.Should().Be("PICO-8");
        pvm.Colors.Should().HaveCount(16);
    }

    [Fact]
    public void PaletteViewModel_AddAndRemoveColor_ModifiesColorsAndCurrentPalette()
    {
        // Arrange
        var pvm = new PaletteViewModel();
        var customColor = new PixelColor(123, 234, 45, 255);
        int initialCount = pvm.Colors.Count;

        // Act - Add
        pvm.AddColor(customColor);

        // Assert
        pvm.Colors.Should().HaveCount(initialCount + 1);
        pvm.Colors.Should().Contain(customColor);
        pvm.CurrentPalette.Colors.Should().Contain(customColor);

        // Act - Duplicate Add should not add again
        pvm.AddColor(customColor);
        pvm.Colors.Should().HaveCount(initialCount + 1);

        // Act - Remove
        pvm.RemoveColor(customColor);

        // Assert
        pvm.Colors.Should().HaveCount(initialCount);
        pvm.Colors.Should().NotContain(customColor);
        pvm.CurrentPalette.Colors.Should().NotContain(customColor);
    }
}

using System.ComponentModel;
using FluentAssertions;
using PixelForge.App.Converters;
using PixelForge.App.ViewModels;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using SkiaSharp;
using Xunit;

namespace PixelForge.App.Tests;

public class UXPolishAndStatusFeedbackTests
{
    [Fact]
    public void CanvasViewModel_CursorTracking_UpdatesCoordinatesAndHoveredColor()
    {
        // Arrange
        var canvas = new PixelCanvas(32, 32);
        var vm = new CanvasViewModel(canvas);
        var testColor = new PixelColor(255, 128, 0, 255);

        int propertyChangeCount = 0;
        vm.PropertyChanged += (s, e) => propertyChangeCount++;

        // Act - Update cursor inside canvas
        vm.UpdateCursor(10, 20, true, testColor);

        // Assert
        vm.CursorX.Should().Be(10);
        vm.CursorY.Should().Be(20);
        vm.IsCursorInside.Should().BeTrue();
        vm.CursorCoordinatesText.Should().Be("X: 10, Y: 20");
        vm.HoveredColor.Should().Be(testColor);
        vm.HasHoveredColor.Should().BeTrue();
        vm.HoveredColorHex.Should().Be("#FF8000");

        // Act - Sub-pixel mouse movement within same pixel (Zero-alloc guard)
        int countBefore = propertyChangeCount;
        vm.UpdateCursor(10, 20, true, testColor);

        // Assert: no redundant property changed notifications
        propertyChangeCount.Should().Be(countBefore);

        // Act - Cursor leaves canvas
        vm.UpdateCursor(-1, -1, false, PixelColor.Transparent);
        vm.IsCursorInside.Should().BeFalse();
        vm.CursorCoordinatesText.Should().Be("—");
        vm.HasHoveredColor.Should().BeFalse();
    }

    [Fact]
    public void CanvasViewModel_SelectionTracking_ReflectsActiveSelectionAndDimensions()
    {
        // Arrange
        var canvas = new PixelCanvas(64, 64);
        var vm = new CanvasViewModel(canvas);

        vm.HasActiveSelection.Should().BeFalse();
        vm.SelectionText.Should().BeEmpty();
        vm.SelectionShortText.Should().BeEmpty();

        // Act - Select rectangle (10, 15) to (30, 45) -> 20x30
        vm.Selection.Select(new SKRectI(10, 15, 30, 45));

        // Assert
        vm.HasActiveSelection.Should().BeTrue();
        vm.SelectionShortText.Should().Be("20 × 30");
        vm.SelectionText.Should().Be("20 × 30 px (10, 15)");

        // Act - Clear selection
        vm.Selection.Clear();

        // Assert
        vm.HasActiveSelection.Should().BeFalse();
        vm.SelectionText.Should().BeEmpty();
        vm.SelectionShortText.Should().BeEmpty();
    }

    [Fact]
    public void CanvasViewModel_SymmetryTracking_ReflectsState()
    {
        // Arrange
        var canvas = new PixelCanvas(32, 32);
        var vm = new CanvasViewModel(canvas);

        vm.HasActiveSymmetry.Should().BeFalse();

        // Act - SymX
        vm.ToggleSymmetryX();
        vm.HasActiveSymmetry.Should().BeTrue();
        vm.SymmetryStatusText.Should().Be("SYM X");

        // Act - SymY
        vm.ToggleSymmetryY();
        vm.HasActiveSymmetry.Should().BeTrue();
        vm.SymmetryStatusText.Should().Be("SYM XY");

        // Act - Disable SymX
        vm.ToggleSymmetryX();
        vm.HasActiveSymmetry.Should().BeTrue();
        vm.SymmetryStatusText.Should().Be("SYM Y");

        // Act - Disable SymY
        vm.ToggleSymmetryY();
        vm.HasActiveSymmetry.Should().BeFalse();
    }

    [Fact]
    public void CanvasViewModel_CycleZoomPreset_CyclesThroughStandardScales()
    {
        // Arrange
        var canvas = new PixelCanvas(32, 32);
        var vm = new CanvasViewModel(canvas);

        // Act - Set to 1.0f and cycle
        vm.SetZoom(1.0f);
        vm.Zoom.Should().Be(1.0f);

        vm.CycleZoomPreset();
        vm.Zoom.Should().Be(2.0f);

        vm.CycleZoomPreset();
        vm.Zoom.Should().Be(4.0f);

        vm.CycleZoomPreset();
        vm.Zoom.Should().Be(8.0f);

        vm.CycleZoomPreset();
        vm.Zoom.Should().Be(16.0f);

        vm.CycleZoomPreset();
        vm.Zoom.Should().Be(1.0f); // Wraps around
    }

    [Fact]
    public void PaletteViewModel_ColorCycling_SelectsNextAndPrevious()
    {
        // Arrange
        var pvm = new PaletteViewModel();
        pvm.Colors.Count.Should().BeGreaterThan(2);

        var firstColor = pvm.Colors[0];
        var secondColor = pvm.Colors[1];
        var lastColor = pvm.Colors[^1];

        pvm.PrimaryColor = firstColor;

        string? receivedStatus = null;
        pvm.StatusRequested += (s, msg) => receivedStatus = msg;

        // Act - Next
        pvm.SelectNextColor();
        pvm.PrimaryColor.Should().Be(secondColor);
        receivedStatus.Should().NotBeNullOrEmpty();

        // Act - Prev (back to first)
        pvm.SelectPreviousColor();
        pvm.PrimaryColor.Should().Be(firstColor);

        // Act - Prev again (wraps to last)
        pvm.SelectPreviousColor();
        pvm.PrimaryColor.Should().Be(lastColor);
    }

    [Fact]
    public void LayerViewModel_Operations_EmitStatusFeedback()
    {
        // Arrange
        var canvas = new PixelCanvas(32, 32);
        var undoRedo = new UndoRedoService();
        var lvm = new LayerViewModel(canvas, undoRedo);

        var statusHistory = new List<string>();
        lvm.StatusRequested += (s, msg) => statusHistory.Add(msg);

        // Act 1: Add Layer
        lvm.AddLayer();
        statusHistory.Should().Contain(msg => msg.Contains("Created Layer 2"));

        // Act 2: Alpha lock
        lvm.ToggleAlphaLock();
        lvm.IsActiveLayerAlphaLocked.Should().BeTrue();
        statusHistory.Should().Contain(msg => msg.Contains("Alpha Lock: enabled"));

        // Act 3: Toggle visibility
        lvm.ToggleVisibility(lvm.ActiveLayer);
        statusHistory.Should().Contain(msg => msg.Contains("hidden"));

        // Act 4: Duplicate Layer
        lvm.DuplicateLayer();
        statusHistory.Should().Contain(msg => msg.Contains("Duplicated"));

        // Act 5: Merge Down
        lvm.MergeDown();
        statusHistory.Should().Contain(msg => msg.Contains("Merged"));
    }

    [Fact]
    public void ToolViewModel_IsPixelPerfectActive_OnlyWhenSupportedAndEnabled()
    {
        // Arrange
        var tvm = new ToolViewModel();

        // Pencil supports pixel perfect
        tvm.SelectTool(CanvasToolType.Pencil);
        tvm.SupportsPixelPerfect.Should().BeTrue();
        tvm.IsPixelPerfect = false;
        tvm.IsPixelPerfectActive.Should().BeFalse();

        tvm.TogglePixelPerfect();
        tvm.IsPixelPerfect.Should().BeTrue();
        tvm.IsPixelPerfectActive.Should().BeTrue();

        // Switch to Fill which does not support pixel-perfect
        tvm.SelectTool(CanvasToolType.Fill);
        tvm.SupportsPixelPerfect.Should().BeFalse();
        tvm.IsPixelPerfectActive.Should().BeFalse();

        // Switch back to Pencil
        tvm.SelectTool(CanvasToolType.Pencil);
        tvm.IsPixelPerfectActive.Should().BeTrue();
    }

    [Fact]
    public void PixelColorToBrushConverter_ConvertsColorToBrush()
    {
        // Arrange
        var converter = PixelColorToBrushConverter.Instance;
        var color = new PixelColor(255, 0, 128, 200);

        // Act
        var result = converter.Convert(color, typeof(Avalonia.Media.IBrush), null, System.Globalization.CultureInfo.InvariantCulture);

        // Assert
        result.Should().BeOfType<Avalonia.Media.SolidColorBrush>();
        var brush = (Avalonia.Media.SolidColorBrush)result!;
        brush.Color.R.Should().Be(255);
        brush.Color.G.Should().Be(0);
        brush.Color.B.Should().Be(128);
        brush.Color.A.Should().Be(200);
    }

    [Fact]
    public void Layer_IsRenaming_NotifiesPropertyChanged()
    {
        // Arrange
        var layer = new Layer("Background", 16, 16);
        bool notified = false;
        layer.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Layer.IsRenaming)) notified = true;
        };

        // Act & Assert
        layer.IsRenaming.Should().BeFalse();
        layer.IsRenaming = true;
        notified.Should().BeTrue();
        layer.IsRenaming.Should().BeTrue();
    }

    [Fact]
    public void Localization_NewToolbarKeys_ExistAndAreNonEmpty()
    {
        // Arrange
        var loc = PixelForge.Core.Localization.LocalizationService.Instance;

        // Act & Assert
        loc.Get("Tip_QuickExport").Should().NotBeNullOrWhiteSpace();
        loc.Get("Tip_FlipH").Should().NotBeNullOrWhiteSpace();
        loc.Get("Tip_FlipV").Should().NotBeNullOrWhiteSpace();
        loc.Get("Tip_RotateCW").Should().NotBeNullOrWhiteSpace();
        loc.Get("Tip_SmartTools").Should().NotBeNullOrWhiteSpace();
    }
}

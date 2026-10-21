using FluentAssertions;
using PixelForge.App.ViewModels;
using PixelForge.Core.Patterns;
using PixelForge.Core.Tools;
using Xunit;

namespace PixelForge.App.Tests;

public class ToolViewModelTests
{
    [Fact]
    public void SetBrushSize_ClampsValueBetween1And32()
    {
        // Arrange
        var vm = new ToolViewModel();

        // Act & Assert
        vm.SetBrushSize(0);
        vm.BrushSize.Should().Be(1);

        vm.SetBrushSize(5);
        vm.BrushSize.Should().Be(5);

        vm.SetBrushSize(32);
        vm.BrushSize.Should().Be(32);

        vm.SetBrushSize(50);
        vm.BrushSize.Should().Be(32);
    }

    [Fact]
    public void IncreaseAndDecreaseBrushSize_WorkWithinBounds()
    {
        // Arrange
        var vm = new ToolViewModel();
        vm.BrushSize.Should().Be(1);

        // Act - Decrease at min
        vm.DecreaseBrushSize();
        vm.BrushSize.Should().Be(1);

        // Act - Increase
        vm.IncreaseBrushSize();
        vm.BrushSize.Should().Be(2);

        vm.DecreaseBrushSize();
        vm.BrushSize.Should().Be(1);
    }

    [Theory]
    [InlineData(CanvasToolType.Pencil, "Карандаш", true, false)]
    [InlineData(CanvasToolType.Eraser, "Ластик", true, false)]
    [InlineData(CanvasToolType.Fill, "Заливка", false, false)]
    [InlineData(CanvasToolType.Eyedropper, "Пипетка", false, false)]
    [InlineData(CanvasToolType.Line, "Линия", true, false)]
    [InlineData(CanvasToolType.Rectangle, "Прямоугольник", false, true)]
    [InlineData(CanvasToolType.Ellipse, "Эллипс", false, true)]
    [InlineData(CanvasToolType.Selection, "Выделение", false, false)]
    [InlineData(CanvasToolType.PatternBrush, "Текстурная кисть", true, false)]
    [InlineData(CanvasToolType.MagicWand, "Волшебная палочка", false, false)]
    public void ToolSelection_UpdatesDisplayAndCapabilitiesCorrectly(
        CanvasToolType toolType,
        string expectedDisplayName,
        bool expectedSupportsBrush,
        bool expectedSupportsFill)
    {
        // Arrange
        var vm = new ToolViewModel();

        // Act
        vm.SelectTool(toolType);

        // Assert
        vm.ActiveTool.Should().Be(toolType);
        vm.ActiveToolDisplayName.Should().Be(expectedDisplayName);
        vm.SupportsBrushSize.Should().Be(expectedSupportsBrush);
        vm.SupportsShapeFill.Should().Be(expectedSupportsFill);
    }

    [Fact]
    public void ToolViewModel_RegistersMagicWandAndPatternBrushTools()
    {
        // Arrange
        var vm = new ToolViewModel();

        // Assert
        vm.MagicWandTool.Should().NotBeNull();
        vm.PatternBrushTool.Should().NotBeNull();
        vm.Tools.Should().ContainKey(CanvasToolType.MagicWand);
        vm.Tools.Should().ContainKey(CanvasToolType.PatternBrush);
        vm.Tools[CanvasToolType.MagicWand].Should().BeSameAs(vm.MagicWandTool);
        vm.Tools[CanvasToolType.PatternBrush].Should().BeSameAs(vm.PatternBrushTool);

        vm.AvailableTools.Should().Contain(t => t.Type == CanvasToolType.MagicWand && t.IconKey == "IconMagicWand");
        vm.AvailableTools.Should().Contain(t => t.Type == CanvasToolType.PatternBrush && t.IconKey == "IconPatternBrush");
    }

    [Fact]
    public void TogglePixelPerfect_TogglesFlagAndUpdatesPatternBrush()
    {
        // Arrange
        var vm = new ToolViewModel();
        vm.IsPixelPerfect.Should().BeFalse();
        vm.PatternBrushTool.PixelPerfect.Should().BeFalse();

        // Act
        vm.TogglePixelPerfect();

        // Assert
        vm.IsPixelPerfect.Should().BeTrue();
        vm.PatternBrushTool.PixelPerfect.Should().BeTrue();

        // Act again
        vm.TogglePixelPerfect();
        vm.IsPixelPerfect.Should().BeFalse();
        vm.PatternBrushTool.PixelPerfect.Should().BeFalse();
    }

    [Fact]
    public void StabilizerStrength_SyncsAndClampsToPatternBrush()
    {
        // Arrange
        var vm = new ToolViewModel();
        vm.StabilizerStrength.Should().Be(0);

        // Act
        vm.StabilizerStrength = 7;

        // Assert
        vm.StabilizerStrength.Should().Be(7);
        vm.PatternBrushTool.StabilizerStrength.Should().Be(7);
    }

    [Fact]
    public void PatternPreset_ProvidesPresetsAndSyncsToTool()
    {
        // Arrange
        var vm = new ToolViewModel();
        vm.Available8x8Patterns.Should().NotBeEmpty();
        vm.SelectedPatternPreset.Name.Should().Be(PatternPresets.Checkerboard.Name);

        // Act
        vm.SelectedPatternPreset = PatternPresets.Bricks;

        // Assert
        vm.PatternBrushTool.Pattern.Should().Be(PatternPresets.Bricks);
    }

    [Fact]
    public void BrushSize_SyncsToPatternBrushTool()
    {
        // Arrange
        var vm = new ToolViewModel();

        // Act
        vm.SetBrushSize(4);

        // Assert
        vm.PatternBrushTool.BrushSize.Should().Be(4);
    }

    [Fact]
    public void FillToleranceAndContiguous_SyncToMagicWandTool()
    {
        // Arrange
        var vm = new ToolViewModel();

        // Act
        vm.FillTolerance = 20;
        vm.FillContiguous = false;

        // Assert
        vm.MagicWandTool.Tolerance.Should().Be((int)(20 * 2.55f));
        vm.MagicWandTool.Contiguous.Should().BeFalse();
    }

    [Fact]
    public void SelectedGradientPreset_SyncsStopsToGradientTool()
    {
        // Arrange
        var vm = new ToolViewModel();
        vm.AvailableGradientPresets.Should().NotBeEmpty();
        vm.SelectedGradientPreset.Should().Be(GradientPresets.TwoTone);
        vm.GradientTool.Stops.Should().BeEmpty();

        // Act
        vm.SelectedGradientPreset = GradientPresets.Sunset;

        // Assert
        vm.GradientTool.Stops.Should().HaveCount(GradientPresets.Sunset.Stops.Count);
        vm.GradientTool.Stops.Should().BeEquivalentTo(GradientPresets.Sunset.Stops);
    }

    [Fact]
    public void GradientType_SyncsToGradientTool()
    {
        // Arrange
        var vm = new ToolViewModel();
        vm.GradientType.Should().Be(GradientType.Linear);
        vm.GradientTool.Type.Should().Be(GradientType.Linear);

        // Act
        vm.GradientType = GradientType.Radial;

        // Assert
        vm.GradientTool.Type.Should().Be(GradientType.Radial);
    }

    [Fact]
    public void ApplyRampAsGradient_SetsStopsAndActivatesGradientTool()
    {
        // Arrange
        var vm = new ToolViewModel();
        vm.ActiveTool = CanvasToolType.Pencil;
        var rampColors = new[]
        {
            new PixelForge.Core.Models.PixelColor(10, 20, 30),
            new PixelForge.Core.Models.PixelColor(50, 60, 70),
            new PixelForge.Core.Models.PixelColor(200, 210, 220)
        };

        // Act
        vm.ApplyRampAsGradient(rampColors, "Тестовая рампа");

        // Assert
        vm.ActiveTool.Should().Be(CanvasToolType.Gradient);
        vm.SelectedGradientPreset.Name.Should().Be("Тестовая рампа");
        vm.GradientTool.Stops.Should().HaveCount(3);
        vm.GradientTool.Stops[0].Position.Should().Be(0f);
        vm.GradientTool.Stops[0].Color.Should().Be(rampColors[0]);
        vm.GradientTool.Stops[2].Position.Should().Be(1f);
        vm.GradientTool.Stops[2].Color.Should().Be(rampColors[2]);
    }
}

using FluentAssertions;
using PixelForge.App.ViewModels;
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
}

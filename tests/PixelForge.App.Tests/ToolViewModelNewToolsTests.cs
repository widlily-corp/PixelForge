using System.Globalization;
using FluentAssertions;
using PixelForge.App.Converters;
using PixelForge.App.ViewModels;
using PixelForge.Core.Localization;
using PixelForge.Core.Models;
using PixelForge.Core.Tools;
using Xunit;

namespace PixelForge.App.Tests;

/// <summary>
/// Comprehensive unit tests verifying ShadeBrush, SprayBrush, ToolContext propagation,
/// DitherPatternLocalizationConverter, and dynamic RU/EN localization.
/// </summary>
public class ToolViewModelNewToolsTests
{
    private static ToolContext CreateTestContext(int width = 16, int height = 16)
    {
        var canvas = new PixelCanvas(width, height);
        return new ToolContext(canvas, canvas.ActiveLayer);
    }

    #region 1. Tool Registration & Metadata Tests

    [Fact]
    public void ToolViewModel_RegistersShadeAndSprayBrushTools()
    {
        // Arrange
        var vm = new ToolViewModel();

        // Assert - Tools Dictionary
        vm.Tools.Should().ContainKey(CanvasToolType.ShadeBrush);
        vm.Tools.Should().ContainKey(CanvasToolType.SprayBrush);

        vm.ShadeBrushTool.Should().NotBeNull();
        vm.SprayBrushTool.Should().NotBeNull();
        vm.Tools[CanvasToolType.ShadeBrush].Should().BeSameAs(vm.ShadeBrushTool);
        vm.Tools[CanvasToolType.SprayBrush].Should().BeSameAs(vm.SprayBrushTool);

        // Assert - AvailableTools list
        var shadeItem = vm.AvailableTools.FirstOrDefault(t => t.Type == CanvasToolType.ShadeBrush);
        shadeItem.Should().NotBeNull();
        shadeItem!.Name.Should().Be("Shade Brush");
        shadeItem.Shortcut.Should().Be("H");
        shadeItem.IconKey.Should().Be("IconShadeBrush");

        var sprayItem = vm.AvailableTools.FirstOrDefault(t => t.Type == CanvasToolType.SprayBrush);
        sprayItem.Should().NotBeNull();
        sprayItem!.Name.Should().Be("Spray Brush");
        sprayItem.Shortcut.Should().Be("A");
        sprayItem.IconKey.Should().Be("IconSprayBrush");
    }

    [Fact]
    public void ToolViewModel_DefaultSpraySettings_HaveValidInitialValues()
    {
        // Arrange
        var vm = new ToolViewModel();

        // Assert
        vm.SprayRadius.Should().Be(4);
        vm.SprayDensity.Should().Be(0.25f);
        vm.IsPixelPerfect.Should().BeFalse();
        vm.ActiveToolContext.Should().BeNull();
        vm.ActiveColorRamp.Should().BeNull();
    }

    #endregion

    #region 2. Tool Switching & Capabilities Tests

    [Theory]
    [InlineData(CanvasToolType.ShadeBrush, false, false, false)]
    [InlineData(CanvasToolType.SprayBrush, false, true, false)]
    [InlineData(CanvasToolType.Pencil, true, false, true)]
    [InlineData(CanvasToolType.Eraser, true, false, true)]
    [InlineData(CanvasToolType.DitherBrush, true, false, true)]
    [InlineData(CanvasToolType.PatternBrush, true, false, true)]
    [InlineData(CanvasToolType.Fill, false, false, false)]
    [InlineData(CanvasToolType.Line, false, false, true)]
    public void ToolSelection_UpdatesCapabilitiesAccurately(
        CanvasToolType toolType,
        bool expectedSupportsPixelPerfect,
        bool expectedSupportsSpraySettings,
        bool expectedSupportsBrushSize)
    {
        // Arrange
        var vm = new ToolViewModel();

        // Act
        vm.SelectTool(toolType);

        // Assert
        vm.ActiveTool.Should().Be(toolType);
        vm.ActiveToolItem.Type.Should().Be(toolType);
        vm.SupportsPixelPerfect.Should().Be(expectedSupportsPixelPerfect);
        vm.SupportsSpraySettings.Should().Be(expectedSupportsSpraySettings);
        vm.SupportsBrushSize.Should().Be(expectedSupportsBrushSize);
    }

    [Fact]
    public void ActiveToolDisplayName_LocalizesCorrectlyForShadeAndSpray()
    {
        // Arrange
        var vm = new ToolViewModel();
        var loc = LocalizationService.Instance;

        try
        {
            // Russian
            loc.SetLanguage(AppLanguage.Russian);
            vm.SelectTool(CanvasToolType.ShadeBrush);
            vm.ActiveToolDisplayName.Should().Be("Кисть светотени");

            vm.SelectTool(CanvasToolType.SprayBrush);
            vm.ActiveToolDisplayName.Should().Be("Спрей / Шум");

            // English
            loc.SetLanguage(AppLanguage.English);
            vm.SelectTool(CanvasToolType.ShadeBrush);
            vm.ActiveToolDisplayName.Should().Be("Shade Brush");

            vm.SelectTool(CanvasToolType.SprayBrush);
            vm.ActiveToolDisplayName.Should().Be("Spray / Noise Brush");
        }
        finally
        {
            loc.SetLanguage(AppLanguage.Russian);
        }
    }

    [Fact]
    public void LanguageChange_RaisesPropertyChangedForActiveToolDisplayNameAndAvailablePatterns()
    {
        // Arrange
        var vm = new ToolViewModel();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null) raised.Add(e.PropertyName);
        };

        // Act
        LocalizationService.Instance.ToggleLanguage();

        // Assert
        raised.Should().Contain(nameof(ToolViewModel.ActiveToolDisplayName));
        raised.Should().Contain(nameof(ToolViewModel.AvailablePatterns));
    }

    #endregion

    #region 3. Property Change Notifications & ToolContext Propagation

    [Fact]
    public void SettingActiveTool_RaisesExpectedPropertyChangedNotifications()
    {
        // Arrange
        var vm = new ToolViewModel();
        var raisedProps = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null) raisedProps.Add(e.PropertyName);
        };

        // Act
        vm.ActiveTool = CanvasToolType.SprayBrush;

        // Assert
        raisedProps.Should().Contain(nameof(ToolViewModel.ActiveTool));
        raisedProps.Should().Contain(nameof(ToolViewModel.ActiveToolItem));
        raisedProps.Should().Contain(nameof(ToolViewModel.ActiveToolDisplayName));
        raisedProps.Should().Contain(nameof(ToolViewModel.SupportsSpraySettings));
        raisedProps.Should().Contain(nameof(ToolViewModel.SupportsPixelPerfect));
    }

    [Fact]
    public void IsPixelPerfect_PropagatesToActiveToolContextAndPatternBrush()
    {
        // Arrange
        var vm = new ToolViewModel();
        var ctx = CreateTestContext();
        vm.ToolContext = ctx;

        // Act
        vm.IsPixelPerfect = true;

        // Assert
        vm.IsPixelPerfect.Should().BeTrue();
        ctx.IsPixelPerfect.Should().BeTrue();
        vm.PatternBrushTool.PixelPerfect.Should().BeTrue();

        // Act via toggle
        vm.TogglePixelPerfect();

        // Assert
        vm.IsPixelPerfect.Should().BeFalse();
        ctx.IsPixelPerfect.Should().BeFalse();
        vm.PatternBrushTool.PixelPerfect.Should().BeFalse();
    }

    [Fact]
    public void SprayRadius_ClampsAndPropagatesToContextAndTool()
    {
        // Arrange
        var vm = new ToolViewModel();
        var ctx = CreateTestContext();
        vm.ToolContext = ctx;

        // Act
        vm.SprayRadius = 8;

        // Assert
        vm.SprayRadius.Should().Be(8);
        ctx.SprayRadius.Should().Be(8);
        vm.SprayBrushTool.Radius.Should().Be(8);

        // Clamping check
        vm.SprayRadius = 25;
        vm.SprayBrushTool.Radius.Should().Be(16);
        ctx.SprayRadius.Should().Be(16);
    }

    [Fact]
    public void SprayDensity_ClampsAndPropagatesToContextAndTool()
    {
        // Arrange
        var vm = new ToolViewModel();
        var ctx = CreateTestContext();
        vm.ToolContext = ctx;

        // Act
        vm.SprayDensity = 0.75f;

        // Assert
        vm.SprayDensity.Should().Be(0.75f);
        ctx.SprayDensity.Should().Be(0.75f);
        vm.SprayBrushTool.Density.Should().Be(0.75f);

        // Clamping check
        vm.SprayDensity = 1.5f;
        vm.SprayBrushTool.Density.Should().Be(1.0f);
        ctx.SprayDensity.Should().Be(1.0f);
    }

    [Fact]
    public void ActiveColorRamp_PropagatesToContextImmediately()
    {
        // Arrange
        var vm = new ToolViewModel();
        var ctx = CreateTestContext();
        vm.ToolContext = ctx;

        var ramp = new[]
        {
            PixelColor.Black,
            PixelColor.Red,
            PixelColor.White
        };

        // Act
        vm.SetActiveColorRamp(ramp);

        // Assert
        vm.ActiveColorRamp.Should().BeSameAs(ramp);
        ctx.ActiveColorRamp.Should().BeSameAs(ramp);
    }

    [Fact]
    public void SettingToolContext_InitializesAllSettingsFromViewModel()
    {
        // Arrange
        var vm = new ToolViewModel
        {
            IsPixelPerfect = true,
            SprayRadius = 10,
            SprayDensity = 0.6f
        };
        var ramp = new[] { PixelColor.Black, PixelColor.White };
        vm.SetActiveColorRamp(ramp);

        var ctx = CreateTestContext();

        // Act
        vm.ToolContext = ctx;

        // Assert: context should immediately receive vm settings
        ctx.IsPixelPerfect.Should().BeTrue();
        ctx.SprayRadius.Should().Be(10);
        ctx.SprayDensity.Should().Be(0.6f);
        ctx.ActiveColorRamp.Should().BeSameAs(ramp);
    }

    #endregion

    #region 4. DitherPatternLocalizationConverter Tests

    [Theory]
    [InlineData(DitherPattern.None, "Без дизеринга")]
    [InlineData(DitherPattern.Checker50, "Шахматка 50%")]
    [InlineData(DitherPattern.Bayer2x2, "Байер 2×2")]
    [InlineData(DitherPattern.Bayer4x4, "Байер 4×4")]
    [InlineData(DitherPattern.Bayer8x8, "Байер 8×8")]
    [InlineData(DitherPattern.Dots25, "Точки 25%")]
    [InlineData(DitherPattern.Dots75, "Точки 75%")]
    [InlineData(DitherPattern.HorizontalLines, "Горизонтальные линии")]
    [InlineData(DitherPattern.VerticalLines, "Вертикальные линии")]
    [InlineData(DitherPattern.DiagonalLines, "Диагональные линии")]
    public void DitherPatternLocalizationConverter_TranslatesAllPatterns_InRussian(
        DitherPattern pattern,
        string expectedRussian)
    {
        // Arrange
        var converter = DitherPatternLocalizationConverter.Instance;
        var loc = LocalizationService.Instance;

        try
        {
            loc.SetLanguage(AppLanguage.Russian);

            // Act
            var result = converter.Convert(pattern, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            result.Should().Be(expectedRussian);
        }
        finally
        {
            loc.SetLanguage(AppLanguage.Russian);
        }
    }

    [Theory]
    [InlineData(DitherPattern.None, "No Dithering")]
    [InlineData(DitherPattern.Checker50, "Checkerboard 50%")]
    [InlineData(DitherPattern.Bayer2x2, "Bayer 2×2")]
    [InlineData(DitherPattern.Bayer4x4, "Bayer 4×4")]
    [InlineData(DitherPattern.Bayer8x8, "Bayer 8×8")]
    [InlineData(DitherPattern.Dots25, "Sparse Dots 25%")]
    [InlineData(DitherPattern.Dots75, "Dense Dots 75%")]
    [InlineData(DitherPattern.HorizontalLines, "Horizontal Lines")]
    [InlineData(DitherPattern.VerticalLines, "Vertical Lines")]
    [InlineData(DitherPattern.DiagonalLines, "Diagonal Lines")]
    public void DitherPatternLocalizationConverter_TranslatesAllPatterns_InEnglish(
        DitherPattern pattern,
        string expectedEnglish)
    {
        // Arrange
        var converter = DitherPatternLocalizationConverter.Instance;
        var loc = LocalizationService.Instance;

        try
        {
            loc.SetLanguage(AppLanguage.English);

            // Act
            var result = converter.Convert(pattern, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            result.Should().Be(expectedEnglish);
        }
        finally
        {
            loc.SetLanguage(AppLanguage.Russian);
        }
    }

    [Fact]
    public void DitherPatternLocalizationConverter_HandlesStringAndFallbackInputs()
    {
        // Arrange
        var converter = DitherPatternLocalizationConverter.Instance;
        var loc = LocalizationService.Instance;

        try
        {
            loc.SetLanguage(AppLanguage.English);

            // String parsing
            var strResult = converter.Convert("Bayer4x4", typeof(string), null, CultureInfo.InvariantCulture);
            strResult.Should().Be("Bayer 4×4");

            // Integer enum parsing
            var intEnumResult = converter.Convert((int)DitherPattern.Bayer2x2, typeof(string), null, CultureInfo.InvariantCulture);
            intEnumResult.Should().Be("Bayer 2×2");

            // Null input
            var nullResult = converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);
            nullResult.Should().Be(string.Empty);

            // Arbitrary object
            var intResult = converter.Convert(12345, typeof(string), null, CultureInfo.InvariantCulture);
            intResult.Should().Be("12345");
        }
        finally
        {
            loc.SetLanguage(AppLanguage.Russian);
        }
    }

    [Fact]
    public void DitherPatternLocalizationConverter_ConvertBack_ThrowsNotSupportedException()
    {
        // Arrange
        var converter = DitherPatternLocalizationConverter.Instance;

        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack("No Dithering", typeof(DitherPattern), null, CultureInfo.InvariantCulture));
    }

    #endregion
}

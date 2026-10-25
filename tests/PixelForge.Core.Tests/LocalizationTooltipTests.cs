using FluentAssertions;
using PixelForge.Core.Localization;
using Xunit;

namespace PixelForge.Core.Tests;

/// <summary>
/// Comprehensive verification suite ensuring all tooltips across tools, toolbar,
/// panels, options, and status bar exist with high-quality descriptions in both Russian and English.
/// </summary>
[Collection("LocalizationSharedState")]
public class LocalizationTooltipTests
{
    private readonly LocalizationService _service = LocalizationService.Instance;

    [Theory]
    // Tool Options (Context Bar)
    [InlineData("Tip_Stabilizer")]
    [InlineData("Tip_PixelPerfect")]
    [InlineData("Tip_Dithering")]
    [InlineData("Tip_BrushSize")]
    [InlineData("Context_BrushTip1")]
    [InlineData("Context_BrushTip2")]
    [InlineData("Context_BrushTip3")]
    [InlineData("Context_BrushTip4")]
    [InlineData("Context_BrushTip8")]
    [InlineData("Tip_SprayRadius")]
    [InlineData("Tip_SprayDensity")]
    [InlineData("Tip_Pattern8x8")]
    [InlineData("Tip_FillTolerance")]
    [InlineData("Tip_FillContiguous")]
    [InlineData("Tip_GradientType")]
    [InlineData("Tip_GradientPreset")]
    [InlineData("Tip_ColorRampBtn")]
    [InlineData("Tip_ShapeFilled")]
    [InlineData("Tip_ActiveTool")]
    // All 14 Left Rail Tools
    [InlineData("Tool_PencilTip")]
    [InlineData("Tip_ToolPencil")]
    [InlineData("Tool_EraserTip")]
    [InlineData("Tip_ToolEraser")]
    [InlineData("Tool_DitherBrushTip")]
    [InlineData("Tip_ToolDitherBrush")]
    [InlineData("Tool_PatternBrushTip")]
    [InlineData("Tip_ToolPatternBrush")]
    [InlineData("Tool_ShadeBrushTip")]
    [InlineData("Tip_ToolShadeBrush")]
    [InlineData("Tool_SprayBrushTip")]
    [InlineData("Tip_ToolSprayBrush")]
    [InlineData("Tool_FillTip")]
    [InlineData("Tip_ToolFill")]
    [InlineData("Tool_MagicWandTip")]
    [InlineData("Tip_ToolMagicWand")]
    [InlineData("Tool_GradientTip")]
    [InlineData("Tip_ToolGradient")]
    [InlineData("Tool_EyedropperTip")]
    [InlineData("Tip_ToolEyedropper")]
    [InlineData("Tool_LineTip")]
    [InlineData("Tip_ToolLine")]
    [InlineData("Tool_RectangleTip")]
    [InlineData("Tip_ToolRectangle")]
    [InlineData("Tool_EllipseTip")]
    [InlineData("Tip_ToolEllipse")]
    [InlineData("Tool_SelectionTip")]
    [InlineData("Tip_ToolSelection")]
    // Top Action Bar Buttons
    [InlineData("Tip_NewCanvas")]
    [InlineData("Tip_ResizeCanvas")]
    [InlineData("Tip_OpenProject")]
    [InlineData("Tip_SaveProject")]
    [InlineData("Tip_QuickExport")]
    [InlineData("Tip_ClearCanvas")]
    [InlineData("Tip_Undo")]
    [InlineData("Tip_Redo")]
    [InlineData("Tip_FlipH")]
    [InlineData("Tip_FlipV")]
    [InlineData("Tip_RotateCW")]
    [InlineData("Tip_SmartTools")]
    [InlineData("Tip_Grid")]
    [InlineData("Tip_TileGrid")]
    [InlineData("Tip_SymX")]
    [InlineData("Tip_SymY")]
    [InlineData("Tip_Tiled")]
    [InlineData("Tip_ZoomOut")]
    [InlineData("Tip_ZoomIn")]
    [InlineData("Tip_ZoomFit")]
    [InlineData("Tip_ToggleTheme")]
    [InlineData("Tip_ToggleLanguage")]
    // Right Panels: Live Preview
    [InlineData("Tip_Preview1x")]
    [InlineData("Tip_Preview2x")]
    [InlineData("Tip_Preview4x")]
    [InlineData("Tip_PreviewToggle")]
    [InlineData("Tip_PreviewDimensions")]
    // Right Panels: Color Picker & Palette
    [InlineData("Tip_ColorPrimary")]
    [InlineData("Tip_ColorSecondary")]
    [InlineData("Tip_ColorSwap")]
    [InlineData("Tip_ColorAdd")]
    [InlineData("Tip_ColorPresets")]
    [InlineData("Tip_TabWheel")]
    [InlineData("Tip_TabPalette")]
    [InlineData("Tip_TabSliders")]
    [InlineData("Tip_ColorWheel")]
    [InlineData("Tip_SliderRed")]
    [InlineData("Tip_SliderGreen")]
    [InlineData("Tip_SliderBlue")]
    [InlineData("Tip_SliderAlpha")]
    [InlineData("Tip_ColorHex")]
    // Right Panels: Reference Image
    [InlineData("Tip_RefLoad")]
    [InlineData("Tip_RefClear")]
    [InlineData("Tip_RefOpacity")]
    [InlineData("Tip_RefVisibility")]
    // Right Panels: Layers
    [InlineData("Tip_LayerAdd")]
    [InlineData("Tip_LayerDuplicate")]
    [InlineData("Tip_LayerMoveUp")]
    [InlineData("Tip_LayerMoveDown")]
    [InlineData("Tip_LayerMergeDown")]
    [InlineData("Tip_LayerDelete")]
    [InlineData("Tip_LayerOpacity")]
    [InlineData("Tip_LayerVisibility")]
    [InlineData("Tip_LayerAlphaLock")]
    [InlineData("Tip_LayerRename")]
    // Bottom Status Bar
    [InlineData("Tip_StatusReady")]
    [InlineData("Tip_StatusDimensions")]
    [InlineData("Tip_StatusTileGrid")]
    [InlineData("Tip_StatusTileSize")]
    [InlineData("Tip_StatusZoom")]
    [InlineData("Tip_StatusLanguage")]
    [InlineData("Tip_StatusSelection")]
    [InlineData("Tip_StatusTiled")]
    [InlineData("Tip_StatusSymmetry")]
    [InlineData("Tip_StatusPixelPerfect")]
    [InlineData("Tip_StatusAlphaLock")]
    [InlineData("Tip_StatusCoords")]
    [InlineData("Tip_StatusHints")]
    public void TooltipKey_MustExistInRussianAndEnglish_WithRichDescription(string key)
    {
        // 1. Russian Dictionary check
        _service.SetLanguage(AppLanguage.Russian);
        var ruText = _service.Get(key);

        ruText.Should().NotBeNullOrWhiteSpace($"Tooltip key '{key}' should have a non-empty Russian translation");
        ruText.Should().NotBe(key, $"Tooltip key '{key}' should not fallback to key name in Russian dictionary");
        ruText.Length.Should().BeGreaterThan(5, $"Tooltip '{key}' in Russian should be detailed and informative");

        // 2. English Dictionary check
        _service.SetLanguage(AppLanguage.English);
        var enText = _service.Get(key);

        enText.Should().NotBeNullOrWhiteSpace($"Tooltip key '{key}' should have a non-empty English translation");
        enText.Should().NotBe(key, $"Tooltip key '{key}' should not fallback to key name in English dictionary");
        enText.Length.Should().BeGreaterThan(5, $"Tooltip '{key}' in English should be detailed and informative");

        // Restore default
        _service.SetLanguage(AppLanguage.Russian);
    }

    [Fact]
    public void StabilizerTooltip_MustExplainHandSmoothingAndLazyMouse()
    {
        _service.SetLanguage(AppLanguage.Russian);
        var ru = _service.Get("Tip_Stabilizer");
        ru.Should().Contain("Стабилизатор");
        ru.Should().Contain("LazyMouse");

        _service.SetLanguage(AppLanguage.English);
        var en = _service.Get("Tip_Stabilizer");
        en.Should().Contain("Stabilizer");
        en.Should().Contain("LazyMouse");

        _service.SetLanguage(AppLanguage.Russian);
    }

    [Fact]
    public void PixelPerfectTooltip_MustExplainDuplicatePixelRemoval()
    {
        _service.SetLanguage(AppLanguage.Russian);
        var ru = _service.Get("Tip_PixelPerfect");
        ru.Should().Contain("Pixel-Perfect");
        ru.Should().Contain("угловые");

        _service.SetLanguage(AppLanguage.English);
        var en = _service.Get("Tip_PixelPerfect");
        en.Should().Contain("Pixel-Perfect");
        en.Should().Contain("staircase");

        _service.SetLanguage(AppLanguage.Russian);
    }

    [Fact]
    public void DitheringTooltip_MustExplainOpticalBlendingAndBayerMatrix()
    {
        _service.SetLanguage(AppLanguage.Russian);
        var ru = _service.Get("Tip_Dithering");
        ru.Should().Contain("дизеринг");
        ru.Should().Contain("смешивание");

        _service.SetLanguage(AppLanguage.English);
        var en = _service.Get("Tip_Dithering");
        en.Should().Contain("Dithering");
        en.Should().Contain("Bayer");

        _service.SetLanguage(AppLanguage.Russian);
    }
}

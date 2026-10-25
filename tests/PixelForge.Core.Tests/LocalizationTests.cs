using FluentAssertions;
using PixelForge.Core.Localization;
using Xunit;

namespace PixelForge.Core.Tests;

[Collection("LocalizationSharedState")]
public class LocalizationTests
{
    [Fact]
    public void DefaultLanguage_ShouldBeRussian()
    {
        var service = LocalizationService.Instance;
        service.CurrentLanguage.Should().Be(AppLanguage.Russian);
        service.IsRussian.Should().BeTrue();
        service.IsEnglish.Should().BeFalse();
    }

    [Fact]
    public void GetString_RussianAndEnglish_ShouldReturnValidTranslations()
    {
        var service = LocalizationService.Instance;

        service.SetLanguage(AppLanguage.Russian);
        service.Get("Menu_File").Should().Be("Файл");
        service.Get("Tool_Pencil").Should().Be("Карандаш");
        service["Menu_Canvas"].Should().Be("Холст");

        service.SetLanguage(AppLanguage.English);
        service.Get("Menu_File").Should().Be("File");
        service.Get("Tool_Pencil").Should().Be("Pencil");
        service["Menu_Canvas"].Should().Be("Canvas");

        // Restore
        service.SetLanguage(AppLanguage.Russian);
    }

    [Fact]
    public void ToggleLanguage_ShouldSwitchBetweenRussianAndEnglish()
    {
        var service = LocalizationService.Instance;
        service.SetLanguage(AppLanguage.Russian);

        service.ToggleLanguage();
        service.CurrentLanguage.Should().Be(AppLanguage.English);

        service.ToggleLanguage();
        service.CurrentLanguage.Should().Be(AppLanguage.Russian);
    }

    [Fact]
    public void Format_ShouldInterpolateValuesProperly()
    {
        var service = LocalizationService.Instance;

        service.SetLanguage(AppLanguage.Russian);
        var formattedRu = service.Format("Msg_LoadedProject", "my_pixel_art.pxproj");
        formattedRu.Should().Contain("my_pixel_art.pxproj");

        service.SetLanguage(AppLanguage.English);
        var formattedEn = service.Format("Msg_LoadedProject", "my_pixel_art.pxproj");
        formattedEn.Should().Be("Loaded project my_pixel_art.pxproj");

        // Restore
        service.SetLanguage(AppLanguage.Russian);
    }

    [Fact]
    public void MissingKey_ShouldReturnKeyNameAsFallback()
    {
        var service = LocalizationService.Instance;
        var unknown = service.Get("UNKNOWN_NONEXISTENT_KEY_XYZ");
        unknown.Should().Be("UNKNOWN_NONEXISTENT_KEY_XYZ");
    }

    [Theory]
    [InlineData("Preview_Scale", "Масштаб:", "Scale:")]
    [InlineData("Panel_TogglePreview", "Свернуть / развернуть предпросмотр", "Collapse / expand preview")]
    [InlineData("Splash_TelemetryRam", "ОЗУ пул 1024 МБ", "RAM Pool 1024 MB")]
    [InlineData("Splash_TelemetryJit", "Pre-JIT компиляция", "Pre-JIT Engine")]
    [InlineData("Splash_TelemetrySkia", "SkiaSharp 60+ FPS", "SkiaSharp 60+ FPS")]
    [InlineData("Splash_TelemetryUndo", "Zero-Alloc дельта", "Zero-Alloc Delta")]
    [InlineData("Splash_TelemetryOnline", "В СЕТИ", "ONLINE")]
    [InlineData("Splash_SystemTag", "x64 • .NET 8 • Skia 2.88", "x64 • .NET 8 • Skia 2.88")]
    public void NewUIAndSplashKeys_ShouldExistInRussianAndEnglish(string key, string expectedRu, string expectedEn)
    {
        var service = LocalizationService.Instance;

        service.SetLanguage(AppLanguage.Russian);
        service.Get(key).Should().Be(expectedRu);

        service.SetLanguage(AppLanguage.English);
        service.Get(key).Should().Be(expectedEn);

        // Restore
        service.SetLanguage(AppLanguage.Russian);
    }
}

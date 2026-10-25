using System.Text.Json;
using FluentAssertions;
using PixelForge.Core.Localization;
using PixelForge.Core.Services;
using Xunit;

namespace PixelForge.Core.Tests;

public class AppSettingsExtendedTests
{
    [Fact]
    public void AppSettings_DefaultValues_ShouldMatchSpecification()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_ext_{Guid.NewGuid():N}.json");
        try
        {
            var service = new AppSettingsService(tempFile);

            // General
            service.Language.Should().Be(AppLanguage.Russian);
            service.IsDarkTheme.Should().BeTrue();
            service.CheckUpdatesOnStartup.Should().BeTrue();

            // Canvas & Grid
            service.DefaultCanvasWidth.Should().Be(32);
            service.DefaultCanvasHeight.Should().Be(32);
            service.ShowPixelGrid.Should().BeTrue();
            service.PixelGridMinZoom.Should().Be(4.0);
            service.TileGridWidth.Should().Be(16);
            service.TileGridHeight.Should().Be(16);
            service.CheckerSize.Should().Be(16);
            service.ColorCheckerLight.Should().Be("#2A2E40");
            service.ColorCheckerDark.Should().Be("#1F2332");

            // Tools
            service.DefaultStabilizerStrength.Should().Be(0);
            service.DefaultPixelPerfect.Should().BeFalse();
            service.DefaultBrushSize.Should().Be(1);

            // Autosave & History
            service.AutosaveEnabled.Should().BeTrue();
            service.AutosaveIntervalMinutes.Should().Be(5);
            service.MaxUndoSteps.Should().Be(100);

            // Performance
            service.PreloadMemory.Should().BeTrue();
            service.HardwareAcceleration.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void AppSettings_CustomValues_ShouldPersistAcrossReload()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_ext_{Guid.NewGuid():N}.json");
        try
        {
            var service = new AppSettingsService(tempFile);

            // Mutate all extended settings
            service.Language = AppLanguage.English;
            service.IsDarkTheme = false;
            service.CheckUpdatesOnStartup = false;

            service.DefaultCanvasWidth = 64;
            service.DefaultCanvasHeight = 48;
            service.ShowPixelGrid = false;
            service.PixelGridMinZoom = 8.0;
            service.TileGridWidth = 32;
            service.TileGridHeight = 24;
            service.CheckerSize = 32;
            service.ColorCheckerLight = "#FFFFFF";
            service.ColorCheckerDark = "#E2E8F0";

            service.DefaultStabilizerStrength = 5;
            service.DefaultPixelPerfect = true;
            service.DefaultBrushSize = 3;

            service.AutosaveEnabled = false;
            service.AutosaveIntervalMinutes = 10;
            service.MaxUndoSteps = 200;

            service.PreloadMemory = false;
            service.HardwareAcceleration = false;

            // Reload from disk
            var reloaded = new AppSettingsService(tempFile);

            reloaded.Language.Should().Be(AppLanguage.English);
            reloaded.IsDarkTheme.Should().BeFalse();
            reloaded.CheckUpdatesOnStartup.Should().BeFalse();

            reloaded.DefaultCanvasWidth.Should().Be(64);
            reloaded.DefaultCanvasHeight.Should().Be(48);
            reloaded.ShowPixelGrid.Should().BeFalse();
            reloaded.PixelGridMinZoom.Should().Be(8.0);
            reloaded.TileGridWidth.Should().Be(32);
            reloaded.TileGridHeight.Should().Be(24);
            reloaded.CheckerSize.Should().Be(32);
            reloaded.ColorCheckerLight.Should().Be("#FFFFFF");
            reloaded.ColorCheckerDark.Should().Be("#E2E8F0");

            reloaded.DefaultStabilizerStrength.Should().Be(5);
            reloaded.DefaultPixelPerfect.Should().BeTrue();
            reloaded.DefaultBrushSize.Should().Be(3);

            reloaded.AutosaveEnabled.Should().BeFalse();
            reloaded.AutosaveIntervalMinutes.Should().Be(10);
            reloaded.MaxUndoSteps.Should().Be(200);

            reloaded.PreloadMemory.Should().BeFalse();
            reloaded.HardwareAcceleration.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void AppSettings_ApplySettings_ShouldUpdateAndFireEvent()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_ext_{Guid.NewGuid():N}.json");
        try
        {
            var service = new AppSettingsService(tempFile);
            bool eventFired = false;
            service.SettingsChanged += (_, _) => eventFired = true;

            var snapshot = service.Data.Clone();
            snapshot.DefaultCanvasWidth = 128;
            snapshot.DefaultBrushSize = 7;
            snapshot.MaxUndoSteps = 250;

            service.ApplySettings(snapshot);

            eventFired.Should().BeTrue();
            service.DefaultCanvasWidth.Should().Be(128);
            service.DefaultBrushSize.Should().Be(7);
            service.MaxUndoSteps.Should().Be(250);

            var reloaded = new AppSettingsService(tempFile);
            reloaded.DefaultCanvasWidth.Should().Be(128);
            reloaded.DefaultBrushSize.Should().Be(7);
            reloaded.MaxUndoSteps.Should().Be(250);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void AppSettings_ResetToDefaults_ShouldRestoreFactoryDefaults()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_ext_{Guid.NewGuid():N}.json");
        try
        {
            var service = new AppSettingsService(tempFile);
            service.AddRecentProject("C:\\dummy.pxproj");
            service.DefaultCanvasWidth = 256;
            service.MaxUndoSteps = 300;
            service.IsDarkTheme = false;

            service.ResetToDefaults(preserveRecentProjects: true);

            service.DefaultCanvasWidth.Should().Be(32);
            service.MaxUndoSteps.Should().Be(100);
            service.IsDarkTheme.Should().BeTrue();
            service.RecentProjects.Should().Contain("C:\\dummy.pxproj");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void AppSettings_CorruptedJson_ShouldFallbackGracefully()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_ext_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempFile, "{ this is not valid json! }");

            var service = new AppSettingsService(tempFile);
            service.Language.Should().Be(AppLanguage.Russian);
            service.DefaultCanvasWidth.Should().Be(32);
            service.MaxUndoSteps.Should().Be(100);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void AppSettings_OutOfBoundsValues_ShouldBeSanitized()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_ext_{Guid.NewGuid():N}.json");
        try
        {
            var rawJson = """
            {
                "defaultCanvasWidth": -10,
                "defaultCanvasHeight": 0,
                "tileGridWidth": -5,
                "checkerSize": -2,
                "pixelGridMinZoom": 0.2,
                "defaultBrushSize": -1,
                "autosaveIntervalMinutes": 0,
                "maxUndoSteps": -100,
                "colorCheckerLight": "",
                "colorCheckerDark": "   "
            }
            """;
            File.WriteAllText(tempFile, rawJson);

            var service = new AppSettingsService(tempFile);
            service.DefaultCanvasWidth.Should().Be(32);
            service.DefaultCanvasHeight.Should().Be(32);
            service.TileGridWidth.Should().Be(16);
            service.CheckerSize.Should().Be(16);
            service.PixelGridMinZoom.Should().Be(4.0);
            service.DefaultBrushSize.Should().Be(1);
            service.AutosaveIntervalMinutes.Should().Be(5);
            service.MaxUndoSteps.Should().Be(100);
            service.ColorCheckerLight.Should().Be("#2A2E40");
            service.ColorCheckerDark.Should().Be("#1F2332");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void AppSettingsData_Clone_CreatesIndependentDeepCopy()
    {
        var original = new AppSettingsData
        {
            DefaultCanvasWidth = 64,
            RecentProjects = ["C:\\Test.pxproj"]
        };

        var clone = original.Clone();
        clone.DefaultCanvasWidth = 128;
        clone.RecentProjects.Add("C:\\Another.pxproj");

        original.DefaultCanvasWidth.Should().Be(64);
        original.RecentProjects.Should().HaveCount(1);
        clone.DefaultCanvasWidth.Should().Be(128);
        clone.RecentProjects.Should().HaveCount(2);
    }
}

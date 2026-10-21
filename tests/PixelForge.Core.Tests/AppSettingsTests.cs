using FluentAssertions;
using PixelForge.Core.Localization;
using PixelForge.Core.Services;
using Xunit;

namespace PixelForge.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void AppSettings_DefaultValues_ShouldBeValid()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_test_{Guid.NewGuid():N}.json");
        try
        {
            var service = new AppSettingsService(tempFile);
            service.Language.Should().Be(AppLanguage.Russian);
            service.IsDarkTheme.Should().BeTrue();
            service.RecentProjects.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void AddRecentProject_ShouldStoreAndPruneDuplicates()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_test_{Guid.NewGuid():N}.json");
        try
        {
            var service = new AppSettingsService(tempFile);
            service.AddRecentProject("C:\\Projects\\Art1.pxproj");
            service.AddRecentProject("C:\\Projects\\Art2.pxproj");
            service.AddRecentProject("C:\\Projects\\Art1.pxproj"); // Duplicate -> should move to top

            service.RecentProjects.Count.Should().Be(2);
            service.RecentProjects[0].Should().Be("C:\\Projects\\Art1.pxproj");
            service.RecentProjects[1].Should().Be("C:\\Projects\\Art2.pxproj");

            // Verify persistence by reloading from disk
            var reloaded = new AppSettingsService(tempFile);
            reloaded.RecentProjects.Count.Should().Be(2);
            reloaded.RecentProjects[0].Should().Be("C:\\Projects\\Art1.pxproj");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void LanguageAndThemeChange_ShouldPersistToDisk()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_test_{Guid.NewGuid():N}.json");
        try
        {
            var service = new AppSettingsService(tempFile);
            service.Language = AppLanguage.English;
            service.IsDarkTheme = false;

            var reloaded = new AppSettingsService(tempFile);
            reloaded.Language.Should().Be(AppLanguage.English);
            reloaded.IsDarkTheme.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

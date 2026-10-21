using FluentAssertions;
using Xunit;

namespace PixelForge.Core.Tests;

public class AppVersionInfoTests
{
    [Fact]
    public void CurrentVersion_ShouldNotBeNullOrEmpty()
    {
        // Act
        var version = AppVersionInfo.CurrentVersion;

        // Assert
        version.Should().NotBeNullOrWhiteSpace();
        version.Should().MatchRegex(@"^\d+\.\d+\.\d+");
    }

    [Fact]
    public void FullVersionString_ShouldStartWithV()
    {
        // Act
        var fullVersion = AppVersionInfo.FullVersionString;

        // Assert
        fullVersion.Should().StartWith("v");
        fullVersion.Substring(1).Should().Be(AppVersionInfo.CurrentVersion);
    }
}

using FluentAssertions;
using PixelForge.App.Services;
using Xunit;

namespace PixelForge.App.Tests;

public class FileAssociationServiceTests
{
    [Fact]
    public void IsWindows_ShouldMatchOperatingSystem()
    {
        FileAssociationService.IsWindows.Should().Be(OperatingSystem.IsWindows());
    }

    [Fact]
    public void RegisterAssociation_OnWindows_ShouldExecuteSafely()
    {
        if (OperatingSystem.IsWindows())
        {
            var act = () => FileAssociationService.RegisterAssociation();
            act.Should().NotThrow();
        }
    }
}

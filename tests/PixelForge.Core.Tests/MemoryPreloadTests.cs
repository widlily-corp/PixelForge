using FluentAssertions;
using PixelForge.Core.Models;
using PixelForge.Core.Services;
using Xunit;

namespace PixelForge.Core.Tests;

public class MemoryPreloadTests
{
    [Fact]
    public async Task PreloadAsync_CompletesSuccessfully_AndSetsIsPreloadedTrue()
    {
        // Arrange
        var service = new MemoryPreloadService();
        var reported = new List<PreloadProgress>();
        var progress = new Progress<PreloadProgress>(reported.Add);

        // Act
        await service.PreloadAsync(progress);

        // Assert
        service.IsPreloaded.Should().BeTrue();
    }

    [Fact]
    public async Task PreloadAsync_WhenAlreadyPreloaded_CompletesImmediately()
    {
        // Arrange
        var service = new MemoryPreloadService();
        await service.PreloadAsync();
        service.IsPreloaded.Should().BeTrue();

        var reported = new List<PreloadProgress>();
        var progress = new Progress<PreloadProgress>(reported.Add);

        // Act
        await service.PreloadAsync(progress);

        // Assert
        service.IsPreloaded.Should().BeTrue();
    }

    [Fact]
    public void PreJitType_WithValidType_ExecutesWithoutException()
    {
        // Arrange & Act
        Action act = () => MemoryPreloadService.PreJitType(typeof(PixelCanvas));

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task PreloadAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var service = new MemoryPreloadService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await service.PreloadAsync(null, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

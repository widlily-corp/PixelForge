using System.Net;
using FluentAssertions;
using PixelForge.App.Services;
using Xunit;

namespace PixelForge.App.Tests;

public class UpdateServiceTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenRemoteVersionNewer_ReturnsUpdateAvailable()
    {
        // Arrange
        var jsonManifest = """
        {
            "version": "9.9.9",
            "releaseNotes": "Major update",
            "downloadUrl": "https://example.com/PixelForge.exe"
        }
        """;

        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonManifest, System.Text.Encoding.UTF8, "application/json")
        });

        var client = new HttpClient(handler);
        var service = new UpdateService(client, "https://example.com/latest.json");

        // Act
        var result = await service.CheckForUpdatesAsync(silent: false);

        // Assert
        result.Success.Should().BeTrue();
        result.IsUpdateAvailable.Should().BeTrue();
        result.LatestVersion.Should().Be("9.9.9");
        result.DownloadUrl.Should().Be("https://example.com/PixelForge.exe");
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenRemoteVersionSame_ReturnsNoUpdate()
    {
        // Arrange
        var jsonManifest = """
        {
            "version": "0.1.1",
            "releaseNotes": "Current version",
            "downloadUrl": "https://example.com/PixelForge.exe"
        }
        """;

        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonManifest, System.Text.Encoding.UTF8, "application/json")
        });

        var client = new HttpClient(handler);
        var service = new UpdateService(client, "https://example.com/latest.json");

        // Act
        var result = await service.CheckForUpdatesAsync(silent: false);

        // Assert
        result.Success.Should().BeTrue();
        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadAndInstallUpdateAsync_WithEmptyUrl_ReturnsFalse()
    {
        // Arrange
        var service = new UpdateService();

        // Act
        var success = await service.DownloadAndInstallUpdateAsync(string.Empty);

        // Assert
        success.Should().BeFalse();
    }
}

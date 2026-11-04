using PixelForge.Core.Models;

namespace PixelForge.Core.Services;

/// <summary>
/// Provides image export operations for canvas and pixel data to PNG and BMP formats.
/// </summary>
public interface IExportService
{
    Task ExportPngAsync(PixelCanvas canvas, string filePath, int scale = 1, CancellationToken cancellationToken = default);
    Task ExportPngAsync(PixelCanvas canvas, Stream destinationStream, int scale = 1, CancellationToken cancellationToken = default);
    Task ExportPngAsync(PixelColor[] pixels, int width, int height, string filePath, int scale = 1, CancellationToken cancellationToken = default);
    Task ExportPngAsync(PixelColor[] pixels, int width, int height, Stream destinationStream, int scale = 1, CancellationToken cancellationToken = default);

    Task ExportBmpAsync(PixelCanvas canvas, string filePath, int scale = 1, CancellationToken cancellationToken = default);
    Task ExportBmpAsync(PixelCanvas canvas, Stream destinationStream, int scale = 1, CancellationToken cancellationToken = default);
    Task ExportBmpAsync(PixelColor[] pixels, int width, int height, string filePath, int scale = 1, CancellationToken cancellationToken = default);
    Task ExportBmpAsync(PixelColor[] pixels, int width, int height, Stream destinationStream, int scale = 1, CancellationToken cancellationToken = default);

    Task ExportGifAsync(PixelCanvas canvas, string filePath, int scale = 1, CancellationToken cancellationToken = default);
    Task ExportGifAsync(PixelCanvas canvas, Stream destinationStream, int scale = 1, CancellationToken cancellationToken = default);

    Task ExportSpritesheetAsync(PixelCanvas canvas, string imagePath, string? jsonPath, SpritesheetSettings settings, CancellationToken cancellationToken = default);

    Task ExportPngSequenceAsync(PixelCanvas canvas, string outputDirectory, string prefix, int scale = 1, CancellationToken cancellationToken = default);
}

using System.Runtime.InteropServices;
using System.Text.Json;
using PixelForge.Core.Models;
using SkiaSharp;

namespace PixelForge.Core.Services;

/// <summary>
/// Implements export of canvas and pixel buffers to PNG and lossless BMP formats using SkiaSharp.
/// </summary>
public class ExportService : IExportService
{
    public Task ExportPngAsync(PixelCanvas canvas, string filePath, int scale = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var composite = canvas.GetCompositePixels();
        return ExportPngAsync(composite, canvas.Width, canvas.Height, filePath, scale, cancellationToken);
    }

    public Task ExportPngAsync(PixelCanvas canvas, Stream destinationStream, int scale = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var composite = canvas.GetCompositePixels();
        return ExportPngAsync(composite, canvas.Width, canvas.Height, destinationStream, scale, cancellationToken);
    }

    public async Task ExportPngAsync(PixelColor[] pixels, int width, int height, string filePath, int scale = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await ExportPngAsync(pixels, width, height, fs, scale, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportPngAsync(PixelColor[] pixels, int width, int height, Stream destinationStream, int scale = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(destinationStream);
        ValidateDimensions(width, height, scale, pixels.Length);

        var (scaledPixels, scaledWidth, scaledHeight) = ApplyNearestNeighborScale(pixels, width, height, scale);

        using var encoded = EncodeToPngData(scaledPixels, scaledWidth, scaledHeight);
        await using var skStream = encoded.AsStream();
        await skStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);
        await destinationStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task ExportBmpAsync(PixelCanvas canvas, string filePath, int scale = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var composite = canvas.GetCompositePixels();
        return ExportBmpAsync(composite, canvas.Width, canvas.Height, filePath, scale, cancellationToken);
    }

    public Task ExportBmpAsync(PixelCanvas canvas, Stream destinationStream, int scale = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var composite = canvas.GetCompositePixels();
        return ExportBmpAsync(composite, canvas.Width, canvas.Height, destinationStream, scale, cancellationToken);
    }

    public async Task ExportBmpAsync(PixelColor[] pixels, int width, int height, string filePath, int scale = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await ExportBmpAsync(pixels, width, height, fs, scale, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportBmpAsync(PixelColor[] pixels, int width, int height, Stream destinationStream, int scale = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(destinationStream);
        ValidateDimensions(width, height, scale, pixels.Length);

        var (scaledPixels, scaledWidth, scaledHeight) = ApplyNearestNeighborScale(pixels, width, height, scale);

        using var encoded = TryEncodeToBmpData(scaledPixels, scaledWidth, scaledHeight);
        if (encoded != null)
        {
            await using var skStream = encoded.AsStream();
            await skStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Fallback: standard lossless 32-bit BGRA BMP writer ensuring 100% color fidelity and compatibility
            await WriteLosslessBmpAsync(destinationStream, scaledPixels, scaledWidth, scaledHeight, cancellationToken).ConfigureAwait(false);
        }

        await destinationStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportGifAsync(PixelCanvas canvas, string filePath, int scale = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await ExportGifAsync(canvas, fs, scale, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportGifAsync(PixelCanvas canvas, Stream destinationStream, int scale = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(destinationStream);
        if (scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be at least 1.");
        }
        if (canvas.Frames.Count == 0)
        {
            throw new InvalidOperationException("Canvas has no frames to export.");
        }

        var frames = new List<(PixelColor[] pixels, int width, int height, int durationMs)>(canvas.Frames.Count);
        for (int i = 0; i < canvas.Frames.Count; i++)
        {
            var pixels = canvas.GetFrameCompositePixels(i);
            frames.Add((pixels, canvas.Width, canvas.Height, canvas.Frames[i].DurationMs));
        }

        await GifEncoder.EncodeAsync(frames, destinationStream, scale, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportSpritesheetAsync(
        PixelCanvas canvas,
        string imagePath,
        string? jsonPath,
        SpritesheetSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path cannot be null or empty.", nameof(imagePath));
        }
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.Scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings.Scale), "Scale must be at least 1.");
        }
        if (settings.Padding < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings.Padding), "Padding cannot be negative.");
        }
        if (settings.Border < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings.Border), "Border cannot be negative.");
        }
        if (canvas.Frames.Count == 0)
        {
            throw new InvalidOperationException("Canvas has no frames to export.");
        }

        int frameCount = canvas.Frames.Count;
        int scale = settings.Scale;
        int frameWidth = canvas.Width * scale;
        int frameHeight = canvas.Height * scale;
        int padding = settings.Padding;
        int border = settings.Border;

        int columns;
        int rows;

        switch (settings.LayoutMode)
        {
            case SpritesheetLayoutMode.HorizontalStrip:
                columns = frameCount;
                rows = 1;
                break;
            case SpritesheetLayoutMode.VerticalStrip:
                columns = 1;
                rows = frameCount;
                break;
            case SpritesheetLayoutMode.Grid:
            default:
                if (settings.Columns > 0)
                {
                    columns = settings.Columns;
                }
                else
                {
                    columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(frameCount)));
                }
                rows = Math.Max(1, (int)Math.Ceiling((double)frameCount / columns));
                break;
        }

        int atlasWidth = (2 * border) + (columns * frameWidth) + ((columns - 1) * padding);
        int atlasHeight = (2 * border) + (rows * frameHeight) + ((rows - 1) * padding);

        var atlasPixels = new PixelColor[atlasWidth * atlasHeight];

        var frameList = new List<object>(frameCount);
        string prefix = Path.GetFileNameWithoutExtension(imagePath);
        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = "frame";
        }

        for (int i = 0; i < frameCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int col = i % columns;
            int row = i / columns;
            int x = border + col * (frameWidth + padding);
            int y = border + row * (frameHeight + padding);

            var framePixels = canvas.GetFrameCompositePixels(i);

            for (int dy = 0; dy < frameHeight; dy++)
            {
                int srcY = dy / scale;
                int srcRowOffset = srcY * canvas.Width;
                int destRowOffset = (y + dy) * atlasWidth;

                for (int dx = 0; dx < frameWidth; dx++)
                {
                    int srcX = dx / scale;
                    atlasPixels[destRowOffset + (x + dx)] = framePixels[srcRowOffset + srcX];
                }
            }

            frameList.Add(new
            {
                filename = $"{prefix}_{i}",
                frame = new { x = x, y = y, w = frameWidth, h = frameHeight },
                rotated = false,
                trimmed = false,
                spriteSourceSize = new { x = 0, y = 0, w = frameWidth, h = frameHeight },
                sourceSize = new { w = frameWidth, h = frameHeight },
                duration = canvas.Frames[i].DurationMs
            });
        }

        // Save atlas image PNG
        await ExportPngAsync(atlasPixels, atlasWidth, atlasHeight, imagePath, scale: 1, cancellationToken).ConfigureAwait(false);

        // Optionally save Aseprite-compatible JSON metadata
        if (!string.IsNullOrWhiteSpace(jsonPath))
        {
            var tagList = new List<object>(canvas.Tags.Count);
            foreach (var tag in canvas.Tags)
            {
                string direction = tag.LoopMode switch
                {
                    AnimationLoopMode.Reverse => "reverse",
                    AnimationLoopMode.PingPong => "pingpong",
                    _ => "forward"
                };

                tagList.Add(new
                {
                    name = tag.Name,
                    from = tag.FromFrame,
                    to = tag.ToFrame,
                    direction = direction
                });
            }

            var metadata = new
            {
                frames = frameList,
                meta = new
                {
                    app = "PixelForge",
                    version = AppVersionInfo.CurrentVersion,
                    image = Path.GetFileName(imagePath),
                    format = "RGBA8888",
                    size = new { w = atlasWidth, h = atlasHeight },
                    scale = scale.ToString(),
                    frameTags = tagList
                }
            };

            var jsonDir = Path.GetDirectoryName(jsonPath);
            if (!string.IsNullOrEmpty(jsonDir) && !Directory.Exists(jsonDir))
            {
                Directory.CreateDirectory(jsonDir);
            }

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var tmpJsonPath = jsonPath + ".tmp";
            await using (var fs = new FileStream(tmpJsonPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(fs, metadata, jsonOptions, cancellationToken).ConfigureAwait(false);
            }
            File.Move(tmpJsonPath, jsonPath, overwrite: true);
        }
    }

    public async Task ExportPngSequenceAsync(
        PixelCanvas canvas,
        string outputDirectory,
        string prefix,
        int scale = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory cannot be null or empty.", nameof(outputDirectory));
        }
        if (scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be at least 1.");
        }
        if (canvas.Frames.Count == 0)
        {
            throw new InvalidOperationException("Canvas has no frames to export.");
        }

        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = "frame";
        }

        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        for (int i = 0; i < canvas.Frames.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var framePixels = canvas.GetFrameCompositePixels(i);
            string fileName = $"{prefix}_{i:D3}.png";
            string filePath = Path.Combine(outputDirectory, fileName);
            await ExportPngAsync(framePixels, canvas.Width, canvas.Height, filePath, scale, cancellationToken).ConfigureAwait(false);
        }
    }

    private static SKData EncodeToPngData(PixelColor[] scaledPixels, int scaledWidth, int scaledHeight)
    {
        var info = new SKImageInfo(scaledWidth, scaledHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        ReadOnlySpan<PixelColor> pixelSpan = scaledPixels.AsSpan();
        ReadOnlySpan<byte> byteSpan = MemoryMarshal.AsBytes(pixelSpan);

        using var data = SKData.CreateCopy(byteSpan);
        using var image = SKImage.FromPixels(info, data);
        if (image == null)
        {
            throw new InvalidOperationException("Failed to create Skia image from pixel buffer.");
        }

        var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        if (encoded == null)
        {
            throw new InvalidOperationException("Failed to encode image to PNG.");
        }

        return encoded;
    }

    private static SKData? TryEncodeToBmpData(PixelColor[] scaledPixels, int scaledWidth, int scaledHeight)
    {
        var info = new SKImageInfo(scaledWidth, scaledHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        ReadOnlySpan<PixelColor> pixelSpan = scaledPixels.AsSpan();
        ReadOnlySpan<byte> byteSpan = MemoryMarshal.AsBytes(pixelSpan);

        using var data = SKData.CreateCopy(byteSpan);
        using var image = SKImage.FromPixels(info, data);
        if (image == null)
        {
            return null;
        }

        try
        {
            return image.Encode(SKEncodedImageFormat.Bmp, 100);
        }
        catch
        {
            return null;
        }
    }

    private static (PixelColor[] Pixels, int Width, int Height) ApplyNearestNeighborScale(
        PixelColor[] source, int width, int height, int scale)
    {
        if (scale == 1)
        {
            return (source, width, height);
        }

        int scaledWidth = width * scale;
        int scaledHeight = height * scale;
        var scaledPixels = new PixelColor[scaledWidth * scaledHeight];

        for (int y = 0; y < scaledHeight; y++)
        {
            int srcY = y / scale;
            int srcRowOffset = srcY * width;
            int dstRowOffset = y * scaledWidth;

            for (int x = 0; x < scaledWidth; x++)
            {
                int srcX = x / scale;
                scaledPixels[dstRowOffset + x] = source[srcRowOffset + srcX];
            }
        }

        return (scaledPixels, scaledWidth, scaledHeight);
    }

    private static async Task WriteLosslessBmpAsync(
        Stream destination, PixelColor[] pixels, int width, int height, CancellationToken cancellationToken)
    {
        const int bytesPerPixel = 4;
        int rowBytes = width * bytesPerPixel;
        int imageSize = rowBytes * height;
        const int fileHeaderSize = 14;
        const int infoHeaderSize = 40;
        int fileSize = fileHeaderSize + infoHeaderSize + imageSize;

        byte[] header = new byte[fileHeaderSize + infoHeaderSize];

        // BITMAPFILEHEADER
        header[0] = 0x42; // 'B'
        header[1] = 0x4D; // 'M'
        BitConverter.GetBytes(fileSize).CopyTo(header, 2);
        BitConverter.GetBytes(fileHeaderSize + infoHeaderSize).CopyTo(header, 10);

        // BITMAPINFOHEADER
        BitConverter.GetBytes(infoHeaderSize).CopyTo(header, 14);
        BitConverter.GetBytes(width).CopyTo(header, 18);
        BitConverter.GetBytes(height).CopyTo(header, 22); // positive = bottom-up
        BitConverter.GetBytes((short)1).CopyTo(header, 26); // Planes
        BitConverter.GetBytes((short)32).CopyTo(header, 28); // Bits per pixel (BGRA)
        BitConverter.GetBytes(0).CopyTo(header, 30); // BI_RGB (uncompressed)
        BitConverter.GetBytes(imageSize).CopyTo(header, 34);

        await destination.WriteAsync(header.AsMemory(0, header.Length), cancellationToken).ConfigureAwait(false);

        // Standard BMP rows are bottom-up
        byte[] rowBuffer = new byte[rowBytes];
        for (int y = height - 1; y >= 0; y--)
        {
            int srcRowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                var color = pixels[srcRowOffset + x];
                int offset = x * 4;
                rowBuffer[offset] = color.B;
                rowBuffer[offset + 1] = color.G;
                rowBuffer[offset + 2] = color.R;
                rowBuffer[offset + 3] = color.A;
            }

            await destination.WriteAsync(rowBuffer.AsMemory(0, rowBytes), cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ValidateDimensions(int width, int height, int scale, int pixelCount)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        if (scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be at least 1.");
        }

        if (pixelCount != width * height)
        {
            throw new ArgumentException($"Pixel count ({pixelCount}) does not match dimensions ({width}x{height} = {width * height}).");
        }
    }
}

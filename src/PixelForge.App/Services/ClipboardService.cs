using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using PixelForge.Core.Models;

namespace PixelForge.App.Services;

/// <summary>
/// Implements pixel buffer clipboard operations supporting both in-memory cache and OS clipboard integration.
/// </summary>
public class ClipboardService : IClipboardService
{
    private PixelColor[]? _memoryBuffer;
    private int _memoryWidth;
    private int _memoryHeight;

    private class ClipboardPayload
    {
        [JsonPropertyName("magic")]
        public string Magic { get; set; } = "PixelForgePixels_v1";

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("pixelsBase64")]
        public string PixelsBase64 { get; set; } = string.Empty;
    }

    public async Task SetPixelsAsync(PixelColor[] pixels, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0 || pixels.Length != width * height)
        {
            throw new ArgumentException("Invalid pixel dimensions or length.");
        }

        // Store copy in memory buffer
        _memoryBuffer = new PixelColor[pixels.Length];
        Array.Copy(pixels, _memoryBuffer, pixels.Length);
        _memoryWidth = width;
        _memoryHeight = height;

        // Try syncing to system clipboard via text payload
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.Clipboard != null)
            {
                var bytes = MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray();
                var payload = new ClipboardPayload
                {
                    Width = width,
                    Height = height,
                    PixelsBase64 = Convert.ToBase64String(bytes)
                };

                string json = JsonSerializer.Serialize(payload);
                await desktop.MainWindow.Clipboard.SetTextAsync(json).ConfigureAwait(false);
            }
        }
        catch
        {
            // Fallback to internal memory buffer if OS clipboard fails
        }
    }

    public async Task<(PixelColor[] Pixels, int Width, int Height)?> GetPixelsAsync()
    {
        // Try OS clipboard first
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.Clipboard != null)
            {
                string? text = await desktop.MainWindow.Clipboard.GetTextAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(text) && text.Contains("PixelForgePixels_v1"))
                {
                    var payload = JsonSerializer.Deserialize<ClipboardPayload>(text);
                    if (payload != null && payload.Width > 0 && payload.Height > 0 && !string.IsNullOrEmpty(payload.PixelsBase64))
                    {
                        byte[] rawBytes = Convert.FromBase64String(payload.PixelsBase64);
                        int expectedByteCount = payload.Width * payload.Height * 4;
                        if (rawBytes.Length == expectedByteCount)
                        {
                            var pixels = new PixelColor[payload.Width * payload.Height];
                            MemoryMarshal.Cast<byte, PixelColor>(rawBytes).CopyTo(pixels);
                            return (pixels, payload.Width, payload.Height);
                        }
                    }
                }
            }
        }
        catch
        {
            // If OS clipboard read fails, fallback to memory buffer
        }

        if (_memoryBuffer != null && _memoryWidth > 0 && _memoryHeight > 0)
        {
            var copy = new PixelColor[_memoryBuffer.Length];
            Array.Copy(_memoryBuffer, copy, _memoryBuffer.Length);
            return (copy, _memoryWidth, _memoryHeight);
        }

        return null;
    }

    public async Task<bool> HasPixelsAsync()
    {
        if (_memoryBuffer != null && _memoryBuffer.Length > 0)
        {
            return true;
        }

        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.Clipboard != null)
            {
                string? text = await desktop.MainWindow.Clipboard.GetTextAsync().ConfigureAwait(false);
                return text != null && text.Contains("PixelForgePixels_v1");
            }
        }
        catch
        {
            // Clipboard check ignored
        }

        return false;
    }
}

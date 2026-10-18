using PixelForge.Core.Models;

namespace PixelForge.App.Services;

/// <summary>
/// Provides clipboard services for copying and pasting pixel buffers.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copies pixel buffer with specified dimensions to the clipboard.
    /// </summary>
    Task SetPixelsAsync(PixelColor[] pixels, int width, int height);

    /// <summary>
    /// Retrieves pixel buffer and its dimensions from the clipboard, or null if no pixels available.
    /// </summary>
    Task<(PixelColor[] Pixels, int Width, int Height)?> GetPixelsAsync();

    /// <summary>
    /// Checks whether the clipboard currently contains pixel data.
    /// </summary>
    Task<bool> HasPixelsAsync();
}

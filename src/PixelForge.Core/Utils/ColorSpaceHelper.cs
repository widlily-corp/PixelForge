using PixelForge.Core.Models;

namespace PixelForge.Core.Utils;

/// <summary>
/// High-performance utilities for color space transformations (RGB <-> HSV).
/// </summary>
public static class ColorSpaceHelper
{
    /// <summary>
    /// Converts a PixelColor (RGB) to HSV color space.
    /// </summary>
    /// <param name="color">The source RGB color.</param>
    /// <param name="h">Hue in degrees [0, 360).</param>
    /// <param name="s">Saturation in range [0, 1].</param>
    /// <param name="v">Value/Brightness in range [0, 1].</param>
    public static void ToHsv(PixelColor color, out float h, out float s, out float v)
    {
        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;

        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        float delta = max - min;

        v = max;

        if (max <= 0.00001f)
        {
            s = 0f;
            h = 0f;
            return;
        }

        s = delta / max;

        if (delta <= 0.00001f)
        {
            h = 0f;
            return;
        }

        if (Math.Abs(r - max) < 0.00001f)
        {
            h = 60f * (((g - b) / delta) % 6f);
        }
        else if (Math.Abs(g - max) < 0.00001f)
        {
            h = 60f * (((b - r) / delta) + 2f);
        }
        else
        {
            h = 60f * (((r - g) / delta) + 4f);
        }

        if (h < 0f)
        {
            h += 360f;
        }
    }

    /// <summary>
    /// Converts HSV values back to a 32-bit RGBA PixelColor.
    /// </summary>
    /// <param name="h">Hue in degrees [0, 360).</param>
    /// <param name="s">Saturation in range [0, 1].</param>
    /// <param name="v">Value/Brightness in range [0, 1].</param>
    /// <param name="alpha">Alpha channel byte (default 255).</param>
    /// <returns>Constructed PixelColor.</returns>
    public static PixelColor FromHsv(float h, float s, float v, byte alpha = 255)
    {
        h = ((h % 360f) + 360f) % 360f;
        s = Math.Clamp(s, 0f, 1f);
        v = Math.Clamp(v, 0f, 1f);

        float c = v * s;
        float x = c * (1f - Math.Abs(((h / 60f) % 2f) - 1f));
        float m = v - c;

        float r1 = 0f, g1 = 0f, b1 = 0f;

        int sector = (int)(h / 60f);
        switch (sector)
        {
            case 0:
                r1 = c; g1 = x; b1 = 0f;
                break;
            case 1:
                r1 = x; g1 = c; b1 = 0f;
                break;
            case 2:
                r1 = 0f; g1 = c; b1 = x;
                break;
            case 3:
                r1 = 0f; g1 = x; b1 = c;
                break;
            case 4:
                r1 = x; g1 = 0f; b1 = c;
                break;
            default: // 5
                r1 = c; g1 = 0f; b1 = x;
                break;
        }

        byte r = (byte)Math.Clamp((int)Math.Round((r1 + m) * 255f), 0, 255);
        byte g = (byte)Math.Clamp((int)Math.Round((g1 + m) * 255f), 0, 255);
        byte b = (byte)Math.Clamp((int)Math.Round((b1 + m) * 255f), 0, 255);

        return new PixelColor(r, g, b, alpha);
    }
}

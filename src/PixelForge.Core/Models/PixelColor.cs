using System.Globalization;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace PixelForge.Core.Models;

/// <summary>
/// Represents a 32-bit RGBA pixel color with conversion utilities and predefined color constants.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct PixelColor : IEquatable<PixelColor>
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public byte A { get; }

    public PixelColor(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public static readonly PixelColor Transparent = new(0, 0, 0, 0);
    public static readonly PixelColor Black = new(0, 0, 0, 255);
    public static readonly PixelColor White = new(255, 255, 255, 255);
    public static readonly PixelColor Red = new(255, 0, 0, 255);
    public static readonly PixelColor Green = new(0, 255, 0, 255);
    public static readonly PixelColor Blue = new(0, 0, 255, 255);
    public static readonly PixelColor Yellow = new(255, 255, 0, 255);
    public static readonly PixelColor Cyan = new(0, 255, 255, 255);
    public static readonly PixelColor Magenta = new(255, 0, 255, 255);
    public static readonly PixelColor Gray = new(128, 128, 128, 255);
    public static readonly PixelColor LightGray = new(192, 192, 192, 255);
    public static readonly PixelColor DarkGray = new(64, 64, 64, 255);
    public static readonly PixelColor Orange = new(255, 165, 0, 255);
    public static readonly PixelColor Purple = new(128, 0, 128, 255);

    /// <summary>
    /// Converts this color to a 32-bit ARGB unsigned integer.
    /// </summary>
    public uint ToArgb() => ((uint)A << 24) | ((uint)R << 16) | ((uint)G << 8) | (uint)B;

    /// <summary>
    /// Creates a PixelColor from a 32-bit ARGB unsigned integer.
    /// </summary>
    public static PixelColor FromArgb(uint argb)
    {
        byte a = (byte)((argb >> 24) & 0xFF);
        byte r = (byte)((argb >> 16) & 0xFF);
        byte g = (byte)((argb >> 8) & 0xFF);
        byte b = (byte)(argb & 0xFF);
        return new PixelColor(r, g, b, a);
    }

    /// <summary>
    /// Converts this color to SkiaSharp's SKColor.
    /// </summary>
    public SKColor ToSKColor() => new(R, G, B, A);

    /// <summary>
    /// Creates a PixelColor from SkiaSharp's SKColor.
    /// </summary>
    public static PixelColor FromSKColor(SKColor skColor) => new(skColor.Red, skColor.Green, skColor.Blue, skColor.Alpha);

    public static implicit operator SKColor(PixelColor color) => color.ToSKColor();
    public static implicit operator PixelColor(SKColor color) => FromSKColor(color);

    /// <summary>
    /// Formats the color as a hexadecimal string (#RRGGBB or #AARRGGBB).
    /// </summary>
    public string ToHex(bool includeAlpha = true)
    {
        return includeAlpha
            ? $"#{A:X2}{R:X2}{G:X2}{B:X2}"
            : $"#{R:X2}{G:X2}{B:X2}";
    }

    /// <summary>
    /// Parses a hexadecimal color string in format #RGB, #ARGB, #RRGGBB, or #AARRGGBB.
    /// </summary>
    public static PixelColor FromHex(string hex)
    {
        if (TryParse(hex, out var color))
        {
            return color;
        }

        throw new FormatException($"Invalid hex color string: '{hex}'");
    }

    /// <summary>
    /// Tries to parse a hexadecimal color string into a PixelColor.
    /// </summary>
    public static bool TryParse(string? hex, out PixelColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        ReadOnlySpan<char> span = hex.AsSpan().Trim();
        if (span.StartsWith("#"))
        {
            span = span[1..];
        }

        switch (span.Length)
        {
            case 3: // RGB
                if (byte.TryParse(span[..1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r3) &&
                    byte.TryParse(span.Slice(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g3) &&
                    byte.TryParse(span.Slice(2, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b3))
                {
                    color = new PixelColor((byte)(r3 * 17), (byte)(g3 * 17), (byte)(b3 * 17), 255);
                    return true;
                }
                return false;

            case 4: // ARGB
                if (byte.TryParse(span[..1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte a4) &&
                    byte.TryParse(span.Slice(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r4) &&
                    byte.TryParse(span.Slice(2, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g4) &&
                    byte.TryParse(span.Slice(3, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b4))
                {
                    color = new PixelColor((byte)(r4 * 17), (byte)(g4 * 17), (byte)(b4 * 17), (byte)(a4 * 17));
                    return true;
                }
                return false;

            case 6: // RRGGBB
                if (uint.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
                {
                    byte r = (byte)((rgb >> 16) & 0xFF);
                    byte g = (byte)((rgb >> 8) & 0xFF);
                    byte b = (byte)(rgb & 0xFF);
                    color = new PixelColor(r, g, b, 255);
                    return true;
                }
                return false;

            case 8: // AARRGGBB
                if (uint.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
                {
                    color = FromArgb(argb);
                    return true;
                }
                return false;

            default:
                return false;
        }
    }

    public static int Distance(PixelColor a, PixelColor b)
    {
        int dr = a.R - b.R;
        int dg = a.G - b.G;
        int db = a.B - b.B;
        int da = a.A - b.A;
        return (int)Math.Sqrt(dr * dr + dg * dg + db * db + da * da);
    }

    public static PixelColor Lerp(PixelColor a, PixelColor b, float t)
    {
        float clampedT = Math.Clamp(t, 0f, 1f);
        byte r = (byte)Math.Round(a.R + (b.R - a.R) * clampedT);
        byte g = (byte)Math.Round(a.G + (b.G - a.G) * clampedT);
        byte bVal = (byte)Math.Round(a.B + (b.B - a.B) * clampedT);
        byte alpha = (byte)Math.Round(a.A + (b.A - a.A) * clampedT);
        return new PixelColor(r, g, bVal, alpha);
    }

    public bool Equals(PixelColor other) => R == other.R && G == other.G && B == other.B && A == other.A;

    public override bool Equals(object? obj) => obj is PixelColor other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(R, G, B, A);

    public static bool operator ==(PixelColor left, PixelColor right) => left.Equals(right);

    public static bool operator !=(PixelColor left, PixelColor right) => !left.Equals(right);

    public override string ToString() => ToHex(A < 255);
}

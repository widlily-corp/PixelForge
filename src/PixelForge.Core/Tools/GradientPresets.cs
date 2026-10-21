using PixelForge.Core.Models;

namespace PixelForge.Core.Tools;

/// <summary>
/// Predefined multi-stop gradient color transitions for retro and modern pixel art.
/// </summary>
public record GradientPreset(string Name, IReadOnlyList<GradientStop> Stops)
{
    public override string ToString() => Name;

    /// <summary>
    /// Creates a multi-stop preset distributing the specified colors evenly from 0.0 to 1.0.
    /// </summary>
    public static GradientPreset FromColors(string name, params PixelColor[] colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        if (colors.Length == 0)
        {
            return new GradientPreset(name, Array.Empty<GradientStop>());
        }

        if (colors.Length == 1)
        {
            return new GradientPreset(name, new[] { new GradientStop(0f, colors[0]), new GradientStop(1f, colors[0]) });
        }

        var stops = new GradientStop[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            float pos = (float)i / (colors.Length - 1);
            stops[i] = new GradientStop(pos, colors[i]);
        }

        return new GradientPreset(name, stops);
    }
}

/// <summary>
/// Collection of classic multi-stop pixel art gradient presets.
/// </summary>
public static class GradientPresets
{
    public static readonly GradientPreset TwoTone = new("2 Цвета (ЛКМ ➔ ПКМ)", Array.Empty<GradientStop>());

    public static readonly GradientPreset Sunset = GradientPreset.FromColors(
        "Закат (Sunset)",
        new PixelColor(59, 20, 67),    // #3B1443 Deep Violet
        new PixelColor(158, 25, 70),   // #9E1946 Crimson
        new PixelColor(226, 109, 34),  // #E26D22 Bright Orange
        new PixelColor(244, 211, 94)   // #F4D35E Golden Yellow
    );

    public static readonly GradientPreset Fire = GradientPreset.FromColors(
        "Огонь (Fire)",
        new PixelColor(10, 10, 10),    // #0A0A0A Pitch Black
        new PixelColor(133, 15, 15),   // #850F0F Deep Crimson
        new PixelColor(222, 84, 30),   // #DE541E Hot Orange
        new PixelColor(249, 231, 73),  // #F9E749 Bright Yellow
        new PixelColor(255, 255, 255)  // #FFFFFF White Hot
    );

    public static readonly GradientPreset Cyberpunk = GradientPreset.FromColors(
        "Киберпанк (Cyberpunk)",
        new PixelColor(31, 18, 53),    // #1F1235 Deep Indigo
        new PixelColor(255, 0, 127),   // #FF007F Neon Magenta
        new PixelColor(0, 240, 255)    // #00F0FF Electric Cyan
    );

    public static readonly GradientPreset Ocean = GradientPreset.FromColors(
        "Океан (Ocean)",
        new PixelColor(5, 25, 35),     // #051923 Midnight Navy
        new PixelColor(0, 53, 84),     // #003554 Deep Marine
        new PixelColor(0, 166, 251),   // #00A6FB Azure Teal
        new PixelColor(144, 224, 239)  // #90E0EF Aqua Mint
    );

    public static readonly GradientPreset Forest = GradientPreset.FromColors(
        "Лес (Forest)",
        new PixelColor(27, 67, 50),    // #1B4332 Deep Moss
        new PixelColor(45, 106, 79),   // #2D6A4F Emerald
        new PixelColor(116, 198, 157), // #74C69D Sage Lime
        new PixelColor(216, 243, 220)  // #D8F3DC Pale Mint
    );

    public static readonly IReadOnlyList<GradientPreset> AllPresets = new[]
    {
        TwoTone,
        Sunset,
        Fire,
        Cyberpunk,
        Ocean,
        Forest
    };
}

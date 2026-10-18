using PixelForge.Core.Models;

namespace PixelForge.Core.Palettes;

/// <summary>
/// Provides predefined retro and pixel art color palettes.
/// </summary>
public static class PalettePresets
{
    /// <summary>
    /// DawnBringer 32 (DB32) - versatile 32-color palette tailored for pixel art.
    /// </summary>
    public static Palette Default32 => new(
        "Default 32",
        [
            PixelColor.FromHex("#000000"),
            PixelColor.FromHex("#222034"),
            PixelColor.FromHex("#45283C"),
            PixelColor.FromHex("#663931"),
            PixelColor.FromHex("#8F563B"),
            PixelColor.FromHex("#DF7126"),
            PixelColor.FromHex("#D9A066"),
            PixelColor.FromHex("#EEC39A"),
            PixelColor.FromHex("#FBF236"),
            PixelColor.FromHex("#99E550"),
            PixelColor.FromHex("#6ABE30"),
            PixelColor.FromHex("#37946E"),
            PixelColor.FromHex("#4B692F"),
            PixelColor.FromHex("#524B24"),
            PixelColor.FromHex("#323C39"),
            PixelColor.FromHex("#3F3F74"),
            PixelColor.FromHex("#306082"),
            PixelColor.FromHex("#5B6EE1"),
            PixelColor.FromHex("#639BFF"),
            PixelColor.FromHex("#5FCDE4"),
            PixelColor.FromHex("#CBDBFC"),
            PixelColor.FromHex("#FFFFFF"),
            PixelColor.FromHex("#9BADB7"),
            PixelColor.FromHex("#847E87"),
            PixelColor.FromHex("#696A6A"),
            PixelColor.FromHex("#595652"),
            PixelColor.FromHex("#76428A"),
            PixelColor.FromHex("#AC3232"),
            PixelColor.FromHex("#D95763"),
            PixelColor.FromHex("#D77643"),
            PixelColor.FromHex("#8F974A"),
            PixelColor.FromHex("#8A6F30")
        ]);

    /// <summary>
    /// PICO-8 16-color fantasy console palette.
    /// </summary>
    public static Palette Pico8 => new(
        "PICO-8",
        [
            PixelColor.FromHex("#000000"),
            PixelColor.FromHex("#1D2B53"),
            PixelColor.FromHex("#7E2553"),
            PixelColor.FromHex("#008751"),
            PixelColor.FromHex("#AB5236"),
            PixelColor.FromHex("#5F574F"),
            PixelColor.FromHex("#C2C3C7"),
            PixelColor.FromHex("#FFF1E8"),
            PixelColor.FromHex("#FF004D"),
            PixelColor.FromHex("#FFA300"),
            PixelColor.FromHex("#FFEC27"),
            PixelColor.FromHex("#00E436"),
            PixelColor.FromHex("#29ADFF"),
            PixelColor.FromHex("#83769C"),
            PixelColor.FromHex("#FF77A8"),
            PixelColor.FromHex("#FFCCAA")
        ]);

    /// <summary>
    /// Original Game Boy DMG 4-shade green palette.
    /// </summary>
    public static Palette GameBoy => new(
        "GameBoy",
        [
            PixelColor.FromHex("#0F380F"),
            PixelColor.FromHex("#306230"),
            PixelColor.FromHex("#8BAC0F"),
            PixelColor.FromHex("#9BBC0F")
        ]);

    /// <summary>
    /// Classic NES 54-color palette.
    /// </summary>
    public static Palette Nes => new(
        "NES",
        [
            PixelColor.FromHex("#666666"),
            PixelColor.FromHex("#002A88"),
            PixelColor.FromHex("#1412A7"),
            PixelColor.FromHex("#3B00A4"),
            PixelColor.FromHex("#5C007E"),
            PixelColor.FromHex("#6E0040"),
            PixelColor.FromHex("#6C0600"),
            PixelColor.FromHex("#561D00"),
            PixelColor.FromHex("#333500"),
            PixelColor.FromHex("#0B4800"),
            PixelColor.FromHex("#005200"),
            PixelColor.FromHex("#004F08"),
            PixelColor.FromHex("#00404D"),
            PixelColor.FromHex("#000000"),
            PixelColor.FromHex("#ADADAD"),
            PixelColor.FromHex("#155FD9"),
            PixelColor.FromHex("#4240FF"),
            PixelColor.FromHex("#7527FE"),
            PixelColor.FromHex("#A01ACC"),
            PixelColor.FromHex("#B71E7B"),
            PixelColor.FromHex("#B53120"),
            PixelColor.FromHex("#994E00"),
            PixelColor.FromHex("#6B6D00"),
            PixelColor.FromHex("#388700"),
            PixelColor.FromHex("#0C9300"),
            PixelColor.FromHex("#008F32"),
            PixelColor.FromHex("#007C8D"),
            PixelColor.FromHex("#FFFFFF"),
            PixelColor.FromHex("#64B0FF"),
            PixelColor.FromHex("#9290FF"),
            PixelColor.FromHex("#C676FF"),
            PixelColor.FromHex("#F36AFF"),
            PixelColor.FromHex("#FE6ECC"),
            PixelColor.FromHex("#FE8170"),
            PixelColor.FromHex("#EA9E22"),
            PixelColor.FromHex("#BCBE00"),
            PixelColor.FromHex("#88D800"),
            PixelColor.FromHex("#5CE430"),
            PixelColor.FromHex("#45E082"),
            PixelColor.FromHex("#48CDDE"),
            PixelColor.FromHex("#4F4F4F"),
            PixelColor.FromHex("#C0E1FF"),
            PixelColor.FromHex("#D3D2FF"),
            PixelColor.FromHex("#E8C1FF"),
            PixelColor.FromHex("#FBC2FF"),
            PixelColor.FromHex("#FEC2EB"),
            PixelColor.FromHex("#FECCC5"),
            PixelColor.FromHex("#F7D8A5"),
            PixelColor.FromHex("#E4E594"),
            PixelColor.FromHex("#CFEF96"),
            PixelColor.FromHex("#BDF4AB"),
            PixelColor.FromHex("#B3F3CC"),
            PixelColor.FromHex("#B5EBF2"),
            PixelColor.FromHex("#B8B8B8")
        ]);

    public static IReadOnlyList<Palette> AllPresets =>
    [
        Default32,
        Pico8,
        GameBoy,
        Nes
    ];

    public static Palette GetPresetByName(string name)
    {
        return AllPresets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? Default32;
    }
}

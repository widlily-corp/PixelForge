using PixelForge.Core.Models;
using PixelForge.Core.Utils;

namespace PixelForge.Core.Palettes;

/// <summary>
/// Predefined color ramp styles for shading and highlight generation.
/// </summary>
public enum RampPresetType
{
    Sunlight,
    Fire,
    Ice,
    Toxic,
    MetallicGold,
    Custom
}

/// <summary>
/// Configuration parameters for generating a harmonious pixel art color ramp.
/// </summary>
public record struct ColorRampSettings
{
    public PixelColor BaseColor { get; set; }
    public int StepCount { get; set; }
    public float HueShiftDegrees { get; set; }
    public float SaturationShift { get; set; }
    public float LightnessMin { get; set; }
    public float LightnessMax { get; set; }

    public ColorRampSettings(
        PixelColor baseColor,
        int stepCount = 5,
        float hueShiftDegrees = 25f,
        float saturationShift = 0.1f,
        float lightnessMin = 0.15f,
        float lightnessMax = 0.95f)
    {
        BaseColor = baseColor;
        StepCount = Math.Clamp(stepCount, 3, 16);
        HueShiftDegrees = hueShiftDegrees;
        SaturationShift = saturationShift;
        LightnessMin = Math.Clamp(lightnessMin, 0.05f, 0.5f);
        LightnessMax = Math.Clamp(lightnessMax, 0.5f, 1.0f);
    }
}

/// <summary>
/// Generates artistic color ramps with professional Hue Shifting and saturation curves.
/// </summary>
public static class ColorRampGenerator
{
    /// <summary>
    /// Generates a palette color ramp from shadow to highlight based on input settings.
    /// </summary>
    /// <param name="settings">The ramp generation parameters.</param>
    /// <returns>An array of PixelColors ordered from darkest shadow to brightest highlight.</returns>
    public static PixelColor[] GenerateRamp(ColorRampSettings settings)
    {
        int count = Math.Clamp(settings.StepCount, 3, 16);
        var result = new PixelColor[count];

        ColorSpaceHelper.ToHsv(settings.BaseColor, out float baseH, out float baseS, out float baseV);

        // Midtone index around the middle of the ramp
        float midIndex = (count - 1) / 2.0f;

        for (int i = 0; i < count; i++)
        {
            // Normalized position: 0.0 (deepest shadow) to 1.0 (highest light)
            float t = (count <= 1) ? 0.5f : (float)i / (count - 1);

            // 1. Value/Lightness curve (monotonically increasing)
            float v = settings.LightnessMin + (settings.LightnessMax - settings.LightnessMin) * t;

            // 2. Hue Shifting:
            // Relative offset from center: -0.5 at shadow, +0.5 at highlight
            float centerOffset = (i - midIndex) / (count - 1);
            float h = baseH + centerOffset * (settings.HueShiftDegrees * 2f);

            // 3. Saturation curve:
            // Midtones maintain highest saturation; extreme shadows and highlights have reduced saturation
            float distFromCenter = Math.Abs(centerOffset) * 2f; // 0 at center, 1 at ends
            float s = baseS * (1f - 0.25f * distFromCenter * distFromCenter) + (settings.SaturationShift * (1f - distFromCenter));
            s = Math.Clamp(s, 0.05f, 1f);

            result[i] = ColorSpaceHelper.FromHsv(h, s, v, settings.BaseColor.A);
        }

        return result;
    }

    /// <summary>
    /// Convenience helper to generate a ramp with default parameters.
    /// </summary>
    public static PixelColor[] GenerateRamp(
        PixelColor baseColor,
        int stepCount = 5,
        float hueShiftDegrees = 25f,
        float saturationShift = 0.1f)
    {
        var settings = new ColorRampSettings(baseColor, stepCount, hueShiftDegrees, saturationShift);
        return GenerateRamp(settings);
    }

    /// <summary>
    /// Generates a ramp tailored to a specific artistic preset theme.
    /// </summary>
    /// <param name="preset">The artistic theme preset.</param>
    /// <param name="overrideBase">Optional base color override.</param>
    /// <param name="stepCount">Number of ramp steps.</param>
    /// <returns>Generated ramp color array.</returns>
    public static PixelColor[] GeneratePreset(RampPresetType preset, PixelColor? overrideBase = null, int stepCount = 5)
    {
        switch (preset)
        {
            case RampPresetType.Sunlight:
                // Cool purple/blue shadow (-30 deg) shifting towards warm yellow (+30 deg)
                return GenerateRamp(new ColorRampSettings(
                    overrideBase ?? new PixelColor(220, 80, 70),
                    stepCount,
                    hueShiftDegrees: 35f,
                    saturationShift: 0.15f,
                    lightnessMin: 0.18f,
                    lightnessMax: 0.98f));

            case RampPresetType.Fire:
                // Deep dark crimson shadow into fiery orange and bright white-yellow
                return GenerateRamp(new ColorRampSettings(
                    overrideBase ?? new PixelColor(230, 80, 20),
                    stepCount,
                    hueShiftDegrees: 28f,
                    saturationShift: 0.2f,
                    lightnessMin: 0.12f,
                    lightnessMax: 0.98f));

            case RampPresetType.Ice:
                // Deep navy into cold cyan into icy white
                return GenerateRamp(new ColorRampSettings(
                    overrideBase ?? new PixelColor(50, 160, 230),
                    stepCount,
                    hueShiftDegrees: -25f,
                    saturationShift: -0.1f,
                    lightnessMin: 0.16f,
                    lightnessMax: 0.98f));

            case RampPresetType.Toxic:
                // Dark emerald shadow shifting into glowing lime/acid yellow
                return GenerateRamp(new ColorRampSettings(
                    overrideBase ?? new PixelColor(50, 205, 50),
                    stepCount,
                    hueShiftDegrees: 30f,
                    saturationShift: 0.15f,
                    lightnessMin: 0.14f,
                    lightnessMax: 0.96f));

            case RampPresetType.MetallicGold:
                // Deep umber brown shifting into rich metallic ochre and pale gold
                return GenerateRamp(new ColorRampSettings(
                    overrideBase ?? new PixelColor(218, 165, 32),
                    stepCount,
                    hueShiftDegrees: 20f,
                    saturationShift: 0.1f,
                    lightnessMin: 0.15f,
                    lightnessMax: 0.95f));

            default: // Custom
                return GenerateRamp(new ColorRampSettings(
                    overrideBase ?? new PixelColor(100, 150, 240),
                    stepCount,
                    hueShiftDegrees: 20f));
        }
    }
}

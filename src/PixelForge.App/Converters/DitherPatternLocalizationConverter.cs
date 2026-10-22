using System;
using System.Globalization;
using Avalonia.Data.Converters;
using PixelForge.Core.Localization;
using PixelForge.Core.Models;

namespace PixelForge.App.Converters;

/// <summary>
/// Value converter translating <see cref="DitherPattern"/> enum values to localized human-readable display names.
/// </summary>
public class DitherPatternLocalizationConverter : IValueConverter
{
    /// <summary>
    /// Static shared singleton instance for XAML bindings.
    /// </summary>
    public static readonly DitherPatternLocalizationConverter Instance = new();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DitherPattern pattern)
        {
            string key = "Dither_" + pattern.ToString();
            string localized = LocalizationService.Instance[key];
            return string.IsNullOrEmpty(localized) || string.Equals(localized, key, StringComparison.OrdinalIgnoreCase)
                ? pattern.ToString()
                : localized;
        }

        if (value is string str && Enum.TryParse<DitherPattern>(str, true, out var parsed))
        {
            string key = "Dither_" + parsed.ToString();
            string localized = LocalizationService.Instance[key];
            return string.IsNullOrEmpty(localized) || string.Equals(localized, key, StringComparison.OrdinalIgnoreCase)
                ? parsed.ToString()
                : localized;
        }

        if (value is int intVal && Enum.IsDefined(typeof(DitherPattern), intVal))
        {
            var patternFromInt = (DitherPattern)intVal;
            string key = "Dither_" + patternFromInt.ToString();
            string localized = LocalizationService.Instance[key];
            return string.IsNullOrEmpty(localized) || string.Equals(localized, key, StringComparison.OrdinalIgnoreCase)
                ? patternFromInt.ToString()
                : localized;
        }

        return value?.ToString() ?? string.Empty;
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

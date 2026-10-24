using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using PixelForge.Core.Models;

namespace PixelForge.App.Converters;

/// <summary>
/// Converts a <see cref="PixelColor"/> struct into an Avalonia <see cref="IBrush"/>.
/// </summary>
public class PixelColorToBrushConverter : IValueConverter
{
    public static readonly PixelColorToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PixelColor color)
        {
            return new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
        }

        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

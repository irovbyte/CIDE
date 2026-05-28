using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace CIDE.Helpers;

public class DepthToMarginConverter : IValueConverter
{
    public double Multiplier { get; set; } = 12.0;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is int depth ? new Thickness(depth * Multiplier, 0, 0, 0) : new Thickness(0);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

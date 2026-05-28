using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace CIDE.Helpers;

public class IconPathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && Uri.TryCreate(path, UriKind.Absolute, out var uri))
        {
            try
            {
                return path.EndsWith(".svg")
                    ? new Avalonia.Svg.Skia.SvgImage { Source = Avalonia.Svg.Skia.SvgSource.Load(path, null) }
                    : new Bitmap(AssetLoader.Open(uri));
            }
            catch { return null; }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

using System.Globalization;
namespace CIDE.Helpers;

internal sealed class DepthToMarginConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new Thickness((value is int d ? d : 0) * (16 + 5), 0, 5, 0);
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => 0;
}

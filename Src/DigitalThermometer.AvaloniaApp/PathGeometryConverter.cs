using System;
using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DigitalThermometer.AvaloniaApp
{
    public class PathGeometryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => PathGeometry.Parse(value.ToString());

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}

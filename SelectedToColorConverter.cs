using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Converters
{
    public class SelectedToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
            {
                return new SolidColorBrush(Color.FromArgb(0xFF, 0xB0, 0xD0, 0xFF));
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
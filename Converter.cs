using System;
using System.Globalization;
using System.Windows.Data;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null || parameter is null)
                return false;

            string? enumValue = value.ToString();
            string? targetValue = parameter.ToString();

            return enumValue?.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase) ?? false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not true || parameter is null)
                return Binding.DoNothing;

            string? parameterString = parameter.ToString();
            if (parameterString is null)
                return Binding.DoNothing;

            return Enum.Parse(targetType, parameterString);
        }
    }
}
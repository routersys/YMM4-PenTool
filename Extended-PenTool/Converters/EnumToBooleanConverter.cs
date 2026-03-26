using System.Globalization;
using System.Windows.Data;

namespace ExtendedPenTool.Converters;

public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return value.ToString()?.Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null) return Binding.DoNothing;
        var str = parameter.ToString();
        return str is null ? Binding.DoNothing : Enum.Parse(targetType, str);
    }
}

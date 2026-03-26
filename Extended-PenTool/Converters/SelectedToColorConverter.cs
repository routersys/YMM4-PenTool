using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ExtendedPenTool.Converters;

public sealed class SelectedToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush SelectedBrush = new(Color.FromArgb(0xFF, 0xB0, 0xD0, 0xFF));
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);

    static SelectedToColorConverter()
    {
        SelectedBrush.Freeze();
        TransparentBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? SelectedBrush : TransparentBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

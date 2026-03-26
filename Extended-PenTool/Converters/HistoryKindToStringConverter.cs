using ExtendedPenTool.Enums;
using System.Globalization;
using System.Windows.Data;

namespace ExtendedPenTool.Converters;

internal sealed class HistoryKindToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is HistoryKind kind ? kind.GetDisplayLabel() : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

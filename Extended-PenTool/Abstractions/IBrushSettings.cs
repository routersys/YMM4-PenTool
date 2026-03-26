using System.Windows.Media;

namespace ExtendedPenTool.Abstractions;

internal interface IBrushSettings
{
    Color StrokeColor { get; set; }
    double StrokeThickness { get; set; }
    bool IsPressure { get; set; }
}

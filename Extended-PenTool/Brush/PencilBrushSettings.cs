using System.Windows.Media;

namespace ExtendedPenTool.Brush;

internal sealed class PencilBrushSettings : BrushSettingsBase
{
    public PencilBrushSettings()
    {
        StrokeColor = Colors.Gray;
        StrokeThickness = 5;
        IsPressure = true;
    }
}

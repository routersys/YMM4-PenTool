using System.Windows.Media;

namespace ExtendedPenTool.Brush;

internal sealed class HighlighterBrushSettings : BrushSettingsBase
{
    public HighlighterBrushSettings()
    {
        StrokeColor = Colors.Yellow;
        StrokeThickness = 20;
    }
}

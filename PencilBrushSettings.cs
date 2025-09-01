using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Brush
{
    internal class PencilBrushSettings : BrushSettingsBase
    {
        public PencilBrushSettings()
        {
            StrokeColor = Colors.Gray;
            StrokeThickness = 5;
            IsPressure = true;
        }
    }
}
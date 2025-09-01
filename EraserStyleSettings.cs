using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Shape.Pen.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    class EraserStyleSettings : BrushSettingsBase
    {
        public EraserMode Mode { get => mode; set => Set(ref mode, value); }
        private EraserMode mode = EraserMode.Line;

        public EraserStyleSettings()
        {
            StrokeThickness = 20;
        }
    }
}
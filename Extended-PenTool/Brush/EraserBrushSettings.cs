using ExtendedPenTool.Enums;

namespace ExtendedPenTool.Brush;

internal sealed class EraserBrushSettings : BrushSettingsBase
{
    public EraserMode Mode { get => mode; set => Set(ref mode, value); }
    private EraserMode mode = EraserMode.Line;

    public EraserBrushSettings()
    {
        StrokeThickness = 20;
    }
}

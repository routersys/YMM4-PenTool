using ExtendedPenTool.Brush;
using ExtendedPenTool.Enums;
using ExtendedPenTool.Models;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin;

namespace ExtendedPenTool.Settings;

internal sealed class PenSettings : SettingsBase<PenSettings>
{
    public override SettingsCategory Category => SettingsCategory.None;
    public override string Name => Texts.SettingsName;
    public override bool HasSettingView => false;
    public override object SettingView => throw new NotImplementedException();

    public BrushType SelectedBrushType { get => selectedBrushType; set => Set(ref selectedBrushType, value); }
    private BrushType selectedBrushType = BrushType.Pen;

    public MouseWheelAction MouseWheelAction { get => mouseWheelAction; set => Set(ref mouseWheelAction, value); }
    private MouseWheelAction mouseWheelAction = MouseWheelAction.PenSize;

    public ToolbarLayout ToolbarLayout { get => toolbarLayout; set => Set(ref toolbarLayout, value); }
    private ToolbarLayout toolbarLayout = ToolbarLayout.Top;

    public PenBrushSettings PenStyle { get; } = new() { StrokeColor = Colors.White, StrokeThickness = 10 };
    public HighlighterBrushSettings HighlighterStyle { get; } = new();
    public PencilBrushSettings PencilStyle { get; } = new();
    public EraserBrushSettings EraserStyle { get; } = new();

    public Dictionary<string, PanelLayoutInfo> Layout { get; set; } = [];

    public override void Initialize()
    {
    }
}

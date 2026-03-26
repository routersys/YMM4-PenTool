using ExtendedPenTool.SourceGenerator;
using ExtendedPenTool.Localization;

namespace ExtendedPenTool.Enums;

internal enum HistoryKind
{
    [DisplayLabel("Draw", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryDraw))]
    Draw,
    [DisplayLabel("Erase", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryErase))]
    Erase,
    [DisplayLabel("Select", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistorySelect))]
    Select,
    [DisplayLabel("AddLayer", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryAddLayer))]
    AddLayer,
    [DisplayLabel("RemoveLayer", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryRemoveLayer))]
    RemoveLayer,
    [DisplayLabel("MoveLayer", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryMoveLayer))]
    MoveLayer,
    [DisplayLabel("Move", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryMoveStrokes))]
    MoveStrokes,
    [DisplayLabel("Resize", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryResizeStrokes))]
    ResizeStrokes,
    [DisplayLabel("Rotate", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryRotateStrokes))]
    RotateStrokes,
    [DisplayLabel("LayerProperty", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryLayerProperty))]
    LayerProperty,
}

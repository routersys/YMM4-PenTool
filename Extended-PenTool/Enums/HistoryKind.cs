using ExtendedPenTool.SourceGenerator;

namespace ExtendedPenTool.Enums;

internal enum HistoryKind
{
    [DisplayLabel("", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryDraw))]
    Draw,
    [DisplayLabel("", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryErase))]
    Erase,
    [DisplayLabel("", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistorySelect))]
    Select,
    [DisplayLabel("", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryAddLayer))]
    AddLayer,
    [DisplayLabel("", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryRemoveLayer))]
    RemoveLayer,
    [DisplayLabel("", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryMoveLayer))]
    MoveLayer,
    [DisplayLabel("", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryMoveStrokes))]
    MoveStrokes,
    [DisplayLabel("", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryResizeStrokes))]
    ResizeStrokes,
    [DisplayLabel("", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryRotateStrokes))]
    RotateStrokes,
    [DisplayLabel("", ResourceType = typeof(Texts), ResourceName = nameof(Texts.HistoryLayerProperty))]
    LayerProperty,
}

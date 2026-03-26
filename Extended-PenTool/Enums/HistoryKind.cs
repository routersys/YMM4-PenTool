using ExtendedPenTool.SourceGenerator;

namespace ExtendedPenTool.Enums;

internal enum HistoryKind
{
    [DisplayLabel("描画")]
    Draw,
    [DisplayLabel("消去")]
    Erase,
    [DisplayLabel("選択")]
    Select,
    [DisplayLabel("レイヤーを追加")]
    AddLayer,
    [DisplayLabel("レイヤーを削除")]
    RemoveLayer,
    [DisplayLabel("レイヤーを移動")]
    MoveLayer,
    [DisplayLabel("移動")]
    MoveStrokes,
    [DisplayLabel("サイズ変更")]
    ResizeStrokes,
    [DisplayLabel("回転")]
    RotateStrokes,
    [DisplayLabel("レイヤーのプロパティを変更")]
    LayerProperty,
}

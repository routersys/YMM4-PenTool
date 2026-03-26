using ExtendedPenTool.Enums;

namespace ExtendedPenTool.Models;

internal sealed class HistoryItem(HistoryKind kind, Action undo, Action redo)
{
    public HistoryKind Kind { get; } = kind;
    public Action Undo { get; } = undo;
    public Action Redo { get; } = redo;
}

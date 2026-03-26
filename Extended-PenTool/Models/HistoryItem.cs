namespace ExtendedPenTool.Models;

internal sealed class HistoryItem(string description, Action undo, Action redo)
{
    public string Description { get; } = description;
    public Action Undo { get; } = undo;
    public Action Redo { get; } = redo;
}

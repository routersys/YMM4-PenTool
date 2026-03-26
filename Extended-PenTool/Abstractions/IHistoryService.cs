using ExtendedPenTool.Enums;
using ExtendedPenTool.Models;
using System.Collections.ObjectModel;

namespace ExtendedPenTool.Abstractions;

internal interface IHistoryService
{
    ObservableCollection<HistoryItem> Items { get; }
    int CurrentIndex { get; set; }
    bool CanUndo { get; }
    bool CanRedo { get; }
    void Push(HistoryKind kind, Action undo, Action redo);
    void Undo();
    void Redo();
    void MoveToState(int targetIndex);
    event Action? StateChanged;
}

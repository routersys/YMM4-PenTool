using ExtendedPenTool.Abstractions;
using ExtendedPenTool.Enums;
using ExtendedPenTool.Models;
using System.Collections.ObjectModel;

namespace ExtendedPenTool.Services;

internal sealed class HistoryService : IHistoryService
{
    public ObservableCollection<HistoryItem> Items { get; } = [];

    private int currentIndex;
    public int CurrentIndex
    {
        get => currentIndex;
        set
        {
            if (currentIndex != value)
            {
                MoveToState(value);
            }
        }
    }

    public bool CanUndo => currentIndex < Items.Count;
    public bool CanRedo => currentIndex > 0;

    public event Action? StateChanged;

    public void Push(HistoryKind kind, Action undo, Action redo)
    {
        for (var i = 0; i < currentIndex; i++)
        {
            Items.RemoveAt(0);
        }
        currentIndex = 0;

        Items.Insert(0, new HistoryItem(kind, undo, redo));
        RaiseStateChanged();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        Items[currentIndex].Undo();
        currentIndex++;
        RaiseStateChanged();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        currentIndex--;
        Items[currentIndex].Redo();
        RaiseStateChanged();
    }

    public void MoveToState(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= Items.Count) return;

        while (currentIndex < targetIndex)
        {
            Items[currentIndex].Undo();
            currentIndex++;
        }
        while (currentIndex > targetIndex)
        {
            currentIndex--;
            Items[currentIndex].Redo();
        }

        RaiseStateChanged();
    }

    private void RaiseStateChanged() => StateChanged?.Invoke();
}

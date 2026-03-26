using ExtendedPenTool.Models;
using System.Collections.ObjectModel;
using System.Windows.Ink;

namespace ExtendedPenTool.Abstractions;

internal interface ILayerService
{
    ObservableCollection<Layer> Layers { get; }
    Layer? SelectedLayer { get; set; }
    StrokeCollection AggregatedStrokes { get; }
    Dictionary<Stroke, Layer> StrokeLayerMap { get; }
    Dictionary<Stroke, Stroke> DisplayToOriginalMap { get; }
    void AddLayer();
    void RemoveLayer(Layer layer);
    void MoveLayer(int oldIndex, int newIndex);
    void RefreshVisibleStrokes();
    void UpdateThumbnail(Layer layer);
    void UpdatePreviewThumbnail(Layer layer);
    event Action? StrokesRefreshed;
}

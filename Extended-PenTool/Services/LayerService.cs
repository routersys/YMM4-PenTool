using ExtendedPenTool.Abstractions;
using ExtendedPenTool.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ExtendedPenTool.Services;

internal sealed class LayerService : ILayerService
{
    private readonly Size canvasSize;

    public ObservableCollection<Layer> Layers { get; } = [];

    public Layer? SelectedLayer { get; set; }

    public StrokeCollection AggregatedStrokes { get; } = [];

    public Dictionary<Stroke, Layer> StrokeLayerMap { get; } = [];

    public Dictionary<Stroke, Stroke> DisplayToOriginalMap { get; } = [];

    public event Action? StrokesRefreshed;

    public LayerService(Size canvasSize)
    {
        this.canvasSize = canvasSize;
    }

    public void AddLayer()
    {
        var layer = new Layer("");
        Layers.Insert(0, layer);
        SelectedLayer = layer;
    }

    public void RemoveLayer(Layer layer)
    {
        if (Layers.Count <= 1) return;
        var index = Layers.IndexOf(layer);
        Layers.Remove(layer);
        SelectedLayer = Layers.Count > index ? Layers[index] : Layers.FirstOrDefault();
    }

    public void MoveLayer(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Layers.Count || newIndex < 0 || newIndex >= Layers.Count) return;
        Layers.Move(oldIndex, newIndex);
    }

    public void RefreshVisibleStrokes()
    {
        AggregatedStrokes.Clear();
        StrokeLayerMap.Clear();
        DisplayToOriginalMap.Clear();

        var batch = new List<Stroke>();

        foreach (var layer in Layers.Reverse().ToList())
        {
            if (!layer.IsVisible) continue;

            foreach (var originalStroke in layer.Strokes.ToList())
            {
                var display = originalStroke.Clone();
                var da = display.DrawingAttributes;
                var color = da.Color;

                if (da.IsHighlighter)
                {
                    da.IsHighlighter = false;
                    color.A = (byte)(color.A / 2.0 * layer.Opacity);
                }
                else
                {
                    color.A = (byte)(color.A * layer.Opacity);
                }
                da.Color = color;

                batch.Add(display);
                StrokeLayerMap[display] = layer;
                DisplayToOriginalMap[display] = originalStroke;
            }
        }

        AggregatedStrokes.Add(new StrokeCollection(batch));
        StrokesRefreshed?.Invoke();
    }

    public void UpdateThumbnail(Layer layer)
    {
        const int thumbnailWidth = 48;
        const int thumbnailHeight = 27;

        var strokes = new StrokeCollection(layer.Strokes.ToList());
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, canvasSize.Width, canvasSize.Height));
            strokes.Draw(ctx);
        }

        var rtb = new RenderTargetBitmap(
            (int)canvasSize.Width, (int)canvasSize.Height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();

        var cropped = new CroppedBitmap(rtb, new Int32Rect(0, 0, (int)canvasSize.Width, (int)canvasSize.Height));
        var scaled = new TransformedBitmap(cropped, new ScaleTransform(
            thumbnailWidth / canvasSize.Width,
            thumbnailHeight / canvasSize.Height));
        scaled.Freeze();
        layer.Thumbnail = scaled;
    }

    public void UpdatePreviewThumbnail(Layer layer)
    {
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, canvasSize.Width, canvasSize.Height));

            var previewStrokes = new StrokeCollection();
            foreach (var stroke in layer.Strokes.ToList())
            {
                var cloned = stroke.Clone();
                var da = cloned.DrawingAttributes;
                var color = da.Color;

                if (da.IsHighlighter)
                {
                    da.IsHighlighter = false;
                    color.A = (byte)(color.A / 2.0 * layer.Opacity);
                }
                else
                {
                    color.A = (byte)(color.A * layer.Opacity);
                }
                da.Color = color;
                previewStrokes.Add(cloned);
            }

            previewStrokes.Draw(ctx);
        }

        var rtb = new RenderTargetBitmap(
            (int)canvasSize.Width, (int)canvasSize.Height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        layer.PreviewThumbnail = rtb;
    }
}

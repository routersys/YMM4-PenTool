using System.Windows.Ink;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Commons;

namespace ExtendedPenTool.Models;

internal sealed class Layer : Bindable
{
    public string Name { get => name; set => Set(ref name, value); }
    private string name = Texts.LayerDefaultNew;

    public bool IsVisible { get => isVisible; set => Set(ref isVisible, value); }
    private bool isVisible = true;

    public bool IsLocked { get => isLocked; set => Set(ref isLocked, value); }
    private bool isLocked;

    public double Opacity { get => opacity; set => Set(ref opacity, Math.Clamp(value, 0.0, 1.0)); }
    private double opacity = 1.0;

    [Newtonsoft.Json.JsonIgnore]
    public StrokeCollection Strokes { get; } = [];

    [Newtonsoft.Json.JsonIgnore]
    public BitmapSource? Thumbnail { get => thumbnail; set => Set(ref thumbnail, value); }
    private BitmapSource? thumbnail;

    [Newtonsoft.Json.JsonIgnore]
    public BitmapSource? PreviewThumbnail { get => previewThumbnail; set => Set(ref previewThumbnail, value); }
    private BitmapSource? previewThumbnail;

    public List<SerializableStroke> SerializableStrokes
    {
        get => [.. Strokes.Select(static s => new SerializableStroke(s))];
        set
        {
            Strokes.Clear();
            if (value is { Count: > 0 })
            {
                Strokes.Add(new StrokeCollection(value.Select(static ss => ss.ToStroke())));
            }
        }
    }

    public Layer(string name) => Name = name;

    public Layer() { }
}

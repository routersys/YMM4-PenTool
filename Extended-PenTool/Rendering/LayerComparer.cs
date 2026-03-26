using ExtendedPenTool.Models;

namespace ExtendedPenTool.Rendering;

internal sealed class LayerComparer : IEqualityComparer<Layer>
{
    public static readonly LayerComparer Instance = new();

    public bool Equals(Layer? x, Layer? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        return x.Name == y.Name
            && x.IsVisible == y.IsVisible
            && x.IsLocked == y.IsLocked
            && Math.Abs(x.Opacity - y.Opacity) < 1e-9
            && x.SerializableStrokes.SequenceEqual(y.SerializableStrokes);
    }

    public int GetHashCode(Layer obj) =>
        HashCode.Combine(obj.Name, obj.IsVisible, obj.IsLocked, obj.Opacity);
}

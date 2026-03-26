using System.Windows.Ink;

namespace ExtendedPenTool.Rendering;

internal sealed class DrawingAttributesComparer : IEqualityComparer<DrawingAttributes>
{
    public static readonly DrawingAttributesComparer Instance = new();

    public bool Equals(DrawingAttributes? x, DrawingAttributes? y)
    {
        if (x is null && y is null) return true;
        if (x is null || y is null) return false;
        return x.StylusTip == y.StylusTip
            && x.Width == y.Width
            && x.Height == y.Height;
    }

    public int GetHashCode(DrawingAttributes obj) =>
        HashCode.Combine(obj.StylusTip, obj.Width, obj.Height);
}

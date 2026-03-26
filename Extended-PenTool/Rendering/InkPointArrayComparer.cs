using Vortice.Direct2D1;

namespace ExtendedPenTool.Rendering;

internal sealed class InkPointArrayComparer : IEqualityComparer<InkPoint[]>
{
    public static readonly InkPointArrayComparer Instance = new();

    public bool Equals(InkPoint[]? x, InkPoint[]? y)
    {
        if (x is null && y is null) return true;
        if (x is null || y is null) return false;
        return x.AsSpan().SequenceEqual(y);
    }

    public int GetHashCode(InkPoint[] obj)
    {
        var hash = new HashCode();
        foreach (ref readonly var point in obj.AsSpan())
        {
            hash.Add(point);
        }
        return hash.ToHashCode();
    }
}

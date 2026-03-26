using System.Windows.Ink;
using System.Windows.Input;

namespace ExtendedPenTool.Models;

internal sealed record SerializableStroke(SerializableStylusPoint[] StylusPoints, DrawingAttributes DrawingAttributes)
{
    public SerializableStroke() : this([], new DrawingAttributes()) { }

    public SerializableStroke(Stroke stroke)
        : this(
            [.. stroke.StylusPoints.Select(static p => new SerializableStylusPoint(p))],
            stroke.DrawingAttributes.Clone())
    { }

    public Stroke ToStroke() =>
        new(
            new StylusPointCollection(StylusPoints.Select(static p => p.ToStylusPoint())),
            DrawingAttributes.Clone());

    public bool DeepEquals(SerializableStroke other) =>
        StylusPoints.Length == other.StylusPoints.Length
        && StylusPoints.AsSpan().SequenceEqual(other.StylusPoints)
        && DrawingAttributes.Equals(other.DrawingAttributes);
}

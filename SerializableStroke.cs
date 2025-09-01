using System.Linq;
using System.Windows.Ink;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal record SerializableStroke(SerializableStylusPoint[] StylusPoints, DrawingAttributes DrawingAttributes)
    {
        public SerializableStroke() : this([], new DrawingAttributes())
        {
        }

        public SerializableStroke(Stroke stroke) :
            this(
                stroke.StylusPoints.Select(x => new SerializableStylusPoint(x)).ToArray(), stroke.DrawingAttributes.Clone())
        {
        }

        public Stroke ToStroke()
        {
            return new Stroke(
                new(StylusPoints.Select(x => x.ToStylusPoint())),
                DrawingAttributes.Clone());
        }

        public bool DeeqEquals(SerializableStroke other)
        {
            return
                StylusPoints.Length == other.StylusPoints.Length
                && StylusPoints.Zip(other.StylusPoints).All(pair => pair.First == pair.Second)
                && DrawingAttributes.Equals(other.DrawingAttributes);
        }
    }
}
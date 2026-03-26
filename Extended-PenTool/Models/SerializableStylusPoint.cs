using System.Windows.Input;

namespace ExtendedPenTool.Models;

internal readonly record struct SerializableStylusPoint(double X, double Y, float PressureFactor)
{
    public SerializableStylusPoint(StylusPoint point) : this(point.X, point.Y, point.PressureFactor) { }

    public StylusPoint ToStylusPoint() => new(X, Y, PressureFactor);
}

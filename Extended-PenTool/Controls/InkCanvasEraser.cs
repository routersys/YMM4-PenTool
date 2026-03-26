using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;

namespace ExtendedPenTool.Controls;

internal static class InkCanvasEraser
{
    public static readonly DependencyProperty SizeProperty =
        DependencyProperty.RegisterAttached(
            "Size",
            typeof(double),
            typeof(InkCanvasEraser),
            new PropertyMetadata(10.0, OnSizeChanged));

    public static double GetSize(DependencyObject obj) => (double)obj.GetValue(SizeProperty);
    public static void SetSize(DependencyObject obj, double value) => obj.SetValue(SizeProperty, value);

    private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InkCanvas inkCanvas)
        {
            var size = (double)e.NewValue;
            inkCanvas.EraserShape = new EllipseStylusShape(size, size);
        }
    }
}

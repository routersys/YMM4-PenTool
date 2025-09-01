using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class InkCanvasEraser
    {
        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.RegisterAttached(
                "Size",
                typeof(double),
                typeof(InkCanvasEraser),
                new PropertyMetadata(10.0, OnSizeChanged));

        public static double GetSize(DependencyObject obj)
        {
            return (double)obj.GetValue(SizeProperty);
        }

        public static void SetSize(DependencyObject obj, double value)
        {
            obj.SetValue(SizeProperty, value);
        }

        private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is InkCanvas inkCanvas)
            {
                var newSize = (double)e.NewValue;
                var eraserShape = new EllipseStylusShape(newSize, newSize);
                inkCanvas.EraserShape = eraserShape;
            }
        }
    }
}
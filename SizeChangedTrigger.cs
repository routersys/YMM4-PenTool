using Microsoft.Xaml.Behaviors;
using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    public class SizeChangedTrigger : EventTriggerBase<FrameworkElement>
    {
        public static readonly DependencyProperty EventNameProperty =
            DependencyProperty.Register(
                "EventName",
                typeof(string),
                typeof(SizeChangedTrigger),
                new PropertyMetadata("SizeChanged", OnEventNameChanged));

        public SizeChangedTrigger() : base()
        {
        }

        protected override string GetEventName()
        {
            return "SizeChanged";
        }

        private static void OnEventNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
        }
    }
}
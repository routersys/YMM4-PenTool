using Microsoft.Xaml.Behaviors;
using System.Windows;

namespace ExtendedPenTool.Behaviors;

public sealed class SizeChangedTrigger : EventTriggerBase<FrameworkElement>
{
    protected override string GetEventName() => "SizeChanged";
}

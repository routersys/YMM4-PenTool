using ExtendedPenTool.Controls;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;

namespace ExtendedPenTool.Editors;

internal sealed class OpenPenToolButtonAttribute : PropertyEditorAttribute2
{
    public override FrameworkElement Create() => new OpenPenToolButton();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is not OpenPenToolButton editor || itemProperties.Length <= 0) return;
        editor.SetBinding(FrameworkElement.DataContextProperty, new Binding { Source = itemProperties[0] });
    }

    public override void ClearBindings(FrameworkElement control) =>
        BindingOperations.ClearBinding(control, FrameworkElement.DataContextProperty);
}

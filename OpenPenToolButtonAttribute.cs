using System;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class OpenPenToolButtonAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new OpenPenToolButton();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not OpenPenToolButton editor) return;
            if (itemProperties.Length > 0)
            {
                var binding = new Binding
                {
                    Source = itemProperties[0]
                };
                editor.SetBinding(FrameworkElement.DataContextProperty, binding);
            }
        }

        public override void ClearBindings(FrameworkElement control)
        {
            BindingOperations.ClearBinding(control, FrameworkElement.DataContextProperty);
        }
    }
}
using ExtendedPenTool.Controls;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace ExtendedPenTool.Editors;

internal sealed class UpdateCheckPanelEditorAttribute : PropertyEditorAttribute2
{
    public override FrameworkElement Create() => new UpdateCheckPanel();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties) { }

    public override void ClearBindings(FrameworkElement control) { }
}

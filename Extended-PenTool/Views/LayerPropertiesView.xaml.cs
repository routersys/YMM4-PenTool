using System.Windows;

namespace ExtendedPenTool.Views;

public partial class LayerPropertiesView : Window
{
    public LayerPropertiesView() => InitializeComponent();

    private void OkButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

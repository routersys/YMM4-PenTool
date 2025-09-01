using System;
using System.Collections.Immutable;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Community.Shape.Pen.Layer;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    public partial class OpenPenToolButton : UserControl, IPropertyEditorControl2
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        private IEditorInfo? editorInfo;

        public void SetEditorInfo(IEditorInfo info)
        {
            editorInfo = info;
        }

        public OpenPenToolButton()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ItemProperty itemProperty) return;
            if (itemProperty.PropertyOwner is not PenShapeParameter parameter) return;
            if (editorInfo is null) return;

            var layers = itemProperty.GetValue<ImmutableList<Layer.Layer>>();
            if (layers is null) return;

            var viewModel = new PenToolViewModel(parameter, editorInfo, layers);
            var view = new PenToolView()
            {
                Owner = Window.GetWindow(this),
                DataContext = viewModel,
            };

            BeginEdit?.Invoke(this, EventArgs.Empty);
            view.ShowDialog();

            var newLayers = viewModel.Layers
                .Select(layer =>
                {
                    var newLayer = new Layer.Layer(layer.Name)
                    {
                        IsVisible = layer.IsVisible,
                        IsLocked = layer.IsLocked,
                        Opacity = layer.Opacity,
                    };
                    newLayer.SerializableStrokes = layer.Strokes.Select(s => new SerializableStroke(s)).ToList();
                    return newLayer;
                })
                .ToImmutableList();

            if (!layers.SequenceEqual(newLayers))
            {
                itemProperty.SetValue(newLayers);
            }

            viewModel.Dispose();
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }
}
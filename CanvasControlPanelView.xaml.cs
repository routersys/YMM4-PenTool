using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    public partial class CanvasControlPanelView : UserControl
    {
        private readonly TranslateTransform _transform = new();
        private readonly DispatcherTimer _panTimer;
        private Vector _panVelocity;

        public CanvasControlPanelView()
        {
            InitializeComponent();
            JoystickKnob.RenderTransform = _transform;

            _panTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _panTimer.Tick += PanTimer_Tick;
        }

        private void PanTimer_Tick(object? sender, EventArgs e)
        {
            if (DataContext is PenToolViewModel viewModel)
            {
                viewModel.PanCanvas(_panVelocity.X, _panVelocity.Y);
            }
        }

        private void JoystickKnob_DragStarted(object sender, DragStartedEventArgs e)
        {
            _panTimer.Start();
        }

        private void JoystickKnob_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (DataContext is not PenToolViewModel viewModel) return;

            double newX = _transform.X + e.HorizontalChange;
            double newY = _transform.Y + e.VerticalChange;

            double radius = JoystickArea.ActualWidth / 2 - JoystickKnob.ActualWidth / 2;
            var delta = new Vector(newX, newY);

            if (delta.Length > radius)
            {
                delta.Normalize();
                delta *= radius;
            }

            _transform.X = delta.X;
            _transform.Y = delta.Y;

            _panVelocity = new Vector(-delta.X * 0.2, -delta.Y * 0.2);
        }

        private void JoystickKnob_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _transform.X = 0;
            _transform.Y = 0;
            _panVelocity = new Vector(0, 0);
            _panTimer.Stop();
        }
    }
}
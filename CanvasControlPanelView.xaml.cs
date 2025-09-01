using System;
using System.ComponentModel;
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
        private readonly TranslateTransform _joystickTransform;
        private readonly DispatcherTimer _panTimer;
        private Vector _panVelocity;

        private bool _isRotating;
        private Point _rotationCenter;

        private PenToolViewModel? _viewModel;

        public CanvasControlPanelView()
        {
            InitializeComponent();
            _joystickTransform = (TranslateTransform)JoystickKnob.RenderTransform;

            _panTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _panTimer.Tick += PanTimer_Tick;

            DataContextChanged += OnDataContextChanged;

            this.Loaded += (s, e) => {
                if (_viewModel != null)
                {
                    UpdateRotationKnobPosition(_viewModel.CanvasAngle);
                }
            };
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            _viewModel = e.NewValue as PenToolViewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                if (this.IsLoaded)
                {
                    UpdateRotationKnobPosition(_viewModel.CanvasAngle);
                }
            }
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
            double newX = _joystickTransform.X + e.HorizontalChange;
            double newY = _joystickTransform.Y + e.VerticalChange;

            double joystickRadius = 50 - (JoystickKnob.ActualWidth / 2);
            var delta = new Vector(newX, newY);

            if (delta.Length > joystickRadius)
            {
                delta.Normalize();
                delta *= joystickRadius;
            }

            _joystickTransform.X = delta.X;
            _joystickTransform.Y = delta.Y;

            _panVelocity = new Vector(-delta.X * 0.2, -delta.Y * 0.2);
        }

        private void JoystickKnob_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _joystickTransform.X = 0;
            _joystickTransform.Y = 0;
            _panVelocity = new Vector(0, 0);
            _panTimer.Stop();
        }

        private void RotationKnob_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (DataContext is not PenToolViewModel) return;
            _isRotating = true;
            _rotationCenter = new Point(JoystickArea.ActualWidth / 2, JoystickArea.ActualHeight / 2);
            (sender as UIElement)?.CaptureMouse();
        }

        private void RotationKnob_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (!_isRotating || DataContext is not PenToolViewModel viewModel) return;

            var currentPos = Mouse.GetPosition(JoystickArea);
            var vector = currentPos - _rotationCenter;

            if (vector.Length > 0)
            {
                var angle = Vector.AngleBetween(new Vector(0, -1), vector);
                viewModel.RotateCanvas(angle);
            }
        }

        private void UpdateRotationKnobPosition(double angle)
        {
            double outerRadius = (JoystickArea.ActualWidth / 2) - (RotationKnob.ActualWidth / 2) - 2;
            if (outerRadius <= 0) return;

            double angleRad = angle * Math.PI / 180.0;

            double x = (JoystickArea.ActualWidth / 2) + outerRadius * Math.Sin(angleRad) - (RotationKnob.ActualWidth / 2);
            double y = (JoystickArea.ActualHeight / 2) - outerRadius * Math.Cos(angleRad) - (RotationKnob.ActualHeight / 2);

            Canvas.SetLeft(RotationKnob, x);
            Canvas.SetTop(RotationKnob, y);
        }

        private void RotationKnob_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (!_isRotating) return;
            _isRotating = false;
            (sender as UIElement)?.ReleaseMouseCapture();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PenToolViewModel.CanvasAngle) && sender is PenToolViewModel vm)
            {
                UpdateRotationKnobPosition(vm.CanvasAngle);
            }
        }
    }
}
using ExtendedPenTool.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ExtendedPenTool.Views;

public partial class CanvasControlPanelView : UserControl
{
    private readonly TranslateTransform joystickTransform;
    private readonly DispatcherTimer panTimer;
    private Vector panVelocity;
    private bool isRotating;
    private Point rotationCenter;
    private PenToolViewModel? viewModel;

    public CanvasControlPanelView()
    {
        InitializeComponent();
        joystickTransform = (TranslateTransform)JoystickKnob.RenderTransform;

        panTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        panTimer.Tick += PanTimer_Tick;

        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => { viewModel?.Let(vm => UpdateRotationKnobPosition(vm.CanvasAngle)); };
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (viewModel is not null)
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;

        viewModel = e.NewValue as PenToolViewModel;

        if (viewModel is not null)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            if (IsLoaded) UpdateRotationKnobPosition(viewModel.CanvasAngle);
        }
    }

    private void PanTimer_Tick(object? sender, EventArgs e)
    {
        if (DataContext is PenToolViewModel vm)
            vm.PanCanvas(panVelocity.X, panVelocity.Y);
    }

    private void JoystickKnob_DragStarted(object sender, DragStartedEventArgs e) => panTimer.Start();

    private void JoystickKnob_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var newX = joystickTransform.X + e.HorizontalChange;
        var newY = joystickTransform.Y + e.VerticalChange;
        var radius = 50 - JoystickKnob.ActualWidth / 2;
        var delta = new Vector(newX, newY);

        if (delta.Length > radius)
        {
            delta.Normalize();
            delta *= radius;
        }

        joystickTransform.X = delta.X;
        joystickTransform.Y = delta.Y;
        panVelocity = new Vector(-delta.X * 0.2, -delta.Y * 0.2);
    }

    private void JoystickKnob_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        joystickTransform.X = 0;
        joystickTransform.Y = 0;
        panVelocity = default;
        panTimer.Stop();
    }

    private void RotationKnob_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (DataContext is not PenToolViewModel) return;
        isRotating = true;
        rotationCenter = new Point(JoystickArea.ActualWidth / 2, JoystickArea.ActualHeight / 2);
        (sender as UIElement)?.CaptureMouse();
    }

    private void RotationKnob_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!isRotating || DataContext is not PenToolViewModel vm) return;

        var current = Mouse.GetPosition(JoystickArea);
        var vector = current - rotationCenter;

        if (vector.Length > 0)
            vm.RotateCanvas(Vector.AngleBetween(new Vector(0, -1), vector));
    }

    private void RotationKnob_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!isRotating) return;
        isRotating = false;
        (sender as UIElement)?.ReleaseMouseCapture();
    }

    private void UpdateRotationKnobPosition(double angle)
    {
        var outerRadius = JoystickArea.ActualWidth / 2 - RotationKnob.ActualWidth / 2 - 2;
        if (outerRadius <= 0) return;

        var rad = angle * Math.PI / 180.0;
        var x = JoystickArea.ActualWidth / 2 + outerRadius * Math.Sin(rad) - RotationKnob.ActualWidth / 2;
        var y = JoystickArea.ActualHeight / 2 - outerRadius * Math.Cos(rad) - RotationKnob.ActualHeight / 2;

        Canvas.SetLeft(RotationKnob, x);
        Canvas.SetTop(RotationKnob, y);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PenToolViewModel.CanvasAngle) && sender is PenToolViewModel vm)
            UpdateRotationKnobPosition(vm.CanvasAngle);
    }
}

internal static class ObjectExtensions
{
    public static void Let<T>(this T obj, Action<T> action) => action(obj);
}

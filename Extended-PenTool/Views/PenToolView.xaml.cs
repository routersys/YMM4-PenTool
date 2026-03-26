using ExtendedPenTool.Brush;
using ExtendedPenTool.Enums;
using ExtendedPenTool.Models;
using ExtendedPenTool.Settings;
using ExtendedPenTool.Localization;
using ExtendedPenTool.ViewModels;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ExtendedPenTool.Views;

public partial class PenToolView : Window
{
    private const double SnapThreshold = 10.0;
    private const int AlwaysOnTopZIndexBase = 1000;

    private readonly Dictionary<string, ContentControl> panels = [];
    private readonly List<ContentControl> zOrder = [];
    private readonly Dictionary<string, PanelLayout> panelLayouts = [];

    private ContentControl? draggedElement;
    private Point dragOffset;
    private Point dragStartPoint;
    private Point? lastMousePosition;
    private bool isPanning;
    private bool isLayoutInitialized;
    private CustomSelectionAdorner? customAdorner;
    private StrokeCollection? originalLayerStrokes;

    private PenToolViewModel? ViewModel => DataContext as PenToolViewModel;

    public PenToolView()
    {
        InitializeComponent();
        ContentRendered += OnContentRendered;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0024)
        {
            var mmi = Marshal.PtrToStructure<NativeMethods.MINMAXINFO>(lParam);
            var monitor = NativeMethods.MonitorFromWindow(hwnd, 0x00000002);
            if (monitor != IntPtr.Zero)
            {
                var mi = new NativeMethods.MONITORINFO();
                NativeMethods.GetMonitorInfo(monitor, mi);
                mmi.ptMaxPosition.x = Math.Abs(mi.rcWork.left - mi.rcMonitor.left);
                mmi.ptMaxPosition.y = Math.Abs(mi.rcWork.top - mi.rcMonitor.top);
                mmi.ptMaxSize.x = Math.Abs(mi.rcWork.right - mi.rcWork.left);
                mmi.ptMaxSize.y = Math.Abs(mi.rcWork.bottom - mi.rcWork.top);
            }
            Marshal.StructureToPtr(mmi, lParam, true);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnContentRendered;
        ViewModel?.Render();
        InitializePanels();
        SizeChanged += OnSizeChanged;

        if (ViewModel is not null)
            ViewModel.FitToViewAction = FitCanvasToView;

        PenSettings.Default.EraserStyle.PropertyChanged += EraserStyle_PropertyChanged;
        UpdateEraserShape();
        inkCanvas.SelectionMoving += InkCanvas_SelectionMoving;
        inkCanvas.SelectionMoved += InkCanvas_SelectionMoved;
        inkCanvas.SelectionResizing += InkCanvas_SelectionResizing;
        inkCanvas.SelectionResized += InkCanvas_SelectionResized;
        inkCanvas.Strokes.StrokesChanged += Strokes_StrokesChanged;

        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            FitCanvasToView();
            WindowState = WindowState.Maximized;
        });
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed) return;

        if (WindowState == WindowState.Maximized)
        {
            var point = PointToScreen(e.GetPosition(this));
            WindowState = WindowState.Normal;
            Left = point.X - ActualWidth / 2;
            Top = point.Y - e.GetPosition(this).Y;
        }
        DragMove();
        SnapToMonitor();
    }

    private void SnapToMonitor()
    {
        var windowRect = new Rect(Left, Top, Width, Height);
        var monitors = NativeMethods.GetMonitorWorkAreas();
        var bestMonitor = new Rect();
        double maxIntersection = 0;

        foreach (var monitorRect in monitors)
        {
            var intersection = Rect.Intersect(windowRect, monitorRect);
            if (intersection.IsEmpty) continue;
            var area = intersection.Width * intersection.Height;
            if (area > maxIntersection) { maxIntersection = area; bestMonitor = monitorRect; }
        }

        if (maxIntersection > 0 && maxIntersection / (windowRect.Width * windowRect.Height) >= 0.51)
        {
            Left = bestMonitor.Left;
            Top = bestMonitor.Top;
            WindowState = WindowState.Maximized;
        }
    }

    private void ProtectedArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void FitCanvasToView()
    {
        if (ViewModel is null || CanvasViewport.ActualWidth <= 0 || CanvasViewport.ActualHeight <= 0) return;
        var cs = ViewModel.CanvasSize;
        if (cs.Width <= 0 || cs.Height <= 0) return;

        var scale = Math.Clamp(Math.Min(CanvasViewport.ActualWidth / cs.Width, CanvasViewport.ActualHeight / cs.Height), 0.1, 10.0);
        ViewModel.CanvasScale = scale;
        ViewModel.CanvasAngle = 0;
        ViewModel.CanvasTranslateX = (CanvasViewport.ActualWidth - cs.Width) / 2;
        ViewModel.CanvasTranslateY = (CanvasViewport.ActualHeight - cs.Height) / 2;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    protected override void OnClosing(CancelEventArgs e)
    {
        SaveLayout();
        ViewModel?.SaveLayout(PenSettings.Default.Layout);
        base.OnClosing(e);
        SizeChanged -= OnSizeChanged;
        PenSettings.Default.EraserStyle.PropertyChanged -= EraserStyle_PropertyChanged;
        inkCanvas.SelectionMoving -= InkCanvas_SelectionMoving;
        inkCanvas.SelectionMoved -= InkCanvas_SelectionMoved;
        inkCanvas.SelectionResizing -= InkCanvas_SelectionResizing;
        inkCanvas.SelectionResized -= InkCanvas_SelectionResized;
        inkCanvas.Strokes.StrokesChanged -= Strokes_StrokesChanged;
    }

    private void InitializePanels()
    {
        panels["CanvasPanel"] = CanvasPanel;
        panels["LayersPanel"] = LayersPanel;
        panels["CanvasControlPanel"] = CanvasControlPanel;
        panels["HistoryPanel"] = HistoryPanel;

        foreach (var panel in panels.Values)
        {
            zOrder.Add(panel);
            panel.ApplyTemplate();
            if (panel.Template.FindName("Header", panel) is FrameworkElement header)
            {
                header.PreviewMouseLeftButtonDown += Header_PreviewMouseLeftButtonDown;
                header.PreviewMouseRightButtonUp += Header_PreviewMouseRightButtonUp;
            }

            string[] thumbNames = ["ResizeTop", "ResizeBottom", "ResizeLeft", "ResizeRight", "ResizeTopLeft", "ResizeTopRight", "ResizeBottomLeft", "ResizeBottomRight"];
            foreach (var name in thumbNames)
            {
                if (panel.Template.FindName(name, panel) is Thumb thumb)
                {
                    thumb.DragDelta += ResizeThumb_DragDelta;
                    thumb.DragStarted += ResizeThumb_DragStarted;
                    thumb.DragCompleted += ResizeThumb_DragCompleted;
                }
            }

            panel.PreviewMouseLeftButtonDown += Panel_PreviewMouseLeftButtonDown;
            panel.MouseEnter += Panel_MouseEnter;
            panel.MouseLeave += Panel_MouseLeave;
        }

        MainCanvas.PreviewMouseMove += MainCanvas_PreviewMouseMove;
        MainCanvas.PreviewMouseLeftButtonUp += MainCanvas_PreviewMouseLeftButtonUp;
        LoadLayout();
    }

    private void Panel_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is ContentControl p && PenSettings.Default.Layout.TryGetValue(p.Name, out var info) && info.IsTranslucent)
            p.Opacity = 0.7;
    }

    private void Panel_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is ContentControl p && PenSettings.Default.Layout.TryGetValue(p.Name, out var info) && info.IsTranslucent)
            p.Opacity = 0.5;
    }

    private void Panel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ContentControl p) BringToFront(p);
    }

    private void LoadLayout()
    {
        var layout = PenSettings.Default.Layout;
        var needsDefault = layout.Count == 0 || layout.Count != panels.Count;

        zOrder.Clear();
        zOrder.AddRange(panels.Values.OrderBy(p => layout.TryGetValue(p.Name, out var i) ? i.ZIndex : 0));

        foreach (var (name, panel) in panels)
        {
            if (!needsDefault && layout.TryGetValue(name, out var info))
            {
                Canvas.SetLeft(panel, info.X);
                Canvas.SetTop(panel, info.Y);
                panel.Width = info.Width;
                panel.Height = info.Height;
                panel.Opacity = info.IsTranslucent ? 0.5 : 1.0;
            }
            else
            {
                SetDefaultPanelLayout(name, panel);
            }
        }
        NormalizeZIndices();
    }

    private void SetDefaultPanelLayout(string name, ContentControl panel)
    {
        switch (name)
        {
            case "CanvasPanel":
                panel.Width = Math.Max(200, MainCanvas.ActualWidth - 280);
                panel.Height = Math.Max(150, MainCanvas.ActualHeight - 20);
                Canvas.SetLeft(panel, 10); Canvas.SetTop(panel, 10);
                break;
            case "LayersPanel":
                panel.Width = 250; panel.Height = Math.Max(200, MainCanvas.ActualHeight - 20);
                Canvas.SetLeft(panel, Math.Max(220, MainCanvas.ActualWidth - 260)); Canvas.SetTop(panel, 10);
                break;
            case "CanvasControlPanel":
                panel.Width = 200; panel.Height = 240;
                Canvas.SetLeft(panel, Math.Max(220, MainCanvas.ActualWidth - 480)); Canvas.SetTop(panel, MainCanvas.ActualHeight - 250);
                break;
            case "HistoryPanel":
                panel.Width = 200; panel.Height = 300;
                Canvas.SetLeft(panel, 20); Canvas.SetTop(panel, MainCanvas.ActualHeight - 320);
                break;
        }
    }

    private void SaveLayout()
    {
        var layout = PenSettings.Default.Layout;
        foreach (var (name, panel) in panels)
        {
            if (!layout.ContainsKey(name)) layout[name] = new PanelLayoutInfo();
            var info = layout[name];
            info.X = Canvas.GetLeft(panel); info.Y = Canvas.GetTop(panel);
            info.Width = panel.ActualWidth; info.Height = panel.ActualHeight;
            info.ZIndex = Panel.GetZIndex(panel);
            info.IsVisible = panel.Visibility == Visibility.Visible;
        }
    }

    private void SaveLayoutImmediate()
    {
        SaveLayout();
        ViewModel?.SaveLayout(PenSettings.Default.Layout);
    }

    private void NormalizeZIndices()
    {
        var layout = PenSettings.Default.Layout;
        var normal = zOrder.Where(p => !layout.TryGetValue(p.Name, out var i) || !i.IsAlwaysOnTop).ToList();
        var onTop = zOrder.Where(p => layout.TryGetValue(p.Name, out var i) && i.IsAlwaysOnTop).ToList();
        for (var i = 0; i < normal.Count; i++) Panel.SetZIndex(normal[i], i);
        for (var i = 0; i < onTop.Count; i++) Panel.SetZIndex(onTop[i], AlwaysOnTopZIndexBase + i);
    }

    private void BringToFront(ContentControl panel)
    {
        var layout = PenSettings.Default.Layout;
        if (layout.TryGetValue(panel.Name, out var info) && info.IsAlwaysOnTop) return;

        zOrder.Remove(panel);
        var topCount = zOrder.Count(p => layout.TryGetValue(p.Name, out var i) && i.IsAlwaysOnTop);
        zOrder.Insert(zOrder.Count - topCount, panel);
        NormalizeZIndices();
    }

    private void Header_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { TemplatedParent: ContentControl panel })
        {
            draggedElement = panel;
            dragOffset = e.GetPosition(draggedElement);
            BringToFront(panel);
            draggedElement.CaptureMouse();
            e.Handled = true;
        }
    }

    private void MainCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (draggedElement is null) return;

        var pos = e.GetPosition(MainCanvas);
        var (sx, sy) = SnapPosition(
            new Rect(pos.X - dragOffset.X, pos.Y - dragOffset.Y, draggedElement.ActualWidth, draggedElement.ActualHeight),
            GetSnapTargets(draggedElement));

        Canvas.SetLeft(draggedElement, Math.Clamp(sx, 0, MainCanvas.ActualWidth - draggedElement.ActualWidth));
        Canvas.SetTop(draggedElement, Math.Clamp(sy, 0, MainCanvas.ActualHeight - draggedElement.ActualHeight));
    }

    private void MainCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (draggedElement is null) return;
        draggedElement.ReleaseMouseCapture();
        draggedElement = null;
        CapturePanelLayouts();
        SaveLayoutImmediate();
    }

    private List<Rect> GetSnapTargets(ContentControl exclude) =>
    [
        .. panels.Values.Where(p => p != exclude && p.Visibility == Visibility.Visible)
            .Select(p => new Rect(Canvas.GetLeft(p), Canvas.GetTop(p), p.ActualWidth, p.ActualHeight)),
        new(0, 0, MainCanvas.ActualWidth, MainCanvas.ActualHeight),
    ];

    private static (double X, double Y) SnapPosition(Rect source, List<Rect> targets)
    {
        double sx = source.X, sy = source.Y, minX = SnapThreshold, minY = SnapThreshold;
        foreach (var t in targets)
        {
            TrySnap(source.Left, t.Left, ref minX, ref sx);
            TrySnap(source.Left, t.Right, ref minX, ref sx);
            TrySnap(source.Right, t.Left, ref minX, ref sx, source.Width);
            TrySnap(source.Right, t.Right, ref minX, ref sx, source.Width);
            TrySnap(source.Top, t.Top, ref minY, ref sy);
            TrySnap(source.Top, t.Bottom, ref minY, ref sy);
            TrySnap(source.Bottom, t.Top, ref minY, ref sy, source.Height);
            TrySnap(source.Bottom, t.Bottom, ref minY, ref sy, source.Height);
        }
        return (sx, sy);
    }

    private static void TrySnap(double edge, double target, ref double minDist, ref double snapped, double offset = 0)
    {
        var d = Math.Abs(edge - target);
        if (d < minDist) { minDist = d; snapped = target - offset; }
    }

    private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (sender is Thumb { TemplatedParent: ContentControl p }) BringToFront(p);
    }

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        CapturePanelLayouts();
        SaveLayoutImmediate();
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is not Thumb thumb || thumb.TemplatedParent is not ContentControl panel) return;
        var targets = GetSnapTargets(panel);
        double top = Canvas.GetTop(panel), left = Canvas.GetLeft(panel), w = panel.Width, h = panel.Height;
        double pTop = top, pLeft = left, pRight = left + w, pBottom = top + h;

        if (thumb.Name.Contains("Top")) pTop += e.VerticalChange;
        if (thumb.Name.Contains("Bottom")) pBottom += e.VerticalChange;
        if (thumb.Name.Contains("Left")) pLeft += e.HorizontalChange;
        if (thumb.Name.Contains("Right")) pRight += e.HorizontalChange;

        foreach (var t in targets)
        {
            if (thumb.Name.Contains("Top")) SnapEdge(ref pTop, t.Top, t.Bottom);
            if (thumb.Name.Contains("Bottom")) SnapEdge(ref pBottom, t.Top, t.Bottom);
            if (thumb.Name.Contains("Left")) SnapEdge(ref pLeft, t.Left, t.Right);
            if (thumb.Name.Contains("Right")) SnapEdge(ref pRight, t.Left, t.Right);
        }

        if (thumb.Name.Contains("Top")) { h = top + h - pTop; top = pTop; }
        if (thumb.Name.Contains("Bottom")) h = pBottom - top;
        if (thumb.Name.Contains("Left")) { w = left + w - pLeft; left = pLeft; }
        if (thumb.Name.Contains("Right")) w = pRight - left;

        if (h < panel.MinHeight) { if (thumb.Name.Contains("Top")) top = top + h - panel.MinHeight; h = panel.MinHeight; }
        if (w < panel.MinWidth) { if (thumb.Name.Contains("Left")) left = left + w - panel.MinWidth; w = panel.MinWidth; }
        left = Math.Max(0, left); top = Math.Max(0, top);
        if (left + w > MainCanvas.ActualWidth) w = MainCanvas.ActualWidth - left;
        if (top + h > MainCanvas.ActualHeight) h = MainCanvas.ActualHeight - top;

        if (w >= panel.MinWidth) { panel.Width = w; Canvas.SetLeft(panel, left); }
        if (h >= panel.MinHeight) { panel.Height = h; Canvas.SetTop(panel, top); }
    }

    private static void SnapEdge(ref double edge, params double[] targets)
    {
        double min = SnapThreshold;
        foreach (var t in targets)
        {
            var d = Math.Abs(edge - t);
            if (d < min) { min = d; edge = t; }
        }
    }

    private void CapturePanelLayouts()
    {
        if (MainCanvas.ActualWidth == 0 || MainCanvas.ActualHeight == 0) return;
        foreach (var (name, panel) in panels)
        {
            panelLayouts[name] = new PanelLayout
            {
                LeftRatio = Canvas.GetLeft(panel) / MainCanvas.ActualWidth,
                TopRatio = Canvas.GetTop(panel) / MainCanvas.ActualHeight,
                WidthRatio = panel.ActualWidth / MainCanvas.ActualWidth,
                HeightRatio = panel.ActualHeight / MainCanvas.ActualHeight,
            };
        }
    }

    private void ApplyPanelLayouts()
    {
        if (!isLayoutInitialized || MainCanvas.ActualWidth == 0 || MainCanvas.ActualHeight == 0) return;
        foreach (var (name, panel) in panels)
        {
            if (!panelLayouts.TryGetValue(name, out var layout)) continue;
            var w = Math.Max(panel.MinWidth, layout.WidthRatio * MainCanvas.ActualWidth);
            var h = Math.Max(panel.MinHeight, layout.HeightRatio * MainCanvas.ActualHeight);
            var l = Math.Max(0, Math.Min(layout.LeftRatio * MainCanvas.ActualWidth, MainCanvas.ActualWidth - w));
            var t = Math.Max(0, Math.Min(layout.TopRatio * MainCanvas.ActualHeight, MainCanvas.ActualHeight - h));
            panel.Width = w; panel.Height = h;
            Canvas.SetLeft(panel, l); Canvas.SetTop(panel, t);
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!isLayoutInitialized) { CapturePanelLayouts(); isLayoutInitialized = true; }
        else ApplyPanelLayouts();
    }

    private void Header_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement header || header.TemplatedParent is not ContentControl panel || ViewModel is null) return;
        var ctx = new ContextMenu { PlacementTarget = header };

        var close = new MenuItem { Header = Texts.Close };
        close.Click += (_, _) =>
        {
            switch (panel.Name)
            {
                case "CanvasPanel": ViewModel.IsCanvasVisible = false; break;
                case "LayersPanel": ViewModel.IsLayersVisible = false; break;
                case "CanvasControlPanel": ViewModel.IsCanvasControlPanelVisible = false; break;
                case "HistoryPanel": ViewModel.IsHistoryVisible = false; break;
            }
        };
        ctx.Items.Add(close);

        if (panel.Name != "CanvasPanel")
        {
            var layout = PenSettings.Default.Layout;
            if (!layout.ContainsKey(panel.Name)) layout[panel.Name] = new PanelLayoutInfo();
            var info = layout[panel.Name];

            var pinItem = new MenuItem { Header = Texts.AlwaysOnTop, IsCheckable = true, IsChecked = info.IsAlwaysOnTop };
            pinItem.Click += (_, _) => { info.IsAlwaysOnTop = !info.IsAlwaysOnTop; NormalizeZIndices(); };
            ctx.Items.Add(pinItem);

            var opItem = new MenuItem { Header = Texts.Translucent, IsCheckable = true, IsChecked = info.IsTranslucent };
            opItem.Click += (_, _) => { if (opItem is MenuItem mi) { info.IsTranslucent = mi.IsChecked; panel.Opacity = mi.IsChecked ? 0.5 : 1.0; } };
            ctx.Items.Add(opItem);
        }

        ctx.IsOpen = true;
        e.Handled = true;
    }

    private void CanvasViewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel is null) return;
        switch (PenSettings.Default.MouseWheelAction)
        {
            case Enums.MouseWheelAction.Zoom: HandleZoom(sender, e); break;
            case Enums.MouseWheelAction.PenSize: ViewModel.StrokeThickness += e.Delta > 0 ? 1 : -1; UpdatePenPreview(e); break;
        }
    }

    private void HandleZoom(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel is null || sender is not FrameworkElement viewport) return;
        var pos = e.GetPosition(viewport);
        var cs = ViewModel.CanvasSize;
        var center = new Point(cs.Width / 2, cs.Height / 2);

        var m = new Matrix();
        m.Translate(-center.X, -center.Y); m.Scale(ViewModel.CanvasScale, ViewModel.CanvasScale);
        m.Rotate(ViewModel.CanvasAngle); m.Translate(center.X, center.Y);
        m.Translate(ViewModel.CanvasTranslateX, ViewModel.CanvasTranslateY);
        m.Invert();
        var unPos = m.Transform(pos);

        var newScale = Math.Clamp(ViewModel.CanvasScale * (e.Delta > 0 ? 1.2 : 1.0 / 1.2), 0.1, 10.0);
        if (Math.Abs(newScale - ViewModel.CanvasScale) < 0.001) return;

        var nm = new Matrix();
        nm.Translate(-center.X, -center.Y); nm.Scale(newScale, newScale);
        nm.Rotate(ViewModel.CanvasAngle); nm.Translate(center.X, center.Y);
        var newPos = nm.Transform(unPos);

        ViewModel.CanvasScale = newScale;
        ViewModel.CanvasTranslateX = pos.X - newPos.X;
        ViewModel.CanvasTranslateY = pos.Y - newPos.Y;
        UpdatePenPreview(e);
        e.Handled = true;
    }

    private void CanvasViewport_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton != MouseButtonState.Pressed || sender is not FrameworkElement vp) return;
        lastMousePosition = e.GetPosition(vp);
        vp.CaptureMouse(); isPanning = true;
        Mouse.OverrideCursor = Cursors.ScrollAll;
        e.Handled = true;
    }

    private void CanvasViewport_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (isPanning && ViewModel is not null && lastMousePosition.HasValue && sender is FrameworkElement vp)
        {
            var cur = e.GetPosition(vp);
            var d = cur - lastMousePosition.Value;
            lastMousePosition = cur;
            ViewModel.CanvasTranslateX += d.X;
            ViewModel.CanvasTranslateY += d.Y;
            e.Handled = true;
        }
        UpdatePenPreview(e);
    }

    private void CanvasViewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || !isPanning || sender is not FrameworkElement vp) return;
        vp.ReleaseMouseCapture(); isPanning = false; lastMousePosition = null;
        Mouse.OverrideCursor = null; e.Handled = true;
    }

    private void CanvasViewport_MouseEnter(object sender, MouseEventArgs e) => UpdatePenPreview(e);
    private void CanvasViewport_MouseLeave(object sender, MouseEventArgs e) { PenPreview.Visibility = Visibility.Collapsed; PenPreviewRectangle.Visibility = Visibility.Collapsed; }

    private void UpdatePenPreview(MouseEventArgs e)
    {
        if (ViewModel is null || inkCanvas.EditingMode is InkCanvasEditingMode.None or InkCanvasEditingMode.Select || Mouse.LeftButton == MouseButtonState.Pressed)
        {
            PenPreview.Visibility = Visibility.Collapsed; PenPreviewRectangle.Visibility = Visibility.Collapsed; return;
        }

        double w = 0, h = 0;
        var isHighlighter = ViewModel.SelectedBrushType == BrushType.Highlighter;

        if (inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
        {
            w = h = ViewModel.StrokeThickness * ViewModel.CanvasScale;
            if (isHighlighter) w /= 2;
            var c = ViewModel.StrokeColor;
            PenPreview.Fill = PenPreviewRectangle.Fill = new SolidColorBrush(Color.FromArgb(128, c.R, c.G, c.B));
        }
        else
        {
            w = h = PenSettings.Default.EraserStyle.StrokeThickness * ViewModel.CanvasScale;
            PenPreview.Fill = PenPreviewRectangle.Fill = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255));
        }

        if (w <= 0 || h <= 0) { PenPreview.Visibility = Visibility.Collapsed; PenPreviewRectangle.Visibility = Visibility.Collapsed; return; }

        var pos = e.GetPosition(CanvasViewport);
        if (isHighlighter)
        {
            PenPreview.Visibility = Visibility.Collapsed; PenPreviewRectangle.Visibility = Visibility.Visible;
            PenPreviewRectangle.Width = w; PenPreviewRectangle.Height = h;
            Canvas.SetLeft(PenPreviewRectangle, pos.X - w / 2); Canvas.SetTop(PenPreviewRectangle, pos.Y - h / 2);
        }
        else
        {
            PenPreviewRectangle.Visibility = Visibility.Collapsed; PenPreview.Visibility = Visibility.Visible;
            PenPreview.Width = w; PenPreview.Height = h;
            Canvas.SetLeft(PenPreview, pos.X - w / 2); Canvas.SetTop(PenPreview, pos.Y - h / 2);
        }
    }

    private void InkCanvas_SelectionChanged(object sender, EventArgs e)
    {
        if (sender is not InkCanvas ic || ViewModel is null) return;
        var adornerLayer = AdornerLayer.GetAdornerLayer(ic);
        if (adornerLayer is null) return;

        if (customAdorner is not null) { adornerLayer.Remove(customAdorner); customAdorner = null; }

        var selected = ic.GetSelectedStrokes();
        if (selected.Count <= 0) return;

        ViewModel.FilterSelection(selected, s => ic.Select(s));
        selected = ic.GetSelectedStrokes();

        if (selected.Count > 0)
        {
            customAdorner = new CustomSelectionAdorner(ic, ViewModel) { Style = (Style)FindResource("RotationThumbStyle") };
            adornerLayer.Add(customAdorner);
        }
    }

    private void EraserStyle_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EraserBrushSettings.StrokeThickness)) UpdateEraserShape();
    }

    private void UpdateEraserShape()
    {
        var s = PenSettings.Default.EraserStyle.StrokeThickness;
        inkCanvas.EraserShape = new EllipseStylusShape(s, s);
    }

    private void Strokes_StrokesChanged(object sender, StrokeCollectionChangedEventArgs e) => customAdorner?.UpdateSelection();

    private void InkCanvas_SelectionMoving(object sender, InkCanvasSelectionEditingEventArgs e)
    {
        if (originalLayerStrokes is null && ViewModel is not null)
            originalLayerStrokes = ViewModel.GetOriginalStrokes(inkCanvas.GetSelectedStrokes());
    }

    private void InkCanvas_SelectionMoved(object? sender, EventArgs e)
    {
        if (originalLayerStrokes is { Count: > 0 } && ViewModel is not null)
        {
            ViewModel.AddTransformUndo(originalLayerStrokes, inkCanvas.GetSelectedStrokes().Clone(), HistoryKind.MoveStrokes);
            inkCanvas.Select(new StrokeCollection());
        }
        originalLayerStrokes = null;
    }

    private void InkCanvas_SelectionResizing(object sender, InkCanvasSelectionEditingEventArgs e)
    {
        if (originalLayerStrokes is null && ViewModel is not null)
            originalLayerStrokes = ViewModel.GetOriginalStrokes(inkCanvas.GetSelectedStrokes());
    }

    private void InkCanvas_SelectionResized(object? sender, EventArgs e)
    {
        if (originalLayerStrokes is { Count: > 0 } && ViewModel is not null)
        {
            ViewModel.AddTransformUndo(originalLayerStrokes, inkCanvas.GetSelectedStrokes().Clone(), HistoryKind.ResizeStrokes);
            inkCanvas.Select(new StrokeCollection());
        }
        originalLayerStrokes = null;
    }

    private void ListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var checkBox = FindAncestor<CheckBox>(e.OriginalSource as DependencyObject);
        if (checkBox is not null)
        {
            if (checkBox.DataContext is Layer layer && ViewModel is not null)
                ViewModel.ToggleLayerVisibility(layer);
            e.Handled = true;
            return;
        }
        dragStartPoint = e.GetPosition(null);
        if (sender is FrameworkElement { DataContext: Layer l } && ViewModel is not null)
            ViewModel.SelectedLayer = l;
    }

    private void ListViewItem_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.OriginalSource is TextBox { IsReadOnly: false }) return;
        var diff = dragStartPoint - e.GetPosition(null);
        if (e.LeftButton != MouseButtonState.Pressed || (Math.Abs(diff.X) <= SystemParameters.MinimumHorizontalDragDistance && Math.Abs(diff.Y) <= SystemParameters.MinimumVerticalDragDistance)) return;
        if (sender is not Grid { DataContext: Layer layer }) return;
        var item = FindAncestor<ListViewItem>(sender as DependencyObject);
        if (item is null) return;
        DragDrop.DoDragDrop(item, new DataObject("Layer", layer), DragDropEffects.Move);
    }

    private void ListViewItem_Drop(object sender, DragEventArgs e)
    {
        if (sender is not ListViewItem targetItem || !e.Data.GetDataPresent("Layer")) return;
        FindChild<Border>(targetItem, "DropIndicator")?.Let(b => b.Visibility = Visibility.Collapsed);
        if (targetItem.DataContext is Layer tl && e.Data.GetData("Layer") is Layer sl && !ReferenceEquals(sl, tl) && ViewModel is not null)
            ViewModel.MoveLayer(ViewModel.Layers.IndexOf(sl), ViewModel.Layers.IndexOf(tl));
    }

    private void ListViewItem_DragOver(object sender, DragEventArgs e) { if (sender is ListViewItem i) FindChild<Border>(i, "DropIndicator")?.Let(b => b.Visibility = Visibility.Visible); }
    private void ListViewItem_DragLeave(object sender, DragEventArgs e) { if (sender is ListViewItem i) FindChild<Border>(i, "DropIndicator")?.Let(b => b.Visibility = Visibility.Collapsed); }

    private void LayerNameTextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) { if (sender is TextBox tb) { tb.IsReadOnly = false; tb.SelectAll(); tb.Focus(); e.Handled = true; } }
    private void LayerNameTextBox_LostFocus(object sender, RoutedEventArgs e) { if (sender is TextBox tb) { tb.IsReadOnly = true; FindAncestor<ListViewItem>(tb)?.Focus(); } }
    private void LayerNameTextBox_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && sender is TextBox tb) { tb.IsReadOnly = true; FindAncestor<ListViewItem>(tb)?.Focus(); } }


    private static T? FindAncestor<T>(DependencyObject? cur) where T : DependencyObject
    {
        while (cur is not null) { if (cur is T t) return t; cur = VisualTreeHelper.GetParent(cur); }
        return null;
    }

    private static T? FindChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T el && el.Name == name) return el;
            var r = FindChild<T>(child, name);
            if (r is not null) return r;
        }
        return null;
    }

    private sealed class PanelLayout
    {
        public double LeftRatio { get; init; }
        public double TopRatio { get; init; }
        public double WidthRatio { get; init; }
        public double HeightRatio { get; init; }
    }

    internal sealed class CustomSelectionAdorner : Adorner
    {
        private readonly InkCanvas inkCanvas;
        private readonly PenToolViewModel viewModel;
        private readonly Thumb rotationThumb;
        private readonly VisualCollection visuals;
        private Rect selectionRect;
        private Point center;
        private double initialAngle;
        private StrokeCollection? origStrokes;

        public CustomSelectionAdorner(InkCanvas canvas, PenToolViewModel vm) : base(canvas)
        {
            inkCanvas = canvas; viewModel = vm;
            visuals = new VisualCollection(this);
            rotationThumb = new Thumb();
            rotationThumb.DragStarted += OnDragStarted;
            rotationThumb.DragDelta += OnDragDelta;
            rotationThumb.DragCompleted += OnDragCompleted;
            visuals.Add(rotationThumb);
            UpdateSelection();
        }

        public new Style Style { get => rotationThumb.Style; set => rotationThumb.Style = value; }

        private void OnDragStarted(object sender, DragStartedEventArgs e)
        {
            var start = Mouse.GetPosition(inkCanvas);
            initialAngle = Vector.AngleBetween(new Vector(0, -1), start - center);
            origStrokes = viewModel.GetOriginalStrokes(inkCanvas.GetSelectedStrokes());
        }

        private void OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            var cur = Mouse.GetPosition(inkCanvas);
            var angle = Vector.AngleBetween(new Vector(0, -1), cur - center);
            var m = new Matrix();
            m.RotateAt(angle - initialAngle, center.X, center.Y);
            inkCanvas.GetSelectedStrokes().Transform(m, false);
            initialAngle = angle;
            UpdateSelection();
        }

        private void OnDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (origStrokes is { Count: > 0 })
            {
                viewModel.AddTransformUndo(origStrokes, inkCanvas.GetSelectedStrokes().Clone(), HistoryKind.RotateStrokes);
                inkCanvas.Select(new StrokeCollection());
            }
            origStrokes = null;
        }

        public void UpdateSelection()
        {
            var s = inkCanvas.GetSelectedStrokes();
            if (s.Count == 0) { Visibility = Visibility.Collapsed; return; }
            Visibility = Visibility.Visible;
            selectionRect = s.GetBounds();
            center = new Point(selectionRect.Left + selectionRect.Width / 2, selectionRect.Top + selectionRect.Height / 2);
            InvalidateVisual();
        }

        protected override int VisualChildrenCount => visuals.Count;
        protected override Visual GetVisualChild(int index) => visuals[index];

        protected override Size ArrangeOverride(Size finalSize)
        {
            UpdateSelection();
            var sz = new Size(20, 20);
            rotationThumb.Arrange(new Rect(new Point(selectionRect.Left + (selectionRect.Width - sz.Width) / 2, selectionRect.Top - sz.Height - 5), sz));
            return finalSize;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var bounds = inkCanvas.GetSelectedStrokes().GetBounds();
            if (bounds.IsEmpty) return;
            var pen = new System.Windows.Media.Pen(new SolidColorBrush(GetContrastColor(bounds)), 1) { DashStyle = new DashStyle([4, 4], 0) };
            dc.DrawRectangle(Brushes.Transparent, pen, bounds);
        }

        private Color GetContrastColor(Rect bounds)
        {
            try
            {
                var rtb = new RenderTargetBitmap((int)viewModel.CanvasSize.Width, (int)viewModel.CanvasSize.Height, 96, 96, PixelFormats.Pbgra32);
                var v = new DrawingVisual();
                using (var ctx = v.RenderOpen())
                {
                    ctx.DrawImage(viewModel.Bitmap, new Rect(0, 0, viewModel.CanvasSize.Width, viewModel.CanvasSize.Height));
                    new StrokeCollection(inkCanvas.Strokes.Except(inkCanvas.GetSelectedStrokes())).Draw(ctx);
                }
                rtb.Render(v);
                if (bounds.Width < 1 || bounds.Height < 1) return Colors.Black;
                var crop = new CroppedBitmap(rtb, new Int32Rect((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height));
                var stride = crop.PixelWidth * (crop.Format.BitsPerPixel / 8);
                var pixels = new byte[crop.PixelHeight * stride];
                crop.CopyPixels(pixels, stride, 0);
                long r = 0, g = 0, b = 0;
                for (var i = 0; i < pixels.Length; i += 4) { b += pixels[i]; g += pixels[i + 1]; r += pixels[i + 2]; }
                var cnt = pixels.Length / 4;
                var brightness = (0.299 * (r / cnt) + 0.587 * (g / cnt) + 0.114 * (b / cnt)) / 255;
                return brightness > 0.5 ? Colors.Black : Colors.White;
            }
            catch { return Colors.Black; }
        }
    }
}

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] public struct MINMAXINFO { public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] public class MONITORINFO { public int cbSize = Marshal.SizeOf(typeof(MONITORINFO)); public RECT rcMonitor, rcWork; public int dwFlags; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int left, top, right, bottom; }

    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);
    delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    public static List<Rect> GetMonitorWorkAreas()
    {
        var monitors = new List<Rect>();
        MonitorEnumDelegate cb = (IntPtr hMon, IntPtr hdc, ref RECT lp, IntPtr data) =>
        {
            var mi = new MONITORINFO();
            if (GetMonitorInfo(hMon, mi)) { var r = mi.rcWork; monitors.Add(new Rect(r.left, r.top, r.right - r.left, r.bottom - r.top)); }
            return true;
        };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero);
        return monitors;
    }
}

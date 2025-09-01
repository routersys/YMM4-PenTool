using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using YukkuriMovieMaker.Plugin.Community.Shape.Pen.Brush;
using YukkuriMovieMaker.Plugin.Community.Shape.Pen.Layer;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    public partial class PenToolView : Window
    {
        #region Win32 API
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor = new RECT();
            public RECT rcWork = new RECT();
            public int dwFlags = 0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll")]
        static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;
        #endregion

        private class PanelLayout
        {
            public double LeftRatio { get; set; }
            public double TopRatio { get; set; }
            public double WidthRatio { get; set; }
            public double HeightRatio { get; set; }
        }

        private const double SnapThreshold = 10.0;
        private const int AlwaysOnTopZIndexBase = 1000;
        private readonly Dictionary<string, ContentControl> _panels = new();
        private readonly List<ContentControl> _zOrder = new();
        private ContentControl? _draggedElement;
        private Point _dragOffset;
        private PenToolViewModel? ViewModel => DataContext as PenToolViewModel;

        private Point? _lastMousePositionOnViewport;
        private bool _isPanning;

        private readonly Dictionary<string, PanelLayout> _panelLayouts = new();
        private bool _isLayoutInitialized = false;

        private Point _dragStartPoint;

        public PenToolView()
        {
            InitializeComponent();
            ContentRendered += PenToolView_ContentRendered;
            SourceInitialized += OnSourceInitialized;
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            var source = HwndSource.FromHwnd(helper.Handle);
            if (source != null)
            {
                source.AddHook(WndProc);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;

            if (msg == WM_GETMINMAXINFO)
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

                if (monitor != IntPtr.Zero)
                {
                    var monitorInfo = new MONITORINFO();
                    GetMonitorInfo(monitor, monitorInfo);
                    var rcWorkArea = monitorInfo.rcWork;
                    var rcMonitorArea = monitorInfo.rcMonitor;
                    mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                    mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                    mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                    mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
                }

                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void PenToolView_ContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= PenToolView_ContentRendered;

            ViewModel?.Render();
            InitializePanels();
            SizeChanged += PenToolView_SizeChanged;

            if (ViewModel != null)
            {
                ViewModel.FitToViewAction = FitCanvasToView;
            }

            PenSettings.Default.EraserStyle.PropertyChanged += EraserStyle_PropertyChanged;
            UpdateEraserShape();

            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                FitCanvasToView();
                this.WindowState = WindowState.Maximized;
            }));
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
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
        }

        private void SnapToMonitor()
        {
            var windowRect = new Rect(Left, Top, Width, Height);
            var monitors = GetMonitorWorkAreas();

            Rect bestMonitor = new Rect();
            double maxIntersection = 0;

            foreach (var monitorRect in monitors)
            {
                var intersection = Rect.Intersect(windowRect, monitorRect);
                if (!intersection.IsEmpty && intersection.Width * intersection.Height > maxIntersection)
                {
                    maxIntersection = intersection.Width * intersection.Height;
                    bestMonitor = monitorRect;
                }
            }

            if (maxIntersection > 0 && (maxIntersection / (windowRect.Width * windowRect.Height)) >= 0.51)
            {
                Left = bestMonitor.Left;
                Top = bestMonitor.Top;
                WindowState = WindowState.Maximized;
            }
        }

        private List<Rect> GetMonitorWorkAreas()
        {
            var monitors = new List<Rect>();
            MonitorEnumDelegate callback = (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                var mi = new MONITORINFO();
                if (GetMonitorInfo(hMonitor, mi))
                {
                    var r = mi.rcWork;
                    monitors.Add(new Rect(r.left, r.top, r.right - r.left, r.bottom - r.top));
                }
                return true;
            };

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            return monitors;
        }

        private void ProtectedArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void CapturePanelLayouts()
        {
            if (MainCanvas.ActualWidth == 0 || MainCanvas.ActualHeight == 0) return;

            foreach (var (name, panel) in _panels)
            {
                _panelLayouts[name] = new PanelLayout
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
            if (!_isLayoutInitialized || MainCanvas.ActualWidth == 0 || MainCanvas.ActualHeight == 0) return;

            foreach (var (name, panel) in _panels)
            {
                if (_panelLayouts.TryGetValue(name, out var layout))
                {
                    var newWidth = Math.Max(panel.MinWidth, layout.WidthRatio * MainCanvas.ActualWidth);
                    var newHeight = Math.Max(panel.MinHeight, layout.HeightRatio * MainCanvas.ActualHeight);

                    var newLeft = layout.LeftRatio * MainCanvas.ActualWidth;
                    var newTop = layout.TopRatio * MainCanvas.ActualHeight;

                    if (newLeft + newWidth > MainCanvas.ActualWidth)
                        newLeft = MainCanvas.ActualWidth - newWidth;
                    if (newTop + newHeight > MainCanvas.ActualHeight)
                        newTop = MainCanvas.ActualHeight - newHeight;

                    panel.Width = newWidth;
                    panel.Height = newHeight;
                    Canvas.SetLeft(panel, Math.Max(0, newLeft));
                    Canvas.SetTop(panel, Math.Max(0, newTop));
                }
            }
        }

        private void FitCanvasToView()
        {
            if (ViewModel == null || CanvasViewport.ActualWidth <= 0 || CanvasViewport.ActualHeight <= 0) return;

            var canvasSize = ViewModel.CanvasSize;
            if (canvasSize.Width <= 0 || canvasSize.Height <= 0) return;

            var viewportSize = new Size(CanvasViewport.ActualWidth, CanvasViewport.ActualHeight);

            double scaleX = viewportSize.Width / canvasSize.Width;
            double scaleY = viewportSize.Height / canvasSize.Height;
            double newScale = Math.Min(scaleX, scaleY);

            newScale = Math.Max(0.1, newScale);
            newScale = Math.Min(10.0, newScale);

            ViewModel.CanvasScale = newScale;
            ViewModel.CanvasAngle = 0;

            ViewModel.CanvasTranslateX = (viewportSize.Width - canvasSize.Width) / 2;
            ViewModel.CanvasTranslateY = (viewportSize.Height - canvasSize.Height) / 2;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SaveLayout();
            ViewModel?.SaveLayout(PenSettings.Default.Layout);
            base.OnClosing(e);
            SizeChanged -= PenToolView_SizeChanged;
            PenSettings.Default.EraserStyle.PropertyChanged -= EraserStyle_PropertyChanged;
        }

        private void InitializePanels()
        {
            _panels.Add("CanvasPanel", CanvasPanel);
            _panels.Add("LayersPanel", LayersPanel);
            _panels.Add("CanvasControlPanel", CanvasControlPanel);

            foreach (var panel in _panels.Values)
            {
                _zOrder.Add(panel);
                panel.ApplyTemplate();
                if (panel.Template.FindName("Header", panel) is FrameworkElement header)
                {
                    header.PreviewMouseLeftButtonDown += Header_PreviewMouseLeftButtonDown;
                    header.PreviewMouseRightButtonUp += Header_PreviewMouseRightButtonUp;
                }

                var thumbNames = new[] { "ResizeTop", "ResizeBottom", "ResizeLeft", "ResizeRight", "ResizeTopLeft", "ResizeTopRight", "ResizeBottomLeft", "ResizeBottomRight" };
                foreach (var thumbName in thumbNames)
                {
                    if (panel.Template.FindName(thumbName, panel) is Thumb thumb)
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
            if (sender is ContentControl panel)
            {
                var layoutSettings = PenSettings.Default.Layout;
                if (layoutSettings.TryGetValue(panel.Name, out var info) && info.IsTranslucent)
                {
                    panel.Opacity = 0.7;
                }
            }
        }

        private void Panel_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is ContentControl panel)
            {
                var layoutSettings = PenSettings.Default.Layout;
                if (layoutSettings.TryGetValue(panel.Name, out var info) && info.IsTranslucent)
                {
                    panel.Opacity = 0.5;
                }
            }
        }


        private void Panel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ContentControl panel)
            {
                BringToFront(panel);
            }
        }

        private void LoadLayout()
        {
            var layoutSettings = PenSettings.Default.Layout;
            bool needsDefaultLayout = !layoutSettings.Any() || layoutSettings.Count != _panels.Count;

            var orderedPanels = _panels.Values
                .OrderBy(p => layoutSettings.TryGetValue(p.Name, out var info) ? info.ZIndex : 0)
                .ToList();
            _zOrder.Clear();
            _zOrder.AddRange(orderedPanels);

            foreach (var (name, panel) in _panels)
            {
                if (!needsDefaultLayout && layoutSettings.TryGetValue(name, out var info))
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
            if (name == "CanvasPanel")
            {
                panel.Width = Math.Max(200, MainCanvas.ActualWidth - 280);
                panel.Height = Math.Max(150, MainCanvas.ActualHeight - 20);
                Canvas.SetLeft(panel, 10);
                Canvas.SetTop(panel, 10);
            }
            else if (name == "LayersPanel")
            {
                panel.Width = 250;
                panel.Height = Math.Max(200, MainCanvas.ActualHeight - 20);
                Canvas.SetLeft(panel, Math.Max(220, MainCanvas.ActualWidth - 260));
                Canvas.SetTop(panel, 10);
            }
            else if (name == "CanvasControlPanel")
            {
                panel.Width = 200;
                panel.Height = 240;
                Canvas.SetLeft(panel, Math.Max(220, MainCanvas.ActualWidth - 480));
                Canvas.SetTop(panel, MainCanvas.ActualHeight - 250);
            }
        }

        private void SaveLayout()
        {
            var layoutSettings = PenSettings.Default.Layout;
            foreach (var (name, panel) in _panels)
            {
                if (!layoutSettings.ContainsKey(name))
                {
                    layoutSettings[name] = new PanelLayoutInfo();
                }
                var info = layoutSettings[name];
                info.X = Canvas.GetLeft(panel);
                info.Y = Canvas.GetTop(panel);
                info.Width = panel.ActualWidth;
                info.Height = panel.ActualHeight;
                info.ZIndex = Panel.GetZIndex(panel);
                info.IsVisible = panel.Visibility == Visibility.Visible;
            }
        }

        private void NormalizeZIndices()
        {
            var layoutSettings = PenSettings.Default.Layout;

            var normalPanels = _zOrder.Where(p =>
                !layoutSettings.TryGetValue(p.Name, out var info) || !info.IsAlwaysOnTop).ToList();

            var alwaysOnTopPanels = _zOrder.Where(p =>
                layoutSettings.TryGetValue(p.Name, out var info) && info.IsAlwaysOnTop).ToList();

            for (int i = 0; i < normalPanels.Count; i++)
            {
                Panel.SetZIndex(normalPanels[i], i);
            }

            for (int i = 0; i < alwaysOnTopPanels.Count; i++)
            {
                Panel.SetZIndex(alwaysOnTopPanels[i], AlwaysOnTopZIndexBase + i);
            }
        }

        private void BringToFront(ContentControl panel)
        {
            if (panel == null) return;

            var layoutSettings = PenSettings.Default.Layout;

            if (layoutSettings.TryGetValue(panel.Name, out var info) && info.IsAlwaysOnTop)
                return;

            var normalPanels = _zOrder.Where(p =>
                !layoutSettings.TryGetValue(p.Name, out var panelInfo) || !panelInfo.IsAlwaysOnTop).ToList();

            if (normalPanels.Contains(panel))
            {
                _zOrder.Remove(panel);

                var alwaysOnTopPanels = _zOrder.Where(p =>
                    layoutSettings.TryGetValue(p.Name, out var panelInfo) && panelInfo.IsAlwaysOnTop).ToList();

                var insertIndex = _zOrder.Count - alwaysOnTopPanels.Count;
                _zOrder.Insert(insertIndex, panel);

                NormalizeZIndices();
            }
        }

        private void ToggleAlwaysOnTop(ContentControl panel)
        {
            var layoutSettings = PenSettings.Default.Layout;
            if (!layoutSettings.ContainsKey(panel.Name))
            {
                layoutSettings[panel.Name] = new PanelLayoutInfo();
            }

            var info = layoutSettings[panel.Name];
            info.IsAlwaysOnTop = !info.IsAlwaysOnTop;

            NormalizeZIndices();
        }

        private bool IsAlwaysOnTop(ContentControl panel)
        {
            var layoutSettings = PenSettings.Default.Layout;
            return layoutSettings.TryGetValue(panel.Name, out var info) && info.IsAlwaysOnTop;
        }

        private void Header_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement { TemplatedParent: ContentControl panel })
            {
                _draggedElement = panel;
                _dragOffset = e.GetPosition(_draggedElement);

                BringToFront(panel);

                _draggedElement.CaptureMouse();
                e.Handled = true;
            }
        }

        private void MainCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedElement != null)
            {
                var currentPos = e.GetPosition(MainCanvas);
                var newLeft = currentPos.X - _dragOffset.X;
                var newTop = currentPos.Y - _dragOffset.Y;

                var snapTargets = _panels.Values
                    .Where(p => p != _draggedElement && p.Visibility == Visibility.Visible)
                    .Select(p => new Rect(Canvas.GetLeft(p), Canvas.GetTop(p), p.ActualWidth, p.ActualHeight))
                    .ToList();
                snapTargets.Add(new Rect(0, 0, MainCanvas.ActualWidth, MainCanvas.ActualHeight));

                var draggedRect = new Rect(newLeft, newTop, _draggedElement.ActualWidth, _draggedElement.ActualHeight);

                var (snappedLeft, snappedTop) = Snap(draggedRect, snapTargets);
                newLeft = snappedLeft;
                newTop = snappedTop;

                newLeft = Math.Max(0, Math.Min(newLeft, MainCanvas.ActualWidth - _draggedElement.ActualWidth));
                newTop = Math.Max(0, Math.Min(newTop, MainCanvas.ActualHeight - _draggedElement.ActualHeight));

                Canvas.SetLeft(_draggedElement, newLeft);
                Canvas.SetTop(_draggedElement, newTop);
            }
        }

        private (double, double) Snap(Rect sourceRect, List<Rect> targets)
        {
            double snappedX = sourceRect.X;
            double snappedY = sourceRect.Y;
            double minXDist = SnapThreshold;
            double minYDist = SnapThreshold;

            foreach (var target in targets)
            {
                CheckSnap(sourceRect.Left, target.Left, ref minXDist, ref snappedX);
                CheckSnap(sourceRect.Left, target.Right, ref minXDist, ref snappedX);
                CheckSnap(sourceRect.Right, target.Left, ref minXDist, ref snappedX, sourceRect.Width);
                CheckSnap(sourceRect.Right, target.Right, ref minXDist, ref snappedX, sourceRect.Width);

                CheckSnap(sourceRect.Top, target.Top, ref minYDist, ref snappedY);
                CheckSnap(sourceRect.Top, target.Bottom, ref minYDist, ref snappedY);
                CheckSnap(sourceRect.Bottom, target.Top, ref minYDist, ref snappedY, sourceRect.Height);
                CheckSnap(sourceRect.Bottom, target.Bottom, ref minYDist, ref snappedY, sourceRect.Height);
            }

            return (snappedX, snappedY);
        }

        private void CheckSnap(double sourceEdge, double targetEdge, ref double minDist, ref double snappedPos, double offset = 0)
        {
            double dist = Math.Abs(sourceEdge - targetEdge);
            if (dist < minDist)
            {
                minDist = dist;
                snappedPos = targetEdge - offset;
            }
        }

        private void MainCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedElement != null)
            {
                _draggedElement.ReleaseMouseCapture();
                _draggedElement = null;
                CapturePanelLayouts();
            }
        }

        private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is Thumb { TemplatedParent: ContentControl panel })
            {
                BringToFront(panel);
            }
        }

        private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            CapturePanelLayouts();
        }

        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is not Thumb thumb || thumb.TemplatedParent is not ContentControl panel) return;

            var snapTargets = _panels.Values
                .Where(p => p != panel && p.Visibility == Visibility.Visible)
                .Select(p => new Rect(Canvas.GetLeft(p), Canvas.GetTop(p), p.ActualWidth, p.ActualHeight))
                .ToList();
            snapTargets.Add(new Rect(0, 0, MainCanvas.ActualWidth, MainCanvas.ActualHeight));

            double newTop = Canvas.GetTop(panel);
            double newLeft = Canvas.GetLeft(panel);
            double newWidth = panel.Width;
            double newHeight = panel.Height;

            double potentialTop = newTop;
            double potentialLeft = newLeft;
            double potentialRight = newLeft + newWidth;
            double potentialBottom = newTop + newHeight;

            if (thumb.Name.Contains("Top")) potentialTop += e.VerticalChange;
            if (thumb.Name.Contains("Bottom")) potentialBottom += e.VerticalChange;
            if (thumb.Name.Contains("Left")) potentialLeft += e.HorizontalChange;
            if (thumb.Name.Contains("Right")) potentialRight += e.HorizontalChange;

            if (thumb.Name.Contains("Top"))
            {
                double minDist = SnapThreshold;
                foreach (var target in snapTargets)
                {
                    CheckSnapEdge(potentialTop, target.Top, ref minDist, ref potentialTop);
                    CheckSnapEdge(potentialTop, target.Bottom, ref minDist, ref potentialTop);
                }
            }
            if (thumb.Name.Contains("Bottom"))
            {
                double minDist = SnapThreshold;
                foreach (var target in snapTargets)
                {
                    CheckSnapEdge(potentialBottom, target.Top, ref minDist, ref potentialBottom);
                    CheckSnapEdge(potentialBottom, target.Bottom, ref minDist, ref potentialBottom);
                }
            }
            if (thumb.Name.Contains("Left"))
            {
                double minDist = SnapThreshold;
                foreach (var target in snapTargets)
                {
                    CheckSnapEdge(potentialLeft, target.Left, ref minDist, ref potentialLeft);
                    CheckSnapEdge(potentialLeft, target.Right, ref minDist, ref potentialLeft);
                }
            }
            if (thumb.Name.Contains("Right"))
            {
                double minDist = SnapThreshold;
                foreach (var target in snapTargets)
                {
                    CheckSnapEdge(potentialRight, target.Left, ref minDist, ref potentialRight);
                    CheckSnapEdge(potentialRight, target.Right, ref minDist, ref potentialRight);
                }
            }

            if (thumb.Name.Contains("Top"))
            {
                newHeight = (newTop + newHeight) - potentialTop;
                newTop = potentialTop;
            }
            if (thumb.Name.Contains("Bottom"))
            {
                newHeight = potentialBottom - newTop;
            }
            if (thumb.Name.Contains("Left"))
            {
                newWidth = (newLeft + newWidth) - potentialLeft;
                newLeft = potentialLeft;
            }
            if (thumb.Name.Contains("Right"))
            {
                newWidth = potentialRight - newLeft;
            }

            if (newHeight < panel.MinHeight)
            {
                if (thumb.Name.Contains("Top")) newTop = (newTop + newHeight) - panel.MinHeight;
                newHeight = panel.MinHeight;
            }
            if (newWidth < panel.MinWidth)
            {
                if (thumb.Name.Contains("Left")) newLeft = (newLeft + newWidth) - panel.MinWidth;
                newWidth = panel.MinWidth;
            }

            if (newLeft < 0) { newWidth += newLeft; newLeft = 0; }
            if (newTop < 0) { newHeight += newTop; newTop = 0; }
            if (newLeft + newWidth > MainCanvas.ActualWidth) { newWidth = MainCanvas.ActualWidth - newLeft; }
            if (newTop + newHeight > MainCanvas.ActualHeight) { newHeight = MainCanvas.ActualHeight - newTop; }

            if (newWidth >= panel.MinWidth)
            {
                panel.Width = newWidth;
                Canvas.SetLeft(panel, newLeft);
            }
            if (newHeight >= panel.MinHeight)
            {
                panel.Height = newHeight;
                Canvas.SetTop(panel, newTop);
            }
        }

        private void CheckSnapEdge(double sourceEdge, double targetEdge, ref double minDist, ref double snappedEdge)
        {
            double dist = Math.Abs(sourceEdge - targetEdge);
            if (dist < minDist)
            {
                minDist = dist;
                snappedEdge = targetEdge;
            }
        }

        private void PenToolView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isLayoutInitialized)
            {
                CapturePanelLayouts();
                _isLayoutInitialized = true;
            }
            else
            {
                ApplyPanelLayouts();
            }
        }

        private void Header_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement header && header.TemplatedParent is ContentControl panel && ViewModel != null)
            {
                var contextMenu = new ContextMenu();
                contextMenu.PlacementTarget = header;

                var closeItem = new MenuItem { Header = "閉じる" };
                closeItem.Click += (s, args) =>
                {
                    if (panel.Name == "CanvasPanel") ViewModel.IsCanvasVisible = false;
                    else if (panel.Name == "LayersPanel") ViewModel.IsLayersVisible = false;
                    else if (panel.Name == "CanvasControlPanel") ViewModel.IsCanvasControlPanelVisible = false;
                };
                contextMenu.Items.Add(closeItem);

                if (panel.Name != "CanvasPanel")
                {
                    var alwaysOnTopItem = new MenuItem
                    {
                        Header = "常に最前面",
                        IsCheckable = true,
                        IsChecked = IsAlwaysOnTop(panel)
                    };
                    alwaysOnTopItem.Click += (s, args) => ToggleAlwaysOnTop(panel);
                    contextMenu.Items.Add(alwaysOnTopItem);

                    var layoutSettings = PenSettings.Default.Layout;
                    if (!layoutSettings.ContainsKey(panel.Name))
                    {
                        layoutSettings[panel.Name] = new PanelLayoutInfo();
                    }
                    var info = layoutSettings[panel.Name];

                    var opacityItem = new MenuItem { Header = "半透明", IsCheckable = true, IsChecked = info.IsTranslucent };
                    opacityItem.Click += (s, args) =>
                    {
                        if (s is MenuItem mi)
                        {
                            info.IsTranslucent = mi.IsChecked;
                            panel.Opacity = mi.IsChecked ? 0.5 : 1.0;
                        }
                    };
                    contextMenu.Items.Add(opacityItem);
                }

                contextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        private void CanvasViewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ViewModel == null) return;

            switch (PenSettings.Default.MouseWheelAction)
            {
                case MouseWheelAction.Zoom:
                    HandleZoom(sender, e);
                    break;
                case MouseWheelAction.PenSize:
                    ViewModel.StrokeThickness += e.Delta > 0 ? 1 : -1;
                    UpdatePenPreview(e);
                    break;
            }
        }

        private void HandleZoom(object sender, MouseWheelEventArgs e)
        {
            if (ViewModel == null) return;
            var viewport = sender as FrameworkElement;
            if (viewport == null) return;

            var position = e.GetPosition(viewport);

            var oldScale = ViewModel.CanvasScale;
            var angle = ViewModel.CanvasAngle;
            var oldTranslateX = ViewModel.CanvasTranslateX;
            var oldTranslateY = ViewModel.CanvasTranslateY;
            var canvasCenter = new Point(ViewModel.CanvasSize.Width / 2, ViewModel.CanvasSize.Height / 2);

            var transform = new Matrix();
            transform.Translate(-canvasCenter.X, -canvasCenter.Y);
            transform.Scale(oldScale, oldScale);
            transform.Rotate(angle);
            transform.Translate(canvasCenter.X, canvasCenter.Y);
            transform.Translate(oldTranslateX, oldTranslateY);

            transform.Invert();
            var untransformedPos = transform.Transform(position);

            var scaleFactor = e.Delta > 0 ? 1.2 : 1.0 / 1.2;
            var newScale = oldScale * scaleFactor;
            newScale = Math.Clamp(newScale, 0.1, 10.0);

            if (Math.Abs(newScale - oldScale) < 0.001) return;

            var newTransformWithoutTranslate = new Matrix();
            newTransformWithoutTranslate.Translate(-canvasCenter.X, -canvasCenter.Y);
            newTransformWithoutTranslate.Scale(newScale, newScale);
            newTransformWithoutTranslate.Rotate(angle);
            newTransformWithoutTranslate.Translate(canvasCenter.X, canvasCenter.Y);

            var newTransformedPos = newTransformWithoutTranslate.Transform(untransformedPos);

            var newTranslateX = position.X - newTransformedPos.X;
            var newTranslateY = position.Y - newTransformedPos.Y;

            ViewModel.CanvasScale = newScale;
            ViewModel.CanvasTranslateX = newTranslateX;
            ViewModel.CanvasTranslateY = newTranslateY;

            UpdatePenPreview(e);
            e.Handled = true;
        }

        private void CanvasViewport_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                if (sender is FrameworkElement viewport)
                {
                    _lastMousePositionOnViewport = e.GetPosition(viewport);
                    viewport.CaptureMouse();
                    _isPanning = true;
                    Mouse.OverrideCursor = Cursors.ScrollAll;
                    e.Handled = true;
                }
            }
        }

        private void CanvasViewport_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning && ViewModel != null && _lastMousePositionOnViewport.HasValue)
            {
                if (sender is FrameworkElement viewport)
                {
                    var currentPosition = e.GetPosition(viewport);
                    var delta = currentPosition - _lastMousePositionOnViewport.Value;
                    _lastMousePositionOnViewport = currentPosition;

                    ViewModel.CanvasTranslateX += delta.X;
                    ViewModel.CanvasTranslateY += delta.Y;
                    e.Handled = true;
                }
            }
            UpdatePenPreview(e);
        }

        private void CanvasViewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && _isPanning)
            {
                if (sender is FrameworkElement viewport)
                {
                    viewport.ReleaseMouseCapture();
                    _isPanning = false;
                    _lastMousePositionOnViewport = null;
                    Mouse.OverrideCursor = null;
                    e.Handled = true;
                }
            }
        }

        private void InkCanvas_SelectionChanged(object sender, EventArgs e)
        {
            if (sender is InkCanvas inkCanvas && ViewModel != null)
            {
                ViewModel.FilterSelection(inkCanvas.GetSelectedStrokes(), newSelection => inkCanvas.Select(newSelection));
            }
        }

        private void EraserStyle_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EraserStyleSettings.StrokeThickness))
            {
                UpdateEraserShape();
            }
        }

        private void UpdateEraserShape()
        {
            var size = PenSettings.Default.EraserStyle.StrokeThickness;
            inkCanvas.EraserShape = new EllipseStylusShape(size, size);
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source is not TextBox && e.Source is not CheckBox)
            {
                _dragStartPoint = e.GetPosition(null);
            }
        }

        private void ListViewItem_MouseMove(object sender, MouseEventArgs e)
        {
            Point position = e.GetPosition(null);
            Vector diff = _dragStartPoint - position;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                var listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);
                if (listViewItem == null) return;

                var layer = (Layer.Layer)listViewItem.DataContext;
                if (layer == null) return;

                DataObject dragData = new DataObject("Layer", layer);
                DragDrop.DoDragDrop(listViewItem, dragData, DragDropEffects.Move);
            }
        }

        private void ListViewItem_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("Layer"))
            {
                var targetItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);
                if (targetItem == null) return;

                var dropIndicator = FindVisualChild<Border>(targetItem, "DropIndicator");
                if (dropIndicator != null)
                {
                    dropIndicator.Visibility = Visibility.Collapsed;
                }

                var targetLayer = (Layer.Layer)targetItem.DataContext;
                var sourceLayer = (Layer.Layer)e.Data.GetData("Layer");

                if (sourceLayer != null && targetLayer != null && !ReferenceEquals(sourceLayer, targetLayer) && ViewModel != null)
                {
                    int sourceIndex = ViewModel.Layers.IndexOf(sourceLayer);
                    int targetIndex = ViewModel.Layers.IndexOf(targetLayer);
                    ViewModel.MoveLayer(sourceIndex, targetIndex);
                }
            }
        }

        private void ListViewItem_DragOver(object sender, DragEventArgs e)
        {
            var targetItem = sender as ListViewItem;
            if (targetItem == null) return;

            var dropIndicator = FindVisualChild<Border>(targetItem, "DropIndicator");
            if (dropIndicator != null)
            {
                dropIndicator.Visibility = Visibility.Visible;
            }
        }

        private void ListViewItem_DragLeave(object sender, DragEventArgs e)
        {
            var targetItem = sender as ListViewItem;
            if (targetItem == null) return;

            var dropIndicator = FindVisualChild<Border>(targetItem, "DropIndicator");
            if (dropIndicator != null)
            {
                dropIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem item && item.DataContext is Layer.Layer layer)
            {
                if (e.Source is not TextBox && e.Source is not CheckBox && FindAncestor<CheckBox>((DependencyObject)e.OriginalSource) == null)
                {
                    var originalOpacity = layer.Opacity;
                    var propertiesView = new LayerPropertiesView
                    {
                        Owner = this,
                        DataContext = layer
                    };

                    var result = propertiesView.ShowDialog();
                    if (result == false)
                    {
                        layer.Opacity = originalOpacity;
                    }
                    else
                    {
                        ViewModel?.AddLayerPropertyChangedUndo(layer, nameof(Layer.Layer.Opacity), originalOpacity, layer.Opacity);
                    }
                }
            }
        }

        private void CheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is Layer.Layer layer)
            {
                layer.IsVisible = !layer.IsVisible;
                e.Handled = true;
            }
        }

        private void CanvasViewport_MouseEnter(object sender, MouseEventArgs e)
        {
            UpdatePenPreview(e);
        }

        private void CanvasViewport_MouseLeave(object sender, MouseEventArgs e)
        {
            PenPreview.Visibility = Visibility.Collapsed;
            PenPreviewRectangle.Visibility = Visibility.Collapsed;
        }

        private void UpdatePenPreview(MouseEventArgs e)
        {
            if (ViewModel == null || inkCanvas.EditingMode == InkCanvasEditingMode.None || inkCanvas.EditingMode == InkCanvasEditingMode.Select || Mouse.LeftButton == MouseButtonState.Pressed)
            {
                PenPreview.Visibility = Visibility.Collapsed;
                PenPreviewRectangle.Visibility = Visibility.Collapsed;
                return;
            }

            PenPreview.Visibility = Visibility.Visible;

            double width = 0;
            double height = 0;
            var color = ViewModel.StrokeColor;
            var isHighlighter = ViewModel.SelectedBrushType == Brush.BrushType.Highlighter;

            if (inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
            {
                width = ViewModel.StrokeThickness * ViewModel.CanvasScale;
                height = ViewModel.StrokeThickness * ViewModel.CanvasScale;

                if (isHighlighter)
                {
                    width /= 2;
                }
                PenPreview.Fill = new SolidColorBrush(Color.FromArgb(128, color.R, color.G, color.B));
                PenPreviewRectangle.Fill = PenPreview.Fill;
            }
            else if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint || inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke)
            {
                width = PenSettings.Default.EraserStyle.StrokeThickness * ViewModel.CanvasScale;
                height = width;
                PenPreview.Fill = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255));
                PenPreviewRectangle.Fill = PenPreview.Fill;
            }

            if (width <= 0 || height <= 0)
            {
                PenPreview.Visibility = Visibility.Collapsed;
                PenPreviewRectangle.Visibility = Visibility.Collapsed;
                return;
            }

            Point position = e.GetPosition(CanvasViewport);

            if (isHighlighter)
            {
                PenPreview.Visibility = Visibility.Collapsed;
                PenPreviewRectangle.Visibility = Visibility.Visible;
                PenPreviewRectangle.Width = width;
                PenPreviewRectangle.Height = height;
                Canvas.SetLeft(PenPreviewRectangle, position.X - width / 2);
                Canvas.SetTop(PenPreviewRectangle, position.Y - height / 2);
            }
            else
            {
                PenPreviewRectangle.Visibility = Visibility.Collapsed;
                PenPreview.Visibility = Visibility.Visible;
                PenPreview.Width = width;
                PenPreview.Height = height;
                Canvas.SetLeft(PenPreview, position.X - width / 2);
                Canvas.SetTop(PenPreview, position.Y - height / 2);
            }
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        private static T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                {
                    return element;
                }
                else
                {
                    var result = FindVisualChild<T>(child, name);
                    if (result != null)
                        return result;
                }
            }
            return null;
        }
    }
}
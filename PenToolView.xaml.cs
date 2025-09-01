using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Threading;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    public partial class PenToolView : Window
    {
        private const double SnapThreshold = 10.0;
        private const int AlwaysOnTopZIndexBase = 1000;
        private readonly Dictionary<string, ContentControl> _panels = new();
        private readonly List<ContentControl> _zOrder = new();
        private ContentControl? _draggedElement;
        private Point _dragOffset;
        private PenToolViewModel? ViewModel => DataContext as PenToolViewModel;

        private Point? _lastMousePositionOnViewport;
        private bool _isPanning;

        public PenToolView()
        {
            InitializeComponent();
            ContentRendered += PenToolView_ContentRendered;
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

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(FitCanvasToView));
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

            ViewModel.CanvasScale = newScale;

            double newContentWidth = canvasSize.Width * newScale;
            double newContentHeight = canvasSize.Height * newScale;

            ViewModel.CanvasTranslateX = (viewportSize.Width - newContentWidth) / 2;
            ViewModel.CanvasTranslateY = (viewportSize.Height - newContentHeight) / 2;
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
            }
        }

        private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is Thumb { TemplatedParent: ContentControl panel })
            {
                BringToFront(panel);
            }
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
            foreach (var panel in _panels.Values.Where(p => p.Visibility == Visibility.Visible))
            {
                var left = Canvas.GetLeft(panel);
                var top = Canvas.GetTop(panel);

                if (left + panel.ActualWidth > MainCanvas.ActualWidth)
                {
                    left = MainCanvas.ActualWidth - panel.ActualWidth;
                    Canvas.SetLeft(panel, Math.Max(0, left));
                }
                if (top + panel.ActualHeight > MainCanvas.ActualHeight)
                {
                    top = MainCanvas.ActualHeight - panel.ActualHeight;
                    Canvas.SetTop(panel, Math.Max(0, top));
                }
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
                    break;
            }
        }

        private void HandleZoom(object sender, MouseWheelEventArgs e)
        {
            if (ViewModel == null) return;

            var viewport = sender as FrameworkElement;
            if (viewport == null) return;

            var position = e.GetPosition(viewport);
            var scaleFactor = e.Delta > 0 ? 1.2 : 1.0 / 1.2;

            var targetScale = ViewModel.CanvasScale * scaleFactor;
            targetScale = Math.Clamp(targetScale, 0.1, 10.0);
            scaleFactor = targetScale / ViewModel.CanvasScale;

            if (Math.Abs(scaleFactor - 1.0) < 0.001) return;

            var newTranslateX = position.X - (position.X - ViewModel.CanvasTranslateX) * scaleFactor;
            var newTranslateY = position.Y - (position.Y - ViewModel.CanvasTranslateY) * scaleFactor;

            ViewModel.CanvasScale = targetScale;
            ViewModel.CanvasTranslateX = newTranslateX;
            ViewModel.CanvasTranslateY = newTranslateY;

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
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Plugin.Community.Shape.Pen.Brush;
using YukkuriMovieMaker.Plugin.Community.Shape.Pen.Layer;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenToolViewModel : Bindable, IDisposable
    {
        private readonly DisposeCollector disposer = new();
        private readonly PenShapeParameter parameter;
        private readonly IEditorInfo info;
        private readonly ITimelineSourceAndDevices source;
        private readonly Dictionary<Stroke, Layer.Layer> _strokeLayerMap = new();
        private readonly Dictionary<Stroke, Stroke> _displayToOriginalStrokeMap = new();


        public ObservableCollection<Layer.Layer> Layers { get; } = [];

        public Layer.Layer? SelectedLayer
        {
            get => _selectedLayer;
            set
            {
                if (Set(ref _selectedLayer, value))
                {
                    UpdatePenProperties();
                    RemoveLayerCommand.RaiseCanExecuteChanged();
                    MoveLayerUpCommand.RaiseCanExecuteChanged();
                    MoveLayerDownCommand.RaiseCanExecuteChanged();
                }
            }
        }
        private Layer.Layer? _selectedLayer;

        public StrokeCollection Strokes { get; } = new StrokeCollection();


        public BrushType SelectedBrushType
        {
            get => PenSettings.Default.SelectedBrushType;
            set
            {
                if (PenSettings.Default.SelectedBrushType != value)
                {
                    PenSettings.Default.SelectedBrushType = value;
                    OnPropertyChanged();
                    UpdatePenProperties();
                }
            }
        }

        public ToolbarLayout ToolbarLayout
        {
            get => PenSettings.Default.ToolbarLayout;
            set
            {
                if (PenSettings.Default.ToolbarLayout != value)
                {
                    PenSettings.Default.ToolbarLayout = value;
                    OnPropertyChanged();
                }
            }
        }

        public DrawingAttributes Pen => CreateDrawingAttributes();

        public InkCanvasEditingMode EditingMode
        {
            get => editingMode;
            set
            {
                if (Set(ref editingMode, value))
                {
                    OnPropertyChanged(nameof(IsColorPickerEnabled));
                    OnPropertyChanged(nameof(IsSelectMode));
                }
            }
        }
        private InkCanvasEditingMode editingMode = InkCanvasEditingMode.Ink;

        public bool IsSelectMode
        {
            get => EditingMode == InkCanvasEditingMode.Select;
            set
            {
                if (value)
                {
                    EditingMode = InkCanvasEditingMode.Select;
                }
                else if (EditingMode == InkCanvasEditingMode.Select)
                {
                    UpdatePenProperties();
                }
            }
        }

        public Color StrokeColor
        {
            get => GetCurrentBrushSettings()?.StrokeColor ?? Colors.Transparent;
            set
            {
                var settings = GetCurrentBrushSettings();
                if (settings != null)
                {
                    settings.StrokeColor = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Pen));
                }
            }
        }

        public double StrokeThickness
        {
            get => GetCurrentBrushSettings()?.StrokeThickness ?? 0;
            set
            {
                var settings = GetCurrentBrushSettings();
                if (settings != null)
                {
                    settings.StrokeThickness = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Pen));
                }
            }
        }

        public bool IsColorPickerEnabled => SelectedBrushType != BrushType.Eraser && EditingMode != InkCanvasEditingMode.Select;

        public ICommand MouseWheelCommand { get; }
        public ActionCommand UndoCommand { get; }
        public ActionCommand RedoCommand { get; }
        public ICommand SaveImageCommand { get; }
        public ICommand ExportIsfCommand { get; }
        public ICommand ImportIsfCommand { get; }

        public ActionCommand AddLayerCommand { get; }
        public ActionCommand RemoveLayerCommand { get; }
        public ActionCommand MoveLayerUpCommand { get; }
        public ActionCommand MoveLayerDownCommand { get; }

        public ICommand SwitchEraserModeCommand { get; }

        public BitmapSource Bitmap { get => bitmap; private set => Set(ref bitmap, value); }
        private BitmapSource bitmap;

        public Size CanvasSize { get; }

        private readonly Stack<(Action Undo, Action Redo)> undoStack = new();
        private readonly Stack<(Action Undo, Action Redo)> redoStack = new();
        private bool isUndoRedoing = false;

        public bool IsCanvasVisible { get => isCanvasVisible; set => Set(ref isCanvasVisible, value); }
        private bool isCanvasVisible = true;

        public bool IsLayersVisible { get => isLayersVisible; set => Set(ref isLayersVisible, value); }
        private bool isLayersVisible = true;

        public ICommand TogglePanelVisibilityCommand { get; }

        public double CanvasScale { get => canvasScale; set => Set(ref canvasScale, Math.Clamp(value, 0.1, 10)); }
        private double canvasScale = 1.0;

        public double CanvasAngle { get => canvasAngle; set => Set(ref canvasAngle, value); }
        private double canvasAngle = 0;

        public double CanvasTranslateX { get => canvasTranslateX; set => Set(ref canvasTranslateX, value); }
        private double canvasTranslateX = 0;

        public double CanvasTranslateY { get => canvasTranslateY; set => Set(ref canvasTranslateY, value); }
        private double canvasTranslateY = 0;

        public bool IsCanvasControlPanelVisible { get => isCanvasControlPanelVisible; set => Set(ref isCanvasControlPanelVisible, value); }
        private bool isCanvasControlPanelVisible = true;

        public ICommand ZoomCommand { get; }
        public ActionCommand FitToViewCommand { get; }
        public ICommand ResetRotationCommand { get; }

        private Action? _fitToViewAction;
        public Action? FitToViewAction
        {
            get => _fitToViewAction;
            set
            {
                if (Set(ref _fitToViewAction, value))
                {
                    FitToViewCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private static readonly Regex defaultLayerNameRegex = new Regex(@"^レイヤー \d+$");

        private bool _isFilteringSelection = false;

        public PenToolViewModel(PenShapeParameter parameter, IEditorInfo info, IEnumerable<Layer.Layer> initialLayers)
        {
            this.parameter = parameter;
            this.info = info;
            source = info.CreateTimelineVideoSource();
            disposer.Collect(source);

            CanvasSize = new Size(info.VideoInfo.Width, info.VideoInfo.Height);

            MouseWheelCommand = new ActionCommand(_ => true, x =>
            {
                if (x is not MouseWheelEventArgs e) return;
                StrokeThickness += e.Delta > 0 ? 1 : -1;
            });

            UndoCommand = new ActionCommand(_ => undoStack.Count > 0, _ => ExecuteUndo());
            RedoCommand = new ActionCommand(_ => redoStack.Count > 0, _ => ExecuteRedo());

            AddLayerCommand = new ActionCommand(_ => true, _ => AddLayer());
            RemoveLayerCommand = new ActionCommand(_ => Layers.Count > 1 && SelectedLayer != null, _ => RemoveSelectedLayer());
            MoveLayerUpCommand = new ActionCommand(_ => CanMoveLayerUp(), _ => MoveLayerUp());
            MoveLayerDownCommand = new ActionCommand(_ => CanMoveLayerDown(), _ => MoveLayerDown());

            SaveImageCommand = new ActionCommand(_ => true, _ => SaveImage());
            ExportIsfCommand = new ActionCommand(_ => true, _ => ExportIsf());
            ImportIsfCommand = new ActionCommand(_ => true, _ => ImportIsf());

            SwitchEraserModeCommand = new ActionCommand(
                _ => true,
                p =>
                {
                    if (p is EraserMode mode)
                    {
                        PenSettings.Default.EraserStyle.Mode = mode;
                        UpdatePenProperties();
                    }
                });

            TogglePanelVisibilityCommand = new ActionCommand(_ => true, p =>
            {
                if (p is string panelName)
                {
                    switch (panelName)
                    {
                        case "CanvasPanel":
                            IsCanvasVisible = !IsCanvasVisible;
                            break;
                        case "LayersPanel":
                            IsLayersVisible = !IsLayersVisible;
                            break;
                        case "CanvasControlPanel":
                            IsCanvasControlPanelVisible = !IsCanvasControlPanelVisible;
                            break;
                    }
                }
            });

            ZoomCommand = new ActionCommand(
                _ => true,
                p =>
                {
                    if (p is string direction)
                    {
                        double zoomFactor = 1.2;
                        if (direction == "In")
                        {
                            CanvasScale *= zoomFactor;
                        }
                        else if (direction == "Out")
                        {
                            CanvasScale /= zoomFactor;
                        }
                    }
                });

            FitToViewCommand = new ActionCommand(_ => FitToViewAction != null, _ => FitToViewAction?.Invoke());
            ResetRotationCommand = new ActionCommand(_ => true, _ => CanvasAngle = 0);

            foreach (var layer in initialLayers)
            {
                var newLayer = new Layer.Layer(layer.Name)
                {
                    IsVisible = layer.IsVisible,
                    IsLocked = layer.IsLocked,
                    Opacity = layer.Opacity,
                    SerializableStrokes = layer.SerializableStrokes,
                };
                Layers.Add(newLayer);
                ((INotifyPropertyChanged)newLayer).PropertyChanged += OnLayerPropertyChanged;
                UpdateLayerThumbnail(newLayer);
            }
            SelectedLayer = Layers.FirstOrDefault();

            Strokes.StrokesChanged += OnAggregatedStrokesChanged;
            UpdateVisibleStrokes();

            UpdatePenProperties();
            LoadLayout();
            RenumberLayers();

            PenSettings.Default.PenStyle.PropertyChanged += OnBrushSettingsChanged;
            PenSettings.Default.HighlighterStyle.PropertyChanged += OnBrushSettingsChanged;
            PenSettings.Default.PencilStyle.PropertyChanged += OnBrushSettingsChanged;

            Render();
        }

        private void OnBrushSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BrushSettingsBase.IsPressure))
            {
                OnPropertyChanged(nameof(Pen));
            }
        }

        private void UpdateLayerThumbnail(Layer.Layer layer)
        {
            int thumbnailWidth = 48;
            int thumbnailHeight = 27;

            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, CanvasSize.Width, CanvasSize.Height));

                layer.Strokes.Draw(drawingContext);
            }

            var rtb = new RenderTargetBitmap((int)CanvasSize.Width, (int)CanvasSize.Height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);
            rtb.Freeze();

            var croppedBitmap = new CroppedBitmap(rtb, new Int32Rect(0, 0, (int)CanvasSize.Width, (int)CanvasSize.Height));
            var transformedBitmap = new TransformedBitmap(croppedBitmap, new ScaleTransform(
                thumbnailWidth / CanvasSize.Width,
                thumbnailHeight / CanvasSize.Height
            ));
            transformedBitmap.Freeze();
            layer.Thumbnail = transformedBitmap;
        }


        public void FilterSelection(StrokeCollection selectedStrokes, Action<StrokeCollection> updateSelection)
        {
            if (_isFilteringSelection || EditingMode != InkCanvasEditingMode.Select || SelectedLayer == null) return;

            _isFilteringSelection = true;

            var validStrokes = new StrokeCollection();

            foreach (var stroke in selectedStrokes)
            {
                if (_strokeLayerMap.TryGetValue(stroke, out var layer) && layer == SelectedLayer)
                {
                    validStrokes.Add(stroke);
                }
            }

            if (validStrokes.Count != selectedStrokes.Count)
            {
                updateSelection(validStrokes);
            }

            _isFilteringSelection = false;
        }

        public void RotateCanvas(double angle)
        {
            CanvasAngle = angle;
        }

        public void PanCanvas(double dx, double dy)
        {
            CanvasTranslateX += dx;
            CanvasTranslateY += dy;
        }

        [MemberNotNull(nameof(bitmap))]
        public void Render()
        {
            parameter.IsEditing = true;
            try
            {
                TimeSpan time = info.ItemPosition.Time < TimeSpan.Zero
                    ? info.ItemPosition.Time
                    : info.ItemDuration.Time < info.ItemPosition.Time
                    ? info.VideoInfo.GetTimeFrom(info.ItemPosition.Frame + info.ItemDuration.Frame - 1)
                    : info.TimelinePosition.Time;
                source.Update(time, TimelineSourceUsage.Paused);
                bitmap = Bitmap = source.RenderBitmapSource();
            }
            finally
            {
                parameter.IsEditing = false;
            }
        }

        public void LoadLayout()
        {
            if (PenSettings.Default.Layout.TryGetValue("CanvasPanel", out var canvasLayout))
            {
                IsCanvasVisible = canvasLayout.IsVisible;
            }
            if (PenSettings.Default.Layout.TryGetValue("LayersPanel", out var layersLayout))
            {
                IsLayersVisible = layersLayout.IsVisible;
            }
            if (PenSettings.Default.Layout.TryGetValue("CanvasControlPanel", out var canvasControlLayout))
            {
                IsCanvasControlPanelVisible = canvasControlLayout.IsVisible;
            }
        }

        public void SaveLayout(Dictionary<string, PanelLayoutInfo> layout)
        {
            if (layout.TryGetValue("CanvasPanel", out var canvasPanelInfo))
            {
                canvasPanelInfo.IsVisible = IsCanvasVisible;
            }
            if (layout.TryGetValue("LayersPanel", out var layersPanelInfo))
            {
                layersPanelInfo.IsVisible = IsLayersVisible;
            }
            if (layout.TryGetValue("CanvasControlPanel", out var canvasControlPanelInfo))
            {
                canvasControlPanelInfo.IsVisible = IsCanvasControlPanelVisible;
            }
            PenSettings.Default.Layout = layout;
            PenSettings.Default.Save();
        }

        private void OnLayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Layer.Layer.IsVisible) || e.PropertyName == nameof(Layer.Layer.Opacity))
            {
                UpdateVisibleStrokes();
            }
        }

        public void AddLayerPropertyChangedUndo(Layer.Layer layer, string propertyName, object oldValue, object newValue)
        {
            Action redoAction = () =>
            {
                var prop = typeof(Layer.Layer).GetProperty(propertyName);
                prop?.SetValue(layer, newValue);
            };

            Action undoAction = () =>
            {
                var prop = typeof(Layer.Layer).GetProperty(propertyName);
                prop?.SetValue(layer, oldValue);
            };

            PushToUndoStack(undoAction, redoAction);
        }

        private void UpdateVisibleStrokes()
        {
            Strokes.StrokesChanged -= OnAggregatedStrokesChanged;
            isUndoRedoing = true;

            Strokes.Clear();
            _strokeLayerMap.Clear();
            _displayToOriginalStrokeMap.Clear();

            foreach (var layer in Layers.Reverse())
            {
                if (layer.IsVisible)
                {
                    foreach (var originalStroke in layer.Strokes)
                    {
                        var strokeForDisplay = originalStroke.Clone();
                        var drawingAttributes = strokeForDisplay.DrawingAttributes;
                        var color = drawingAttributes.Color;

                        if (drawingAttributes.IsHighlighter)
                        {
                            drawingAttributes.IsHighlighter = false;
                            color.A = (byte)(color.A / 2.0 * layer.Opacity);
                        }
                        else
                        {
                            color.A = (byte)(color.A * layer.Opacity);
                        }
                        drawingAttributes.Color = color;

                        Strokes.Add(strokeForDisplay);
                        _strokeLayerMap[strokeForDisplay] = layer;
                        _displayToOriginalStrokeMap[strokeForDisplay] = originalStroke;
                    }
                }
            }

            isUndoRedoing = false;
            Strokes.StrokesChanged += OnAggregatedStrokesChanged;
        }

        private void OnAggregatedStrokesChanged(object? sender, StrokeCollectionChangedEventArgs e)
        {
            if (isUndoRedoing) return;

            var removedDisplayStrokes = e.Removed.ToList();
            var addedDisplayStrokes = e.Added.ToList();
            var targetLayer = SelectedLayer;

            var isErasing = EditingMode == InkCanvasEditingMode.EraseByPoint || EditingMode == InkCanvasEditingMode.EraseByStroke;

            if (isErasing)
            {
                var strokesToRestore = removedDisplayStrokes
                    .Where(s => _strokeLayerMap.TryGetValue(s, out var layer) && layer != SelectedLayer)
                    .ToList();

                if (strokesToRestore.Any())
                {
                    removedDisplayStrokes = removedDisplayStrokes.Except(strokesToRestore).ToList();

                    isUndoRedoing = true;
                    Strokes.Add(new StrokeCollection(strokesToRestore));
                    isUndoRedoing = false;
                }
            }

            if (!removedDisplayStrokes.Any() && !addedDisplayStrokes.Any()) return;

            if (targetLayer == null && addedDisplayStrokes.Any())
            {
                isUndoRedoing = true;
                Strokes.Remove(new StrokeCollection(addedDisplayStrokes));
                isUndoRedoing = false;
                return;
            }

            var affectedLayers = new HashSet<Layer.Layer>();
            var removedOriginalStrokesMap = new Dictionary<Layer.Layer, StrokeCollection>();

            foreach (var displayStroke in removedDisplayStrokes)
            {
                if (_strokeLayerMap.TryGetValue(displayStroke, out var layer) &&
                    _displayToOriginalStrokeMap.TryGetValue(displayStroke, out var originalStroke))
                {
                    if (layer.IsLocked)
                    {
                        isUndoRedoing = true;
                        Strokes.Add(displayStroke);
                        isUndoRedoing = false;
                        return;
                    }

                    if (!removedOriginalStrokesMap.ContainsKey(layer))
                    {
                        removedOriginalStrokesMap[layer] = new StrokeCollection();
                    }
                    removedOriginalStrokesMap[layer].Add(originalStroke);
                    affectedLayers.Add(layer);
                }
            }

            var addedOriginalStrokes = new StrokeCollection(addedDisplayStrokes.Select(s => s.Clone()));

            if (targetLayer != null && targetLayer.IsLocked && addedOriginalStrokes.Any())
            {
                isUndoRedoing = true;
                Strokes.Remove(new StrokeCollection(addedDisplayStrokes));
                isUndoRedoing = false;
                return;
            }
            if (targetLayer != null)
            {
                affectedLayers.Add(targetLayer);
            }

            Action redoAction = () =>
            {
                foreach (var pair in removedOriginalStrokesMap)
                {
                    pair.Key.Strokes.Remove(pair.Value);
                }
                if (targetLayer != null && addedOriginalStrokes.Any())
                {
                    targetLayer.Strokes.Add(addedOriginalStrokes);
                }
                UpdateVisibleStrokes();
                foreach (var layer in affectedLayers) UpdateLayerThumbnail(layer);
            };

            Action undoAction = () =>
            {
                if (targetLayer != null && addedOriginalStrokes.Any())
                {
                    targetLayer.Strokes.Remove(addedOriginalStrokes);
                }
                foreach (var pair in removedOriginalStrokesMap)
                {
                    pair.Key.Strokes.Add(pair.Value);
                }
                UpdateVisibleStrokes();
                foreach (var layer in affectedLayers) UpdateLayerThumbnail(layer);
            };

            PushToUndoStack(undoAction, redoAction);
            redoAction.Invoke();
        }


        private void PushToUndoStack(Action undo, Action redo)
        {
            undoStack.Push((undo, redo));
            redoStack.Clear();
            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        }

        private void ExecuteUndo()
        {
            if (undoStack.Count == 0) return;

            var (undo, redo) = undoStack.Pop();
            undo.Invoke();
            redoStack.Push((undo, redo));

            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        }

        private void ExecuteRedo()
        {
            if (redoStack.Count == 0) return;

            var (undo, redo) = redoStack.Pop();
            redo.Invoke();
            undoStack.Push((undo, redo));

            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        }

        private void UpdatePenProperties()
        {
            EditingMode = SelectedBrushType switch
            {
                BrushType.Eraser => PenSettings.Default.EraserStyle.Mode switch
                {
                    EraserMode.Line => InkCanvasEditingMode.EraseByStroke,
                    EraserMode.Point => InkCanvasEditingMode.EraseByPoint,
                    _ => InkCanvasEditingMode.None
                },
                _ => InkCanvasEditingMode.Ink
            };
            if (SelectedLayer?.IsLocked ?? false) EditingMode = InkCanvasEditingMode.None;

            OnPropertyChanged(nameof(StrokeThickness));
            OnPropertyChanged(nameof(StrokeColor));
            OnPropertyChanged(nameof(Pen));
            OnPropertyChanged(nameof(IsColorPickerEnabled));
        }

        private BrushSettingsBase? GetCurrentBrushSettings()
        {
            return SelectedBrushType switch
            {
                BrushType.Pen => PenSettings.Default.PenStyle,
                BrushType.Highlighter => PenSettings.Default.HighlighterStyle,
                BrushType.Pencil => PenSettings.Default.PencilStyle,
                BrushType.Eraser => PenSettings.Default.EraserStyle,
                _ => null
            };
        }

        private DrawingAttributes CreateDrawingAttributes()
        {
            var settings = GetCurrentBrushSettings();
            if (settings == null) return new DrawingAttributes();

            var da = new DrawingAttributes
            {
                Color = settings.StrokeColor,
                Width = settings.StrokeThickness,
                Height = settings.StrokeThickness,
                IsHighlighter = SelectedBrushType == BrushType.Highlighter,
                FitToCurve = true,
                IgnorePressure = !settings.IsPressure,
            };

            switch (SelectedBrushType)
            {
                case BrushType.Pen:
                    da.StylusTip = StylusTip.Ellipse;
                    break;
                case BrushType.Highlighter:
                    da.StylusTip = StylusTip.Rectangle;
                    da.Width = settings.StrokeThickness / 2;
                    break;
                case BrushType.Pencil:
                    da.FitToCurve = false;
                    da.StylusTip = StylusTip.Ellipse;
                    break;
                case BrushType.Eraser:
                    da.StylusTip = StylusTip.Rectangle;
                    break;
            }

            return da;
        }

        private void RenumberLayers()
        {
            for (int i = 0; i < Layers.Count; i++)
            {
                var layer = Layers[i];
                if (string.IsNullOrEmpty(layer.Name) || defaultLayerNameRegex.IsMatch(layer.Name))
                {
                    layer.Name = $"レイヤー {Layers.Count - i}";
                }
            }
        }

        private void AddLayer()
        {
            var newLayer = new Layer.Layer("");
            Layers.Insert(0, newLayer);
            SelectedLayer = newLayer;
            UpdateLayerThumbnail(newLayer);

            Action redoAction = () => {
                isUndoRedoing = true;
                if (!Layers.Contains(newLayer))
                {
                    Layers.Insert(0, newLayer);
                }
                ((INotifyPropertyChanged)newLayer).PropertyChanged += OnLayerPropertyChanged;
                SelectedLayer = newLayer;
                RenumberLayers();
                UpdateLayerThumbnail(newLayer);
                isUndoRedoing = false;
                UpdateVisibleStrokes();
            };

            Action undoAction = () => {
                isUndoRedoing = true;
                ((INotifyPropertyChanged)newLayer).PropertyChanged -= OnLayerPropertyChanged;
                Layers.Remove(newLayer);
                SelectedLayer = Layers.FirstOrDefault();
                RenumberLayers();
                isUndoRedoing = false;
                UpdateVisibleStrokes();
            };

            redoAction.Invoke();
            PushToUndoStack(undoAction, redoAction);
        }

        private void RemoveSelectedLayer()
        {
            if (SelectedLayer == null || Layers.Count <= 1) return;

            var layerToRemove = SelectedLayer;
            int index = Layers.IndexOf(layerToRemove);

            Action redoAction = () => {
                isUndoRedoing = true;
                ((INotifyPropertyChanged)layerToRemove).PropertyChanged -= OnLayerPropertyChanged;
                Layers.Remove(layerToRemove);
                SelectedLayer = Layers.Count > index ? Layers[index] : Layers.FirstOrDefault();
                RenumberLayers();
                isUndoRedoing = false;
                UpdateVisibleStrokes();
            };

            Action undoAction = () => {
                isUndoRedoing = true;
                Layers.Insert(index, layerToRemove);
                ((INotifyPropertyChanged)layerToRemove).PropertyChanged += OnLayerPropertyChanged;
                SelectedLayer = layerToRemove;
                RenumberLayers();
                isUndoRedoing = false;
                UpdateVisibleStrokes();
            };

            redoAction.Invoke();
            PushToUndoStack(undoAction, redoAction);
        }

        public void MoveLayer(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= Layers.Count || newIndex < 0 || newIndex >= Layers.Count)
                return;

            var layer = Layers[oldIndex];

            Action redoAction = () => {
                Layers.Move(oldIndex, newIndex);
                RenumberLayers();
                UpdateVisibleStrokes();
            };
            Action undoAction = () => {
                Layers.Move(newIndex, oldIndex);
                RenumberLayers();
                UpdateVisibleStrokes();
            };

            redoAction.Invoke();
            PushToUndoStack(undoAction, redoAction);
        }

        private bool CanMoveLayerUp() => SelectedLayer != null && Layers.IndexOf(SelectedLayer) > 0;
        private void MoveLayerUp()
        {
            if (!CanMoveLayerUp()) return;
            int index = Layers.IndexOf(SelectedLayer!);
            MoveLayer(index, index - 1);
            MoveLayerUpCommand.RaiseCanExecuteChanged();
            MoveLayerDownCommand.RaiseCanExecuteChanged();
        }

        private bool CanMoveLayerDown() => SelectedLayer != null && Layers.IndexOf(SelectedLayer) < Layers.Count - 1;
        private void MoveLayerDown()
        {
            if (!CanMoveLayerDown()) return;
            int index = Layers.IndexOf(SelectedLayer!);
            MoveLayer(index, index + 1);
            MoveLayerUpCommand.RaiseCanExecuteChanged();
            MoveLayerDownCommand.RaiseCanExecuteChanged();
        }

        private void SaveImage()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG|*.png;",
                DefaultExt = ".png",
            };
            if (dialog.ShowDialog() != true) return;

            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, CanvasSize.Width, CanvasSize.Height));
                foreach (var layer in Layers.Reverse())
                {
                    if (layer.IsVisible)
                    {
                        drawingContext.PushOpacity(layer.Opacity);
                        layer.Strokes.Draw(drawingContext);
                        drawingContext.Pop();
                    }
                }
            }

            var rtb = new RenderTargetBitmap((int)CanvasSize.Width, (int)CanvasSize.Height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);

            using var stream = new System.IO.FileStream(dialog.FileName, System.IO.FileMode.Create);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            encoder.Save(stream);
        }

        private void ExportIsf()
        {
            if (SelectedLayer == null) return;
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Ink Serialized Format|*.isf;",
                DefaultExt = ".isf",
            };
            if (dialog.ShowDialog() != true) return;

            using var stream = new System.IO.FileStream(dialog.FileName, System.IO.FileMode.Create);
            SelectedLayer.Strokes.Save(stream);
        }

        private void ImportIsf()
        {
            if (SelectedLayer == null || SelectedLayer.IsLocked) return;
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Ink Serialized Format|*.isf;",
                DefaultExt = ".isf",
            };
            if (dialog.ShowDialog() != true) return;

            using var stream = new System.IO.FileStream(dialog.FileName, System.IO.FileMode.Open);
            try
            {
                var importedStrokes = new StrokeCollection(stream);
                SelectedLayer.Strokes.Add(importedStrokes);
            }
            catch (Exception)
            {
            }
        }

        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var layer in Layers)
                    {
                        ((INotifyPropertyChanged)layer).PropertyChanged -= OnLayerPropertyChanged;
                    }
                    if (Strokes != null) Strokes.StrokesChanged -= OnAggregatedStrokesChanged;
                    PenSettings.Default.PenStyle.PropertyChanged -= OnBrushSettingsChanged;
                    PenSettings.Default.HighlighterStyle.PropertyChanged -= OnBrushSettingsChanged;
                    PenSettings.Default.PencilStyle.PropertyChanged -= OnBrushSettingsChanged;
                    disposer.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
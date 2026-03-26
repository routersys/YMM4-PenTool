using ExtendedPenTool.Abstractions;
using ExtendedPenTool.Brush;
using ExtendedPenTool.Enums;
using ExtendedPenTool.Infrastructure;
using ExtendedPenTool.Localization;
using ExtendedPenTool.Models;
using ExtendedPenTool.Plugin;
using ExtendedPenTool.Services;
using ExtendedPenTool.Settings;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Commons;

namespace ExtendedPenTool.ViewModels;

internal sealed class PenToolViewModel : Bindable, IDisposable
{
    private static readonly Regex DefaultLayerNamePattern = new($"^{Regex.Escape(Texts.LayerNamePrefix)}\\d+$");

    private readonly ServiceRegistry registry = new();
    private readonly IHistoryService historyService;
    private readonly ILayerService layerService;
    private readonly IRenderService renderService;
    private readonly PenShapeParameter parameter;

    private bool isUndoRedoing;
    private bool isFilteringSelection;
    private bool disposed;

    
    public ObservableCollection<HistoryItem> History => historyService.Items;
    public ObservableCollection<Layer> Layers => layerService.Layers;
    public StrokeCollection Strokes => layerService.AggregatedStrokes;

    public int CurrentHistoryIndex
    {
        get => historyService.CurrentIndex;
        set
        {
            if (historyService.CurrentIndex != value && !isUndoRedoing)
            {
                isUndoRedoing = true;
                historyService.MoveToState(value);
                isUndoRedoing = false;
            }
        }
    }

    public Layer? SelectedLayer
    {
        get => selectedLayer;
        set
        {
            if (!Set(ref selectedLayer, value)) return;
            layerService.SelectedLayer = value;
            UpdatePenProperties();
            RemoveLayerCommand.RaiseCanExecuteChanged();
            MoveLayerUpCommand.RaiseCanExecuteChanged();
            MoveLayerDownCommand.RaiseCanExecuteChanged();
        }
    }
    private Layer? selectedLayer;

    public BrushType SelectedBrushType
    {
        get => PenSettings.Default.SelectedBrushType;
        set
        {
            if (PenSettings.Default.SelectedBrushType == value) return;
            PenSettings.Default.SelectedBrushType = value;
            OnPropertyChanged();
            UpdatePenProperties();
        }
    }

    public ToolbarLayout ToolbarLayout
    {
        get => PenSettings.Default.ToolbarLayout;
        set
        {
            if (PenSettings.Default.ToolbarLayout == value) return;
            PenSettings.Default.ToolbarLayout = value;
            OnPropertyChanged();
        }
    }

    public DrawingAttributes Pen => DrawingAttributesFactory.Create(SelectedBrushType, GetCurrentBrushSettings()!);

    public InkCanvasEditingMode EditingMode
    {
        get => editingMode;
        set
        {
            if (!Set(ref editingMode, value)) return;
            OnPropertyChanged(nameof(IsColorPickerEnabled));
            OnPropertyChanged(nameof(IsSelectMode));
        }
    }
    private InkCanvasEditingMode editingMode = InkCanvasEditingMode.Ink;

    public bool IsSelectMode
    {
        get => EditingMode == InkCanvasEditingMode.Select;
        set
        {
            if (value) EditingMode = InkCanvasEditingMode.Select;
            else if (EditingMode == InkCanvasEditingMode.Select) UpdatePenProperties();
        }
    }

    public Color StrokeColor
    {
        get => GetCurrentBrushSettings()?.StrokeColor ?? Colors.Transparent;
        set
        {
            var s = GetCurrentBrushSettings();
            if (s is null) return;
            s.StrokeColor = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Pen));
        }
    }

    public double StrokeThickness
    {
        get => GetCurrentBrushSettings()?.StrokeThickness ?? 0;
        set
        {
            var s = GetCurrentBrushSettings();
            if (s is null) return;
            s.StrokeThickness = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Pen));
        }
    }

    public bool IsColorPickerEnabled =>
        SelectedBrushType != BrushType.Eraser && EditingMode != InkCanvasEditingMode.Select;

    public BitmapSource Bitmap { get => bitmap; private set => Set(ref bitmap, value); }
    private BitmapSource bitmap = null!;

    public Size CanvasSize { get; }

    public bool IsCanvasVisible { get => isCanvasVisible; set => Set(ref isCanvasVisible, value); }
    private bool isCanvasVisible = true;

    public bool IsLayersVisible { get => isLayersVisible; set => Set(ref isLayersVisible, value); }
    private bool isLayersVisible = true;

    public bool IsHistoryVisible { get => isHistoryVisible; set => Set(ref isHistoryVisible, value); }
    private bool isHistoryVisible = true;

    public bool IsCanvasControlPanelVisible { get => isCanvasControlPanelVisible; set => Set(ref isCanvasControlPanelVisible, value); }
    private bool isCanvasControlPanelVisible = true;

    public double CanvasScale { get => canvasScale; set => Set(ref canvasScale, Math.Clamp(value, 0.1, 10)); }
    private double canvasScale = 1.0;

    public double CanvasAngle { get => canvasAngle; set => Set(ref canvasAngle, value); }
    private double canvasAngle;

    public double CanvasTranslateX { get => canvasTranslateX; set => Set(ref canvasTranslateX, value); }
    private double canvasTranslateX;

    public double CanvasTranslateY { get => canvasTranslateY; set => Set(ref canvasTranslateY, value); }
    private double canvasTranslateY;

    public Action? FitToViewAction
    {
        get => fitToViewAction;
        set
        {
            if (!Set(ref fitToViewAction, value)) return;
            FitToViewCommand.RaiseCanExecuteChanged();
        }
    }
    private Action? fitToViewAction;

    public ActionCommand UndoCommand { get; }
    public ActionCommand RedoCommand { get; }
    public ActionCommand AddLayerCommand { get; }
    public ActionCommand RemoveLayerCommand { get; }
    public ActionCommand MoveLayerUpCommand { get; }
    public ActionCommand MoveLayerDownCommand { get; }
    public ActionCommand ShowLayerPropertiesCommand { get; }
    public ActionCommand FitToViewCommand { get; }
    public ICommand ZoomCommand { get; }
    public ICommand ResetRotationCommand { get; }
    public ICommand SwitchEraserModeCommand { get; }
    public ICommand TogglePanelVisibilityCommand { get; }
    public ICommand SaveImageCommand { get; }
    public ICommand ExportIsfCommand { get; }
    public ICommand ImportIsfCommand { get; }

    public PenToolViewModel(PenShapeParameter parameter, IEditorInfo info, IEnumerable<Layer> initialLayers)
    {
        this.parameter = parameter;

        CanvasSize = new Size(info.VideoInfo.Width, info.VideoInfo.Height);

        historyService = new HistoryService();
        layerService = new LayerService(CanvasSize);
        renderService = new RenderService(parameter, info);

        registry.RegisterSingleton<IHistoryService>(historyService);
        registry.RegisterSingleton<ILayerService>(layerService);
        registry.RegisterSingleton<IRenderService>(renderService);

        historyService.StateChanged += OnHistoryStateChanged;

        UndoCommand = new ActionCommand(_ => historyService.CanUndo, _ => ExecuteUndo());
        RedoCommand = new ActionCommand(_ => historyService.CanRedo, _ => ExecuteRedo());
        AddLayerCommand = new ActionCommand(_ => true, _ => AddLayer());
        RemoveLayerCommand = new ActionCommand(_ => Layers.Count > 1 && SelectedLayer is not null, _ => RemoveSelectedLayer());
        MoveLayerUpCommand = new ActionCommand(_ => CanMoveLayerUp(), _ => MoveLayerUp());
        MoveLayerDownCommand = new ActionCommand(_ => CanMoveLayerDown(), _ => MoveLayerDown());
        ShowLayerPropertiesCommand = new ActionCommand(_ => true, p => { if (p is Layer l) ShowLayerProperties(l); });
        FitToViewCommand = new ActionCommand(_ => FitToViewAction is not null, _ => FitToViewAction?.Invoke());
        ResetRotationCommand = new ActionCommand(_ => true, _ => CanvasAngle = 0);

        ZoomCommand = new ActionCommand(_ => true, p =>
        {
            if (p is string dir)
            {
                CanvasScale = dir == "In" ? CanvasScale * 1.2 : CanvasScale / 1.2;
            }
        });

        SwitchEraserModeCommand = new ActionCommand(_ => true, p =>
        {
            if (p is EraserMode mode)
            {
                PenSettings.Default.EraserStyle.Mode = mode;
                UpdatePenProperties();
            }
        });

        TogglePanelVisibilityCommand = new ActionCommand(_ => true, p =>
        {
            if (p is not string name) return;
            switch (name)
            {
                case "CanvasPanel": IsCanvasVisible = !IsCanvasVisible; break;
                case "LayersPanel": IsLayersVisible = !IsLayersVisible; break;
                case "CanvasControlPanel": IsCanvasControlPanelVisible = !IsCanvasControlPanelVisible; break;
                case "HistoryPanel": IsHistoryVisible = !IsHistoryVisible; break;
            }
        });

        SaveImageCommand = new ActionCommand(_ => true, _ => SaveImage());
        ExportIsfCommand = new ActionCommand(_ => true, _ => ExportIsf());
        ImportIsfCommand = new ActionCommand(_ => true, _ => ImportIsf());

        InitializeLayers(initialLayers);
        Strokes.StrokesChanged += OnAggregatedStrokesChanged;
        SafeRefreshStrokes();
        UpdatePenProperties();
        LoadLayout();
        RenumberLayers();

        PenSettings.Default.PenStyle.PropertyChanged += OnBrushSettingsChanged;
        PenSettings.Default.HighlighterStyle.PropertyChanged += OnBrushSettingsChanged;
        PenSettings.Default.PencilStyle.PropertyChanged += OnBrushSettingsChanged;

        Render();
    }

    private void InitializeLayers(IEnumerable<Layer> initial)
    {
        foreach (var src in initial)
        {
            var layer = new Layer(src.Name)
            {
                IsVisible = src.IsVisible,
                IsLocked = src.IsLocked,
                Opacity = src.Opacity,
                SerializableStrokes = src.SerializableStrokes,
            };
            Layers.Add(layer);
            SubscribeLayer(layer);
            layerService.UpdateThumbnail(layer);
            layerService.UpdatePreviewThumbnail(layer);
        }
        SelectedLayer = Layers.FirstOrDefault();
    }

    private void SubscribeLayer(Layer layer) =>
        ((INotifyPropertyChanged)layer).PropertyChanged += OnLayerPropertyChanged;

    private void UnsubscribeLayer(Layer layer) =>
        ((INotifyPropertyChanged)layer).PropertyChanged -= OnLayerPropertyChanged;

    private void SafeRefreshStrokes()
    {
        Strokes.StrokesChanged -= OnAggregatedStrokesChanged;
        try
        {
            isUndoRedoing = true;
            layerService.RefreshVisibleStrokes();
        }
        finally
        {
            isUndoRedoing = false;
            Strokes.StrokesChanged += OnAggregatedStrokesChanged;
        }
    }

    private void OnLayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (isUndoRedoing) return;
        if (e.PropertyName is not (nameof(Layer.IsVisible) or nameof(Layer.Opacity))) return;
        SafeRefreshStrokes();
        if (sender is Layer layer) layerService.UpdatePreviewThumbnail(layer);
    }

    private void OnBrushSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BrushSettingsBase.IsPressure))
        {
            OnPropertyChanged(nameof(Pen));
        }
    }

    private void OnHistoryStateChanged()
    {
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CurrentHistoryIndex));
    }

    private BrushSettingsBase? GetCurrentBrushSettings() => SelectedBrushType switch
    {
        BrushType.Pen => PenSettings.Default.PenStyle,
        BrushType.Highlighter => PenSettings.Default.HighlighterStyle,
        BrushType.Pencil => PenSettings.Default.PencilStyle,
        BrushType.Eraser => PenSettings.Default.EraserStyle,
        _ => null,
    };

    private void UpdatePenProperties()
    {
        EditingMode = SelectedBrushType switch
        {
            BrushType.Eraser => PenSettings.Default.EraserStyle.Mode switch
            {
                EraserMode.Line => InkCanvasEditingMode.EraseByStroke,
                EraserMode.Point => InkCanvasEditingMode.EraseByPoint,
                _ => InkCanvasEditingMode.None,
            },
            _ => InkCanvasEditingMode.Ink,
        };

        if (SelectedLayer?.IsLocked ?? false) EditingMode = InkCanvasEditingMode.None;

        OnPropertyChanged(nameof(StrokeThickness));
        OnPropertyChanged(nameof(StrokeColor));
        OnPropertyChanged(nameof(Pen));
        OnPropertyChanged(nameof(IsColorPickerEnabled));
    }

    private void ExecuteUndo()
    {
        isUndoRedoing = true;
        historyService.Undo();
        isUndoRedoing = false;
    }

    private void ExecuteRedo()
    {
        isUndoRedoing = true;
        historyService.Redo();
        isUndoRedoing = false;
    }

    public void OnAggregatedStrokesChanged(object? sender, StrokeCollectionChangedEventArgs e)
    {
        if (isUndoRedoing) return;

        var added = new StrokeCollection(e.Added);
        var removed = new StrokeCollection(e.Removed);
        if (added.Count == 0 && removed.Count == 0) return;

        var targetLayer = SelectedLayer;
        if (targetLayer is null || targetLayer.IsLocked)
        {
            isUndoRedoing = true;
            if (added.Count > 0) Strokes.Remove(added);
            if (removed.Count > 0) Strokes.Add(removed);
            isUndoRedoing = false;
            return;
        }

        var removedOriginals = new Dictionary<Layer, StrokeCollection>();
        var restore = new StrokeCollection();

        foreach (var stroke in removed)
        {
            if (!layerService.StrokeLayerMap.TryGetValue(stroke, out var layer) ||
                !layerService.DisplayToOriginalMap.TryGetValue(stroke, out var original))
                continue;

            if (layer != targetLayer || layer.IsLocked)
            {
                restore.Add(stroke);
            }
            else
            {
                if (!removedOriginals.ContainsKey(layer))
                    removedOriginals[layer] = [];
                removedOriginals[layer].Add(original);
            }
        }

        if (restore.Count > 0)
        {
            isUndoRedoing = true;
            Strokes.Add(restore);
            isUndoRedoing = false;
        }

        if (added.Count == 0 && removedOriginals.Count == 0) return;

        var addedOriginals = new StrokeCollection(added.Select(static s => s.Clone()));
        var affected = new HashSet<Layer>(removedOriginals.Keys);
        if (addedOriginals.Count > 0) affected.Add(targetLayer);

        Action redo = () =>
        {
            isUndoRedoing = true;
            try
            {
                foreach (var (layer, strokes) in removedOriginals)
                    layer.Strokes.Remove(strokes);
                if (addedOriginals.Count > 0)
                    targetLayer.Strokes.Add(addedOriginals);
            }
            finally
            {
                isUndoRedoing = false;
            }
            SafeRefreshStrokes();
            foreach (var layer in affected)
            {
                layerService.UpdateThumbnail(layer);
                layerService.UpdatePreviewThumbnail(layer);
            }
        };

        Action undo = () =>
        {
            isUndoRedoing = true;
            try
            {
                if (addedOriginals.Count > 0)
                    targetLayer.Strokes.Remove(addedOriginals);
                foreach (var (layer, strokes) in removedOriginals)
                    layer.Strokes.Add(strokes);
            }
            finally
            {
                isUndoRedoing = false;
            }
            SafeRefreshStrokes();
            foreach (var layer in affected)
            {
                layerService.UpdateThumbnail(layer);
                layerService.UpdatePreviewThumbnail(layer);
            }
        };

        var kind = EditingMode switch
        {
            InkCanvasEditingMode.EraseByPoint or InkCanvasEditingMode.EraseByStroke => HistoryKind.Erase,
            InkCanvasEditingMode.Select => HistoryKind.Select,
            _ => HistoryKind.Draw,
        };

        isUndoRedoing = true;
        historyService.Push(kind, undo, redo);
        isUndoRedoing = false;
        redo();
    }

    public void FilterSelection(StrokeCollection selected, Action<StrokeCollection> update)
    {
        if (isFilteringSelection || EditingMode != InkCanvasEditingMode.Select || SelectedLayer is null) return;
        isFilteringSelection = true;

        var valid = new StrokeCollection();
        foreach (var stroke in selected)
        {
            if (layerService.StrokeLayerMap.TryGetValue(stroke, out var layer) && layer == SelectedLayer)
                valid.Add(stroke);
        }

        if (valid.Count != selected.Count)
            update(valid);

        isFilteringSelection = false;
    }

    public void RotateCanvas(double angle) => CanvasAngle = angle;

    public void PanCanvas(double dx, double dy)
    {
        CanvasTranslateX += dx;
        CanvasTranslateY += dy;
    }

    [MemberNotNull(nameof(bitmap))]
    public void Render() => bitmap = Bitmap = renderService.Render();

    public StrokeCollection GetOriginalStrokes(StrokeCollection displayStrokes)
    {
        var originals = new StrokeCollection();
        foreach (var s in displayStrokes)
        {
            if (layerService.DisplayToOriginalMap.TryGetValue(s, out var orig))
                originals.Add(orig);
        }
        return originals;
    }

    public void AddTransformUndo(StrokeCollection originalLayerStrokes, StrokeCollection transformedDisplay, HistoryKind kind)
    {
        if (SelectedLayer is not { } layer) return;
        if (!originalLayerStrokes.All(layer.Strokes.Contains)) return;

        var transformed = new StrokeCollection();
        if (originalLayerStrokes.Count == transformedDisplay.Count)
        {
            for (var i = 0; i < originalLayerStrokes.Count; i++)
            {
                var s = transformedDisplay[i].Clone();
                s.DrawingAttributes = originalLayerStrokes[i].DrawingAttributes.Clone();
                transformed.Add(s);
            }
        }
        else
        {
            transformed = transformedDisplay.Clone();
        }

        Action redo = () =>
        {
            isUndoRedoing = true;
            try
            {
                layer.Strokes.Remove(originalLayerStrokes);
                layer.Strokes.Add(transformed);
            }
            finally
            {
                isUndoRedoing = false;
            }
            SafeRefreshStrokes();
            layerService.UpdateThumbnail(layer);
            layerService.UpdatePreviewThumbnail(layer);
        };

        Action undo = () =>
        {
            isUndoRedoing = true;
            try
            {
                layer.Strokes.Remove(transformed);
                layer.Strokes.Add(originalLayerStrokes);
            }
            finally
            {
                isUndoRedoing = false;
            }
            SafeRefreshStrokes();
            layerService.UpdateThumbnail(layer);
            layerService.UpdatePreviewThumbnail(layer);
        };

        isUndoRedoing = true;
        historyService.Push(kind, undo, redo);
        isUndoRedoing = false;
        redo();
    }

    public void AddLayerPropertyChangedUndo(Layer layer, string propertyName, object oldValue, object newValue)
    {
        var prop = typeof(Layer).GetProperty(propertyName);
        if (prop is null) return;

        isUndoRedoing = true;
        historyService.Push(
            HistoryKind.LayerProperty,
            () =>
            {
                isUndoRedoing = true;
                try { prop.SetValue(layer, oldValue); }
                finally { isUndoRedoing = false; }
                SafeRefreshStrokes();
                layerService.UpdatePreviewThumbnail(layer);
            },
            () =>
            {
                isUndoRedoing = true;
                try { prop.SetValue(layer, newValue); }
                finally { isUndoRedoing = false; }
                SafeRefreshStrokes();
                layerService.UpdatePreviewThumbnail(layer);
            });
        isUndoRedoing = false;
    }

    public void ToggleLayerVisibility(Layer layer)
    {
        var oldValue = layer.IsVisible;
        var newValue = !oldValue;
        isUndoRedoing = true;
        try { layer.IsVisible = newValue; }
        finally { isUndoRedoing = false; }
        SafeRefreshStrokes();
        layerService.UpdatePreviewThumbnail(layer);
        AddLayerPropertyChangedUndo(layer, nameof(Layer.IsVisible), oldValue, newValue);
    }

    private void RenumberLayers()
    {
        for (var i = 0; i < Layers.Count; i++)
        {
            var layer = Layers[i];
            if (string.IsNullOrEmpty(layer.Name) || DefaultLayerNamePattern.IsMatch(layer.Name))
            {
                layer.Name = string.Format(Texts.LayerNameFormat, Layers.Count - i);
            }
        }
    }

    private void AddLayer()
    {
        var layer = new Layer("");

        Action redo = () =>
        {
            isUndoRedoing = true;
            try
            {
                if (!Layers.Contains(layer)) Layers.Insert(0, layer);
                SubscribeLayer(layer);
                SelectedLayer = layer;
                RenumberLayers();
            }
            finally
            {
                isUndoRedoing = false;
            }
            layerService.UpdateThumbnail(layer);
            layerService.UpdatePreviewThumbnail(layer);
            SafeRefreshStrokes();
        };

        Action undo = () =>
        {
            isUndoRedoing = true;
            try
            {
                UnsubscribeLayer(layer);
                Layers.Remove(layer);
                SelectedLayer = Layers.FirstOrDefault();
                RenumberLayers();
            }
            finally
            {
                isUndoRedoing = false;
            }
            SafeRefreshStrokes();
        };

        isUndoRedoing = true;
        historyService.Push(HistoryKind.AddLayer, undo, redo);
        isUndoRedoing = false;
        redo();
    }

    private void RemoveSelectedLayer()
    {
        if (SelectedLayer is null || Layers.Count <= 1) return;

        var target = SelectedLayer;
        var index = Layers.IndexOf(target);

        Action redo = () =>
        {
            isUndoRedoing = true;
            try
            {
                UnsubscribeLayer(target);
                Layers.Remove(target);
                SelectedLayer = Layers.Count > index ? Layers[index] : Layers.FirstOrDefault();
                RenumberLayers();
            }
            finally
            {
                isUndoRedoing = false;
            }
            SafeRefreshStrokes();
        };

        Action undo = () =>
        {
            isUndoRedoing = true;
            try
            {
                Layers.Insert(index, target);
                SubscribeLayer(target);
                SelectedLayer = target;
                RenumberLayers();
            }
            finally
            {
                isUndoRedoing = false;
            }
            SafeRefreshStrokes();
            layerService.UpdateThumbnail(target);
            layerService.UpdatePreviewThumbnail(target);
        };

        isUndoRedoing = true;
        historyService.Push(HistoryKind.RemoveLayer, undo, redo);
        isUndoRedoing = false;
        redo();
    }

    public void MoveLayer(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Layers.Count || newIndex < 0 || newIndex >= Layers.Count) return;

        isUndoRedoing = true;
        historyService.Push(HistoryKind.MoveLayer,
            () =>
            {
                isUndoRedoing = true;
                try { Layers.Move(newIndex, oldIndex); RenumberLayers(); }
                finally { isUndoRedoing = false; }
                SafeRefreshStrokes();
            },
            () =>
            {
                isUndoRedoing = true;
                try { Layers.Move(oldIndex, newIndex); RenumberLayers(); }
                finally { isUndoRedoing = false; }
                SafeRefreshStrokes();
            });
        isUndoRedoing = false;

        isUndoRedoing = true;
        try { Layers.Move(oldIndex, newIndex); RenumberLayers(); }
        finally { isUndoRedoing = false; }
        SafeRefreshStrokes();
    }

    private bool CanMoveLayerUp() => SelectedLayer is not null && Layers.IndexOf(SelectedLayer) > 0;
    private bool CanMoveLayerDown() => SelectedLayer is not null && Layers.IndexOf(SelectedLayer) < Layers.Count - 1;

    private void MoveLayerUp()
    {
        if (!CanMoveLayerUp()) return;
        var index = Layers.IndexOf(SelectedLayer!);
        MoveLayer(index, index - 1);
        MoveLayerUpCommand.RaiseCanExecuteChanged();
        MoveLayerDownCommand.RaiseCanExecuteChanged();
    }

    private void MoveLayerDown()
    {
        if (!CanMoveLayerDown()) return;
        var index = Layers.IndexOf(SelectedLayer!);
        MoveLayer(index, index + 1);
        MoveLayerUpCommand.RaiseCanExecuteChanged();
        MoveLayerDownCommand.RaiseCanExecuteChanged();
    }

    private void ShowLayerProperties(Layer layer)
    {
        var original = layer.Opacity;
        var view = new Views.LayerPropertiesView
        {
            Owner = Application.Current.Windows.OfType<Views.PenToolView>().FirstOrDefault(),
            DataContext = layer,
        };

        if (view.ShowDialog() == true && Math.Abs(original - layer.Opacity) > 1e-9)
        {
            AddLayerPropertyChangedUndo(layer, nameof(Layer.Opacity), original, layer.Opacity);
        }
        else
        {
            layer.Opacity = original;
        }
    }

    private void SaveImage()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "PNG|*.png", DefaultExt = ".png" };
        if (dialog.ShowDialog() != true) return;

        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, CanvasSize.Width, CanvasSize.Height));
            foreach (var layer in Layers.Reverse())
            {
                if (!layer.IsVisible) continue;
                ctx.PushOpacity(layer.Opacity);
                layer.Strokes.Draw(ctx);
                ctx.Pop();
            }
        }

        var rtb = new RenderTargetBitmap((int)CanvasSize.Width, (int)CanvasSize.Height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        using var stream = new System.IO.FileStream(dialog.FileName, System.IO.FileMode.Create);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        encoder.Save(stream);
    }

    private void ExportIsf()
    {
        if (SelectedLayer is null) return;
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Ink Serialized Format|*.isf", DefaultExt = ".isf" };
        if (dialog.ShowDialog() != true) return;

        using var stream = new System.IO.FileStream(dialog.FileName, System.IO.FileMode.Create);
        SelectedLayer.Strokes.Save(stream);
    }

    private void ImportIsf()
    {
        if (SelectedLayer is null || SelectedLayer.IsLocked) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Ink Serialized Format|*.isf", DefaultExt = ".isf" };
        if (dialog.ShowDialog() != true) return;

        using var stream = new System.IO.FileStream(dialog.FileName, System.IO.FileMode.Open);
        try
        {
            var imported = new StrokeCollection(stream);
            SelectedLayer.Strokes.Add(imported);
        }
        catch
        {
        }
    }

    public void LoadLayout()
    {
        var layout = PenSettings.Default.Layout;
        if (layout.TryGetValue("CanvasPanel", out var c)) IsCanvasVisible = c.IsVisible;
        if (layout.TryGetValue("LayersPanel", out var l)) IsLayersVisible = l.IsVisible;
        if (layout.TryGetValue("CanvasControlPanel", out var cc)) IsCanvasControlPanelVisible = cc.IsVisible;
        if (layout.TryGetValue("HistoryPanel", out var h)) IsHistoryVisible = h.IsVisible;
    }

    public void SaveLayout(Dictionary<string, PanelLayoutInfo> layout)
    {
        if (layout.TryGetValue("CanvasPanel", out var c)) c.IsVisible = IsCanvasVisible;
        if (layout.TryGetValue("LayersPanel", out var l)) l.IsVisible = IsLayersVisible;
        if (layout.TryGetValue("CanvasControlPanel", out var cc)) cc.IsVisible = IsCanvasControlPanelVisible;
        if (layout.TryGetValue("HistoryPanel", out var h)) h.IsVisible = IsHistoryVisible;
        PenSettings.Default.Layout = layout;
        PenSettings.Default.Save();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        foreach (var layer in Layers) UnsubscribeLayer(layer);
        Strokes.StrokesChanged -= OnAggregatedStrokesChanged;
        historyService.StateChanged -= OnHistoryStateChanged;
        PenSettings.Default.PenStyle.PropertyChanged -= OnBrushSettingsChanged;
        PenSettings.Default.HighlighterStyle.PropertyChanged -= OnBrushSettingsChanged;
        PenSettings.Default.PencilStyle.PropertyChanged -= OnBrushSettingsChanged;
        registry.Dispose();
        GC.SuppressFinalize(this);
    }
}

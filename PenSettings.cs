using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Shape.Pen.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PanelLayoutInfo
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsVisible { get; set; } = true;
        public int ZIndex { get; set; } = 0;
        public bool IsTranslucent { get; set; } = false;
        public bool IsAlwaysOnTop { get; set; } = false;
    }

    internal class PenSettings : SettingsBase<PenSettings>
    {
        public override SettingsCategory Category => SettingsCategory.None;

        public override string Name => "拡張ペンツール";

        public override bool HasSettingView => false;

        public override object SettingView => throw new NotImplementedException();

        public BrushType SelectedBrushType { get => selectedBrushType; set => Set(ref selectedBrushType, value); }
        private BrushType selectedBrushType = BrushType.Pen;

        public MouseWheelAction MouseWheelAction { get => mouseWheelAction; set => Set(ref mouseWheelAction, value); }
        private MouseWheelAction mouseWheelAction = MouseWheelAction.PenSize;



        public ToolbarLayout ToolbarLayout { get => toolbarLayout; set => Set(ref toolbarLayout, value); }
        private ToolbarLayout toolbarLayout = ToolbarLayout.Top;

        public PenStyleSettings PenStyle { get; } = new() { StrokeColor = Colors.White, StrokeThickness = 10 };

        public PenStyleSettings HighlighterStyle { get; } = new() { StrokeColor = Colors.Yellow, StrokeThickness = 20 };

        public PencilBrushSettings PencilStyle { get; } = new();

        public EraserStyleSettings EraserStyle { get; } = new();

        public Dictionary<string, PanelLayoutInfo> Layout { get; set; } = new();
        private static string GetPluginDirectory()
        {
            var asm = Assembly.GetExecutingAssembly();
            var dir = Path.GetDirectoryName(asm.Location);
            return string.IsNullOrEmpty(dir) ? AppDomain.CurrentDomain.BaseDirectory : dir;
        }

        private static string FilePath => Path.Combine(GetPluginDirectory(), "YukkuriMovieMaker.Plugin.Community.Shape.Pen.settings.json");


        public override void Initialize()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    var json = File.ReadAllText(FilePath);
                    var loaded = JsonSerializer.Deserialize<PenSettings>(json);
                    if (loaded != null)
                    {
                        SelectedBrushType = loaded.SelectedBrushType;
                        MouseWheelAction = loaded.MouseWheelAction;
                        ToolbarLayout = loaded.ToolbarLayout;
                        PenStyle.StrokeColor = loaded.PenStyle.StrokeColor;
                        PenStyle.StrokeThickness = loaded.PenStyle.StrokeThickness;
                        PenStyle.IsPressure = loaded.PenStyle.IsPressure;
                        HighlighterStyle.StrokeColor = loaded.HighlighterStyle.StrokeColor;
                        HighlighterStyle.StrokeThickness = loaded.HighlighterStyle.StrokeThickness;
                        HighlighterStyle.IsPressure = loaded.HighlighterStyle.IsPressure;
                        PencilStyle.StrokeColor = loaded.PencilStyle.StrokeColor;
                        PencilStyle.StrokeThickness = loaded.PencilStyle.StrokeThickness;
                        PencilStyle.IsPressure = loaded.PencilStyle.IsPressure;
                        EraserStyle.StrokeThickness = loaded.EraserStyle.StrokeThickness;
                        EraserStyle.Mode = loaded.EraserStyle.Mode;
                        Layout = loaded.Layout ?? new Dictionary<string, PanelLayoutInfo>();
                    }
                }
                catch { }
            }
        }

        public new void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}
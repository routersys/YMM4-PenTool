using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows.Ink;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Layer
{
    internal class Layer : Bindable
    {
        public string Name { get => name; set => Set(ref name, value); }
        private string name = "新規レイヤー";

        public bool IsVisible { get => isVisible; set => Set(ref isVisible, value); }
        private bool isVisible = true;

        public bool IsLocked { get => isLocked; set => Set(ref isLocked, value); }
        private bool isLocked = false;

        public double Opacity { get => opacity; set => Set(ref opacity, Math.Clamp(value, 0.0, 1.0)); }
        private double opacity = 1.0;

        [Newtonsoft.Json.JsonIgnore]
        public StrokeCollection Strokes { get; } = [];

        [Newtonsoft.Json.JsonIgnore]
        public BitmapSource? Thumbnail { get => thumbnail; set => Set(ref thumbnail, value); }
        private BitmapSource? thumbnail;


        public List<SerializableStroke> SerializableStrokes
        {
            get => Strokes.Select(s => new SerializableStroke(s)).ToList();
            set
            {
                Strokes.Clear();
                if (value != null)
                {
                    Strokes.Add(new StrokeCollection(value.Select(ss => ss.ToStroke())));
                }
            }
        }

        public Layer(string name)
        {
            Name = name;
        }

        public Layer() { }
    }
}
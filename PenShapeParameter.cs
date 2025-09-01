using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Shape.Pen.Layer;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Resources.Localization;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenShapeParameter : ShapeParameterBase
    {
        [Display(Name = "ペンツールで編集", Description = "拡張ペンツールウィンドウを開いて描画を編集します。")]
        [OpenPenToolButton]
        public ImmutableList<Layer.Layer> Layers { get => layers; set => Set(ref layers, value); }
        private ImmutableList<Layer.Layer> layers = [new Layer.Layer("レイヤー 1")];

        [Display(Name = "太さ", Description = "描画全体の太さを変更します。")]
        [AnimationSlider("F1", "%", 0.1d, 200)]
        public Animation Thickness { get; } = new Animation(100, 0.1, YMM4Constants.VeryLargeValue);

        [Display(Name = "長さ", Description = "描画のアニメーション（始点から終点まで）を制御します。")]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Length { get; } = new Animation(100, 0, 100);

        [Display(Name = "オフセット", Description = "描画開始位置をずらします。")]
        [AnimationSlider("F1", "%", -100, 100)]
        public Animation Offset { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        public bool IsEditing { get => isEditing; set => Set(ref isEditing, value); }
        private bool isEditing = false;

        [Display(GroupName = " ", Name = "", Order = -1)]
        [UpdateCheckPanelEditor]
        public bool UpdateCheckPlaceholder { get; set; }

        public PenShapeParameter() : base() { }

        public PenShapeParameter(SharedDataStore? sharedData) : base(sharedData) { }

        public override IEnumerable<string> CreateMaskExoFilter(int keyFrameIndex, ExoOutputDescription desc, ShapeMaskExoOutputDescription shapeMaskParameters)
        {
            return [];
        }

        public override IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc)
        {
            return [];
        }

        public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices)
        {
            return new PenShapeSource(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Thickness, Length, Offset];

        protected override void LoadSharedData(SharedDataStore store)
        {
            var data = store.Load<SharedData>();
            if (data is null)
                return;
            data.ApplyTo(this);
        }

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new SharedData(this));
        }

        private class SharedData
        {
            public ImmutableList<Layer.Layer> Layers { get; set; } = [new Layer.Layer("レイヤー 1")];
            public Animation Thickness { get; } = new Animation(100, 0.1, YMM4Constants.VeryLargeValue);
            public Animation Length { get; } = new Animation(100, 0, 100);
            public Animation Offset { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

            public SharedData() { }
            public SharedData(PenShapeParameter parameter)
            {
                Layers = parameter.Layers;
                Thickness.CopyFrom(parameter.Thickness);
                Length.CopyFrom(parameter.Length);
                Offset.CopyFrom(parameter.Offset);
            }
            public void ApplyTo(PenShapeParameter parameter)
            {
                parameter.Layers = Layers;
                parameter.Thickness.CopyFrom(Thickness);
                parameter.Length.CopyFrom(Length);
                parameter.Offset.CopyFrom(Offset);
            }
        }
    }
}
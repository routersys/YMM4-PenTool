using ExtendedPenTool.Controls;
using ExtendedPenTool.Editors;
using ExtendedPenTool.Models;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace ExtendedPenTool.Plugin;

internal sealed class PenShapeParameter : ShapeParameterBase
{
    [Display(Name = "ペンツールで編集", Description = "拡張ペンツールウィンドウを開いて描画を編集します。")]
    [OpenPenToolButton]
    public ImmutableList<Layer> Layers { get => layers; set => Set(ref layers, value); }
    private ImmutableList<Layer> layers = [new Layer("レイヤー 1")];

    [Display(Name = "太さ", Description = "描画全体の太さを変更します。")]
    [AnimationSlider("F1", "%", 0.1d, 200)]
    public Animation Thickness { get; } = new(100, 0.1, YMM4Constants.VeryLargeValue);

    [Display(Name = "長さ", Description = "描画のアニメーション（始点から終点まで）を制御します。")]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Length { get; } = new(100, 0, 100);

    [Display(Name = "オフセット", Description = "描画開始位置をずらします。")]
    [AnimationSlider("F1", "%", -100, 100)]
    public Animation Offset { get; } = new(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

    public bool IsEditing { get => isEditing; set => Set(ref isEditing, value); }
    private bool isEditing;

    [Display(GroupName = "Version Check Only", Name = "", Order = -1)]
    [UpdateCheckPanelEditor]
    public bool UpdateCheckPlaceholder { get; set; }

    public PenShapeParameter() { }
    public PenShapeParameter(SharedDataStore? sharedData) : base(sharedData) { }

    public override IEnumerable<string> CreateMaskExoFilter(
        int keyFrameIndex, ExoOutputDescription desc, ShapeMaskExoOutputDescription shapeMaskParameters) => [];

    public override IEnumerable<string> CreateShapeItemExoFilter(
        int keyFrameIndex, ExoOutputDescription desc) => [];

    public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices) =>
        new PenShapeSource(devices, this);

    protected override IEnumerable<IAnimatable> GetAnimatables() => [Thickness, Length, Offset];

    protected override void LoadSharedData(SharedDataStore store)
    {
        var data = store.Load<SharedData>();
        data?.ApplyTo(this);
    }

    protected override void SaveSharedData(SharedDataStore store) =>
        store.Save(new SharedData(this));

    private sealed class SharedData
    {
        public ImmutableList<Layer> Layers { get; set; } = [new Layer("レイヤー 1")];
        public Animation Thickness { get; } = new(100, 0.1, YMM4Constants.VeryLargeValue);
        public Animation Length { get; } = new(100, 0, 100);
        public Animation Offset { get; } = new(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        public SharedData() { }

        public SharedData(PenShapeParameter p)
        {
            Layers = p.Layers;
            Thickness.CopyFrom(p.Thickness);
            Length.CopyFrom(p.Length);
            Offset.CopyFrom(p.Offset);
        }

        public void ApplyTo(PenShapeParameter p)
        {
            p.Layers = Layers;
            p.Thickness.CopyFrom(Thickness);
            p.Length.CopyFrom(Length);
            p.Offset.CopyFrom(Offset);
        }
    }
}

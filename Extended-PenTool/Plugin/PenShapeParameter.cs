using ExtendedPenTool.Controls;
using ExtendedPenTool.Editors;
using ExtendedPenTool.Models;
using ExtendedPenTool.Localization;
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
    [Display(Name = nameof(Texts.EditWithPenTool), Description = nameof(Texts.EditWithPenToolDescription), ResourceType = typeof(Texts))]
    [OpenPenToolButton]
    public ImmutableList<Layer> Layers { get => layers; set => Set(ref layers, value); }
    private ImmutableList<Layer> layers = [new Layer(string.Format(Texts.LayerNameFormat, 1))];

    [Display(Name = nameof(Texts.Thickness), Description = nameof(Texts.ThicknessDescription), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0.1d, 200)]
    public Animation Thickness { get; } = new(100, 0.1, YMM4Constants.VeryLargeValue);

    [Display(Name = nameof(Texts.Length), Description = nameof(Texts.LengthDescription), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Length { get; } = new(100, 0, 100);

    [Display(Name = nameof(Texts.Offset), Description = nameof(Texts.OffsetDescription), ResourceType = typeof(Texts))]
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
        public ImmutableList<Layer> Layers { get; set; } = [new Layer(string.Format(Texts.LayerNameFormat, 1))];
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

using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace ExtendedPenTool.Plugin;

internal sealed class PenShapePlugin : IShapePlugin
{
    public bool IsExoShapeSupported => false;
    public bool IsExoMaskSupported => false;
    public string Name => "拡張ペンツール";

    public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData) =>
        new PenShapeParameter(sharedData);
}

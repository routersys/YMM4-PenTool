using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

using ExtendedPenTool.Localization;

namespace ExtendedPenTool.Plugin;

internal sealed class PenShapePlugin : IShapePlugin
{
    public bool IsExoShapeSupported => false;
    public bool IsExoMaskSupported => false;
    public string Name => Texts.PluginName;

    public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData) =>
        new PenShapeParameter(sharedData);
}

using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenShapePlugin : IShapePlugin
    {
        public bool IsExoShapeSupported => false;

        public bool IsExoMaskSupported => false;

        public string Name => "拡張ペンツール";

        public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData)
        {
            return new PenShapeParameter(sharedData);
        }
    }
}
using ExtendedPenTool.Infrastructure;
using System.Numerics;
using System.Windows.Ink;
using Vortice.Direct2D1;

namespace ExtendedPenTool.Rendering;

internal sealed class InkStyleResourceManager : IDisposable
{
    private readonly PooledResourceManager<DrawingAttributes, ID2D1InkStyle> pool = new(DrawingAttributesComparer.Instance);

    public ID2D1InkStyle GetInkStyle(ID2D1DeviceContext2 dc, DrawingAttributes attributes)
    {
        return pool.GetOrCreate(attributes, () => CreateInkStyle(dc, attributes));
    }

    private static ID2D1InkStyle CreateInkStyle(ID2D1DeviceContext2 dc, DrawingAttributes attributes)
    {
        var properties = new InkStyleProperties
        {
            NibShape = attributes.StylusTip == StylusTip.Rectangle ? InkNibShape.Square : InkNibShape.Round,
            NibTransform = Matrix3x2.Identity,
        };
        return dc.CreateInkStyle(properties);
    }

    public void BeginFrame() => pool.BeginFrame();
    public void EndFrame() => pool.EndFrame();
    public void Dispose() => pool.Dispose();
}

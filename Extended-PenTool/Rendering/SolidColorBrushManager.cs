using ExtendedPenTool.Infrastructure;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace ExtendedPenTool.Rendering;

internal sealed class SolidColorBrushManager : IDisposable
{
    private readonly PooledResourceManager<Color4, ID2D1SolidColorBrush> pool = new();

    public ID2D1SolidColorBrush GetBrush(ID2D1DeviceContext dc, Color4 color)
    {
        return pool.GetOrCreate(color, () => dc.CreateSolidColorBrush(color));
    }

    public void BeginFrame() => pool.BeginFrame();
    public void EndFrame() => pool.EndFrame();
    public void Dispose() => pool.Dispose();
}

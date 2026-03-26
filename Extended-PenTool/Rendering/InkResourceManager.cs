using ExtendedPenTool.Infrastructure;
using Vortice.Direct2D1;

namespace ExtendedPenTool.Rendering;

internal sealed class InkResourceManager : IDisposable
{
    private readonly PooledResourceManager<InkPoint[], ID2D1Ink> pool = new(InkPointArrayComparer.Instance);

    public ID2D1Ink GetInk(ID2D1DeviceContext2 dc, InkPoint[] points)
    {
        return pool.GetOrCreate(points, () => CreateInk(dc, points));
    }

    private static ID2D1Ink CreateInk(ID2D1DeviceContext2 dc, InkPoint[] points)
    {
        var ink = dc.CreateInk(points[0]);

        if (points.Length > 1)
        {
            var segments = new InkBezierSegment[points.Length - 1];
            for (var i = 0; i < points.Length - 1; i++)
            {
                segments[i] = new InkBezierSegment
                {
                    Point1 = points[i],
                    Point2 = points[i + 1],
                    Point3 = points[i + 1],
                };
            }
            ink.AddSegments(segments, segments.Length);
        }
        else
        {
            var segment = new InkBezierSegment
            {
                Point1 = points[0],
                Point2 = points[0],
                Point3 = points[0],
            };
            ink.AddSegments([segment], 1);
        }

        return ink;
    }

    public void BeginFrame() => pool.BeginFrame();
    public void EndFrame() => pool.EndFrame();
    public void Dispose() => pool.Dispose();
}

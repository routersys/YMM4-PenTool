using ExtendedPenTool.Models;
using ExtendedPenTool.Rendering;
using System.Collections.Immutable;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace ExtendedPenTool.Plugin;

internal sealed class PenShapeSource : IShapeSource
{
    private readonly DisposeCollector disposer = new();
    private readonly InkResourceManager inkManager = new();
    private readonly InkStyleResourceManager inkStyleManager = new();
    private readonly SolidColorBrushManager brushManager = new();

    private readonly IGraphicsDevicesAndContext devices;
    private readonly PenShapeParameter parameter;
    private readonly ID2D1SolidColorBrush transparent;
    private readonly AffineTransform2D transformEffect;

    public ID2D1Image Output { get; }
    private ID2D1CommandList? commandList;

    private bool cachedIsEditing;
    private ImmutableList<Layer> cachedLayers = [];
    private double cachedThickness;
    private int cachedPointFrom;
    private int cachedPointLength;

    public PenShapeSource(IGraphicsDevicesAndContext devices, PenShapeParameter parameter)
    {
        this.devices = devices;
        this.parameter = parameter;

        disposer.Collect(inkManager);
        disposer.Collect(inkStyleManager);
        disposer.Collect(brushManager);

        transparent = devices.DeviceContext.CreateSolidColorBrush(new Color4(0, 0, 0, 0));
        disposer.Collect(transparent);

        transformEffect = new AffineTransform2D(devices.DeviceContext);
        disposer.Collect(transformEffect);

        Output = transformEffect.Output;
        disposer.Collect(Output);
    }

    public void Update(TimelineItemSourceDescription desc)
    {
        var dc = devices.DeviceContext;
        if (dc is not ID2D1DeviceContext2 dc2) return;

        var frame = desc.ItemPosition.Frame;
        var duration = desc.ItemDuration.Frame;
        var fps = desc.FPS;

        var thickness = parameter.Thickness.GetValue(frame, duration, fps);
        var lengthRate = parameter.Length.GetValue(frame, duration, fps);
        var offset = parameter.Offset.GetValue(frame, duration, fps);
        var currentLayers = parameter.Layers;
        var isEditing = parameter.IsEditing;

        var totalPoints = currentLayers
            .Where(static l => l.IsVisible)
            .SelectMany(static l => l.SerializableStrokes)
            .SelectMany(static s => s.ToStroke().GetBezierStylusPoints())
            .Count();

        var doubleTotalPoints = totalPoints > 0 ? totalPoints * 2 : 1;
        var pointFrom = (int)((totalPoints * (offset + 100) / 100 % doubleTotalPoints + doubleTotalPoints) % doubleTotalPoints) - totalPoints;
        var pointLength = (int)(totalPoints * lengthRate / 100);

        if (commandList is not null
            && Math.Abs(cachedThickness - thickness) < 1e-9
            && cachedLayers.SequenceEqual(currentLayers, LayerComparer.Instance)
            && cachedIsEditing == isEditing
            && cachedPointFrom == pointFrom
            && cachedPointLength == pointLength)
        {
            return;
        }

        cachedThickness = thickness;
        cachedLayers = currentLayers;
        cachedIsEditing = isEditing;
        cachedPointFrom = pointFrom;
        cachedPointLength = pointLength;

        inkManager.BeginFrame();
        inkStyleManager.BeginFrame();
        brushManager.BeginFrame();

        if (commandList is not null)
        {
            disposer.RemoveAndDispose(ref commandList);
        }

        commandList = dc.CreateCommandList();
        disposer.Collect(commandList);

        dc.Target = commandList;
        dc.BeginDraw();
        dc.Clear(null);
        dc.DrawRectangle(new Vortice.RawRectF(0, 0, 1, 1), transparent);

        if (!parameter.IsEditing)
        {
            RenderStrokes(dc, dc2, desc, thickness, pointFrom, pointLength);
        }

        dc.EndDraw();
        dc.Target = null;
        commandList.Close();

        transformEffect.SetInput(0, commandList, true);

        inkManager.EndFrame();
        inkStyleManager.EndFrame();
        brushManager.EndFrame();
    }

    private void RenderStrokes(
        ID2D1DeviceContext dc,
        ID2D1DeviceContext2 dc2,
        TimelineItemSourceDescription desc,
        double thickness,
        int pointFrom,
        int pointLength)
    {
        var currentPoint = 0;
        dc.Transform = Matrix3x2.CreateTranslation(-desc.ScreenSize.Width / 2f, -desc.ScreenSize.Height / 2f);

        foreach (var layer in parameter.Layers.Reverse())
        {
            if (!layer.IsVisible) continue;

            var layerOpacity = (float)layer.Opacity;

            foreach (var stroke in layer.SerializableStrokes)
            {
                var stylusPoints = stroke.ToStroke().GetBezierStylusPoints().ToArray();
                var strokeLength = stylusPoints.Length;

                var start = Math.Max(0, pointFrom - currentPoint);
                var end = Math.Min(strokeLength, pointFrom + pointLength - currentPoint);
                currentPoint += strokeLength;

                if (start >= end || end - start < 1) continue;

                var points = new InkPoint[end - start];
                for (var i = start; i < end; i++)
                {
                    var p = stylusPoints[i];
                    points[i - start] = new InkPoint
                    {
                        X = (float)p.X,
                        Y = (float)p.Y,
                        Radius = (float)stroke.DrawingAttributes.Width * p.PressureFactor * (float)thickness / 100f,
                    };
                }

                if (points.Length < 1) continue;

                var ink = inkManager.GetInk(dc2, points);
                var inkStyle = inkStyleManager.GetInkStyle(dc2, stroke.DrawingAttributes);

                var c = stroke.DrawingAttributes.Color;
                Color4 color;

                if (stroke.DrawingAttributes.IsHighlighter)
                {
                    dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
                    color = new Color4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f / 2f * layerOpacity);
                }
                else
                {
                    dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
                    var orig = stroke.DrawingAttributes.Color.ToColor4();
                    color = new Color4(orig.R, orig.G, orig.B, orig.A * layerOpacity);
                }

                var brush = brushManager.GetBrush(dc, color);
                dc2.DrawInk(ink, brush, inkStyle);
            }
        }

        dc.Transform = Matrix3x2.Identity;
    }

    private bool disposed;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        disposer.Dispose();
        GC.SuppressFinalize(this);
    }
}

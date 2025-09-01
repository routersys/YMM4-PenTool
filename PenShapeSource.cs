using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Shape.Pen.Layer;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenShapeSource : IShapeSource
    {
        private readonly DisposeCollector disposer = new();
        private readonly InkResourceManager inkResourceManager = new();
        private readonly InkStyleResourceManager inkStyleResourceManager = new();
        private readonly SolidColorBrushManager solidColorBrushManager = new();

        private readonly IGraphicsDevicesAndContext devices;
        private readonly PenShapeParameter penShapeParameter;

        private readonly ID2D1SolidColorBrush transparent;
        private readonly AffineTransform2D transformEffect;

        public ID2D1Image Output { get; }
        private ID2D1CommandList? commandList;

        private bool isEditing;
        private ImmutableList<Layer.Layer> layers = [];
        private double thickness;
        private int pointFrom, pointLength;

        public PenShapeSource(IGraphicsDevicesAndContext devices, PenShapeParameter penShapeParameter)
        {
            this.devices = devices;
            this.penShapeParameter = penShapeParameter;
            disposer.Collect(inkResourceManager);
            disposer.Collect(inkStyleResourceManager);
            disposer.Collect(solidColorBrushManager);

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

            var thickness = penShapeParameter.Thickness.GetValue(frame, duration, fps);
            var lengthRate = penShapeParameter.Length.GetValue(frame, duration, fps);
            var offset = penShapeParameter.Offset.GetValue(frame, duration, fps);
            var currentLayers = penShapeParameter.Layers;
            var isEditing = penShapeParameter.IsEditing;

            var totalPoints = currentLayers.Where(l => l.IsVisible).SelectMany(l => l.SerializableStrokes).SelectMany(s => s.ToStroke().GetBezierStylusPoints()).Count();
            var doubleTotalPoints = totalPoints > 0 ? totalPoints * 2 : 1;
            var pointFrom = (int)((totalPoints * (offset + 100) / 100 % doubleTotalPoints + doubleTotalPoints) % doubleTotalPoints) - totalPoints;
            var pointLength = (int)(totalPoints * lengthRate / 100);

            if (commandList is not null &&
                this.thickness == thickness &&
                this.layers.SequenceEqual(currentLayers, new LayerComparer()) &&
                this.isEditing == isEditing &&
                this.pointFrom == pointFrom &&
                this.pointLength == pointLength)
                return;

            this.thickness = thickness;
            this.layers = currentLayers;
            this.isEditing = isEditing;
            this.pointFrom = pointFrom;
            this.pointLength = pointLength;

            inkResourceManager.BeginUse();
            inkStyleResourceManager.BeginUse();
            solidColorBrushManager.BeginUse();

            if (commandList is not null)
                disposer.RemoveAndDispose(ref commandList);
            commandList = dc.CreateCommandList();
            disposer.Collect(commandList);

            dc.Target = commandList;
            dc.BeginDraw();
            dc.Clear(null);

            dc.DrawRectangle(new Vortice.RawRectF(0, 0, 1, 1), transparent);

            if (!penShapeParameter.IsEditing)
            {
                int currentPoint = 0;
                dc.Transform = Matrix3x2.CreateTranslation(-desc.ScreenSize.Width / 2f, -desc.ScreenSize.Height / 2f);

                foreach (var layer in penShapeParameter.Layers.Reverse())
                {
                    if (!layer.IsVisible)
                        continue;

                    foreach (var stroke in layer.SerializableStrokes)
                    {
                        var strokePoints = stroke.ToStroke().GetBezierStylusPoints().ToArray();
                        var currentStrokeLength = strokePoints.Length;

                        var start = Math.Max(0, pointFrom - currentPoint);
                        var end = Math.Min(currentStrokeLength, pointFrom + pointLength - currentPoint);

                        currentPoint += currentStrokeLength;

                        if (start >= end)
                            continue;

                        if (end - start < 1) continue;

                        var points = strokePoints
                            .Select(p => new InkPoint()
                            {
                                X = (float)p.X,
                                Y = (float)p.Y,
                                Radius = (float)stroke.DrawingAttributes.Width * p.PressureFactor * (float)thickness / 100f,
                            })
                            .ToArray()[start..end];

                        if (points.Length < 1) continue;

                        var ink = inkResourceManager.GetInk(dc2, points);
                        var inkStyle = inkStyleResourceManager.GetInkStyle(dc2, stroke.DrawingAttributes);

                        Color4 color;
                        var c = stroke.DrawingAttributes.Color;
                        var layerOpacity = (float)layer.Opacity;

                        if (stroke.DrawingAttributes.IsHighlighter)
                        {
                            dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
                            color = new Color4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f / 2f * layerOpacity);
                        }
                        else
                        {
                            dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
                            var originalColor = stroke.DrawingAttributes.Color.ToColor4();
                            color = new Color4(originalColor.R, originalColor.G, originalColor.B, originalColor.A * layerOpacity);
                        }
                        var brush = solidColorBrushManager.GetBrush(dc, color);

                        dc2.DrawInk(ink, brush, inkStyle);
                    }
                }
                dc.Transform = Matrix3x2.Identity;
            }
            dc.EndDraw();
            dc.Target = null;
            commandList.Close();

            transformEffect.SetInput(0, commandList, true);

            inkResourceManager.EndUse();
            inkStyleResourceManager.EndUse();
            solidColorBrushManager.EndUse();
        }

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    disposer.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private class LayerComparer : IEqualityComparer<Layer.Layer>
        {
            public bool Equals(Layer.Layer? x, Layer.Layer? y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null) return false;
                return x.Name == y.Name &&
                       x.IsVisible == y.IsVisible &&
                       x.IsLocked == y.IsLocked &&
                       x.Opacity == y.Opacity &&
                       x.SerializableStrokes.SequenceEqual(y.SerializableStrokes);
            }

            public int GetHashCode(Layer.Layer obj)
            {
                return HashCode.Combine(obj.Name, obj.IsVisible, obj.IsLocked, obj.Opacity, obj.SerializableStrokes);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Ink;
using Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class InkStyleResourceManager : IDisposable
    {
        private class DrawingAttributesComparer : IEqualityComparer<DrawingAttributes>
        {
            public bool Equals(DrawingAttributes? x, DrawingAttributes? y)
            {
                if (x is null && y is null) return true;
                if (x is null || y is null) return false;
                return x.StylusTip == y.StylusTip &&
                       x.Width == y.Width &&
                       x.Height == y.Height;
            }

            public int GetHashCode(DrawingAttributes obj)
            {
                return HashCode.Combine(obj.StylusTip, obj.Width, obj.Height);
            }
        }

        private readonly Dictionary<DrawingAttributes, ResourceItem<ID2D1InkStyle>> styles = new(new DrawingAttributesComparer());

        public ID2D1InkStyle GetInkStyle(ID2D1DeviceContext2 dc, DrawingAttributes attributes)
        {
            if (styles.TryGetValue(attributes, out var item))
            {
                item.IsUsed = true;
                return item.Resource;
            }

            var inkStyleProperties = new InkStyleProperties
            {
                NibShape = attributes.StylusTip == StylusTip.Rectangle ? InkNibShape.Square : InkNibShape.Round,
                NibTransform = Matrix3x2.Identity,
            };

            var inkStyle = dc.CreateInkStyle(inkStyleProperties);
            var newItem = new ResourceItem<ID2D1InkStyle>(inkStyle) { IsUsed = true };
            styles.Add(attributes.Clone(), newItem);
            return inkStyle;
        }

        public void BeginUse()
        {
            foreach (var item in styles.Values)
            {
                item.IsUsed = false;
            }
        }

        public void EndUse()
        {
            var unused = styles.Where(kv => !kv.Value.IsUsed).Select(kv => kv.Key).ToList();
            foreach (var key in unused)
            {
                if (styles.Remove(key, out var item))
                {
                    item.Resource.Dispose();
                }
            }
        }

        public void Dispose()
        {
            foreach (var item in styles.Values)
            {
                item.Resource.Dispose();
            }
            styles.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
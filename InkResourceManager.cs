using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Ink;
using System.Windows.Media;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class InkResourceManager : IDisposable
    {
        private class InkPointArrayComparer : IEqualityComparer<InkPoint[]>
        {
            public bool Equals(InkPoint[]? x, InkPoint[]? y)
            {
                if (x is null && y is null) return true;
                if (x is null || y is null) return false;
                return x.SequenceEqual(y);
            }

            public int GetHashCode(InkPoint[] obj)
            {
                int hash = 17;
                foreach (var point in obj)
                {
                    hash = hash * 31 + point.GetHashCode();
                }
                return hash;
            }
        }

        private readonly Dictionary<InkPoint[], ResourceItem<ID2D1Ink>> inks = new(new InkPointArrayComparer());

        public ID2D1Ink GetInk(ID2D1DeviceContext2 dc, InkPoint[] points)
        {
            if (inks.TryGetValue(points, out var item))
            {
                item.IsUsed = true;
                return item.Resource;
            }

            var ink = dc.CreateInk(points[0]);
            if (points.Length > 1)
            {
                var segments = new InkBezierSegment[points.Length - 1];
                for (int i = 0; i < points.Length - 1; i++)
                {
                    segments[i] = new InkBezierSegment { Point1 = points[i], Point2 = points[i + 1], Point3 = points[i + 1] };
                }
                ink.AddSegments(segments, segments.Length);
            }
            else if (points.Length == 1)
            {
                var segment = new InkBezierSegment { Point1 = points[0], Point2 = points[0], Point3 = points[0] };
                ink.AddSegments(new[] { segment }, 1);
            }

            var newItem = new ResourceItem<ID2D1Ink>(ink) { IsUsed = true };
            inks.Add(points, newItem);
            return ink;
        }


        public void BeginUse()
        {
            foreach (var item in inks.Values)
            {
                item.IsUsed = false;
            }
        }

        public void EndUse()
        {
            var unused = inks.Where(kv => !kv.Value.IsUsed).Select(kv => kv.Key).ToList();
            foreach (var key in unused)
            {
                if (inks.Remove(key, out var item))
                {
                    item.Resource.Dispose();
                }
            }
        }

        public void Dispose()
        {
            foreach (var item in inks.Values)
            {
                item.Resource.Dispose();
            }
            inks.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
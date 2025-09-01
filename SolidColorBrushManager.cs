using System;
using System.Collections.Generic;
using System.Linq;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class SolidColorBrushManager : IDisposable
    {
        private readonly Dictionary<Color4, ResourceItem<ID2D1SolidColorBrush>> brushes = new();

        public ID2D1SolidColorBrush GetBrush(ID2D1DeviceContext dc, Color4 color)
        {
            if (brushes.TryGetValue(color, out var item))
            {
                item.IsUsed = true;
                return item.Resource;
            }

            var brush = dc.CreateSolidColorBrush(color);
            var newItem = new ResourceItem<ID2D1SolidColorBrush>(brush) { IsUsed = true };
            brushes.Add(color, newItem);
            return brush;
        }

        public void BeginUse()
        {
            foreach (var item in brushes.Values)
            {
                item.IsUsed = false;
            }
        }

        public void EndUse()
        {
            var unused = brushes.Where(kv => !kv.Value.IsUsed).Select(kv => kv.Key).ToList();
            foreach (var key in unused)
            {
                if (brushes.Remove(key, out var item))
                {
                    item.Resource.Dispose();
                }
            }
        }

        public void Dispose()
        {
            foreach (var item in brushes.Values)
            {
                item.Resource.Dispose();
            }
            brushes.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
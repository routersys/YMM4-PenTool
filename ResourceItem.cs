using System;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class ResourceItem<T> where T : class, IDisposable
    {
        public T Resource { get; }
        public bool IsUsed { get; set; }

        public ResourceItem(T resource)
        {
            Resource = resource;
            IsUsed = false;
        }
    }
}
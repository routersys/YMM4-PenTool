namespace ExtendedPenTool.Infrastructure;

internal sealed class PooledResourceManager<TKey, TResource> : IDisposable
    where TResource : class, IDisposable
    where TKey : notnull
{
    private readonly Dictionary<TKey, ResourceItem<TResource>> pool;
    private readonly List<TKey> removalBuffer = [];
    private bool disposed;

    public PooledResourceManager(IEqualityComparer<TKey>? comparer = null)
    {
        pool = new Dictionary<TKey, ResourceItem<TResource>>(comparer ?? EqualityComparer<TKey>.Default);
    }

    public TResource GetOrCreate(TKey key, Func<TResource> factory)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (pool.TryGetValue(key, out var item))
        {
            item.IsUsed = true;
            return item.Resource;
        }

        var resource = factory();
        pool[key] = new ResourceItem<TResource>(resource) { IsUsed = true };
        return resource;
    }

    public void BeginFrame()
    {
        foreach (var item in pool.Values)
        {
            item.IsUsed = false;
        }
    }

    public void EndFrame()
    {
        removalBuffer.Clear();
        foreach (var kvp in pool)
        {
            if (!kvp.Value.IsUsed)
            {
                removalBuffer.Add(kvp.Key);
            }
        }

        foreach (var key in removalBuffer)
        {
            if (pool.Remove(key, out var item))
            {
                item.Resource.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        foreach (var item in pool.Values)
        {
            item.Resource.Dispose();
        }
        pool.Clear();
        GC.SuppressFinalize(this);
    }
}

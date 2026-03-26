namespace ExtendedPenTool.Abstractions;

internal interface IResourceManager<TKey, TResource> : IDisposable
    where TResource : class, IDisposable
    where TKey : notnull
{
    TResource GetOrCreate(TKey key, Func<TResource> factory);
    void BeginFrame();
    void EndFrame();
}

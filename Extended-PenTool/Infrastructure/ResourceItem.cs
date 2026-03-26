namespace ExtendedPenTool.Infrastructure;

internal sealed class ResourceItem<T>(T resource) where T : class, IDisposable
{
    public T Resource { get; } = resource;
    public bool IsUsed { get; set; }
}

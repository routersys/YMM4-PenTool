using System.Collections.Concurrent;

namespace ExtendedPenTool.Infrastructure;

internal sealed class ServiceRegistry : IDisposable
{
    private readonly ConcurrentDictionary<Type, object> services = new();
    private readonly ConcurrentDictionary<Type, Func<ServiceRegistry, object>> factories = new();
    private bool disposed;

    public void RegisterSingleton<TService>(TService instance) where TService : class
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        services[typeof(TService)] = instance;
    }

    public void RegisterFactory<TService>(Func<ServiceRegistry, TService> factory) where TService : class
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        factories[typeof(TService)] = r => factory(r);
    }

    public TService Resolve<TService>() where TService : class
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (services.TryGetValue(typeof(TService), out var existing))
        {
            return (TService)existing;
        }

        if (factories.TryGetValue(typeof(TService), out var factory))
        {
            var instance = (TService)factory(this);
            services[typeof(TService)] = instance;
            return instance;
        }

        throw new InvalidOperationException($"Service {typeof(TService).Name} is not registered.");
    }

    public bool TryResolve<TService>(out TService? service) where TService : class
    {
        if (services.TryGetValue(typeof(TService), out var existing))
        {
            service = (TService)existing;
            return true;
        }

        if (factories.TryGetValue(typeof(TService), out var factory))
        {
            service = (TService)factory(this);
            services[typeof(TService)] = service;
            return true;
        }

        service = null;
        return false;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        foreach (var service in services.Values)
        {
            if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        services.Clear();
        factories.Clear();
    }
}

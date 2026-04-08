using System;

namespace Underground.Core.Architecture
{
    public interface IServiceRegistry
    {
        void Register<TService>(TService service) where TService : class;
        void Unregister<TService>(TService service) where TService : class;
        bool TryResolve<TService>(out TService service) where TService : class;
        TService ResolveOrNull<TService>() where TService : class;
        void Clear();
    }
}

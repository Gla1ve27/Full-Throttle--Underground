using UnityEngine;

namespace Underground.Core.Architecture
{
    public static class ServiceResolver
    {
        public static TService Resolve<TService>(TService current) where TService : class
        {
            if (current != null)
            {
                return current;
            }

            TService resolved = ServiceLocator.ResolveOrNull<TService>();
            if (resolved != null)
            {
                return resolved;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(TService)))
            {
                resolved = Object.FindFirstObjectByType(typeof(TService)) as TService;
                if (resolved != null)
                {
                    ServiceLocator.Register(resolved);
                }
            }

            return resolved;
        }
    }
}

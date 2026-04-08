using System;
using System.Collections.Generic;
using UnityEngine;

namespace Underground.Core.Architecture
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> servicesByType = new Dictionary<Type, object>();
        private static readonly EventBus defaultEventBus = new EventBus();

        public static IEventBus EventBus => defaultEventBus;

        public static void Register<TService>(TService service) where TService : class
        {
            if (service == null)
            {
                return;
            }

            servicesByType[typeof(TService)] = service;
        }

        public static void Unregister<TService>(TService service) where TService : class
        {
            Type serviceType = typeof(TService);
            if (!servicesByType.TryGetValue(serviceType, out object existing))
            {
                return;
            }

            if (ReferenceEquals(existing, service))
            {
                servicesByType.Remove(serviceType);
            }
        }

        public static bool TryResolve<TService>(out TService service) where TService : class
        {
            if (servicesByType.TryGetValue(typeof(TService), out object existing) && existing is TService typed)
            {
                service = typed;
                return true;
            }

            service = null;
            return false;
        }

        public static TService ResolveOrNull<TService>() where TService : class
        {
            return TryResolve(out TService service) ? service : null;
        }

        public static TService ResolveOrFind<TService>() where TService : class
        {
            if (TryResolve(out TService service))
            {
                return service;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(TService)))
            {
                TService found = UnityEngine.Object.FindFirstObjectByType(typeof(TService)) as TService;
                if (found != null)
                {
                    Register(found);
                }

                return found;
            }

            return null;
        }

        public static void Reset()
        {
            servicesByType.Clear();
            defaultEventBus.Clear();
        }
    }
}

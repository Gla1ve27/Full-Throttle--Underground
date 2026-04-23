using System;
using System.Collections.Generic;

namespace FullThrottle.SacredCore.Runtime
{
    /// <summary>
    /// Small, explicit service registry for the sacred core.
    /// Keep the number of services low and deliberate.
    /// </summary>
    public static class FTServices
    {
        private static readonly Dictionary<Type, object> Map = new();

        public static void Register<T>(T instance) where T : class
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            Map[typeof(T)] = instance;
        }

        public static bool TryGet<T>(out T instance) where T : class
        {
            if (Map.TryGetValue(typeof(T), out object value) && value is T typed)
            {
                instance = typed;
                return true;
            }

            instance = null;
            return false;
        }

        public static T Get<T>() where T : class
        {
            if (TryGet(out T instance))
            {
                return instance;
            }

            throw new InvalidOperationException($"Full Throttle sacred service missing: {typeof(T).Name}");
        }

        public static void ResetAll() => Map.Clear();
    }
}

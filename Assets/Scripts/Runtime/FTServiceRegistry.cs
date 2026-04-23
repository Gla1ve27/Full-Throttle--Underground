using UnityEngine;

namespace FullThrottle.SacredCore.Runtime
{
    /// <summary>
    /// Scene-visible bridge for the static service map. Keep service ownership explicit.
    /// </summary>
    [DefaultExecutionOrder(-9980)]
    public sealed class FTServiceRegistry : MonoBehaviour
    {
        public static FTServiceRegistry Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            FTServices.Register(this);
            Debug.Log("[SacredCore] Service registry online.");
        }

        public void Register<T>(T service) where T : class
        {
            FTServices.Register(service);
        }

        public bool TryResolve<T>(out T service) where T : class
        {
            return FTServices.TryGet(out service);
        }

        public T Resolve<T>() where T : class
        {
            return FTServices.Get<T>();
        }
    }
}

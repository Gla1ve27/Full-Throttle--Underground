using UnityEngine;

namespace FullThrottle.SacredCore.Runtime
{
    /// <summary>
    /// Persistent runtime root for scenes that do not carry the full bootstrap object.
    /// </summary>
    [DefaultExecutionOrder(-9990)]
    public sealed class FTRuntimeRoot : MonoBehaviour
    {
        public static FTRuntimeRoot Instance { get; private set; }

        [SerializeField] private bool dontDestroyOnLoad = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (!FTServices.TryGet(out FTSignalBus signalBus))
            {
                signalBus = new FTSignalBus();
                FTServices.Register(signalBus);
            }

            if (!FTServices.TryGet(out FTEventBus _))
            {
                FTServices.Register(new FTEventBus(signalBus));
            }

            FTServices.Register(this);
            Debug.Log("[SacredCore] Runtime root ready.");
        }
    }
}

using FullThrottle.SacredCore.Save;
using UnityEngine;

namespace FullThrottle.SacredCore.Runtime
{
    /// <summary>
    /// Drop this once into the bootstrap scene or a persistent runtime root.
    /// It wires the sacred-core service graph in a predictable order.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class FTBootstrap : MonoBehaviour
    {
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private FTSaveGateway saveGateway;
        [SerializeField] private MonoBehaviour[] serviceBehaviours;

        private void Awake()
        {
            if (FTRuntimeRoot.Instance != null && FTRuntimeRoot.Instance.gameObject != gameObject)
            {
                Debug.Log("[SacredCore] Ignored duplicate runtime root bootstrap in loaded scene.");
                Destroy(gameObject);
                return;
            }

            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            FTServices.ResetAll();
            FTSignalBus signalBus = new FTSignalBus();
            FTServices.Register(signalBus);
            FTServices.Register(new FTEventBus(signalBus));

            if (saveGateway == null)
            {
                saveGateway = GetComponent<FTSaveGateway>();
                if (saveGateway == null)
                {
                    saveGateway = FindFirstObjectByType<FTSaveGateway>();
                }
            }

            if (saveGateway != null)
            {
                FTServices.Register(saveGateway);
                saveGateway.LoadOrCreate();
            }

            foreach (MonoBehaviour behaviour in serviceBehaviours)
            {
                if (behaviour == null) continue;
                behaviour.gameObject.SetActive(true);
            }

            Debug.Log("[SacredCore] Bootstrap initialized runtime service graph.");
        }
    }
}

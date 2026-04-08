using UnityEngine;
using Underground.Core.Architecture;

namespace Underground.Core
{
    public class PersistentRuntimeRoot : MonoBehaviour
    {
        private static PersistentRuntimeRoot instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            ServiceLocator.Reset();
            ServiceLocator.Register<IEventBus>(ServiceLocator.EventBus);
            DontDestroyOnLoad(gameObject);
        }
    }
}

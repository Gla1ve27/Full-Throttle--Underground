using FullThrottle.SacredCore.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FullThrottle.SacredCore.World
{
    /// <summary>
    /// Carries the garage truth into the world without stale fallback behavior.
    /// </summary>
    [DefaultExecutionOrder(-9830)]
    public sealed class FTWorldTravelDirector : MonoBehaviour
    {
        [SerializeField] private bool dontDestroyOnLoad = true;

        public string PendingCarId { get; private set; }
        public string PendingSpawnPointId { get; private set; } = "player_start";
        public bool HasPendingWorldEntry => !string.IsNullOrWhiteSpace(PendingCarId);

        private FTSignalBus bus;

        private void Awake()
        {
            FTServices.Register(this);
            bus = FTServices.Get<FTSignalBus>();
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        public void QueueWorldEntry(string carId, string spawnPointId)
        {
            if (string.IsNullOrWhiteSpace(carId))
            {
                Debug.LogError("[SacredCore] Refused world entry queue with empty car id.");
                return;
            }

            PendingCarId = carId;
            PendingSpawnPointId = string.IsNullOrWhiteSpace(spawnPointId) ? "player_start" : spawnPointId;
            bus.Raise(new FTWorldTravelQueuedSignal(PendingCarId, PendingSpawnPointId));
            Debug.Log($"[SacredCore] World entry queued. car={PendingCarId}, spawn={PendingSpawnPointId}");
        }

        public void LoadWorld(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(PendingCarId))
            {
                Debug.LogError("[SacredCore] Refused world load. No selected car is queued for handoff.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.Log($"[SacredCore] Loading world scene '{sceneName}' with pendingCar={PendingCarId}, pendingSpawn={PendingSpawnPointId}.");
                SceneManager.LoadScene(sceneName);
            }
        }

        public string ConsumePendingCarId()
        {
            string carId = PendingCarId;
            PendingCarId = null;
            return carId;
        }

        public string ConsumePendingSpawnPointId()
        {
            string spawnPointId = PendingSpawnPointId;
            PendingSpawnPointId = "player_start";
            return spawnPointId;
        }
    }
}

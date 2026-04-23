using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Underground.Audio;
using Underground.Garage;
using Underground.Core.Architecture;
using Underground.Save;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Single authoritative runtime initializer for the player car.
    /// 
    /// Startup order enforced by this bridge:
    ///   1. Load save
    ///   2. Read selected car ID from PersistentProgressManager
    ///   3. Apply appearance (via PlayerCarAppearanceController)
    ///   4. Resolve stats
    ///   5. Initialize active controller (V2 or legacy fallback)
    ///   6. Bind audio
    ///   7. Gameplay begins
    ///
    /// Key rules:
    ///   - VehicleControllerV2 does NOT self-initialize
    ///   - Legacy controller is disabled when V2 is active
    ///   - Only one audio system runs at a time
    ///   - Car identity comes from save, not from prefab defaults
    /// </summary>
    public sealed class VehicleSetupBridge : MonoBehaviour
    {
        [Header("Strategy")]
        [SerializeField] private bool useV2Controller = true;

        [Header("References")]
        [SerializeField] private VehicleControllerV2 v2Controller;
        
        [SerializeField] private PlayerCarAppearanceController appearanceController;
        [SerializeField] private Transform centerOfMassReference;

        [Header("Debug")]
        [SerializeField] private bool logStartup = true;

        public bool IsV2Active => useV2Controller && v2Controller != null && v2Controller.IsInitialized;
        public bool IsLegacyActive => false;

        private void Start()
        {
            StartCoroutine(InitializeRoutine());
        }

        private IEnumerator InitializeRoutine()
        {
            // Allow persistent services and scene objects one frame to finish startup.
            yield return null;

            ResolveReferences();

            bool isGaragePreview = GetComponentInParent<GarageShowroomController>() != null;
            if (appearanceController != null)
            {
                appearanceController.SetShowroomPresentationMode(isGaragePreview);
            }

            if (isGaragePreview)
            {
                if (logStartup)
                {
                    Debug.Log("[SetupBridge] Skipping gameplay initialization on garage showroom preview car.");
                }
                yield break;
            }

            string selectedCarId = GetSavedCarId();

            if (logStartup)
            {
                Debug.Log($"[SetupBridge] Starting vehicle initialization. Selected car: {selectedCarId}, Strategy: {(useV2Controller ? "V2" : "Legacy")}");
            }

            if (appearanceController != null)
            {
                bool applied = appearanceController.ApplyAppearance(selectedCarId);
                if (!applied)
                {
                    Debug.LogWarning($"[SetupBridge] Initial appearance apply failed for {selectedCarId}. Retrying next frame.");
                    yield return null;
                    appearanceController.ApplyAppearance(selectedCarId);
                }
            }

            VehicleStatsData stats = ResolveStats(selectedCarId);

            if (useV2Controller)
            {
                InitializeV2(stats);
            }
            else
            {
                InitializeLegacy(stats);
            }

            if (appearanceController != null && appearanceController.CurrentCarId != PlayerCarCatalog.MigrateCarId(selectedCarId))
            {
                Debug.LogWarning($"[SetupBridge] Current appearance car '{appearanceController.CurrentCarId}' does not match expected '{selectedCarId}'. Forcing one more reapply.");
                appearanceController.ApplyAppearance(selectedCarId);
            }

            if (VehicleSceneSelectionBridge.TryConsumePendingCarId(out string consumedCarId))
            {
                if (logStartup)
                {
                    Debug.Log($"[SetupBridge] Consumed pending garage selection: {consumedCarId}");
                }

                if (ServiceLocator.TryResolve<IProgressService>(out var progress) && !string.Equals(progress.CurrentOwnedCarId, consumedCarId, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (progress is PersistentProgressManager ppm)
                    {
                        ppm.SetCurrentCar(consumedCarId);
                    }
                }
            }

            yield return null;
            SnapToStartupRespawnIfAvailable();

            if (logStartup)
            {
                Debug.Log($"[SetupBridge] Vehicle initialization complete. Active: {(useV2Controller ? "V2" : "Legacy")}");
            }
        }

        private void SnapToStartupRespawnIfAvailable()
        {
            if (v2Controller == null || !v2Controller.IsInitialized)
            {
                return;
            }

            Transform startupRespawn = FindStartupRespawnPoint();
            if (startupRespawn == null)
            {
                if (logStartup)
                {
                    Debug.Log("[SetupBridge] No startup respawn point found. Keeping authored scene position.");
                }
                return;
            }

            Vector3 before = transform.position;
            v2Controller.ResetVehicle(startupRespawn);

            if (logStartup)
            {
                Debug.Log($"[SetupBridge] Startup snap to '{startupRespawn.name}' at {startupRespawn.position} from {before}.");
            }
        }

        private static Transform FindStartupRespawnPoint()
        {
            GameObject named = GameObject.Find("RespawnCheckpoint");
            if (named != null)
            {
                return named.transform;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return null;
            }

            foreach (GameObject root in activeScene.GetRootGameObjects())
            {
                Transform found = FindChildRecursive(root.transform, "RespawnCheckpoint");
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            if (string.Equals(parent.name, childName, System.StringComparison.OrdinalIgnoreCase))
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Switches the active controller at runtime (for testing/migration).
        /// </summary>
        public void SwitchToV2()
        {
            if (v2Controller == null)
            {
                Debug.LogWarning("[SetupBridge] No VehicleControllerV2 found.");
                return;
            }

            useV2Controller = true;
            string carId = GetSavedCarId();
            VehicleStatsData stats = ResolveStats(carId);
            InitializeV2(stats);
        }

        public void SwitchToLegacy()
        {
            Debug.LogWarning("[SetupBridge] Legacy controller is deleted. Keeping V2 active.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Initialization
        // ─────────────────────────────────────────────────────────────────────

        private void InitializeV2(VehicleStatsData stats)
        {
            // Disable legacy audio
            DisableLegacyAudio();

            // Enable and initialize V2
            if (v2Controller != null)
            {
                v2Controller.enabled = true;
                v2Controller.Initialize(stats);

                if (logStartup)
                {
                    Debug.Log("[SetupBridge] VehicleControllerV2 initialized.");
                }
            }
        }

        private void InitializeLegacy(VehicleStatsData stats)
        {
            Debug.LogWarning("[SetupBridge] Legacy controller is removed from the project.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private string GetSavedCarId()
        {
            string pendingCarId = VehicleSceneSelectionBridge.PeekPendingCarId();
            if (!string.IsNullOrEmpty(pendingCarId))
            {
                return PlayerCarCatalog.MigrateCarId(pendingCarId);
            }

            if (ServiceLocator.TryResolve<IProgressService>(out var progress) && !string.IsNullOrEmpty(progress.CurrentOwnedCarId))
            {
                return PlayerCarCatalog.MigrateCarId(progress.CurrentOwnedCarId);
            }

            PersistentProgressManager progressManager = FindFirstObjectByType<PersistentProgressManager>();
            if (progressManager != null && !string.IsNullOrEmpty(progressManager.CurrentOwnedCarId))
            {
                return PlayerCarCatalog.MigrateCarId(progressManager.CurrentOwnedCarId);
            }

            Debug.LogWarning("[SetupBridge] No progress selection found. Using starter car.");
            return PlayerCarCatalog.StarterCarId;
        }

        private VehicleStatsData ResolveStats(string carId)
        {
            if (PlayerCarCatalog.TryGetDefinition(carId, out PlayerCarDefinition def))
            {
                VehicleStatsData stats = def.LoadStatsAsset();
                if (stats != null)
                {
                    return stats;
                }
            }

            // Fallback: use whatever stats are already on the controller
            if (v2Controller != null)
            {
                return v2Controller.BaseStats;
            }

            Debug.LogWarning($"[SetupBridge] Could not resolve stats for car '{carId}'. Using prefab defaults.");
            return null;
        }

        private void DisableLegacyAudio()
        {
            // Legacy audio deleted.
        }

        private void ResolveReferences()
        {
            if (v2Controller == null)
            {
                v2Controller = GetComponent<VehicleControllerV2>();
            }

            if (appearanceController == null)
            {
                appearanceController = GetComponent<PlayerCarAppearanceController>();
            }
        }
    }
}

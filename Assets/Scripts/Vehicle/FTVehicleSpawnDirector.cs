using FullThrottle.SacredCore.Garage;
using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Save;
using FullThrottle.SacredCore.World;
using FullThrottle.SacredCore.Audio;
using UnityEngine;

namespace FullThrottle.SacredCore.Vehicle
{
    /// <summary>
    /// World-side truth for the player car.
    /// It trusts the garage/profile handoff first and refuses stale placeholder fallback behavior.
    /// </summary>
    [DefaultExecutionOrder(-7600)]
    public sealed class FTVehicleSpawnDirector : MonoBehaviour
    {
        [SerializeField] private Transform vehicleParent;
        [SerializeField] private FTPlayerVehicleBinder activeVehicle;
        [SerializeField] private bool reuseScenePlayerVehicle = true;
        [SerializeField] private bool removeExtraSceneVehicles = true;

        private FTCarRegistry carRegistry;
        private FTSaveGateway saveGateway;
        private FTWorldTravelDirector worldTravel;
        private FTSelectedCarRuntime selectedCarRuntime;
        private FTSpawnPointResolver spawnPointResolver;

        public FTPlayerVehicleBinder ActiveVehicle => activeVehicle;

        private void Awake()
        {
            FTServices.Register(this);
        }

        private void Start()
        {
            if (!ResolveServices())
            {
                Debug.LogError("[SacredCore] Vehicle spawn halted. Required truth services are missing.");
                return;
            }

            FTServices.TryGet(out spawnPointResolver);
            SpawnSelectedVehicle();
        }

        public void SpawnSelectedVehicle()
        {
            if (!ResolveSpawnDefinition(out string requestedId, out string validatedId, out FTCarDefinition definition))
            {
                return;
            }

            if (definition == null || definition.worldPrefab == null)
            {
                Debug.LogError($"[SacredCore] No prefab available for car '{validatedId}'.");
                return;
            }

            saveGateway.Profile.currentCarId = validatedId;
            if (!selectedCarRuntime.TrySetOwnedCurrentCar(validatedId, "World spawn", true))
            {
                Debug.LogError($"[SacredCore] Spawn refused. Selected car '{validatedId}' failed ownership/runtime truth validation.");
                return;
            }

            saveGateway.Profile.OwnCar(validatedId);
            saveGateway.Save();

            string requestedSpawn = worldTravel.ConsumePendingSpawnPointId();
            Pose spawnPose = ResolveSpawnPose(requestedSpawn);

            FTPlayerVehicleBinder sceneVehicle = reuseScenePlayerVehicle
                ? ResolveSceneVehicleCandidate()
                : null;

            if (activeVehicle != null && activeVehicle != sceneVehicle)
            {
                Destroy(activeVehicle.gameObject);
            }

            GameObject instance;
            if (sceneVehicle != null)
            {
                activeVehicle = sceneVehicle;
                instance = sceneVehicle.gameObject;
                if (vehicleParent != null && instance.transform.parent != vehicleParent)
                {
                    instance.transform.SetParent(vehicleParent, true);
                }

                Debug.Log($"[SacredCore] Reusing scene player vehicle '{instance.name}' for world spawn.");
            }
            else
            {
                instance = Instantiate(definition.worldPrefab, spawnPose.position, spawnPose.rotation, vehicleParent);
            }

            instance.name = "PlayerCar";
            TrySetPlayerTag(instance);
            SanitizeAudioRigs(instance);
            EnsureVisualApplier(instance);
            activeVehicle = instance.GetComponent<FTPlayerVehicleBinder>();
            if (activeVehicle == null)
            {
                activeVehicle = instance.AddComponent<FTPlayerVehicleBinder>();
            }

            FTVehiclePhysicsGuard guard = instance.GetComponent<FTVehiclePhysicsGuard>();
            if (guard == null)
            {
                guard = instance.AddComponent<FTVehiclePhysicsGuard>();
            }

            guard.SanitizeWheelRadii();
            guard.SanitizeBody();
            RemoveExtraSceneVehicles(activeVehicle);

            activeVehicle.ApplyDefinition(definition);
            activeVehicle.PrepareForSpawn(spawnPose);

            Debug.Log($"[SacredCore] Spawned selected car. requested={requestedId}, validated={validatedId}, display={definition.displayName}, spawn={requestedSpawn}, parent={(vehicleParent != null ? vehicleParent.name : "scene")}.");
        }

        private bool ResolveServices()
        {
            bool ok = true;
            if (carRegistry == null && !FTServices.TryGet(out carRegistry)) ok = false;
            if (saveGateway == null && !FTServices.TryGet(out saveGateway)) ok = false;
            if (worldTravel == null && !FTServices.TryGet(out worldTravel)) ok = false;
            if (selectedCarRuntime == null && !FTServices.TryGet(out selectedCarRuntime)) ok = false;
            return ok;
        }

        private bool ResolveSpawnDefinition(out string requestedId, out string validatedId, out FTCarDefinition definition)
        {
            requestedId = worldTravel != null ? worldTravel.ConsumePendingCarId() : string.Empty;
            if (string.IsNullOrWhiteSpace(requestedId))
            {
                requestedId = selectedCarRuntime != null && !string.IsNullOrWhiteSpace(selectedCarRuntime.CurrentCarId)
                    ? selectedCarRuntime.CurrentCarId
                    : saveGateway.Profile.currentCarId;
            }

            validatedId = carRegistry.ValidateOrFallback(requestedId);
            if (string.IsNullOrWhiteSpace(validatedId))
            {
                definition = null;
                Debug.LogError($"[SacredCore] Spawn failed. Requested car '{requestedId}' did not resolve and no starter fallback exists.");
                return false;
            }

            if (!saveGateway.Profile.OwnsCar(validatedId))
            {
                string profileId = selectedCarRuntime.ForceSyncFromProfile();
                if (!string.IsNullOrWhiteSpace(profileId) && saveGateway.Profile.OwnsCar(profileId))
                {
                    Debug.LogWarning($"[SacredCore] Spawn rejected unowned requested car '{validatedId}'. Using owned profile car '{profileId}'.");
                    validatedId = profileId;
                }
            }

            definition = carRegistry.Get(validatedId);
            return definition != null;
        }

        private static void SanitizeAudioRigs(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            Transform root = instance.transform;
            Transform primaryAudioRoot = null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name != "FTVehicleAudio")
                {
                    continue;
                }

                if (primaryAudioRoot == null || child.GetComponent<FTVehicleAudioDirector>() != null)
                {
                    primaryAudioRoot = child;
                }
            }

            if (primaryAudioRoot == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child == primaryAudioRoot || child.name != "FTVehicleAudio")
                {
                    continue;
                }

                StopAudioSources(child);
                Debug.LogWarning($"[SacredCore] Removed duplicate FTVehicleAudio root '{child.name}' from {instance.name}.");
                Destroy(child.gameObject);
            }

            FTVehicleAudioDirector[] directors = instance.GetComponentsInChildren<FTVehicleAudioDirector>(true);
            for (int i = 0; i < directors.Length; i++)
            {
                FTVehicleAudioDirector director = directors[i];
                if (director == null || director.transform == primaryAudioRoot)
                {
                    continue;
                }

                StopAudioSources(director.transform);
                director.enabled = false;
                Debug.LogWarning($"[SacredCore] Disabled duplicate audio director '{director.name}' on {instance.name}.");
            }
        }

        private static void StopAudioSources(Transform root)
        {
            AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i].Stop();
                sources[i].clip = null;
            }
        }

        private FTPlayerVehicleBinder ResolveSceneVehicleCandidate()
        {
            if (activeVehicle != null)
            {
                return activeVehicle;
            }

            FTPlayerVehicleBinder[] candidates = FindObjectsByType<FTPlayerVehicleBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            FTPlayerVehicleBinder fallback = null;
            for (int i = 0; i < candidates.Length; i++)
            {
                FTPlayerVehicleBinder candidate = candidates[i];
                if (candidate == null || candidate.GetComponentInParent<FTGarageShowroomDirector>() != null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = candidate;
                }

                if (candidate.CompareTag("Player") || candidate.name == "PlayerCar")
                {
                    return candidate;
                }
            }

            return fallback;
        }

        private void RemoveExtraSceneVehicles(FTPlayerVehicleBinder keep)
        {
            if (!removeExtraSceneVehicles || keep == null)
            {
                return;
            }

            FTPlayerVehicleBinder[] candidates = FindObjectsByType<FTPlayerVehicleBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < candidates.Length; i++)
            {
                FTPlayerVehicleBinder candidate = candidates[i];
                if (candidate == null || candidate == keep || candidate.GetComponentInParent<FTGarageShowroomDirector>() != null)
                {
                    continue;
                }

                Debug.LogWarning($"[SacredCore] Removed extra scene vehicle '{candidate.name}' so spawn truth has one player car.");
                Destroy(candidate.gameObject);
            }
        }

        private static void EnsureVisualApplier(GameObject instance)
        {
            if (instance != null && instance.GetComponent<FTVehicleVisualApplier>() == null)
            {
                instance.AddComponent<FTVehicleVisualApplier>();
            }
        }

        private static void TrySetPlayerTag(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            try
            {
                instance.tag = "Player";
            }
            catch (UnityException)
            {
                Debug.LogWarning("[SacredCore] Could not set Player tag on spawned vehicle. Ensure the Player tag exists.");
            }
        }

        private Pose ResolveSpawnPose(string spawnPointId)
        {
            if (spawnPointResolver != null)
            {
                return spawnPointResolver.Resolve(spawnPointId);
            }

            FTSpawnPoint[] points = FindObjectsOfType<FTSpawnPoint>(true);
            FTSpawnPoint fallback = null;
            foreach (FTSpawnPoint point in points)
            {
                if (point == null) continue;
                if (point.defaultForScene && fallback == null)
                {
                    fallback = point;
                }

                if (point.spawnPointId == spawnPointId)
                {
                    return new Pose(point.transform.position, point.transform.rotation);
                }
            }

            if (fallback != null)
            {
                return new Pose(fallback.transform.position, fallback.transform.rotation);
            }

            return new Pose(Vector3.zero, Quaternion.identity);
        }
    }
}

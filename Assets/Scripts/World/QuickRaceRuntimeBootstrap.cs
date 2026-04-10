using System.Collections;
using Underground.Race;
using Underground.Session;
using Underground.TimeSystem;
using Underground.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Underground.World
{
    public static class QuickRaceRuntimeBootstrap
    {
        private const string WorldSceneName = "World";
        private const float NightStartTime = 22f;

        private static QuickRaceBootstrapRunner runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureRunner();
            TryQueueCurrentScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryQueueCurrentScene(scene);
        }

        private static void TryQueueCurrentScene(Scene scene)
        {
            if (!QuickRaceSessionData.IsActive)
            {
                return;
            }

            if (!string.Equals(scene.name, WorldSceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                QuickRaceSessionData.Clear();
                return;
            }

            if (!QuickRaceSessionData.HasPendingLaunch)
            {
                return;
            }

            EnsureRunner().Run(scene);
        }

        private static QuickRaceBootstrapRunner EnsureRunner()
        {
            if (runner != null)
            {
                return runner;
            }

            GameObject runnerObject = new GameObject("QuickRaceRuntimeBootstrap");
            Object.DontDestroyOnLoad(runnerObject);
            runnerObject.hideFlags = HideFlags.HideAndDontSave;
            runner = runnerObject.AddComponent<QuickRaceBootstrapRunner>();
            return runner;
        }

        private sealed class QuickRaceBootstrapRunner : MonoBehaviour
        {
            private Coroutine routine;

            public void Run(Scene scene)
            {
                if (routine != null)
                {
                    StopCoroutine(routine);
                }

                routine = StartCoroutine(ApplyQuickRaceSetup(scene));
            }

            private static IEnumerator ApplyQuickRaceSetup(Scene scene)
            {
                yield return null;

                if (!scene.isLoaded ||
                    !QuickRaceSessionData.HasPendingLaunch ||
                    !string.Equals(scene.name, WorldSceneName, System.StringComparison.OrdinalIgnoreCase))
                {
                    yield break;
                }

                VehicleDynamicsController playerVehicle = FindPlayerVehicle();
                RaceManager raceManager = FindSelectedRaceManager();

                if (playerVehicle == null || raceManager == null || raceManager.ActiveDefinition == null)
                {
                    QuickRaceSessionData.Clear();
                    yield break;
                }

                PlayerCarAppearanceController appearanceController = playerVehicle.GetComponent<PlayerCarAppearanceController>();
                if (appearanceController == null)
                {
                    appearanceController = playerVehicle.gameObject.AddComponent<PlayerCarAppearanceController>();
                }

                if (!string.IsNullOrWhiteSpace(QuickRaceSessionData.SelectedCarId))
                {
                    appearanceController.ApplyAppearance(QuickRaceSessionData.SelectedCarId);
                }

                bool useManualTransmission = QuickRaceSessionData.TransmissionMode == QuickRaceTransmissionMode.Manual;
                playerVehicle.SetUseManualTransmission(useManualTransmission);

                SessionManager sessionManager = Object.FindFirstObjectByType<SessionManager>();
                sessionManager?.BeginSession();

                DayNightCycleController dayNightCycle = Object.FindFirstObjectByType<DayNightCycleController>();
                if (raceManager.ActiveDefinition.nightOnly)
                {
                    dayNightCycle?.SetTime(NightStartTime);
                }

                PositionPlayerAtRaceStart(playerVehicle, raceManager);

                CarRespawn carRespawn = playerVehicle.GetComponent<CarRespawn>();
                if (carRespawn != null)
                {
                    carRespawn.RegisterRespawnPoint(raceManager.transform);
                }

                bool started = raceManager.TryStartRace();
                if (started)
                {
                    QuickRaceSessionData.MarkLaunchConsumed();
                }
                else
                {
                    QuickRaceSessionData.Clear();
                }
            }

            private static VehicleDynamicsController FindPlayerVehicle()
            {
                VehicleDynamicsController[] vehicles = Object.FindObjectsByType<VehicleDynamicsController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < vehicles.Length; i++)
                {
                    if (vehicles[i] != null && vehicles[i].CompareTag("Player"))
                    {
                        return vehicles[i];
                    }
                }

                return Object.FindFirstObjectByType<VehicleDynamicsController>();
            }

            private static RaceManager FindSelectedRaceManager()
            {
                RaceManager[] raceManagers = Object.FindObjectsByType<RaceManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < raceManagers.Length; i++)
                {
                    RaceManager raceManager = raceManagers[i];
                    if (raceManager == null || raceManager.ActiveDefinition == null)
                    {
                        continue;
                    }

                    if (QuickRaceSessionData.IsSelectedRace(raceManager.ActiveDefinition.raceId))
                    {
                        return raceManager;
                    }
                }

                return null;
            }

            private static void PositionPlayerAtRaceStart(VehicleDynamicsController playerVehicle, RaceManager raceManager)
            {
                if (playerVehicle == null || playerVehicle.Rigidbody == null || raceManager == null)
                {
                    return;
                }

                Transform playerTransform = playerVehicle.transform;
                Vector3 position = raceManager.transform.position - (raceManager.transform.forward * 6f) + (Vector3.up * 0.75f);
                Quaternion rotation = raceManager.transform.rotation;

                playerVehicle.Rigidbody.linearVelocity = Vector3.zero;
                playerVehicle.Rigidbody.angularVelocity = Vector3.zero;
                playerTransform.SetPositionAndRotation(position, rotation);
                playerVehicle.Rigidbody.position = position;
                playerVehicle.Rigidbody.rotation = rotation;
            }
        }
    }
}

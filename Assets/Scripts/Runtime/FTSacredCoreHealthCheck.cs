using System.Collections.Generic;
using System.Text;
using FullThrottle.SacredCore.Audio;
using FullThrottle.SacredCore.Garage;
using FullThrottle.SacredCore.Race;
using FullThrottle.SacredCore.Save;
using FullThrottle.SacredCore.Vehicle;
using FullThrottle.SacredCore.World;
using UnityEngine;

namespace FullThrottle.SacredCore.Runtime
{
    /// <summary>
    /// Executable Phase 4 validation for the sacred core.
    /// Keep it on the runtime root while integrating scenes.
    /// </summary>
    [DefaultExecutionOrder(-7000)]
    public sealed class FTSacredCoreHealthCheck : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private float repeatIntervalSeconds;
        [SerializeField] private bool validateSceneSpawnPoints = true;
        [SerializeField] private bool validateActiveVehicle = true;
        [SerializeField] private bool logPassedChecks;

        private float nextRunTime;

        public bool LastPassed { get; private set; }
        public int LastErrorCount { get; private set; }
        public int LastWarningCount { get; private set; }
        public string LastSummary { get; private set; } = "";

        private void Start()
        {
            if (runOnStart)
            {
                RunValidation();
            }
        }

        private void Update()
        {
            if (repeatIntervalSeconds <= 0f || Time.unscaledTime < nextRunTime)
            {
                return;
            }

            nextRunTime = Time.unscaledTime + repeatIntervalSeconds;
            RunValidation();
        }

        [ContextMenu("Run Sacred Core Validation")]
        public void RunValidation()
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            StringBuilder passed = new StringBuilder();

            ValidateServices(errors, warnings, passed, out FTCarDefinition selectedCar, out FTVehicleAudioProfile selectedProfile);

            if (validateSceneSpawnPoints)
            {
                ValidateSpawnPoints(errors, warnings, passed);
            }

            if (validateActiveVehicle)
            {
                ValidateVehicle(errors, warnings, passed, selectedCar, selectedProfile);
            }

            ValidateRaceSession(errors, warnings, passed);

            LastErrorCount = errors.Count;
            LastWarningCount = warnings.Count;
            LastPassed = LastErrorCount == 0;
            LastSummary = $"Sacred core health passed={LastPassed}, errors={LastErrorCount}, warnings={LastWarningCount}";

            for (int i = 0; i < errors.Count; i++)
            {
                Debug.LogError($"[SacredCore][Health] {errors[i]}");
            }

            for (int i = 0; i < warnings.Count; i++)
            {
                Debug.LogWarning($"[SacredCore][Health] {warnings[i]}");
            }

            if (logPassedChecks && passed.Length > 0)
            {
                Debug.Log($"[SacredCore][Health] Passed checks: {passed}");
            }

            if (LastPassed)
            {
                Debug.Log($"[SacredCore][Health] PASS. warnings={LastWarningCount}");
            }
            else
            {
                Debug.LogError($"[SacredCore][Health] FAIL. errors={LastErrorCount}, warnings={LastWarningCount}");
            }

            if (FTServices.TryGet(out FTEventBus eventBus))
            {
                eventBus.Raise(new FTSacredCoreHealthSignal(LastPassed, LastErrorCount, LastWarningCount, LastSummary));
            }
        }

        private static void ValidateServices(
            List<string> errors,
            List<string> warnings,
            StringBuilder passed,
            out FTCarDefinition selectedCar,
            out FTVehicleAudioProfile selectedProfile)
        {
            selectedCar = null;
            selectedProfile = null;

            if (!FTServices.TryGet(out FTSaveGateway saveGateway))
            {
                errors.Add("Missing FTSaveGateway service.");
                return;
            }

            if (saveGateway.Profile == null)
            {
                errors.Add("FTSaveGateway.Profile is null.");
                return;
            }

            AppendPassed(passed, "save");

            if (!FTServices.TryGet(out FTCarRegistry carRegistry))
            {
                errors.Add("Missing FTCarRegistry service.");
                return;
            }

            if (carRegistry.Cars == null || carRegistry.Cars.Count == 0)
            {
                errors.Add("FTCarRegistry has no car definitions assigned.");
                return;
            }

            AppendPassed(passed, "car-registry");

            string profileCarId = saveGateway.Profile.currentCarId;
            if (!FTServices.TryGet(out FTSelectedCarRuntime selectedRuntime))
            {
                errors.Add("Missing FTSelectedCarRuntime service.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(selectedRuntime.CurrentCarId))
                {
                    errors.Add("FTSelectedCarRuntime.CurrentCarId is empty.");
                }
                else if (!string.IsNullOrWhiteSpace(profileCarId) && selectedRuntime.CurrentCarId != profileCarId)
                {
                    errors.Add($"Selected car mismatch. runtime={selectedRuntime.CurrentCarId}, profile={profileCarId}");
                }

                profileCarId = string.IsNullOrWhiteSpace(selectedRuntime.CurrentCarId) ? profileCarId : selectedRuntime.CurrentCarId;
            }

            if (string.IsNullOrWhiteSpace(profileCarId))
            {
                errors.Add("No selected car id exists in runtime or profile.");
                return;
            }

            if (!saveGateway.Profile.OwnsCar(profileCarId))
            {
                errors.Add($"Selected car '{profileCarId}' is not in ownedCarIds.");
            }

            selectedCar = carRegistry.Get(profileCarId);
            if (selectedCar == null)
            {
                errors.Add($"Selected car '{profileCarId}' did not resolve to an FTCarDefinition.");
                return;
            }

            if (selectedCar.worldPrefab == null)
            {
                errors.Add($"Selected car '{selectedCar.carId}' has no worldPrefab.");
            }

            AppendPassed(passed, "selected-car");

            if (!FTServices.TryGet(out FTAudioProfileRegistry audioRegistry))
            {
                errors.Add("Missing FTAudioProfileRegistry service.");
                return;
            }

            bool profileValid = audioRegistry.ResolveProfile(selectedCar, out selectedProfile, out string audioReport);
            if (selectedProfile == null)
            {
                errors.Add($"Selected car audio profile failed to resolve. {audioReport}");
                return;
            }

            if (selectedProfile.devEmergencyFallback)
            {
                errors.Add($"Selected car '{selectedCar.carId}' resolved to dev emergency fallback audio profile '{selectedProfile.audioProfileId}'.");
            }

            if (!profileValid)
            {
                warnings.Add($"Selected car audio profile resolved with validation warnings. {audioReport}");
            }

            if (selectedCar.audioProfileId != selectedProfile.audioProfileId && !selectedProfile.devEmergencyFallback)
            {
                errors.Add($"Audio profile mismatch. car={selectedCar.carId}, car.audioProfileId={selectedCar.audioProfileId}, resolved={selectedProfile.audioProfileId}");
            }

            AppendPassed(passed, "audio-profile");

            FTGarageShowroomDirector showroom = Object.FindFirstObjectByType<FTGarageShowroomDirector>();
            if (showroom != null && !string.IsNullOrWhiteSpace(showroom.CurrentDisplayCarId) && showroom.CurrentDisplayCarId != profileCarId)
            {
                errors.Add($"Garage showroom mismatch. showroom={showroom.CurrentDisplayCarId}, selected={profileCarId}");
            }
        }

        private static void ValidateSpawnPoints(List<string> errors, List<string> warnings, StringBuilder passed)
        {
            FTSpawnPoint[] points = Object.FindObjectsByType<FTSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (points == null || points.Length == 0)
            {
                warnings.Add("No FTSpawnPoint objects in the active scene. This is expected in pure bootstrap scenes, not in world scenes.");
                return;
            }

            bool hasDefault = false;
            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < points.Length; i++)
            {
                FTSpawnPoint point = points[i];
                if (point == null) continue;

                if (string.IsNullOrWhiteSpace(point.spawnPointId))
                {
                    errors.Add($"Spawn point '{point.name}' has an empty spawnPointId.");
                    continue;
                }

                if (!ids.Add(point.spawnPointId))
                {
                    warnings.Add($"Duplicate FTSpawnPoint id '{point.spawnPointId}' found.");
                }

                hasDefault |= point.defaultForScene;
            }

            if (!hasDefault)
            {
                warnings.Add("No FTSpawnPoint is marked defaultForScene.");
            }

            AppendPassed(passed, $"spawn-points:{points.Length}");
        }

        private static void ValidateVehicle(
            List<string> errors,
            List<string> warnings,
            StringBuilder passed,
            FTCarDefinition selectedCar,
            FTVehicleAudioProfile selectedProfile)
        {
            FTPlayerVehicleBinder active = null;
            bool hasSpawnDirector = FTServices.TryGet(out FTVehicleSpawnDirector spawnDirector);
            if (hasSpawnDirector)
            {
                active = spawnDirector.ActiveVehicle;
            }

            if (active == null && hasSpawnDirector)
            {
                active = Object.FindFirstObjectByType<FTPlayerVehicleBinder>();
            }

            if (active == null)
            {
                warnings.Add("No active FTPlayerVehicleBinder found. This is expected in bootstrap/garage scenes, not in world driving scenes.");
                return;
            }

            if (active.Definition == null)
            {
                errors.Add($"Active vehicle '{active.name}' has no applied FTCarDefinition.");
            }
            else if (selectedCar != null && active.Definition.carId != selectedCar.carId)
            {
                errors.Add($"Active vehicle definition mismatch. active={active.Definition.carId}, selected={selectedCar.carId}");
            }

            Rigidbody body = active.Body != null ? active.Body : active.GetComponent<Rigidbody>();
            if (body == null)
            {
                errors.Add($"Active vehicle '{active.name}' has no Rigidbody.");
            }

            WheelCollider[] wheels = active.WheelColliders;
            if (wheels == null || wheels.Length == 0)
            {
                wheels = active.GetComponentsInChildren<WheelCollider>(true);
            }

            if (wheels == null || wheels.Length == 0)
            {
                errors.Add($"Active vehicle '{active.name}' has no WheelColliders.");
            }

            if (active.GetComponentInChildren<FTVehiclePhysicsGuard>(true) == null)
            {
                errors.Add($"Active vehicle '{active.name}' has no FTVehiclePhysicsGuard.");
            }

            if (active.GetComponentInChildren<FTVehicleController>(true) == null)
            {
                warnings.Add($"Active vehicle '{active.name}' has no FTVehicleController. It may still be using legacy vehicle physics.");
            }

            if (active.GetComponentInChildren<FTVehicleTelemetry>(true) == null)
            {
                warnings.Add($"Active vehicle '{active.name}' has no FTVehicleTelemetry. Audio/HUD/camera will be incomplete.");
            }

            FTVehicleAudioDirector audioDirector = active.GetComponentInChildren<FTVehicleAudioDirector>(true);
            if (audioDirector == null)
            {
                warnings.Add($"Active vehicle '{active.name}' has no FTVehicleAudioDirector.");
            }
            else if (selectedProfile != null && audioDirector.CurrentProfile != null && audioDirector.CurrentProfile.audioProfileId != selectedProfile.audioProfileId)
            {
                errors.Add($"Active vehicle audio profile mismatch. active={audioDirector.CurrentProfile.audioProfileId}, selected={selectedProfile.audioProfileId}");
            }

            AppendPassed(passed, $"active-vehicle:{active.name}");
        }

        private static void ValidateRaceSession(List<string> errors, List<string> warnings, StringBuilder passed)
        {
            if (!FTServices.TryGet(out FTSaveGateway saveGateway))
            {
                return;
            }

            FTServices.TryGet(out FTRaceDirector raceDirector);
            bool profileRace = saveGateway.Profile.session != null && saveGateway.Profile.session.raceInProgress;
            bool directorRace = raceDirector != null && raceDirector.RaceLive;

            if (profileRace && !directorRace)
            {
                warnings.Add($"Profile says race is in progress but FTRaceDirector is not live. activeRaceId={saveGateway.Profile.session.activeRaceId}");
            }

            if (directorRace && !profileRace)
            {
                warnings.Add("FTRaceDirector is live but profile session raceInProgress is false.");
            }

            AppendPassed(passed, "race-session");
        }

        private static void AppendPassed(StringBuilder builder, string label)
        {
            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(label);
        }
    }
}

using System.Collections.Generic;
using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Story;
using FullThrottle.SacredCore.Vehicle;
using UnityEngine;

namespace FullThrottle.SacredCore.Audio
{
    /// <summary>
    /// Validation pass for the per-car audio identity law.
    /// Use this first for starter/player cars and named rival signature cars.
    /// </summary>
    [DefaultExecutionOrder(-9200)]
    public sealed class FTAudioRosterValidator : MonoBehaviour
    {
        [SerializeField] private FTCarRegistry carRegistry;
        [SerializeField] private FTAudioProfileRegistry audioRegistry;
        [SerializeField] private List<FTRivalDefinition> namedRivals = new();
        [SerializeField] private bool validateOnAwake = true;

        private void Awake()
        {
            if (carRegistry == null) FTServices.TryGet(out carRegistry);
            if (audioRegistry == null) FTServices.TryGet(out audioRegistry);

            if (validateOnAwake)
            {
                ValidatePlayableAndRivalAudio();
            }
        }

        [ContextMenu("Validate Playable And Rival Audio")]
        public void ValidatePlayableAndRivalAudio()
        {
            if (carRegistry == null || audioRegistry == null)
            {
                Debug.LogWarning("[SacredCore] Audio roster validation skipped. Missing car or audio registry.");
                return;
            }

            int checkedCars = 0;
            int failures = 0;

            for (int i = 0; i < carRegistry.Cars.Count; i++)
            {
                FTCarDefinition car = carRegistry.Cars[i];
                if (car == null) continue;
                checkedCars++;
                if (!ValidateCar(car, car.starterOwned || car.haloCar))
                {
                    failures++;
                }
            }

            for (int i = 0; i < namedRivals.Count; i++)
            {
                FTRivalDefinition rival = namedRivals[i];
                if (rival == null || string.IsNullOrWhiteSpace(rival.signatureCarId)) continue;
                if (!carRegistry.TryGet(rival.signatureCarId, out FTCarDefinition rivalCar))
                {
                    Debug.LogWarning($"[SacredCore] Rival audio validation failed: {rival.rivalId} signature car '{rival.signatureCarId}' is not registered.");
                    failures++;
                    continue;
                }

                checkedCars++;
                if (!ValidateCar(rivalCar, true))
                {
                    failures++;
                }
            }

            Debug.Log($"[SacredCore] Audio roster validation complete. checked={checkedCars}, failures={failures}.");
        }

        private bool ValidateCar(FTCarDefinition car, bool mustBeDedicated)
        {
            bool valid = audioRegistry.ResolveProfile(car, out FTVehicleAudioProfile profile, out string report);
            if (profile == null)
            {
                Debug.LogError($"[SacredCore] Audio identity missing. {report}");
                return false;
            }

            bool identityAccepted = valid && !profile.devEmergencyFallback;
            if (mustBeDedicated && !profile.dedicatedHeroProfile)
            {
                identityAccepted = false;
                report += " Dedicated starter/rival/hero profile required.";
            }

            if (!identityAccepted)
            {
                Debug.LogWarning($"[SacredCore] Audio identity rejected for car={car.carId}. {report}");
                return false;
            }

            Debug.Log($"[SacredCore] Audio identity accepted for car={car.carId}. profile={profile.audioProfileId}, family={profile.audioFamilyTag}, engine={car.engineCharacterTag}");
            return true;
        }
    }
}

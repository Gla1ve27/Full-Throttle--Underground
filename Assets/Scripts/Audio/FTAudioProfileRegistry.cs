using System.Collections.Generic;
using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Vehicle;
using UnityEngine;

namespace FullThrottle.SacredCore.Audio
{
    [DefaultExecutionOrder(-9840)]
    public sealed class FTAudioProfileRegistry : MonoBehaviour
    {
        [SerializeField] private List<FTVehicleAudioProfile> profiles = new();
        [SerializeField] private FTVehicleAudioProfile devEmergencyFallbackProfile;

        private readonly Dictionary<string, FTVehicleAudioProfile> map = new();

        public IReadOnlyList<FTVehicleAudioProfile> Profiles => profiles;

        private void Awake()
        {
            FTServices.Register(this);
            Rebuild();
        }

        public void Rebuild()
        {
            map.Clear();
            for (int i = 0; i < profiles.Count; i++)
            {
                FTVehicleAudioProfile profile = profiles[i];
                if (profile == null || string.IsNullOrWhiteSpace(profile.audioProfileId))
                {
                    continue;
                }

                map[profile.audioProfileId] = profile;
            }

            Debug.Log($"[SacredCore] Audio profile registry rebuilt. profiles={map.Count}");
        }

        public bool ResolveProfile(FTCarDefinition car, out FTVehicleAudioProfile profile, out string report)
        {
            profile = null;
            if (car == null)
            {
                report = "No car definition supplied.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(car.audioProfileId) && map.TryGetValue(car.audioProfileId, out profile))
            {
                bool valid = profile.ValidateRequiredLayers(out string validation);
                bool identityValid = profile.HasMeaningfulOverrides();
                report = $"car={car.carId}, profile={profile.audioProfileId}, dedicated={profile.dedicatedHeroProfile}, inherited={profile.inheritsFamily}, valid={valid}, identityOverrides={identityValid}. {validation}";
                if (!identityValid)
                {
                    report += " WARNING: inherited profile does not have enough unique identity overrides.";
                }

                return valid && identityValid;
            }

            if (devEmergencyFallbackProfile != null && devEmergencyFallbackProfile.devEmergencyFallback)
            {
                profile = devEmergencyFallbackProfile;
                report = $"Fallback profile used for car={car.carId}. Missing requested audioProfileId='{car.audioProfileId}'. fallback={profile.audioProfileId}";
                return false;
            }

            report = $"No audio profile for car={car.carId}, requested='{car.audioProfileId}'. No fallback allowed.";
            return false;
        }

        public bool TryGetById(string profileId, out FTVehicleAudioProfile profile)
        {
            return map.TryGetValue(profileId, out profile);
        }
    }
}

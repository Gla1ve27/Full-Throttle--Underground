using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Save;
using FullThrottle.SacredCore.Vehicle;
using UnityEngine;

namespace FullThrottle.SacredCore.Garage
{
    /// <summary>
    /// The single truth for what Gla1ve is currently claiming as his ride.
    /// Garage, world, save, and HUD should all respect this.
    /// </summary>
    [DefaultExecutionOrder(-9700)]
    public sealed class FTSelectedCarRuntime : MonoBehaviour
    {
        public string CurrentCarId { get; private set; }
        public FTCarDefinition CurrentDefinition { get; private set; }

        private FTSaveGateway saveGateway;
        private FTCarRegistry carRegistry;
        private FTSignalBus bus;

        private void Awake()
        {
            FTServices.Register(this);
            saveGateway = FTServices.Get<FTSaveGateway>();
            carRegistry = FTServices.Get<FTCarRegistry>();
            bus = FTServices.Get<FTSignalBus>();
            ForceSyncFromProfile();
        }

        public string ForceSyncFromProfile()
        {
            string fallback = carRegistry.GetStarter()?.carId ?? string.Empty;
            saveGateway.Profile.EnsureDefaults(fallback);
            string requested = saveGateway.Profile.currentCarId;
            string validated = carRegistry.ValidateOrFallback(requested);
            if (string.IsNullOrWhiteSpace(validated))
            {
                Debug.LogError("[SacredCore] Selected car sync failed. No valid starter or current car exists.");
                CurrentCarId = string.Empty;
                CurrentDefinition = null;
                return CurrentCarId;
            }

            bool changed = CurrentCarId != validated || saveGateway.Profile.currentCarId != validated;
            if (requested != validated)
            {
                Debug.LogWarning($"[SacredCore] Selected car corrected from '{requested}' to '{validated}'.");
                changed = true;
            }

            CurrentCarId = validated;
            CurrentDefinition = carRegistry.Get(validated);
            saveGateway.Profile.currentCarId = CurrentCarId;
            saveGateway.Profile.OwnCar(CurrentCarId);
            saveGateway.Save();
            if (changed)
            {
                bus.Raise(new FTCarSelectionChangedSignal(CurrentCarId));
            }

            return CurrentCarId;
        }

        public bool TrySetCurrentCar(string carId, bool persistToProfile = true)
        {
            string validated = carRegistry.ValidateOrFallback(carId);
            if (string.IsNullOrWhiteSpace(validated)) return false;

            if (!saveGateway.Profile.OwnsCar(validated))
            {
                Debug.LogWarning($"[SacredCore] Ignored unowned car selection: {validated}");
                return false;
            }

            bool changed = CurrentCarId != validated;
            CurrentCarId = validated;
            CurrentDefinition = carRegistry.Get(validated);
            if (persistToProfile)
            {
                saveGateway.Profile.currentCarId = validated;
                saveGateway.Save();
            }

            if (changed)
            {
                bus.Raise(new FTCarSelectionChangedSignal(validated));
            }

            return true;
        }

        public bool TrySetOwnedCurrentCar(string carId, string source, bool persistToProfile = true)
        {
            string validated = carRegistry.ValidateOrFallback(carId);
            if (string.IsNullOrWhiteSpace(validated))
            {
                Debug.LogError($"[SacredCore] {source} could not select car '{carId}'. No valid fallback exists.");
                return false;
            }

            if (!saveGateway.Profile.OwnsCar(validated))
            {
                Debug.LogWarning($"[SacredCore] {source} rejected unowned car '{validated}'. Restoring profile selection.");
                ForceSyncFromProfile();
                return false;
            }

            return TrySetCurrentCar(validated, persistToProfile);
        }
    }
}

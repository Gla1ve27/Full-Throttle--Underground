using FullThrottle.SacredCore.Garage;
using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Vehicle;
using UnityEngine;

namespace FullThrottle.SacredCore.Audio
{
    /// <summary>
    /// This is the new sacred audio identity layer.
    /// It does not try to be the loop mixer itself.
    /// It decides which profile belongs to the car and exposes stable runtime state.
    /// </summary>
    [DefaultExecutionOrder(-9300)]
    public sealed class FTAudioIdentityDirector : MonoBehaviour
    {
        [SerializeField] private FTAudioProfileRegistry registry;
        private FTSelectedCarRuntime selectedCarRuntime;
        private FTCarRegistry carRegistry;

        public FTVehicleAudioProfile CurrentProfile { get; private set; }

        private void Awake()
        {
            FTServices.Register(this);
            selectedCarRuntime = FTServices.Get<FTSelectedCarRuntime>();
            carRegistry = FTServices.Get<FTCarRegistry>();
            if (registry == null && !FTServices.TryGet(out registry))
            {
                registry = FindFirstObjectByType<FTAudioProfileRegistry>();
            }

            FTSignalBus bus = FTServices.Get<FTSignalBus>();
            bus.Subscribe<FTCarSelectionChangedSignal>(OnCarSelectionChanged);
            RefreshFromCurrentCar();
        }

        private void OnDestroy()
        {
            if (FTServices.TryGet(out FTSignalBus bus))
            {
                bus.Unsubscribe<FTCarSelectionChangedSignal>(OnCarSelectionChanged);
            }
        }

        public void RefreshFromCurrentCar()
        {
            string carId = selectedCarRuntime.CurrentCarId;
            if (!carRegistry.TryGet(carId, out FTCarDefinition car))
            {
                CurrentProfile = null;
                return;
            }

            if (registry != null)
            {
                bool valid = registry.ResolveProfile(car, out FTVehicleAudioProfile profile, out string report);
                CurrentProfile = profile;
                if (CurrentProfile != null)
                {
                    Debug.Log($"[SacredCore] Audio identity resolved valid={valid}. {report}");
                    return;
                }
            }

            CurrentProfile = null;
            Debug.LogWarning($"[SacredCore] Audio identity unresolved for car={car.carId}. Registry missing or profile invalid.");
        }

        private void OnCarSelectionChanged(FTCarSelectionChangedSignal signal)
        {
            RefreshFromCurrentCar();
            Debug.Log($"[SacredCore] Audio identity refreshed for {signal.CarId}. Profile={(CurrentProfile != null ? CurrentProfile.audioProfileId : "None")}");
        }
    }
}

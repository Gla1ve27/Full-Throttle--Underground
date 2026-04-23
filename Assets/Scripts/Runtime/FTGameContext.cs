using FullThrottle.SacredCore.Audio;
using FullThrottle.SacredCore.Garage;
using FullThrottle.SacredCore.Save;
using FullThrottle.SacredCore.Vehicle;
using UnityEngine;

namespace FullThrottle.SacredCore.Runtime
{
    /// <summary>
    /// Read-only runtime context for high-level systems that need to inspect current truth.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    public sealed class FTGameContext : MonoBehaviour
    {
        public FTProfileData Profile => saveGateway != null ? saveGateway.Profile : null;
        public FTCarDefinition SelectedCar { get; private set; }
        public FTVehicleAudioProfile SelectedAudioProfile { get; private set; }

        private FTSaveGateway saveGateway;
        private FTCarRegistry carRegistry;
        private FTSelectedCarRuntime selectedCarRuntime;
        private FTAudioProfileRegistry audioRegistry;
        private FTEventBus eventBus;

        private void Awake()
        {
            FTServices.Register(this);
            FTServices.TryGet(out saveGateway);
            FTServices.TryGet(out carRegistry);
            FTServices.TryGet(out selectedCarRuntime);
            FTServices.TryGet(out audioRegistry);
            FTServices.TryGet(out eventBus);

            if (eventBus != null)
            {
                eventBus.Subscribe<FTCarSelectionChangedSignal>(OnCarSelectionChanged);
            }

            Refresh();
            Debug.Log("[SacredCore] Game context ready.");
        }

        private void OnDestroy()
        {
            if (eventBus != null)
            {
                eventBus.Unsubscribe<FTCarSelectionChangedSignal>(OnCarSelectionChanged);
            }
        }

        public void Refresh()
        {
            string carId = selectedCarRuntime != null ? selectedCarRuntime.CurrentCarId : saveGateway?.Profile.currentCarId;
            SelectedCar = !string.IsNullOrWhiteSpace(carId) && carRegistry != null ? carRegistry.Get(carId) : null;
            SelectedAudioProfile = null;

            if (SelectedCar != null && audioRegistry != null)
            {
                audioRegistry.ResolveProfile(SelectedCar, out FTVehicleAudioProfile profile, out _);
                SelectedAudioProfile = profile;
            }
        }

        private void OnCarSelectionChanged(FTCarSelectionChangedSignal signal)
        {
            Refresh();
            Debug.Log($"[SacredCore] Context selected car={signal.CarId}, audio={(SelectedAudioProfile != null ? SelectedAudioProfile.audioProfileId : "None")}.");
        }
    }
}

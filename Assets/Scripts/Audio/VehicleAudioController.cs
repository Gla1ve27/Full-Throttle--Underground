using UnityEngine;
using Underground.UI;
using Underground.Vehicle;

namespace Underground.Audio
{
    public class VehicleAudioController : MonoBehaviour
    {
        [SerializeField] private GearboxSystem gearbox;
        [SerializeField] private VehicleDynamicsController vehicle;
        [SerializeField] private AudioSource engineSource;
        [SerializeField] private float minPitch = 0.8f;
        [SerializeField] private float maxPitch = 2f;
        [SerializeField] private float minVolume = 0.2f;
        [SerializeField] private float maxVolume = 0.9f;

        private GameSettingsManager settingsManager;

        private void Awake()
        {
            if (gearbox == null)
            {
                gearbox = GetComponentInParent<GearboxSystem>();
            }

            if (vehicle == null)
            {
                vehicle = GetComponentInParent<VehicleDynamicsController>();
            }

            if (engineSource == null)
            {
                engineSource = GetComponent<AudioSource>();
            }

            settingsManager = FindFirstObjectByType<GameSettingsManager>();
            RouteAudioSource();
        }

        private void Update()
        {
            if (gearbox == null || engineSource == null)
            {
                return;
            }

            float pitchT = Mathf.InverseLerp(900f, 7200f, gearbox.CurrentRPM);
            engineSource.pitch = Mathf.Lerp(minPitch, maxPitch, pitchT);

            float speedT = vehicle != null ? Mathf.InverseLerp(0f, 220f, vehicle.SpeedKph) : pitchT;
            engineSource.volume = Mathf.Lerp(minVolume, maxVolume, speedT);
        }

        private void RouteAudioSource()
        {
            if (settingsManager == null)
            {
                settingsManager = FindFirstObjectByType<GameSettingsManager>();
            }

            settingsManager?.RouteAudioSource(engineSource, "SFX");
        }
    }
}

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
        [SerializeField] private AudioSource whineSource;
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

            float minRpm = vehicle != null && vehicle.RuntimeStats != null ? vehicle.RuntimeStats.IdleRPM : 900f;
            float maxRpm = vehicle != null && vehicle.RuntimeStats != null ? vehicle.RuntimeStats.MaxRPM : 7200f;
            float rpmT = Mathf.InverseLerp(minRpm, maxRpm, gearbox.CurrentRPM);
            float pitchCurve = 1f + (rpmT * 0.5f);
            engineSource.pitch = Mathf.Lerp(minPitch, maxPitch, rpmT) * pitchCurve;

            float speedT = vehicle != null ? Mathf.InverseLerp(0f, 220f, vehicle.SpeedKph) : rpmT;
            engineSource.volume = Mathf.Lerp(minVolume, maxVolume, Mathf.Max(speedT, rpmT * 0.8f));

            if (whineSource != null)
            {
                whineSource.pitch = Mathf.Lerp(1.05f, 1.65f, rpmT);
                whineSource.volume = rpmT > 0.9f ? Mathf.InverseLerp(0.9f, 1f, rpmT) * 0.35f : 0f;
            }
        }

        private void RouteAudioSource()
        {
            if (settingsManager == null)
            {
                settingsManager = FindFirstObjectByType<GameSettingsManager>();
            }

            settingsManager?.RouteAudioSource(engineSource, "SFX");
            settingsManager?.RouteAudioSource(whineSource, "SFX");
        }
    }
}

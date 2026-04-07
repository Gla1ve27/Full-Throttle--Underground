using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Underground.UI;
using Underground.Vehicle;

namespace Underground.World
{
    [RequireComponent(typeof(Camera))]
    public class VehicleSpeedEffectsController : MonoBehaviour
    {
        [SerializeField] private VehicleDynamicsController vehicle;
        [SerializeField] private float vignetteBase = 0.18f;
        [SerializeField] private float vignetteMax = 0.33f;
        [SerializeField] private float chromaticBase = 0.015f;
        [SerializeField] private float chromaticMax = 0.08f;
        [SerializeField] private float motionBlurMax = 0.08f;

        private GameSettingsManager settingsManager;
        private Volume runtimeVolume;
        private VolumeProfile runtimeProfile;
        private Vignette vignette;
        private ChromaticAberration chromaticAberration;
        private MotionBlur motionBlur;

        private void Awake()
        {
            settingsManager = FindFirstObjectByType<GameSettingsManager>();
            ResolveVehicle();
            CreateRuntimeVolumeProfile();
        }

        private void Update()
        {
            if (settingsManager == null)
            {
                settingsManager = FindFirstObjectByType<GameSettingsManager>();
            }

            ResolveVehicle();
            if (vehicle == null || runtimeProfile == null)
            {
                return;
            }

            bool effectsEnabled = settingsManager == null || settingsManager.CameraEffectsEnabled;
            float speedT = Mathf.Clamp01(vehicle.SpeedKph / Mathf.Max(1f, vehicle.RuntimeStats != null ? vehicle.RuntimeStats.MaxSpeedKph : 160f));
            float highSpeedPulse = speedT > 0.94f ? (Mathf.Sin(Time.unscaledTime * 9f) * 0.5f + 0.5f) * Mathf.InverseLerp(0.94f, 1f, speedT) : 0f;

            if (vignette != null)
            {
                vignette.intensity.Override(effectsEnabled ? Mathf.Lerp(vignetteBase, vignetteMax, speedT) : vignetteBase);
            }

            if (chromaticAberration != null)
            {
                float intensity = effectsEnabled
                    ? Mathf.Lerp(chromaticBase, chromaticMax, Mathf.SmoothStep(0f, 1f, speedT)) + highSpeedPulse * 0.04f
                    : 0f;
                chromaticAberration.intensity.Override(Mathf.Clamp01(intensity));
            }

            if (motionBlur != null)
            {
                motionBlur.intensity.Override(effectsEnabled ? Mathf.Lerp(0f, motionBlurMax, Mathf.SmoothStep(0f, 1f, speedT)) : 0f);
            }
        }

        private void ResolveVehicle()
        {
            if (vehicle == null)
            {
                vehicle = FindFirstObjectByType<VehicleDynamicsController>();
            }
        }

        private void CreateRuntimeVolumeProfile()
        {
            Volume sourceVolume = FindFirstObjectByType<Volume>();
            if (sourceVolume == null || sourceVolume.sharedProfile == null)
            {
                return;
            }

            runtimeVolume = sourceVolume;
            runtimeProfile = Instantiate(sourceVolume.sharedProfile);
            runtimeProfile.name = $"{sourceVolume.sharedProfile.name}_Runtime";
            runtimeVolume.profile = runtimeProfile;
            runtimeProfile.TryGet(out vignette);
            runtimeProfile.TryGet(out chromaticAberration);
            runtimeProfile.TryGet(out motionBlur);
        }
    }
}

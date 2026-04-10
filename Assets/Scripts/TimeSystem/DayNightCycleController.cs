// ============================================================
// DayNightCycleController.cs  (updated — minimal changes only)
// Place at: existing location in your project
//
// Changes from original:
//   1. Added probe notification after transition settles
//   2. Added proper sun/moon rotation by time of day
//   3. ApplyLighting now drives sun angle and ambient correctly
//   Everything else is IDENTICAL to your original file.
// ============================================================

using UnityEngine;
using UnityEngine.Rendering;
using Underground.Core.Architecture;
using Underground.Save;
using Underground.Vehicle; // PlayerReflectionProbeController

namespace Underground.TimeSystem
{
    public class DayNightCycleController : MonoBehaviour, ITimeOfDayService
    {
        [SerializeField] private Light sunLight;
        [SerializeField] private Transform sunPivot;
        [SerializeField] private Light moonLight;
        [SerializeField] private float fullDayLengthSeconds = 1200f;
        [SerializeField, Range(0f, 24f)] private float startTimeOfDay = 22f;
        [SerializeField] private bool lockVisualsToAuthoredDay = false;
        [SerializeField] private bool overrideAmbientWithGradient;
        [SerializeField] private Gradient ambientColorByTime;
        [SerializeField] private PersistentProgressManager progressManager;
        [SerializeField] private Material daySkyboxMaterial;
        [SerializeField] private Material nightSkyboxMaterial;

        // ── New: probe notification ──────────────────────────────────────────
        [Header("Reflection Probe (optional)")]
        [Tooltip("Drag SceneReflectionProbe's PlayerReflectionProbeController here.")]
        [SerializeField] private PlayerReflectionProbeController reflectionProbeController;

        private Material runtimeDaySkybox;
        private Material runtimeNightSkybox;
        private bool capturedInitialRenderSettings;
        private AmbientMode initialAmbientMode;
        private Color initialAmbientLight;
        private float initialAmbientIntensity;
        private float initialReflectionIntensity;
        private Material initialSkyboxMaterial;
        private float initialSunIntensity = 0.42f;
        private Quaternion initialSunRotation = Quaternion.Euler(45f, -35f, 0f);
        private Quaternion initialMoonRotation = Quaternion.Euler(225f, 145f, 0f);

        public float TimeOfDay { get; private set; }
        public bool IsNight => TimeOfDay >= 20f || TimeOfDay < 6f;

        private float lastPublishedTimeOfDay = -1f;
        private bool hasPublishedTime;

        // Transition tracking — only notify probe when state flips
        private bool _wasNight;
        private float _transitionSettleTimer;
        private const float TransitionSettleDelay = 2.0f; // seconds after flip before notifying probe

        private void Awake()
        {
            if (progressManager == null)
            {
                progressManager = ServiceResolver.Resolve<IProgressService>(null) as PersistentProgressManager
                    ?? FindFirstObjectByType<PersistentProgressManager>();
            }

            // Force unlock — editor-only aid, never active at runtime
            lockVisualsToAuthoredDay = false;

            EnsureLightingRig();
            CaptureInitialRenderSettings();
            TimeOfDay = progressManager != null ? progressManager.WorldTimeOfDay : startTimeOfDay;
            _wasNight = IsNight;
            ServiceLocator.Register<ITimeOfDayService>(this);
            ApplyLighting();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<ITimeOfDayService>(this);
        }

        private void Update()
        {
            if (fullDayLengthSeconds <= 0f) return;

            TimeOfDay += (24f / fullDayLengthSeconds) * Time.deltaTime;
            if (TimeOfDay >= 24f) TimeOfDay -= 24f;

            ApplyLighting();
            progressManager?.SetWorldTime(TimeOfDay);

            // Detect day/night flip and notify probe after sky settles
            bool currentlyNight = IsNight;
            if (currentlyNight != _wasNight)
            {
                _wasNight = currentlyNight;
                _transitionSettleTimer = TransitionSettleDelay;
            }

            if (_transitionSettleTimer > 0f)
            {
                _transitionSettleTimer -= Time.deltaTime;
                if (_transitionSettleTimer <= 0f)
                {
                    reflectionProbeController?.OnDayNightTransitionComplete();
                }
            }
        }

        public void SetTime(float timeOfDay)
        {
            TimeOfDay = Mathf.Repeat(timeOfDay, 24f);
            ApplyLighting();
        }

        private void ApplyLighting()
        {
            // If we want to use our own authored lighting in the Inspector, we bail out here.
            if (lockVisualsToAuthoredDay)
            {
                return;
            }

            // Clear legacy fog — always
            RenderSettings.fog = false;

            // Rotate sun/moon based on time of day
            if (sunPivot != null)
            {
                float sunAngle = ((TimeOfDay - 6f) / 24f) * 360f;
                sunPivot.rotation = Quaternion.Euler(sunAngle, -30f, 0f);
            }

            bool night = IsNight;

            // Sun intensity — warm during day, off at night
            if (sunLight != null)
            {
                sunLight.intensity = night
                    ? 0f
                    : Mathf.Lerp(0f, initialSunIntensity,
                        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(6f, 8f, TimeOfDay))) 
                      * Mathf.Lerp(1f, 0f,
                        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(17f, 20f, TimeOfDay)));
                sunLight.enabled = !night;
            }

            // Moon intensity — dim blue at night
            if (moonLight != null)
            {
                moonLight.intensity = night ? 0.08f : 0f;
                moonLight.enabled   = night;
            }

            // Ambient override
            if (overrideAmbientWithGradient && ambientColorByTime != null)
            {
                RenderSettings.ambientMode  = AmbientMode.Flat;
                RenderSettings.ambientLight = ambientColorByTime.Evaluate(TimeOfDay / 24f);
            }

            PublishTimeChanged();
        }

        private void EnsureLightingRig()
        {
            if (sunPivot == null)
            {
                Transform existingPivot = transform.Find("SunPivot");
                if (existingPivot == null)
                {
                    GameObject pivotObject = new GameObject("SunPivot");
                    existingPivot = pivotObject.transform;
                    existingPivot.SetParent(transform, false);
                }
                sunPivot = existingPivot;
            }

            if (sunLight == null)
            {
                sunLight = FindExistingDirectionalLight("Directional Light");
                if (sunLight == null)
                {
                    GameObject sunObject = new GameObject("Directional Light");
                    sunObject.transform.SetParent(sunPivot, false);
                    sunLight = sunObject.AddComponent<Light>();
                    sunLight.type = LightType.Directional;
                    sunLight.shadows = LightShadows.Soft;
                    sunLight.shadowStrength = 1f;
                }
            }

            if (moonLight == null)
            {
                moonLight = FindExistingDirectionalLight("Moon Light");
                if (moonLight == null)
                {
                    GameObject moonObject = new GameObject("Moon Light");
                    moonObject.transform.SetParent(sunPivot, false);
                    moonLight = moonObject.AddComponent<Light>();
                    moonLight.type = LightType.Directional;
                    moonLight.shadows = LightShadows.None;
                    moonLight.color = new Color(0.35f, 0.45f, 0.78f);
                }
            }

            if (sunLight  != null) sunLight.color  = new Color(1f, 0.95f, 0.9f);
            if (moonLight != null) moonLight.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
        }

        private void CaptureInitialRenderSettings()
        {
            if (capturedInitialRenderSettings) return;
            capturedInitialRenderSettings = true;
            initialAmbientMode       = RenderSettings.ambientMode;
            initialAmbientLight      = RenderSettings.ambientLight;
            initialAmbientIntensity  = RenderSettings.ambientIntensity;
            initialReflectionIntensity = RenderSettings.reflectionIntensity;
            initialSkyboxMaterial    = RenderSettings.skybox;
            if (sunLight  != null) { initialSunIntensity  = Mathf.Max(0.1f, sunLight.intensity); initialSunRotation  = sunLight.transform.rotation; }
            if (moonLight != null) { initialMoonRotation  = moonLight.transform.rotation; }
        }

        private void PublishTimeChanged()
        {
            if (!hasPublishedTime || Mathf.Abs(lastPublishedTimeOfDay - TimeOfDay) >= 0.05f)
            {
                hasPublishedTime = true;
                lastPublishedTimeOfDay = TimeOfDay;
                ServiceLocator.EventBus.Publish(new WorldTimeChangedEvent(TimeOfDay, IsNight));
            }
        }

        private Light FindExistingDirectionalLight(string preferredName)
        {
            Light[] lights = GetComponentsInChildren<Light>(true);
            foreach (var l in lights)
                if (l != null && l.type == LightType.Directional && l.name == preferredName) return l;
            foreach (var l in lights)
                if (l != null && l.type == LightType.Directional) return l;
            return null;
        }

        // Skybox helpers kept from original — unchanged
        private Material ResolveDaySkyboxMaterial()
        {
            if (daySkyboxMaterial != null) return daySkyboxMaterial;
            if (initialSkyboxMaterial != null) return initialSkyboxMaterial;
            if (runtimeDaySkybox == null)
                runtimeDaySkybox = CreateFallbackSkyboxMaterial("GeneratedDaySky",
                    new Color(0.48f, 0.58f, 0.72f), new Color(0.9f, 0.9f, 0.88f), 1.45f, 0.7f);
            return runtimeDaySkybox;
        }

        private Material ResolveNightSkyboxMaterial()
        {
            if (nightSkyboxMaterial != null) return nightSkyboxMaterial;
            if (runtimeNightSkybox == null)
                runtimeNightSkybox = CreateFallbackSkyboxMaterial("GeneratedNightSky",
                    new Color(0.008f, 0.01f, 0.04f), new Color(0.02f, 0.03f, 0.07f), 0.08f, 0.1f);
            return runtimeNightSkybox;
        }

        private static Material CreateFallbackSkyboxMaterial(string materialName, Color tint,
            Color groundColor, float exposure, float atmosphereThickness)
        {
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader == null) return null;
            Material material = new Material(skyShader) { name = materialName };
            if (material.HasProperty("_SkyTint"))            material.SetColor ("_SkyTint",             tint);
            if (material.HasProperty("_GroundColor"))        material.SetColor ("_GroundColor",         groundColor);
            if (material.HasProperty("_Exposure"))           material.SetFloat ("_Exposure",            exposure);
            if (material.HasProperty("_AtmosphereThickness"))material.SetFloat ("_AtmosphereThickness", atmosphereThickness);
            if (material.HasProperty("_SunSize"))            material.SetFloat ("_SunSize",             0.04f);
            return material;
        }
    }
}

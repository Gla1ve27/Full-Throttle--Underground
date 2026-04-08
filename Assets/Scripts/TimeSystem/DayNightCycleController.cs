using UnityEngine;
using UnityEngine.Rendering;
using Underground.Core.Architecture;
using Underground.Save;

namespace Underground.TimeSystem
{
    public class DayNightCycleController : MonoBehaviour, ITimeOfDayService
    {
        [SerializeField] private Light sunLight;
        [SerializeField] private Transform sunPivot;
        [SerializeField] private Light moonLight;
        [SerializeField] private float fullDayLengthSeconds = 1200f;
        [SerializeField, Range(0f, 24f)] private float startTimeOfDay = 12f;
        [SerializeField] private bool lockVisualsToAuthoredDay = true;
        [SerializeField] private bool overrideAmbientWithGradient;
        [SerializeField] private Gradient ambientColorByTime;
        [SerializeField] private PersistentProgressManager progressManager;
        [SerializeField] private Material daySkyboxMaterial;
        [SerializeField] private Material nightSkyboxMaterial;

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

        private void Awake()
        {
            if (progressManager == null)
            {
                progressManager = ServiceResolver.Resolve<IProgressService>(null) as PersistentProgressManager
                    ?? FindFirstObjectByType<PersistentProgressManager>();
            }

            EnsureLightingRig();
            CaptureInitialRenderSettings();
            TimeOfDay = lockVisualsToAuthoredDay
                ? startTimeOfDay
                : (progressManager != null ? progressManager.WorldTimeOfDay : startTimeOfDay);
            ServiceLocator.Register<ITimeOfDayService>(this);
            ApplyLighting();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<ITimeOfDayService>(this);
        }

        private void Update()
        {
            if (lockVisualsToAuthoredDay || fullDayLengthSeconds <= 0f)
            {
                return;
            }

            TimeOfDay += (24f / fullDayLengthSeconds) * Time.deltaTime;
            if (TimeOfDay >= 24f)
            {
                TimeOfDay -= 24f;
            }

            ApplyLighting();
            progressManager?.SetWorldTime(TimeOfDay);
        }

        public void SetTime(float timeOfDay)
        {
            TimeOfDay = lockVisualsToAuthoredDay ? startTimeOfDay : Mathf.Repeat(timeOfDay, 24f);
            ApplyLighting();
        }

        private void ApplyLighting()
        {
            EnsureLightingRig();

            if (lockVisualsToAuthoredDay)
            {
                ApplyAuthoredDayLook();
                PublishTimeChanged();
                return;
            }

            float normalizedTime = TimeOfDay / 24f;

            if (sunPivot != null)
            {
                sunPivot.rotation = Quaternion.Euler((normalizedTime * 360f) - 90f, 170f, 0f);
            }
            else if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Euler((normalizedTime * 360f) - 90f, 170f, 0f);
            }

            if (sunLight != null)
            {
                float dayFactor = Mathf.Clamp01(Vector3.Dot(-sunLight.transform.forward, Vector3.up));
                sunLight.intensity = Mathf.Lerp(0.015f, initialSunIntensity, dayFactor);
                sunLight.enabled = dayFactor > 0.01f;
                RenderSettings.sun = sunLight;
            }

            if (moonLight != null)
            {
                float nightFactor = Mathf.Clamp01(Vector3.Dot(-moonLight.transform.forward, Vector3.up));
                moonLight.intensity = Mathf.Lerp(0f, 0.12f, nightFactor);
                moonLight.enabled = nightFactor > 0.01f;
            }

            Color nightAmbient = new Color(0.04f, 0.05f, 0.08f);
            Color dayAmbient = capturedInitialRenderSettings ? initialAmbientLight : new Color(0.24f, 0.25f, 0.28f);
            float ambientBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((TimeOfDay - 5.5f) / 7f));
            if (TimeOfDay >= 18f)
            {
                ambientBlend = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((TimeOfDay - 18f) / 3f));
            }

            if (overrideAmbientWithGradient && ambientColorByTime != null && ambientColorByTime.colorKeys.Length > 0)
            {
                RenderSettings.ambientLight = ambientColorByTime.Evaluate(normalizedTime);
            }
            else
            {
                RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, ambientBlend);
            }

            RenderSettings.ambientIntensity = Mathf.Lerp(0.08f, initialAmbientIntensity, ambientBlend);
            RenderSettings.reflectionIntensity = Mathf.Lerp(0.08f, initialReflectionIntensity, ambientBlend);

            Material targetSkybox = IsNight ? ResolveNightSkyboxMaterial() : ResolveDaySkyboxMaterial();
            RenderSettings.ambientMode = initialAmbientMode;
            if (targetSkybox != null && RenderSettings.skybox != targetSkybox)
            {
                RenderSettings.skybox = targetSkybox;
                DynamicGI.UpdateEnvironment();
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
                    moonLight.color = new Color(0.5f, 0.6f, 1f);
                }
            }

            if (sunLight != null)
            {
                sunLight.color = new Color(1f, 0.95f, 0.9f);
            }

            if (moonLight != null)
            {
                moonLight.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            }
        }

        private void CaptureInitialRenderSettings()
        {
            if (capturedInitialRenderSettings)
            {
                return;
            }

            capturedInitialRenderSettings = true;
            initialAmbientMode = RenderSettings.ambientMode;
            initialAmbientLight = RenderSettings.ambientLight;
            initialAmbientIntensity = RenderSettings.ambientIntensity;
            initialReflectionIntensity = RenderSettings.reflectionIntensity;
            initialSkyboxMaterial = RenderSettings.skybox;
            if (sunLight != null)
            {
                initialSunIntensity = Mathf.Max(0.1f, sunLight.intensity);
                initialSunRotation = sunLight.transform.rotation;
            }

            if (moonLight != null)
            {
                initialMoonRotation = moonLight.transform.rotation;
            }
        }

        private void ApplyAuthoredDayLook()
        {
            TimeOfDay = startTimeOfDay;

            if (sunLight != null)
            {
                sunLight.transform.rotation = initialSunRotation;
                sunLight.intensity = initialSunIntensity;
                sunLight.enabled = true;
                RenderSettings.sun = sunLight;
            }

            if (moonLight != null)
            {
                moonLight.transform.rotation = initialMoonRotation;
                moonLight.intensity = 0f;
                moonLight.enabled = false;
            }

            RenderSettings.ambientMode = initialAmbientMode;
            RenderSettings.ambientLight = initialAmbientLight;
            RenderSettings.ambientIntensity = initialAmbientIntensity;
            RenderSettings.reflectionIntensity = initialReflectionIntensity;

            Material targetSkybox = ResolveDaySkyboxMaterial();
            if (targetSkybox != null && RenderSettings.skybox != targetSkybox)
            {
                RenderSettings.skybox = targetSkybox;
                DynamicGI.UpdateEnvironment();
            }
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
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional && lights[i].name == preferredName)
                {
                    return lights[i];
                }
            }

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    return lights[i];
                }
            }

            return null;
        }

        private Material ResolveDaySkyboxMaterial()
        {
            if (daySkyboxMaterial != null)
            {
                return daySkyboxMaterial;
            }

            if (initialSkyboxMaterial != null)
            {
                return initialSkyboxMaterial;
            }

            if (runtimeDaySkybox == null)
            {
                runtimeDaySkybox = CreateFallbackSkyboxMaterial(
                    "GeneratedDaySky",
                    new Color(0.48f, 0.58f, 0.72f),
                    new Color(0.9f, 0.9f, 0.88f),
                    1.45f,
                    0.7f);
            }

            return runtimeDaySkybox;
        }

        private Material ResolveNightSkyboxMaterial()
        {
            if (nightSkyboxMaterial != null)
            {
                return nightSkyboxMaterial;
            }

            if (runtimeNightSkybox == null)
            {
                runtimeNightSkybox = CreateFallbackSkyboxMaterial(
                    "GeneratedNightSky",
                    new Color(0.02f, 0.03f, 0.08f),
                    new Color(0.08f, 0.12f, 0.2f),
                    0.25f,
                    0.2f);
            }

            return runtimeNightSkybox;
        }

        private static Material CreateFallbackSkyboxMaterial(string materialName, Color tint, Color groundColor, float exposure, float atmosphereThickness)
        {
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader == null)
            {
                return null;
            }

            Material material = new Material(skyShader)
            {
                name = materialName
            };

            if (material.HasProperty("_SkyTint"))
            {
                material.SetColor("_SkyTint", tint);
            }

            if (material.HasProperty("_GroundColor"))
            {
                material.SetColor("_GroundColor", groundColor);
            }

            if (material.HasProperty("_Exposure"))
            {
                material.SetFloat("_Exposure", exposure);
            }

            if (material.HasProperty("_AtmosphereThickness"))
            {
                material.SetFloat("_AtmosphereThickness", atmosphereThickness);
            }

            if (material.HasProperty("_SunSize"))
            {
                material.SetFloat("_SunSize", 0.04f);
            }

            return material;
        }
    }
}

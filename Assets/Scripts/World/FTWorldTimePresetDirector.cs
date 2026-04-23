using System;
using System.Collections.Generic;
using Underground.TimeSystem;
using UnityEngine;
using UnityEngine.Rendering;

namespace FullThrottle.SacredCore.World
{
    [DefaultExecutionOrder(-9100)]
    public sealed class FTWorldTimePresetDirector : MonoBehaviour
    {
        [SerializeField] private FTWorldTimePreset defaultPreset;
        [SerializeField] private List<FTWorldTimePreset> presets = new();
        [SerializeField] private Light directionalLight;
        [SerializeField] private Volume globalVolume;
        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool freezeGameplayTime = true;
        [SerializeField] private bool disableLegacyLiveTimeComponents = true;
        [SerializeField] private bool forceDuskNightOnly = true;
        [SerializeField, Range(0f, 24f)] private float enforcedDuskNightHour = PackageTimeOfDayUtility.DefaultDuskNightHour;

        private readonly Dictionary<string, FTWorldTimePreset> presetMap = new(StringComparer.OrdinalIgnoreCase);
        private FTWorldTimePreset activePreset;

        public FTWorldTimePreset ActivePreset => activePreset;

        private void Awake()
        {
            RebuildPresetMap();
            ResolveReferences();
        }

        private void Start()
        {
            if (forceDuskNightOnly)
            {
                ApplyLegacyDuskNightLock(enforcedDuskNightHour);
            }

            if (disableLegacyLiveTimeComponents)
            {
                DisableLegacyLiveTimeComponents();
            }

            if (applyOnStart)
            {
                ApplyPreset(defaultPreset != null ? defaultPreset.presetId : "night_free_roam");
            }
        }

        public bool ApplyPreset(string presetId)
        {
            RebuildPresetMap();
            ResolveReferences();

            FTWorldTimePreset preset = ResolvePreset(presetId);
            if (preset == null)
            {
                Debug.LogWarning($"[SacredCore] World time preset '{presetId}' missing. Keeping current lighting.");
                return false;
            }

            activePreset = preset;
            float gameplayHour = forceDuskNightOnly || preset.duskNightOnly
                ? PackageTimeOfDayUtility.ConstrainToDuskNightHours(preset.gameplayHour)
                : Mathf.Repeat(preset.gameplayHour, 24f);

            if (globalVolume != null && preset.volumeProfile != null)
            {
                globalVolume.profile = preset.volumeProfile;
                globalVolume.weight = 1f;
            }

            if (preset.skyboxMaterial != null)
            {
                RenderSettings.skybox = preset.skyboxMaterial;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = preset.ambientColor;
            RenderSettings.fog = preset.enableFog;
            RenderSettings.fogColor = preset.fogColor;
            RenderSettings.fogDensity = preset.fogDensity;

            if (directionalLight != null)
            {
                directionalLight.transform.rotation = Quaternion.Euler(preset.sunEuler);
                directionalLight.color = preset.sunColor;
                directionalLight.intensity = preset.sunIntensity;
                directionalLight.shadows = preset.sunShadows;
            }

            QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance <= 0f ? preset.shadowDistance : QualitySettings.shadowDistance, preset.shadowDistance);
            QualitySettings.shadowCascades = Mathf.Min(QualitySettings.shadowCascades, preset.shadowCascades);

            if (freezeGameplayTime || preset.gameplayTimeScale <= 0f)
            {
                SetLegacyTimeScale(0f);
            }
            else
            {
                SetLegacyTimeScale(preset.gameplayTimeScale);
            }

            if (forceDuskNightOnly || preset.duskNightOnly)
            {
                ApplyLegacyDuskNightLock(gameplayHour);
            }
            else
            {
                SetLegacyTimeHours(gameplayHour);
            }

            Debug.Log($"[SacredCore] World time preset applied: {preset.presetId} ({preset.displayName}). hour={gameplayHour:0.00}, duskNightOnly={forceDuskNightOnly || preset.duskNightOnly}, freeze={freezeGameplayTime}, volume={(preset.volumeProfile != null ? preset.volumeProfile.name : "none")}, sky={(preset.skyboxMaterial != null ? preset.skyboxMaterial.name : "current")}.");
            return true;
        }

        private void RebuildPresetMap()
        {
            presetMap.Clear();
            if (defaultPreset != null && !string.IsNullOrWhiteSpace(defaultPreset.presetId))
            {
                presetMap[defaultPreset.presetId] = defaultPreset;
            }

            for (int i = 0; i < presets.Count; i++)
            {
                FTWorldTimePreset preset = presets[i];
                if (preset != null && !string.IsNullOrWhiteSpace(preset.presetId))
                {
                    presetMap[preset.presetId] = preset;
                }
            }
        }

        private FTWorldTimePreset ResolvePreset(string presetId)
        {
            if (!string.IsNullOrWhiteSpace(presetId) && presetMap.TryGetValue(presetId, out FTWorldTimePreset preset))
            {
                return preset;
            }

            return defaultPreset != null ? defaultPreset : presets.Find(item => item != null);
        }

        private void ResolveReferences()
        {
            if (directionalLight == null)
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null && lights[i].type == LightType.Directional)
                    {
                        directionalLight = lights[i];
                        break;
                    }
                }
            }

            if (globalVolume == null)
            {
                Volume[] volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < volumes.Length; i++)
                {
                    if (volumes[i] != null && volumes[i].isGlobal)
                    {
                        globalVolume = volumes[i];
                        break;
                    }
                }
            }
        }

        private static void DisableLegacyLiveTimeComponents()
        {
            int disabled = 0;
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || !behaviour.enabled)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                if (typeName != "SunRotation" && typeName != "TimeOfDay")
                {
                    continue;
                }

                behaviour.enabled = false;
                disabled++;
            }

            if (disabled > 0)
            {
                Debug.Log($"[SacredCore] Disabled {disabled} legacy live time components. Fixed dusk/night presets now own gameplay lighting.");
            }
        }

        private static void SetLegacyTimeScale(float value)
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour.GetType().Name != "TimeOfDay")
                {
                    continue;
                }

                System.Reflection.FieldInfo field = behaviour.GetType().GetField("time_scale");
                if (field != null && field.FieldType == typeof(float))
                {
                    field.SetValue(behaviour, value);
                }
            }
        }

        private static void SetLegacyTimeHours(float hours)
        {
            hours = Mathf.Repeat(hours, 24f);

            global::SunRotation[] sunRotations = FindObjectsByType<global::SunRotation>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sunRotations.Length; i++)
            {
                if (sunRotations[i] != null)
                {
                    sunRotations[i].SetTime(hours);
                }
            }

            global::TimeOfDay[] clocks = FindObjectsByType<global::TimeOfDay>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < clocks.Length; i++)
            {
                PackageTimeOfDayUtility.SetUnrestrictedHours(clocks[i], hours);
            }
        }

        private static void ApplyLegacyDuskNightLock(float hours)
        {
            hours = PackageTimeOfDayUtility.ConstrainToDuskNightHours(hours);
            SetLegacyTimeHours(hours);

            global::DayNight[] dayNightSystems = FindObjectsByType<global::DayNight>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < dayNightSystems.Length; i++)
            {
                global::DayNight dayNight = dayNightSystems[i];
                if (dayNight == null)
                {
                    continue;
                }

                dayNight.duskNightOnly = true;
                dayNight.isNight = true;
                dayNight.night = true;
                dayNight.SetStreetLights(true);
                dayNight.ChangeVolume();
            }
        }
    }
}

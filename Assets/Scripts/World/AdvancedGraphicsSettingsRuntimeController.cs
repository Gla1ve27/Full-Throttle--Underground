using System;
using System.Reflection;
using FCG;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Underground.UI;
using Underground.Vehicle;

namespace Underground.World
{
    [DefaultExecutionOrder(250)]
    public sealed class AdvancedGraphicsSettingsRuntimeController : MonoBehaviour
    {
        private const float RefreshIntervalSeconds = 1f;

        private static AdvancedGraphicsSettingsRuntimeController instance;

        private GameSettingsManager settingsManager;
        private Volume runtimeVolume;
        private VolumeProfile runtimeProfile;
        private VolumeComponent bloom;
        private VolumeComponent depthOfField;
        private VolumeComponent colorAdjustments;
        private VolumeComponent screenSpaceReflection;
        private float nextRefreshTime;
        private bool pendingApply = true;
        private int sceneWarmupRefreshesRemaining;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
            {
                return;
            }

            GameObject controllerObject = new GameObject("AdvancedGraphicsSettingsRuntimeController");
            DontDestroyOnLoad(controllerObject);
            instance = controllerObject.AddComponent<AdvancedGraphicsSettingsRuntimeController>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ResolveSettingsManager();
            EnsureRuntimeVolume();
            pendingApply = true;
            sceneWarmupRefreshesRemaining = 3;
            ApplyAllSettings();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (settingsManager != null)
            {
                settingsManager.SettingsChanged -= HandleSettingsChanged;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
            if (!pendingApply && sceneWarmupRefreshesRemaining <= 0)
            {
                return;
            }

            ResolveSettingsManager();
            ApplyAllSettings();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveSettingsManager();
            EnsureRuntimeVolume();
            pendingApply = true;
            sceneWarmupRefreshesRemaining = 3;
            ApplyAllSettings();
        }

        private void HandleSettingsChanged()
        {
            pendingApply = true;
            ApplyAllSettings();
        }

        private void ResolveSettingsManager()
        {
            GameSettingsManager resolved = GameSettingsManager.Instance ?? FindFirstObjectByType<GameSettingsManager>();
            if (resolved == settingsManager)
            {
                return;
            }

            if (settingsManager != null)
            {
                settingsManager.SettingsChanged -= HandleSettingsChanged;
            }

            settingsManager = resolved;
            if (settingsManager != null)
            {
                settingsManager.SettingsChanged += HandleSettingsChanged;
            }
        }

        private void ApplyAllSettings()
        {
            if (settingsManager == null)
            {
                return;
            }

            pendingApply = false;
            if (sceneWarmupRefreshesRemaining > 0)
            {
                sceneWarmupRefreshesRemaining--;
            }

            ApplyQualityAndDisplaySettings();
            EnsureRuntimeVolume();
            ApplyVolumeOverrides();
            ApplyCameraSettings();
            ApplyVehicleReflectionSettings();
            ApplyVehicleGeometrySettings();
            ApplyTrafficSettings();
            ApplyParticleSettings();
        }

        private void ApplyQualityAndDisplaySettings()
        {
            RenderSettings.fog = settingsManager.FogEnabled || settingsManager.HorizonFogEnabled;
            if (RenderSettings.fog)
            {
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = settingsManager.HorizonFogEnabled ? 0.0045f : 0.0025f;
            }

            QualitySettings.antiAliasing = settingsManager.FullScreenAntiAliasing switch
            {
                0 => 0,
                1 => 2,
                _ => 4
            };

            QualitySettings.anisotropicFiltering = settingsManager.TextureFiltering switch
            {
                0 => AnisotropicFiltering.Disable,
                1 => AnisotropicFiltering.Enable,
                _ => AnisotropicFiltering.ForceEnable
            };

            QualitySettings.lodBias = settingsManager.WorldDetail switch
            {
                0 => 0.6f,
                1 => 1f,
                _ => 1.6f
            };

            QualitySettings.maximumLODLevel = settingsManager.WorldDetail switch
            {
                0 => 2,
                1 => 1,
                _ => 0
            };
        }

        private void EnsureRuntimeVolume()
        {
            if (runtimeVolume != null && runtimeProfile != null)
            {
                return;
            }

            runtimeVolume = GetComponent<Volume>();
            if (runtimeVolume == null)
            {
                runtimeVolume = gameObject.AddComponent<Volume>();
            }

            runtimeVolume.isGlobal = true;
            runtimeVolume.priority = 500f;

            runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeProfile.name = "AdvancedGraphicsSettings_Runtime";
            runtimeVolume.profile = runtimeProfile;

            bloom = GetOrAddVolumeComponent(
                runtimeProfile,
                "UnityEngine.Rendering.HighDefinition.Bloom, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.Bloom, Unity.RenderPipelines.Universal.Runtime");
            depthOfField = GetOrAddVolumeComponent(
                runtimeProfile,
                "UnityEngine.Rendering.HighDefinition.DepthOfField, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.DepthOfField, Unity.RenderPipelines.Universal.Runtime");
            colorAdjustments = GetOrAddVolumeComponent(
                runtimeProfile,
                "UnityEngine.Rendering.HighDefinition.ColorAdjustments, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.ColorAdjustments, Unity.RenderPipelines.Universal.Runtime");
            screenSpaceReflection = GetOrAddVolumeComponent(
                runtimeProfile,
                "UnityEngine.Rendering.HighDefinition.ScreenSpaceReflection, Unity.RenderPipelines.HighDefinition.Runtime");
        }

        private void ApplyVolumeOverrides()
        {
            bool useBloom = settingsManager.LightGlowEnabled || settingsManager.OverBrightEnabled;
            SetComponentActive(bloom, useBloom);
            SetVolumeFloat(bloom, "threshold", 2f);
            SetVolumeFloat(bloom, "intensity", settingsManager.OverBrightEnabled ? 0.08f : settingsManager.LightGlowEnabled ? 0.03f : 0f);
            SetVolumeFloat(bloom, "scatter", 0.18f);

            bool useDepthOfField = settingsManager.DepthOfFieldEnabled;
            SetComponentActive(depthOfField, useDepthOfField);
            SetVolumeEnum(depthOfField, "focusMode", "Manual");
            SetVolumeFloat(depthOfField, "focusDistance", 14f);
            SetVolumeFloat(depthOfField, "nearFocusStart", 5f);
            SetVolumeFloat(depthOfField, "nearFocusEnd", 8f);
            SetVolumeFloat(depthOfField, "farFocusStart", 22f);
            SetVolumeFloat(depthOfField, "farFocusEnd", 80f);
            SetVolumeFloat(depthOfField, "gaussianStart", 12f);
            SetVolumeFloat(depthOfField, "gaussianEnd", 45f);
            SetVolumeFloat(depthOfField, "gaussianMaxRadius", 0.6f);

            bool useColorAdjustments = settingsManager.TintingEnabled || settingsManager.OverBrightEnabled || settingsManager.AdvancedContrastEnabled;
            SetComponentActive(colorAdjustments, useColorAdjustments);
            SetVolumeFloat(colorAdjustments, "postExposure", settingsManager.OverBrightEnabled ? 0.3f : 0f);
            SetVolumeFloat(colorAdjustments, "contrast", settingsManager.AdvancedContrastEnabled ? 12f : 0f);
            SetVolumeFloat(colorAdjustments, "saturation", settingsManager.TintingEnabled ? 6f : 0f);
            SetVolumeColor(colorAdjustments, "colorFilter", settingsManager.TintingEnabled ? new Color(0.97f, 1f, 0.98f, 1f) : Color.white);

            bool useSsr = screenSpaceReflection != null;
            SetComponentActive(screenSpaceReflection, useSsr);
            SetVolumeBool(screenSpaceReflection, "enabled", true);
            SetVolumeBool(screenSpaceReflection, "enabledTransparent", true);
            SetVolumeBool(screenSpaceReflection, "reflectSky", true);
            SetVolumeFloat(screenSpaceReflection, "m_MinSmoothness", settingsManager.RoadReflectionDetail switch
            {
                0 => 0.4f,
                1 => 0.32f,
                _ => 0.24f
            });
            SetVolumeFloat(screenSpaceReflection, "m_SmoothnessFadeStart", settingsManager.RoadReflectionDetail switch
            {
                0 => 0.26f,
                1 => 0.2f,
                _ => 0.16f
            });
            SetVolumeFloat(screenSpaceReflection, "m_RayLength", settingsManager.RoadReflectionDetail switch
            {
                0 => 42f,
                1 => 72f,
                _ => 96f
            });
            SetVolumeFloat(screenSpaceReflection, "m_ClampValue", settingsManager.RoadReflectionDetail switch
            {
                0 => 8f,
                1 => 11f,
                _ => 14f
            });
            SetVolumeFloat(screenSpaceReflection, "m_DenoiserRadius", settingsManager.RoadReflectionDetail switch
            {
                0 => 0.45f,
                1 => 0.6f,
                _ => 0.75f
            });
            SetVolumeFloat(screenSpaceReflection, "screenFadeDistance", settingsManager.RoadReflectionDetail switch
            {
                0 => 0.12f,
                1 => 0.08f,
                _ => 0.06f
            });
            SetVolumeBool(screenSpaceReflection, "m_FullResolution", settingsManager.RoadReflectionDetail >= 2);
        }

        private void ApplyCameraSettings()
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera camera = cameras[index];
                if (camera == null)
                {
                    continue;
                }

                ApplyHdrpCameraAntialiasing(camera);
                ApplyUrpCameraAntialiasing(camera);
            }
        }

        private void ApplyVehicleReflectionSettings()
        {
            PlayerReflectionProbeController[] probes = FindObjectsByType<PlayerReflectionProbeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Vector3 probeSize = settingsManager.CarReflectionDetail switch
            {
                0 => new Vector3(84f, 24f, 84f),
                1 => new Vector3(108f, 30f, 108f),
                _ => new Vector3(132f, 40f, 132f)
            };
            float refreshInterval = settingsManager.CarReflectionUpdateRate switch
            {
                0 => 0.55f,
                1 => 0.3f,
                _ => 0.16f
            };
            int probeResolution = settingsManager.CarReflectionDetail switch
            {
                0 => 128,
                1 => 256,
                _ => 512
            };
            float probeIntensity = settingsManager.CarReflectionDetail switch
            {
                0 => 1.05f,
                1 => 1.2f,
                _ => 1.35f
            };

            for (int index = 0; index < probes.Length; index++)
            {
                PlayerReflectionProbeController controller = probes[index];
                if (controller == null)
                {
                    continue;
                }

                Transform target = controller.transform.parent != null ? controller.transform.parent : controller.transform;
                controller.Configure(target, new Vector3(0f, 5f, 0f), probeSize, 10f, refreshInterval, 6f);

                ReflectionProbe probe = controller.GetComponent<ReflectionProbe>();
                if (probe == null)
                {
                    probe = controller.gameObject.AddComponent<ReflectionProbe>();
                }

                if (probe != null)
                {
                    probe.size = probeSize;
                    probe.resolution = probeResolution;
                    probe.intensity = probeIntensity;
                    probe.blendDistance = 6f;
                }
            }

            VehicleReflectionRuntimeController[] vehicleReflectionControllers = FindObjectsByType<VehicleReflectionRuntimeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < vehicleReflectionControllers.Length; index++)
            {
                vehicleReflectionControllers[index]?.ApplyReflectionSupport();
            }
        }

        private void ApplyVehicleGeometrySettings()
        {
            LODGroup[] lodGroups = FindObjectsByType<LODGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int forcedLod = settingsManager.CarGeometryDetail switch
            {
                0 => 2,
                1 => 1,
                _ => -1
            };

            for (int index = 0; index < lodGroups.Length; index++)
            {
                LODGroup lodGroup = lodGroups[index];
                if (lodGroup == null)
                {
                    continue;
                }

                if (lodGroup.GetComponentInParent<VehicleDynamicsController>() == null &&
                    lodGroup.GetComponentInParent<TrafficCar>() == null)
                {
                    continue;
                }

                lodGroup.ForceLOD(forcedLod);
                lodGroup.RecalculateBounds();
            }
        }

        private void ApplyTrafficSettings()
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (candidate == null)
                {
                    continue;
                }

                if (!candidate.CompareTag("Traffic"))
                {
                    continue;
                }

                candidate.gameObject.SetActive(settingsManager.CrowdsEnabled);
            }
        }

        private void ApplyParticleSettings()
        {
            ParticleSystem[] particleSystems = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem particleSystem = particleSystems[index];
                if (particleSystem == null)
                {
                    continue;
                }

                var emission = particleSystem.emission;
                emission.enabled = settingsManager.ParticleSystemsEnabled;

                ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = settingsManager.ParticleSystemsEnabled;
                }

                if (settingsManager.ParticleSystemsEnabled)
                {
                    if (particleSystem.gameObject.activeInHierarchy && !particleSystem.isPlaying)
                    {
                        particleSystem.Play();
                    }
                }
                else
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void ApplyHdrpCameraAntialiasing(Camera camera)
        {
            Type hdCameraType = FindType("UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData, Unity.RenderPipelines.HighDefinition.Runtime");
            if (hdCameraType == null)
            {
                return;
            }

            Component hdCameraData = camera.GetComponent(hdCameraType);
            if (hdCameraData == null)
            {
                return;
            }

            SetEnumPropertyIfPresent(
                hdCameraData,
                "antialiasing",
                settingsManager.FullScreenAntiAliasing switch
                {
                    0 => "None",
                    1 => "SubpixelMorphologicalAntiAliasing",
                    _ => "TemporalAntialiasing"
                });
        }

        private void ApplyUrpCameraAntialiasing(Camera camera)
        {
            Type urpCameraType = FindType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpCameraType == null)
            {
                return;
            }

            Component urpCameraData = camera.GetComponent(urpCameraType);
            if (urpCameraData == null)
            {
                return;
            }

            SetEnumPropertyIfPresent(
                urpCameraData,
                "antialiasing",
                settingsManager.FullScreenAntiAliasing switch
                {
                    0 => "None",
                    1 => "FastApproximateAntialiasing",
                    _ => "SubpixelMorphologicalAntiAliasing"
                });
        }

        private static void SetEnumPropertyIfPresent(object target, string propertyName, string enumName)
        {
            if (target == null)
            {
                return;
            }

            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || !property.CanWrite || !property.PropertyType.IsEnum)
            {
                return;
            }

            try
            {
                object enumValue = Enum.Parse(property.PropertyType, enumName, true);
                property.SetValue(target, enumValue);
            }
            catch (ArgumentException)
            {
            }
        }

        private static VolumeComponent GetOrAddVolumeComponent(VolumeProfile profile, params string[] typeNames)
        {
            Type type = FindType(typeNames);
            if (type == null)
            {
                return null;
            }

            for (int index = 0; index < profile.components.Count; index++)
            {
                VolumeComponent component = profile.components[index];
                if (component != null && type.IsInstanceOfType(component))
                {
                    return component;
                }
            }

            MethodInfo addMethod = typeof(VolumeProfile).GetMethod("Add", new[] { typeof(Type), typeof(bool) });
            if (addMethod == null)
            {
                return null;
            }

            try
            {
                return addMethod.Invoke(profile, new object[] { type, true }) as VolumeComponent;
            }
            catch (TargetInvocationException)
            {
                return null;
            }
        }

        private static void SetComponentActive(VolumeComponent component, bool isActive)
        {
            if (component == null)
            {
                return;
            }

            FieldInfo activeField = typeof(VolumeComponent).GetField("active", BindingFlags.Instance | BindingFlags.Public);
            if (activeField != null)
            {
                activeField.SetValue(component, isActive);
                return;
            }

            PropertyInfo activeProperty = typeof(VolumeComponent).GetProperty("active", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (activeProperty != null && activeProperty.CanWrite)
            {
                activeProperty.SetValue(component, isActive);
            }
        }

        private static void SetVolumeFloat(VolumeComponent component, string fieldName, float value)
        {
            SetVolumeParameter(component, fieldName, value);
        }

        private static void SetVolumeBool(VolumeComponent component, string fieldName, bool value)
        {
            SetVolumeParameter(component, fieldName, value);
        }

        private static void SetVolumeColor(VolumeComponent component, string fieldName, Color value)
        {
            SetVolumeParameter(component, fieldName, value);
        }

        private static void SetVolumeEnum(VolumeComponent component, string fieldName, string enumName)
        {
            if (component == null)
            {
                return;
            }

            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return;
            }

            object parameter = field.GetValue(component);
            if (parameter == null)
            {
                return;
            }

            MethodInfo overrideMethod = FindOverrideMethod(parameter.GetType());
            if (overrideMethod == null)
            {
                return;
            }

            ParameterInfo[] parameters = overrideMethod.GetParameters();
            if (parameters.Length != 1 || !parameters[0].ParameterType.IsEnum)
            {
                return;
            }

            try
            {
                object enumValue = Enum.Parse(parameters[0].ParameterType, enumName, true);
                overrideMethod.Invoke(parameter, new[] { enumValue });
            }
            catch (ArgumentException)
            {
            }
        }

        private static void SetVolumeParameter(VolumeComponent component, string fieldName, object value)
        {
            if (component == null)
            {
                return;
            }

            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return;
            }

            object parameter = field.GetValue(component);
            if (parameter == null)
            {
                return;
            }

            MethodInfo overrideMethod = FindOverrideMethod(parameter.GetType());
            if (overrideMethod == null)
            {
                return;
            }

            ParameterInfo[] parameters = overrideMethod.GetParameters();
            if (parameters.Length != 1)
            {
                return;
            }

            overrideMethod.Invoke(parameter, new[] { ConvertValue(value, parameters[0].ParameterType) });
        }

        private static MethodInfo FindOverrideMethod(Type parameterType)
        {
            MethodInfo[] methods = parameterType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int index = 0; index < methods.Length; index++)
            {
                if (methods[index].Name == "Override")
                {
                    return methods[index];
                }
            }

            return null;
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            if (targetType == typeof(float))
            {
                return Convert.ToSingle(value);
            }

            if (targetType == typeof(int))
            {
                return Convert.ToInt32(value);
            }

            if (targetType == typeof(bool))
            {
                return Convert.ToBoolean(value);
            }

            return value;
        }

        private static Type FindType(params string[] typeNames)
        {
            for (int index = 0; index < typeNames.Length; index++)
            {
                Type type = Type.GetType(typeNames[index]);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}

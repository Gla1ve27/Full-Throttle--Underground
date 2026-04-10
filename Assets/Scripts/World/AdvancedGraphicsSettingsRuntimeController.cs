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
            ResolveSettingsManager();
            if (pendingApply || sceneWarmupRefreshesRemaining > 0)
            {
                ApplyAllSettings();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveSettingsManager();
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
            ApplyVehicleGeometrySettings();
            ApplyTrafficSettings();
            ApplyParticleSettings();
        }

        private void ApplyQualityAndDisplaySettings()
        {
            // Fully and explicitly disable legacy fog to prevent it from stacking 
            // with HDRP's Volume-based fog. This resolves the white horizon/black sky bug.
            RenderSettings.fog = false;

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
            // Fully removed script-injected volume profile logic.
            // This prevents "AdvancedGraphicsSettings_Runtime" from flooding the scene
            // and overriding the user's authored World/Garage Volume Profiles.
        }

        private void ApplyVolumeOverrides()
        {
            // Disabled.
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

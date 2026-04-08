using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
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
        private VolumeComponent vignette;
        private VolumeComponent chromaticAberration;
        private VolumeComponent motionBlur;

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

            SetVolumeFloat(vignette, "intensity", effectsEnabled ? Mathf.Lerp(vignetteBase, vignetteMax, speedT) : vignetteBase);

            float chromaticIntensity = effectsEnabled
                ? Mathf.Lerp(chromaticBase, chromaticMax, Mathf.SmoothStep(0f, 1f, speedT)) + highSpeedPulse * 0.04f
                : 0f;
            SetVolumeFloat(chromaticAberration, "intensity", Mathf.Clamp01(chromaticIntensity));

            bool motionBlurEnabled = effectsEnabled && (settingsManager == null || settingsManager.MotionBlurEnabled);
            float motionBlurIntensity = motionBlurEnabled ? Mathf.Lerp(0f, motionBlurMax, Mathf.SmoothStep(0f, 1f, speedT)) : 0f;
            if (!SetVolumeFloat(motionBlur, "intensity", motionBlurIntensity))
            {
                SetVolumeFloat(motionBlur, "maximumVelocity", motionBlurIntensity);
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
            runtimeVolume = GetComponent<Volume>();
            if (runtimeVolume == null)
            {
                runtimeVolume = gameObject.AddComponent<Volume>();
            }

            runtimeVolume.isGlobal = true;
            runtimeVolume.priority = 100f;

            runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeProfile.name = "VehicleSpeedEffectsProfile_Runtime";
            runtimeVolume.profile = runtimeProfile;

            vignette = GetOrAddVolumeComponent(
                runtimeProfile,
                "UnityEngine.Rendering.HighDefinition.Vignette, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.Vignette, Unity.RenderPipelines.Universal.Runtime");
            chromaticAberration = GetOrAddVolumeComponent(
                runtimeProfile,
                "UnityEngine.Rendering.HighDefinition.ChromaticAberration, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.ChromaticAberration, Unity.RenderPipelines.Universal.Runtime");
            motionBlur = GetOrAddVolumeComponent(
                runtimeProfile,
                "UnityEngine.Rendering.HighDefinition.MotionBlur, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.MotionBlur, Unity.RenderPipelines.Universal.Runtime");

            SetVolumeFloat(vignette, "intensity", vignetteBase);
            SetVolumeFloat(chromaticAberration, "intensity", chromaticBase);
            SetVolumeFloat(motionBlur, "intensity", 0f);
        }

        private static VolumeComponent GetOrAddVolumeComponent(VolumeProfile profile, params string[] typeNames)
        {
            Type type = FindType(typeNames);
            if (type == null)
            {
                return null;
            }

            for (int i = 0; i < profile.components.Count; i++)
            {
                VolumeComponent component = profile.components[i];
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
            catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException)
            {
                for (int i = 0; i < profile.components.Count; i++)
                {
                    VolumeComponent component = profile.components[i];
                    if (component != null && type.IsInstanceOfType(component))
                    {
                        return component;
                    }
                }

                return null;
            }
        }

        private static bool SetVolumeFloat(VolumeComponent component, string fieldName, float value)
        {
            if (component == null)
            {
                return false;
            }

            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return false;
            }

            object parameter = field.GetValue(component);
            if (parameter == null)
            {
                return false;
            }

            MethodInfo overrideMethod = FindOverrideMethod(parameter.GetType());
            if (overrideMethod == null)
            {
                return false;
            }

            ParameterInfo[] parameters = overrideMethod.GetParameters();
            if (parameters.Length != 1)
            {
                return false;
            }

            overrideMethod.Invoke(parameter, new[] { ConvertValue(value, parameters[0].ParameterType) });
            return true;
        }

        private static MethodInfo FindOverrideMethod(Type parameterType)
        {
            MethodInfo[] methods = parameterType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name == "Override")
                {
                    return methods[i];
                }
            }

            return null;
        }

        private static object ConvertValue(object value, Type targetType)
        {
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
            for (int i = 0; i < typeNames.Length; i++)
            {
                Type type = Type.GetType(typeNames[i]);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}

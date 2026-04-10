using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using Underground.UI;

namespace Underground.Vehicle
{
    /// <summary>
    /// Unified chase camera: handles follow, FOV warp, vignette, chromatic aberration,
    /// motion blur, and speed perception shake — all in one script.
    /// Every value is Inspector-driven. Nothing is hard-coded at runtime.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class VehicleCameraFollow : MonoBehaviour
    {
        [Header("References")]
        public Transform target;
        public VehicleDynamicsController targetVehicle;
        public Rigidbody targetBody;
        public GearboxSystem gearbox;

        [Header("Follow")]
        public Vector3 targetOffset = new Vector3(0f, 0.08f, 0f);
        public float followDistance = 4.6f;
        public float highSpeedFollowDistance = 7.2f;
        public float followHeight = 0.48f;
        public float highSpeedFollowHeight = 0.9f;
        public float followSmoothTime = 0.05f;
        public float rotationSharpness = 8.5f;
        public float lookAheadDistance = 2.8f;
        public float highSpeedLookAheadDistance = 3.55f;
        public float velocityHeadingBlend = 0.14f;
        public float headingResponsiveness = 3.5f;
        public float reverseHeadingDamping = 0.12f;
        public float focusHeight = 0.4f;
        public float maxLookAheadPivotOffset = 2.9f;
        public float lookAheadPivotResponsiveness = 2.8f;

        [Header("Camera")]
        public float minFieldOfView = 46f;
        public float maxFieldOfView = 72f;
        public float speedForMaxFieldOfView = 155f;
        public float fovWarpPower = 1.35f;
        [Tooltip("How quickly the camera reacts to speed changes. Lower = smoother. NFS Heat ~ 1.5.")]
        public float speedSmoothRate = 1.5f;

        [Header("Speed VFX (Post-Processing)")]
        [Tooltip("Assign a Volume on this camera with Vignette + Motion Blur overrides to scale by speed.")]
        public Volume speedEffectsVolume;
        public float vfxStartSpeedKph = 35f;
        public float vfxFullSpeedKph = 240f;
        [Space]
        [Tooltip("Vignette intensity at idle.")]
        public float vignetteBase = 0.25f;
        [Tooltip("Vignette intensity at top speed.")]
        public float vignetteMax = 0.50f;
        [Tooltip("Chromatic aberration at idle.")]
        public float chromaticBase = 0.015f;
        [Tooltip("Chromatic aberration at top speed.")]
        public float chromaticMax = 0.08f;
        [Tooltip("Motion blur intensity at top speed.")]
        public float motionBlurMax = 0.08f;

        [Header("Speed Perception (Camera Shake)")]
        public float buffetingStartSpeedKph = 185f;
        public float buffetingFullSpeedKph = 240f;
        public float buffetingAmplitude = 0.004f;
        public float buffetingFrequency = 11f;
        public float shakeStartSpeedKph = 160f;
        public float shakeFullSpeedKph = 230f;
        public float shakePositionAmplitude = 0.0025f;
        public float shakeRotationAmplitude = 0.12f;

        // Private state
        private Camera attachedCamera;
        private Vector3 followVelocity;
        private GameSettingsManager settingsManager;
        private Vector3 smoothedPlanarForward = Vector3.forward;
        private float lookAheadPivotOffset;
        private float shakeTime;
        private float smoothedSpeedKph;

        // Runtime post-processing references (created once)
        private VolumeProfile runtimeProfile;
        private VolumeComponent vignetteComponent;
        private VolumeComponent chromaticComponent;
        private VolumeComponent motionBlurComponent;
        private bool postProcessingInitialized;

        private void Awake()
        {
            attachedCamera = GetComponent<Camera>();
            settingsManager = FindFirstObjectByType<GameSettingsManager>();
            RefreshReferences();
            InitializePostProcessing();
        }

        private void LateUpdate()
        {
            RefreshReferences();

            if (target == null)
            {
                return;
            }

            float dt = Mathf.Max(0.0001f, Time.deltaTime);

            if (settingsManager == null)
            {
                settingsManager = FindFirstObjectByType<GameSettingsManager>();
            }

            // --- Speed ---
            Vector3 localVelocity = targetBody != null ? target.InverseTransformDirection(targetBody.linearVelocity) : Vector3.zero;
            float rawSpeedKph = targetVehicle != null
                ? targetVehicle.SpeedKph
                : (targetBody != null ? targetBody.linearVelocity.magnitude * 3.6f : 0f);
            smoothedSpeedKph = Mathf.Lerp(smoothedSpeedKph, rawSpeedKph, 1f - Mathf.Exp(-speedSmoothRate * dt));
            float speedKph = smoothedSpeedKph;
            float speedT = Mathf.Clamp01(speedKph / Mathf.Max(1f, speedForMaxFieldOfView));

            bool cameraEffectsEnabled = settingsManager == null || settingsManager.CameraEffectsEnabled;

            // --- Position ---
            Vector3 pivot = target.position + targetOffset;
            float desiredDistance = Mathf.Lerp(followDistance, highSpeedFollowDistance, speedT);
            float desiredHeight = Mathf.Lerp(followHeight, highSpeedFollowHeight, speedT);
            Vector3 planarForward = GetPlanarForward(localVelocity, cameraEffectsEnabled);
            float targetLookAheadPivotOffset = Mathf.Lerp(0f, maxLookAheadPivotOffset, speedT);
            lookAheadPivotOffset = Mathf.Lerp(lookAheadPivotOffset, targetLookAheadPivotOffset, 1f - Mathf.Exp(-lookAheadPivotResponsiveness * dt));

            shakeTime += dt * GetShakeFrequency();

            Vector3 desiredPosition = pivot
                - planarForward * desiredDistance
                + Vector3.up * desiredHeight;

            if (cameraEffectsEnabled)
            {
                desiredPosition += GetBuffetingOffset(planarForward, speedKph);
                desiredPosition += GetShakePositionOffset(planarForward, speedKph);
            }

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref followVelocity, followSmoothTime, Mathf.Infinity, dt);

            // --- Rotation ---
            float activeLookAheadDistance = cameraEffectsEnabled
                ? Mathf.Lerp(lookAheadDistance, highSpeedLookAheadDistance, speedT)
                : lookAheadDistance;
            Vector3 dynamicPivot = pivot + planarForward * lookAheadPivotOffset;
            Vector3 lookPoint = dynamicPivot + planarForward * activeLookAheadDistance + Vector3.up * focusHeight;
            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);

            if (cameraEffectsEnabled)
            {
                desiredRotation *= GetShakeRotationOffset(speedKph);
            }

            float blend = 1f - Mathf.Exp(-rotationSharpness * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);

            // --- FOV ---
            UpdateFieldOfView(speedT, dt);

            // --- Post-Processing VFX ---
            UpdateSpeedEffects(speedKph, cameraEffectsEnabled);
        }

        // ──────────────────────────── References ────────────────────────────

        private void RefreshReferences()
        {
            if (targetVehicle != null && targetBody == null)
            {
                targetBody = targetVehicle.Rigidbody;
            }

            if (target == null && targetVehicle != null)
            {
                Transform cameraTarget = targetVehicle.transform.Find("CameraTarget");
                target = cameraTarget != null ? cameraTarget : targetVehicle.transform;
            }

            if (targetBody == null && target != null)
            {
                targetBody = target.GetComponentInParent<Rigidbody>();
            }

            if (gearbox == null && targetVehicle != null)
            {
                gearbox = targetVehicle.GetComponent<GearboxSystem>();
            }
        }

        // ──────────────────────────── FOV ────────────────────────────

        private void UpdateFieldOfView(float speedT, float dt)
        {
            if (attachedCamera == null)
            {
                return;
            }

            float baseFieldOfView = settingsManager != null ? settingsManager.CameraFieldOfView : minFieldOfView;
            float topFieldOfView = settingsManager != null && !settingsManager.CameraEffectsEnabled
                ? baseFieldOfView
                : Mathf.Max(baseFieldOfView, maxFieldOfView);
            float fovBlend = settingsManager != null && !settingsManager.CameraEffectsEnabled ? 0f : Mathf.Pow(speedT, fovWarpPower);
            float targetFov = Mathf.Lerp(baseFieldOfView, topFieldOfView, fovBlend);
            attachedCamera.fieldOfView = Mathf.Lerp(attachedCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-10f * dt));
        }

        // ──────────────────────────── Post-Processing ────────────────────────────

        private void InitializePostProcessing()
        {
            if (postProcessingInitialized)
            {
                return;
            }

            // Create a dedicated runtime volume if none is assigned
            Volume vol = speedEffectsVolume;
            if (vol == null)
            {
                vol = GetComponent<Volume>();
                if (vol == null)
                {
                    vol = gameObject.AddComponent<Volume>();
                }
                speedEffectsVolume = vol;
            }

            // Use local volume first to avoid overriding global scene lighting
            vol.isGlobal = false; 
            vol.priority = 1f;
            vol.weight = 1f;

            // Only create a new profile if the current one is null, 
            // otherwise we'd be deleting the user's authored camera settings!
            if (vol.profile == null)
            {
                runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                runtimeProfile.name = "CameraSpeedVFX_Runtime";
                vol.profile = runtimeProfile;
            }
            else
            {
                runtimeProfile = vol.profile;
            }

            vignetteComponent = GetOrAddVolumeComponent(runtimeProfile,
                "UnityEngine.Rendering.HighDefinition.Vignette, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.Vignette, Unity.RenderPipelines.Universal.Runtime");
            chromaticComponent = GetOrAddVolumeComponent(runtimeProfile,
                "UnityEngine.Rendering.HighDefinition.ChromaticAberration, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.ChromaticAberration, Unity.RenderPipelines.Universal.Runtime");
            motionBlurComponent = GetOrAddVolumeComponent(runtimeProfile,
                "UnityEngine.Rendering.HighDefinition.MotionBlur, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.MotionBlur, Unity.RenderPipelines.Universal.Runtime");

            SetVolumeFloat(vignetteComponent, "intensity", vignetteBase);
            SetVolumeFloat(chromaticComponent, "intensity", chromaticBase);
            SetVolumeFloat(motionBlurComponent, "intensity", 0f);

            postProcessingInitialized = true;
        }

        private void UpdateSpeedEffects(float speedKph, bool effectsEnabled)
        {
            if (!postProcessingInitialized)
            {
                return;
            }

            float maxSpeed = targetVehicle != null && targetVehicle.RuntimeStats != null
                ? Mathf.Max(1f, targetVehicle.RuntimeStats.MaxSpeedKph)
                : vfxFullSpeedKph;
            float speedT = Mathf.Clamp01(speedKph / maxSpeed);

            // Vignette: always present at base, ramps up with speed
            float vignetteIntensity = effectsEnabled
                ? Mathf.Lerp(vignetteBase, vignetteMax, speedT)
                : vignetteBase;
            SetVolumeFloat(vignetteComponent, "intensity", vignetteIntensity);

            // Chromatic aberration
            float chromaticIntensity = effectsEnabled
                ? Mathf.Lerp(chromaticBase, chromaticMax, Mathf.SmoothStep(0f, 1f, speedT))
                : 0f;
            SetVolumeFloat(chromaticComponent, "intensity", Mathf.Clamp01(chromaticIntensity));

            // Motion blur
            bool motionBlurEnabled = effectsEnabled && (settingsManager == null || settingsManager.MotionBlurEnabled);
            float motionBlurIntensity = motionBlurEnabled
                ? Mathf.Lerp(0f, motionBlurMax, Mathf.SmoothStep(0f, 1f, speedT))
                : 0f;
            if (!SetVolumeFloat(motionBlurComponent, "intensity", motionBlurIntensity))
            {
                SetVolumeFloat(motionBlurComponent, "maximumVelocity", motionBlurIntensity);
            }
        }

        // ──────────────────────────── Heading ────────────────────────────

        private Vector3 GetPlanarForward(Vector3 localVelocity, bool cameraEffectsEnabled)
        {
            Vector3 referenceForward = targetVehicle != null ? targetVehicle.transform.forward : target.forward;
            Vector3 targetForward = Vector3.ProjectOnPlane(referenceForward, Vector3.up).normalized;
            if (targetForward.sqrMagnitude < 0.001f)
            {
                targetForward = Vector3.forward;
            }

            if (targetBody == null || !cameraEffectsEnabled)
            {
                smoothedPlanarForward = Vector3.Slerp(
                    smoothedPlanarForward.sqrMagnitude > 0.001f ? smoothedPlanarForward : targetForward,
                    targetForward,
                    1f - Mathf.Exp(-headingResponsiveness * Mathf.Max(0.0001f, Time.deltaTime)));
                return smoothedPlanarForward.normalized;
            }

            Vector3 planarVelocity = Vector3.ProjectOnPlane(targetBody.linearVelocity, Vector3.up);
            Vector3 velocityForward = planarVelocity.sqrMagnitude > 0.5f
                ? planarVelocity.normalized
                : targetForward;

            bool reversing = localVelocity.z < -0.5f;
            float headingBlend = reversing ? reverseHeadingDamping : velocityHeadingBlend;
            Vector3 desiredHeading = Vector3.Slerp(targetForward, velocityForward, headingBlend).normalized;

            smoothedPlanarForward = Vector3.Slerp(
                smoothedPlanarForward.sqrMagnitude > 0.001f ? smoothedPlanarForward : desiredHeading,
                desiredHeading,
                1f - Mathf.Exp(-headingResponsiveness * Mathf.Max(0.0001f, Time.deltaTime)));

            if (smoothedPlanarForward.sqrMagnitude < 0.001f)
            {
                smoothedPlanarForward = targetForward;
            }

            return smoothedPlanarForward.normalized;
        }

        // ──────────────────────────── Speed Perception ────────────────────────────

        private Vector3 GetBuffetingOffset(Vector3 planarForward, float speedKph)
        {
            if (speedKph <= buffetingStartSpeedKph)
            {
                return Vector3.zero;
            }

            float effectT = Mathf.InverseLerp(buffetingStartSpeedKph, Mathf.Max(buffetingStartSpeedKph + 1f, buffetingFullSpeedKph), speedKph);
            float time = Time.unscaledTime * buffetingFrequency;
            Vector3 lateral = Vector3.Cross(Vector3.up, planarForward).normalized;
            float lateralNoise = (Mathf.PerlinNoise(time, 0.17f) - 0.5f) * 2f;
            float verticalNoise = (Mathf.PerlinNoise(0.31f, time * 0.83f) - 0.5f) * 2f;
            return (lateral * lateralNoise + Vector3.up * verticalNoise * 0.65f) * (buffetingAmplitude * effectT);
        }

        private Vector3 GetShakePositionOffset(Vector3 planarForward, float speedKph)
        {
            float amplitude = GetShakeAmplitude(speedKph) * shakePositionAmplitude;
            if (amplitude <= 0f)
            {
                return Vector3.zero;
            }

            Vector3 lateral = Vector3.Cross(Vector3.up, planarForward).normalized;
            float x = (Mathf.PerlinNoise(shakeTime, 0.19f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0.47f, shakeTime) - 0.5f) * 2f;
            float z = (Mathf.PerlinNoise(shakeTime * 0.67f, 0.81f) - 0.5f) * 2f;
            return (lateral * x + Vector3.up * y + planarForward * z * 0.4f) * amplitude;
        }

        private Quaternion GetShakeRotationOffset(float speedKph)
        {
            float amplitude = GetShakeAmplitude(speedKph) * shakeRotationAmplitude;
            if (amplitude <= 0f)
            {
                return Quaternion.identity;
            }

            float pitch = (Mathf.PerlinNoise(shakeTime * 0.91f, 0.13f) - 0.5f) * 2f * amplitude;
            float yaw = (Mathf.PerlinNoise(0.37f, shakeTime * 1.07f) - 0.5f) * 2f * amplitude;
            float roll = (Mathf.PerlinNoise(shakeTime * 0.73f, 0.59f) - 0.5f) * 2f * amplitude * 1.25f;
            return Quaternion.Euler(pitch, yaw, roll);
        }

        private float GetShakeFrequency()
        {
            float rpmT = gearbox != null && targetVehicle != null && targetVehicle.RuntimeStats != null
                ? Mathf.InverseLerp(targetVehicle.RuntimeStats.IdleRPM, targetVehicle.RuntimeStats.MaxRPM, gearbox.CurrentRPM)
                : 0f;
            return Mathf.Lerp(6f, 14f, rpmT);
        }

        private float GetShakeAmplitude(float speedKph)
        {
            if (speedKph <= shakeStartSpeedKph)
            {
                return 0f;
            }

            return Mathf.InverseLerp(shakeStartSpeedKph, Mathf.Max(shakeStartSpeedKph + 1f, shakeFullSpeedKph), speedKph);
        }

        // ──────────────────────────── Volume Helpers (Reflection) ────────────────────────────

        private static VolumeComponent GetOrAddVolumeComponent(VolumeProfile profile, params string[] typeNames)
        {
            Type type = FindType(typeNames);
            if (type == null)
            {
                return null;
            }

            for (int i = 0; i < profile.components.Count; i++)
            {
                if (profile.components[i] != null && type.IsInstanceOfType(profile.components[i]))
                {
                    return profile.components[i];
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
            catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
            {
                for (int i = 0; i < profile.components.Count; i++)
                {
                    if (profile.components[i] != null && type.IsInstanceOfType(profile.components[i]))
                    {
                        return profile.components[i];
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
            if (targetType == typeof(float)) return Convert.ToSingle(value);
            if (targetType == typeof(int)) return Convert.ToInt32(value);
            if (targetType == typeof(bool)) return Convert.ToBoolean(value);
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

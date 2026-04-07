using UnityEngine;
using Underground.UI;

namespace Underground.Vehicle
{
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
        public float followDistance = 4.9f;
        public float highSpeedFollowDistance = 5.95f;
        public float followHeight = 0.48f;
        public float highSpeedFollowHeight = 0.9f;
        public float followSmoothTime = 0.065f;
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
        public float maxFieldOfView = 58f;
        public float speedForMaxFieldOfView = 155f;
        public float fovWarpPower = 1.35f;

        [Header("Shift Lunge")]
        public float shiftLungeDuration = 0.3f;
        public float shiftLungeDistance = 0.35f;
        public float shiftLungeDampingMultiplier = 1.25f;
        public float shiftLungeRotationLag = 0.9f;

        [Header("Speed Perception")]
        public float buffetingStartSpeedKph = 185f;
        public float buffetingFullSpeedKph = 240f;
        public float buffetingAmplitude = 0.004f;
        public float buffetingFrequency = 11f;
        public float shakeStartSpeedKph = 160f;
        public float shakeFullSpeedKph = 230f;
        public float shakePositionAmplitude = 0.0025f;
        public float shakeRotationAmplitude = 0.12f;

        private Camera attachedCamera;
        private Vector3 followVelocity;
        private GameSettingsManager settingsManager;
        private Vector3 smoothedPlanarForward = Vector3.forward;
        private float lookAheadPivotOffset;
        private float shakeTime;

        private void Awake()
        {
            ApplySimulatorPreset();
            attachedCamera = GetComponent<Camera>();
            settingsManager = FindFirstObjectByType<GameSettingsManager>();
            RefreshReferences();
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

            Vector3 localVelocity = targetBody != null ? target.InverseTransformDirection(targetBody.linearVelocity) : Vector3.zero;
            float speedKph = targetVehicle != null
                ? targetVehicle.SpeedKph
                : (targetBody != null ? targetBody.linearVelocity.magnitude * 3.6f : 0f);
            float speedT = Mathf.Clamp01(speedKph / Mathf.Max(1f, speedForMaxFieldOfView));
            Vector3 pivot = target.position + targetOffset;
            bool cameraEffectsEnabled = settingsManager == null || settingsManager.CameraEffectsEnabled;
            float desiredDistance = Mathf.Lerp(followDistance, highSpeedFollowDistance, speedT);
            float desiredHeight = Mathf.Lerp(followHeight, highSpeedFollowHeight, speedT);
            Vector3 planarForward = GetPlanarForward(localVelocity, cameraEffectsEnabled);
            float targetLookAheadPivotOffset = Mathf.Lerp(0f, maxLookAheadPivotOffset, speedT);
            lookAheadPivotOffset = Mathf.Lerp(lookAheadPivotOffset, targetLookAheadPivotOffset, 1f - Mathf.Exp(-lookAheadPivotResponsiveness * dt));
            float shiftLungeT = GetShiftLungeBlend();
            float effectiveFollowSmoothTime = followSmoothTime * Mathf.Lerp(1f, shiftLungeDampingMultiplier, shiftLungeT);
            float effectiveRotationSharpness = rotationSharpness * Mathf.Lerp(1f, shiftLungeRotationLag, shiftLungeT);
            shakeTime += dt * GetShakeFrequency();
            Vector3 desiredPosition = pivot
                - planarForward * (desiredDistance + shiftLungeDistance * shiftLungeT)
                + Vector3.up * desiredHeight;

            if (cameraEffectsEnabled)
            {
                desiredPosition += GetBuffetingOffset(planarForward, speedKph);
                desiredPosition += GetShakePositionOffset(planarForward, speedKph);
            }

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref followVelocity, effectiveFollowSmoothTime, Mathf.Infinity, dt);

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

            float blend = 1f - Mathf.Exp(-effectiveRotationSharpness * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);

            UpdateFieldOfView(speedT, dt);
        }

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

        private float GetShiftLungeBlend()
        {
            if (gearbox == null || shiftLungeDuration <= 0f)
            {
                return 0f;
            }

            float elapsed = Time.time - gearbox.LastShiftTime;
            if (elapsed < 0f || elapsed > shiftLungeDuration)
            {
                return 0f;
            }

            float normalized = 1f - (elapsed / shiftLungeDuration);
            return normalized * normalized;
        }

        private void ApplySimulatorPreset()
        {
            targetOffset = new Vector3(0f, 0.08f, 0f);
            followDistance = 4.9f;
            highSpeedFollowDistance = 5.95f;
            followHeight = 0.48f;
            highSpeedFollowHeight = 0.9f;
            followSmoothTime = 0.065f;
            rotationSharpness = 8.5f;
            lookAheadDistance = 2.8f;
            highSpeedLookAheadDistance = 3.55f;
            velocityHeadingBlend = 0.14f;
            headingResponsiveness = 3.5f;
            reverseHeadingDamping = 0.12f;
            focusHeight = 0.4f;
            maxLookAheadPivotOffset = 2.9f;
            lookAheadPivotResponsiveness = 2.8f;
            minFieldOfView = 46f;
            maxFieldOfView = 58f;
            speedForMaxFieldOfView = 155f;
            fovWarpPower = 1.35f;
            shiftLungeDuration = 0.3f;
            shiftLungeDistance = 0.35f;
            shiftLungeDampingMultiplier = 1.25f;
            shiftLungeRotationLag = 0.9f;
            buffetingStartSpeedKph = 185f;
            buffetingFullSpeedKph = 240f;
            buffetingAmplitude = 0.004f;
            buffetingFrequency = 11f;
            shakeStartSpeedKph = 160f;
            shakeFullSpeedKph = 230f;
            shakePositionAmplitude = 0.0025f;
            shakeRotationAmplitude = 0.12f;
        }
    }
}

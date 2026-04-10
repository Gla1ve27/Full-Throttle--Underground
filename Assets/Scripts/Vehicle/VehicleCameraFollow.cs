using UnityEngine;

namespace Underground.Vehicle
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class VehicleCameraFollow : MonoBehaviour
    {
        private const int RecommendedPresetVersion = 4;

        [Header("References")]
        public Transform target;
        public Rigidbody targetBody;

        [Header("Inspector Automation")]
        [Tooltip("Auto-applies preset once after script update.")]
        public bool autoApplyRecommendedSettings = true;

        [SerializeField, HideInInspector] private int appliedPresetVersion;

        [Header("Pivot")]
        public Vector3 targetOffset = new Vector3(0f, 0.20f, 0f);

        [Header("Chase Camera")]
        public float followDistance = 4.06f;
        public float highSpeedFollowDistance = 5.0f;
        public float followHeight = 0.25f;
        public float highSpeedFollowHeight = 1.2f;
        public float lookAheadDistance = 3.5f;
        public float highSpeedLookAheadDistance = 6.4f;
        public float focusHeight = 0.36f;
        public float rotationSharpness = 13.5f;

        [Header("Heading")]
        public float velocityHeadingBlend = 0.08f;
        public float headingResponsiveness = 6.0f;
        public float reverseHeadingDamping = 0.08f;

        [Header("Speed Feel")]
        public float minFieldOfView = 80f;
        public float maxFieldOfView = 100f;
        public float speedForMaxFieldOfView = 180f;
        public float fovWarpPower = 1.35f;
        public float speedFovResponse = 10.0f;
        public float speedPitchAtMax = -4.8f;
        public float highSpeedLateralLookOffset = 0.18f;

        [Header("Orbit (RMB)")]
        public float orbitMouseSensitivityX = 50f;
        public float orbitMouseSensitivityY = 0f;
        public float orbitPitchMin = 1f;
        public float orbitPitchMax = 1f;
        public float orbitYawSharpness = 1f;
        public float orbitPitchSharpness = 1f;
        public float orbitReturnSpeed = 4.75f;

        [Header("Orbit Shape")]
        public float orbitDistance = 4.06f;
        public float orbitVerticalLift = 0.25f;
        public float orbitLookHeight = 0.30f;

        [Header("Camera Energy")]
        public bool enableCameraShake = true;
        public float shakeStartSpeedKph = 70f;
        public float shakeFullSpeedKph = 165f;
        public float shakeRotationAmplitude = 0.10f;
        public float buffetingStartSpeedKph = 95f;
        public float buffetingFullSpeedKph = 180f;
        public float buffetingPositionAmplitude = 0.0035f;
        public float buffetingFrequency = 10f;

        private Camera attachedCamera;
        private Vector3 smoothedPlanarForward = Vector3.forward;
        private float smoothedSpeedKph;
        private float shakeTime;

        private float orbitYaw;
        private float orbitPitch;
        private float currentOrbitYaw;
        private float currentOrbitPitch;

        private void Reset()
        {
            ApplyRecommendedSettings(preserveReferences: true);
            AutoAssignReferences();
        }

        private void Awake()
        {
            attachedCamera = GetComponent<Camera>();
            AutoAssignReferences();
            ApplyRecommendedSettingsIfNeeded();
        }

        private void OnValidate()
        {
            ApplyRecommendedSettingsIfNeeded();
        }

        [ContextMenu("Apply Camera Preset")]
        public void ApplyRecommendedSettingsContextMenu()
        {
            ApplyRecommendedSettings(preserveReferences: true);
            appliedPresetVersion = RecommendedPresetVersion;
        }

        private void ApplyRecommendedSettingsIfNeeded()
        {
            if (!autoApplyRecommendedSettings)
            {
                return;
            }

            if (appliedPresetVersion >= RecommendedPresetVersion)
            {
                return;
            }

            ApplyRecommendedSettings(preserveReferences: true);
            appliedPresetVersion = RecommendedPresetVersion;
        }

        private void ApplyRecommendedSettings(bool preserveReferences)
        {
            Transform existingTarget = target;
            Rigidbody existingBody = targetBody;

            targetOffset = new Vector3(0f, 0.20f, 0f);

            followDistance = 4.06f;
            highSpeedFollowDistance = 5.0f;
            followHeight = 0.25f;
            highSpeedFollowHeight = 1.2f;
            lookAheadDistance = 3.5f;
            highSpeedLookAheadDistance = 6.4f;
            focusHeight = 0.36f;
            rotationSharpness = 13.5f;

            velocityHeadingBlend = 0.08f;
            headingResponsiveness = 6.0f;
            reverseHeadingDamping = 0.08f;

            minFieldOfView = 80f;
            maxFieldOfView = 100f;
            speedForMaxFieldOfView = 180f;
            fovWarpPower = 1.35f;
            speedFovResponse = 10.0f;
            speedPitchAtMax = -4.8f;
            highSpeedLateralLookOffset = 0.18f;

            orbitMouseSensitivityX = 50f;
            orbitMouseSensitivityY = 0f;
            orbitPitchMin = 1f;
            orbitPitchMax = 1f;
            orbitYawSharpness = 1f;
            orbitPitchSharpness = 1f;
            orbitReturnSpeed = 4.75f;

            orbitDistance = 4.06f;
            orbitVerticalLift = 0.25f;
            orbitLookHeight = 0.30f;

            enableCameraShake = true;
            shakeStartSpeedKph = 70f;
            shakeFullSpeedKph = 165f;
            shakeRotationAmplitude = 0.10f;
            buffetingStartSpeedKph = 95f;
            buffetingFullSpeedKph = 180f;
            buffetingPositionAmplitude = 0.0035f;
            buffetingFrequency = 10f;

            if (preserveReferences)
            {
                target = existingTarget;
                targetBody = existingBody;
            }
        }

        private void AutoAssignReferences()
        {
            if (target == null)
            {
                GameObject playerCar = GameObject.FindWithTag("Player");
                if (playerCar != null)
                {
                    Transform cameraTarget = playerCar.transform.Find("CameraTarget");
                    target = cameraTarget != null ? cameraTarget : playerCar.transform;
                }
            }

            if (targetBody == null && target != null)
            {
                targetBody = target.GetComponentInParent<Rigidbody>();
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                AutoAssignReferences();
                if (target == null)
                {
                    return;
                }
            }

            float dt = Mathf.Max(0.0001f, Time.deltaTime);

            Vector3 velocity = targetBody != null ? targetBody.linearVelocity : Vector3.zero;
            float rawSpeedKph = velocity.magnitude * 3.6f;
            smoothedSpeedKph = Mathf.Lerp(smoothedSpeedKph, rawSpeedKph, 1f - Mathf.Exp(-7.0f * dt));

            float speedT = Mathf.Clamp01(smoothedSpeedKph / Mathf.Max(1f, speedForMaxFieldOfView));
            shakeTime += dt * Mathf.Lerp(6f, 14f, speedT);

            Vector3 pivot = GetStablePivot();
            Vector3 planarForward = GetPlanarForward(dt);

            bool isOrbiting = Input.GetMouseButton(1);

            if (isOrbiting)
            {
                UpdateOrbitInput(dt);
            }
            else
            {
                ReturnOrbitToRear(dt);
            }

            UpdateCameraPose(pivot, planarForward, speedT, dt, isOrbiting);
            UpdateFieldOfView(speedT, dt);
        }

        private Vector3 GetStablePivot()
        {
            // When interpolation is enabled, targetBody.worldCenterOfMass might return 
            // non-interpolated physics state, causing jitter. Using target.position
            // ensures we follow the smoothed visual representation of the car.
            return target.position + targetOffset;
        }

       private Vector3 GetPlanarForward(float dt)
{
    Transform referenceTransform = targetBody != null ? targetBody.transform : target;

    Vector3 targetForward = Vector3.ProjectOnPlane(referenceTransform.forward, Vector3.up).normalized;
    if (targetForward.sqrMagnitude < 0.001f)
    {
        targetForward = Vector3.forward;
    }

    if (targetBody == null)
    {
        smoothedPlanarForward = Vector3.Slerp(
            smoothedPlanarForward.sqrMagnitude > 0.001f ? smoothedPlanarForward : targetForward,
            targetForward,
            1f - Mathf.Exp(-headingResponsiveness * dt)
        );

        return smoothedPlanarForward.normalized;
    }

Vector3 localVelocity = referenceTransform.InverseTransformDirection(targetBody.linearVelocity);
    Vector3 planarVelocity = Vector3.ProjectOnPlane(targetBody.linearVelocity, Vector3.up);

    Vector3 velocityForward = planarVelocity.sqrMagnitude > 1.0f
        ? planarVelocity.normalized
        : targetForward;

    float speedKph = planarVelocity.magnitude * 3.6f;

    // only trust velocity heading more at higher speed
    float speedBlend = Mathf.InverseLerp(55f, 180f, speedKph);

    // suppress velocity influence when side movement gets strong during turning
    float lateralAmount = Mathf.Abs(localVelocity.x);
    float steerSuppression = 1f - Mathf.InverseLerp(0.08f, 1.2f, lateralAmount);

    bool reversing = localVelocity.z < -0.5f;

    float dynamicBlend = reversing
        ? reverseHeadingDamping
        : velocityHeadingBlend * speedBlend * steerSuppression;

    Vector3 desiredHeading = Vector3.Slerp(targetForward, velocityForward, dynamicBlend).normalized;

    smoothedPlanarForward = Vector3.Slerp(
        smoothedPlanarForward.sqrMagnitude > 0.001f ? smoothedPlanarForward : desiredHeading,
        desiredHeading,
        1f - Mathf.Exp(-headingResponsiveness * dt)
    );

    if (smoothedPlanarForward.sqrMagnitude < 0.001f)
    {
        smoothedPlanarForward = targetForward;
    }

    return smoothedPlanarForward.normalized;
}
        private void UpdateOrbitInput(float dt)
        {
            orbitYaw += Input.GetAxisRaw("Mouse X") * orbitMouseSensitivityX * dt;
            orbitPitch -= Input.GetAxisRaw("Mouse Y") * orbitMouseSensitivityY * dt;
            orbitPitch = Mathf.Clamp(orbitPitch, orbitPitchMin, orbitPitchMax);

            currentOrbitYaw = Mathf.LerpAngle(
                currentOrbitYaw,
                orbitYaw,
                1f - Mathf.Exp(-orbitYawSharpness * dt)
            );

            currentOrbitPitch = Mathf.Lerp(
                currentOrbitPitch,
                orbitPitch,
                1f - Mathf.Exp(-orbitPitchSharpness * dt)
            );
        }

        private void ReturnOrbitToRear(float dt)
        {
            orbitYaw = Mathf.LerpAngle(orbitYaw, 0f, 1f - Mathf.Exp(-orbitReturnSpeed * dt));
            orbitPitch = Mathf.Lerp(orbitPitch, 0f, 1f - Mathf.Exp(-orbitReturnSpeed * dt));

            currentOrbitYaw = Mathf.LerpAngle(
                currentOrbitYaw,
                orbitYaw,
                1f - Mathf.Exp(-orbitYawSharpness * dt)
            );

            currentOrbitPitch = Mathf.Lerp(
                currentOrbitPitch,
                orbitPitch,
                1f - Mathf.Exp(-orbitPitchSharpness * dt)
            );
        }

        private void UpdateCameraPose(Vector3 pivot, Vector3 planarForward, float speedT, float dt, bool isOrbiting)
        {
            Vector3 desiredPosition;
            Quaternion desiredRotation;

            bool isReturning = Mathf.Abs(currentOrbitYaw) > 0.1f || Mathf.Abs(currentOrbitPitch) > 0.1f;

            if (isOrbiting || isReturning)
            {
                // Align orbit basis with car forward so offset puts camera behind
                Quaternion orbitBasis = Quaternion.LookRotation(planarForward, Vector3.up);
                Quaternion orbitRotation = orbitBasis * Quaternion.Euler(currentOrbitPitch, currentOrbitYaw, 0f);

                Vector3 orbitOffset = orbitRotation * new Vector3(0f, orbitVerticalLift, -orbitDistance);
                desiredPosition = pivot + orbitOffset;

                Vector3 orbitLookTarget = pivot + Vector3.up * orbitLookHeight;
                desiredRotation = Quaternion.LookRotation(orbitLookTarget - desiredPosition, Vector3.up);
            }
            else
            {
                float distance = Mathf.Lerp(followDistance, highSpeedFollowDistance, speedT);
                float height = Mathf.Lerp(followHeight, highSpeedFollowHeight, speedT);
                float lookAhead = Mathf.Lerp(lookAheadDistance, highSpeedLookAheadDistance, speedT);
                float speedPitch = Mathf.Lerp(0f, speedPitchAtMax, speedT);

                Vector3 lateral = Vector3.Cross(Vector3.up, planarForward).normalized;
                float lateralLook = highSpeedLateralLookOffset * speedT;

                desiredPosition = pivot - planarForward * distance + Vector3.up * height;

                Vector3 lookTarget =
                    pivot
                    + planarForward * lookAhead
                    + lateral * lateralLook
                    + Vector3.up * focusHeight;

                desiredRotation = Quaternion.LookRotation(lookTarget - desiredPosition, Vector3.up);
                desiredRotation *= Quaternion.Euler(speedPitch, 0f, 0f);

                if (enableCameraShake)
                {
                    desiredPosition += GetBuffetingOffset(planarForward, smoothedSpeedKph);
                    desiredRotation *= GetShakeRotationOffset(smoothedSpeedKph);
                }
            }

            transform.position = desiredPosition;

            float rotationBlend = 1f - Mathf.Exp(-rotationSharpness * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
        }

        private void UpdateFieldOfView(float speedT, float dt)
        {
            if (attachedCamera == null)
            {
                attachedCamera = GetComponent<Camera>();
                if (attachedCamera == null)
                {
                    return;
                }
            }

            float fovBlend = Mathf.Pow(speedT, fovWarpPower);
            float targetFov = Mathf.Lerp(minFieldOfView, maxFieldOfView, fovBlend);

            attachedCamera.fieldOfView = Mathf.Lerp(
                attachedCamera.fieldOfView,
                targetFov,
                1f - Mathf.Exp(-speedFovResponse * dt)
            );
        }

        private Vector3 GetBuffetingOffset(Vector3 planarForward, float speedKph)
        {
            if (speedKph <= buffetingStartSpeedKph)
            {
                return Vector3.zero;
            }

            float effectT = Mathf.InverseLerp(
                buffetingStartSpeedKph,
                Mathf.Max(buffetingStartSpeedKph + 1f, buffetingFullSpeedKph),
                speedKph
            );

            float time = Time.unscaledTime * buffetingFrequency;
            Vector3 lateral = Vector3.Cross(Vector3.up, planarForward).normalized;

            float lateralNoise = (Mathf.PerlinNoise(time, 0.17f) - 0.5f) * 2f;
            float verticalNoise = (Mathf.PerlinNoise(0.31f, time * 0.83f) - 0.5f) * 2f;

            return (lateral * lateralNoise + Vector3.up * verticalNoise * 0.65f)
                   * (buffetingPositionAmplitude * effectT);
        }

        private Quaternion GetShakeRotationOffset(float speedKph)
        {
            if (speedKph <= shakeStartSpeedKph)
            {
                return Quaternion.identity;
            }

            float effectT = Mathf.InverseLerp(
                shakeStartSpeedKph,
                Mathf.Max(shakeStartSpeedKph + 1f, shakeFullSpeedKph),
                speedKph
            );

            float amplitude = shakeRotationAmplitude * effectT;

            float pitch = (Mathf.PerlinNoise(shakeTime * 0.91f, 0.13f) - 0.5f) * 2f * amplitude;
            float yaw = (Mathf.PerlinNoise(0.37f, shakeTime * 1.07f) - 0.5f) * 2f * amplitude;
            float roll = (Mathf.PerlinNoise(shakeTime * 0.73f, 0.59f) - 0.5f) * 2f * amplitude * 1.1f;

            return Quaternion.Euler(pitch, yaw, roll);
        }
    }
}
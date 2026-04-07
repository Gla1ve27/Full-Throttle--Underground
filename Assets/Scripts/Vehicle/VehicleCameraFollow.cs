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

        [Header("Follow")]
        public Vector3 targetOffset = new Vector3(0f, 0.05f, 0f);
        public float followDistance = 5f;
        public float highSpeedFollowDistance = 7.25f;
        public float followHeight = 0.5f;
        public float highSpeedFollowHeight = 1.25f;
        public float followSmoothTime = 0.01f;
        public float rotationSharpness = 12f;
        public float lookAheadDistance = 6f;
        public float highSpeedLookAheadDistance = 7f;
        public float velocityHeadingBlend = 0.32f;
        public float headingResponsiveness = 8f;
        public float reverseHeadingDamping = 0.2f;
        public float focusHeight = 0.55f;

        [Header("Camera")]
        public float minFieldOfView = 40f;
        public float maxFieldOfView = 60f;
        public float speedForMaxFieldOfView = 120f;

        private Camera attachedCamera;
        private Vector3 followVelocity;
        private GameSettingsManager settingsManager;
        private Vector3 smoothedPlanarForward = Vector3.forward;

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
            Vector3 desiredPosition = pivot
                - planarForward * desiredDistance
                + Vector3.up * desiredHeight;

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref followVelocity, followSmoothTime);

            float activeLookAheadDistance = cameraEffectsEnabled
                ? Mathf.Lerp(lookAheadDistance, highSpeedLookAheadDistance, speedT)
                : lookAheadDistance;
            Vector3 lookPoint = pivot + planarForward * activeLookAheadDistance + Vector3.up * focusHeight;
            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            float blend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);

            UpdateFieldOfView(speedKph, speedT);
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
        }

        private void UpdateFieldOfView(float speedKph, float speedT)
        {
            if (attachedCamera == null)
            {
                return;
            }

            float baseFieldOfView = settingsManager != null ? settingsManager.CameraFieldOfView : minFieldOfView;
            float topFieldOfView = settingsManager != null && !settingsManager.CameraEffectsEnabled
                ? baseFieldOfView
                : Mathf.Max(baseFieldOfView, maxFieldOfView);
            float fovBlend = settingsManager != null && !settingsManager.CameraEffectsEnabled ? 0f : speedT;
            attachedCamera.fieldOfView = Mathf.Lerp(baseFieldOfView, topFieldOfView, fovBlend);
        }

        private Vector3 GetPlanarForward(Vector3 localVelocity, bool cameraEffectsEnabled)
        {
            Vector3 targetForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
            if (targetForward.sqrMagnitude < 0.001f)
            {
                targetForward = Vector3.forward;
            }

            if (targetBody == null || !cameraEffectsEnabled)
            {
                smoothedPlanarForward = Vector3.Slerp(
                    smoothedPlanarForward.sqrMagnitude > 0.001f ? smoothedPlanarForward : targetForward,
                    targetForward,
                    1f - Mathf.Exp(-headingResponsiveness * Time.deltaTime));
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
                1f - Mathf.Exp(-headingResponsiveness * Time.deltaTime));

            if (smoothedPlanarForward.sqrMagnitude < 0.001f)
            {
                smoothedPlanarForward = targetForward;
            }

            return smoothedPlanarForward.normalized;
        }

        private void ApplySimulatorPreset()
        {
            targetOffset = new Vector3(0f, 0.05f, 0f);
            followDistance = 5f;
            highSpeedFollowDistance = 7.25f;
            followHeight = 0.5f;
            highSpeedFollowHeight = 1.25f;
            followSmoothTime = 0.01f;
            rotationSharpness = 12f;
            lookAheadDistance = 6f;
            highSpeedLookAheadDistance = 7f;
            velocityHeadingBlend = 0.32f;
            headingResponsiveness = 8f;
            reverseHeadingDamping = 0.2f;
            focusHeight = 0.55f;
            minFieldOfView = 40f;
            maxFieldOfView = 60f;
            speedForMaxFieldOfView = 120f;
        }
    }
}

using FullThrottle.SacredCore.Vehicle;
using UnityEngine;

namespace FullThrottle.SacredCore.Camera
{
    public sealed class FTVehicleCameraDirector : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera cameraTarget;
        [SerializeField] private Transform followTarget;
        [SerializeField] private FTVehicleTelemetry telemetry;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 2.2f, -6.2f);
        [SerializeField] private float followResponse = 9f;
        [SerializeField] private float lookResponse = 12f;
        [SerializeField] private float baseFov = 62f;
        [SerializeField] private FTHighSpeedCameraMode highSpeed = new FTHighSpeedCameraMode();
        [SerializeField] private FTDriftCameraMode drift = new FTDriftCameraMode();

        private void Awake()
        {
            if (cameraTarget == null) cameraTarget = GetComponent<UnityEngine.Camera>();
            if (cameraTarget == null) cameraTarget = UnityEngine.Camera.main;
        }

        private void LateUpdate()
        {
            ResolveTarget();
            if (followTarget == null)
            {
                return;
            }

            float speed = telemetry != null ? telemetry.SpeedKph : 0f;
            float slip = telemetry != null ? telemetry.Slip01 : 0f;
            float speedWeight = highSpeed.Weight(speed);
            float driftWeight = drift.Weight(slip);

            Vector3 offset = localOffset;
            offset.z -= highSpeed.extraDistance * speedWeight;
            Vector3 desiredPosition = followTarget.TransformPoint(offset);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-followResponse * Time.deltaTime));

            Vector3 lookPoint = followTarget.position + followTarget.forward * Mathf.Lerp(6f, 11f, speedWeight) + Vector3.up * 1.1f;
            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            desiredRotation *= Quaternion.Euler(0f, drift.sideLookDegrees * driftWeight * Mathf.Sign(Vector3.Dot(followTarget.right, transform.position - followTarget.position)), drift.rollDegrees * driftWeight);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-lookResponse * Time.deltaTime));

            if (cameraTarget != null)
            {
                cameraTarget.fieldOfView = Mathf.Lerp(cameraTarget.fieldOfView, baseFov + highSpeed.extraFov * speedWeight, 1f - Mathf.Exp(-6f * Time.deltaTime));
            }
        }

        public void SetTarget(Transform target)
        {
            followTarget = target;
            telemetry = target != null ? target.GetComponentInChildren<FTVehicleTelemetry>() : null;
            Debug.Log($"[SacredCore] Vehicle camera target={(target != null ? target.name : "None")}.");
        }

        private void ResolveTarget()
        {
            if (followTarget != null)
            {
                return;
            }

            FTVehicleController controller = FindFirstObjectByType<FTVehicleController>();
            if (controller != null)
            {
                SetTarget(controller.transform);
            }
        }
    }
}

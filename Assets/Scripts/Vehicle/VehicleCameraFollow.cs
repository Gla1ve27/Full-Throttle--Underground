using UnityEngine;

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
        public Vector3 targetOffset = Vector3.zero;
        public float followDistance = 6.5f;
        public float followHeight = 2.2f;
        public float followSmoothTime = 0.12f;
        public float rotationSharpness = 10f;
        public float lookAheadDistance = 4f;
        public float velocityInfluence = 0.05f;

        [Header("Camera")]
        public float minFieldOfView = 60f;
        public float maxFieldOfView = 78f;
        public float speedForMaxFieldOfView = 180f;

        private Camera attachedCamera;
        private Vector3 followVelocity;

        private void Awake()
        {
            attachedCamera = GetComponent<Camera>();
            RefreshReferences();
        }

        private void LateUpdate()
        {
            RefreshReferences();

            if (target == null)
            {
                return;
            }

            Vector3 pivot = target.position + targetOffset;
            Vector3 velocityOffset = targetBody != null ? targetBody.velocity * velocityInfluence : Vector3.zero;
            Vector3 desiredPosition = pivot - target.forward * followDistance + Vector3.up * followHeight - velocityOffset;

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref followVelocity, followSmoothTime);

            Vector3 lookPoint = pivot + target.forward * lookAheadDistance;
            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            float blend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);

            UpdateFieldOfView();
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

        private void UpdateFieldOfView()
        {
            if (attachedCamera == null)
            {
                return;
            }

            float speedKph = 0f;

            if (targetVehicle != null)
            {
                speedKph = targetVehicle.SpeedKph;
            }
            else if (targetBody != null)
            {
                speedKph = targetBody.velocity.magnitude * 3.6f;
            }

            float t = Mathf.Clamp01(speedKph / Mathf.Max(1f, speedForMaxFieldOfView));
            attachedCamera.fieldOfView = Mathf.Lerp(minFieldOfView, maxFieldOfView, t);
        }
    }
}

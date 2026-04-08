using UnityEngine;
using Underground.Vehicle;

namespace Underground.World
{
    [RequireComponent(typeof(ReflectionProbe))]
    public sealed class PlayerReflectionProbeController : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 probeOffset = new Vector3(0f, 5f, 0f);
        [SerializeField] private Vector3 probeSize = new Vector3(120f, 36f, 120f);
        [SerializeField] private float snapDistance = 10f;
        [SerializeField] private float refreshInterval = 0.2f;
        [SerializeField] private float minMoveDistance = 6f;

        private ReflectionProbe reflectionProbe;
        private Vector3 lastRefreshPosition;
        private float nextRefreshTime;
        private bool hasRendered;

        public void Configure(Transform targetTransform, Vector3 offset, Vector3 size, float snapStep, float interval, float moveDistance)
        {
            target = targetTransform;
            probeOffset = offset;
            probeSize = size;
            snapDistance = snapStep;
            refreshInterval = interval;
            minMoveDistance = moveDistance;
            reflectionProbe ??= GetComponent<ReflectionProbe>();
            ConfigureProbe();
            hasRendered = false;
            nextRefreshTime = 0f;
        }

        private void Awake()
        {
            reflectionProbe = GetComponent<ReflectionProbe>();
            ConfigureProbe();
        }

        private void OnEnable()
        {
            reflectionProbe ??= GetComponent<ReflectionProbe>();
            ConfigureProbe();
            hasRendered = false;
            nextRefreshTime = 0f;
        }

        private void LateUpdate()
        {
            if (!ResolveTarget())
            {
                return;
            }

            Vector3 desiredPosition = target.position + probeOffset;
            desiredPosition.x = Snap(desiredPosition.x, snapDistance);
            desiredPosition.z = Snap(desiredPosition.z, snapDistance);
            transform.position = desiredPosition;

            float sqrMoveDistance = (transform.position - lastRefreshPosition).sqrMagnitude;
            bool movedEnough = sqrMoveDistance >= (minMoveDistance * minMoveDistance);
            if (!hasRendered || movedEnough || Time.time >= nextRefreshTime)
            {
                reflectionProbe.RenderProbe();
                lastRefreshPosition = transform.position;
                nextRefreshTime = Time.time + Mathf.Max(0.05f, refreshInterval);
                hasRendered = true;
            }
        }

        private void ConfigureProbe()
        {
            if (reflectionProbe == null)
            {
                return;
            }

            reflectionProbe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            reflectionProbe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            reflectionProbe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            reflectionProbe.boxProjection = true;
            reflectionProbe.size = probeSize;
        }

        private bool ResolveTarget()
        {
            if (target != null)
            {
                return true;
            }

            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                target = playerObject.transform;
                return true;
            }

            VehicleDynamicsController playerVehicle = FindFirstObjectByType<VehicleDynamicsController>();
            if (playerVehicle != null)
            {
                target = playerVehicle.transform;
                return true;
            }

            return false;
        }

        private static float Snap(float value, float step)
        {
            if (step <= 0.01f)
            {
                return value;
            }

            return Mathf.Round(value / step) * step;
        }
    }
}

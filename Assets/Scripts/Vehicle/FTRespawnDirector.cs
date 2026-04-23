using UnityEngine;

namespace FullThrottle.SacredCore.Vehicle
{
    public sealed class FTRespawnDirector : MonoBehaviour
    {
        [SerializeField] private FTVehicleController vehicle;
        [SerializeField] private FTDriverInput input;
        [SerializeField] private float safePoseRecordInterval = 0.75f;
        [SerializeField] private float minimumSafeSpeedKph = 8f;

        private Pose lastSafePose;
        private float nextRecordTime;
        private Rigidbody body;

        private void Awake()
        {
            if (vehicle == null) vehicle = GetComponent<FTVehicleController>();
            if (input == null) input = GetComponent<FTDriverInput>();
            body = GetComponent<Rigidbody>();
            lastSafePose = new Pose(transform.position, transform.rotation);
        }

        private void Update()
        {
            if (Time.time >= nextRecordTime)
            {
                nextRecordTime = Time.time + safePoseRecordInterval;
                if (vehicle != null && vehicle.Telemetry != null && vehicle.Telemetry.Grounded && vehicle.Telemetry.SpeedKph > minimumSafeSpeedKph)
                {
                    lastSafePose = new Pose(transform.position, transform.rotation);
                }
            }

            if (input != null && input.RespawnPressed)
            {
                RespawnAtLastSafePose();
            }
        }

        public void RespawnAtLastSafePose()
        {
            transform.SetPositionAndRotation(lastSafePose.position + Vector3.up * 0.35f, lastSafePose.rotation);
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            Debug.Log($"[SacredCore] Vehicle respawned at {lastSafePose.position}.");
        }
    }
}

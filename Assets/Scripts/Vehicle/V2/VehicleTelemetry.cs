using UnityEngine;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Computes speed, slip angle, grounded state, and weight transfer
    /// from the raw rigidbody and wheel collider data.
    /// Writes results into <see cref="VehicleState"/>.
    /// </summary>
    public sealed class VehicleTelemetry : MonoBehaviour
    {
        [Header("Weight Transfer")]
        [SerializeField] private float longitudinalTransferRate = 0.12f;
        [SerializeField] private float lateralTransferRate = 0.10f;
        [SerializeField] private float transferSmoothSpeed = 5f;

        [Header("Slip Detection")]
        [SerializeField] private float slideSlipAngleThreshold = 15f;

        private Rigidbody body;
        private WheelSet[] wheels;
        private VehicleState state;
        private Vector3 previousVelocity;

        public void Initialize(Rigidbody rb, WheelSet[] wheelSets, VehicleState vehicleState)
        {
            body = rb;
            wheels = wheelSets;
            state = vehicleState;
            previousVelocity = Vector3.zero;
        }

        /// <summary>
        /// Called at the start of FixedUpdate, before any physics system runs.
        /// Populates the VehicleState with fresh telemetry.
        /// </summary>
        public void UpdateTelemetry()
        {
            if (body == null || state == null)
            {
                return;
            }

            // ── Speed ──
            state.SpeedMs = body.linearVelocity.magnitude;
            state.SpeedKph = state.SpeedMs * 3.6f;
            state.ForwardSpeedKph = Vector3.Dot(body.linearVelocity, transform.forward) * 3.6f;

            // ── Grounded ──
            state.IsGrounded = false;
            if (wheels != null)
            {
                for (int i = 0; i < wheels.Length; i++)
                {
                    if (wheels[i] != null && wheels[i].collider != null && wheels[i].collider.isGrounded)
                    {
                        state.IsGrounded = true;
                        break;
                    }
                }
            }

            // ── Slip Angle ──
            UpdateSlipAngle();

            // ── Weight Transfer ──
            UpdateWeightTransfer();

            // ── Wheel Data ──
            UpdateWheelData();
        }

        private void UpdateSlipAngle()
        {
            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            if (planarVelocity.sqrMagnitude < 1f)
            {
                state.SlipAngleDegrees = 0f;
                state.SignedSlipAngleDegrees = 0f;
                state.IsSliding = false;
                return;
            }

            Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 planarVelocityDir = planarVelocity.normalized;
            state.SlipAngleDegrees = Vector3.Angle(planarForward, planarVelocityDir);
            state.SignedSlipAngleDegrees = Vector3.SignedAngle(planarForward, planarVelocityDir, Vector3.up);
            state.IsSliding = state.SlipAngleDegrees >= slideSlipAngleThreshold;
        }

        private void UpdateWeightTransfer()
        {
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f)
            {
                return;
            }

            Vector3 deltaV = body.linearVelocity - previousVelocity;

            // Longitudinal: braking shifts weight forward (negative), throttle shifts rear (positive)
            float longitudinalAccel = Vector3.Dot(deltaV, transform.forward) / Mathf.Max(0.001f, dt);
            float targetLong = Mathf.Clamp(-longitudinalAccel * longitudinalTransferRate / 9.81f, -1f, 1f);

            // Lateral: turning shifts weight to outside
            float lateralAccel = Vector3.Dot(deltaV, transform.right) / Mathf.Max(0.001f, dt);
            float targetLat = Mathf.Clamp(lateralAccel * lateralTransferRate / 9.81f, -1f, 1f);

            state.LongitudinalLoadShift = Mathf.MoveTowards(state.LongitudinalLoadShift, targetLong, transferSmoothSpeed * dt);
            state.LateralLoadShift = Mathf.MoveTowards(state.LateralLoadShift, targetLat, transferSmoothSpeed * dt);

            previousVelocity = body.linearVelocity;
        }

        private void UpdateWheelData()
        {
            if (wheels == null)
            {
                return;
            }

            float drivenRpmSum = 0f;
            int drivenCount = 0;
            float frontSlipSum = 0f;
            int frontCount = 0;
            float rearSlipSum = 0f;
            int rearCount = 0;

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet wheel = wheels[i];
                if (wheel == null || wheel.collider == null)
                {
                    continue;
                }

                if (wheel.drive)
                {
                    drivenRpmSum += wheel.collider.rpm;
                    drivenCount++;
                }

                if (wheel.collider.GetGroundHit(out WheelHit hit))
                {
                    bool isFront = wheel.steer || wheel.axleId == "Front";
                    float slip = Mathf.Abs(hit.sidewaysSlip);

                    if (isFront)
                    {
                        frontSlipSum += slip;
                        frontCount++;
                    }
                    else
                    {
                        rearSlipSum += slip;
                        rearCount++;
                    }
                }
            }

            state.AverageDrivenWheelRPM = drivenCount > 0 ? drivenRpmSum / drivenCount : 0f;
            state.FrontSlip = frontCount > 0 ? frontSlipSum / frontCount : 0f;
            state.RearSlip = rearCount > 0 ? rearSlipSum / rearCount : 0f;
        }

        public void ResetTelemetry()
        {
            previousVelocity = Vector3.zero;
        }
    }
}

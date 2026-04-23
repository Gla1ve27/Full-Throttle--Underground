using UnityEngine;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Stability assists: countersteer, yaw damping, high-speed stability,
    /// lateral grip correction, and low-speed stop assist.
    /// Supports intent — doesn't fight it.
    /// </summary>
    public sealed class VehicleAssistSystem : MonoBehaviour
    {
        [Header("Low-Speed Stabilization")]
        [SerializeField] private float lowSpeedAssistEntryKph = 12f;
        [SerializeField] private float lowSpeedStopSnapKph = 0.85f;
        [SerializeField] private float lowSpeedCoastDeceleration = 4.5f;
        [SerializeField] private float lowSpeedLateralDamping = 3.5f;
        [SerializeField] private float lowSpeedAngularDamping = 2.4f;

        public void UpdateAssists(
            Rigidbody body,
            VehicleState state,
            DriverCommand cmd,
            RuntimeVehicleStats stats,
            float driftAssistBlend)
        {
            if (body == null || state == null || stats == null || !state.IsGrounded)
            {
                return;
            }

            ApplyCounterSteerAssist(body, state, cmd, stats);
            ApplyYawStability(body, state, cmd, stats, driftAssistBlend);
            ApplyHighSpeedStability(body, state, stats);
            ApplyLateralGripAssist(body, state, cmd, stats, driftAssistBlend);
            ApplyDrivetrainBehavior(body, state, cmd, stats);
            ApplyLowSpeedStopAssist(body, state, cmd, stats);
        }

        private void ApplyCounterSteerAssist(Rigidbody body, VehicleState state, DriverCommand cmd, RuntimeVehicleStats stats)
        {
            if (!state.IsSliding)
            {
                return;
            }

            float assistStrength = stats.CounterSteerAssist;
            if (assistStrength <= 0f)
            {
                return;
            }

            float speedAssistT = Mathf.InverseLerp(35f, 150f, Mathf.Abs(state.ForwardSpeedKph));
            if (speedAssistT <= 0f)
            {
                return;
            }

            float desiredYawAssist = -state.SignedSlipAngleDegrees * assistStrength * 1.8f * speedAssistT;
            body.AddTorque(Vector3.up * desiredYawAssist, ForceMode.Acceleration);
        }

        private void ApplyYawStability(Rigidbody body, VehicleState state, DriverCommand cmd, RuntimeVehicleStats stats, float driftBlend)
        {
            float yawStrength = stats.YawStability;
            if (yawStrength <= 0f)
            {
                return;
            }

            float yawRate = body.angularVelocity.y;
            float dampingTorque = -yawRate * yawStrength * 2.5f;
            float steerIntent = Mathf.Abs(cmd.Steering);

            // Reduce damping when player is actively steering or drifting
            float dampScale = Mathf.Lerp(1f, 0.3f, steerIntent);
            dampScale *= Mathf.Lerp(1f, 0.4f, driftBlend); // Less interference during drift

            body.AddTorque(Vector3.up * dampingTorque * dampScale, ForceMode.Acceleration);
        }

        private void ApplyHighSpeedStability(Rigidbody body, VehicleState state, RuntimeVehicleStats stats)
        {
            float stabilityFactor = stats.HighSpeedStability;
            if (stabilityFactor <= 0f)
            {
                return;
            }

            float speedT = Mathf.InverseLerp(120f, 280f, state.SpeedKph);
            if (speedT <= 0f)
            {
                return;
            }

            Vector3 localVelocity = body.transform.InverseTransformDirection(body.linearVelocity);
            float lateralCorrection = -localVelocity.x * stabilityFactor * speedT * 0.4f;
            body.AddForce(body.transform.right * lateralCorrection, ForceMode.Acceleration);

            float yawDamp = -body.angularVelocity.y * stabilityFactor * speedT * 0.6f;
            body.AddTorque(Vector3.up * yawDamp, ForceMode.Acceleration);
        }

        private void ApplyLateralGripAssist(Rigidbody body, VehicleState state, DriverCommand cmd,
            RuntimeVehicleStats stats, float driftBlend)
        {
            Vector3 localVelocity = body.transform.InverseTransformDirection(body.linearVelocity);
            float driftMod = Mathf.Max(cmd.Handbrake ? 1f : 0f, driftBlend);
            float handbrakeGripReduction = Mathf.Lerp(1f, stats.HandbrakeGripMultiplier, driftMod);
            Vector3 correctiveForce = -body.transform.right * localVelocity.x * stats.LateralGripAssist * handbrakeGripReduction;
            body.AddForce(correctiveForce, ForceMode.Acceleration);
        }

        private void ApplyDrivetrainBehavior(Rigidbody body, VehicleState state, DriverCommand cmd, RuntimeVehicleStats stats)
        {
            float throttle = cmd.Throttle;
            float steer = cmd.Steering;
            float absSteer = Mathf.Abs(steer);

            switch (stats.Drivetrain)
            {
                case DrivetrainType.FWD:
                    ApplyFwdBehavior(body, state, stats, throttle, steer, absSteer);
                    break;
                case DrivetrainType.RWD:
                    ApplyRwdBehavior(body, state, stats, throttle, steer, absSteer);
                    break;
                case DrivetrainType.AWD:
                    ApplyAwdBehavior(body, state, stats, throttle);
                    break;
            }
        }

        private void ApplyFwdBehavior(Rigidbody body, VehicleState state, RuntimeVehicleStats stats,
            float throttle, float steer, float absSteer)
        {
            if (throttle < 0.1f || absSteer < 0.1f || state.SpeedKph < 25f)
            {
                return;
            }

            float understeerForce = throttle * absSteer * 0.35f;
            float speedScale = Mathf.InverseLerp(25f, 120f, state.SpeedKph);
            body.AddForce(body.transform.forward * understeerForce * speedScale * stats.Mass * 0.02f, ForceMode.Force);
        }

        private void ApplyRwdBehavior(Rigidbody body, VehicleState state, RuntimeVehicleStats stats,
            float throttle, float steer, float absSteer)
        {
            if (throttle < 0.3f || state.SpeedKph < 30f)
            {
                return;
            }

            float oversteerTorque = throttle * absSteer * 0.25f;
            float direction = steer > 0f ? 1f : -1f;
            float speedScale = Mathf.InverseLerp(30f, 100f, state.SpeedKph);
            body.AddTorque(Vector3.up * oversteerTorque * direction * speedScale * 0.8f, ForceMode.Acceleration);
        }

        private void ApplyAwdBehavior(Rigidbody body, VehicleState state, RuntimeVehicleStats stats, float throttle)
        {
            if (throttle > 0.1f)
            {
                Vector3 localVelocity = body.transform.InverseTransformDirection(body.linearVelocity);
                float stabilityForce = -localVelocity.x * throttle * 0.15f;
                body.AddForce(body.transform.right * stabilityForce, ForceMode.Acceleration);
            }

            if (throttle > 0.8f && state.SpeedKph < 40f)
            {
                body.AddForce(body.transform.forward * throttle * stats.Mass * 0.008f, ForceMode.Force);
            }
        }

        private void ApplyLowSpeedStopAssist(Rigidbody body, VehicleState state, DriverCommand cmd, RuntimeVehicleStats stats)
        {
            if (cmd.Handbrake || cmd.Throttle > 0.05f || state.IsReversing || cmd.ReverseHeld || state.IsSliding)
            {
                return;
            }

            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            if (planarVelocity.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 planarForward = Vector3.ProjectOnPlane(body.transform.forward, Vector3.up).normalized;
            Vector3 planarRight = Vector3.ProjectOnPlane(body.transform.right, Vector3.up).normalized;
            if (planarForward.sqrMagnitude <= 0.0001f || planarRight.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float forwardVelocityMs = Vector3.Dot(planarVelocity, planarForward);
            float lateralVelocityMs = Vector3.Dot(planarVelocity, planarRight);
            float absForwardSpeedKph = Mathf.Abs(forwardVelocityMs) * 3.6f;

            if (absForwardSpeedKph > lowSpeedAssistEntryKph)
            {
                return;
            }

            float assistT = 1f - Mathf.InverseLerp(0f, lowSpeedAssistEntryKph, absForwardSpeedKph);
            float brakeAssist = Mathf.Lerp(1f, 1.8f, Mathf.Clamp01(cmd.Brake));
            float forwardStep = lowSpeedCoastDeceleration * Mathf.Lerp(0.4f, 1f, assistT) * brakeAssist * Time.fixedDeltaTime;
            float lateralStep = lowSpeedLateralDamping * Mathf.Lerp(0.35f, 1f, assistT) * Time.fixedDeltaTime;

            forwardVelocityMs = Mathf.MoveTowards(forwardVelocityMs, 0f, forwardStep);
            lateralVelocityMs = Mathf.MoveTowards(lateralVelocityMs, 0f, lateralStep);

            bool shouldSnap = absForwardSpeedKph <= lowSpeedStopSnapKph
                && Mathf.Abs(lateralVelocityMs) * 3.6f <= lowSpeedStopSnapKph
                && Mathf.Abs(cmd.Steering) < 0.2f;

            if (shouldSnap)
            {
                forwardVelocityMs = 0f;
                lateralVelocityMs = 0f;
            }

            Vector3 stabilized = (planarForward * forwardVelocityMs) + (planarRight * lateralVelocityMs);
            body.linearVelocity = stabilized + Vector3.Project(body.linearVelocity, Vector3.up);

            Vector3 angularVelocity = body.angularVelocity;
            float angularStep = lowSpeedAngularDamping * Mathf.Lerp(0.35f, 1f, assistT) * Time.fixedDeltaTime;
            angularVelocity.y = Mathf.MoveTowards(angularVelocity.y, 0f, angularStep);

            if (shouldSnap)
            {
                angularVelocity.x = Mathf.MoveTowards(angularVelocity.x, 0f, angularStep * 0.6f);
                angularVelocity.z = Mathf.MoveTowards(angularVelocity.z, 0f, angularStep * 0.6f);
            }

            body.angularVelocity = angularVelocity;
        }
    }
}

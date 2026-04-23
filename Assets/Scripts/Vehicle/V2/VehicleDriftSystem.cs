using UnityEngine;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Drift state machine: None → Entry → Sustain → Donut → Recovery.
    /// Manages drift initiation timers, yaw assist, and speed preservation.
    /// </summary>
    public sealed class VehicleDriftSystem : MonoBehaviour
    {
        [Header("Drift Initiation")]
        [SerializeField] private float handbrakeDriftWindow = 0.45f;
        [SerializeField] private float driftSustainWindow = 0.35f;

        [Header("Drift Physics")]
        [SerializeField] private float driftYawAssist = 0.82f;
        [SerializeField] private float driftSpeedPreservation = 0.985f;
        [SerializeField] private float donutYawAssist = 3.6f;

        [Header("Thresholds")]
        [SerializeField] private float slideSlipAngleThreshold = 15f;
        [SerializeField] private float fullSlideSlipAngle = 28f;

        private float driftInitiationTimer;
        private float driftSustainTimer;
        private bool wasHandbrakeHeld;

        /// <summary>
        /// Returns the current drift assist blend (0 = no drift, 1 = full drift assist).
        /// Used by the grip system to reduce rear grip during drift.
        /// </summary>
        public float DriftAssistBlend { get; private set; }

        public void UpdateDriftState(VehicleState state, DriverCommand cmd)
        {
            if (state == null)
            {
                return;
            }

            // Detect handbrake press
            bool handbrakePressed = cmd.Handbrake && !wasHandbrakeHeld;
            if (handbrakePressed && state.SpeedKph > 8f)
            {
                driftInitiationTimer = handbrakeDriftWindow;
            }

            // Sustain conditions
            bool driftSustainInput = cmd.Handbrake && state.SpeedKph > 8f
                || cmd.Throttle > 0.35f
                    && Mathf.Abs(cmd.Steering) > 0.18f
                    && state.SpeedKph > 12f
                    && (driftInitiationTimer > 0f || state.IsSliding);

            if (driftSustainInput)
            {
                driftSustainTimer = driftSustainWindow;
            }

            driftInitiationTimer = Mathf.Max(0f, driftInitiationTimer - Time.fixedDeltaTime);
            driftSustainTimer = Mathf.Max(0f, driftSustainTimer - Time.fixedDeltaTime);
            wasHandbrakeHeld = cmd.Handbrake;

            // Compute blend
            float initiation = handbrakeDriftWindow > 0f ? Mathf.Clamp01(driftInitiationTimer / handbrakeDriftWindow) : 0f;
            float sustain = driftSustainWindow > 0f ? Mathf.Clamp01(driftSustainTimer / driftSustainWindow) : 0f;
            float slide = Mathf.InverseLerp(slideSlipAngleThreshold * 0.65f, fullSlideSlipAngle, state.SlipAngleDegrees);
            DriftAssistBlend = Mathf.Clamp01(Mathf.Max(initiation, sustain * 0.75f, slide * 0.35f));

            // Update drift phase in state
            UpdateDriftPhase(state, cmd);

            state.DriftAngle = state.SlipAngleDegrees;
        }

        /// <summary>
        /// Applies drift yaw assist and speed preservation forces.
        /// Call after grip system in FixedUpdate.
        /// </summary>
        public void ApplyDriftForces(Rigidbody body, VehicleState state, DriverCommand cmd, RuntimeVehicleStats stats)
        {
            if (body == null || state == null || stats == null || !state.IsGrounded)
            {
                return;
            }

            float driftStrength = stats.DriftAssist;
            if (driftStrength <= 0f || DriftAssistBlend <= 0f)
            {
                return;
            }

            float assistBlend = DriftAssistBlend * driftStrength;

            float steerDirection = Mathf.Abs(cmd.Steering) > 0.05f
                ? Mathf.Sign(cmd.Steering)
                : Mathf.Sign(state.SignedSlipAngleDegrees);

            float speedT = Mathf.Clamp01(Mathf.InverseLerp(8f, 130f, state.SpeedKph));

            // Donut yaw when handbrake + throttle + steering at low speed
            float handbrakeDonutT = cmd.Handbrake && cmd.Throttle > 0.25f
                ? Mathf.Clamp01(Mathf.Abs(cmd.Steering) + cmd.Throttle * 0.45f)
                : 0f;

            float yawStrength = driftYawAssist * assistBlend * Mathf.Max(speedT, handbrakeDonutT * donutYawAssist);
            body.AddTorque(Vector3.up * steerDirection * yawStrength, ForceMode.Acceleration);

            // Speed preservation during drift
            Vector3 localVelocity = body.transform.InverseTransformDirection(body.linearVelocity);
            if (localVelocity.z > 1f)
            {
                localVelocity.z *= Mathf.Lerp(1f, driftSpeedPreservation, assistBlend * Time.fixedDeltaTime * 3f);
                body.linearVelocity = body.transform.TransformDirection(localVelocity);
            }
        }

        private void UpdateDriftPhase(VehicleState state, DriverCommand cmd)
        {
            if (DriftAssistBlend <= 0.05f)
            {
                state.CurrentDriftPhase = DriftPhase.None;
                state.DriftTimer = 0f;
                return;
            }

            // Donut: handbrake + throttle + steering at very low speed
            if (cmd.Handbrake && cmd.Throttle > 0.35f && state.SpeedKph < 30f && Mathf.Abs(cmd.Steering) > 0.4f)
            {
                state.CurrentDriftPhase = DriftPhase.Donut;
                state.DriftTimer += Time.fixedDeltaTime;
                return;
            }

            // Entry: just initiated
            if (driftInitiationTimer > handbrakeDriftWindow * 0.5f)
            {
                state.CurrentDriftPhase = DriftPhase.Entry;
                state.DriftTimer += Time.fixedDeltaTime;
                return;
            }

            // Sustain: active sliding with input
            if (state.IsSliding && (cmd.Throttle > 0.2f || cmd.Handbrake))
            {
                state.CurrentDriftPhase = DriftPhase.Sustain;
                state.DriftTimer += Time.fixedDeltaTime;
                return;
            }

            // Recovery: was sliding, now slip angle decreasing
            if (state.CurrentDriftPhase == DriftPhase.Sustain || state.CurrentDriftPhase == DriftPhase.Entry)
            {
                state.CurrentDriftPhase = DriftPhase.Recovery;
                state.DriftTimer = 0f;
                return;
            }

            state.CurrentDriftPhase = DriftPhase.None;
            state.DriftTimer = 0f;
        }

        public void ResetDrift()
        {
            driftInitiationTimer = 0f;
            driftSustainTimer = 0f;
            wasHandbrakeHeld = false;
            DriftAssistBlend = 0f;
        }
    }
}

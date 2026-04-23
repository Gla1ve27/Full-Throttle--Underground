using UnityEngine;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Blends free-rev RPM and wheel-coupled RPM based on clutch state,
    /// slip, grounded state, and shift state. Produces a stable engine RPM
    /// suitable for premium audio consumption.
    /// </summary>
    public sealed class EngineRPMModel : MonoBehaviour
    {
        [Header("RPM Dynamics")]
        [SerializeField] private float freeRevRiseRate = 8000f;
        [SerializeField] private float freeRevFallRate = 5000f;
        [SerializeField] private float wheelCouplingResponse = 14f;
        [SerializeField, Range(0f, 1f)] private float launchSlipInfluence = 0.01f;
        [SerializeField, Range(0f, 1f)] private float rollingSlipInfluence = 0.03f;
        [SerializeField] private float fullWheelInfluenceSpeedKph = 65f;

        [Header("Telemetry Trust")]
        [SerializeField] private float saneWheelRadiusMin = 0.22f;
        [SerializeField] private float saneWheelRadiusMax = 0.65f;
        [SerializeField] private float colliderRpmTrustSpeedKph = 18f;
        [SerializeField] private float wheelRpmMismatchTolerance = 850f;
        [SerializeField] private bool logRadiusWarnings = true;

        [Header("Handbrake Rev")]
        [SerializeField, Range(0f, 1f)] private float handbrakeRevTarget01 = 0.72f;

        private float smoothedWheelRpm;
        private float handbrakeRevRpm01;
        private float lastRadiusWarningTime = -999f;

        public void Initialize()
        {
            smoothedWheelRpm = 0f;
            handbrakeRevRpm01 = 0f;
        }

        public void UpdateRPM(
            VehicleState state,
            DriverCommand cmd,
            RuntimeVehicleStats stats,
            float driveRatio,
            WheelSet[] wheels)
        {
            if (state == null || stats == null)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            float freeRevTarget = Mathf.Lerp(stats.IdleRPM, stats.MaxRPM, cmd.Throttle * 0.92f);
            float freeRevRate = cmd.Throttle > 0.05f ? freeRevRiseRate : freeRevFallRate;
            state.FreeRevRPM = Mathf.MoveTowards(state.FreeRevRPM, freeRevTarget, freeRevRate * dt);

            float stableWheelRpm = ComputeStableWheelRpm(state, stats, wheels);
            stableWheelRpm = ApplyHandbrakeRev(stableWheelRpm, state, cmd, stats, driveRatio);
            state.WheelDrivenRPM = Mathf.Clamp(
                Mathf.Abs(stableWheelRpm) * Mathf.Abs(driveRatio),
                stats.IdleRPM,
                stats.MaxRPM);

            float couplingBase = state.IsGrounded ? 0.92f : 0.1f;
            float clutchFactor = state.ClutchEngagement;
            float shiftFactor = state.IsShifting ? Mathf.Lerp(0.15f, 0.85f, state.ShiftProgress) : 1f;
            float handbrakeDecouple = cmd.Handbrake && cmd.Throttle > 0.05f ? 0.45f : 1f;
            float coupling = couplingBase * clutchFactor * shiftFactor * handbrakeDecouple;

            float blendedRpm = Mathf.Lerp(state.FreeRevRPM, state.WheelDrivenRPM, coupling);
            state.EngineRPM = Mathf.Clamp(blendedRpm, stats.IdleRPM, stats.MaxRPM);
            state.NormalizedRPM = Mathf.InverseLerp(stats.IdleRPM, stats.MaxRPM, state.EngineRPM);

            state.EngineLoad = cmd.Throttle * Mathf.Lerp(0.3f, 1f, state.NormalizedRPM);

            float limiterStart = 0.96f;
            state.LimiterAmount = state.NormalizedRPM > limiterStart
                ? Mathf.InverseLerp(limiterStart, 1f, state.NormalizedRPM)
                : 0f;

            float turboTarget = cmd.Throttle > 0.3f && state.NormalizedRPM > 0.35f
                ? Mathf.Clamp01(cmd.Throttle * state.NormalizedRPM * 1.5f)
                : 0f;
            float turboRate = turboTarget > state.TurboSpoolAmount ? 8f : 5f;
            state.TurboSpoolAmount = Mathf.MoveTowards(state.TurboSpoolAmount, turboTarget, turboRate * dt);
        }

        private float ComputeStableWheelRpm(VehicleState state, RuntimeVehicleStats stats, WheelSet[] wheels)
        {
            if (wheels == null || wheels.Length == 0)
            {
                return 0f;
            }

            float averageRadius = 0f;
            int radiusCount = 0;
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null || wheels[i].collider == null || !wheels[i].drive)
                {
                    continue;
                }

                float rawRadius = wheels[i].collider.radius;
                if ((rawRadius < saneWheelRadiusMin || rawRadius > saneWheelRadiusMax)
                    && logRadiusWarnings && Time.time - lastRadiusWarningTime > 1f)
                {
                    Debug.LogWarning($"[EngineRPMModel] Clamped suspicious wheel radius {rawRadius:F3} on {wheels[i].collider.name}.");
                    lastRadiusWarningTime = Time.time;
                }

                averageRadius += Mathf.Clamp(rawRadius, saneWheelRadiusMin, saneWheelRadiusMax);
                radiusCount++;
            }

            if (radiusCount == 0)
            {
                return state.AverageDrivenWheelRPM;
            }

            averageRadius /= radiusCount;
            float circumference = Mathf.Max(0.01f, Mathf.PI * 2f * averageRadius);
            float speedBasedRpm = (state.ForwardSpeedKph / 3.6f) / circumference * 60f;
            float absSpeed = Mathf.Abs(state.ForwardSpeedKph);
            float speedInfluence = Mathf.InverseLerp(12f, fullWheelInfluenceSpeedKph, absSpeed);
            float slipInfluence = Mathf.Lerp(launchSlipInfluence, rollingSlipInfluence, speedInfluence);

            float colliderDrivenRpm = state.AverageDrivenWheelRPM;
            float mismatch = Mathf.Abs(colliderDrivenRpm - speedBasedRpm);
            float mismatchT = Mathf.InverseLerp(wheelRpmMismatchTolerance, wheelRpmMismatchTolerance * 2.2f, mismatch);
            float trustColliderRpm = absSpeed < colliderRpmTrustSpeedKph
                ? 1f
                : Mathf.Lerp(1f, 0.15f, mismatchT);

            float targetWheelRpm = Mathf.Lerp(speedBasedRpm, colliderDrivenRpm, slipInfluence * trustColliderRpm);
            float response = 1f - Mathf.Exp(-Mathf.Max(0.01f, wheelCouplingResponse) * Time.fixedDeltaTime);
            smoothedWheelRpm = Mathf.Lerp(smoothedWheelRpm, targetWheelRpm, response);
            return smoothedWheelRpm;
        }

        private float ApplyHandbrakeRev(float stableWheelRpm, VehicleState state, DriverCommand cmd,
            RuntimeVehicleStats stats, float driveRatio)
        {
            if (state.IsReversing)
            {
                handbrakeRevRpm01 = Mathf.MoveTowards(handbrakeRevRpm01, 0f, Time.fixedDeltaTime * 5f);
                return stableWheelRpm;
            }

            bool canRev = cmd.Handbrake && cmd.Throttle > 0.05f;
            float target01 = canRev ? Mathf.Lerp(0.22f, handbrakeRevTarget01, cmd.Throttle) : 0f;
            float rate = canRev ? 5.5f : 8f;
            handbrakeRevRpm01 = Mathf.MoveTowards(handbrakeRevRpm01, target01, rate * Time.fixedDeltaTime);

            if (handbrakeRevRpm01 <= 0f)
            {
                return stableWheelRpm;
            }

            float targetEngineRpm = Mathf.Lerp(stats.IdleRPM, stats.MaxRPM, handbrakeRevRpm01);
            float safeRatio = Mathf.Max(0.01f, Mathf.Abs(driveRatio));
            float virtualWheelRpm = targetEngineRpm / safeRatio;
            return Mathf.Max(stableWheelRpm, virtualWheelRpm);
        }

        public void ResetRPM()
        {
            smoothedWheelRpm = 0f;
            handbrakeRevRpm01 = 0f;
        }
    }
}

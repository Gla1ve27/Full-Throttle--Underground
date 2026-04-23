using UnityEngine;
using Underground.Vehicle.V2;

namespace Underground.Audio.V2
{
    /// <summary>
    /// Converts raw <see cref="VehicleState"/> into smooth, audio-optimized values.
    /// This is the ONLY entry point for the V2 audio system to read vehicle data.
    /// All smoothing and sanity filtering happens here so the loop system can trust
    /// the telemetry more than raw transient physics spikes.
    /// </summary>
    public sealed class EngineAudioStateFeed : MonoBehaviour
    {
        [Header("Smoothing")]
        [SerializeField] private float rpmRiseResponse = 12f;
        [SerializeField] private float rpmFallResponse = 9.5f;
        [SerializeField] private float throttleResponse = 14f;
        [SerializeField] private float speedResponse = 10f;
        [SerializeField] private float slipAttackResponse = 8f;
        [SerializeField] private float slipReleaseResponse = 20f;

        [Header("Deadband")]
        [SerializeField] private float rpmDeadband = 18f;

        [Header("Telemetry Trust")]
        [SerializeField] private float trustedRpmRiseLimitPerSecond = 9500f;
        [SerializeField] private float trustedRpmFallLimitPerSecond = 7800f;
        [SerializeField] private float drivetrainJumpSuppressionThreshold = 1350f;

        [Header("RPM Source")]
        [SerializeField] private float fallbackIdleRPM = 900f;
        [SerializeField] private float fallbackMaxRPM = 7800f;

        public float RawEngineRPM { get; private set; }
        public float TrustedEngineRPM { get; private set; }
        public float RawWheelDrivenRPM { get; private set; }
        public float SmoothedRPM { get; private set; }
        public float NormalizedRPM { get; private set; }
        public float SmoothedThrottle { get; private set; }
        public float SmoothedSpeedKph { get; private set; }
        public float SmoothedSlip { get; private set; }
        public float OnThrottleBlend { get; private set; }
        public float TurboSpool { get; private set; }
        public float LimiterAmount { get; private set; }
        public int Gear { get; private set; } = 1;
        public int PreviousGear { get; private set; } = 1;
        public bool GearJustChanged { get; private set; }
        public float TimeSinceLastGearChange { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsReversing { get; private set; }
        public bool IsShifting { get; private set; }
        public float ShiftProgress { get; private set; }
        public ShiftDirection LastShiftDirection { get; private set; }
        public DriftPhase CurrentDriftPhase { get; private set; }
        public float EngineLoad { get; private set; }
        public float ClutchEngagement { get; private set; }
        public bool ShiftJustStarted { get; private set; }
        public bool ShiftJustEnded { get; private set; }

        private float onThrottleTarget;
        private bool wasShifting;
        private float idleRPM;
        private float maxRPM;

        [Header("Throttle Thresholds")]
        [SerializeField] private float throttleOnThreshold = 0.18f;
        [SerializeField] private float throttleOffThreshold = 0.10f;
        [SerializeField] private float onOffBlendResponse = 11f;

        private VehicleState vehicleState;

        public void Bind(VehicleState state, float idle = 0f, float max = 0f)
        {
            vehicleState = state;
            idleRPM = idle > 0f ? idle : fallbackIdleRPM;
            maxRPM = max > 0f ? max : fallbackMaxRPM;
            RawEngineRPM = idleRPM;
            TrustedEngineRPM = idleRPM;
            SmoothedRPM = idleRPM;
            NormalizedRPM = 0f;
            Gear = 1;
            PreviousGear = 1;
            TimeSinceLastGearChange = 0f;
        }

        public void ApplyTuning(NFSU2CarAudioBank.RuntimeTuning tuning)
        {
            if (tuning == null)
            {
                return;
            }

            rpmRiseResponse = Mathf.Max(0.01f, tuning.rpmRiseResponse);
            rpmFallResponse = Mathf.Max(0.01f, tuning.rpmFallResponse);
            throttleResponse = Mathf.Max(0.01f, tuning.throttleResponse);
            speedResponse = Mathf.Max(0.01f, tuning.speedResponse);
            throttleOnThreshold = Mathf.Clamp01(tuning.throttleOnThreshold);
            throttleOffThreshold = Mathf.Clamp(tuning.throttleOffThreshold, 0f, throttleOnThreshold);
        }

        public void UpdateFeed(float dt)
        {
            if (vehicleState == null || dt <= 0f)
            {
                return;
            }

            RawEngineRPM = ClampFinite(vehicleState.EngineRPM, idleRPM, maxRPM, idleRPM);
            RawWheelDrivenRPM = ClampFinite(vehicleState.WheelDrivenRPM, -maxRPM, maxRPM, 0f);

            float riseLimit = trustedRpmRiseLimitPerSecond * dt;
            float fallLimit = trustedRpmFallLimitPerSecond * dt;
            float rawDelta = RawEngineRPM - TrustedEngineRPM;
            if (Mathf.Abs(rawDelta) > drivetrainJumpSuppressionThreshold)
            {
                float limit = rawDelta > 0f ? riseLimit : fallLimit;
                TrustedEngineRPM += Mathf.Clamp(rawDelta, -limit, limit);
            }
            else
            {
                TrustedEngineRPM = RawEngineRPM;
            }

            float rpmTarget = TrustedEngineRPM;
            if (Mathf.Abs(rpmTarget - SmoothedRPM) <= rpmDeadband)
            {
                rpmTarget = SmoothedRPM;
            }

            float rpmRate = rpmTarget >= SmoothedRPM ? rpmRiseResponse : rpmFallResponse;
            SmoothedRPM = ExpSmooth(SmoothedRPM, rpmTarget, rpmRate, dt);
            NormalizedRPM = maxRPM > idleRPM ? Mathf.InverseLerp(idleRPM, maxRPM, SmoothedRPM) : 0f;

            SmoothedThrottle = ExpSmooth(SmoothedThrottle, ClampFinite(vehicleState.Throttle, 0f, 1f, 0f), throttleResponse, dt);

            if (SmoothedThrottle > throttleOnThreshold) onThrottleTarget = 1f;
            else if (SmoothedThrottle < throttleOffThreshold) onThrottleTarget = 0f;
            OnThrottleBlend = ExpSmooth(OnThrottleBlend, onThrottleTarget, onOffBlendResponse, dt);

            SmoothedSpeedKph = ExpSmooth(SmoothedSpeedKph, Mathf.Abs(ClampFinite(vehicleState.ForwardSpeedKph, -450f, 450f, 0f)), speedResponse, dt);

            float rawSlip = Mathf.Max(
                ClampFinite(vehicleState.FrontSlip, 0f, 1.5f, 0f),
                ClampFinite(vehicleState.RearSlip, 0f, 1.5f, 0f));
            float slipRate = rawSlip >= SmoothedSlip ? slipAttackResponse : slipReleaseResponse;
            SmoothedSlip = ExpSmooth(SmoothedSlip, rawSlip, slipRate, dt);

            int newGear = vehicleState.Gear;
            GearJustChanged = newGear != Gear;
            if (GearJustChanged)
            {
                PreviousGear = Gear;
                Gear = newGear;
                TimeSinceLastGearChange = 0f;
            }
            else
            {
                Gear = newGear;
                PreviousGear = vehicleState.PreviousGear;
                TimeSinceLastGearChange += dt;
            }

            IsGrounded = vehicleState.IsGrounded;
            IsReversing = vehicleState.IsReversing;
            TurboSpool = ClampFinite(vehicleState.TurboSpoolAmount, 0f, 1f, 0f);
            LimiterAmount = ClampFinite(vehicleState.LimiterAmount, 0f, 1f, 0f);
            EngineLoad = ClampFinite(vehicleState.EngineLoad, -1f, 1f, 0f);
            ClutchEngagement = ClampFinite(vehicleState.ClutchEngagement, 0f, 1f, 1f);
            CurrentDriftPhase = vehicleState.CurrentDriftPhase;

            IsShifting = vehicleState.IsShifting;
            ShiftProgress = vehicleState.ShiftProgress;
            LastShiftDirection = vehicleState.LastShiftDirection;
            ShiftJustStarted = IsShifting && !wasShifting;
            ShiftJustEnded = !IsShifting && wasShifting;
            wasShifting = IsShifting;
        }

        private static float ExpSmooth(float current, float target, float response, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-response * dt));
        }

        private static float ClampFinite(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            return Mathf.Clamp(value, min, max);
        }
    }
}

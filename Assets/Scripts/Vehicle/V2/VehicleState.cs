using UnityEngine;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Central mutable runtime state for the V2 vehicle stack.
    /// Physics systems write to this; audio/UI/VFX only read from it.
    /// This is the backbone that prevents audio from poking directly into
    /// wheel colliders or controller internals.
    /// </summary>
    public sealed class VehicleState
    {
        // ── Speed & Motion ──
        public float SpeedKph;
        public float ForwardSpeedKph;
        public float SpeedMs;

        // ── Engine & Powertrain ──
        public float EngineRPM;
        public float NormalizedRPM;
        public float WheelDrivenRPM;
        public float FreeRevRPM;
        public float EngineLoad;
        public float ClutchEngagement;
        public float TurboSpoolAmount;
        public float LimiterAmount;

        // ── Input State ──
        public float Throttle;
        public float Brake;
        public float Steering;
        public bool Handbrake;

        // ── Transmission ──
        public int Gear;
        public int PreviousGear;
        public float ShiftProgress;
        public bool IsShifting;
        public ShiftDirection LastShiftDirection;

        // ── Ground & Stability ──
        public bool IsGrounded;
        public bool IsReversing;
        public bool IsSliding;
        public float SlipAngleDegrees;
        public float SignedSlipAngleDegrees;

        // ── Wheel Data ──
        public float AverageDrivenWheelRPM;
        public float FrontSlip;
        public float RearSlip;

        // ── Weight Transfer ──
        public float LongitudinalLoadShift;
        public float LateralLoadShift;

        // ── Drift ──
        public DriftPhase CurrentDriftPhase;
        public float DriftAngle;
        public float DriftTimer;

        /// <summary>
        /// Resets to default state. Called on spawn and respawn.
        /// </summary>
        public void Reset()
        {
            SpeedKph = 0f;
            ForwardSpeedKph = 0f;
            SpeedMs = 0f;
            EngineRPM = 0f;
            NormalizedRPM = 0f;
            WheelDrivenRPM = 0f;
            FreeRevRPM = 0f;
            EngineLoad = 0f;
            ClutchEngagement = 1f;
            TurboSpoolAmount = 0f;
            LimiterAmount = 0f;
            Throttle = 0f;
            Brake = 0f;
            Steering = 0f;
            Handbrake = false;
            Gear = 1;
            PreviousGear = 1;
            ShiftProgress = 1f;
            IsShifting = false;
            LastShiftDirection = ShiftDirection.None;
            IsGrounded = false;
            IsReversing = false;
            IsSliding = false;
            SlipAngleDegrees = 0f;
            SignedSlipAngleDegrees = 0f;
            AverageDrivenWheelRPM = 0f;
            FrontSlip = 0f;
            RearSlip = 0f;
            LongitudinalLoadShift = 0f;
            LateralLoadShift = 0f;
            CurrentDriftPhase = DriftPhase.None;
            DriftAngle = 0f;
            DriftTimer = 0f;
        }
    }

    public enum DriftPhase
    {
        None,
        Entry,
        Sustain,
        Donut,
        Recovery
    }

    public enum ShiftDirection
    {
        None,
        Up,
        Down
    }
}

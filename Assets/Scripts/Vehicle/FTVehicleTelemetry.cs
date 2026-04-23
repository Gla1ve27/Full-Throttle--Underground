using UnityEngine;

namespace FullThrottle.SacredCore.Vehicle
{
    public sealed class FTVehicleTelemetry : MonoBehaviour
    {
        public float SpeedKph { get; private set; }
        public float ForwardSpeedKph { get; private set; }
        public float EngineRPM { get; private set; }
        public float NormalizedRPM { get; private set; }
        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Steer { get; private set; }
        public float Slip01 { get; private set; }
        public int Gear { get; private set; } = 1;
        public bool Grounded { get; private set; }
        public bool IsShifting { get; private set; }
        public bool AbsActive { get; private set; }
        public bool TractionControlActive { get; private set; }
        public bool StabilityControlActive { get; private set; }
        public bool HandbrakeActive { get; private set; }
        public bool IsDrifting => Slip01 > 0.45f && SpeedKph > 35f;

        public void UpdateFromController(
            Rigidbody body,
            Transform vehicleTransform,
            float engineRPM,
            float normalizedRPM,
            int gear,
            bool shifting,
            float throttle,
            float brake,
            float steer,
            float slip01,
            bool grounded,
            bool absActive = false,
            bool tractionControlActive = false,
            bool stabilityControlActive = false,
            bool handbrakeActive = false)
        {
            Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
            SpeedKph = velocity.magnitude * 3.6f;
            ForwardSpeedKph = vehicleTransform != null ? Vector3.Dot(velocity, vehicleTransform.forward) * 3.6f : SpeedKph;
            EngineRPM = engineRPM;
            NormalizedRPM = normalizedRPM;
            Gear = gear;
            IsShifting = shifting;
            Throttle = throttle;
            Brake = brake;
            Steer = steer;
            Slip01 = Mathf.Clamp01(slip01);
            Grounded = grounded;
            AbsActive = absActive;
            TractionControlActive = tractionControlActive;
            StabilityControlActive = stabilityControlActive;
            HandbrakeActive = handbrakeActive;
        }
    }
}

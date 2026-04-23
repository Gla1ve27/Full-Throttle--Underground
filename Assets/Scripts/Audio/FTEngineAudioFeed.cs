using FullThrottle.SacredCore.Vehicle;
using UnityEngine;

namespace FullThrottle.SacredCore.Audio
{
    public sealed class FTEngineAudioFeed : MonoBehaviour
    {
        [SerializeField] private FTVehicleTelemetry telemetry;
        [SerializeField] private FTDriverInput driverInput;
        [SerializeField] private Rigidbody body;

        public float RawRPM { get; private set; } = 900f;
        public float AudioRPM { get; private set; } = 900f;
        public float RawNormalizedRPM { get; private set; }
        public float NormalizedRPM { get; private set; }
        public float Throttle { get; private set; }
        public float SpeedKph { get; private set; }
        public float Slip01 { get; private set; }
        public int Gear { get; private set; } = 1;
        public bool Grounded { get; private set; } = true;
        public bool IsShifting { get; private set; }
        public bool ShiftStartedThisFrame { get; private set; }
        public int GearChangeDirection { get; private set; }
        public bool ThrottleLiftThisFrame { get; private set; }

        private FTVehicleAudioProfile profile;
        private int lastGear = 1;
        private bool wasShifting;
        private float lastThrottle;

        public void Configure(FTVehicleAudioProfile audioProfile, FTVehicleTelemetry sourceTelemetry)
        {
            profile = audioProfile;
            telemetry = sourceTelemetry != null ? sourceTelemetry : telemetry;
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            ResolveReferences();
            ShiftStartedThisFrame = false;
            GearChangeDirection = 0;
            ThrottleLiftThisFrame = false;

            float dt = Time.deltaTime;
            float idle = profile != null ? profile.idle.referenceRPM : 900f;
            float redline = profile != null ? Mathf.Max(profile.topRedline.referenceRPM, profile.highAccel.maxRPM) : 7200f;
            float rawRPM = telemetry != null ? telemetry.EngineRPM : idle;
            float rawThrottle = telemetry != null ? telemetry.Throttle : driverInput != null ? driverInput.Throttle : 0f;
            float rawSpeed = telemetry != null ? telemetry.SpeedKph : body != null ? body.linearVelocity.magnitude * 3.6f : 0f;
            float rawSlip = telemetry != null ? telemetry.Slip01 : 0f;
            int rawGear = telemetry != null ? telemetry.Gear : 1;
            bool shifting = telemetry != null && telemetry.IsShifting;

            if (!Mathf.Approximately(rawRPM, rawRPM) || rawRPM < 1f)
            {
                rawRPM = idle;
            }

            rawRPM = Mathf.Clamp(rawRPM, idle, redline * 1.05f);
            RawRPM = rawRPM;
            RawNormalizedRPM = Mathf.InverseLerp(idle, redline, rawRPM);
            float rpmResponse = rawRPM >= AudioRPM ? profile?.response.rpmRiseResponse ?? 8.5f : profile?.response.rpmFallResponse ?? 7f;
            AudioRPM = Exp(AudioRPM <= 1f ? idle : AudioRPM, rawRPM, rpmResponse, dt);
            NormalizedRPM = Mathf.InverseLerp(idle, redline, AudioRPM);
            Throttle = Exp(Throttle, Mathf.Clamp01(rawThrottle), profile?.response.throttleResponse ?? 13f, dt);
            SpeedKph = Exp(SpeedKph, Mathf.Max(0f, rawSpeed), 10f, dt);
            Slip01 = Exp(Slip01, Mathf.Clamp01(rawSlip), rawSlip > Slip01 ? 12f : 18f, dt);
            Gear = rawGear;
            Grounded = telemetry == null || telemetry.Grounded;
            IsShifting = shifting;

            if (Gear != lastGear)
            {
                GearChangeDirection = Gear.CompareTo(lastGear);
            }

            if (shifting && !wasShifting)
            {
                ShiftStartedThisFrame = true;
            }

            if (lastThrottle - Throttle > 0.28f)
            {
                ThrottleLiftThisFrame = true;
            }

            lastGear = Gear;
            wasShifting = shifting;
            lastThrottle = Throttle;
        }

        private void ResolveReferences()
        {
            if (telemetry == null) telemetry = GetComponentInParent<FTVehicleTelemetry>();
            if (driverInput == null) driverInput = GetComponentInParent<FTDriverInput>();
            if (body == null) body = GetComponentInParent<Rigidbody>();
        }

        private static float Exp(float current, float target, float response, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-Mathf.Max(0.01f, response) * dt));
        }
    }
}

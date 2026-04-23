using System.Collections.Generic;
using UnityEngine;

namespace FullThrottle.SacredCore.Vehicle
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FTVehicleController : MonoBehaviour, IFTVehicleDefinitionReceiver
    {
        [SerializeField] private Rigidbody body;
        [SerializeField] private FTDriverInput driverInput;
        [SerializeField] private FTVehicleTelemetry telemetry;
        [SerializeField] private List<FTWheelState> wheels = new();
        [SerializeField] private FTPowertrain powertrain = new FTPowertrain();
        [SerializeField] private FTGearbox gearbox = new FTGearbox();
        [SerializeField] private FTSteeringModel steering = new FTSteeringModel();
        [SerializeField] private FTBrakeModel brakes = new FTBrakeModel();
        [SerializeField] private FTGripModel grip = new FTGripModel();
        [SerializeField] private FTDriftModel drift = new FTDriftModel();
        [Header("Steering Authority")]
        [SerializeField] private bool enableSteeringYawAssist = true;
        [SerializeField, Range(0f, 8f)] private float lowSpeedYawAuthority = 1.35f;
        [SerializeField, Range(0f, 8f)] private float highSpeedYawAuthority = 0.35f;
        [SerializeField] private float yawAssistStartSpeedKph = 4f;
        [SerializeField] private float yawAssistFullSpeedKph = 55f;
        [SerializeField] private float lowSpeedTargetYawRate = 1.75f;
        [SerializeField] private float highSpeedTargetYawRate = 0.74f;
        [SerializeField, Range(0f, 1f)] private float steeringLateralDamping = 0.022f;
        [Header("Drift Authority")]
        [SerializeField] private float driftAssistMinSpeedKph = 24f;
        [SerializeField, Range(0f, 6f)] private float handbrakeDriftYawAuthority = 3.35f;
        [SerializeField, Range(0f, 6f)] private float throttleDriftYawAuthority = 1.1f;
        [SerializeField, Range(0f, 1f)] private float throttleDriftThreshold = 0.72f;
        [SerializeField, Range(0f, 1f)] private float driftSteerThreshold = 0.24f;
        [Header("Driver Assists")]
        [SerializeField] private bool enableABS = true;
        [SerializeField] private bool enableTractionControl = true;
        [SerializeField] private bool enableStabilityControl = true;
        [SerializeField] private float stabilityMinimumSpeedKph = 45f;
        [SerializeField, Range(0f, 2f)] private float stabilityYawDamping = 0.58f;
        [SerializeField, Range(0f, 1f)] private float stabilitySlipThreshold = 0.42f;
        [SerializeField, Range(0f, 1f)] private float stabilityLateralDamping = 0.035f;

        private float steerAngle;
        private float engineRPM;

        public FTVehicleTelemetry Telemetry => telemetry;
        public FTDriverInput DriverInput => driverInput;
        public int CurrentGear => gearbox.CurrentGear;
        public bool IsShifting => gearbox.IsShifting;

        private void Awake()
        {
            ResolveReferences();
            engineRPM = powertrain.idleRPM;
            gearbox.Reset();
            Debug.Log($"[SacredCore] Vehicle controller ready on {name}. wheels={wheels.Count}");
        }

        private void FixedUpdate()
        {
            ResolveReferences();
            if (body == null || driverInput == null || wheels.Count == 0)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            float speedKph = body.linearVelocity.magnitude * 3.6f;
            float rpm01 = powertrain.Rpm01(engineRPM);
            gearbox.UpdateAutomatic(rpm01, driverInput.Throttle, driverInput.ReverseHeld, speedKph, dt);

            float throttle = gearbox.CurrentGear < 0 ? driverInput.Brake : driverInput.Throttle;
            float brakeInput = gearbox.CurrentGear < 0 ? driverInput.Throttle : driverInput.Brake;
            float torque = powertrain.EvaluateTorque(throttle, rpm01, speedKph) * Mathf.Sign(gearbox.CurrentRatio);
            float brakeTorque = brakes.EvaluateBrake(brakeInput);
            float maxSlip = 0f;
            int groundedCount = 0;
            int motorCount = CountMotorWheels();
            bool absActive = false;
            bool tractionControlActive = false;

            steerAngle = steering.Evaluate(driverInput.Steer, speedKph, steerAngle, dt);
            brakes.absEnabled = enableABS;
            grip.tractionControlEnabled = enableTractionControl;

            for (int i = 0; i < wheels.Count; i++)
            {
                FTWheelState wheel = wheels[i];
                if (wheel == null || wheel.wheel == null) continue;

                wheel.CaptureGround();
                if (wheel.grounded) groundedCount++;
                maxSlip = Mathf.Max(maxSlip, Mathf.Abs(wheel.sidewaysSlip), Mathf.Abs(wheel.forwardSlip));

                grip.Apply(wheel.wheel, speedKph);
                drift.Apply(wheel, driverInput.Handbrake, throttle, speedKph);

                if (wheel.steer)
                {
                    wheel.wheel.steerAngle = steerAngle;
                }

                float wheelMotorTorque = wheel.motor && !gearbox.IsShifting ? torque / motorCount : 0f;
                if (driverInput.Handbrake && wheel.rear)
                {
                    wheelMotorTorque = 0f;
                }

                if (wheel.motor)
                {
                    wheelMotorTorque = grip.ApplyTractionControl(wheelMotorTorque, throttle, speedKph, wheel.forwardSlip, wheel.grounded, out bool wheelTractionActive);
                    tractionControlActive |= wheelTractionActive;
                }

                float wheelBrakeTorque = wheel.brake ? brakeTorque : 0f;
                if (wheel.brake)
                {
                    wheelBrakeTorque = brakes.ApplyAbs(wheelBrakeTorque, brakeInput, speedKph, wheel.forwardSlip, wheel.grounded, out bool wheelAbsActive);
                    absActive |= wheelAbsActive;
                }

                if (driverInput.Handbrake && wheel.rear)
                {
                    wheelBrakeTorque = Mathf.Max(wheelBrakeTorque, brakes.handbrakeTorque);
                }

                wheel.wheel.motorTorque = wheelMotorTorque;
                wheel.wheel.brakeTorque = wheelBrakeTorque;
                wheel.UpdateVisual();
            }

            ApplySteeringAuthority(driverInput.Steer, throttle, speedKph, groundedCount, maxSlip, dt);
            ApplyDriftAuthority(driverInput.Steer, throttle, speedKph, groundedCount, maxSlip);
            bool stabilityActive = ApplyStabilityControl(driverInput.Steer, speedKph, groundedCount, maxSlip, driverInput.Handbrake, dt);

            float wheelRpm = EstimateDrivenWheelRpm();
            float targetRPM = Mathf.Clamp(Mathf.Abs(wheelRpm * gearbox.CurrentRatio * gearbox.finalDrive), powertrain.idleRPM, powertrain.redlineRPM);
            if (throttle > 0.05f && speedKph < 4f)
            {
                targetRPM = Mathf.Lerp(powertrain.idleRPM, powertrain.redlineRPM * 0.35f, throttle);
            }

            engineRPM = Mathf.Lerp(engineRPM, targetRPM, 1f - Mathf.Exp(-8f * dt));
            telemetry.UpdateFromController(body, transform, engineRPM, powertrain.Rpm01(engineRPM), gearbox.CurrentGear, gearbox.IsShifting, throttle, brakeInput, driverInput.Steer, Mathf.InverseLerp(0.18f, 0.85f, maxSlip), groundedCount > 0, absActive, tractionControlActive, stabilityActive, driverInput.Handbrake);
        }

        public void ApplyDefinition(FTCarDefinition definition)
        {
            if (definition == null || definition.feel == null)
            {
                return;
            }

            powertrain.maxMotorTorque = Mathf.Lerp(950f, 2300f, definition.feel.acceleration / 10f);
            powertrain.maxSpeedKph = Mathf.Lerp(165f, 330f, definition.feel.topSpeed / 10f);
            steering.lowSpeedAngle = Mathf.Lerp(34f, 42f, definition.feel.handling / 10f);
            steering.highSpeedAngle = Mathf.Lerp(16f, 22f, definition.feel.handling / 10f);
            steering.response = Mathf.Lerp(13f, 18f, definition.feel.handling / 10f);
            grip.sidewaysStiffness = Mathf.Lerp(1.04f, 1.34f, definition.feel.handling / 10f);
            lowSpeedYawAuthority = Mathf.Lerp(1.05f, 1.62f, definition.feel.handling / 10f);
            highSpeedYawAuthority = Mathf.Lerp(0.24f, 0.44f, definition.feel.handling / 10f);
            steeringLateralDamping = Mathf.Lerp(0.012f, 0.03f, definition.feel.handling / 10f);
            drift.driftBias = definition.feel.driftBias;
            ApplyDriveType(definition.driveType);
            Debug.Log($"[SacredCore] Vehicle feel applied: {definition.carId} drive={definition.driveType} accel={definition.feel.acceleration:0.0} handling={definition.feel.handling:0.0}");
        }

        private void ResolveReferences()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (driverInput == null) driverInput = GetComponent<FTDriverInput>();
            if (telemetry == null) telemetry = GetComponent<FTVehicleTelemetry>();
            if (telemetry == null) telemetry = gameObject.AddComponent<FTVehicleTelemetry>();

            if (wheels.Count == 0)
            {
                WheelCollider[] colliders = GetComponentsInChildren<WheelCollider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    WheelCollider wheel = colliders[i];
                    string lower = wheel.name.ToLowerInvariant();
                    bool rear = lower.Contains("rear") || lower.Contains("rl") || lower.Contains("rr") || lower.Contains("back");
                    wheels.Add(new FTWheelState
                    {
                        wheel = wheel,
                        steer = !rear,
                        motor = rear,
                        brake = true,
                        rear = rear
                    });
                }
            }
        }

        private int CountMotorWheels()
        {
            int count = 0;
            for (int i = 0; i < wheels.Count; i++)
            {
                if (wheels[i] != null && wheels[i].motor)
                {
                    count++;
                }
            }

            return Mathf.Max(1, count);
        }

        private float EstimateDrivenWheelRpm()
        {
            float total = 0f;
            int count = 0;
            for (int i = 0; i < wheels.Count; i++)
            {
                FTWheelState wheel = wheels[i];
                if (wheel == null || wheel.wheel == null || !wheel.motor) continue;
                total += wheel.wheel.rpm;
                count++;
            }

            return count > 0 ? total / count : 0f;
        }

        private void ApplySteeringAuthority(float steerInput, float throttle, float speedKph, int groundedCount, float maxSlip, float dt)
        {
            if (!enableSteeringYawAssist || body == null || wheels.Count == 0)
            {
                return;
            }

            float absSteer = Mathf.Abs(steerInput);
            if (absSteer < 0.02f || groundedCount <= 0)
            {
                return;
            }

            float speedBuild = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(yawAssistStartSpeedKph, yawAssistFullSpeedKph, speedKph));
            if (speedBuild <= 0.001f)
            {
                return;
            }

            float highSpeedT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(85f, 230f, speedKph));
            float groundScale = groundedCount / Mathf.Max(1f, wheels.Count);
            float slipScale = Mathf.Lerp(1f, driverInput != null && driverInput.Handbrake ? 0.85f : 0.62f, Mathf.Clamp01(maxSlip));
            float authority = Mathf.Lerp(lowSpeedYawAuthority, highSpeedYawAuthority, highSpeedT);
            float targetYawRate = steerInput * Mathf.Lerp(lowSpeedTargetYawRate, highSpeedTargetYawRate, highSpeedT);
            float yawError = targetYawRate - body.angularVelocity.y;

            body.AddTorque(Vector3.up * yawError * authority * speedBuild * groundScale * slipScale, ForceMode.Acceleration);

            bool powerDriftIntent = throttle >= throttleDriftThreshold
                && absSteer >= driftSteerThreshold
                && speedKph >= driftAssistMinSpeedKph;

            if (steeringLateralDamping > 0f && (driverInput == null || !driverInput.Handbrake) && !powerDriftIntent)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(body.linearVelocity);
                float damping = steeringLateralDamping * absSteer * speedBuild * groundScale * dt * 10f;
                localVelocity.x = Mathf.Lerp(localVelocity.x, 0f, Mathf.Clamp01(damping));
                body.linearVelocity = transform.TransformDirection(localVelocity);
            }
        }

        private void ApplyDriftAuthority(float steerInput, float throttle, float speedKph, int groundedCount, float maxSlip)
        {
            if (body == null || wheels.Count == 0 || groundedCount <= 0)
            {
                return;
            }

            float absSteer = Mathf.Abs(steerInput);
            if (absSteer < driftSteerThreshold || speedKph < driftAssistMinSpeedKph)
            {
                return;
            }

            bool handbrakeDrift = driverInput != null && driverInput.Handbrake;
            bool throttleDrift = throttle >= throttleDriftThreshold;
            if (!handbrakeDrift && !throttleDrift)
            {
                return;
            }

            float speedT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(driftAssistMinSpeedKph, 92f, speedKph));
            float groundScale = groundedCount / Mathf.Max(1f, wheels.Count);
            float throttleT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(throttleDriftThreshold, 1f, throttle));
            float slipT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.72f, maxSlip));
            float authority = handbrakeDrift
                ? handbrakeDriftYawAuthority
                : throttleDriftYawAuthority * throttleT;

            float driftPersonality = Mathf.Lerp(0.78f, 1.28f, drift.driftBias);
            float slipBuild = Mathf.Lerp(0.72f, 1.12f, slipT);
            body.AddTorque(
                Vector3.up * Mathf.Sign(steerInput) * authority * speedT * groundScale * driftPersonality * slipBuild,
                ForceMode.Acceleration);
        }

        private bool ApplyStabilityControl(float steerInput, float speedKph, int groundedCount, float maxSlip, bool handbrakeActive, float dt)
        {
            if (!enableStabilityControl || body == null || groundedCount <= 0 || handbrakeActive || speedKph < stabilityMinimumSpeedKph)
            {
                return false;
            }

            bool throttleDriftIntent = driverInput != null
                && driverInput.Throttle >= throttleDriftThreshold
                && Mathf.Abs(steerInput) >= driftSteerThreshold
                && speedKph >= driftAssistMinSpeedKph;
            if (throttleDriftIntent)
            {
                return false;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(body.linearVelocity);
            float lateralSpeed01 = Mathf.InverseLerp(1.5f, 9f, Mathf.Abs(localVelocity.x));
            float slip01 = Mathf.InverseLerp(stabilitySlipThreshold, stabilitySlipThreshold + 0.45f, maxSlip);
            float intervention = Mathf.Clamp01(Mathf.Max(lateralSpeed01, slip01));
            if (intervention <= 0.02f)
            {
                return false;
            }

            float highSpeedT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(80f, 220f, speedKph));
            float targetYawRate = steerInput * Mathf.Lerp(lowSpeedTargetYawRate * 0.74f, highSpeedTargetYawRate * 0.82f, highSpeedT);
            float yawError = body.angularVelocity.y - targetYawRate;
            body.AddTorque(Vector3.up * -yawError * stabilityYawDamping * intervention, ForceMode.Acceleration);

            float damping = stabilityLateralDamping * intervention * dt * 10f;
            localVelocity.x = Mathf.Lerp(localVelocity.x, 0f, Mathf.Clamp01(damping));
            body.linearVelocity = transform.TransformDirection(localVelocity);
            return true;
        }

        private void ApplyDriveType(string driveType)
        {
            bool frontDrive = string.Equals(driveType, "FWD", System.StringComparison.OrdinalIgnoreCase);
            bool allDrive = string.Equals(driveType, "AWD", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(driveType, "4WD", System.StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < wheels.Count; i++)
            {
                FTWheelState wheel = wheels[i];
                if (wheel == null)
                {
                    continue;
                }

                wheel.motor = allDrive || frontDrive != wheel.rear;
            }
        }
    }
}

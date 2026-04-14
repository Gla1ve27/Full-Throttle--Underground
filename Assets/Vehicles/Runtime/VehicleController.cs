// ============================================================
// VehicleController.cs
// Part 3 — Runtime Vehicle Physics
// Place at: Assets/FullThrottle/Vehicles/Runtime/VehicleController.cs
//
// Hybrid simcade model — 70% grounded, 30% NFS dramatized feel.
// Reads entirely from VehicleStatsData. No hardcoded per-car values.
// ============================================================

using UnityEngine;

namespace Underground.Vehicle
{
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleController : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Stats Reference")]
        [Tooltip("Drag the VehicleStatsData asset here, or leave null — " +
                 "PlayerCarSpawner will inject it at runtime.")]
        public VehicleStatsData stats;

        [Tooltip("Drag the VehicleDefinition asset here — provides drivetrain type and identity.")]
        public VehicleDefinition definition;

        [Header("Wheel Colliders")]
        public WheelCollider wheelFL;
        public WheelCollider wheelFR;
        public WheelCollider wheelRL;
        public WheelCollider wheelRR;

        [Header("Input (injected by input bridge)")]
        [Range(-1f, 1f)] public float throttleInput;
        [Range(-1f, 1f)] public float brakeInput;
        [Range(-1f, 1f)] public float steerInput;
        public bool isReversing;

        // ── Runtime state ────────────────────────────────────────────────────
        public float CurrentSpeedKph { get; private set; }
        public float CurrentRPM      { get; private set; }
        public int   CurrentGear     { get; private set; } = 1;
        public bool  IsGrounded      { get; private set; }
        public float SlipAngleFront  { get; private set; }
        public float SlipAngleRear   { get; private set; }

        // Expose for VehicleLightController
        public bool IsBraking  => brakeInput > 0.05f;
        public bool IsReverse  => isReversing;

        // ── Private ──────────────────────────────────────────────────────────
        private Rigidbody _rb;
        private float _currentGripFront;
        private float _currentGripRear;

        // Weight transfer working values
        private float _weightTransferLongitudinal; // positive = nose heavy under brake
        private float _weightTransferLateral;       // positive = right heavy

        // Gear timing
        private float _gearTimer;
        private const float GearChangeDelay = 0.3f;

        // ── Unity ────────────────────────────────────────────────────────────
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            ApplyChassisSetup();
        }

        private void FixedUpdate()
        {
            if (stats == null) return;

            UpdateGroundedState();
            UpdateSpeedAndRPM();
            UpdateGear();
            UpdateWeightTransfer();
            UpdateDynamicGrip();
            ApplySteering();
            ApplyDrivetrainForce();
            ApplyBraking();
            ApplyAssists();
            ApplyWheelColliderFriction();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Inject stats and definition from the spawner after prefab instantiation.</summary>
        public void InjectStats(VehicleStatsData injectedStats, VehicleDefinition injectedDefinition = null)
        {
            stats = injectedStats;
            if (injectedDefinition != null) definition = injectedDefinition;
            ApplyChassisSetup();
        }

        // ── Setup ────────────────────────────────────────────────────────────

        private void ApplyChassisSetup()
        {
            if (stats == null || _rb == null) return;

            _rb.mass           = stats.defaultMass;    // was: stats.mass
            _rb.linearDamping  = stats.linearDamping;  // was: hardcoded 0.02f
            _rb.angularDamping = stats.angularDamping; // was: hardcoded 0.05f

            // Center of mass
            _rb.centerOfMass = stats.centerOfMassOffset;

            // WheelCollider spring/damper from stats
            SetWheelSpring(wheelFL); SetWheelSpring(wheelFR);
            SetWheelSpring(wheelRL); SetWheelSpring(wheelRR);
        }

        private void SetWheelSpring(WheelCollider wc)
        {
            if (wc == null || stats == null) return;
            JointSpring sp = wc.suspensionSpring;
            sp.spring        = stats.spring;   // was: stats.suspensionStiffness
            sp.damper        = stats.damper;   // was: stats.suspensionDamping
            sp.targetPosition = 0.5f;
            wc.suspensionSpring = sp;
        }

        // ── Per-frame ────────────────────────────────────────────────────────

        private void UpdateGroundedState()
        {
            IsGrounded = (wheelFL != null && wheelFL.isGrounded) ||
                         (wheelFR != null && wheelFR.isGrounded) ||
                         (wheelRL != null && wheelRL.isGrounded) ||
                         (wheelRR != null && wheelRR.isGrounded);
        }

        private void UpdateSpeedAndRPM()
        {
            CurrentSpeedKph = _rb.linearVelocity.magnitude * 3.6f;

            // Derive RPM from wheel speed and current gear
            float wheelRPM = 0f;
            int grounded = 0;
            foreach (var wc in new[] { wheelRL, wheelRR })
            {
                if (wc != null && wc.isGrounded) { wheelRPM += wc.rpm; grounded++; }
            }
            if (grounded > 0) wheelRPM /= grounded;

            float[] ratios = stats.gearRatios;
            float gearRatio = (CurrentGear - 1 < ratios.Length) ? ratios[CurrentGear - 1] : 1f;
            CurrentRPM = Mathf.Abs(wheelRPM) * gearRatio * stats.finalDriveRatio; // was: stats.finalDrive
            CurrentRPM = Mathf.Clamp(CurrentRPM, stats.idleRPM, stats.maxRPM);    // was: stats.redlineRPM
        }

        private void UpdateGear()
        {
            _gearTimer += Time.fixedDeltaTime;
            if (_gearTimer < GearChangeDelay) return;

            int maxGear = stats.gearRatios.Length;
            float normalizedRPM = Mathf.InverseLerp(stats.idleRPM, stats.maxRPM, CurrentRPM); // was: stats.redlineRPM

            if (normalizedRPM > 0.85f && CurrentGear < maxGear)
            {
                CurrentGear++;
                _gearTimer = 0f;
            }
            else if (normalizedRPM < 0.25f && CurrentGear > 1)
            {
                CurrentGear--;
                _gearTimer = 0f;
            }
        }

        private void UpdateWeightTransfer()
        {
            float speedFactor = Mathf.Clamp01(CurrentSpeedKph / 120f);

            // Longitudinal: brake pitches nose down, throttle pitches tail down
            float longInput = brakeInput - throttleInput;
            _weightTransferLongitudinal = Mathf.Lerp(
                _weightTransferLongitudinal,
                longInput * speedFactor * 0.3f,
                Time.fixedDeltaTime * 4f);

            // Lateral: steer shifts load to outside wheels
            _weightTransferLateral = Mathf.Lerp(
                _weightTransferLateral,
                -steerInput * speedFactor * 0.2f,
                Time.fixedDeltaTime * 4f);
        }

        private void UpdateDynamicGrip()
        {
            if (stats == null) return;

            float speedFactor    = Mathf.Clamp01(CurrentSpeedKph / 200f);
            float throttleFactor = Mathf.Abs(throttleInput);
            float brakeFactor    = Mathf.Abs(brakeInput);

            // Front grip reduces under heavy braking (nose load) and high speed
            _currentGripFront = stats.frontGrip
                * Mathf.Lerp(1f, 1.1f, brakeFactor)           // extra front load under braking
                * Mathf.Lerp(1f, 0.85f, speedFactor);          // slight reduction at high speed

            // Rear grip reduces under heavy throttle (wheelspin risk) especially on RWD
            DrivetrainType dt = GetDrivetrain();
            float drivetrainRearPenalty = dt == DrivetrainType.RWD ? 0.9f : 1f;
            _currentGripRear = stats.rearGrip
                * Mathf.Lerp(1f, drivetrainRearPenalty, throttleFactor)
                * Mathf.Lerp(1f, 0.88f, speedFactor);

            // AWD gives better traction across the board
            if (dt == DrivetrainType.AWD)
            {
                _currentGripFront *= 1.05f;
                _currentGripRear  *= 1.05f;
            }

            // FWD has understeer tendency — reduce front cornering grip under throttle
            if (dt == DrivetrainType.FWD)
            {
                _currentGripFront *= Mathf.Lerp(1f, 0.88f, throttleFactor);
            }
        }

        private void ApplySteering()
        {
            if (wheelFL == null || wheelFR == null || stats == null) return;

            float speedKph = CurrentSpeedKph;

            // Speed-sensitive steering: full angle at low speed, reduced at highway speed
            float maxAngle = Mathf.Lerp(38f, 18f, Mathf.Clamp01(speedKph / 140f));

            // Steering response from stats
            float targetAngle = steerInput * maxAngle * stats.steeringResponse;
            float currentAngle = wheelFL.steerAngle;
            float smoothAngle = Mathf.Lerp(currentAngle, targetAngle, Time.fixedDeltaTime * 8f);

            wheelFL.steerAngle = smoothAngle;
            wheelFR.steerAngle = smoothAngle;
        }

        private DrivetrainType GetDrivetrain()
        {
            // drivetrain is authored on VehicleDefinition, not VehicleStatsData
            if (definition != null) return definition.drivetrain;
            return DrivetrainType.RWD; // safe fallback
        }

        private void ApplyDrivetrainForce()
        {
            if (stats == null || !IsGrounded) return;

            float normalizedRPM  = Mathf.InverseLerp(stats.idleRPM, stats.maxRPM, CurrentRPM); // was: stats.redlineRPM
            float torqueFromCurve = stats.torqueCurve.Evaluate(normalizedRPM);
            float motorTorque = throttleInput * stats.maxMotorTorque * torqueFromCurve * stats.finalDriveRatio; // was: stats.torque / stats.finalDrive

            switch (GetDrivetrain())
            {
                case DrivetrainType.RWD:
                    SetMotorTorque(0f, 0f, motorTorque * 0.5f, motorTorque * 0.5f);
                    break;
                case DrivetrainType.FWD:
                    SetMotorTorque(motorTorque * 0.5f, motorTorque * 0.5f, 0f, 0f);
                    break;
                case DrivetrainType.AWD:
                    // Slight rear bias for planted feel
                    SetMotorTorque(
                        motorTorque * 0.25f,
                        motorTorque * 0.25f,
                        motorTorque * 0.35f,
                        motorTorque * 0.35f);
                    break;
            }
        }

        private void SetMotorTorque(float fl, float fr, float rl, float rr)
        {
            if (wheelFL != null) wheelFL.motorTorque = fl;
            if (wheelFR != null) wheelFR.motorTorque = fr;
            if (wheelRL != null) wheelRL.motorTorque = rl;
            if (wheelRR != null) wheelRR.motorTorque = rr;
        }

        private void ApplyBraking()
        {
            if (stats == null) return;
            float brakeTorque = brakeInput * 3500f * stats.brakeGrip;
            if (wheelFL != null) wheelFL.brakeTorque = brakeTorque;
            if (wheelFR != null) wheelFR.brakeTorque = brakeTorque;
            if (wheelRL != null) wheelRL.brakeTorque = brakeTorque;
            if (wheelRR != null) wheelRR.brakeTorque = brakeTorque;
        }

        private void ApplyAssists()
        {
            if (stats == null || !IsGrounded) return;

            Vector3 vel = _rb.linearVelocity;
            if (vel.sqrMagnitude < 0.5f) return;

            // Yaw stabilization — prevent uncontrolled spin
            float yawRate  = _rb.angularVelocity.y;
            float yawLimit = Mathf.Lerp(3.5f, 1.5f, stats.yawStability);
            if (Mathf.Abs(yawRate) > yawLimit)
            {
                Vector3 av = _rb.angularVelocity;
                av.y = Mathf.MoveTowards(av.y, 0f, stats.yawStability * Time.fixedDeltaTime * 4f);
                _rb.angularVelocity = av;
            }

            // Countersteer assist — nudge car back when sliding
            float sideslip = Vector3.Dot(vel.normalized, transform.right);
            if (Mathf.Abs(sideslip) > 0.15f && stats.counterSteerAssist > 0f)
            {
                float correction = -sideslip * stats.counterSteerAssist * 800f;
                _rb.AddForce(transform.right * correction, ForceMode.Force);
            }

            // High-speed stability — reduce lateral drift at speed
            float speedFactor = Mathf.Clamp01(CurrentSpeedKph / 180f);
            float lateralVel  = Vector3.Dot(vel, transform.right);
            _rb.AddForce(
                -transform.right * lateralVel * stats.highSpeedStability * speedFactor * _rb.mass * 0.3f,
                ForceMode.Force);
        }

        private void ApplyWheelColliderFriction()
        {
            if (stats == null) return;

            SetWheelFriction(wheelFL, _currentGripFront, isFront: true);
            SetWheelFriction(wheelFR, _currentGripFront, isFront: true);
            SetWheelFriction(wheelRL, _currentGripRear,  isFront: false);
            SetWheelFriction(wheelRR, _currentGripRear,  isFront: false);
        }

        private void SetWheelFriction(WheelCollider wc, float grip, bool isFront)
        {
            if (wc == null) return;

            WheelFrictionCurve sideways = wc.sidewaysFriction;
            sideways.extremumSlip    = stats.slipAngle * Mathf.Deg2Rad;
            sideways.extremumValue   = grip;
            sideways.asymptoteSlip   = stats.slipAngle * Mathf.Deg2Rad * 2f;
            sideways.asymptoteValue  = grip * 0.75f;
            sideways.stiffness       = grip;
            wc.sidewaysFriction = sideways;

            WheelFrictionCurve forward = wc.forwardFriction;
            forward.extremumValue  = stats.traction;
            forward.asymptoteValue = stats.traction * 0.8f;
            forward.stiffness      = stats.traction;
            wc.forwardFriction = forward;
        }
    }
}

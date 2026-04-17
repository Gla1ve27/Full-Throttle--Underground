using UnityEngine;
using Underground.Progression;
using Underground.World;

namespace Underground.Vehicle
{
    /// <summary>
    /// Full Throttle hybrid driving model — 70% grounded simcade, 30% NFS dramatized street feel.
    /// 
    /// Part 3 systems:
    ///  1. Torque-curve-driven acceleration via gearing
    ///  2. Drivetrain identity (FWD / RWD / AWD)
    ///  3. Lightweight weight transfer (longitudinal + lateral)
    ///  4. Dynamic grip model (speed, steering, slip, throttle, braking)
    ///  5. Assist layer (drift forgiveness, countersteer, yaw stability, high-speed stability)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleDynamicsController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Serialized Fields
        // ─────────────────────────────────────────────────────────────────────

        [Header("Data")]
        [SerializeField] private VehicleStatsData baseStats;
        [SerializeField] private RuntimeVehicleStats runtimeStats = new RuntimeVehicleStats();
        [SerializeField] private UpgradeDefinition[] startupUpgrades;

        [Header("References")]
        [SerializeField] private InputReader input;
        [SerializeField] private EngineModel engineModel;
        [SerializeField] private GearboxSystem gearbox;
        [SerializeField] private Transform centerOfMassReference;
        [SerializeField] private WheelSet[] wheels;

        [Header("Braking")]
        [SerializeField] private float maxHandbrakeTorque = 6500f;
        [SerializeField, Range(0f, 1f)] private float poweredHandbrakeTorqueScale = 0.28f;
        [SerializeField] private float reverseEngageSpeedKph = 3f;
        [SerializeField] private float reverseReleaseSpeedKph = 1.5f;
        [SerializeField] private float reverseHoldToEngageDelay = 0.12f;
        [SerializeField] private float aerodynamicDragCoefficient = 0.36f;

        [Header("Low-Speed Stabilization")]
        [SerializeField] private float lowSpeedAssistEntryKph = 12f;
        [SerializeField] private float lowSpeedStopSnapKph = 0.85f;
        [SerializeField] private float lowSpeedCoastDeceleration = 4.5f;
        [SerializeField] private float lowSpeedLateralDamping = 3.5f;
        [SerializeField] private float lowSpeedAngularDamping = 2.4f;

        [Header("Feel")]
        [SerializeField] private float motorTorqueResponse = 2600f;
        [SerializeField] private float baseLoadGripBias = 0.92f;
        [SerializeField] private float maxLoadGripBias = 1.08f;
        [SerializeField] private float slipGripPenalty = 0.18f;
        [SerializeField] private float slideSlipAngleThreshold = 15f;
        [SerializeField] private float fullSlideSlipAngle = 28f;
        [SerializeField] private float slideSideFrictionMultiplier = 0.7f;
        [SerializeField] private float slideForwardFrictionMultiplier = 0.96f;

        [Header("UG2 Arcade Handling")]
        [SerializeField] private float lowSpeedSteerLock = 34f;
        [SerializeField] private float highSpeedSteerLock = 7.5f;
        [SerializeField] private float steeringAuthorityHighSpeedFloor = 0.38f;
        [SerializeField] private float stableGearboxRpmResponse = 14f;
        [SerializeField, Range(0f, 1f)] private float launchWheelSlipRpmInfluence = 0.01f;
        [SerializeField, Range(0f, 1f)] private float wheelSlipRpmInfluence = 0.03f;
        [SerializeField] private float fullWheelRpmInfluenceSpeedKph = 65f;
        [SerializeField, Range(0f, 1f)] private float shiftTorqueCut = 0.38f;
        [SerializeField] private float handbrakeDriftWindow = 0.45f;
        [SerializeField] private float driftSustainWindow = 0.35f;
        [SerializeField] private float rearGripDriftMultiplier = 0.38f;
        [SerializeField] private float driftYawAssist = 0.82f;
        [SerializeField] private float driftSpeedPreservation = 0.985f;
        [SerializeField, Range(0f, 1f)] private float handbrakeSlideRearGripMultiplier = 0.24f;
        [SerializeField, Range(0f, 1f)] private float powerSlideRearGripMultiplier = 0.55f;
        [SerializeField, Range(0f, 1f)] private float donutRearGripMultiplier = 0.18f;
        [SerializeField] private float donutYawAssist = 3.6f;
        [SerializeField] private float handbrakeRevTargetRpm01 = 0.72f;
        [SerializeField] private float wallGlanceSpeedRetention = 0.90f;
        [SerializeField] private float wallBounceImpulse = 2.25f;

        [Header("Weight Transfer")]
        [SerializeField] private float longitudinalTransferRate = 0.12f;
        [SerializeField] private float lateralTransferRate = 0.10f;
        [SerializeField] private float transferSmoothSpeed = 5f;

        // ─────────────────────────────────────────────────────────────────────
        //  Public Properties
        // ─────────────────────────────────────────────────────────────────────

        public Rigidbody Rigidbody { get; private set; }
        public VehicleStatsData BaseStats => baseStats;
        public RuntimeVehicleStats RuntimeStats => runtimeStats;
        public WheelSet[] WheelSets => wheels;
        public float SpeedKph { get; private set; }
        public float ForwardSpeedKph { get; private set; }
        public float SlipAngleDegrees { get; private set; }
        public float SignedSlipAngleDegrees { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsReversing { get; private set; }
        public bool IsSliding { get; private set; }

        private float smoothedGearboxWheelRpm;
        private float handbrakeRevRpm01;
        private float reverseHoldTimer;
        private float driftInitiationTimer;
        private float driftSustainTimer;
        private bool wasHandbrakeHeld;

        /// <summary>Current longitudinal weight transfer (−1 = full front, +1 = full rear).</summary>
        public float LongitudinalLoadShift { get; private set; }

        /// <summary>Current lateral weight transfer (−1 = full left, +1 = full right).</summary>
        public float LateralLoadShift { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody>();

            if (input == null)
            {
                input = GetComponent<InputReader>();
            }

            if (engineModel == null)
            {
                engineModel = GetComponent<EngineModel>();
            }

            if (gearbox == null)
            {
                gearbox = GetComponent<GearboxSystem>();
            }

            if (CompareTag("Player") && GetComponent<PlayerCarAppearanceController>() == null)
            {
                gameObject.AddComponent<PlayerCarAppearanceController>();
            }

            if (GetComponent<PlayerCarAppearanceController>() == null && GetComponent<VehicleReflectionRuntimeController>() == null)
            {
                gameObject.AddComponent<VehicleReflectionRuntimeController>();
            }

            Initialize(baseStats, startupUpgrades);
        }

        private void FixedUpdate()
        {
            if (Rigidbody == null || input == null || gearbox == null || wheels == null || wheels.Length == 0)
            {
                return;
            }

            ApplyCenterOfMass();
            UpdateTelemetry();
            UpdateWeightTransfer();
            UpdateReverseState();
            UpdateDriftStateTimers();
            ApplySteering();
            ApplyDrive();
            ApplyBraking();
            ApplyAntiRoll();
            ApplyDynamicTireGrip();
            ApplyDownforce();
            ApplyAerodynamicDrag();
            ApplyLateralGripAssist();
            ApplyDrivetrainBehavior();
            ApplyCounterSteerAssist();
            ApplyYawStability();
            ApplyHighSpeedStability();
            ApplyDriftAssist();
            ApplyLowSpeedStopAssist();
            UpdateTelemetry();
        }

        private void LateUpdate()
        {
            SyncWheelVisuals();
        }

        private void OnCollisionStay(Collision collision)
        {
            ApplyArcadeWallGlance(collision);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public void Initialize(VehicleStatsData source, params UpgradeDefinition[] upgrades)
        {
            baseStats = source;
            if (runtimeStats == null)
            {
                runtimeStats = new RuntimeVehicleStats();
            }

            if (baseStats != null)
            {
                runtimeStats.LoadFromBase(baseStats);
            }

            if (upgrades != null)
            {
                for (int i = 0; i < upgrades.Length; i++)
                {
                    runtimeStats.ApplyUpgrade(upgrades[i]);
                }
            }

            ApplyStatsToVehicle();
        }

        public RuntimeVehicleStats GetRuntimeStats()
        {
            return runtimeStats;
        }

        public void SetUseManualTransmission(bool enabled)
        {
            gearbox?.SetUseManualTransmission(enabled);
        }

        public void SetWheelVisualByColliderName(string colliderName, Transform visual)
        {
            if (string.IsNullOrEmpty(colliderName) || wheels == null)
            {
                return;
            }

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet wheel = wheels[i];
                if (wheel == null || wheel.collider == null || wheel.collider.name != colliderName)
                {
                    continue;
                }

                wheel.mesh = visual;
                return;
            }
        }

        public void ApplyUpgrade(UpgradeDefinition definition)
        {
            runtimeStats.ApplyUpgrade(definition);
            ApplyStatsToVehicle();
        }

        public void ApplyStatsToVehicle()
        {
            if (Rigidbody == null || baseStats == null || runtimeStats == null)
            {
                return;
            }

            Rigidbody.mass = runtimeStats.Mass;
            Rigidbody.linearDamping = 0.12f;
            Rigidbody.angularDamping = 1.5f;
            Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            Rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            Rigidbody.centerOfMass = centerOfMassReference != null
                ? transform.InverseTransformPoint(centerOfMassReference.position)
                : runtimeStats.CenterOfMassOffset;

            if (wheels == null)
            {
                return;
            }

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet wheel = wheels[i];
                if (wheel == null || wheel.collider == null)
                {
                    continue;
                }

                JointSpring spring = wheel.collider.suspensionSpring;
                spring.spring = runtimeStats.Spring;
                spring.damper = runtimeStats.Damper;
                spring.targetPosition = 0.45f;
                wheel.collider.suspensionSpring = spring;
                wheel.collider.suspensionDistance = runtimeStats.SuspensionDistance;

                WheelFrictionCurve forward = wheel.collider.forwardFriction;
                forward.stiffness = runtimeStats.ForwardStiffness;
                wheel.collider.forwardFriction = forward;

                WheelFrictionCurve sideways = wheel.collider.sidewaysFriction;
                sideways.stiffness = runtimeStats.SidewaysStiffness;
                wheel.collider.sidewaysFriction = sideways;
            }
        }

        public void ResetVehicle(Transform respawnPoint)
        {
            if (respawnPoint == null)
            {
                return;
            }

            Rigidbody.linearVelocity = Vector3.zero;
            Rigidbody.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(respawnPoint.position, respawnPoint.rotation);
            Rigidbody.position += Vector3.up * runtimeStats.ResetLift;
            IsReversing = false;
            reverseHoldTimer = 0f;
            LongitudinalLoadShift = 0f;
            LateralLoadShift = 0f;
            smoothedGearboxWheelRpm = 0f;
            handbrakeRevRpm01 = 0f;
            driftInitiationTimer = 0f;
            driftSustainTimer = 0f;
            wasHandbrakeHeld = false;
            gearbox?.ResetToFirstGear();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Core Physics — Center of Mass
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyCenterOfMass()
        {
            if (centerOfMassReference != null)
            {
                Rigidbody.centerOfMass = transform.InverseTransformPoint(centerOfMassReference.position);
            }
            else if (runtimeStats != null)
            {
                Rigidbody.centerOfMass = runtimeStats.CenterOfMassOffset;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Telemetry
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateTelemetry()
        {
            SpeedKph = Rigidbody.linearVelocity.magnitude * 3.6f;
            ForwardSpeedKph = Vector3.Dot(Rigidbody.linearVelocity, transform.forward) * 3.6f;
            IsGrounded = false;
            UpdateSlipAngleState();

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] != null && wheels[i].collider != null && wheels[i].collider.isGrounded)
                {
                    IsGrounded = true;
                    return;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PART 3 SYSTEM 3: Weight Transfer (Simcade)
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateWeightTransfer()
        {
            if (runtimeStats == null)
            {
                return;
            }

            // Longitudinal: braking shifts weight forward (negative), throttle shifts rear (positive)
            float longitudinalAccel = Vector3.Dot(Rigidbody.linearVelocity - _previousVelocity, transform.forward)
                / Mathf.Max(0.001f, Time.fixedDeltaTime);
            float targetLongShift = Mathf.Clamp(-longitudinalAccel * longitudinalTransferRate / 9.81f, -1f, 1f);

            // Lateral: turning shifts weight to outside (positive = right load)
            float lateralAccel = Vector3.Dot(Rigidbody.linearVelocity - _previousVelocity, transform.right)
                / Mathf.Max(0.001f, Time.fixedDeltaTime);
            float targetLatShift = Mathf.Clamp(lateralAccel * lateralTransferRate / 9.81f, -1f, 1f);

            LongitudinalLoadShift = Mathf.MoveTowards(LongitudinalLoadShift, targetLongShift, transferSmoothSpeed * Time.fixedDeltaTime);
            LateralLoadShift = Mathf.MoveTowards(LateralLoadShift, targetLatShift, transferSmoothSpeed * Time.fixedDeltaTime);

            _previousVelocity = Rigidbody.linearVelocity;
        }

        private Vector3 _previousVelocity;

        /// <summary>
        /// Returns a grip multiplier for a specific wheel position based on weight transfer.
        /// Front wheels gain grip under braking, rear under acceleration.
        /// </summary>
        private float GetWeightTransferGripMultiplier(bool isFrontAxle, bool isLeftSide)
        {
            float longMul = 1f;
            float latMul = 1f;

            // Longitudinal: braking (shift < 0) loads front, accel (shift > 0) loads rear
            if (isFrontAxle)
            {
                longMul = 1f + Mathf.Clamp01(-LongitudinalLoadShift) * 0.15f   // front gains under braking
                             - Mathf.Clamp01(LongitudinalLoadShift) * 0.10f;   // front loses under accel
            }
            else
            {
                longMul = 1f + Mathf.Clamp01(LongitudinalLoadShift) * 0.15f    // rear gains under accel
                             - Mathf.Clamp01(-LongitudinalLoadShift) * 0.10f;  // rear loses under braking
            }

            // Lateral: outside wheels gain grip, inside wheels lose
            float latSign = isLeftSide ? -1f : 1f;
            float outsideLoad = Mathf.Clamp01(LateralLoadShift * latSign);
            float insideUnload = Mathf.Clamp01(-LateralLoadShift * latSign);
            latMul = 1f + outsideLoad * 0.08f - insideUnload * 0.06f;

            return Mathf.Max(0.5f, longMul * latMul);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Steering
        // ─────────────────────────────────────────────────────────────────────

        private void ApplySteering()
        {
            float steerResponse = runtimeStats != null ? runtimeStats.SteeringResponse : 72f;
            float absForwardSpeed = Mathf.Abs(ForwardSpeedKph);
            float speedFactor = Mathf.InverseLerp(0f, Mathf.Max(1f, runtimeStats.MaxSpeedKph), absForwardSpeed);
            float steeringLockT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(25f, 205f, absForwardSpeed));
            float dynamicSteerLock = Mathf.Lerp(lowSpeedSteerLock, highSpeedSteerLock, steeringLockT);
            float highSpeedFloor = Mathf.Max(runtimeStats.HighSpeedSteerReduction, steeringAuthorityHighSpeedFloor);
            float steerReduction = Mathf.Lerp(1f, highSpeedFloor, speedFactor);
            float slideBonus = IsSliding ? Mathf.InverseLerp(slideSlipAngleThreshold, fullSlideSlipAngle, SlipAngleDegrees) * 2.5f : 0f;
            float targetSteerAngle = input.Steering * Mathf.Min(dynamicSteerLock + slideBonus, runtimeStats.MaxSteerAngle * steerReduction);

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet wheel = wheels[i];
                if (wheel == null || wheel.collider == null || !wheel.steer)
                {
                    continue;
                }

                wheel.collider.steerAngle = Mathf.MoveTowards(
                    wheel.collider.steerAngle,
                    targetSteerAngle,
                    steerResponse * Time.fixedDeltaTime);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Reverse State
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateReverseState()
        {
            if (input == null)
            {
                return;
            }

            if (input.Throttle > 0.05f)
            {
                IsReversing = false;
                reverseHoldTimer = 0f;
                return;
            }

            if (!IsReversing)
            {
                if (input.ReverseHeld && Mathf.Abs(ForwardSpeedKph) <= reverseEngageSpeedKph)
                {
                    reverseHoldTimer += Time.fixedDeltaTime;
                    if (reverseHoldTimer >= reverseHoldToEngageDelay)
                    {
                        IsReversing = true;
                        gearbox?.ResetToFirstGear();
                    }
                }
                else
                {
                    reverseHoldTimer = 0f;
                }

                return;
            }

            if (ForwardSpeedKph > reverseEngageSpeedKph)
            {
                IsReversing = false;
                reverseHoldTimer = 0f;
                return;
            }

            if (!input.ReverseHeld && Mathf.Abs(ForwardSpeedKph) <= reverseReleaseSpeedKph)
            {
                IsReversing = false;
                reverseHoldTimer = 0f;
            }
        }

        private void UpdateDriftStateTimers()
        {
            bool handbrakePressed = input.Handbrake && !wasHandbrakeHeld;
            if (handbrakePressed && SpeedKph > 8f)
            {
                driftInitiationTimer = handbrakeDriftWindow;
            }

            bool driftSustainInput = input.Handbrake && SpeedKph > 8f
                || input.Throttle > 0.35f
                    && Mathf.Abs(input.Steering) > 0.18f
                    && SpeedKph > 12f
                    && (driftInitiationTimer > 0f || IsSliding);

            if (driftSustainInput)
            {
                driftSustainTimer = driftSustainWindow;
            }

            driftInitiationTimer = Mathf.Max(0f, driftInitiationTimer - Time.fixedDeltaTime);
            driftSustainTimer = Mathf.Max(0f, driftSustainTimer - Time.fixedDeltaTime);
            wasHandbrakeHeld = input.Handbrake;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PART 3 SYSTEM 1: Torque-Curve-Driven Acceleration
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyDrive()
        {
            int drivenCount = 0;
            float averageDrivenWheelRpm = 0f;

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet wheel = wheels[i];
                if (wheel == null || wheel.collider == null || !wheel.drive)
                {
                    continue;
                }

                averageDrivenWheelRpm += wheel.collider.rpm;
                drivenCount++;
            }

            if (drivenCount == 0)
            {
                return;
            }

            averageDrivenWheelRpm /= drivenCount;
            float gearboxWheelRpm = GetStableGearboxWheelRpm(averageDrivenWheelRpm);
            gearboxWheelRpm = ApplyHandbrakeRevToGearboxWheelRpm(gearboxWheelRpm);
            if (IsReversing)
            {
                gearbox.UpdateRpmOnly(gearboxWheelRpm, baseStats, GetReverseDriveRatio());
            }
            else if (gearbox.UseManualTransmission)
            {
                gearbox.UpdateRpmOnly(gearboxWheelRpm, baseStats, gearbox.GetCurrentDriveRatio(baseStats));
                if (input.ConsumeUpshiftRequest())
                {
                    gearbox.TryShiftUp(baseStats, gearboxWheelRpm);
                }
                else if (input.ConsumeDownshiftRequest())
                {
                    gearbox.TryShiftDown(baseStats, gearboxWheelRpm);
                }
            }
            else
            {
                gearbox.UpdateAutomatic(gearboxWheelRpm, baseStats, input.Throttle, SpeedKph);
            }

            float driveRatio = IsReversing ? GetReverseDriveRatio() : gearbox.GetCurrentDriveRatio(baseStats);

            // Sample torque curve — prefer stats-embedded curve, fall back to EngineModel component
            float normalizedRpm = Mathf.InverseLerp(runtimeStats.IdleRPM, runtimeStats.MaxRPM, gearbox.CurrentRPM);
            float torqueCurveSample = EvaluateTorqueCurve(normalizedRpm);

            float driveInput = IsReversing && input.ReverseHeld ? -input.Brake : input.Throttle;
            if (!IsReversing && gearbox != null && gearbox.IsShifting)
            {
                driveInput *= Mathf.Lerp(shiftTorqueCut, 1f, gearbox.ShiftBlend);
            }

            float torquePerWheel = driveInput * runtimeStats.MaxMotorTorque * torqueCurveSample * driveRatio / drivenCount;
            bool canApplyTorque = driveInput >= 0f
                ? SpeedKph < runtimeStats.MaxSpeedKph || driveInput < 0.05f
                : ForwardSpeedKph > -runtimeStats.MaxSpeedKph || Mathf.Abs(driveInput) < 0.05f;

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet wheel = wheels[i];
                if (wheel == null || wheel.collider == null)
                {
                    continue;
                }

                float targetTorque = wheel.drive && canApplyTorque ? torquePerWheel : 0f;
                wheel.collider.motorTorque = Mathf.MoveTowards(
                    wheel.collider.motorTorque,
                    targetTorque,
                    motorTorqueResponse * Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// Evaluates torque curve from RuntimeVehicleStats.TorqueCurve if available,
        /// otherwise falls back to the EngineModel component.
        /// </summary>
        private float EvaluateTorqueCurve(float normalizedRpm)
        {
            if (runtimeStats?.TorqueCurve != null && runtimeStats.TorqueCurve.length > 0)
            {
                return runtimeStats.TorqueCurve.Evaluate(Mathf.Clamp01(normalizedRpm));
            }

            if (engineModel != null)
            {
                return engineModel.EvaluateNormalizedTorque(normalizedRpm);
            }

            // Last resort: flat curve
            return 0.7f;
        }

        private float GetStableGearboxWheelRpm(float colliderWheelRpm)
        {
            float averageRadius = 0f;
            int radiusCount = 0;
            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet wheel = wheels[i];
                if (wheel == null || wheel.collider == null || !wheel.drive)
                {
                    continue;
                }

                averageRadius += Mathf.Max(0.01f, wheel.collider.radius);
                radiusCount++;
            }

            if (radiusCount == 0)
            {
                return colliderWheelRpm;
            }

            averageRadius /= radiusCount;
            float circumference = Mathf.Max(0.01f, Mathf.PI * 2f * averageRadius);
            float speedBasedRpm = (ForwardSpeedKph / 3.6f) / circumference * 60f;
            float speedInfluence = Mathf.InverseLerp(12f, fullWheelRpmInfluenceSpeedKph, Mathf.Abs(ForwardSpeedKph));
            float slipInfluence = Mathf.Lerp(launchWheelSlipRpmInfluence, wheelSlipRpmInfluence, speedInfluence);
            float targetWheelRpm = Mathf.Lerp(speedBasedRpm, colliderWheelRpm, slipInfluence);
            float response = 1f - Mathf.Exp(-Mathf.Max(0.01f, stableGearboxRpmResponse) * Time.fixedDeltaTime);
            smoothedGearboxWheelRpm = Mathf.Lerp(smoothedGearboxWheelRpm, targetWheelRpm, response);
            return smoothedGearboxWheelRpm;
        }

        private float ApplyHandbrakeRevToGearboxWheelRpm(float stableWheelRpm)
        {
            if (input == null || runtimeStats == null || gearbox == null || IsReversing)
            {
                handbrakeRevRpm01 = Mathf.MoveTowards(handbrakeRevRpm01, 0f, Time.fixedDeltaTime * 5f);
                return stableWheelRpm;
            }

            bool canRevAgainstHandbrake = input.Handbrake && input.Throttle > 0.05f;
            float targetRpm01 = canRevAgainstHandbrake
                ? Mathf.Lerp(0.22f, handbrakeRevTargetRpm01, input.Throttle)
                : 0f;
            float response = canRevAgainstHandbrake ? 5.5f : 8f;
            handbrakeRevRpm01 = Mathf.MoveTowards(handbrakeRevRpm01, targetRpm01, response * Time.fixedDeltaTime);

            if (handbrakeRevRpm01 <= 0f)
            {
                return stableWheelRpm;
            }

            float targetEngineRpm = Mathf.Lerp(runtimeStats.IdleRPM, runtimeStats.MaxRPM, handbrakeRevRpm01);
            float driveRatio = Mathf.Max(0.01f, Mathf.Abs(gearbox.GetCurrentDriveRatio(baseStats)));
            float virtualWheelRpm = targetEngineRpm / driveRatio;
            return Mathf.Max(stableWheelRpm, virtualWheelRpm);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Braking
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyBraking()
        {
            float footBrakeTorque = IsReversing && input.ReverseHeld ? 0f : input.Brake * runtimeStats.MaxBrakeTorque;
            float poweredRelease = Mathf.Lerp(1f, poweredHandbrakeTorqueScale, input.Throttle);
            float handbrakeTorque = input.Handbrake ? maxHandbrakeTorque * poweredRelease : 0f;

            // Apply brake grip modifier from stats
            float brakeGripMod = runtimeStats != null ? runtimeStats.BrakeGrip : 1f;
            footBrakeTorque *= brakeGripMod;

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet wheel = wheels[i];
                if (wheel == null || wheel.collider == null)
                {
                    continue;
                }

                wheel.collider.brakeTorque = footBrakeTorque + (wheel.handbrake ? handbrakeTorque : 0f);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Anti-Roll
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyAntiRoll()
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet leftWheel = wheels[i];
                if (leftWheel == null || leftWheel.collider == null || !leftWheel.leftSide)
                {
                    continue;
                }

                WheelSet rightWheel = FindAxlePartner(leftWheel.axleId, false);
                if (rightWheel == null || rightWheel.collider == null)
                {
                    continue;
                }

                float leftTravel = 1f;
                float rightTravel = 1f;

                bool leftGrounded = leftWheel.collider.GetGroundHit(out WheelHit leftHit);
                bool rightGrounded = rightWheel.collider.GetGroundHit(out WheelHit rightHit);

                if (leftGrounded)
                {
                    leftTravel = CalculateSuspensionTravel(leftWheel.collider, leftHit);
                }

                if (rightGrounded)
                {
                    rightTravel = CalculateSuspensionTravel(rightWheel.collider, rightHit);
                }

                float antiRoll = (leftTravel - rightTravel) * runtimeStats.AntiRollForce;

                if (leftGrounded)
                {
                    Rigidbody.AddForceAtPosition(leftWheel.collider.transform.up * -antiRoll, leftWheel.collider.transform.position);
                }

                if (rightGrounded)
                {
                    Rigidbody.AddForceAtPosition(rightWheel.collider.transform.up * antiRoll, rightWheel.collider.transform.position);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PART 3 SYSTEM 4: Dynamic Grip Model
        //  Blends grip based on speed, steering, slip, throttle, braking,
        //  weight transfer, and per-axle grip values from stats.
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyDynamicTireGrip()
        {
            if (wheels == null || runtimeStats == null)
            {
                return;
            }

            float slideT = GetSlideBlend();
            float slideSideMultiplier = Mathf.Lerp(1f, slideSideFrictionMultiplier, slideT);
            float slideForwardMultiplier = Mathf.Lerp(1f, slideForwardFrictionMultiplier, slideT);
            float driftAssistBlend = GetDriftAssistBlend();

            // Speed-based grip falloff — grip decreases slightly at extreme speed
            float speedGripFalloff = Mathf.Lerp(1f, 0.92f, Mathf.InverseLerp(180f, 350f, SpeedKph));

            // Throttle-under-steer grip reduction (simulates power oversteer / understeer)
            float steerMagnitude = Mathf.Abs(input.Steering);
            float throttleSteerGripLoss = steerMagnitude * input.Throttle * 0.06f;

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet wheel = wheels[i];
                if (wheel == null || wheel.collider == null)
                {
                    continue;
                }

                bool isFront = wheel.axleId == "Front" || wheel.steer;
                bool isRear = !isFront;
                float axleGrip = isFront ? runtimeStats.FrontGrip : runtimeStats.RearGrip;
                float tractionMod = runtimeStats.Traction;

                // Weight transfer influence
                float weightMul = GetWeightTransferGripMultiplier(isFront, wheel.leftSide);

                float loadBias = 1f;
                float slipPenalty = 1f;

                if (wheel.collider.GetGroundHit(out WheelHit hit))
                {
                    float travel = CalculateSuspensionTravel(wheel.collider, hit);
                    loadBias = Mathf.Lerp(maxLoadGripBias, baseLoadGripBias, travel);
                    float combinedSlip = Mathf.Abs(hit.sidewaysSlip);
                    slipPenalty = Mathf.Clamp01(1f - (combinedSlip * slipGripPenalty));
                }

                float combinedGrip = axleGrip * tractionMod * weightMul * loadBias * slipPenalty
                    * speedGripFalloff * (1f - throttleSteerGripLoss);
                if (isRear && driftAssistBlend > 0f)
                {
                    combinedGrip *= Mathf.Lerp(1f, rearGripDriftMultiplier, driftAssistBlend);
                }

                if (isRear)
                {
                    float lowSpeedDonut = input.Handbrake && input.Throttle > 0.35f
                        ? Mathf.InverseLerp(0.05f, 0.7f, Mathf.Abs(input.Steering) + input.Throttle * 0.35f)
                        : 0f;
                    float powerSlide = input.Throttle > 0.45f && Mathf.Abs(input.Steering) > 0.18f && SpeedKph > 8f
                        ? Mathf.InverseLerp(8f, 45f, SpeedKph)
                        : 0f;

                    if (input.Handbrake)
                    {
                        combinedGrip *= Mathf.Lerp(1f, handbrakeSlideRearGripMultiplier, Mathf.Clamp01(input.Throttle + 0.35f));
                    }

                    combinedGrip *= Mathf.Lerp(1f, powerSlideRearGripMultiplier, Mathf.Clamp01(powerSlide));
                    combinedGrip *= Mathf.Lerp(1f, donutRearGripMultiplier, Mathf.Clamp01(lowSpeedDonut));
                }

                WheelFrictionCurve forward = wheel.collider.forwardFriction;
                float forwardDriftMultiplier = isRear ? Mathf.Lerp(1f, driftSpeedPreservation, driftAssistBlend) : 1f;
                forward.stiffness = runtimeStats.ForwardStiffness * combinedGrip * slideForwardMultiplier * forwardDriftMultiplier;
                wheel.collider.forwardFriction = forward;

                WheelFrictionCurve sideways = wheel.collider.sidewaysFriction;
                sideways.stiffness = runtimeStats.SidewaysStiffness * combinedGrip * slideSideMultiplier;
                wheel.collider.sidewaysFriction = sideways;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Downforce & Aerodynamic Drag
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyDownforce()
        {
            if (!IsGrounded)
            {
                return;
            }

            Rigidbody.AddForce(-transform.up * SpeedKph * runtimeStats.Downforce, ForceMode.Force);
        }

        private void ApplyAerodynamicDrag()
        {
            Vector3 planarVelocity = Vector3.ProjectOnPlane(Rigidbody.linearVelocity, Vector3.up);
            float speedMs = planarVelocity.magnitude;
            if (speedMs < 0.5f)
            {
                return;
            }

            Vector3 dragForce = -planarVelocity.normalized * speedMs * speedMs * aerodynamicDragCoefficient;
            Rigidbody.AddForce(dragForce, ForceMode.Force);
        }

        private void ApplyArcadeWallGlance(Collision collision)
        {
            if (collision == null || Rigidbody == null || SpeedKph < 18f)
            {
                return;
            }

            Vector3 wallNormal = Vector3.zero;
            int normalCount = 0;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                if (contact.normal.y > 0.45f)
                {
                    continue;
                }

                Vector3 planarNormal = Vector3.ProjectOnPlane(contact.normal, Vector3.up);
                if (planarNormal.sqrMagnitude < 0.01f)
                {
                    continue;
                }

                wallNormal += planarNormal.normalized;
                normalCount++;
            }

            if (normalCount == 0)
            {
                return;
            }

            wallNormal.Normalize();
            Vector3 planarVelocity = Vector3.ProjectOnPlane(Rigidbody.linearVelocity, Vector3.up);
            float intoWallSpeed = -Vector3.Dot(planarVelocity, wallNormal);
            if (intoWallSpeed <= 0.2f)
            {
                return;
            }

            Vector3 tangentVelocity = planarVelocity - Vector3.Project(planarVelocity, wallNormal);
            float retainedSpeed = planarVelocity.magnitude * Mathf.Clamp01(wallGlanceSpeedRetention);
            if (tangentVelocity.sqrMagnitude > 0.01f)
            {
                tangentVelocity = tangentVelocity.normalized * Mathf.Max(tangentVelocity.magnitude, retainedSpeed * 0.65f);
            }
            else
            {
                tangentVelocity = Vector3.Reflect(planarVelocity, wallNormal) * 0.35f;
            }

            Rigidbody.linearVelocity = tangentVelocity + Vector3.Project(Rigidbody.linearVelocity, Vector3.up);
            Rigidbody.AddForce(wallNormal * wallBounceImpulse, ForceMode.VelocityChange);
        }

        private void ApplyLowSpeedStopAssist()
        {
            if (!IsGrounded || Rigidbody == null || input == null || runtimeStats == null)
            {
                return;
            }

            if (input.Handbrake || input.Throttle > 0.05f || IsReversing || input.ReverseHeld || IsSliding)
            {
                return;
            }

            Vector3 planarVelocity = Vector3.ProjectOnPlane(Rigidbody.linearVelocity, Vector3.up);
            if (planarVelocity.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 planarRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
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
            float brakeAssist = Mathf.Lerp(1f, 1.8f, Mathf.Clamp01(input.Brake));
            float forwardStep = lowSpeedCoastDeceleration * Mathf.Lerp(0.4f, 1f, assistT) * brakeAssist * Time.fixedDeltaTime;
            float lateralStep = lowSpeedLateralDamping * Mathf.Lerp(0.35f, 1f, assistT) * Time.fixedDeltaTime;

            forwardVelocityMs = Mathf.MoveTowards(forwardVelocityMs, 0f, forwardStep);
            lateralVelocityMs = Mathf.MoveTowards(lateralVelocityMs, 0f, lateralStep);

            bool shouldSnapToRest = absForwardSpeedKph <= lowSpeedStopSnapKph
                && Mathf.Abs(lateralVelocityMs) * 3.6f <= lowSpeedStopSnapKph
                && Mathf.Abs(input.Steering) < 0.2f;

            if (shouldSnapToRest)
            {
                forwardVelocityMs = 0f;
                lateralVelocityMs = 0f;
            }

            Vector3 stabilizedPlanarVelocity = (planarForward * forwardVelocityMs) + (planarRight * lateralVelocityMs);
            Rigidbody.linearVelocity = stabilizedPlanarVelocity + Vector3.Project(Rigidbody.linearVelocity, Vector3.up);

            Vector3 angularVelocity = Rigidbody.angularVelocity;
            float angularStep = lowSpeedAngularDamping * Mathf.Lerp(0.35f, 1f, assistT) * Time.fixedDeltaTime;
            angularVelocity.y = Mathf.MoveTowards(angularVelocity.y, 0f, angularStep);

            if (shouldSnapToRest)
            {
                angularVelocity.x = Mathf.MoveTowards(angularVelocity.x, 0f, angularStep * 0.6f);
                angularVelocity.z = Mathf.MoveTowards(angularVelocity.z, 0f, angularStep * 0.6f);
            }

            Rigidbody.angularVelocity = angularVelocity;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Lateral Grip Assist
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyLateralGripAssist()
        {
            if (!IsGrounded)
            {
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(Rigidbody.linearVelocity);
            float driftBlend = Mathf.Max(input.Handbrake ? 1f : 0f, GetDriftAssistBlend());
            float handbrakeGripReduction = Mathf.Lerp(1f, runtimeStats.HandbrakeGripMultiplier, driftBlend);
            Vector3 correctiveForce = -transform.right * localVelocity.x * runtimeStats.LateralGripAssist * handbrakeGripReduction;
            Rigidbody.AddForce(correctiveForce, ForceMode.Acceleration);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PART 3 SYSTEM 2: Drivetrain Identity
        //  Applies drivetrain-specific forces that make FWD/RWD/AWD feel different.
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyDrivetrainBehavior()
        {
            if (!IsGrounded || runtimeStats == null)
            {
                return;
            }

            DrivetrainType dt = runtimeStats.Drivetrain;
            float throttle = input.Throttle;
            float steer = input.Steering;
            float absSteer = Mathf.Abs(steer);

            switch (dt)
            {
                case DrivetrainType.FWD:
                    ApplyFwdBehavior(throttle, steer, absSteer);
                    break;
                case DrivetrainType.RWD:
                    ApplyRwdBehavior(throttle, steer, absSteer);
                    break;
                case DrivetrainType.AWD:
                    ApplyAwdBehavior(throttle, steer, absSteer);
                    break;
            }
        }

        /// <summary>FWD: Understeer bias under power + steering. Pull car into line.</summary>
        private void ApplyFwdBehavior(float throttle, float steer, float absSteer)
        {
            if (throttle < 0.1f || absSteer < 0.1f || SpeedKph < 25f)
            {
                return;
            }

            // FWD understeer: the harder you push throttle while steering, the wider you go
            float understeerForce = throttle * absSteer * 0.35f;
            float speedScale = Mathf.InverseLerp(25f, 120f, SpeedKph);
            Rigidbody.AddForce(transform.forward * understeerForce * speedScale * runtimeStats.Mass * 0.02f, ForceMode.Force);
        }

        /// <summary>RWD: Throttle-on oversteer / rotation. Encourages drifting.</summary>
        private void ApplyRwdBehavior(float throttle, float steer, float absSteer)
        {
            if (throttle < 0.3f || SpeedKph < 30f)
            {
                return;
            }

            // RWD oversteer: heavy throttle mid-corner rotates the rear
            float oversteerTorque = throttle * absSteer * 0.25f;
            float direction = steer > 0f ? 1f : -1f;
            float speedScale = Mathf.InverseLerp(30f, 100f, SpeedKph);

            Rigidbody.AddTorque(Vector3.up * oversteerTorque * direction * speedScale * 0.8f, ForceMode.Acceleration);
        }

        /// <summary>AWD: Planted feel, subtle understeer, better launch traction.</summary>
        private void ApplyAwdBehavior(float throttle, float steer, float absSteer)
        {
            if (!IsGrounded)
            {
                return;
            }

            // AWD stability bonus: mild lateral correction when throttle is applied
            if (throttle > 0.1f)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(Rigidbody.linearVelocity);
                float stabilityForce = -localVelocity.x * throttle * 0.15f;
                Rigidbody.AddForce(transform.right * stabilityForce, ForceMode.Acceleration);
            }

            // AWD launch boost: extra forward push at low speed with high throttle
            if (throttle > 0.8f && SpeedKph < 40f)
            {
                Rigidbody.AddForce(transform.forward * throttle * runtimeStats.Mass * 0.008f, ForceMode.Force);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PART 3 SYSTEM 5: Assist Layer
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Countersteer assistance — auto-corrects yaw during slides.</summary>
        private void ApplyCounterSteerAssist()
        {
            if (!IsGrounded || !IsSliding || runtimeStats == null)
            {
                return;
            }

            float assistStrength = runtimeStats.CounterSteerAssist;
            if (assistStrength <= 0f)
            {
                return;
            }

            float speedAssistT = Mathf.InverseLerp(35f, 150f, Mathf.Abs(ForwardSpeedKph));
            if (speedAssistT <= 0f)
            {
                return;
            }

            float desiredYawAssist = -SignedSlipAngleDegrees * assistStrength * 1.8f * speedAssistT;
            Rigidbody.AddTorque(Vector3.up * desiredYawAssist, ForceMode.Acceleration);
        }

        /// <summary>Yaw stability — damps excessive rotation to prevent spin-outs.</summary>
        private void ApplyYawStability()
        {
            if (!IsGrounded || runtimeStats == null)
            {
                return;
            }

            float yawStrength = runtimeStats.YawStability;
            if (yawStrength <= 0f)
            {
                return;
            }

            // Damp angular velocity around the up axis
            float yawRate = Rigidbody.angularVelocity.y;
            float dampingTorque = -yawRate * yawStrength * 2.5f;

            // Only apply when the car is rotating faster than the player is asking for
            float steerIntent = Mathf.Abs(input.Steering);
            float dampScale = Mathf.Lerp(1f, 0.3f, steerIntent); // Reduce damping when player is actively steering

            Rigidbody.AddTorque(Vector3.up * dampingTorque * dampScale, ForceMode.Acceleration);
        }

        /// <summary>High-speed stability — keeps the car composed above ~150 kph.</summary>
        private void ApplyHighSpeedStability()
        {
            if (!IsGrounded || runtimeStats == null)
            {
                return;
            }

            float stabilityFactor = runtimeStats.HighSpeedStability;
            if (stabilityFactor <= 0f)
            {
                return;
            }

            float speedT = Mathf.InverseLerp(120f, 280f, SpeedKph);
            if (speedT <= 0f)
            {
                return;
            }

            // Straighten the car at high speed — resist lateral velocity
            Vector3 localVelocity = transform.InverseTransformDirection(Rigidbody.linearVelocity);
            float lateralCorrection = -localVelocity.x * stabilityFactor * speedT * 0.4f;
            Rigidbody.AddForce(transform.right * lateralCorrection, ForceMode.Acceleration);

            // Damp yaw rotation at speed
            float yawDamp = -Rigidbody.angularVelocity.y * stabilityFactor * speedT * 0.6f;
            Rigidbody.AddTorque(Vector3.up * yawDamp, ForceMode.Acceleration);
        }

        /// <summary>Drift initiation forgiveness — briefly reduces rear grip when entering a slide.</summary>
        private void ApplyDriftAssist()
        {
            if (!IsGrounded || runtimeStats == null || wheels == null)
            {
                return;
            }

            float driftStrength = runtimeStats.DriftAssist;
            if (driftStrength <= 0f)
            {
                return;
            }

            float assistBlend = GetDriftAssistBlend() * driftStrength;
            if (assistBlend <= 0f)
            {
                return;
            }

            float steerDirection = Mathf.Abs(input.Steering) > 0.05f
                ? Mathf.Sign(input.Steering)
                : Mathf.Sign(SignedSlipAngleDegrees);
            float speedT = Mathf.Clamp01(Mathf.InverseLerp(8f, 130f, SpeedKph));
            float handbrakeDonutT = input.Handbrake && input.Throttle > 0.25f
                ? Mathf.Clamp01(Mathf.Abs(input.Steering) + input.Throttle * 0.45f)
                : 0f;
            float yawStrength = driftYawAssist * assistBlend * Mathf.Max(speedT, handbrakeDonutT * donutYawAssist);
            Rigidbody.AddTorque(Vector3.up * steerDirection * yawStrength, ForceMode.Acceleration);

            Vector3 localVelocity = transform.InverseTransformDirection(Rigidbody.linearVelocity);
            if (localVelocity.z > 1f)
            {
                localVelocity.z *= Mathf.Lerp(1f, driftSpeedPreservation, assistBlend * Time.fixedDeltaTime * 3f);
                Rigidbody.linearVelocity = transform.TransformDirection(localVelocity);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Wheel Visuals
        // ─────────────────────────────────────────────────────────────────────

        private void SyncWheelVisuals()
        {
            if (wheels == null)
            {
                return;
            }

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null)
                {
                    continue;
                }

                UpdateWheelPose(wheels[i].collider, wheels[i].mesh);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private float GetReverseDriveRatio()
        {
            if (baseStats == null || baseStats.gearRatios == null || baseStats.gearRatios.Length <= 1)
            {
                return 1f;
            }

            return Mathf.Abs(baseStats.gearRatios[1] * baseStats.finalDriveRatio);
        }

        private WheelSet FindAxlePartner(string axleId, bool leftSide)
        {
            if (wheels == null)
            {
                return null;
            }

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet wheel = wheels[i];
                if (wheel != null && wheel.axleId == axleId && wheel.leftSide == leftSide)
                {
                    return wheel;
                }
            }

            return null;
        }

        private static float CalculateSuspensionTravel(WheelCollider wheel, WheelHit hit)
        {
            float suspensionDistance = Mathf.Max(0.001f, wheel.suspensionDistance);
            float travel = (-wheel.transform.InverseTransformPoint(hit.point).y - wheel.radius) / suspensionDistance;
            return Mathf.Clamp01(travel);
        }

        private void UpdateSlipAngleState()
        {
            float slipThreshold = runtimeStats != null ? runtimeStats.SlipAngle : slideSlipAngleThreshold;

            Vector3 planarVelocity = Vector3.ProjectOnPlane(Rigidbody.linearVelocity, Vector3.up);
            if (planarVelocity.sqrMagnitude < 1f)
            {
                SlipAngleDegrees = 0f;
                SignedSlipAngleDegrees = 0f;
                IsSliding = false;
                return;
            }

            Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 planarVelocityDirection = planarVelocity.normalized;
            SlipAngleDegrees = Vector3.Angle(planarForward, planarVelocityDirection);
            SignedSlipAngleDegrees = Vector3.SignedAngle(planarForward, planarVelocityDirection, Vector3.up);
            IsSliding = SlipAngleDegrees >= slipThreshold;
        }

        private float GetSlideBlend()
        {
            if (SlipAngleDegrees <= slideSlipAngleThreshold)
            {
                return 0f;
            }

            return Mathf.InverseLerp(slideSlipAngleThreshold, Mathf.Max(slideSlipAngleThreshold + 0.01f, fullSlideSlipAngle), SlipAngleDegrees);
        }

        private float GetDriftAssistBlend()
        {
            float initiation = handbrakeDriftWindow > 0f ? Mathf.Clamp01(driftInitiationTimer / handbrakeDriftWindow) : 0f;
            float sustain = driftSustainWindow > 0f ? Mathf.Clamp01(driftSustainTimer / driftSustainWindow) : 0f;
            float slide = Mathf.InverseLerp(slideSlipAngleThreshold * 0.65f, fullSlideSlipAngle, SlipAngleDegrees);
            return Mathf.Clamp01(Mathf.Max(initiation, sustain * 0.75f, slide * 0.35f));
        }

        private static void UpdateWheelPose(WheelCollider wheel, Transform visual)
        {
            if (wheel == null || visual == null)
            {
                return;
            }

            wheel.GetWorldPose(out Vector3 worldPosition, out Quaternion worldRotation);
            visual.position = worldPosition;
            visual.rotation = worldRotation;
        }
    }
}

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
        [SerializeField] private float reverseEngageSpeedKph = 3f;
        [SerializeField] private float reverseReleaseSpeedKph = 1.5f;
        [SerializeField] private float aerodynamicDragCoefficient = 0.36f;

        [Header("Feel")]
        [SerializeField] private float motorTorqueResponse = 2600f;
        [SerializeField] private float baseLoadGripBias = 0.92f;
        [SerializeField] private float maxLoadGripBias = 1.08f;
        [SerializeField] private float slipGripPenalty = 0.18f;
        [SerializeField] private float slideSlipAngleThreshold = 15f;
        [SerializeField] private float fullSlideSlipAngle = 28f;
        [SerializeField] private float slideSideFrictionMultiplier = 0.7f;
        [SerializeField] private float slideForwardFrictionMultiplier = 0.96f;

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
        public float SpeedKph { get; private set; }
        public float ForwardSpeedKph { get; private set; }
        public float SlipAngleDegrees { get; private set; }
        public float SignedSlipAngleDegrees { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsReversing { get; private set; }
        public bool IsSliding { get; private set; }

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
        }

        private void LateUpdate()
        {
            SyncWheelVisuals();
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
            LongitudinalLoadShift = 0f;
            LateralLoadShift = 0f;
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
            float speedFactor = Mathf.InverseLerp(0f, Mathf.Max(1f, runtimeStats.MaxSpeedKph), Mathf.Abs(ForwardSpeedKph));
            float steeringLockT = Mathf.InverseLerp(20f, 200f, Mathf.Abs(ForwardSpeedKph));
            float dynamicSteerLock = Mathf.Lerp(40f, 8f, steeringLockT);
            float steerReduction = Mathf.Lerp(1f, runtimeStats.HighSpeedSteerReduction, speedFactor);
            float targetSteerAngle = input.Steering * Mathf.Min(dynamicSteerLock, runtimeStats.MaxSteerAngle * steerReduction);

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
                return;
            }

            if (!IsReversing)
            {
                if (input.ConsumeReverseRequest() && Mathf.Abs(ForwardSpeedKph) <= reverseEngageSpeedKph)
                {
                    IsReversing = true;
                    gearbox?.ResetToFirstGear();
                }

                return;
            }

            if (ForwardSpeedKph > reverseEngageSpeedKph)
            {
                IsReversing = false;
                return;
            }

            if (!input.ReverseHeld && Mathf.Abs(ForwardSpeedKph) <= reverseReleaseSpeedKph)
            {
                IsReversing = false;
            }
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
            float driveRatio = IsReversing ? GetReverseDriveRatio() : gearbox.GetCurrentDriveRatio(baseStats);
            if (IsReversing)
            {
                gearbox.UpdateRpmOnly(averageDrivenWheelRpm, baseStats, driveRatio);
            }
            else
            {
                gearbox.UpdateAutomatic(averageDrivenWheelRpm, baseStats);
            }

<<<<<<< HEAD
            float driveRatio = IsReversing ? GetReverseDriveRatio() : gearbox.GetCurrentDriveRatio(baseStats);

            // Sample torque curve — prefer stats-embedded curve, fall back to EngineModel component
=======
>>>>>>> parent of 0c7394b (New Garage.)
            float normalizedRpm = Mathf.InverseLerp(runtimeStats.IdleRPM, runtimeStats.MaxRPM, gearbox.CurrentRPM);
            float torqueCurveSample = EvaluateTorqueCurve(normalizedRpm);

            float driveInput = IsReversing && input.ReverseHeld ? -input.Brake : input.Throttle;
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

        // ─────────────────────────────────────────────────────────────────────
        //  Braking
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyBraking()
        {
            float footBrakeTorque = IsReversing && input.ReverseHeld ? 0f : input.Brake * runtimeStats.MaxBrakeTorque;
            float handbrakeTorque = input.Handbrake ? maxHandbrakeTorque : 0f;

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

                WheelFrictionCurve forward = wheel.collider.forwardFriction;
                forward.stiffness = runtimeStats.ForwardStiffness * combinedGrip * slideForwardMultiplier;
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
            float handbrakeGripReduction = input.Handbrake ? runtimeStats.HandbrakeGripMultiplier : 1f;
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

            // Active during handbrake or when throttle + steer exceed threshold
            bool isDriftInput = input.Handbrake
                || (input.Throttle > 0.6f && Mathf.Abs(input.Steering) > 0.5f && SpeedKph > 35f);

            if (!isDriftInput)
            {
                return;
            }

            // Briefly reduce rear sideways stiffness to help initiate drifts
            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet wheel = wheels[i];
                if (wheel == null || wheel.collider == null)
                {
                    continue;
                }

                bool isRear = wheel.axleId == "Rear" || (!wheel.steer && !wheel.drive && wheel.handbrake)
                    || (wheel.axleId != "Front" && !wheel.steer);

                if (!isRear)
                {
                    continue;
                }

                WheelFrictionCurve sideways = wheel.collider.sidewaysFriction;
                sideways.stiffness *= Mathf.Lerp(1f, 0.65f, driftStrength);
                wheel.collider.sidewaysFriction = sideways;
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

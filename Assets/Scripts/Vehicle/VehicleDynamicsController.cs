using UnityEngine;
using Underground.Progression;
using Underground.World;

namespace Underground.Vehicle
{
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleDynamicsController : MonoBehaviour
    {
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
        [SerializeField] private float steerAngleResponse = 72f;
        [SerializeField] private float motorTorqueResponse = 2600f;
        [SerializeField] private float baseLoadGripBias = 0.92f;
        [SerializeField] private float maxLoadGripBias = 1.08f;
        [SerializeField] private float slipGripPenalty = 0.18f;
        [SerializeField] private float slideSlipAngleThreshold = 15f;
        [SerializeField] private float fullSlideSlipAngle = 28f;
        [SerializeField] private float slideSideFrictionMultiplier = 0.7f;
        [SerializeField] private float slideForwardFrictionMultiplier = 0.96f;
        [SerializeField] private float counterSteerAssistTorque = 1.15f;
        [SerializeField] private float counterSteerAssistSpeedStartKph = 35f;

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
            if (Rigidbody == null || input == null || gearbox == null || engineModel == null || wheels == null || wheels.Length == 0)
            {
                return;
            }

            ApplyCenterOfMass();
            UpdateTelemetry();
            UpdateReverseState();
            ApplySteering();
            ApplyDrive();
            ApplyBraking();
            ApplyAntiRoll();
            ApplyDynamicTireGrip();
            ApplyDownforce();
            ApplyAerodynamicDrag();
            ApplyLateralGripAssist();
            ApplyCounterSteerAssist();
        }

        private void LateUpdate()
        {
            SyncWheelVisuals();
        }

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
            gearbox?.ResetToFirstGear();
        }

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

        private void ApplySteering()
        {
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
                    steerAngleResponse * Time.fixedDeltaTime);
            }
        }

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

            float normalizedRpm = Mathf.InverseLerp(runtimeStats.IdleRPM, runtimeStats.MaxRPM, gearbox.CurrentRPM);
            float torqueCurveSample = engineModel.EvaluateNormalizedTorque(normalizedRpm);
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

        private void ApplyBraking()
        {
            float footBrakeTorque = IsReversing && input.ReverseHeld ? 0f : input.Brake * runtimeStats.MaxBrakeTorque;
            float handbrakeTorque = input.Handbrake ? maxHandbrakeTorque : 0f;

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

        private void ApplyDynamicTireGrip()
        {
            if (wheels == null || runtimeStats == null)
            {
                return;
            }

            float slideT = GetSlideBlend();
            float slideSideMultiplier = Mathf.Lerp(1f, slideSideFrictionMultiplier, slideT);
            float slideForwardMultiplier = Mathf.Lerp(1f, slideForwardFrictionMultiplier, slideT);

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet wheel = wheels[i];
                if (wheel == null || wheel.collider == null)
                {
                    continue;
                }

                float loadBias = 1f;
                float slipPenalty = 1f;

                if (wheel.collider.GetGroundHit(out WheelHit hit))
                {
                    float travel = CalculateSuspensionTravel(wheel.collider, hit);
                    loadBias = Mathf.Lerp(maxLoadGripBias, baseLoadGripBias, travel);
                    float combinedSlip = Mathf.Abs(hit.sidewaysSlip);
                    slipPenalty = Mathf.Clamp01(1f - (combinedSlip * slipGripPenalty));
                }

                WheelFrictionCurve forward = wheel.collider.forwardFriction;
                forward.stiffness = runtimeStats.ForwardStiffness * loadBias * slipPenalty * slideForwardMultiplier;
                wheel.collider.forwardFriction = forward;

                WheelFrictionCurve sideways = wheel.collider.sidewaysFriction;
                sideways.stiffness = runtimeStats.SidewaysStiffness * loadBias * slipPenalty * slideSideMultiplier;
                wheel.collider.sidewaysFriction = sideways;
            }
        }

        private void ApplyCounterSteerAssist()
        {
            if (!IsGrounded || !IsSliding)
            {
                return;
            }

            float speedAssistT = Mathf.InverseLerp(counterSteerAssistSpeedStartKph, 150f, Mathf.Abs(ForwardSpeedKph));
            if (speedAssistT <= 0f)
            {
                return;
            }

            float desiredYawAssist = -SignedSlipAngleDegrees * counterSteerAssistTorque * speedAssistT;
            Rigidbody.AddTorque(Vector3.up * desiredYawAssist, ForceMode.Acceleration);
        }

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
            IsSliding = SlipAngleDegrees >= slideSlipAngleThreshold;
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

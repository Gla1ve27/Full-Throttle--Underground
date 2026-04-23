using UnityEngine;
using Underground.Progression;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Top-level V2 vehicle controller. Orchestrates all modular systems
    /// in the correct FixedUpdate order:
    ///   1. Input capture
    ///   2. Telemetry
    ///   3. Reverse state
    ///   4. Drift state
    ///   5. Powertrain (gearbox → RPM → torque)
    ///   6. Steering
    ///   7. Braking
    ///   8. Grip
    ///   9. Aero
    ///   10. Anti-roll
    ///   11. Assists + drift forces
    ///   12. Final telemetry refresh
    ///   13. Wheel visual sync (LateUpdate)
    ///
    /// Does NOT self-select a car identity — that comes from VehicleSetupBridge.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VehicleControllerV2 : MonoBehaviour
    {
        // ── Data ──
        [Header("Data")]
        [SerializeField] private VehicleStatsData baseStats;
        [SerializeField] private RuntimeVehicleStats runtimeStats = new RuntimeVehicleStats();

        // ── References ──
        [Header("References")]
        [SerializeField] private Transform centerOfMassReference;
        [SerializeField] private WheelSet[] wheels;

        // ── Systems (auto-resolved) ──
        [Header("Systems")]
        [SerializeField] private VehicleInputAdapter inputAdapter;
        [SerializeField] private VehicleTelemetry telemetry;
        [SerializeField] private PowertrainSystem powertrain;
        [SerializeField] private VehicleSteeringSystem steering;
        [SerializeField] private VehicleBrakeSystem brakes;
        [SerializeField] private VehicleGripSystem grip;
        [SerializeField] private VehicleDriftSystem drift;
        [SerializeField] private VehicleAssistSystem assists;
        [SerializeField] private VehicleAeroSystem aero;
        [SerializeField] private VehicleWallResponseSystem wallResponse;
        [SerializeField] private WheelVisualSynchronizer wheelVisuals;

        // ── Reverse ──
        [Header("Reverse")]
        [SerializeField] private float reverseEngageSpeedKph = 3f;
        [SerializeField] private float reverseReleaseSpeedKph = 1.5f;
        [SerializeField] private float reverseHoldToEngageDelay = 0.12f;

        // ── Runtime ──
        public Rigidbody Rigidbody { get; private set; }
        public VehicleState State { get; private set; }
        public VehicleStatsData BaseStats => baseStats;
        public RuntimeVehicleStats RuntimeStats => runtimeStats;
        public WheelSet[] WheelSets => wheels;
        public bool IsInitialized { get; private set; }

        private float reverseHoldTimer;

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Initializes the V2 controller with the given stats and upgrades.
        /// Called by <see cref="VehicleSetupBridge"/> — never self-initializes.
        /// </summary>
        public void Initialize(VehicleStatsData source, params UpgradeDefinition[] upgrades)
        {
            Rigidbody = GetComponent<Rigidbody>();
            if (Rigidbody == null)
            {
                Debug.LogError("[V2] No Rigidbody found on vehicle.");
                return;
            }

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

            State = new VehicleState();
            State.Reset();

            // ── Resolve sub-systems ──
            ResolveSubSystems();

            // ── Apply stats to vehicle ──
            ApplyStatsToVehicle();

            // ── Initialize sub-systems ──
            telemetry?.Initialize(Rigidbody, wheels, State);
            powertrain?.Initialize(baseStats, runtimeStats, wheels);
            wallResponse?.Initialize(Rigidbody, State);

            IsInitialized = true;

            // ── Safety: ensure critical physics values are never zero ──
            if (runtimeStats.Mass < 100f)
            {
                Debug.LogWarning("[V2] Mass was below 100 — applying safe default (1480).");
                runtimeStats.Mass = 1480f;
            }
            if (runtimeStats.Spring < 1000f)
            {
                runtimeStats.Spring = 30000f;
                runtimeStats.Damper = 3900f;
                runtimeStats.SuspensionDistance = 0.18f;
                Debug.LogWarning("[V2] Suspension values were near-zero — applied safe defaults.");
            }
            // Re-apply after safety defaults
            ApplyStatsToVehicle();

            Debug.Log($"[V2] VehicleControllerV2 initialized with {baseStats?.name ?? "null stats"}.");
        }

        public void ApplyUpgrade(UpgradeDefinition definition)
        {
            runtimeStats.ApplyUpgrade(definition);
            ApplyStatsToVehicle();
        }

        public RuntimeVehicleStats GetRuntimeStats()
        {
            return runtimeStats;
        }

        public void SetUseManualTransmission(bool enabled)
        {
            powertrain?.Gearbox?.SetUseManualTransmission(enabled);
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

        public void ResetVehicle(Transform respawnPoint)
        {
            if (respawnPoint == null || Rigidbody == null)
            {
                return;
            }

            Rigidbody.linearVelocity = Vector3.zero;
            Rigidbody.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(respawnPoint.position, respawnPoint.rotation);
            Rigidbody.position += Vector3.up * (runtimeStats?.ResetLift ?? 0.5f);

            reverseHoldTimer = 0f;
            State?.Reset();
            telemetry?.ResetTelemetry();
            powertrain?.ResetPowertrain();
            drift?.ResetDrift();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void FixedUpdate()
        {
            if (!IsInitialized || Rigidbody == null || State == null || wheels == null || wheels.Length == 0)
            {
                return;
            }

            // 1. Center of mass
            ApplyCenterOfMass();

            // 2. Capture input
            inputAdapter?.CaptureCommand();
            DriverCommand cmd = inputAdapter != null ? inputAdapter.CurrentCommand : DriverCommand.None;

            // Write input to state for audio/UI consumers
            State.Throttle = cmd.Throttle;
            State.Brake = cmd.Brake;
            State.Steering = cmd.Steering;
            State.Handbrake = cmd.Handbrake;

            // 3. Telemetry
            telemetry?.UpdateTelemetry();

            // 4. Reverse state
            UpdateReverseState(cmd);

            // 5. Drift state
            drift?.UpdateDriftState(State, cmd);

            // 6. Powertrain (gearbox → RPM → torque)
            powertrain?.UpdatePowertrain(State, cmd);

            // 7. Steering
            steering?.UpdateSteering(wheels, State, cmd, runtimeStats);

            // 8. Braking
            brakes?.UpdateBraking(wheels, State, cmd, runtimeStats);

            // 9. Anti-roll
            wheelVisuals?.ApplyAntiRoll(Rigidbody, wheels, runtimeStats?.AntiRollForce ?? 0f);

            // 10. Grip
            float driftBlend = drift != null ? drift.DriftAssistBlend : 0f;
            grip?.UpdateGrip(wheels, State, cmd, runtimeStats, driftBlend);

            // 11. Aero
            aero?.UpdateAero(Rigidbody, State, runtimeStats);

            // 12. Assists + drift forces
            assists?.UpdateAssists(Rigidbody, State, cmd, runtimeStats, driftBlend);
            drift?.ApplyDriftForces(Rigidbody, State, cmd, runtimeStats);

            // 13. Final telemetry refresh
            telemetry?.UpdateTelemetry();
        }

        private void LateUpdate()
        {
            if (!IsInitialized)
            {
                return;
            }

            wheelVisuals?.SyncVisuals(wheels);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Internals
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

        private void UpdateReverseState(DriverCommand cmd)
        {
            if (cmd.Throttle > 0.05f)
            {
                State.IsReversing = false;
                reverseHoldTimer = 0f;
                return;
            }

            if (!State.IsReversing)
            {
                if (cmd.ReverseHeld && Mathf.Abs(State.ForwardSpeedKph) <= reverseEngageSpeedKph)
                {
                    reverseHoldTimer += Time.fixedDeltaTime;
                    if (reverseHoldTimer >= reverseHoldToEngageDelay)
                    {
                        State.IsReversing = true;
                        powertrain?.Gearbox?.ResetToFirstGear();
                    }
                }
                else
                {
                    reverseHoldTimer = 0f;
                }

                return;
            }

            if (State.ForwardSpeedKph > reverseEngageSpeedKph)
            {
                State.IsReversing = false;
                reverseHoldTimer = 0f;
                return;
            }

            if (!cmd.ReverseHeld && Mathf.Abs(State.ForwardSpeedKph) <= reverseReleaseSpeedKph)
            {
                State.IsReversing = false;
                reverseHoldTimer = 0f;
            }
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

            if (centerOfMassReference != null)
            {
                Rigidbody.centerOfMass = transform.InverseTransformPoint(centerOfMassReference.position);
            }
            else
            {
                Rigidbody.centerOfMass = runtimeStats.CenterOfMassOffset;
            }

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

        private void ResolveSubSystems()
        {
            if (inputAdapter == null) inputAdapter = GetComponent<VehicleInputAdapter>();
            if (telemetry == null) telemetry = GetComponent<VehicleTelemetry>();
            if (powertrain == null) powertrain = GetComponent<PowertrainSystem>();
            if (steering == null) steering = GetComponent<VehicleSteeringSystem>();
            if (brakes == null) brakes = GetComponent<VehicleBrakeSystem>();
            if (grip == null) grip = GetComponent<VehicleGripSystem>();
            if (drift == null) drift = GetComponent<VehicleDriftSystem>();
            if (assists == null) assists = GetComponent<VehicleAssistSystem>();
            if (aero == null) aero = GetComponent<VehicleAeroSystem>();
            if (wallResponse == null) wallResponse = GetComponent<VehicleWallResponseSystem>();
            if (wheelVisuals == null) wheelVisuals = GetComponent<WheelVisualSynchronizer>();
        }
    }
}

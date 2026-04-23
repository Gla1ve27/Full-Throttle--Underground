using UnityEngine;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Top-level powertrain orchestrator. Coordinates the engine, gearbox, and torque distributor
    /// to produce drive torque from throttle input.
    /// </summary>
    public sealed class PowertrainSystem : MonoBehaviour
    {
        [SerializeField] private EngineRPMModel rpmModel;
        [SerializeField] private GearboxSystemV2 gearbox;
        [SerializeField] private TorqueDistributor torqueDistributor;
        [SerializeField] private EngineModel engineModel;

        [Header("Torque")]
        [SerializeField] private float motorTorqueResponse = 2600f;

        private VehicleStatsData cachedStats;
        private RuntimeVehicleStats runtimeStats;
        private WheelSet[] wheels;

        public GearboxSystemV2 Gearbox => gearbox;
        public EngineRPMModel RPMModel => rpmModel;

        public void Initialize(VehicleStatsData stats, RuntimeVehicleStats runtime, WheelSet[] wheelSets)
        {
            cachedStats = stats;
            runtimeStats = runtime;
            wheels = wheelSets;

            if (rpmModel == null)
            {
                rpmModel = GetComponent<EngineRPMModel>();
            }

            if (gearbox == null)
            {
                gearbox = GetComponent<GearboxSystemV2>();
            }

            if (torqueDistributor == null)
            {
                torqueDistributor = GetComponent<TorqueDistributor>();
            }

            if (engineModel == null)
            {
                engineModel = GetComponent<EngineModel>();
            }

            rpmModel?.Initialize();
            gearbox?.ResetToFirstGear();
        }

        /// <summary>
        /// Called in FixedUpdate after telemetry. Computes RPM, gearbox, and drive torque.
        /// </summary>
        public void UpdatePowertrain(VehicleState state, DriverCommand cmd)
        {
            if (state == null || cachedStats == null || runtimeStats == null)
            {
                return;
            }

            // ── 1. Gearbox — decide gear ──
            gearbox?.UpdateGearbox(state, cmd, cachedStats);

            // ── 2. Drive ratio ──
            float driveRatio = state.IsReversing
                ? gearbox.GetReverseDriveRatio(cachedStats)
                : gearbox.GetCurrentDriveRatio(cachedStats);

            // ── 3. RPM — blend free-rev and wheel-coupled ──
            rpmModel?.UpdateRPM(state, cmd, runtimeStats, driveRatio, wheels);

            // ── 4. Torque curve sample ──
            float torqueCurveSample = EvaluateTorqueCurve(state.NormalizedRPM);

            // ── 5. Drive input ──
            float driveInput;
            if (state.IsReversing && cmd.ReverseHeld)
            {
                driveInput = -cmd.Brake;
            }
            else
            {
                driveInput = cmd.Throttle;
            }

            // Torque cut during shift
            if (!state.IsReversing && state.IsShifting)
            {
                float cutFactor = Mathf.Lerp(gearbox.ShiftTorqueCut, 1f, state.ShiftProgress);
                driveInput *= cutFactor;
            }

            // ── 6. Total torque ──
            int drivenCount = CountDrivenWheels();
            if (drivenCount == 0)
            {
                return;
            }

            float totalTorque = driveInput * runtimeStats.MaxMotorTorque * torqueCurveSample * driveRatio;

            // Speed limiter
            bool canApplyTorque;
            if (driveInput >= 0f)
            {
                canApplyTorque = state.SpeedKph < runtimeStats.MaxSpeedKph || driveInput < 0.05f;
            }
            else
            {
                canApplyTorque = state.ForwardSpeedKph > -runtimeStats.MaxSpeedKph || Mathf.Abs(driveInput) < 0.05f;
            }

            if (!canApplyTorque)
            {
                totalTorque = 0f;
            }

            // ── 7. Distribute ──
            torqueDistributor?.DistributeTorque(wheels, totalTorque, runtimeStats.Drivetrain, state);
        }

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

            return 0.7f;
        }

        private int CountDrivenWheels()
        {
            if (wheels == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] != null && wheels[i].collider != null && wheels[i].drive)
                {
                    count++;
                }
            }

            return count;
        }

        public void ResetPowertrain()
        {
            rpmModel?.ResetRPM();
            gearbox?.ResetToFirstGear();
        }
    }
}



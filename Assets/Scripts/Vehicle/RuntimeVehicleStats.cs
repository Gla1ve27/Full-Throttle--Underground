using UnityEngine;
using Underground.Progression;

namespace Underground.Vehicle
{
    /// <summary>
    /// Mutable runtime snapshot of a vehicle's stats.
    /// Created from a VehicleStatsData base, then modified by upgrades.
    /// The runtime physics layer reads values from this instead of the
    /// immutable ScriptableObject directly.
    /// </summary>
    [System.Serializable]
    public class RuntimeVehicleStats
    {
        // ── Identity / Classification ──
        public VehicleArchetype Archetype;
        public DrivetrainType Drivetrain;

        // ── Powertrain ──
        public float Horsepower;
        public float MaxMotorTorque;
        public float MaxBrakeTorque;
        public float MaxSpeedKph;
        public AnimationCurve TorqueCurve;

        // ── Transmission ──
        public float IdleRPM;
        public float MaxRPM;
        public float FinalDriveRatio;

        // ── Steering ──
        public float MaxSteerAngle;
        public float HighSpeedSteerReduction;
        public float SteeringResponse;

        // ── Chassis ──
        public float Mass;
        public float WeightDistributionFront;
        public float CenterOfMassHeight;
        public Vector3 CenterOfMassOffset;

        // ── Suspension ──
        public float Spring;
        public float Damper;
        public float SuspensionDistance;
        public float AntiRollForce;

        // ── Grip & Handling ──
        public float ForwardStiffness;
        public float SidewaysStiffness;
        public float FrontGrip;
        public float RearGrip;
        public float Traction;
        public float BrakeGrip;
        public float SlipAngle;
        public float RecoveryRate;
        public float HighSpeedStability;

        // ── Stability ──
        public float Downforce;
        public float LateralGripAssist;
        public float HandbrakeGripMultiplier;
        public float ResetLift;

        // ── Assist Layer ──
        public float DriftAssist;
        public float CounterSteerAssist;
        public float YawStability;
        public float NitrousGripAssist;

        /// <summary>
        /// Copies all values from the base ScriptableObject data asset.
        /// </summary>
        public void LoadFromBase(VehicleStatsData data)
        {
            if (data == null)
            {
                return;
            }

            // Identity
            Archetype = data.archetype;
            Drivetrain = data.drivetrain;

            // Powertrain
            Horsepower = data.horsepower;
            MaxMotorTorque = data.maxMotorTorque;
            MaxBrakeTorque = data.maxBrakeTorque;
            MaxSpeedKph = data.maxSpeedKph;
            TorqueCurve = data.torqueCurve;

            // Transmission
            IdleRPM = data.idleRPM;
            MaxRPM = data.maxRPM;
            FinalDriveRatio = data.finalDriveRatio;

            // Steering
            MaxSteerAngle = data.maxSteerAngle;
            HighSpeedSteerReduction = data.highSpeedSteerReduction;
            SteeringResponse = data.steeringResponse;

            // Chassis
            Mass = data.defaultMass;
            WeightDistributionFront = data.weightDistributionFront;
            CenterOfMassHeight = data.centerOfMassHeight;
            CenterOfMassOffset = data.centerOfMassOffset;

            // Suspension
            Spring = data.spring;
            Damper = data.damper;
            SuspensionDistance = data.suspensionDistance;
            AntiRollForce = data.antiRollForce;

            // Grip & Handling
            ForwardStiffness = data.forwardStiffness;
            SidewaysStiffness = data.sidewaysStiffness;
            FrontGrip = data.frontGrip;
            RearGrip = data.rearGrip;
            Traction = data.traction;
            BrakeGrip = data.brakeGrip;
            SlipAngle = data.slipAngle;
            RecoveryRate = data.recoveryRate;
            HighSpeedStability = data.highSpeedStability;

            // Stability
            Downforce = data.downforce;
            LateralGripAssist = data.lateralGripAssist;
            HandbrakeGripMultiplier = data.handbrakeGripMultiplier;
            ResetLift = data.resetLift;

            // Assists
            DriftAssist = data.driftAssist;
            CounterSteerAssist = data.counterSteerAssist;
            YawStability = data.yawStability;
            NitrousGripAssist = data.nitrousGripAssist;
        }

        /// <summary>
        /// Applies additive modifications from a performance upgrade.
        /// </summary>
        public void ApplyUpgrade(UpgradeDefinition upgrade)
        {
            if (upgrade == null)
            {
                return;
            }

            MaxMotorTorque += upgrade.motorTorqueAdd;
            MaxBrakeTorque += upgrade.brakeTorqueAdd;
            ForwardStiffness += upgrade.forwardStiffnessAdd;
            SidewaysStiffness += upgrade.sidewaysStiffnessAdd;
            Spring += upgrade.springAdd;
            Damper += upgrade.damperAdd;
            Mass += upgrade.massDelta;
        }
    }
}


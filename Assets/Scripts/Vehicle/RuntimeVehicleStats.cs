using UnityEngine;
using Underground.Progression;

namespace Underground.Vehicle
{
    [System.Serializable]
    public class RuntimeVehicleStats
    {
        public float MaxMotorTorque;
        public float MaxBrakeTorque;
        public float MaxSpeedKph;
        public float MaxSteerAngle;
        public float HighSpeedSteerReduction;
        public float Downforce;
        public float LateralGripAssist;
        public float AntiRollForce;
        public float HandbrakeGripMultiplier;
        public float ResetLift;
        public Vector3 CenterOfMassOffset;
        public float Spring;
        public float Damper;
        public float SuspensionDistance;
        public float ForwardStiffness;
        public float SidewaysStiffness;
        public float IdleRPM;
        public float MaxRPM;
        public float FinalDriveRatio;
        public float Mass;

        public void LoadFromBase(VehicleStatsData data)
        {
            if (data == null)
            {
                return;
            }

            MaxMotorTorque = data.maxMotorTorque;
            MaxBrakeTorque = data.maxBrakeTorque;
            MaxSpeedKph = data.maxSpeedKph;
            MaxSteerAngle = data.maxSteerAngle;
            HighSpeedSteerReduction = data.highSpeedSteerReduction;
            Downforce = data.downforce;
            LateralGripAssist = data.lateralGripAssist;
            AntiRollForce = data.antiRollForce;
            HandbrakeGripMultiplier = data.handbrakeGripMultiplier;
            ResetLift = data.resetLift;
            CenterOfMassOffset = data.centerOfMassOffset;
            Spring = data.spring;
            Damper = data.damper;
            SuspensionDistance = data.suspensionDistance;
            ForwardStiffness = data.forwardStiffness;
            SidewaysStiffness = data.sidewaysStiffness;
            IdleRPM = data.idleRPM;
            MaxRPM = data.maxRPM;
            FinalDriveRatio = data.finalDriveRatio;
            Mass = data.defaultMass;
        }

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

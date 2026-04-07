using UnityEngine;

namespace Underground.Vehicle
{
    [CreateAssetMenu(menuName = "Racing/Vehicle Stats Data", fileName = "VehicleStatsData")]
    public class VehicleStatsData : ScriptableObject
    {
        [Header("Identity")]
        public string vehicleId = "starter_car";
        public string displayName = "Starter Coupe";

        [Header("Power")]
        public float maxMotorTorque = 540f;
        public float maxBrakeTorque = 4800f;
        public float maxSpeedKph = 136f;

        [Header("Steering")]
        public float maxSteerAngle = 20.5f;
        public float highSpeedSteerReduction = 0.18f;

        [Header("Stability")]
        public float downforce = 34f;
        public float lateralGripAssist = 1.15f;
        public float antiRollForce = 3200f;
        public float handbrakeGripMultiplier = 0.5f;
        public float resetLift = 1.2f;
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.48f, 0.03f);

        [Header("Suspension")]
        public float spring = 30000f;
        public float damper = 3900f;
        public float suspensionDistance = 0.18f;

        [Header("Friction")]
        public float forwardStiffness = 1.28f;
        public float sidewaysStiffness = 1.45f;

        [Header("Transmission")]
        public float idleRPM = 900f;
        public float maxRPM = 5800f;
        public float shiftUpRPM = 5000f;
        public float shiftDownRPM = 1800f;
        public float finalDriveRatio = 3.85f;
        public float[] gearRatios = { 0f, 2.35f, 1.82f, 1.42f, 1.08f, 0.92f, 0.74f };

        [Header("Mass")]
        public float defaultMass = 1480f;

        [Header("Economy Defaults")]
        public int baseValue = 15000;
        public int repairCostPerDamagePoint = 20;
    }
}

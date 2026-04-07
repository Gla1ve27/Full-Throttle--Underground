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
        public float maxMotorTorque = 2200f;
        public float maxBrakeTorque = 5000f;
        public float maxSpeedKph = 220f;

        [Header("Steering")]
        public float maxSteerAngle = 30f;
        public float highSpeedSteerReduction = 0.45f;

        [Header("Stability")]
        public float downforce = 60f;
        public float lateralGripAssist = 2.5f;
        public float antiRollForce = 4500f;
        public float handbrakeGripMultiplier = 0.55f;
        public float resetLift = 1.2f;
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.45f, 0f);

        [Header("Suspension")]
        public float spring = 35000f;
        public float damper = 4500f;
        public float suspensionDistance = 0.2f;

        [Header("Friction")]
        public float forwardStiffness = 1.6f;
        public float sidewaysStiffness = 2.0f;

        [Header("Transmission")]
        public float idleRPM = 900f;
        public float maxRPM = 7200f;
        public float shiftUpRPM = 6500f;
        public float shiftDownRPM = 2200f;
        public float finalDriveRatio = 3.7f;
        public float[] gearRatios = { 0f, 3.2f, 2.1f, 1.5f, 1.15f, 0.92f, 0.75f };

        [Header("Mass")]
        public float defaultMass = 1350f;

        [Header("Economy Defaults")]
        public int baseValue = 15000;
        public int repairCostPerDamagePoint = 20;
    }
}

// ============================================================
// VehicleStatsData.cs
// Part 2 — Data Model and ScriptableObjects
// Place at: Assets/FullThrottle/Vehicles/Data/VehicleStatsData.cs
// ============================================================

using UnityEngine;

namespace Underground.Vehicle
{
    [CreateAssetMenu(
        fileName = "NewVehicleStats",
        menuName = "Underground/Vehicle/Vehicle Stats Data")]
    public class VehicleStatsData : ScriptableObject
    {
        // ── Identity (legacy compat — source of truth is VehicleDefinition) ─────────────
        [Header("Identity (compat — prefer VehicleDefinition)")]
        [Tooltip("Canonical lore ID. Authoritative source is VehicleDefinition.")]
        public string vehicleId;

        [Tooltip("Display name for UI. Authoritative source is VehicleDefinition.displayName.")]
        public string displayName;

        [Tooltip("Archetype used by RuntimeVehicleStats. Authoritative source is VehicleDefinition.")]
        public VehicleArchetype archetype;

        [Tooltip("Drivetrain used by RuntimeVehicleStats. Authoritative source is VehicleDefinition.")]
        public DrivetrainType drivetrain;
        // ── Powertrain ───────────────────────────────────────────────────────
        [Header("Powertrain")]
        public float horsepower = 320f;
        public float maxMotorTorque = 540f;
        public float maxBrakeTorque = 4800f;
        public float maxSpeedKph = 136f;

        [Tooltip("Normalized torque curve (0–1 RPM range → 0–1 torque output).")]
        public AnimationCurve torqueCurve = new AnimationCurve(
            new Keyframe(0f, 0.24f),
            new Keyframe(0.2f, 0.42f),
            new Keyframe(0.45f, 0.62f),
            new Keyframe(0.68f, 0.74f),
            new Keyframe(0.86f, 0.72f),
            new Keyframe(1f, 0.56f));

        [Header("Transmission")]
        public float idleRPM = 900f;
        public float maxRPM = 5800f;
        [Tooltip("RPM threshold to upshift (used by GearboxSystem).")]
        public float shiftUpRPM = 5000f;
        [Tooltip("RPM threshold to downshift (used by GearboxSystem).")]
        public float shiftDownRPM = 1800f;
        public float finalDriveRatio = 3.85f;
        public float[] gearRatios = { 0f, 2.35f, 1.82f, 1.42f, 1.08f, 0.92f, 0.74f };

        // ── Chassis ──────────────────────────────────────────────────────────
        [Header("Chassis")]
        public float defaultMass = 1480f;
        public float weightDistributionFront = 0.52f;
        public float centerOfMassHeight = -0.48f;
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.48f, 0.03f);

        [Tooltip("Rigidbody linear drag. Lower = faster top speed. Default 0.02.")]
        public float linearDamping = 0.02f;

        [Tooltip("Rigidbody angular drag. Lower = snappier rotation. Default 0.05.")]
        public float angularDamping = 0.05f;

        [Header("Suspension")]
        public float spring = 30000f;
        public float damper = 3900f;
        public float suspensionDistance = 0.18f;
        public float antiRollForce = 3200f;

        // ── Steering ──────────────────────────────────────────────────────────
        [Header("Steering")]
        public float maxSteerAngle = 20.5f;
        public float highSpeedSteerReduction = 0.18f;
        public float steeringResponse = 72f;

        // ── Grip & Handling ────────────────────────────────────────────────
        [Header("Grip & Handling")]
        public float forwardStiffness = 1.28f;
        public float sidewaysStiffness = 1.45f;
        public float frontGrip = 1.0f;
        public float rearGrip = 1.0f;
        public float traction = 1.0f;
        public float brakeGrip = 1.0f;
        public float slipAngle = 15f;
        public float recoveryRate = 3.5f;
        public float highSpeedStability = 1.0f;

        [Header("Stability")]
        public float downforce = 34f;
        public float lateralGripAssist = 1.15f;
        public float handbrakeGripMultiplier = 0.5f;
        public float resetLift = 1.2f;

        // ── Assist Layer ─────────────────────────────────────────────────────
        [Header("Assist Layer")]
        public float driftAssist = 0.5f;
        public float counterSteerAssist = 0.6f;
        public float yawStability = 0.4f;
        public float nitrousGripAssist = 0.15f;

        // ── UI Summary (keep for tools) ───────────────────────────────────────
        [Header("UI Summary")]
        [Range(1, 10)] public int uiSpeed    = 5;
        [Range(1, 10)] public int uiHandling = 5;
        [Range(1, 10)] public int uiPower    = 5;
        [Range(1, 10)] public int uiWeight   = 5;
        // ── Economy ──────────────────────────────────────────────────────────
        [Header("Economy")]
        [Tooltip("Base repair cost multiplier per damage point. Used by RepairSystem.")]
        public int repairCostPerDamagePoint = 20;

        [Tooltip("Base purchase price. Used by shop UI.")]
        public int baseValue = 15000;
    }
}

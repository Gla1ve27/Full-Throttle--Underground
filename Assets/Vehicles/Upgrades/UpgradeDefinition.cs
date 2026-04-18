// ============================================================
// UpgradeDefinition.cs
// Part 2 & 5 — Data Model + Visual Kits
// Place at: Assets/FullThrottle/Vehicles/Upgrades/UpgradeDefinition.cs
// ============================================================

using UnityEngine;

namespace Underground.Vehicle
{
    [CreateAssetMenu(
        fileName = "NewUpgrade",
        menuName = "Full Throttle/Vehicle/Upgrade Definition")]
    public class UpgradeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string upgradeId;
        public string displayName;
        public UpgradeCategory category;

        [TextArea(1, 3)]
        public string description;

        [Header("Ownership")]
        public int reputationRequired;
        public int price;

        [Tooltip("Legacy alias for 'price'. Used by older Garage UI scripts.")]
        public int cost;

        [Header("Visual Kit (UpgradeCategory.VisualKit only)")]
        [Tooltip("Override prefab path for this visual kit. Leave empty for performance-only upgrades.")]
        public string visualPrefabOverridePath;

        [Header("Stats Modifier (optional)")]
        [Tooltip("Applied on top of base stats when this upgrade is active.")]
        public VehicleStatsModifier statsModifier;

        [Header("Handling Sub-Upgrade (Visual Kits)")]
        [Tooltip("Optional performance UpgradeDefinition applied alongside a visual kit.")]
        public UpgradeDefinition handlingModifier;

        [Header("Legacy Physics Modifiers")]
        [Tooltip("Additive delta applied to MaxMotorTorque.")]
        public float motorTorqueAdd;
        [Tooltip("Additive delta applied to MaxBrakeTorque.")]
        public float brakeTorqueAdd;
        [Tooltip("Additive delta applied to ForwardStiffness.")]
        public float forwardStiffnessAdd;
        [Tooltip("Additive delta applied to SidewaysStiffness.")]
        public float sidewaysStiffnessAdd;
        [Tooltip("Additive delta applied to Spring.")]
        public float springAdd;
        [Tooltip("Additive delta applied to Damper.")]
        public float damperAdd;
        [Tooltip("Additive delta applied to Mass. Negative = weight reduction.")]
        public float massDelta;
    }

    /// <summary>
    /// Additive stats modifier applied when an upgrade is equipped.
    /// All values are deltas — positive increases the stat.
    /// </summary>
    [System.Serializable]
    public class VehicleStatsModifier
    {
        [Range(-1f, 1f)] public float frontGripDelta;
        [Range(-1f, 1f)] public float rearGripDelta;
        [Range(-1f, 1f)] public float steeringResponseDelta;
        [Range(-1f, 1f)] public float driftAssistDelta;
        [Range(-200f, 200f)] public float horsepowerDelta;
        [Range(-200f, 200f)] public float torqueDelta;
        [Range(-500f, 500f)] public float massDelta;
    }
}

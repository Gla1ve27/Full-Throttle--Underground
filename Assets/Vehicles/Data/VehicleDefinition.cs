// ============================================================
// VehicleDefinition.cs
// Part 2 — Data Model and ScriptableObjects
// Place at: Assets/FullThrottle/Vehicles/Data/VehicleDefinition.cs
// ============================================================

using UnityEngine;

namespace Underground.Vehicle
{
    [CreateAssetMenu(
        fileName = "NewVehicleDefinition",
        menuName = "Full Throttle/Vehicle/Vehicle Definition")]
    public class VehicleDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Canonical lore ID. Must match a VehicleRoster constant exactly.")]
        public string vehicleId;

        [Tooltip("Display name shown in UI.")]
        public string displayName;

        [Tooltip("Optional manufacturer lore name.")]
        public string manufacturerLoreName;

        [TextArea(2, 4)]
        public string description;

        [Header("Classification")]
        public VehicleArchetype archetype;
        public DrivetrainType drivetrain;

        [Header("Asset References")]
        [Tooltip("Path to the visual prefab. Used by PlayerCarCatalog for runtime loading.")]
        public string visualPrefabPath;

        [Tooltip("Path to the stats asset. Optional — can be null if stats are inline.")]
        public string statsAssetPath;

        [Tooltip("Inline stats reference. Preferred over statsAssetPath.")]
        public VehicleStatsData stats;

        [Header("Prefab Setup")]
        public CarWheelMapping wheelMapping;

        [Tooltip("Body drop offset for showroom presentation.")]
        public float showroomBodyDrop = -0.16f;

        [Tooltip("Whether this car uses detached wheel visuals.")]
        public bool useDetachedWheelVisuals = true;

        [Header("Progression")]
        [Tooltip("Reputation required to unlock this car.")]
        public int reputationRequired = 0;

        [Tooltip("Purchase price in session cash.")]
        public int price = 0;

        [Header("Upgrades")]
        [Tooltip("Direct references to upgrade assets. Used for visual kits and performance mods.")]
        public UpgradeDefinition[] availableUpgrades;

        [Tooltip("Upgrade IDs available for this vehicle. Legacy/alternate lookup method.")]
        public string[] availableUpgradeIds;

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the VehicleStatsData from the direct reference,
        /// falling back to path-based loading in-editor.
        /// </summary>
        public VehicleStatsData LoadStats()
        {
            if (stats != null) return stats;

#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(statsAssetPath))
            {
                stats = UnityEditor.AssetDatabase.LoadAssetAtPath<VehicleStatsData>(statsAssetPath);
            }
#endif
            return stats;
        }

        /// <summary>
        /// Loads the visual prefab.
        /// </summary>
        public GameObject LoadVisualPrefab()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(visualPrefabPath))
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(visualPrefabPath);
            }
#endif
            return null;
        }

        /// <summary>
        /// Returns true if the wheel mapping has been explicitly authored.
        /// </summary>
        public bool HasAuthoredWheelMapping => wheelMapping != null && wheelMapping.IsAuthored;
    }
}

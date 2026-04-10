// ============================================================
// PlayerCarAppearanceController.cs
// Part 5 — Visual Kits and Appearance Overrides
// Place at: Assets/FullThrottle/Vehicles/Visual/PlayerCarAppearanceController.cs
//
// Handles visual kit selection for hero car (Solstice Type-S).
// rmcar26_b/c/d are now visual kit upgrades, NOT separate roster entries.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Underground.Save;

namespace Underground.Vehicle
{
    public class VisualKitResolver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PersistentProgressManager progressManager;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the best visual kit prefab path for the given vehicle ID,
        /// or null if no kit is owned / applicable.
        /// Uses highest-tier owned kit automatically.
        /// </summary>
        public string ResolveVisualPrefabPath(string vehicleId)
        {
            // Only hero car has kits right now
            if (vehicleId != VehicleRoster.HeroCarId) return null;

            // Check kits from highest to lowest tier
            string[] kitOrder = {
                VehicleRoster.SolsticeKitD,
                VehicleRoster.SolsticeKitC,
                VehicleRoster.SolsticeKitB
            };

            foreach (string kitId in kitOrder)
            {
                if (OwnsUpgrade(kitId))
                {
                    string path = GetKitPrefabPath(kitId);
                    if (!string.IsNullOrEmpty(path)) return path;
                }
            }

            return null; // use base prefab
        }

        /// <summary>
        /// Returns the stats modifier for the active visual kit (if any).
        /// Returns null if no modifier kit is active.
        /// </summary>
        public VehicleStatsModifier ResolveStatsModifier(string vehicleId)
        {
            if (vehicleId != VehicleRoster.HeroCarId) return null;

            string[] kitOrder = {
                VehicleRoster.SolsticeKitD,
                VehicleRoster.SolsticeKitC,
                VehicleRoster.SolsticeKitB
            };

            foreach (string kitId in kitOrder)
            {
                if (OwnsUpgrade(kitId))
                {
                    UpgradeDefinition def = LoadUpgradeDefinition(kitId);
                    if (def?.statsModifier != null) return def.statsModifier;
                }
            }

            return null;
        }

        // ── Private ──────────────────────────────────────────────────────────

        private bool OwnsUpgrade(string upgradeId)
        {
            if (progressManager == null) return false;
            return progressManager.HasPurchasedUpgrade(upgradeId);
        }

        private string GetKitPrefabPath(string kitId)
        {
            UpgradeDefinition def = LoadUpgradeDefinition(kitId);
            return def?.visualPrefabOverridePath;
        }

        private UpgradeDefinition LoadUpgradeDefinition(string upgradeId)
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets(
                $"t:UpgradeDefinition {upgradeId}");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                UpgradeDefinition def =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
                if (def != null && def.upgradeId == upgradeId) return def;
            }
#endif
            // Runtime: load from Resources
            return Resources.Load<UpgradeDefinition>($"Upgrades/{upgradeId}");
        }
    }
}

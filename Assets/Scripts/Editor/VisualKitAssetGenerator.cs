// ─────────────────────────────────────────────────────────────────────────────
//  Part 5 — Visual Kit Asset Generator
//  Creates UpgradeDefinition assets for hero car visual kits (Solstice Type-S)
//  and any future vehicle kit definitions.
//
//  Menu: Underground → Vehicles → Generate Visual Kit Assets
// ─────────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Underground.Progression;
using Underground.Vehicle;

namespace Underground.Editor
{
    public static class VisualKitAssetGenerator
    {
        private const string MenuPath = VehicleConstants.VehicleMenuRoot + "Generate Visual Kit Assets";

        [MenuItem(MenuPath, priority = 110)]
        public static void GenerateAll()
        {
            int created = 0;

            // ── Solstice Type-S Visual Kits ──
            // Old rmcar26_b, rmcar26_c, rmcar26_d become visual kit upgrades
            created += CreateKitIfMissing(new KitSpec
            {
                vehicleId = "solstice_type_s",
                kitId = "solstice_type_s_kit_b",
                displayName = "Solstice Type-S Kit B",
                description = "Aggressive street kit with wider fenders and low front splitter.",
                visualPrefabPath = "Assets/RealisticMobileCars - Pro3DModels/RMCar26/Prefabs/RMCar26_b.prefab",
                cost = 8000,
                reputationRequired = 500,
                motorTorqueBonus = 15f,
                massChange = -10f,
            });

            created += CreateKitIfMissing(new KitSpec
            {
                vehicleId = "solstice_type_s",
                kitId = "solstice_type_s_kit_c",
                displayName = "Solstice Type-S Kit C",
                description = "Track-focused aero kit with rear wing and canards.",
                visualPrefabPath = "Assets/RealisticMobileCars - Pro3DModels/RMCar26/Prefabs/RMCar26_c.prefab",
                cost = 15000,
                reputationRequired = 1200,
                motorTorqueBonus = 30f,
                forwardStiffnessBonus = 0.05f,
                sidewaysStiffnessBonus = 0.08f,
                massChange = -20f,
            });

            created += CreateKitIfMissing(new KitSpec
            {
                vehicleId = "solstice_type_s",
                kitId = "solstice_type_s_kit_d",
                displayName = "Solstice Type-S Kit D",
                description = "Full widebody transformation with maximum downforce package.",
                visualPrefabPath = "Assets/RealisticMobileCars - Pro3DModels/RMCar26/Prefabs/RMCar26_d.prefab",
                cost = 25000,
                reputationRequired = 2500,
                motorTorqueBonus = 50f,
                forwardStiffnessBonus = 0.10f,
                sidewaysStiffnessBonus = 0.12f,
                springBonus = 2000f,
                massChange = -30f,
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[VisualKitAssetGenerator] Done. Created {created} kit asset(s).");
            EditorUtility.DisplayDialog(
                "Visual Kit Generator",
                $"Created {created} visual kit asset(s).",
                "OK");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Kit Specification
        // ─────────────────────────────────────────────────────────────────────

        private struct KitSpec
        {
            public string vehicleId;
            public string kitId;
            public string displayName;
            public string description;
            public string visualPrefabPath;
            public int cost;
            public int reputationRequired;
            public float motorTorqueBonus;
            public float forwardStiffnessBonus;
            public float sidewaysStiffnessBonus;
            public float springBonus;
            public float massChange;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Asset Creation
        // ─────────────────────────────────────────────────────────────────────

        private static int CreateKitIfMissing(KitSpec spec)
        {
            string folder = $"{VehicleConstants.UpgradesRoot}/{spec.vehicleId}";
            string kitPath = $"{folder}/{spec.kitId}.asset";

            if (File.Exists(kitPath))
            {
                Debug.Log($"[VisualKitAssetGenerator] Skipping {spec.kitId} — already exists.");
                return 0;
            }

            EnsureFolder(folder);

            // ── Create the handling modifier (optional stats tweaks) ──
            UpgradeDefinition handlingMod = null;
            bool hasHandlingMod = spec.motorTorqueBonus != 0f || spec.forwardStiffnessBonus != 0f
                || spec.sidewaysStiffnessBonus != 0f || spec.springBonus != 0f || spec.massChange != 0f;

            if (hasHandlingMod)
            {
                handlingMod = ScriptableObject.CreateInstance<UpgradeDefinition>();
                handlingMod.upgradeId = $"{spec.kitId}_handling";
                handlingMod.displayName = $"{spec.displayName} Handling Modifier";
                handlingMod.category = UpgradeCategory.Performance;
                handlingMod.motorTorqueAdd = spec.motorTorqueBonus;
                handlingMod.forwardStiffnessAdd = spec.forwardStiffnessBonus;
                handlingMod.sidewaysStiffnessAdd = spec.sidewaysStiffnessBonus;
                handlingMod.springAdd = spec.springBonus;
                handlingMod.massDelta = spec.massChange;

                string modPath = $"{folder}/{spec.kitId}_handling.asset";
                AssetDatabase.CreateAsset(handlingMod, modPath);
            }

            // ── Create the visual kit ──
            UpgradeDefinition kit = ScriptableObject.CreateInstance<UpgradeDefinition>();
            kit.upgradeId = spec.kitId;
            kit.displayName = spec.displayName;
            kit.category = UpgradeCategory.VisualKit;
            kit.cost = spec.cost;
            kit.reputationRequired = spec.reputationRequired;
            kit.visualPrefabOverridePath = spec.visualPrefabPath;
            kit.handlingModifier = handlingMod;

            AssetDatabase.CreateAsset(kit, kitPath);
            Debug.Log($"[VisualKitAssetGenerator] Created {spec.kitId} → {kitPath}");
            return 1;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
#endif

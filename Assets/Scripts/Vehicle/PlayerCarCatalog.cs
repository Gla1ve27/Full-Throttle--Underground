using System.Collections.Generic;
using Underground.Save;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Underground.Vehicle
{
    /// <summary>
    /// Authored wheel transform paths for a single car prefab.
    /// If all four paths are null/empty, the system falls back to generic discovery.
    /// </summary>
    [System.Serializable]
    public sealed class CarWheelMapping
    {
        public string frontLeftPath;
        public string frontRightPath;
        public string rearLeftPath;
        public string rearRightPath;

        public bool IsAuthored =>
            !string.IsNullOrEmpty(frontLeftPath) ||
            !string.IsNullOrEmpty(frontRightPath) ||
            !string.IsNullOrEmpty(rearLeftPath) ||
            !string.IsNullOrEmpty(rearRightPath);

        public CarWheelMapping() { }

        public CarWheelMapping(string fl, string fr, string rl, string rr)
        {
            frontLeftPath = fl;
            frontRightPath = fr;
            rearLeftPath = rl;
            rearRightPath = rr;
        }
    }

    public readonly struct PlayerCarDefinition
    {
        public PlayerCarDefinition(
            string carId,
            string displayName,
            string visualPrefabPath,
            string statsAssetPath = null,
            CarWheelMapping wheelMapping = null,
            float showroomBodyDrop = -0.16f,
            bool useDetachedWheelVisuals = true)
        {
            CarId = carId;
            DisplayName = displayName;
            VisualPrefabPath = visualPrefabPath;
            StatsAssetPath = statsAssetPath;
            Stats = null;
            WheelMapping = wheelMapping;
            ShowroomBodyDrop = showroomBodyDrop;
            UseDetachedWheelVisuals = useDetachedWheelVisuals;
        }

        public PlayerCarDefinition(VehicleDefinition asset)
        {
            CarId = asset.vehicleId;
            DisplayName = asset.displayName;
            VisualPrefabPath = asset.visualPrefabPath;
            StatsAssetPath = asset.statsAssetPath;
            Stats = asset.stats;
            WheelMapping = asset.wheelMapping;
            ShowroomBodyDrop = asset.showroomBodyDrop;
            UseDetachedWheelVisuals = asset.useDetachedWheelVisuals;
        }

        public string CarId { get; }
        public string DisplayName { get; }
        public string VisualPrefabPath { get; }
        public string StatsAssetPath { get; }
        public VehicleStatsData Stats { get; }
        public CarWheelMapping WheelMapping { get; }
        public float ShowroomBodyDrop { get; }
        public bool UseDetachedWheelVisuals { get; }

        public bool HasAuthoredWheelMapping => WheelMapping != null && WheelMapping.IsAuthored;

        public GameObject LoadVisualPrefab()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
#else
            return null;
#endif
        }

        public VehicleStatsData LoadStatsAsset()
        {
            if (Stats != null)
            {
                return Stats;
            }

            if (string.IsNullOrEmpty(StatsAssetPath))
            {
                return null;
            }

#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<VehicleStatsData>(StatsAssetPath);
#else
            return null;
#endif
        }
    }

    /// <summary>
    /// Current vehicle catalog with hardcoded roster definitions.
    /// This class is transitional — once Part 2 migrates all cars to
    /// VehicleDefinition ScriptableObject assets, the hardcoded arrays
    /// will be replaced by asset-based loading.
    ///
    /// For lore-friendly ID migration to the new roster, see <see cref="VehicleRoster"/>.
    /// </summary>
    public static class PlayerCarCatalog
    {
        public const string StarterCarId = "rmcar26";

        public static readonly string[] DefaultOwnedCarIds =
        {
            "solstice_type_s",
            "maverick_vengeance_srt",
            "zodic_s_classic",
            "protoso_c16",
            "weaver_pup_s",
            "stratos_element_9",
            "reizan_gt_rb",
            "reizan_icon_iv",
            "uruk_grinder_4x4",
            "reizan_vanguard_34",
            "cyro_monolith",
            "hanse_executive",
            "rmcar26",
            "rmcar26_b",
            "rmcar26_c",
            "rmcar26_d",
            "reizan_350z"
        };

        // ------------------------------------------------------------------
        // Legacy ID migration table.
        // Keys are old IDs that might exist in save files;
        // values are the current canonical ID they map to.
        //
        // NOTE: Lore-friendly IDs (e.g. "solstice_type_s") are reverse-mapped
        // to old IDs so PlayerCarCatalog can still resolve them to Definitions[].
        // Once Part 7 completes, this reverse mapping becomes unnecessary.
        // ------------------------------------------------------------------
        private static readonly Dictionary<string, string> LegacyIdMap = new Dictionary<string, string>
        {
            { "starter_car", "rmcar26" },
            { "starter", "rmcar26" },
            { "default_car", "rmcar26" },
            { "default", "rmcar26" },
            { "Simple Retro Car", "simple_retro_car" },
            { "simple retro car", "simple_retro_car" },
            { "Arcade Car 1", "arcade_car_1" },
            { "Arcade Car 2", "arcade_car_2" },
            { "Arcade Car 3", "arcade_car_3" },
            { "Arcade Car 4", "arcade_car_4" },
            { "Arcade Car 5", "arcade_car_5" },
            { "Arcade Car 6", "arcade_car_6" },
            { "Arcade Car 7", "arcade_car_7" },
            { "Arcade Car 8", "arcade_car_8" },
            { "Arcade Car 9", "arcade_car_9" },
            { "Arcade Car 10", "arcade_car_10" },
            { "arcade car 1", "arcade_car_1" },
            { "arcade car 2", "arcade_car_2" },
            { "arcade car 3", "arcade_car_3" },
            { "arcade car 4", "arcade_car_4" },
            { "arcade car 5", "arcade_car_5" },
            { "arcade car 6", "arcade_car_6" },
            { "arcade car 7", "arcade_car_7" },
            { "arcade car 8", "arcade_car_8" },
            { "arcade car 9", "arcade_car_9" },
            { "arcade car 10", "arcade_car_10" },
            { "Car 1", "arcade_car_1" },
            { "Car 2", "arcade_car_2" },
            { "Car 3", "arcade_car_3" },
            { "Car 4", "arcade_car_4" },
            { "Car 5", "arcade_car_5" },
            { "Car 6", "arcade_car_6" },
            { "Car 7", "arcade_car_7" },
            { "Car 8", "arcade_car_8" },
            { "Car 9", "arcade_car_9" },
            { "Car 10", "arcade_car_10" },

            // ── Lore-friendly → old ID reverse bridge (transition period) ──
            { "solstice_type_s",       "rmcar26" },
            { "zodic_s_classic",       "simple_retro_car" },
            { "maverick_vengeance_srt", "arcade_car_7" },
            { "protoso_c16",           "arcade_car_2" },
            { "weaver_pup_s",          "arcade_car_3" },
            { "stratos_element_9",     "arcade_car_1" },
            { "reizan_gt_rb",          "arcade_car_4" },
            { "reizan_icon_iv",        "arcade_car_5" },
            { "uruk_grinder_4x4",      "arcade_car_8" },
            { "reizan_vanguard_34",    "arcade_car_6" },
            { "cyro_monolith",         "american_sedan" },
            { "hanse_executive",       "american_sedan_stylized" },
            { "reizan_350z",           "reizan_350z" },
        };

        // ------------------------------------------------------------------
        // Authored wheel mappings per RMCar26 variant.
        // Paths are relative to the instantiated visual prefab root.
        // ------------------------------------------------------------------
        private static readonly CarWheelMapping RMCar26Wheels = new CarWheelMapping(
            "RMCar26_WheelFrontLeft/RMCar26WheelFrontLeft",
            "RMCar26_WheelFrontRight/RMCar26WheelFrontRight",
            "RMCar26_WheelRearLeft/RMCar26WheelRearLeft",
            "RMCar26_WheelRearRight/RMCar26WheelRearRight"
        );

        private static readonly CarWheelMapping RMCar26_B_Wheels = new CarWheelMapping(
            "RMCar26_WheelFrontLeft/RMCar26WheelFrontLeft",
            "RMCar26_WheelFrontRight/RMCar26WheelFrontRight",
            "RMCar26_WheelRearLeft/RMCar26WheelRearLeft",
            "RMCar26_WheelRearRight/RMCar26WheelRearRight"
        );

        private static readonly CarWheelMapping RMCar26_C_Wheels = new CarWheelMapping(
            "RMCar26_WheelFrontLeft/RMCar26WheelFrontLeft",
            "RMCar26_WheelFrontRight/RMCar26WheelFrontRight",
            "RMCar26_WheelRearLeft/RMCar26WheelRearLeft",
            "RMCar26_WheelRearRight/RMCar26WheelRearRight"
        );

        private static readonly CarWheelMapping RMCar26_D_Wheels = new CarWheelMapping(
            "RMCar26_WheelFrontLeft/RMCar26WheelFrontLeft",
            "RMCar26_WheelFrontRight/RMCar26WheelFrontRight",
            "RMCar26_WheelRearLeft/RMCar26WheelRearLeft",
            "RMCar26_WheelRearRight/RMCar26WheelRearRight"
        );

        // American Sedan packs both rear wheels into one combined rear mesh.
        private static readonly CarWheelMapping AmericanSedanWheels = new CarWheelMapping(
            "Car_Wheels/C_WheelFL",
            "Car_Wheels/C_WheelFR",
            "Car_Wheels/C_Wheels_B",
            "Car_Wheels/C_Wheels_B"
        );

        private static readonly CarWheelMapping AmericanSedanStylizedWheels = new CarWheelMapping(
            "CS_WheelFL",
            "CS_WheelFR",
            "CS_Wheels_B",
            "CS_Wheels_B"
        );

        private static readonly CarWheelMapping SimpleRetroCarWheels = new CarWheelMapping(
            "FL",
            "FR",
            "RL",
            "RR"
        );

        private static readonly CarWheelMapping ArcadeCarWheels = new CarWheelMapping(
            "Front Left Wheel",
            "Front Right Wheel",
            "Rear Left Wheel",
            "Rear Right Wheel"
        );

        private static readonly CarWheelMapping Reizan350ZWheels = new CarWheelMapping(
            "Wheel_FL",
            "Wheel_FR",
            "Wheel_LR",
            "Wheel_RR"
        );
        // ------------------------------------------------------------------
        // Full car roster with per-car definitions.
        // StatsAssetPath is optional; null means "keep base rig stats".
        // ------------------------------------------------------------------
        private static readonly PlayerCarDefinition[] Definitions =
        {
            new PlayerCarDefinition(
                "rmcar26",
                "RMCar26",
                "Assets/RealisticMobileCars - Pro3DModels/RMCar26/Prefabs/RMCar26.prefab",
                null,
                RMCar26Wheels,
                -0.16f),
            new PlayerCarDefinition(
                "rmcar26_b",
                "RMCar26 B",
                "Assets/RealisticMobileCars - Pro3DModels/RMCar26/Prefabs/RMCar26_B.prefab",
                null,
                RMCar26_B_Wheels,
                -0.16f),
            new PlayerCarDefinition(
                "rmcar26_c",
                "RMCar26 C",
                "Assets/RealisticMobileCars - Pro3DModels/RMCar26/Prefabs/RMCar26_C.prefab",
                null,
                RMCar26_C_Wheels,
                -0.16f),
            new PlayerCarDefinition(
                "rmcar26_d",
                "RMCar26 D",
                "Assets/RealisticMobileCars - Pro3DModels/RMCar26/Prefabs/RMCar26_D.prefab",
                null,
                RMCar26_D_Wheels,
                -0.16f),
            new PlayerCarDefinition(
                "reizan_350z",
                "Reizan 350Z",
                "Assets/Blender3DByBads/350z.fbx",
                null,
                Reizan350ZWheels,
                -0.16f
            ),
        };

        public static IReadOnlyList<PlayerCarDefinition> GetDefinitions()
        {
            List<PlayerCarDefinition> all = new List<PlayerCarDefinition>();

            // Add new asset-based definitions
            if (VehicleDefinitionCatalog.Instance != null)
            {
                foreach (var asset in VehicleDefinitionCatalog.Instance.AllDefinitions)
                {
                    all.Add(new PlayerCarDefinition(asset));
                }
            }

            // Add legacy definitions (avoiding duplicates by ID)
            foreach (var legacy in Definitions)
            {
                bool alreadyAdded = false;
                foreach (var added in all)
                {
                    if (added.CarId == legacy.CarId)
                    {
                        alreadyAdded = true;
                        break;
                    }
                }

                if (!alreadyAdded)
                {
                    all.Add(legacy);
                }
            }

            return all;
        }

        public static bool TryGetDefinition(string carId, out PlayerCarDefinition definition)
        {
            // Transparently resolve legacy IDs before lookup.
            string resolvedId = MigrateCarId(carId);

            // ── Part 7: Asset-Based Discovery ──
            // If the new catalog has a definition asset for this ID, use it.
            // This prioritizes the new data-driven system over hardcoded arrays.
            if (VehicleDefinitionCatalog.Instance != null && 
                VehicleDefinitionCatalog.Instance.TryGetDefinition(resolvedId, out VehicleDefinition asset))
            {
                definition = new PlayerCarDefinition(asset);
                return true;
            }

            for (int i = 0; i < Definitions.Length; i++)
            {
                if (Definitions[i].CarId == resolvedId)
                {
                    definition = Definitions[i];
                    return true;
                }
            }

            definition = GetStarterDefinition();
            return false;
        }

        public static PlayerCarDefinition GetStarterDefinition()
        {
            for (int i = 0; i < Definitions.Length; i++)
            {
                if (Definitions[i].CarId == StarterCarId)
                {
                    return Definitions[i];
                }
            }

            return Definitions[0];
        }

        /// <summary>
        /// Resolves a potentially-legacy car ID to its canonical current ID.
        /// Returns the input unchanged if it is already canonical.
        /// </summary>
        public static string MigrateCarId(string rawId)
        {
            if (string.IsNullOrEmpty(rawId))
            {
                return StarterCarId;
            }

            if (LegacyIdMap.TryGetValue(rawId, out string canonical))
            {
                return canonical;
            }

            string normalized = rawId.Trim();
            if (LegacyIdMap.TryGetValue(normalized, out canonical))
            {
                return canonical;
            }

            string slug = normalized
                .ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("-", "_");

            if (LegacyIdMap.TryGetValue(slug, out canonical))
            {
                return canonical;
            }

            for (int i = 0; i < Definitions.Length; i++)
            {
                if (Definitions[i].CarId == normalized || Definitions[i].CarId == slug)
                {
                    return Definitions[i].CarId;
                }

                if (string.Equals(Definitions[i].DisplayName, normalized, System.StringComparison.OrdinalIgnoreCase))
                {
                    return Definitions[i].CarId;
                }
            }

            // ── Final fallback: try lore roster migration ──
            string loreMigrated = VehicleRoster.MigrateLegacyId(rawId);
            if (loreMigrated != rawId)
            {
                // The lore ID won't match any Definitions[] entry yet
                // (that happens in Part 2), but we return it so systems
                // that understand the new roster can use it.
                return loreMigrated;
            }

            return rawId;
        }

        public static List<PlayerCarDefinition> GetOwnedCars(PersistentProgressManager progressManager)
        {
            List<PlayerCarDefinition> ownedCars = new List<PlayerCarDefinition>();
            var allDefinitions = GetDefinitions();

            if (progressManager == null)
            {
                for (int i = 0; i < allDefinitions.Count; i++)
                {
                    ownedCars.Add(allDefinitions[i]);
                }

                return ownedCars;
            }

            for (int i = 0; i < allDefinitions.Count; i++)
            {
                if (progressManager.OwnsCar(allDefinitions[i].CarId))
                {
                    ownedCars.Add(allDefinitions[i]);
                }
            }

            if (ownedCars.Count == 0)
            {
                ownedCars.Add(GetStarterDefinition());
            }

            return ownedCars;
        }
    }
}

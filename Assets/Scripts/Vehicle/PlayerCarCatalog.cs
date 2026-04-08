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
            WheelMapping = wheelMapping;
            ShowroomBodyDrop = showroomBodyDrop;
            UseDetachedWheelVisuals = useDetachedWheelVisuals;
        }

        public string CarId { get; }
        public string DisplayName { get; }
        public string VisualPrefabPath { get; }
        public string StatsAssetPath { get; }
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

    public static class PlayerCarCatalog
    {
        public const string StarterCarId = "rmcar26";

        public static readonly string[] DefaultOwnedCarIds =
        {
            "simple_retro_car",
            "arcade_car_1",
            "arcade_car_2",
            "arcade_car_3",
            "arcade_car_4",
            "arcade_car_5",
            "arcade_car_6",
            "arcade_car_7",
            "arcade_car_8",
            "arcade_car_9",
            "arcade_car_10",
            "american_sedan",
            "american_sedan_stylized",
            "rmcar26",
            "rmcar26_b",
            "rmcar26_c",
            "rmcar26_d"
        };

        // ------------------------------------------------------------------
        // Legacy ID migration table.
        // Keys are old IDs that might exist in save files;
        // values are the current canonical ID they map to.
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

        // ------------------------------------------------------------------
        // Full car roster with per-car definitions.
        // StatsAssetPath is optional; null means "keep base rig stats".
        // ------------------------------------------------------------------
        private static readonly PlayerCarDefinition[] Definitions =
        {
            new PlayerCarDefinition(
                "simple_retro_car",
                "Simple Retro Car",
                "Assets/Polyeler/Simple Retro Car/Prefabs/Simple Retro Car.prefab",
                null,
                SimpleRetroCarWheels,
                -0.16f),
            new PlayerCarDefinition(
                "arcade_car_1",
                "Arcade Car 1",
                "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 1.prefab",
                null,
                ArcadeCarWheels,
                -0.16f),
            new PlayerCarDefinition(
                "arcade_car_2",
                "Arcade Car 2",
                "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 2.prefab",
                null,
                ArcadeCarWheels,
                -0.16f),
            new PlayerCarDefinition(
                "arcade_car_3",
                "Arcade Car 3",
                "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 3.prefab",
                null,
                ArcadeCarWheels,
                -0.16f),
            new PlayerCarDefinition(
                "arcade_car_4",
                "Arcade Car 4",
                "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 4.prefab",
                null,
                ArcadeCarWheels,
                -0.16f),
            new PlayerCarDefinition(
                "arcade_car_5",
                "Arcade Car 5",
                "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 5.prefab",
                null,
                ArcadeCarWheels,
                -0.16f),
            new PlayerCarDefinition(
                "arcade_car_6",
                "Arcade Car 6",
                "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 6.prefab",
                null,
                ArcadeCarWheels,
                -0.16f),
            new PlayerCarDefinition(
                "arcade_car_7",
                "Arcade Car 7",
                "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 7.prefab",
                null,
                ArcadeCarWheels,
                -0.16f),
            new PlayerCarDefinition(
                "arcade_car_8",
                "Arcade Car 8",
                "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 8.prefab",
                null,
                ArcadeCarWheels,
                -0.16f),
            new PlayerCarDefinition(
                "arcade_car_9",
                "Arcade Car 9",
                "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 9.prefab",
                null,
                ArcadeCarWheels,
                -0.16f),
            new PlayerCarDefinition(
                "arcade_car_10",
                "Arcade Car 10",
                "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 10.prefab",
                null,
                ArcadeCarWheels,
                -0.16f),
            new PlayerCarDefinition(
                "american_sedan",
                "American Sedan",
                "Assets/High Matters/Free American Sedans/Prefabs/Car.prefab",
                null,
                AmericanSedanWheels,
                -0.16f),
            new PlayerCarDefinition(
                "american_sedan_stylized",
                "American Sedan Stylized",
                "Assets/High Matters/Free American Sedans/Prefabs/Car_stylized.prefab",
                null,
                AmericanSedanStylizedWheels,
                -0.16f),
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
                -0.16f)
        };

        public static IReadOnlyList<PlayerCarDefinition> GetDefinitions()
        {
            return Definitions;
        }

        public static bool TryGetDefinition(string carId, out PlayerCarDefinition definition)
        {
            // Transparently resolve legacy IDs before lookup.
            string resolvedId = MigrateCarId(carId);

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

            return rawId;
        }

        public static List<PlayerCarDefinition> GetOwnedCars(PersistentProgressManager progressManager)
        {
            List<PlayerCarDefinition> ownedCars = new List<PlayerCarDefinition>();

            if (progressManager == null)
            {
                for (int i = 0; i < Definitions.Length; i++)
                {
                    ownedCars.Add(Definitions[i]);
                }

                return ownedCars;
            }

            for (int i = 0; i < Definitions.Length; i++)
            {
                if (progressManager.OwnsCar(Definitions[i].CarId))
                {
                    ownedCars.Add(Definitions[i]);
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

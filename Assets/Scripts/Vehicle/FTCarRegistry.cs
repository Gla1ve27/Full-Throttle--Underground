using System.Collections.Generic;
using FullThrottle.SacredCore.Runtime;
using UnityEngine;

namespace FullThrottle.SacredCore.Vehicle
{
    [DefaultExecutionOrder(-9850)]
    public sealed class FTCarRegistry : MonoBehaviour
    {
        [SerializeField] private List<FTCarDefinition> cars = new();
        [Header("Migration")]
        [SerializeField] private bool includeLegacyPlayerCatalogInEditor = true;
        [SerializeField] private GameObject sharedRuntimeVehiclePrefab;
        [SerializeField] private string migrationStarterCarId = "reizan_350z";

        private readonly Dictionary<string, FTCarDefinition> map = new();
        private readonly List<FTCarDefinition> runtimeCars = new();
        public IReadOnlyList<FTCarDefinition> Cars => runtimeCars;

        private void Awake()
        {
            FTServices.Register(this);
            Rebuild();
        }

        public void Rebuild()
        {
            map.Clear();
            runtimeCars.Clear();
            foreach (FTCarDefinition car in cars)
            {
                if (car == null || string.IsNullOrWhiteSpace(car.carId)) continue;
                runtimeCars.Add(car);
                map[car.carId] = car;
            }

            AddLegacyRosterDuringMigration();
        }

        public bool TryGet(string carId, out FTCarDefinition definition)
        {
            return map.TryGetValue(NormalizeMigrationCarId(carId), out definition);
        }

        public FTCarDefinition Get(string carId)
        {
            if (TryGet(carId, out FTCarDefinition definition))
            {
                return definition;
            }

            return GetStarter();
        }

        public FTCarDefinition GetStarter()
        {
            if (!string.IsNullOrWhiteSpace(migrationStarterCarId) && map.TryGetValue(migrationStarterCarId, out FTCarDefinition migratedStarter))
            {
                return migratedStarter;
            }

            foreach (FTCarDefinition car in runtimeCars)
            {
                if (car != null && car.starterOwned)
                {
                    return car;
                }
            }

            return runtimeCars.Count > 0 ? runtimeCars[0] : null;
        }

        public string ValidateOrFallback(string requestedCarId)
        {
            string normalized = NormalizeMigrationCarId(requestedCarId);
            return TryGet(normalized, out FTCarDefinition definition) && definition != null
                ? normalized
                : GetStarter()?.carId ?? string.Empty;
        }

        private string NormalizeMigrationCarId(string carId)
        {
            if (string.IsNullOrWhiteSpace(carId))
            {
                return carId;
            }

            if ((carId == "starter_01" || carId == "starter_stock" || carId == "starter_car") &&
                !string.IsNullOrWhiteSpace(migrationStarterCarId) &&
                map.ContainsKey(migrationStarterCarId))
            {
                return migrationStarterCarId;
            }

            return carId;
        }

        private GameObject ResolveSharedRuntimePrefab()
        {
            if (sharedRuntimeVehiclePrefab != null)
            {
                return sharedRuntimeVehiclePrefab;
            }

            for (int i = 0; i < runtimeCars.Count; i++)
            {
                if (runtimeCars[i] != null && runtimeCars[i].worldPrefab != null)
                {
                    sharedRuntimeVehiclePrefab = runtimeCars[i].worldPrefab;
                    return sharedRuntimeVehiclePrefab;
                }
            }

            return null;
        }

        private void AddLegacyRosterDuringMigration()
        {
#if UNITY_EDITOR
            if (!includeLegacyPlayerCatalogInEditor)
            {
                return;
            }

            GameObject sharedPrefab = ResolveSharedRuntimePrefab();
            if (sharedPrefab == null)
            {
                return;
            }

            IReadOnlyList<Underground.Vehicle.PlayerCarDefinition> legacyDefinitions = Underground.Vehicle.PlayerCarCatalog.GetDefinitions();
            for (int i = 0; i < legacyDefinitions.Count; i++)
            {
                Underground.Vehicle.PlayerCarDefinition legacy = legacyDefinitions[i];
                if (string.IsNullOrWhiteSpace(legacy.CarId) || map.ContainsKey(legacy.CarId))
                {
                    continue;
                }

                FTCarDefinition generated = ScriptableObject.CreateInstance<FTCarDefinition>();
                generated.hideFlags = HideFlags.HideAndDontSave;
                generated.carId = legacy.CarId;
                generated.displayName = legacy.DisplayName;
                generated.vehicleClass = legacy.CarId == migrationStarterCarId ? "Starter" : "Street";
                generated.driveType = "RWD";
                generated.engineCharacterTag = legacy.CarId == "reizan_350z" ? "turbo_i4_hero" : "legacy_roster";
                generated.audioProfileId = legacy.CarId == "reizan_350z" ? "car_27_hero_stock" : "starter_stock";
                generated.garagePreviewRevStyle = "street";
                generated.forcedInductionType = legacy.CarId == "reizan_350z" ? "Turbo" : "";
                generated.audioFamilyTag = legacy.CarId == "reizan_350z" ? "car_27" : "legacy_roster";
                generated.starterOwned = legacy.CarId == migrationStarterCarId;
                generated.worldPrefab = sharedPrefab;
                generated.visualPrefab = legacy.LoadVisualPrefab();
                generated.editorVisualPrefabPath = legacy.VisualPrefabPath;
                generated.feel.acceleration = legacy.CarId == migrationStarterCarId ? 5.2f : 4.5f;
                generated.feel.topSpeed = legacy.CarId == migrationStarterCarId ? 5.0f : 4.5f;
                generated.feel.handling = legacy.CarId == migrationStarterCarId ? 5.2f : 4.5f;

                runtimeCars.Add(generated);
                map[generated.carId] = generated;
            }
#endif
        }
    }
}

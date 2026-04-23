using System.Collections.Generic;
using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Save;
using FullThrottle.SacredCore.Vehicle;
using FullThrottle.SacredCore.World;
using UnityEngine;

namespace FullThrottle.SacredCore.Garage
{
    /// <summary>
    /// Sacred garage loop: browse, commit, continue.
    /// The garage is not just a menu. It is Gla1ve's home base.
    /// </summary>
    [DefaultExecutionOrder(-8000)]
    public sealed class FTGarageDirector : MonoBehaviour
    {
        [SerializeField] private string worldSceneName = "World";
        [SerializeField] private string defaultSpawnPointId = "garage_exit";
        [SerializeField] private bool useLegacyGarageRosterDuringMigration = true;

        private FTSelectedCarRuntime selectedCarRuntime;
        private FTSaveGateway saveGateway;
        private FTWorldTravelDirector worldTravel;
        private FTCarRegistry carRegistry;
        private readonly List<string> ownedCars = new();
        private int currentIndex;

        private void Awake()
        {
            selectedCarRuntime = FTServices.Get<FTSelectedCarRuntime>();
            saveGateway = FTServices.Get<FTSaveGateway>();
            worldTravel = FTServices.Get<FTWorldTravelDirector>();
            carRegistry = FTServices.Get<FTCarRegistry>();
            RebuildOwnedList();
        }

        public void RebuildOwnedList()
        {
            ownedCars.Clear();
            ImportLegacyOwnedCarsDuringMigration();
            selectedCarRuntime.ForceSyncFromProfile();
            foreach (string carId in saveGateway.Profile.ownedCarIds)
            {
                if (carRegistry.TryGet(carId, out _))
                {
                    ownedCars.Add(carId);
                }
            }

            if (ownedCars.Count == 0)
            {
                string fallback = carRegistry.GetStarter()?.carId;
                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    saveGateway.Profile.OwnCar(fallback);
                    ownedCars.Add(fallback);
                }
            }

            int selectedIndex = ownedCars.IndexOf(selectedCarRuntime.CurrentCarId);
            if (selectedIndex < 0 && ownedCars.Count > 0)
            {
                selectedCarRuntime.TrySetCurrentCar(ownedCars[0], true);
                selectedIndex = 0;
                Debug.LogWarning($"[SacredCore] Garage corrected selected car to owned car '{ownedCars[0]}'.");
            }

            currentIndex = Mathf.Max(0, selectedIndex);
            saveGateway.Save();
        }

        public string BrowseNextOwnedCar()
        {
            if (ownedCars.Count == 0) RebuildOwnedList();
            currentIndex = (currentIndex + 1) % ownedCars.Count;
            string carId = ownedCars[currentIndex];
            selectedCarRuntime.TrySetCurrentCar(carId, true);
            return carId;
        }

        public string BrowsePreviousOwnedCar()
        {
            if (ownedCars.Count == 0) RebuildOwnedList();
            currentIndex = (currentIndex - 1 + ownedCars.Count) % ownedCars.Count;
            string carId = ownedCars[currentIndex];
            selectedCarRuntime.TrySetCurrentCar(carId, true);
            return carId;
        }

        public void ContinueToWorld()
        {
            RebuildOwnedList();
            string carId = selectedCarRuntime.ForceSyncFromProfile();
            if (!selectedCarRuntime.TrySetOwnedCurrentCar(carId, "Garage handoff", true))
            {
                Debug.LogError($"[SacredCore] Cannot leave garage. Selected car '{carId}' is not valid/owned.");
                return;
            }

            if (!carRegistry.TryGet(carId, out FTCarDefinition definition) || definition == null || definition.worldPrefab == null)
            {
                Debug.LogError($"[SacredCore] Cannot leave garage. Car '{carId}' has no valid world prefab.");
                return;
            }

            FTGarageShowroomDirector showroom = FindFirstObjectByType<FTGarageShowroomDirector>();
            if (showroom != null && showroom.CurrentDisplayCarId != carId)
            {
                Debug.LogWarning($"[SacredCore] Garage showroom mismatch before handoff. showroom={showroom.CurrentDisplayCarId}, selected={carId}. Rebuilding showroom.");
                showroom.RebuildShowroom();
                if (showroom.CurrentDisplayCarId != carId)
                {
                    Debug.LogError($"[SacredCore] Cannot leave garage. Showroom failed to display selected car '{carId}'.");
                    return;
                }
            }

            saveGateway.Profile.currentCarId = carId;
            saveGateway.Profile.OwnCar(carId);
            saveGateway.Save();
            if (worldTravel == null && !FTServices.TryGet(out worldTravel))
            {
                worldTravel = FindFirstObjectByType<FTWorldTravelDirector>();
            }

            if (worldTravel == null)
            {
                Debug.LogError("[SacredCore] Cannot leave garage. Missing FTWorldTravelDirector.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(worldSceneName))
            {
                Debug.LogError($"[SacredCore] Cannot leave garage. Scene '{worldSceneName}' is not loadable. Check Build Settings scene path.");
                return;
            }

            worldTravel.QueueWorldEntry(carId, defaultSpawnPointId);
            Debug.Log($"[SacredCore] Garage handoff -> world. car={carId}, display={definition.displayName}, scene={worldSceneName}, spawn={defaultSpawnPointId}");
            worldTravel.LoadWorld(worldSceneName);
        }

        private void ImportLegacyOwnedCarsDuringMigration()
        {
#if UNITY_EDITOR
            if (!useLegacyGarageRosterDuringMigration)
            {
                return;
            }

            for (int i = 0; i < Underground.Vehicle.PlayerCarCatalog.DefaultOwnedCarIds.Length; i++)
            {
                string legacyCarId = Underground.Vehicle.PlayerCarCatalog.MigrateCarId(Underground.Vehicle.PlayerCarCatalog.DefaultOwnedCarIds[i]);
                if (!string.IsNullOrWhiteSpace(legacyCarId) && carRegistry.TryGet(legacyCarId, out _))
                {
                    saveGateway.Profile.OwnCar(legacyCarId);
                }
            }
#endif
        }
    }
}

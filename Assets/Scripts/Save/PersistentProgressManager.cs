using System.Collections.Generic;
using UnityEngine;
using Underground.Core.Architecture;
using Underground.Vehicle;

namespace Underground.Save
{
    public class PersistentProgressManager : MonoBehaviour, IProgressService
    {
        [SerializeField] private SaveSystem saveSystem;
        [SerializeField] private string starterCarId = PlayerCarCatalog.StarterCarId;
        [SerializeField] private int starterMoney = 5000;
        [SerializeField] private List<string> defaultOwnedCarIds = new List<string>(PlayerCarCatalog.DefaultOwnedCarIds);
        [SerializeField] private List<string> ownedCarIds = new List<string>();
        [SerializeField] private List<string> purchasedUpgradeIds = new List<string>();

        public int SavedMoney { get; private set; }
        public int SavedReputation { get; private set; }
        public string CurrentOwnedCarId { get; private set; }
        public IReadOnlyList<string> OwnedCarIds => ownedCarIds;
        public IReadOnlyList<string> PurchasedUpgradeIds => purchasedUpgradeIds;
        public float WorldTimeOfDay { get; private set; } = 12f;
        private bool hasLoadedSave;

        private void Awake()
        {
            if (saveSystem == null)
            {
                saveSystem = GetComponent<SaveSystem>() ?? ServiceLocator.ResolveOrNull<ISaveService>() as SaveSystem;
            }

            LoadFromDisk();
            MigrateLegacyCarIds();
            EnsureDefaultProfile();
            ServiceLocator.Register<IProgressService>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<IProgressService>(this);
        }

        public void AddMoney(int amount)
        {
            SavedMoney += Mathf.Max(0, amount);
            ServiceLocator.EventBus.Publish(new MoneyChangedEvent(SavedMoney));
        }

        public bool SpendMoney(int amount)
        {
            if (amount < 0 || SavedMoney < amount)
            {
                return false;
            }

            SavedMoney -= amount;
            ServiceLocator.EventBus.Publish(new MoneyChangedEvent(SavedMoney));
            return true;
        }

        public void AddReputation(int amount)
        {
            SavedReputation += Mathf.Max(0, amount);
            ServiceLocator.EventBus.Publish(new ReputationChangedEvent(SavedReputation));
        }

        public bool OwnsCar(string carId)
        {
            string migratedId = PlayerCarCatalog.MigrateCarId(carId);
            return !string.IsNullOrEmpty(migratedId) && ownedCarIds.Contains(migratedId);
        }

        public void AddOwnedCar(string carId)
        {
            carId = PlayerCarCatalog.MigrateCarId(carId);
            if (string.IsNullOrEmpty(carId))
            {
                return;
            }

            if (!ownedCarIds.Contains(carId))
            {
                ownedCarIds.Add(carId);
            }

            if (string.IsNullOrEmpty(CurrentOwnedCarId))
            {
                CurrentOwnedCarId = carId;
                ServiceLocator.EventBus.Publish(new CurrentCarChangedEvent(CurrentOwnedCarId));
            }
        }

        public void SetCurrentCar(string carId)
        {
            carId = PlayerCarCatalog.MigrateCarId(carId);
            if (OwnsCar(carId))
            {
                CurrentOwnedCarId = carId;
                ServiceLocator.EventBus.Publish(new CurrentCarChangedEvent(CurrentOwnedCarId));
            }
        }

        public bool HasPurchasedUpgrade(string upgradeId)
        {
            return !string.IsNullOrEmpty(upgradeId) && purchasedUpgradeIds.Contains(upgradeId);
        }

        public void RegisterUpgrade(string upgradeId)
        {
            if (!string.IsNullOrEmpty(upgradeId) && !purchasedUpgradeIds.Contains(upgradeId))
            {
                purchasedUpgradeIds.Add(upgradeId);
            }
        }

        public void SaveNow(float worldTime = 12f, string garageScene = "Garage")
        {
            if (saveSystem == null)
            {
                return;
            }

            WorldTimeOfDay = worldTime;

            SaveGameData data = new SaveGameData
            {
                savedMoney = SavedMoney,
                savedReputation = SavedReputation,
                currentOwnedCarId = CurrentOwnedCarId,
                ownedCarIds = new List<string>(ownedCarIds),
                purchasedUpgradeIds = new List<string>(purchasedUpgradeIds),
                worldTimeOfDay = WorldTimeOfDay,
                lastGarageScene = garageScene
            };

            saveSystem.Save(data);
            ServiceLocator.EventBus.Publish(new ProgressSavedEvent(WorldTimeOfDay, garageScene));
        }

        public void LoadFromDisk()
        {
            if (saveSystem == null)
            {
                return;
            }

            SaveGameData data = saveSystem.Load();
            if (data == null)
            {
                return;
            }

            hasLoadedSave = true;
            SavedMoney = data.savedMoney;
            SavedReputation = data.savedReputation;
            CurrentOwnedCarId = data.currentOwnedCarId;
            ownedCarIds = data.ownedCarIds ?? new List<string>();
            purchasedUpgradeIds = data.purchasedUpgradeIds ?? new List<string>();
            WorldTimeOfDay = data.worldTimeOfDay;

            MigrateLegacyCarIds();
            ServiceLocator.EventBus.Publish(new MoneyChangedEvent(SavedMoney));
            ServiceLocator.EventBus.Publish(new ReputationChangedEvent(SavedReputation));
            ServiceLocator.EventBus.Publish(new CurrentCarChangedEvent(CurrentOwnedCarId));
            ServiceLocator.EventBus.Publish(new WorldTimeChangedEvent(WorldTimeOfDay, WorldTimeOfDay >= 20f || WorldTimeOfDay < 6f));
        }

        public void SetWorldTime(float worldTime)
        {
            WorldTimeOfDay = worldTime;
            ServiceLocator.EventBus.Publish(new WorldTimeChangedEvent(WorldTimeOfDay, WorldTimeOfDay >= 20f || WorldTimeOfDay < 6f));
        }

        public void ResetToDefaults()
        {
            SavedMoney = starterMoney;
            SavedReputation = 0;
            CurrentOwnedCarId = starterCarId;
            ownedCarIds = new List<string>();
            purchasedUpgradeIds = new List<string>();
            WorldTimeOfDay = 12f;
            MigrateLegacyCarIds();
            EnsureDefaultProfile();
            ServiceLocator.EventBus.Publish(new MoneyChangedEvent(SavedMoney));
            ServiceLocator.EventBus.Publish(new ReputationChangedEvent(SavedReputation));
            ServiceLocator.EventBus.Publish(new CurrentCarChangedEvent(CurrentOwnedCarId));
            ServiceLocator.EventBus.Publish(new WorldTimeChangedEvent(WorldTimeOfDay, false));
        }

        /// <summary>
        /// Migrates any legacy/obsolete car IDs in owned list and current selection
        /// to their canonical replacements (e.g. "starter_car" → "rmcar26").
        /// Safe to call multiple times; no-ops if nothing needs migration.
        /// </summary>
        private void MigrateLegacyCarIds()
        {
            CurrentOwnedCarId = PlayerCarCatalog.MigrateCarId(CurrentOwnedCarId);

            if (ownedCarIds == null)
            {
                return;
            }

            for (int i = 0; i < ownedCarIds.Count; i++)
            {
                string migrated = PlayerCarCatalog.MigrateCarId(ownedCarIds[i]);
                if (migrated != ownedCarIds[i])
                {
                    // Replace with the canonical ID, avoid duplicates.
                    if (!ownedCarIds.Contains(migrated))
                    {
                        ownedCarIds[i] = migrated;
                    }
                    else
                    {
                        ownedCarIds.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        private void EnsureDefaultProfile()
        {
            if (ownedCarIds == null)
            {
                ownedCarIds = new List<string>();
            }

            if (purchasedUpgradeIds == null)
            {
                purchasedUpgradeIds = new List<string>();
            }

            if (defaultOwnedCarIds == null || defaultOwnedCarIds.Count == 0)
            {
                defaultOwnedCarIds = new List<string>(PlayerCarCatalog.DefaultOwnedCarIds);
            }
            else
            {
                for (int i = 0; i < PlayerCarCatalog.DefaultOwnedCarIds.Length; i++)
                {
                    string catalogCarId = PlayerCarCatalog.DefaultOwnedCarIds[i];
                    if (!string.IsNullOrEmpty(catalogCarId) && !defaultOwnedCarIds.Contains(catalogCarId))
                    {
                        defaultOwnedCarIds.Add(catalogCarId);
                    }
                }
            }

            if (string.IsNullOrEmpty(starterCarId))
            {
                starterCarId = PlayerCarCatalog.StarterCarId;
            }
            else
            {
                starterCarId = PlayerCarCatalog.MigrateCarId(starterCarId);
            }

            for (int i = 0; i < defaultOwnedCarIds.Count; i++)
            {
                string carId = defaultOwnedCarIds[i];
                if (!string.IsNullOrEmpty(carId) && !ownedCarIds.Contains(carId))
                {
                    ownedCarIds.Add(carId);
                }
            }

            if (string.IsNullOrEmpty(CurrentOwnedCarId) || !ownedCarIds.Contains(CurrentOwnedCarId))
            {
                if (!string.IsNullOrEmpty(starterCarId) && ownedCarIds.Contains(starterCarId))
                {
                    CurrentOwnedCarId = starterCarId;
                }
                else if (ownedCarIds.Count > 0)
                {
                    CurrentOwnedCarId = ownedCarIds[0];
                }
            }

            if (!hasLoadedSave && SavedMoney <= 0)
            {
                SavedMoney = starterMoney;
            }
        }
    }
}

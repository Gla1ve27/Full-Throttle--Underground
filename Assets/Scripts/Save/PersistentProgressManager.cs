using System.Collections.Generic;
using UnityEngine;

namespace Underground.Save
{
    public class PersistentProgressManager : MonoBehaviour
    {
        [SerializeField] private SaveSystem saveSystem;
        [SerializeField] private string starterCarId = "starter_car";
        [SerializeField] private int starterMoney = 5000;

        public int SavedMoney { get; private set; }
        public int SavedReputation { get; private set; }
        public string CurrentOwnedCarId { get; private set; }
        public List<string> OwnedCarIds { get; private set; } = new List<string>();
        public List<string> PurchasedUpgradeIds { get; private set; } = new List<string>();
        public float WorldTimeOfDay { get; private set; } = 12f;
        private bool hasLoadedSave;

        private void Awake()
        {
            if (saveSystem == null)
            {
                saveSystem = GetComponent<SaveSystem>();
            }

            LoadFromDisk();
            EnsureDefaultProfile();
        }

        public void AddMoney(int amount)
        {
            SavedMoney += Mathf.Max(0, amount);
        }

        public bool SpendMoney(int amount)
        {
            if (amount < 0 || SavedMoney < amount)
            {
                return false;
            }

            SavedMoney -= amount;
            return true;
        }

        public void AddReputation(int amount)
        {
            SavedReputation += Mathf.Max(0, amount);
        }

        public bool OwnsCar(string carId)
        {
            return !string.IsNullOrEmpty(carId) && OwnedCarIds.Contains(carId);
        }

        public void AddOwnedCar(string carId)
        {
            if (string.IsNullOrEmpty(carId))
            {
                return;
            }

            if (!OwnedCarIds.Contains(carId))
            {
                OwnedCarIds.Add(carId);
            }

            if (string.IsNullOrEmpty(CurrentOwnedCarId))
            {
                CurrentOwnedCarId = carId;
            }
        }

        public void SetCurrentCar(string carId)
        {
            if (OwnsCar(carId))
            {
                CurrentOwnedCarId = carId;
            }
        }

        public bool HasPurchasedUpgrade(string upgradeId)
        {
            return !string.IsNullOrEmpty(upgradeId) && PurchasedUpgradeIds.Contains(upgradeId);
        }

        public void RegisterUpgrade(string upgradeId)
        {
            if (!string.IsNullOrEmpty(upgradeId) && !PurchasedUpgradeIds.Contains(upgradeId))
            {
                PurchasedUpgradeIds.Add(upgradeId);
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
                ownedCarIds = new List<string>(OwnedCarIds),
                purchasedUpgradeIds = new List<string>(PurchasedUpgradeIds),
                worldTimeOfDay = WorldTimeOfDay,
                lastGarageScene = garageScene
            };

            saveSystem.Save(data);
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
            OwnedCarIds = data.ownedCarIds ?? new List<string>();
            PurchasedUpgradeIds = data.purchasedUpgradeIds ?? new List<string>();
            WorldTimeOfDay = data.worldTimeOfDay;
        }

        public void SetWorldTime(float worldTime)
        {
            WorldTimeOfDay = worldTime;
        }

        public void ResetToDefaults()
        {
            SavedMoney = starterMoney;
            SavedReputation = 0;
            CurrentOwnedCarId = starterCarId;
            OwnedCarIds = new List<string>();
            PurchasedUpgradeIds = new List<string>();
            WorldTimeOfDay = 12f;
            EnsureDefaultProfile();
        }

        private void EnsureDefaultProfile()
        {
            if (OwnedCarIds == null)
            {
                OwnedCarIds = new List<string>();
            }

            if (PurchasedUpgradeIds == null)
            {
                PurchasedUpgradeIds = new List<string>();
            }

            if (string.IsNullOrEmpty(CurrentOwnedCarId))
            {
                CurrentOwnedCarId = starterCarId;
            }

            if (!string.IsNullOrEmpty(starterCarId) && !OwnedCarIds.Contains(starterCarId))
            {
                OwnedCarIds.Insert(0, starterCarId);
            }

            if (!hasLoadedSave && SavedMoney <= 0)
            {
                SavedMoney = starterMoney;
            }
        }
    }
}

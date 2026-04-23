using System.Collections.Generic;

namespace Underground.Core.Architecture
{
    public interface IProgressService
    {
        int SavedMoney { get; }
        int SavedReputation { get; }
        string CurrentOwnedCarId { get; }
        IReadOnlyList<string> OwnedCarIds { get; }
        IReadOnlyList<string> PurchasedUpgradeIds { get; }
        IReadOnlyList<string> CompletedRaceIds { get; }
        float WorldTimeOfDay { get; }

        void AddMoney(int amount);
        bool SpendMoney(int amount);
        void AddReputation(int amount);
        bool OwnsCar(string carId);
        void AddOwnedCar(string carId);
        void SetCurrentCar(string carId);
        bool HasPurchasedUpgrade(string upgradeId);
        void RegisterUpgrade(string upgradeId);
        void RegisterRaceCompletion(string raceId);
        bool IsRaceUnlocked(string raceId);
        void SaveNow(float worldTime = Underground.TimeSystem.PackageTimeOfDayUtility.DefaultDuskNightHour, string garageScene = "Garage");
        void LoadFromDisk();
        void SetWorldTime(float worldTime);
        void ResetToDefaults();
    }
}

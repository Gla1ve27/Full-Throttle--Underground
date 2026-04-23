using System;

namespace Underground.Core.Architecture
{
    public interface ISessionService
    {
        int SessionMoney { get; }
        int SessionReputation { get; }

        void BeginSession();
        void AddMoney(int amount);
        void AddReputation(int amount);
        void BankSession(float worldTime = Underground.TimeSystem.PackageTimeOfDayUtility.DefaultDuskNightHour);
        [Obsolete("Vehicle totalled flow removed for milestone 1")]
        void OnVehicleTotalled();
    }
}

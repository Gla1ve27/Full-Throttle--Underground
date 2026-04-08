namespace Underground.Core.Architecture
{
    public interface ISessionService
    {
        int SessionMoney { get; }
        int SessionReputation { get; }

        void BeginSession();
        void AddMoney(int amount);
        void AddReputation(int amount);
        void BankSession(float worldTime = 12f);
        void OnVehicleTotalled();
    }
}

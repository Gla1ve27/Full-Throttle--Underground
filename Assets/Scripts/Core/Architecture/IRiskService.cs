namespace Underground.Core.Architecture
{
    public interface IRiskService
    {
        float CurrentRisk { get; }
        void IncreaseRisk(float amount);
        void ResetRisk();
        float GetRewardMultiplier(bool isNight);
    }
}

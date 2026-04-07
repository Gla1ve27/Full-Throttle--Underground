using UnityEngine;

namespace Underground.Session
{
    public class RiskSystem : MonoBehaviour
    {
        public float CurrentRisk { get; private set; }

        public void IncreaseRisk(float amount)
        {
            CurrentRisk += Mathf.Max(0f, amount);
        }

        public void ResetRisk()
        {
            CurrentRisk = 0f;
        }

        public float GetRewardMultiplier(bool isNight)
        {
            float baseMultiplier = isNight ? 1.75f : 1f;
            return baseMultiplier + (CurrentRisk * 0.05f);
        }
    }
}

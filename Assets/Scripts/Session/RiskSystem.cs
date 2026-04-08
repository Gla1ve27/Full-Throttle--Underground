using UnityEngine;
using Underground.Core.Architecture;

namespace Underground.Session
{
    public class RiskSystem : MonoBehaviour, IRiskService
    {
        public float CurrentRisk { get; private set; }

        private void Awake()
        {
            ServiceLocator.Register<IRiskService>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<IRiskService>(this);
        }

        public void IncreaseRisk(float amount)
        {
            CurrentRisk += Mathf.Max(0f, amount);
            ServiceLocator.EventBus.Publish(new RiskChangedEvent(CurrentRisk));
        }

        public void ResetRisk()
        {
            CurrentRisk = 0f;
            ServiceLocator.EventBus.Publish(new RiskChangedEvent(CurrentRisk));
        }

        public float GetRewardMultiplier(bool isNight)
        {
            float baseMultiplier = isNight ? 1.75f : 1f;
            return baseMultiplier + (CurrentRisk * 0.05f);
        }
    }
}

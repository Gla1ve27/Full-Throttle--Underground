using UnityEngine;
using UnityEngine.SceneManagement;
using Underground.Core.Architecture;
using Underground.Save;

namespace Underground.Session
{
    public class SessionManager : MonoBehaviour, ISessionService
    {
        [SerializeField] private PersistentProgressManager persistentProgress;
        [SerializeField] private RiskSystem riskSystem;
        [SerializeField] private string garageSceneName = "Garage";

        public int SessionMoney { get; private set; }
        public int SessionReputation { get; private set; }

        private void Awake()
        {
            if (persistentProgress == null)
            {
                persistentProgress = ServiceResolver.Resolve<IProgressService>(null) as PersistentProgressManager
                    ?? FindFirstObjectByType<PersistentProgressManager>();
            }

            if (riskSystem == null)
            {
                riskSystem = ServiceResolver.Resolve<IRiskService>(null) as RiskSystem
                    ?? FindFirstObjectByType<RiskSystem>();
            }

            ServiceLocator.Register<ISessionService>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<ISessionService>(this);
        }

        public void BeginSession()
        {
            SessionMoney = 0;
            SessionReputation = 0;
            riskSystem?.ResetRisk();
            ServiceLocator.EventBus.Publish(new SessionStartedEvent(SceneManager.GetActiveScene().name));
        }

        public void AddMoney(int amount)
        {
            SessionMoney += Mathf.Max(0, amount);
            riskSystem?.IncreaseRisk(0.5f);
        }

        public void AddReputation(int amount)
        {
            SessionReputation += Mathf.Max(0, amount);
            riskSystem?.IncreaseRisk(0.75f);
        }

        public void BankSession(float worldTime = 12f)
        {
            if (persistentProgress == null)
            {
                return;
            }

            int moneyBanked = SessionMoney;
            int reputationBanked = SessionReputation;
            persistentProgress.AddMoney(SessionMoney);
            persistentProgress.AddReputation(SessionReputation);
            SessionMoney = 0;
            SessionReputation = 0;
            riskSystem?.ResetRisk();
            persistentProgress.SaveNow(worldTime, garageSceneName);
            ServiceLocator.EventBus.Publish(new SessionBankedEvent(moneyBanked, reputationBanked, worldTime));
        }

        public void OnVehicleTotalled()
        {
            SessionMoney = 0;
            SessionReputation = 0;
            riskSystem?.ResetRisk();
            ServiceLocator.EventBus.Publish(new SessionFailedEvent("VehicleTotalled"));
            SceneManager.LoadScene(garageSceneName);
        }
    }
}

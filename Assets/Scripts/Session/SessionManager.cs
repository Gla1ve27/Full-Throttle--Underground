using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Underground.Core.Architecture;
using Underground.Save;
using Underground.TimeSystem;

namespace Underground.Session
{
    public class SessionManager : MonoBehaviour, ISessionService
    {
        [SerializeField] private PersistentProgressManager persistentProgress;

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

            ServiceLocator.EventBus.Publish(new SessionStartedEvent(SceneManager.GetActiveScene().name));
        }

        public void AddMoney(int amount)
        {
            SessionMoney += Mathf.Max(0, amount);

        }

        public void AddReputation(int amount)
        {
            SessionReputation += Mathf.Max(0, amount);

        }

        public void BankSession(float worldTime = PackageTimeOfDayUtility.DefaultDuskNightHour)
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

            float savedWorldTime = PackageTimeOfDayUtility.ConstrainToDuskNightHours(worldTime);
            persistentProgress.SaveNow(savedWorldTime, garageSceneName);
            ServiceLocator.EventBus.Publish(new SessionBankedEvent(moneyBanked, reputationBanked, savedWorldTime));
        }

        [Obsolete("Vehicle totalled flow removed for milestone 1")]
        public void OnVehicleTotalled()
        {
            // No operation - vehicle totalled flow is disabled for first milestone
        }
    }
}

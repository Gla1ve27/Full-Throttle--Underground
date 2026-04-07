using UnityEngine;
using UnityEngine.SceneManagement;
using Underground.Save;

namespace Underground.Session
{
    public class SessionManager : MonoBehaviour
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
                persistentProgress = FindFirstObjectByType<PersistentProgressManager>();
            }

            if (riskSystem == null)
            {
                riskSystem = FindFirstObjectByType<RiskSystem>();
            }
        }

        public void BeginSession()
        {
            SessionMoney = 0;
            SessionReputation = 0;
            riskSystem?.ResetRisk();
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

            persistentProgress.AddMoney(SessionMoney);
            persistentProgress.AddReputation(SessionReputation);
            SessionMoney = 0;
            SessionReputation = 0;
            riskSystem?.ResetRisk();
            persistentProgress.SaveNow(worldTime, garageSceneName);
        }

        public void OnVehicleTotalled()
        {
            SessionMoney = 0;
            SessionReputation = 0;
            riskSystem?.ResetRisk();
            SceneManager.LoadScene(garageSceneName);
        }
    }
}

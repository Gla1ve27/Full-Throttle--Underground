using UnityEngine;
using Underground.Session;
using Underground.TimeSystem;

namespace Underground.Race
{
    public class RaceManager : MonoBehaviour
    {
        [SerializeField] private RaceDefinition activeRace;
        [SerializeField] private SessionManager sessionManager;
        [SerializeField] private RiskSystem riskSystem;
        [SerializeField] private DayNightCycleController dayNightCycle;

        private void Awake()
        {
            if (sessionManager == null)
            {
                sessionManager = FindFirstObjectByType<SessionManager>();
            }

            if (riskSystem == null)
            {
                riskSystem = FindFirstObjectByType<RiskSystem>();
            }

            if (dayNightCycle == null)
            {
                dayNightCycle = FindFirstObjectByType<DayNightCycleController>();
            }
        }

        public bool CanStartRace()
        {
            if (activeRace == null)
            {
                return false;
            }

            if (activeRace.nightOnly && (dayNightCycle == null || !dayNightCycle.IsNight))
            {
                return false;
            }

            return true;
        }

        public void CompleteRace(bool playerWon)
        {
            if (activeRace == null || !playerWon)
            {
                return;
            }

            bool isNight = dayNightCycle != null && dayNightCycle.IsNight;
            float multiplier = riskSystem != null ? riskSystem.GetRewardMultiplier(isNight) : 1f;

            int money = Mathf.RoundToInt(activeRace.rewardMoney * multiplier);
            int rep = Mathf.RoundToInt(activeRace.rewardReputation * multiplier);

            sessionManager?.AddMoney(money);
            sessionManager?.AddReputation(rep);
        }
    }
}

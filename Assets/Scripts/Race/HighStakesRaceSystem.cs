using UnityEngine;
using Underground.Progression;
using Underground.Save;
using Underground.Session;
using Underground.TimeSystem;

namespace Underground.Race
{
    public class HighStakesRaceSystem : MonoBehaviour
    {
        [SerializeField] private DayNightCycleController dayNightCycle;
        [SerializeField] private PersistentProgressManager progressManager;
        [SerializeField] private VehicleOwnershipSystem ownershipSystem;
        [SerializeField] private SessionManager sessionManager;
        [SerializeField] private int minReputation = 200;
        [SerializeField] private string pendingRewardCarId = "wager_reward_car";

        private void Awake()
        {
            if (dayNightCycle == null)
            {
                dayNightCycle = FindFirstObjectByType<DayNightCycleController>();
            }

            if (progressManager == null)
            {
                progressManager = FindFirstObjectByType<PersistentProgressManager>();
            }

            if (ownershipSystem == null)
            {
                ownershipSystem = FindFirstObjectByType<VehicleOwnershipSystem>();
            }

            if (sessionManager == null)
            {
                sessionManager = FindFirstObjectByType<SessionManager>();
            }
        }

        public bool CanEnterWagerRace()
        {
            return dayNightCycle != null
                && dayNightCycle.IsNight
                && progressManager != null
                && progressManager.SavedReputation >= minReputation
                && ownershipSystem != null
                && ownershipSystem.CanWagerCurrentCar();
        }

        public void ResolveWagerRace(bool playerWon)
        {
            if (playerWon)
            {
                ownershipSystem?.WinCar(pendingRewardCarId);
                sessionManager?.AddReputation(50);
                return;
            }

            sessionManager?.OnVehicleTotalled();
        }
    }
}

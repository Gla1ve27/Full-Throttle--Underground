using UnityEngine;
using Underground.Save;
using Underground.Vehicle;

namespace Underground.Garage
{
    public class RepairSystem : MonoBehaviour
    {
        [SerializeField] private PersistentProgressManager progressManager;
        [SerializeField] private VehicleDamageSystem playerDamageSystem;
        [SerializeField] private VehicleStatsData currentCarStats;

        private void Awake()
        {
            if (progressManager == null)
            {
                progressManager = FindFirstObjectByType<PersistentProgressManager>();
            }

            if (playerDamageSystem == null)
            {
                playerDamageSystem = FindFirstObjectByType<VehicleDamageSystem>();
            }

            if (currentCarStats == null)
            {
                VehicleDynamicsController vehicle = FindFirstObjectByType<VehicleDynamicsController>();
                currentCarStats = vehicle != null ? vehicle.BaseStats : null;
            }
        }

        public int CalculateRepairCost()
        {
            if (playerDamageSystem == null || currentCarStats == null)
            {
                return 0;
            }

            return Mathf.RoundToInt(playerDamageSystem.DamageNormalized * 100f * currentCarStats.repairCostPerDamagePoint);
        }

        public bool Repair()
        {
            int cost = CalculateRepairCost();
            if (cost > 0 && (progressManager == null || !progressManager.SpendMoney(cost)))
            {
                return false;
            }

            playerDamageSystem?.RepairFully();
            progressManager?.SaveNow();
            return true;
        }
    }
}

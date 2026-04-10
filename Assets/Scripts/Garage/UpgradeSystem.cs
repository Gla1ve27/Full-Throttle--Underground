using UnityEngine;
using Underground.Save;
using Underground.Vehicle; // UpgradeDefinition, VehicleDynamicsController

namespace Underground.Garage
{
    public class UpgradeSystem : MonoBehaviour
    {
        [SerializeField] private PersistentProgressManager progressManager;
        [SerializeField] private VehicleDynamicsController playerVehicle;

        private void Awake()
        {
            if (progressManager == null)
            {
                progressManager = FindFirstObjectByType<PersistentProgressManager>();
            }

            if (playerVehicle == null)
            {
                playerVehicle = FindFirstObjectByType<VehicleDynamicsController>();
            }
        }

        public bool PurchaseAndApplyUpgrade(UpgradeDefinition definition)
        {
            if (definition == null || progressManager == null || playerVehicle == null)
            {
                return false;
            }

            if (progressManager.HasPurchasedUpgrade(definition.upgradeId))
            {
                return false;
            }

            if (progressManager.SavedReputation < definition.reputationRequired)
            {
                return false;
            }

            if (!progressManager.SpendMoney(definition.cost))
            {
                return false;
            }

            progressManager.RegisterUpgrade(definition.upgradeId);
            playerVehicle.ApplyUpgrade(definition);
            progressManager.SaveNow();
            return true;
        }
    }
}

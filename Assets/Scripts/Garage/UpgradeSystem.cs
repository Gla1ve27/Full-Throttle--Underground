using UnityEngine;
using Underground.Save;
using Underground.Vehicle;
using Underground.Vehicle.V2;

namespace Underground.Garage
{
    public class UpgradeSystem : MonoBehaviour
    {
        [SerializeField] private PersistentProgressManager progressManager;
        [SerializeField] private VehicleControllerV2 playerVehicleV2;

        private void Awake()
        {
            if (progressManager == null)
            {
                progressManager = FindFirstObjectByType<PersistentProgressManager>();
            }

            if (playerVehicleV2 == null)
            {
                playerVehicleV2 = FindFirstObjectByType<VehicleControllerV2>();
            }
        }

        public bool PurchaseAndApplyUpgrade(UpgradeDefinition definition)
        {
            if (definition == null || progressManager == null)
            {
                return false;
            }

            bool hasV2 = playerVehicleV2 != null && playerVehicleV2.IsInitialized && playerVehicleV2.enabled;

            if (!hasV2)
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

            playerVehicleV2.ApplyUpgrade(definition);

            progressManager.SaveNow();
            return true;
        }
    }
}

using UnityEngine;
using Underground.Vehicle; // UpgradeDefinition

namespace Underground.Garage
{
    public class UpgradePurchaseAction : MonoBehaviour
    {
        [SerializeField] private UpgradeSystem upgradeSystem;
        [SerializeField] private UpgradeDefinition upgradeDefinition;

        private void Awake()
        {
            if (upgradeSystem == null)
            {
                upgradeSystem = FindFirstObjectByType<UpgradeSystem>();
            }
        }

        public void PurchaseAssignedUpgrade()
        {
            upgradeSystem?.PurchaseAndApplyUpgrade(upgradeDefinition);
        }

        public bool TryPurchaseAssignedUpgrade()
        {
            return upgradeSystem != null && upgradeSystem.PurchaseAndApplyUpgrade(upgradeDefinition);
        }
    }
}

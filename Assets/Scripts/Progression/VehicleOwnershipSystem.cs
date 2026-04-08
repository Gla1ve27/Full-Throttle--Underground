using UnityEngine;
using Underground.Save;
using Underground.Vehicle;

namespace Underground.Progression
{
    public class VehicleOwnershipSystem : MonoBehaviour
    {
        [SerializeField] private PersistentProgressManager progressManager;
        [SerializeField] private string protectedStarterCarId = PlayerCarCatalog.StarterCarId;

        private void Awake()
        {
            if (progressManager == null)
            {
                progressManager = FindFirstObjectByType<PersistentProgressManager>();
            }
        }

        public bool HasMultipleCars()
        {
            return progressManager != null && progressManager.OwnedCarIds.Count > 1;
        }

        public bool CanWagerCurrentCar()
        {
            if (progressManager == null)
            {
                return false;
            }

            return !string.IsNullOrEmpty(progressManager.CurrentOwnedCarId)
                && progressManager.CurrentOwnedCarId != protectedStarterCarId
                && HasMultipleCars();
        }

        public void WinCar(string newCarId)
        {
            progressManager?.AddOwnedCar(newCarId);
        }
    }
}

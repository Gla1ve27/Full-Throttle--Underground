using UnityEngine;
using Underground.Save;

namespace Underground.Progression
{
    public class VehicleOwnershipSystem : MonoBehaviour
    {
        [SerializeField] private PersistentProgressManager progressManager;
        [SerializeField] private string protectedStarterCarId = "starter_car";

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

using UnityEngine;
using Underground.Vehicle.V2;

namespace Underground.Vehicle
{
    public class CarRespawn : MonoBehaviour
    {
        [SerializeField] private VehicleControllerV2 vehicleV2;
        [SerializeField] private InputReader input;
        [SerializeField] private Transform defaultRespawnPoint;
        [SerializeField] private float fallBelowY = -25f;

        private Transform currentRespawnPoint;

        private void Awake()
        {
            if (vehicleV2 == null)
            {
                vehicleV2 = GetComponent<VehicleControllerV2>();
            }

            if (input == null)
            {
                input = GetComponent<InputReader>();
            }

            currentRespawnPoint = defaultRespawnPoint != null ? defaultRespawnPoint : transform;
        }

        private void Update()
        {
            if (transform.position.y < fallBelowY)
            {
                RespawnToLastPoint();
                return;
            }

            if (input != null && input.ResetPressed)
            {
                RespawnToLastPoint();
                input.ClearOneShotInputs();
            }
        }

        public void RegisterRespawnPoint(Transform respawnPoint)
        {
            if (respawnPoint != null)
            {
                currentRespawnPoint = respawnPoint;
            }
        }

        public void RespawnToLastPoint()
        {
            if (currentRespawnPoint == null)
            {
                return;
            }

            if (vehicleV2 != null && vehicleV2.IsInitialized && vehicleV2.enabled)
            {
                vehicleV2.ResetVehicle(currentRespawnPoint);
            }
        }
    }
}

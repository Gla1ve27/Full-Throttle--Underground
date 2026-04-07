using UnityEngine;

namespace Underground.Vehicle
{
    public class CarRespawn : MonoBehaviour
    {
        [SerializeField] private VehicleDynamicsController vehicle;
        [SerializeField] private InputReader input;
        [SerializeField] private Transform defaultRespawnPoint;
        [SerializeField] private float fallBelowY = -25f;

        private Transform currentRespawnPoint;

        private void Awake()
        {
            if (vehicle == null)
            {
                vehicle = GetComponent<VehicleDynamicsController>();
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
            if (vehicle == null || currentRespawnPoint == null)
            {
                return;
            }

            vehicle.ResetVehicle(currentRespawnPoint);
        }
    }
}

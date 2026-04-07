using UnityEngine;
using Underground.Vehicle;

namespace Underground.World
{
    public class RespawnPointTrigger : MonoBehaviour
    {
        [SerializeField] private Transform respawnPoint;

        private void OnTriggerEnter(Collider other)
        {
            CarRespawn respawn = other.GetComponentInParent<CarRespawn>();
            if (respawn == null)
            {
                return;
            }

            respawn.RegisterRespawnPoint(respawnPoint != null ? respawnPoint : transform);
        }
    }
}

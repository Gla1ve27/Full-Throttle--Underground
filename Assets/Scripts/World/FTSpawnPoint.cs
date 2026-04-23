using UnityEngine;

namespace FullThrottle.SacredCore.World
{
    public sealed class FTSpawnPoint : MonoBehaviour
    {
        public string spawnPointId = "player_start";
        public bool defaultForScene = false;

        private void OnDrawGizmos()
        {
            Gizmos.color = defaultForScene ? Color.cyan : Color.green;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        }
    }
}

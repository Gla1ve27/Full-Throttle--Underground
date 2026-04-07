using UnityEngine;

namespace Underground.AI
{
    public class AIWaypointFollower : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float speed = 18f;
        [SerializeField] private float rotationSpeed = 6f;
        [SerializeField] private float waypointReachDistance = 6f;
        [SerializeField] private Transform player;
        [SerializeField] private float catchUpDistance = 50f;
        [SerializeField] private float catchUpSpeedBonus = 10f;

        private int currentIndex;

        private void Awake()
        {
            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null)
                {
                    player = playerObject.transform;
                }
            }
        }

        private void Update()
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return;
            }

            Transform target = waypoints[currentIndex];
            Vector3 toTarget = (target.position - transform.position).normalized;

            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(toTarget.x, 0f, toTarget.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            float effectiveSpeed = speed;
            if (player != null)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, player.position);
                if (distanceToPlayer > catchUpDistance)
                {
                    effectiveSpeed += Mathf.InverseLerp(catchUpDistance, catchUpDistance + 120f, distanceToPlayer) * catchUpSpeedBonus;
                }
            }

            transform.position += transform.forward * effectiveSpeed * Time.deltaTime;

            if (Vector3.Distance(transform.position, target.position) <= waypointReachDistance)
            {
                currentIndex = (currentIndex + 1) % waypoints.Length;
            }
        }
    }
}

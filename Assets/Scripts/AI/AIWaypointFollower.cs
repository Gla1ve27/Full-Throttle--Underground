using UnityEngine;

namespace Underground.AI
{
    public class AIWaypointFollower : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float speed = 18f;
        [SerializeField] private float rotationSpeed = 6f;
        [SerializeField] private float waypointReachDistance = 6f;

        private int currentIndex;

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
            transform.position += transform.forward * speed * Time.deltaTime;

            if (Vector3.Distance(transform.position, target.position) <= waypointReachDistance)
            {
                currentIndex = (currentIndex + 1) % waypoints.Length;
            }
        }
    }
}

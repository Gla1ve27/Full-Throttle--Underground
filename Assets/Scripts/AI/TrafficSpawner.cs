using UnityEngine;

namespace Underground.AI
{
    public class TrafficSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject[] trafficPrefabs;
        [SerializeField] private Transform[] spawnPoints;

        private void Start()
        {
            if (trafficPrefabs == null || trafficPrefabs.Length == 0 || spawnPoints == null)
            {
                return;
            }

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                Transform point = spawnPoints[i];
                if (point == null)
                {
                    continue;
                }

                int index = Random.Range(0, trafficPrefabs.Length);
                Instantiate(trafficPrefabs[index], point.position, point.rotation);
            }
        }
    }
}

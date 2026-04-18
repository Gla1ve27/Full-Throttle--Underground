using UnityEngine;
using Underground.Vehicle;
using Underground.World;

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
                GameObject trafficCar = Instantiate(trafficPrefabs[index], point.position, point.rotation);
                VehicleReflectionRuntimeController reflectionController = trafficCar.GetComponent<VehicleReflectionRuntimeController>() ?? trafficCar.AddComponent<VehicleReflectionRuntimeController>();
                reflectionController.Configure(false);

                VehicleNightLightingController lightingController = trafficCar.GetComponent<VehicleNightLightingController>() ?? trafficCar.AddComponent<VehicleNightLightingController>();
                lightingController.ConfigureForTraffic(false);
            }
        }
    }
}

using FCG;
using UnityEngine;
using Underground.Vehicle;

namespace Underground.World
{
    public class TrafficNightLightingInstaller : MonoBehaviour
    {
        [SerializeField] private float scanInterval = 1f;

        private float nextScanTime;

        private void Start()
        {
            InstallLighting();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScanTime)
            {
                return;
            }

            nextScanTime = Time.unscaledTime + scanInterval;
            InstallLighting();
        }

        private void InstallLighting()
        {
            TrafficCar[] trafficCars = FindObjectsByType<TrafficCar>(FindObjectsSortMode.None);
            for (int i = 0; i < trafficCars.Length; i++)
            {
                if (trafficCars[i] == null)
                {
                    continue;
                }

                VehicleNightLightingController lightingController = trafficCars[i].GetComponent<VehicleNightLightingController>();
                if (lightingController == null)
                {
                    lightingController = trafficCars[i].gameObject.AddComponent<VehicleNightLightingController>();
                }

                lightingController.ConfigureForTraffic(false);
            }
        }
    }
}

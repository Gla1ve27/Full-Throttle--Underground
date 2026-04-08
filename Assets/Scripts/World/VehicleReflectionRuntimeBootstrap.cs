using UnityEngine;
using UnityEngine.SceneManagement;
using Underground.Vehicle;

namespace Underground.World
{
    public static class VehicleReflectionRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureSceneVehicleReflections();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureSceneVehicleReflections();
        }

        private static void EnsureSceneVehicleReflections()
        {
            VehicleDynamicsController[] vehicles = Object.FindObjectsByType<VehicleDynamicsController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < vehicles.Length; index++)
            {
                VehicleDynamicsController vehicle = vehicles[index];
                if (vehicle == null || vehicle.GetComponent<PlayerCarAppearanceController>() != null)
                {
                    continue;
                }

                EnsureController(vehicle.gameObject, false);
            }

            try
            {
                GameObject[] trafficObjects = GameObject.FindGameObjectsWithTag("Traffic");
                for (int index = 0; index < trafficObjects.Length; index++)
                {
                    EnsureController(trafficObjects[index], false);
                }
            }
            catch (UnityException)
            {
            }
        }

        private static void EnsureController(GameObject target, bool allowRealtimeProbe)
        {
            if (target == null)
            {
                return;
            }

            VehicleReflectionRuntimeController controller = target.GetComponent<VehicleReflectionRuntimeController>() ?? target.AddComponent<VehicleReflectionRuntimeController>();
            controller.Configure(allowRealtimeProbe);
        }
    }
}

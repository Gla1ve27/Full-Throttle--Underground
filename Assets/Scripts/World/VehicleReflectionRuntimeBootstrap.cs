using System.Collections.Generic;
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
            HashSet<int> processedTargets = new HashSet<int>();

            VehicleDynamicsController[] vehicles = Object.FindObjectsByType<VehicleDynamicsController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < vehicles.Length; index++)
            {
                VehicleDynamicsController vehicle = vehicles[index];
                if (vehicle == null || vehicle.GetComponent<PlayerCarAppearanceController>() != null)
                {
                    continue;
                }

                EnsureController(vehicle.gameObject, false, processedTargets);
            }

            try
            {
                GameObject[] trafficObjects = GameObject.FindGameObjectsWithTag("Traffic");
                for (int index = 0; index < trafficObjects.Length; index++)
                {
                    EnsureController(trafficObjects[index], false, processedTargets);
                }
            }
            catch (UnityException)
            {
            }

            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (candidate == null || candidate.parent != null)
                {
                    continue;
                }

                GameObject root = candidate.gameObject;
                if (root.GetComponent<PlayerCarAppearanceController>() != null ||
                    !VehicleReflectionRuntimeController.IsVehicleLikeObject(root))
                {
                    continue;
                }

                EnsureController(root, root.CompareTag("Player"), processedTargets);
            }
        }

        private static void EnsureController(GameObject target, bool allowRealtimeProbe, HashSet<int> processedTargets)
        {
            if (target == null)
            {
                return;
            }

            int instanceId = target.GetInstanceID();
            if (processedTargets != null && !processedTargets.Add(instanceId))
            {
                return;
            }

            VehicleReflectionRuntimeController controller = target.GetComponent<VehicleReflectionRuntimeController>() ?? target.AddComponent<VehicleReflectionRuntimeController>();
            controller.Configure(allowRealtimeProbe);
        }
    }
}

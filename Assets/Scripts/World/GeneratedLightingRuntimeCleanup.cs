using UnityEngine;
using UnityEngine.SceneManagement;

namespace Underground.World
{
    public static class GeneratedLightingRuntimeCleanup
    {
        private static readonly string[] GeneratedProbeNames =
        {
            "SceneReflectionProbe",
            "GarageReflectionProbe",
            "GameplayReflectionProbe",
            "RuntimeVehicleReflectionProbe"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            RemoveGeneratedProbes();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RemoveGeneratedProbes();
        }

        private static void RemoveGeneratedProbes()
        {
            ReflectionProbe[] probes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = probes.Length - 1; i >= 0; i--)
            {
                ReflectionProbe probe = probes[i];
                if (probe == null || !IsGeneratedProbeName(probe.gameObject.name))
                {
                    continue;
                }

                Object.Destroy(probe.gameObject);
            }
        }

        private static bool IsGeneratedProbeName(string objectName)
        {
            for (int i = 0; i < GeneratedProbeNames.Length; i++)
            {
                if (objectName == GeneratedProbeNames[i])
                {
                    return true;
                }
            }

            return false;
        }
    }
}

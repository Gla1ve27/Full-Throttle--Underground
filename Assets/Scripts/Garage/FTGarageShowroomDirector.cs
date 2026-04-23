using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Vehicle;
using FullThrottle.SacredCore.Audio;
using UnityEngine;

namespace FullThrottle.SacredCore.Garage
{
    /// <summary>
    /// Owns the physical showroom car. It reflects the selected runtime car and nothing else.
    /// </summary>
    [DefaultExecutionOrder(-7900)]
    public sealed class FTGarageShowroomDirector : MonoBehaviour
    {
        [SerializeField] private Transform displayAnchor;
        [SerializeField] private Transform displayParent;
        [SerializeField] private bool disableSpawnedPhysics = true;
        [SerializeField] private bool disableShowroomAudio = true;
        [SerializeField] private bool snapToTurntableSurface = true;
        [SerializeField] private float surfaceOffset = 0.035f;

        private FTSelectedCarRuntime selectedCarRuntime;
        private FTCarRegistry carRegistry;
        private FTEventBus eventBus;
        private GameObject currentDisplay;
        private string currentCarId;

        public string CurrentDisplayCarId => currentCarId;
        public GameObject CurrentDisplay => currentDisplay;

        private void Awake()
        {
            FTServices.Register(this);
            selectedCarRuntime = FTServices.Get<FTSelectedCarRuntime>();
            carRegistry = FTServices.Get<FTCarRegistry>();
            eventBus = FTServices.Get<FTEventBus>();
            eventBus.Subscribe<FTCarSelectionChangedSignal>(OnSelectionChanged);
            RebuildShowroom();
        }

        private void OnDestroy()
        {
            eventBus?.Unsubscribe<FTCarSelectionChangedSignal>(OnSelectionChanged);
        }

        public void RebuildShowroom()
        {
            string carId = selectedCarRuntime.ForceSyncFromProfile();
            if (carId == currentCarId && currentDisplay != null)
            {
                return;
            }

            if (currentDisplay != null)
            {
                Destroy(currentDisplay);
            }

            currentCarId = carId;
            FTCarDefinition definition = carRegistry.Get(carId);
            if (definition == null || definition.worldPrefab == null)
            {
                Debug.LogWarning($"[SacredCore] Showroom could not display car={carId}. Missing definition or prefab.");
                return;
            }

            Transform parent = displayParent != null ? displayParent : transform;
            Vector3 position = displayAnchor != null ? displayAnchor.position : transform.position;
            Quaternion rotation = displayAnchor != null ? displayAnchor.rotation : Quaternion.Euler(definition.garageEuler);
            currentDisplay = Instantiate(definition.worldPrefab, position, rotation, parent);
            currentDisplay.name = $"Showroom_{definition.carId}";

            DisableShowroomRuntimeSystems(currentDisplay);

            FTVehicleVisualApplier visualApplier = EnsureVisualApplier(currentDisplay);
            visualApplier.SetGaragePresentationMode(true);
            visualApplier.ApplyDefinition(definition);

            if (disableSpawnedPhysics)
            {
                Rigidbody[] bodies = currentDisplay.GetComponentsInChildren<Rigidbody>(true);
                for (int i = 0; i < bodies.Length; i++)
                {
                    bodies[i].isKinematic = true;
                    bodies[i].detectCollisions = false;
                }

                Collider[] colliders = currentDisplay.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    colliders[i].enabled = false;
                }
            }

            if (snapToTurntableSurface)
            {
                SnapDisplayToSurface(currentDisplay, position.y + surfaceOffset);
            }

            Debug.Log($"[SacredCore] Showroom displaying {definition.carId} ({definition.displayName}).");
        }

        private static FTVehicleVisualApplier EnsureVisualApplier(GameObject instance)
        {
            FTVehicleVisualApplier applier = instance.GetComponent<FTVehicleVisualApplier>();
            if (applier == null)
            {
                applier = instance.AddComponent<FTVehicleVisualApplier>();
            }

            return applier;
        }

        private void DisableShowroomRuntimeSystems(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            FTVehicleController[] controllers = instance.GetComponentsInChildren<FTVehicleController>(true);
            for (int i = 0; i < controllers.Length; i++)
            {
                controllers[i].enabled = false;
            }

            FTDriverInput[] inputs = instance.GetComponentsInChildren<FTDriverInput>(true);
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i].enabled = false;
            }

            FTVehiclePhysicsGuard[] guards = instance.GetComponentsInChildren<FTVehiclePhysicsGuard>(true);
            for (int i = 0; i < guards.Length; i++)
            {
                guards[i].enabled = false;
            }

            if (!disableShowroomAudio)
            {
                return;
            }

            DisableAudioBehaviours(instance);

            FTVehicleAudioDirector[] directors = instance.GetComponentsInChildren<FTVehicleAudioDirector>(true);
            for (int i = 0; i < directors.Length; i++)
            {
                directors[i].enabled = false;
            }

            FTEngineAudioFeed[] feeds = instance.GetComponentsInChildren<FTEngineAudioFeed>(true);
            for (int i = 0; i < feeds.Length; i++)
            {
                feeds[i].enabled = false;
            }

            FTEngineLoopMixer[] mixers = instance.GetComponentsInChildren<FTEngineLoopMixer>(true);
            for (int i = 0; i < mixers.Length; i++)
            {
                mixers[i].enabled = false;
            }

            FTShiftAudioDirector[] shifts = instance.GetComponentsInChildren<FTShiftAudioDirector>(true);
            for (int i = 0; i < shifts.Length; i++)
            {
                shifts[i].enabled = false;
            }

            FTTurboAudioDirector[] turbos = instance.GetComponentsInChildren<FTTurboAudioDirector>(true);
            for (int i = 0; i < turbos.Length; i++)
            {
                turbos[i].enabled = false;
            }

            FTSurfaceAudioDirector[] surfaces = instance.GetComponentsInChildren<FTSurfaceAudioDirector>(true);
            for (int i = 0; i < surfaces.Length; i++)
            {
                surfaces[i].enabled = false;
            }

            AudioSource[] sources = instance.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i].Stop();
                sources[i].enabled = false;
            }
        }

        private static void DisableAudioBehaviours(GameObject instance)
        {
            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                System.Type type = behaviour.GetType();
                string typeName = type.Name;
                string namespaceName = type.Namespace ?? string.Empty;
                bool audioNamespace = namespaceName == "FullThrottle.SacredCore.Audio"
                    || namespaceName.StartsWith("FullThrottle.SacredCore.Audio.")
                    || namespaceName == "Underground.Audio"
                    || namespaceName.StartsWith("Underground.Audio.");

                if (audioNamespace || typeName.Contains("Audio"))
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static void SnapDisplayToSurface(GameObject display, float surfaceY)
        {
            Renderer[] renderers = display.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                return;
            }

            Vector3 position = display.transform.position;
            position.y += surfaceY - bounds.min.y;
            display.transform.position = position;
        }

        private void OnSelectionChanged(FTCarSelectionChangedSignal signal)
        {
            RebuildShowroom();
        }
    }
}

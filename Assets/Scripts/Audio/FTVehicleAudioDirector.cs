using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Vehicle;
using UnityEngine;

namespace FullThrottle.SacredCore.Audio
{
    [DisallowMultipleComponent]
    public sealed class FTVehicleAudioDirector : MonoBehaviour, IFTVehicleDefinitionReceiver
    {
        [SerializeField] private FTCarDefinition carDefinition;
        [SerializeField] private FTVehicleTelemetry telemetry;
        [SerializeField] private Transform audioRoot;
        [SerializeField] private bool logLoopState;
        [SerializeField] private float loopLogInterval = 0.25f;

        private FTAudioProfileRegistry registry;
        private FTVehicleAudioProfile profile;
        private FTEngineAudioFeed feed;
        private FTEngineLoopMixer engineMixer;
        private FTShiftAudioDirector shiftDirector;
        private FTTurboAudioDirector turboDirector;
        private FTSweetenerAudioDirector sweetenerDirector;
        private FTSurfaceAudioDirector surfaceDirector;
        private FTAudioMixerRouter mixerRouter;
        private float nextLoopLogTime;
        private bool disabledAsDuplicate;

        public FTVehicleAudioProfile CurrentProfile => profile;

        private void Awake()
        {
            if (!ClaimSingleDirector())
            {
                return;
            }

            ResolveComponents();
        }

        private void Start()
        {
            if (disabledAsDuplicate)
            {
                return;
            }

            if (carDefinition == null && TryGetComponent(out FTPlayerVehicleBinder binder))
            {
                carDefinition = binder.Definition;
            }

            if (carDefinition != null)
            {
                ConfigureForCar(carDefinition);
            }
        }

        private void Update()
        {
            if (disabledAsDuplicate)
            {
                return;
            }

            if (logLoopState && engineMixer != null && feed != null && Time.time >= nextLoopLogTime)
            {
                nextLoopLogTime = Time.time + Mathf.Max(0.05f, loopLogInterval);
                Debug.Log($"[SacredCore] Audio gear={feed.Gear} rawRPM={feed.RawRPM:0} audioRPM={feed.AudioRPM:0} throttle={feed.Throttle:0.00} shift={feed.IsShifting} bed={engineMixer.TotalEngineVolume:0.00} loops={engineMixer.ActiveLoopSummary}");
            }
        }

        public void ApplyDefinition(FTCarDefinition definition)
        {
            if (disabledAsDuplicate)
            {
                return;
            }

            carDefinition = definition;
            ConfigureForCar(definition);
        }

        public void ConfigureForCar(FTCarDefinition definition)
        {
            if (disabledAsDuplicate)
            {
                return;
            }

            ResolveComponents();
            if (definition == null)
            {
                Debug.LogError("[SacredCore] Vehicle audio director received null car definition.");
                return;
            }

            if (registry == null)
            {
                Debug.LogError($"[SacredCore] Vehicle audio has no FTAudioProfileRegistry. car={definition.carId}");
                return;
            }

            bool valid = registry.ResolveProfile(definition, out profile, out string report);
            if (profile == null)
            {
                Debug.LogError($"[SacredCore] Audio profile failed. {report}");
                return;
            }

            if (!valid)
            {
                StopAllSourcesBelow(transform);
                Debug.LogError($"[SacredCore] Vehicle audio refused unsafe profile: chosenCar={definition.carId}, requestedProfile={definition.audioProfileId}. {report}");
                return;
            }

            feed.Configure(profile, telemetry);
            engineMixer.Configure(profile, feed, audioRoot);
            shiftDirector.Configure(profile, feed, audioRoot);
            turboDirector.Configure(profile, feed, audioRoot);
            sweetenerDirector.Configure(profile, feed, audioRoot);
            surfaceDirector.Configure(profile, feed, audioRoot);
            mixerRouter.Configure(profile, audioRoot);

            string level = valid ? "loaded" : "loaded with warnings";
            Debug.Log($"[SacredCore] Vehicle audio {level}: chosenCar={definition.carId}, chosenProfile={profile.audioProfileId}. {report}");
        }

        private void ResolveComponents()
        {
            if (audioRoot == null)
            {
                audioRoot = ResolveAudioRoot();
            }

            if (registry == null)
            {
                if (!FTServices.TryGet(out registry))
                {
                    registry = FindFirstObjectByType<FTAudioProfileRegistry>();
                }
            }

            if (telemetry == null) telemetry = GetComponentInParent<FTVehicleTelemetry>();
            if (feed == null) feed = GetComponent<FTEngineAudioFeed>() ?? gameObject.AddComponent<FTEngineAudioFeed>();
            if (engineMixer == null) engineMixer = GetComponent<FTEngineLoopMixer>() ?? gameObject.AddComponent<FTEngineLoopMixer>();
            if (shiftDirector == null) shiftDirector = GetComponent<FTShiftAudioDirector>() ?? gameObject.AddComponent<FTShiftAudioDirector>();
            if (turboDirector == null) turboDirector = GetComponent<FTTurboAudioDirector>() ?? gameObject.AddComponent<FTTurboAudioDirector>();
            if (sweetenerDirector == null) sweetenerDirector = GetComponent<FTSweetenerAudioDirector>() ?? gameObject.AddComponent<FTSweetenerAudioDirector>();
            if (surfaceDirector == null) surfaceDirector = GetComponent<FTSurfaceAudioDirector>() ?? gameObject.AddComponent<FTSurfaceAudioDirector>();
            if (mixerRouter == null) mixerRouter = GetComponent<FTAudioMixerRouter>() ?? gameObject.AddComponent<FTAudioMixerRouter>();
        }

        private Transform ResolveAudioRoot()
        {
            if (transform.name == "FTVehicleAudio")
            {
                return transform;
            }

            Transform existing = transform.Find("FTAudioRuntime");
            if (existing != null)
            {
                return existing;
            }

            existing = transform.Find("FTVehicleAudio");
            if (existing != null)
            {
                return existing;
            }

            GameObject root = new GameObject("FTAudioRuntime");
            root.transform.SetParent(transform, false);
            return root.transform;
        }

        private bool ClaimSingleDirector()
        {
            FTPlayerVehicleBinder binder = GetComponentInParent<FTPlayerVehicleBinder>();
            Transform scope = binder != null ? binder.transform : transform.root;
            FTVehicleAudioDirector[] directors = scope.GetComponentsInChildren<FTVehicleAudioDirector>(true);
            if (directors.Length <= 1)
            {
                return true;
            }

            FTVehicleAudioDirector chosen = ChoosePrimaryDirector(scope, directors);
            if (chosen == this)
            {
                return true;
            }

            disabledAsDuplicate = true;
            StopAllSourcesBelow(transform);
            enabled = false;
            Debug.LogWarning($"[SacredCore] Disabled duplicate FTVehicleAudioDirector on '{name}'. Primary is '{chosen.name}'.");
            return false;
        }

        private static FTVehicleAudioDirector ChoosePrimaryDirector(Transform scope, FTVehicleAudioDirector[] directors)
        {
            FTVehicleAudioDirector best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < directors.Length; i++)
            {
                FTVehicleAudioDirector candidate = directors[i];
                if (candidate == null)
                {
                    continue;
                }

                int score = 0;
                if (candidate.transform.parent == scope) score += 100;
                if (candidate.transform.name == "FTVehicleAudio") score += 20;
                score -= GetDepthFrom(scope, candidate.transform);

                if (best == null || score > bestScore || score == bestScore && candidate.GetInstanceID() < best.GetInstanceID())
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        private static int GetDepthFrom(Transform root, Transform target)
        {
            int depth = 0;
            Transform current = target;
            while (current != null && current != root)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }

        private static void StopAllSourcesBelow(Transform root)
        {
            if (root == null)
            {
                return;
            }

            AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i].Stop();
                sources[i].clip = null;
            }
        }
    }
}

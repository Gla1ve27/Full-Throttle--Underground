using UnityEngine;
using UnityEngine.Audio;

namespace FullThrottle.SacredCore.Audio
{
    public sealed class FTAudioMixerRouter : MonoBehaviour
    {
        [Header("Mixer")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup defaultCarGroup;
        [SerializeField] private AudioMixerGroup engineCoreGroup;
        [SerializeField] private AudioMixerGroup engineDecelGroup;
        [SerializeField] private AudioMixerGroup sweetenerGroup;
        [SerializeField] private AudioMixerGroup turboGroup;
        [SerializeField] private AudioMixerGroup shiftGroup;
        [SerializeField] private AudioMixerGroup skidGroup;

        [Header("Mode")]
        [SerializeField] private FTAudioMode mode = FTAudioMode.FreeRoam;
        [SerializeField] private bool routeOnInterval;
        [SerializeField] private float routeInterval = 1f;

        private FTVehicleAudioProfile profile;
        private Transform sourceRoot;
        private float nextRouteTime;

        public void Configure(FTVehicleAudioProfile audioProfile, Transform root)
        {
            profile = audioProfile;
            sourceRoot = root != null ? root : transform;
            ApplyMode(mode);
            RouteNow();
        }

        public void SetMode(FTAudioMode newMode)
        {
            mode = newMode;
            ApplyMode(mode);
            RouteNow();
        }

        [ContextMenu("Route Now")]
        public void RouteNow()
        {
            Transform root = sourceRoot != null ? sourceRoot : transform;
            AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source == null)
                {
                    continue;
                }

                source.outputAudioMixerGroup = ResolveGroupForSource(source);
            }
        }

        private void Update()
        {
            if (!routeOnInterval || Time.unscaledTime < nextRouteTime)
            {
                return;
            }

            nextRouteTime = Time.unscaledTime + Mathf.Max(0.25f, routeInterval);
            RouteNow();
        }

        private void ApplyMode(FTAudioMode audioMode)
        {
            FTAudioModePreset preset = profile != null && profile.modePresets != null
                ? profile.modePresets.GetPreset(audioMode)
                : null;

            if (preset != null)
            {
                preset.Apply(mixer);
                Debug.Log($"[SacredCore] Audio mix preset applied. mode={audioMode}, preset={preset.name}, profile={profile.audioProfileId}");
            }
        }

        private AudioMixerGroup ResolveGroupForSource(AudioSource source)
        {
            string sourceName = source.gameObject.name;
            if (sourceName.StartsWith("FT_EngineLoop_LowDecel")
                || sourceName.StartsWith("FT_EngineLoop_MidDecel")
                || sourceName.StartsWith("FT_EngineLoop_HighDecel")
                || sourceName.StartsWith("FT_EngineSweep_Decel"))
            {
                return engineDecelGroup != null ? engineDecelGroup : defaultCarGroup;
            }

            if (sourceName.StartsWith("FT_EngineLoop_") || sourceName.StartsWith("FT_EngineSweep_"))
            {
                return engineCoreGroup != null ? engineCoreGroup : defaultCarGroup;
            }

            if (sourceName.StartsWith("FT_Sweetener_"))
            {
                return sweetenerGroup != null ? sweetenerGroup : defaultCarGroup;
            }

            if (sourceName.StartsWith("FT_Turbo"))
            {
                return turboGroup != null ? turboGroup : defaultCarGroup;
            }

            if (sourceName.StartsWith("FT_Shift"))
            {
                return shiftGroup != null ? shiftGroup : defaultCarGroup;
            }

            if (sourceName.StartsWith("FT_Surface"))
            {
                return skidGroup != null ? skidGroup : defaultCarGroup;
            }

            return defaultCarGroup;
        }
    }
}

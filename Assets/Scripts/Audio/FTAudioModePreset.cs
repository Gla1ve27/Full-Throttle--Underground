using UnityEngine;
using UnityEngine.Audio;

namespace FullThrottle.SacredCore.Audio
{
    [CreateAssetMenu(menuName = "Full Throttle/Sacred Core/Audio Mode Preset", fileName = "FT_AudioModePreset")]
    public sealed class FTAudioModePreset : ScriptableObject
    {
        [Header("Mode")]
        public FTAudioMode mode = FTAudioMode.FreeRoam;
        public AudioMixerSnapshot snapshot;
        [Min(0f)] public float transitionSeconds = 0.35f;

        [Header("Group Gains")]
        [Range(0f, 2f)] public float engineCore = 1f;
        [Range(0f, 2f)] public float engineDecel = 1f;
        [Range(0f, 2f)] public float sweetener = 1f;
        [Range(0f, 2f)] public float turbo = 1f;
        [Range(0f, 2f)] public float shift = 1f;
        [Range(0f, 2f)] public float skid = 1f;
        [Range(0f, 2f)] public float worldBed = 1f;

        [Header("Optional Mixer Parameters")]
        public string engineCoreParameter = "CarEngineCoreVol";
        public string engineDecelParameter = "CarEngineDecelVol";
        public string sweetenerParameter = "CarSweetenerVol";
        public string turboParameter = "CarTurboVol";
        public string shiftParameter = "CarShiftVol";
        public string skidParameter = "CarSkidVol";
        public string worldBedParameter = "WorldBedVol";

        public void Apply(AudioMixer mixerOverride = null)
        {
            AudioMixer mixer = mixerOverride != null ? mixerOverride : snapshot != null ? snapshot.audioMixer : null;

            if (snapshot != null)
            {
                snapshot.TransitionTo(Mathf.Max(0f, transitionSeconds));
            }

            if (mixer == null)
            {
                return;
            }

            SetGain(mixer, engineCoreParameter, engineCore);
            SetGain(mixer, engineDecelParameter, engineDecel);
            SetGain(mixer, sweetenerParameter, sweetener);
            SetGain(mixer, turboParameter, turbo);
            SetGain(mixer, shiftParameter, shift);
            SetGain(mixer, skidParameter, skid);
            SetGain(mixer, worldBedParameter, worldBed);
        }

        private static void SetGain(AudioMixer mixer, string parameterName, float linearGain)
        {
            if (mixer == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            mixer.SetFloat(parameterName, LinearToDb(linearGain));
        }

        private static float LinearToDb(float value)
        {
            return value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;
        }
    }
}

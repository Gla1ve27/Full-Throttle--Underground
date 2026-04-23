using UnityEngine;
using UnityEngine.Audio;

namespace FullThrottle.SacredCore.Audio
{
    public enum FTAudioMode
    {
        FreeRoam,
        Race,
        Drift,
        Drag,
        Garage
    }

    public enum FTAudioUpgradeStage
    {
        Stock,
        Street,
        Sport,
        Race,
        Elite
    }

    public abstract class FTAudioProfile : ScriptableObject
    {
        [Header("Identity")]
        public string audioProfileId = "profile_id";
        public string audioFamilyTag = "";
        public bool dedicatedHeroProfile = true;
        public bool devEmergencyFallback;
    }

    [System.Serializable]
    public sealed class FTAudioUpgradeModifier
    {
        public FTAudioUpgradeStage stage = FTAudioUpgradeStage.Stock;
        [Range(0.5f, 1.5f)] public float toneFullness = 1f;
        [Range(0.5f, 1.8f)] public float shiftAggression = 1f;
        [Range(0f, 2f)] public float inductionLoudness = 1f;
        [Range(0f, 2f)] public float turboResponse = 1f;
        [Range(0f, 2f)] public float backfireIntensity = 0.5f;
        [Range(0f, 2f)] public float limiterHarshness = 1f;
        [Range(0f, 2f)] public float overrunCharacter = 1f;
    }

    [System.Serializable]
    public sealed class FTAudioLoopLayer
    {
        public AudioClip clip;
        [Range(0f, 2f)] public float volume = 0.7f;
        [Min(1f)] public float referenceRPM = 3000f;
        [Min(1f)] public float minRPM = 1000f;
        [Min(1f)] public float maxRPM = 4500f;
        [Range(0.25f, 2.5f)] public float basePitch = 1f;
    }

    [System.Serializable]
    public sealed class FTAudioSweepLayer
    {
        public bool enabled;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.18f;
        public Vector2 playbackWindow01 = new Vector2(0.03f, 0.97f);
        public Vector2 pitchCorrectionRange = new Vector2(0.98f, 1.02f);
        [Min(0.01f)] public float seekResponse = 6f;
        [Min(0.01f)] public float resyncThresholdSeconds = 0.65f;
    }

    [System.Serializable]
    public sealed class FTAudioTransientLayer
    {
        public AudioClip clip;
        [Range(0f, 2f)] public float volume = 0.7f;
        public Vector2 pitchRange = new Vector2(0.96f, 1.04f);
        [Min(0f)] public float cooldown = 0.08f;
    }

    [System.Serializable]
    public sealed class FTAudioLimiterSettings
    {
        [Range(0f, 1f)] public float enterRPM01 = 0.955f;
        [Range(0f, 0.25f)] public float volumePulse = 0.06f;
        [Range(1f, 30f)] public float pulseHz = 12f;
    }

    [System.Serializable]
    public sealed class FTAudioResponseSettings
    {
        public float rpmRiseResponse = 8.5f;
        public float rpmFallResponse = 7f;
        public float throttleResponse = 13f;
        public float volumeResponse = 13f;
        public float pitchResponse = 6.5f;
        public Vector2 pitchClamp = new Vector2(0.74f, 1.24f);
        [Range(0f, 1f)] public float minimumBedVolume = 0.32f;
        [Range(0f, 1f)] public float bedProtectionBoost = 0.20f;
        [Range(0f, 1f)] public float shiftDuck = 0.12f;
        [Min(0f)] public float shiftDuckDuration = 0.08f;
        [Range(0f, 0.08f)] public float shiftPitchDip = 0.008f;
    }

    [System.Serializable]
    public sealed class FTAudioInteriorExteriorSettings
    {
        [Range(0f, 1f)] public float interiorBlend;
        [Range(0f, 1f)] public float exteriorSpatialBlend = 0.65f;
        [Range(0f, 1f)] public float interiorSpatialBlend = 0.22f;
        [Range(0f, 1f)] public float interiorEngineScale = 0.82f;
        [Range(0f, 1f)] public float interiorSweetenerScale = 0.72f;
        [Range(0f, 1f)] public float interiorWorldScale = 0.55f;
    }

    [System.Serializable]
    public sealed class FTAudioModePresetOverrides
    {
        public FTAudioModePreset freeRoam;
        public FTAudioModePreset race;
        public FTAudioModePreset drift;
        public FTAudioModePreset drag;
        public FTAudioModePreset garage;

        public FTAudioModePreset GetPreset(FTAudioMode mode)
        {
            return mode switch
            {
                FTAudioMode.Race => race,
                FTAudioMode.Drift => drift,
                FTAudioMode.Drag => drag,
                FTAudioMode.Garage => garage,
                _ => freeRoam
            };
        }
    }

    [System.Serializable]
    public sealed class FTGaragePreviewSettings
    {
        public string revStyle = "restrained";
        [Range(0f, 1f)] public float previewThrottle = 0.35f;
        [Min(0f)] public float revRiseSeconds = 0.8f;
        [Min(0f)] public float revFallSeconds = 1.0f;
    }
}

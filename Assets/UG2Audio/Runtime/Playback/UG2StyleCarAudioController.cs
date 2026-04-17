using UG2Audio.Data;
using UnityEngine;
using UnityEngine.Audio;

namespace UG2Audio.Playback
{
    public sealed class UG2StyleCarAudioController : MonoBehaviour
    {
        [Header("UG2 Graph")]
        public UG2StyleCarAudioProfile profile;

        [Header("Routing")]
        public AudioMixerGroup exteriorGroup;
        public AudioMixerGroup interiorGroup;
        public bool interior;

        [Header("Vehicle State")]
        [Range(0f, 1f)] public float normalizedRpm;
        [Range(0f, 1f)] public float throttle;
        [Range(0f, 1f)] public float speed;
        [Range(0f, 1f)] public float wheelSlip;
        [Range(0f, 1f)] public float turboBoost;
        public int gear;

        [Header("Layer Gains")]
        [Range(0f, 2f)] public float engineBodyGain = 1f;
        [Range(0f, 2f)] public float accelCharacterGain = 0.75f;
        [Range(0f, 2f)] public float decelCharacterGain = 0.65f;
        [Range(0f, 2f)] public float sweetenerGain = 0.9f;
        [Range(0f, 2f)] public float turboGain = 0.8f;
        [Range(0f, 2f)] public float skidGain = 1f;
        [Range(0f, 2f)] public float roadGain = 0.7f;
        [Range(0f, 2f)] public float windGain = 0.65f;

        private AudioSource engineBodySource;
        private AudioSource accelCharacterSource;
        private AudioSource decelCharacterSource;
        private AudioSource sweetenerSource;
        private AudioSource shiftSource;
        private AudioSource turboSource;
        private AudioSource skidSource;
        private AudioSource roadSource;
        private AudioSource windSource;

        private UG2StyleCarAudioProfile activeProfile;
        private int lastGear;
        private float lastThrottle;
        private bool layersStarted;

        private void Awake()
        {
            CreateSources();
            lastGear = gear;
        }

        private void OnEnable()
        {
            BindProfile();
            StartLoopingLayers();
        }

        private void Update()
        {
            if (profile != activeProfile)
                BindProfile();

            UpdateRouting();
            UpdateContinuousLayers();
            UpdateDiscreteLayers();
            lastGear = gear;
            lastThrottle = throttle;
        }

        public void SetVehicleState(float rpm01, float throttle01, float speed01, float slip01, float boost01, int currentGear, bool useInterior)
        {
            normalizedRpm = Mathf.Clamp01(rpm01);
            throttle = Mathf.Clamp01(throttle01);
            speed = Mathf.Clamp01(speed01);
            wheelSlip = Mathf.Clamp01(slip01);
            turboBoost = Mathf.Clamp01(boost01);
            gear = currentGear;
            interior = useInterior;
        }

        public void TriggerSputter(float intensity)
        {
            PlayOneShot(sweetenerSource, sweetenerGain * Mathf.Clamp01(intensity));
        }

        public void TriggerShift(float intensity)
        {
            PlayOneShot(shiftSource, Mathf.Clamp01(intensity));
        }

        private void CreateSources()
        {
            engineBodySource = CreateSource("UG2 Engine Body", true);
            accelCharacterSource = CreateSource("UG2 Accel Character", true);
            decelCharacterSource = CreateSource("UG2 Decel Character", true);
            sweetenerSource = CreateSource("UG2 Sweetener/Sputter", false);
            shiftSource = CreateSource("UG2 Shift", false);
            turboSource = CreateSource("UG2 Turbo", true);
            skidSource = CreateSource("UG2 Skid", true);
            roadSource = CreateSource("UG2 Road", true);
            windSource = CreateSource("UG2 Wind", true);
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0.35f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 3f;
            source.maxDistance = 80f;
            return source;
        }

        private void BindProfile()
        {
            activeProfile = profile;
            layersStarted = false;

            UG2StyleEnginePackage engine = profile == null ? null : profile.enginePackage;
            engineBodySource.clip = FirstClip(engine == null ? null : engine.engineSpuBank, engine == null ? null : engine.engineEeBank);
            accelCharacterSource.clip = Clip(engine == null ? null : engine.accelGin);
            decelCharacterSource.clip = Clip(engine == null ? null : engine.decelGin);
            sweetenerSource.clip = Clip(engine == null ? null : engine.sweetenerBank);
            shiftSource.clip = FirstShiftClip(profile == null ? null : profile.shiftPackage);
            turboSource.clip = FirstTurboClip(profile == null ? null : profile.turboPackage);
            skidSource.clip = FirstSkidClip(profile == null ? null : profile.skidPackage);
            roadSource.clip = FirstRoadClip("ROADNOISE");
            windSource.clip = FirstRoadClip("WIND");

            StartLoopingLayers();
        }

        private void StartLoopingLayers()
        {
            if (layersStarted)
                return;

            PlayLoopIfReady(engineBodySource);
            PlayLoopIfReady(accelCharacterSource);
            PlayLoopIfReady(decelCharacterSource);
            PlayLoopIfReady(turboSource);
            PlayLoopIfReady(skidSource);
            PlayLoopIfReady(roadSource);
            PlayLoopIfReady(windSource);
            layersStarted = true;
        }

        private void UpdateRouting()
        {
            AudioMixerGroup group = interior ? interiorGroup : exteriorGroup;
            AssignGroup(engineBodySource, group);
            AssignGroup(accelCharacterSource, group);
            AssignGroup(decelCharacterSource, group);
            AssignGroup(sweetenerSource, group);
            AssignGroup(shiftSource, group);
            AssignGroup(turboSource, group);
            AssignGroup(skidSource, group);
            AssignGroup(roadSource, group);
            AssignGroup(windSource, group);
        }

        private void UpdateContinuousLayers()
        {
            float rpmPitch = Mathf.Lerp(0.55f, 1.95f, Mathf.Clamp01(normalizedRpm));
            float accelBlend = Mathf.Clamp01(throttle);
            float decelBlend = Mathf.Clamp01(1f - throttle) * Mathf.Clamp01(normalizedRpm * 1.4f);

            engineBodySource.volume = engineBodyGain * Mathf.Lerp(0.35f, 1f, normalizedRpm);
            engineBodySource.pitch = rpmPitch;

            accelCharacterSource.volume = accelCharacterGain * accelBlend;
            accelCharacterSource.pitch = rpmPitch;

            decelCharacterSource.volume = decelCharacterGain * decelBlend;
            decelCharacterSource.pitch = Mathf.Lerp(0.6f, 1.7f, normalizedRpm);

            turboSource.volume = turboGain * Mathf.Clamp01(turboBoost * throttle);
            turboSource.pitch = Mathf.Lerp(0.85f, 1.35f, turboBoost);

            skidSource.volume = skidGain * Mathf.SmoothStep(0f, 1f, wheelSlip);
            skidSource.pitch = Mathf.Lerp(0.85f, 1.2f, speed);

            roadSource.volume = roadGain * Mathf.SmoothStep(0f, 1f, speed);
            roadSource.pitch = Mathf.Lerp(0.8f, 1.25f, speed);

            windSource.volume = windGain * Mathf.SmoothStep(0.15f, 1f, speed);
            windSource.pitch = Mathf.Lerp(0.9f, 1.3f, speed);

            if (interior)
            {
                windSource.volume *= 0.55f;
                roadSource.volume *= 0.8f;
            }
        }

        private void UpdateDiscreteLayers()
        {
            if (gear != lastGear)
                PlayOneShot(shiftSource, 1f);

            bool throttleLift = lastThrottle > 0.55f && throttle < 0.2f && normalizedRpm > 0.35f;
            if (throttleLift)
                TriggerSputter(Mathf.InverseLerp(0.35f, 1f, normalizedRpm));
        }

        private static void PlayLoopIfReady(AudioSource source)
        {
            if (source != null && source.clip != null && !source.isPlaying)
                source.Play();
        }

        private static void PlayOneShot(AudioSource source, float volume)
        {
            if (source != null && source.clip != null)
                source.PlayOneShot(source.clip, Mathf.Clamp01(volume));
        }

        private static void AssignGroup(AudioSource source, AudioMixerGroup group)
        {
            if (source != null)
                source.outputAudioMixerGroup = group;
        }

        private static AudioClip Clip(UG2StyleSourceAssetRef source)
        {
            return source == null ? null : source.decodedClip;
        }

        private static AudioClip FirstClip(UG2StyleSourceAssetRef first, UG2StyleSourceAssetRef second)
        {
            AudioClip firstClip = Clip(first);
            return firstClip != null ? firstClip : Clip(second);
        }

        private static AudioClip FirstShiftClip(UG2StyleShiftPackage package)
        {
            AudioClip clip = FirstTierClip(package == null ? null : package.small);
            if (clip != null) return clip;
            clip = FirstTierClip(package == null ? null : package.medium);
            if (clip != null) return clip;
            clip = FirstTierClip(package == null ? null : package.large);
            if (clip != null) return clip;
            return FirstTierClip(package == null ? null : package.truck);
        }

        private static AudioClip FirstTurboClip(UG2StyleTurboPackage package)
        {
            AudioClip clip = FirstTierClip(package == null ? null : package.small1);
            if (clip != null) return clip;
            clip = FirstTierClip(package == null ? null : package.small2);
            if (clip != null) return clip;
            clip = FirstTierClip(package == null ? null : package.medium);
            if (clip != null) return clip;
            clip = FirstTierClip(package == null ? null : package.big);
            if (clip != null) return clip;
            return FirstTierClip(package == null ? null : package.truck);
        }

        private static AudioClip FirstSkidClip(UG2StyleSkidPackage package)
        {
            if (package == null)
                return null;

            AudioClip clip = Clip(package.pavement == null ? null : package.pavement.bank);
            if (clip != null) return clip;
            clip = Clip(package.pavementAlt == null ? null : package.pavementAlt.bank);
            if (clip != null) return clip;
            clip = Clip(package.drift == null ? null : package.drift.bank);
            if (clip != null) return clip;
            return Clip(package.driftAlt == null ? null : package.driftAlt.bank);
        }

        private AudioClip FirstRoadClip(string namePrefix)
        {
            if (profile == null || profile.roadAndWindBanks == null)
                return null;

            for (int i = 0; i < profile.roadAndWindBanks.Count; i++)
            {
                UG2StyleSourceAssetRef source = profile.roadAndWindBanks[i];
                if (source != null && source.fileName.StartsWith(namePrefix, System.StringComparison.OrdinalIgnoreCase))
                    return source.decodedClip;
            }

            return null;
        }

        private static AudioClip FirstTierClip(System.Collections.Generic.List<UG2StyleTierBankRef> tiers)
        {
            if (tiers == null)
                return null;

            for (int i = 0; i < tiers.Count; i++)
            {
                AudioClip clip = Clip(tiers[i] == null ? null : tiers[i].bank);
                if (clip != null)
                    return clip;
            }

            return null;
        }
    }
}

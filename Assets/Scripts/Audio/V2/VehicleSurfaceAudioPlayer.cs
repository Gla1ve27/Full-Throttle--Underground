using UnityEngine;
using UnityEngine.Audio;

namespace Underground.Audio.V2
{
    /// <summary>
    /// Tire skid and road surface audio. Slip-gated with speed thresholds.
    /// </summary>
    public sealed class VehicleSurfaceAudioPlayer : MonoBehaviour
    {
        [Header("Tire Rolling")]
        [SerializeField] private AudioClip rollingClip;
        [SerializeField, Range(0f, 1f)] private float rollingVolume = 0.17f;
        [SerializeField] private Vector2 rollingPitchRange = new Vector2(0.8f, 1.2f);

        [Header("Skid")]
        [SerializeField] private AudioClip skidClip;
        [SerializeField, Range(0f, 1f)] private float skidVolume = 0.28f;
        [SerializeField] private Vector2 skidPitchRange = new Vector2(0.92f, 1.08f);
        [SerializeField] private float minimumSkidSpeedKph = 36f;
        [SerializeField] private float handbrakeSkidMinSpeedKph = 18f;
        [SerializeField] private float launchSkidMuteSpeedKph = 42f;
        [SerializeField, Range(0f, 1f)] private float launchThrottleSuppression = 0.45f;
        [SerializeField, Range(0f, 1f)] private float slipThreshold = 0.38f;
        [SerializeField, Range(0f, 1.5f)] private float fullSlip = 0.95f;

        [Header("Spatial")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.78f;
        [SerializeField] private float minDistance = 3f;
        [SerializeField] private float maxDistance = 55f;
        [SerializeField] private AudioMixerGroup outputGroup;

        [Header("Response")]
        [SerializeField] private float volumeResponse = 12f;
        [SerializeField] private float pitchResponse = 10f;

        private AudioSource rollingSource;
        private AudioSource skidSource;
        private Transform sourceRoot;
        private float rollingTargetVol;
        private float skidTargetVol;
        private bool initialized;

        public void Initialize(Transform root)
        {
            sourceRoot = root;
            rollingSource = CreateLoop("SF_Rolling", rollingClip);
            skidSource = CreateLoop("SF_Skid", skidClip);
            initialized = true;
        }

        public void ApplyFromBank(NFSU2CarAudioBank.TierAudioPackage tier, bool isExterior)
        {
            if (tier?.skid == null) return;
            var skidPkg = tier.skid;
            skidClip = isExterior ? skidPkg.skid.exteriorClip : skidPkg.skid.interiorClip;
            skidVolume = isExterior ? skidPkg.skid.exteriorVolume : skidPkg.skid.interiorVolume;
            slipThreshold = skidPkg.slipThreshold;
            fullSlip = skidPkg.fullSlip;

            if (skidSource != null && skidClip != null)
            {
                skidSource.clip = skidClip;
                if (!skidSource.isPlaying) skidSource.Play();
            }
        }

        public void UpdateSurface(EngineAudioStateFeed feed, float dt)
        {
            if (!initialized || feed == null) return;

            // Rolling
            float speedT = Mathf.InverseLerp(5f, 120f, feed.SmoothedSpeedKph);
            rollingTargetVol = speedT * rollingVolume;
            float rollingPitch = Mathf.Lerp(rollingPitchRange.x, rollingPitchRange.y, speedT);

            // Skid
            float slipT = 0f;
            bool launchSuppressed = feed.SmoothedThrottle >= launchThrottleSuppression
                && feed.SmoothedSpeedKph < launchSkidMuteSpeedKph
                && feed.SmoothedSlip < fullSlip;
            float effectiveMinSpeed = launchSuppressed ? launchSkidMuteSpeedKph : minimumSkidSpeedKph;
            if (feed.SmoothedSpeedKph > effectiveMinSpeed && feed.SmoothedSlip > slipThreshold)
            {
                slipT = Mathf.InverseLerp(slipThreshold, fullSlip, feed.SmoothedSlip);
            }
            skidTargetVol = slipT * skidVolume;
            float skidPitch = Mathf.Lerp(skidPitchRange.x, skidPitchRange.y, slipT);

            // Apply
            SmoothApply(rollingSource, rollingTargetVol, rollingPitch, dt);
            SmoothApply(skidSource, skidTargetVol, skidPitch, dt);
        }

        private void SmoothApply(AudioSource src, float vol, float pitch, float dt)
        {
            if (src == null) return;
            src.volume = Mathf.Lerp(src.volume, vol, 1f - Mathf.Exp(-volumeResponse * dt));
            src.pitch = Mathf.Lerp(src.pitch, pitch, 1f - Mathf.Exp(-pitchResponse * dt));
        }

        private AudioSource CreateLoop(string name, AudioClip clip)
        {
            if (sourceRoot == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(sourceRoot, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.volume = 0f;
            src.pitch = 1f;
            src.priority = 110;
            src.dopplerLevel = 0f;
            src.spatialBlend = spatialBlend;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.outputAudioMixerGroup = outputGroup;
            if (clip != null) { src.clip = clip; src.Play(); }
            return src;
        }
    }
}

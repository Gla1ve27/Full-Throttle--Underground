using UnityEngine;

namespace FullThrottle.SacredCore.Audio
{
    public sealed class FTSurfaceAudioDirector : MonoBehaviour
    {
        [SerializeField] private float minimumSkidSpeedKph = 32f;
        [SerializeField] private float fullSkidSlip = 0.9f;

        private FTVehicleAudioProfile profile;
        private FTEngineAudioFeed feed;
        private AudioSource skidSource;

        public void Configure(FTVehicleAudioProfile audioProfile, FTEngineAudioFeed audioFeed, Transform root)
        {
            profile = audioProfile;
            feed = audioFeed;
            if (skidSource == null)
            {
                GameObject go = new GameObject("FT_SurfaceSkid");
                go.transform.SetParent(root != null ? root : transform, false);
                skidSource = go.AddComponent<AudioSource>();
                skidSource.loop = true;
                skidSource.playOnAwake = false;
                skidSource.spatialBlend = 0.7f;
                skidSource.dopplerLevel = 0f;
            }

            if (profile != null && profile.skidTire.clip != null)
            {
                skidSource.clip = profile.skidTire.clip;
                if (!skidSource.isPlaying) skidSource.Play();
            }
        }

        private void Update()
        {
            if (profile == null || feed == null || skidSource == null)
            {
                return;
            }

            float slip = feed.SpeedKph >= minimumSkidSpeedKph ? Mathf.InverseLerp(0.28f, fullSkidSlip, feed.Slip01) : 0f;
            skidSource.volume = Mathf.Lerp(skidSource.volume, slip * profile.skidTire.volume, 1f - Mathf.Exp(-14f * Time.deltaTime));
            skidSource.pitch = Mathf.Lerp(0.92f, 1.08f, Mathf.Clamp01(feed.Slip01));
        }
    }
}

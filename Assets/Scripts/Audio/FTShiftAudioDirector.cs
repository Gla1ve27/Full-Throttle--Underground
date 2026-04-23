using UnityEngine;

namespace FullThrottle.SacredCore.Audio
{
    public sealed class FTShiftAudioDirector : MonoBehaviour
    {
        private FTVehicleAudioProfile profile;
        private FTEngineAudioFeed feed;
        private AudioSource source;
        private float lastShiftTime = -999f;
        private float lastLiftTime = -999f;

        public void Configure(FTVehicleAudioProfile audioProfile, FTEngineAudioFeed audioFeed, Transform root)
        {
            profile = audioProfile;
            feed = audioFeed;
            if (source == null)
            {
                GameObject go = new GameObject("FT_ShiftTransients");
                go.transform.SetParent(root != null ? root : transform, false);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0.65f;
                source.dopplerLevel = 0f;
            }
        }

        private void Update()
        {
            if (profile == null || feed == null || source == null)
            {
                return;
            }

            if (feed.GearChangeDirection > 0 || feed.ShiftStartedThisFrame)
            {
                Play(profile.shiftUp, ref lastShiftTime, 1f);
            }
            else if (feed.GearChangeDirection < 0)
            {
                Play(profile.shiftDown, ref lastShiftTime, 0.92f);
            }

            if (feed.ThrottleLiftThisFrame)
            {
                Play(profile.throttleLift, ref lastLiftTime, 0.72f);
            }
        }

        private void Play(FTAudioTransientLayer layer, ref float lastTime, float volumeScale)
        {
            if (layer == null || layer.clip == null || Time.time - lastTime < layer.cooldown)
            {
                return;
            }

            lastTime = Time.time;
            source.pitch = Random.Range(layer.pitchRange.x, layer.pitchRange.y);
            source.PlayOneShot(layer.clip, Mathf.Clamp01(layer.volume * volumeScale));
        }
    }
}

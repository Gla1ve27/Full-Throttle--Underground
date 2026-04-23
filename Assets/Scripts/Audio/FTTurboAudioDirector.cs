using UnityEngine;

namespace FullThrottle.SacredCore.Audio
{
    public sealed class FTTurboAudioDirector : MonoBehaviour
    {
        private FTVehicleAudioProfile profile;
        private FTEngineAudioFeed feed;
        private AudioSource spoolSource;
        private AudioSource whistleSource;
        private AudioSource transientSource;
        private float spool01;
        private float lastBlowoffTime = -999f;

        public void Configure(FTVehicleAudioProfile audioProfile, FTEngineAudioFeed audioFeed, Transform root)
        {
            profile = audioProfile;
            feed = audioFeed;
            Transform parent = root != null ? root : transform;
            if (spoolSource == null)
            {
                spoolSource = CreateLoopSource("FT_TurboSpool", parent);
            }

            if (whistleSource == null)
            {
                whistleSource = CreateLoopSource("FT_TurboWhistle", parent);
            }

            if (transientSource == null)
            {
                GameObject go = new GameObject("FT_TurboTransients");
                go.transform.SetParent(parent, false);
                transientSource = go.AddComponent<AudioSource>();
                transientSource.playOnAwake = false;
                transientSource.spatialBlend = 0.65f;
            }

            if (profile != null && profile.turboSpool.clip != null)
            {
                spoolSource.clip = profile.turboSpool.clip;
                spoolSource.loop = true;
                if (!spoolSource.isPlaying) spoolSource.Play();
            }

            if (profile != null && profile.turboWhistle.clip != null)
            {
                whistleSource.clip = profile.turboWhistle.clip;
                whistleSource.loop = true;
                if (!whistleSource.isPlaying) whistleSource.Play();
            }
        }

        private void Update()
        {
            if (profile == null || feed == null || spoolSource == null)
            {
                return;
            }

            float target = feed.Throttle * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.24f, 0.86f, feed.NormalizedRPM));
            spool01 = Mathf.Lerp(spool01, target, 1f - Mathf.Exp(-(target > spool01 ? 8f : 14f) * Time.deltaTime));
            spoolSource.volume = Mathf.Lerp(spoolSource.volume, spool01 * profile.turboSpool.volume, 1f - Mathf.Exp(-10f * Time.deltaTime));
            spoolSource.pitch = Mathf.Lerp(0.85f, 1.32f, spool01);

            if (whistleSource != null && whistleSource.clip != null)
            {
                float whistleTarget = spool01
                    * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.46f, 0.92f, feed.NormalizedRPM))
                    * profile.turboWhistle.volume;
                whistleSource.volume = Mathf.Lerp(whistleSource.volume, whistleTarget, 1f - Mathf.Exp(-12f * Time.deltaTime));
                whistleSource.pitch = Mathf.Lerp(0.92f, 1.48f, Mathf.Clamp01(spool01));
            }

            if (feed.ThrottleLiftThisFrame && spool01 > 0.38f && profile.turboBlowoff.clip != null && Time.time - lastBlowoffTime >= profile.turboBlowoff.cooldown)
            {
                lastBlowoffTime = Time.time;
                transientSource.pitch = Random.Range(profile.turboBlowoff.pitchRange.x, profile.turboBlowoff.pitchRange.y);
                transientSource.PlayOneShot(profile.turboBlowoff.clip, profile.turboBlowoff.volume);
            }
        }

        private static AudioSource CreateLoopSource(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.volume = 0f;
            source.spatialBlend = 0.65f;
            source.dopplerLevel = 0f;
            return source;
        }
    }
}

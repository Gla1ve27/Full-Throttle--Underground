using UnityEngine;

namespace FullThrottle.SacredCore.Audio
{
    public sealed class FTSweetenerAudioDirector : MonoBehaviour
    {
        private FTVehicleAudioProfile profile;
        private FTEngineAudioFeed feed;
        private AudioSource intakeSource;
        private AudioSource drivetrainSource;
        private AudioSource sputterSource;
        private AudioSource transientSource;
        private float lastCrackleTime = -999f;
        private float lastSparkTime = -999f;

        public void Configure(FTVehicleAudioProfile audioProfile, FTEngineAudioFeed audioFeed, Transform root)
        {
            profile = audioProfile;
            feed = audioFeed;
            Transform parent = root != null ? root : transform;

            if (intakeSource == null) intakeSource = CreateLoopSource("FT_Sweetener_Intake", parent);
            if (drivetrainSource == null) drivetrainSource = CreateLoopSource("FT_Sweetener_Drivetrain", parent);
            if (sputterSource == null) sputterSource = CreateLoopSource("FT_Sweetener_Sputter", parent);
            if (transientSource == null) transientSource = CreateTransientSource("FT_Sweetener_Transients", parent);

            AssignLoop(intakeSource, profile?.intake);
            AssignLoop(drivetrainSource, profile?.drivetrainWhine);
            AssignLoop(sputterSource, profile?.sweetenerSputterLoop);
        }

        private void Update()
        {
            if (profile == null || feed == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            float rpm01 = feed.NormalizedRPM;
            float throttle = feed.Throttle;
            float speed01 = Mathf.InverseLerp(0f, 260f, feed.SpeedKph);
            float interiorSweetenerScale = Mathf.Lerp(1f, profile.interiorExterior.interiorSweetenerScale, profile.interiorExterior.interiorBlend);

            float intakeTarget = throttle
                * Smooth01(Mathf.InverseLerp(0.18f, 0.82f, rpm01))
                * profile.intake.volume
                * interiorSweetenerScale;
            ApplyLoop(intakeSource, intakeTarget, Mathf.Lerp(0.86f, 1.32f, rpm01), dt, 12f);

            float drivetrainTarget = speed01
                * Mathf.Lerp(0.35f, 1f, rpm01)
                * Mathf.Lerp(0.55f, 1f, throttle)
                * profile.drivetrainWhine.volume
                * interiorSweetenerScale;
            ApplyLoop(drivetrainSource, drivetrainTarget, Mathf.Lerp(0.8f, 1.46f, Mathf.Max(speed01, rpm01)), dt, 10f);

            float sputterGate = rpm01 >= profile.sputterMinRPM01 && throttle <= profile.sputterThrottleUpper && feed.SpeedKph > 10f
                ? Smooth01(Mathf.InverseLerp(profile.sputterMinRPM01, profile.sputterMinRPM01 + 0.18f, rpm01))
                : 0f;
            ApplyLoop(sputterSource, sputterGate * profile.sweetenerSputterLoop.volume * interiorSweetenerScale, Mathf.Lerp(0.9f, 1.18f, rpm01), dt, 18f);

            TryPlayLiftCrackle(rpm01);
            TryPlaySparkChatter(rpm01, dt);
        }

        private void TryPlayLiftCrackle(float rpm01)
        {
            if (!feed.ThrottleLiftThisFrame || rpm01 < profile.crackleMinRPM01)
            {
                return;
            }

            if (Random.value > Mathf.Clamp01(profile.crackleChancePerSecond * 0.16f))
            {
                return;
            }

            PlayTransient(profile.sweetenerCrackle, ref lastCrackleTime);
        }

        private void TryPlaySparkChatter(float rpm01, float dt)
        {
            if (feed.Throttle > 0.22f || rpm01 < profile.crackleMinRPM01 || feed.SpeedKph < 18f)
            {
                return;
            }

            float chance = profile.sparkChatterChancePerSecond * dt;
            if (chance <= 0f || Random.value > chance)
            {
                return;
            }

            PlayTransient(profile.sparkChatter, ref lastSparkTime);
        }

        private void PlayTransient(FTAudioTransientLayer layer, ref float lastTime)
        {
            if (transientSource == null || layer == null || layer.clip == null || Time.time - lastTime < layer.cooldown)
            {
                return;
            }

            lastTime = Time.time;
            transientSource.pitch = Random.Range(layer.pitchRange.x, layer.pitchRange.y);
            transientSource.PlayOneShot(layer.clip, Mathf.Clamp01(layer.volume));
        }

        private static void AssignLoop(AudioSource source, FTAudioLoopLayer layer)
        {
            if (source == null)
            {
                return;
            }

            source.clip = layer != null ? layer.clip : null;
            source.volume = 0f;
            source.pitch = 1f;
            if (source.clip != null && !source.isPlaying)
            {
                source.Play();
            }
        }

        private static void ApplyLoop(AudioSource source, float targetVolume, float targetPitch, float dt, float response)
        {
            if (source == null || source.clip == null)
            {
                return;
            }

            source.volume = Mathf.Lerp(source.volume, Mathf.Clamp01(targetVolume), 1f - Mathf.Exp(-response * dt));
            source.pitch = Mathf.Lerp(source.pitch, Mathf.Clamp(targetPitch, 0.25f, 2.5f), 1f - Mathf.Exp(-8f * dt));
            if (source.volume > 0.001f && !source.isPlaying)
            {
                source.Play();
            }
        }

        private static AudioSource CreateLoopSource(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            source.spatialBlend = 0.65f;
            source.dopplerLevel = 0f;
            return source;
        }

        private static AudioSource CreateTransientSource(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.65f;
            source.dopplerLevel = 0f;
            return source;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}

using UnityEngine;
using UnityEngine.Audio;

namespace Underground.Audio.V2
{
    /// <summary>
    /// Handles one-shot transients: gear shifts, throttle blips, exhaust pops,
    /// turbo blow-off, and lift-off crackles.
    /// </summary>
    public sealed class EngineTransientPlayer : MonoBehaviour
    {
        [Header("Shift")]
        [SerializeField] private AudioClip shiftUpClip;
        [SerializeField] private AudioClip shiftDownClip;
        [SerializeField, Range(0f, 1f)] private float shiftVolume = 0.65f;
        [SerializeField, Range(0f, 1f)] private float shiftDownVolume = 0.58f;
        [SerializeField] private Vector2 shiftPitchRange = new Vector2(0.96f, 1.04f);
        [SerializeField] private float shiftCooldown = 0.08f;

        [Header("Throttle Blip")]
        [SerializeField] private AudioClip throttleBlipClip;
        [SerializeField, Range(0f, 1f)] private float blipVolume = 0.55f;
        [SerializeField] private Vector2 blipPitchRange = new Vector2(0.95f, 1.05f);

        [Header("Exhaust Pop")]
        [SerializeField] private AudioClip exhaustPopClip;
        [SerializeField, Range(0f, 1f)] private float popVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float popChancePerSecond = 0.25f;
        [SerializeField, Range(0f, 1f)] private float popMinRpm01 = 0.45f;

        [Header("Crackle")]
        [SerializeField] private AudioClip crackleClip;
        [SerializeField, Range(0f, 1f)] private float crackleVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float crackleMinRpm01 = 0.45f;
        [SerializeField] private float crackleChancePerSecond = 2.5f;
        [SerializeField] private bool enableLiftOffCrackles = true;

        [Header("Turbo Blow-Off")]
        [SerializeField] private AudioClip blowOffClip;
        [SerializeField, Range(0f, 1f)] private float blowOffVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float blowOffThrottleDrop = 0.38f;
        [SerializeField, Range(0f, 1f)] private float blowOffMinSpool = 0.42f;

        [Header("Spatial")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.78f;
        [SerializeField] private float minDistance = 3f;
        [SerializeField] private float maxDistance = 55f;
        [SerializeField] private AudioMixerGroup outputGroup;

        private AudioSource sourceA;
        private AudioSource sourceB;
        private int flipFlop;
        private float lastShiftTime = -999f;
        private float lastOneShotTime = -999f;
        private float lastPopTime = -999f;
        private float lastCrackleTime = -999f;
        private float lastBlowOffTime = -999f;
        private float previousThrottle;
        private float globalCooldown = 0.12f;
        private Transform sourceRoot;

        public void Initialize(Transform root)
        {
            sourceRoot = root;
            sourceA = CreateSource("ET_A");
            sourceB = CreateSource("ET_B");
        }

        public void ApplyFromBank(NFSU2CarAudioBank.TierAudioPackage tier, bool isExterior)
        {
            if (tier == null) return;

            var shift = tier.shift;
            if (shift != null)
            {
                shiftUpClip = shift.shiftUp?.clip;
                shiftDownClip = shift.shiftDown?.clip;
                shiftVolume = shift.shiftUp?.volume ?? 0.65f;
                shiftDownVolume = shift.shiftDown?.volume ?? shiftVolume;
                shiftCooldown = shift.cooldown;
            }

            throttleBlipClip = tier.accelFromIdle?.clip;
            blipVolume = Mathf.Clamp01((tier.accelFromIdle?.volume ?? 0.55f) * 0.75f);

            var sweet = tier.sweetener;
            if (sweet != null)
            {
                crackleClip = sweet.crackle?.clip;
                exhaustPopClip = sweet.crackle?.clip;
                crackleVolume = sweet.crackle?.volume ?? 0.45f;
                popVolume = Mathf.Clamp01(crackleVolume * 0.85f);
                crackleMinRpm01 = sweet.crackleMinRpm01;
                popMinRpm01 = sweet.crackleMinRpm01;
                crackleChancePerSecond = sweet.crackleChancePerSecond;
                popChancePerSecond = Mathf.Max(0.1f, sweet.crackleChancePerSecond * 0.35f);
                enableLiftOffCrackles = sweet.enableLiftOffCrackles;
            }

            var turbo = tier.turbo;
            if (turbo != null)
            {
                blowOffClip = turbo.blowOff?.clip;
                blowOffVolume = turbo.blowOff?.volume ?? 0.7f;
                blowOffThrottleDrop = turbo.blowOffThrottleDrop;
                blowOffMinSpool = turbo.blowOffMinSpool;
            }
        }

        public void UpdateTransients(EngineAudioStateFeed feed, float dt)
        {
            if (feed == null) return;

            // Shift one-shots
            if (feed.ShiftJustStarted && Time.time - lastShiftTime >= shiftCooldown)
            {
                AudioClip clip = feed.LastShiftDirection == Vehicle.V2.ShiftDirection.Up ? shiftUpClip : shiftDownClip;
                if (clip != null)
                {
                    float volume = feed.LastShiftDirection == Vehicle.V2.ShiftDirection.Up ? shiftVolume : shiftDownVolume;
                    PlayOneShot(clip, volume, Random.Range(shiftPitchRange.x, shiftPitchRange.y));
                    lastShiftTime = Time.time;
                }

                // Throttle blip on downshift
                if (feed.LastShiftDirection == Vehicle.V2.ShiftDirection.Down && throttleBlipClip != null)
                {
                    PlayOneShot(throttleBlipClip, blipVolume, Random.Range(blipPitchRange.x, blipPitchRange.y));
                }
            }

            // Exhaust pop on lift-off
            if (exhaustPopClip != null && feed.SmoothedThrottle < 0.08f && feed.NormalizedRPM > popMinRpm01
                && Time.time - lastPopTime > 0.3f)
            {
                if (Random.value < popChancePerSecond * dt)
                {
                    PlayOneShot(exhaustPopClip, popVolume, Random.Range(0.94f, 1.06f));
                    lastPopTime = Time.time;
                }
            }

            // Lift crackle
            if (enableLiftOffCrackles && crackleClip != null && feed.SmoothedThrottle < 0.12f
                && feed.NormalizedRPM > crackleMinRpm01 && Time.time - lastCrackleTime > 0.15f)
            {
                if (Random.value < crackleChancePerSecond * dt)
                {
                    PlayOneShot(crackleClip, crackleVolume, Random.Range(0.92f, 1.08f));
                    lastCrackleTime = Time.time;
                }
            }

            // Turbo blow-off
            if (blowOffClip != null && feed.TurboSpool > blowOffMinSpool
                && previousThrottle - feed.SmoothedThrottle > blowOffThrottleDrop
                && Time.time - lastBlowOffTime > 0.5f)
            {
                PlayOneShot(blowOffClip, blowOffVolume, Random.Range(0.96f, 1.04f));
                lastBlowOffTime = Time.time;
            }

            previousThrottle = feed.SmoothedThrottle;
        }

        private void PlayOneShot(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || Time.time - lastOneShotTime < globalCooldown) return;

            AudioSource src = flipFlop == 0 ? sourceA : sourceB;
            flipFlop = 1 - flipFlop;
            if (src == null) return;

            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.Play();
            lastOneShotTime = Time.time;
        }

        private AudioSource CreateSource(string name)
        {
            if (sourceRoot == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(sourceRoot, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.volume = 0f;
            src.pitch = 1f;
            src.priority = 90;
            src.dopplerLevel = 0f;
            src.spatialBlend = spatialBlend;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.outputAudioMixerGroup = outputGroup;
            return src;
        }
    }
}

using System;
using UnityEngine;

namespace Underground.Audio
{
    [CreateAssetMenu(
        fileName = "NFSU2CarAudioBank",
        menuName = "Full Throttle/Audio/NFSU2 Car Audio Bank")]
    public class NFSU2CarAudioBank : ScriptableObject
    {
        [Serializable]
        public sealed class AudioLoopLayer
        {
            public AudioClip exteriorClip;
            public AudioClip interiorClip;
            [Range(0f, 2f)] public float exteriorVolume = 1f;
            [Range(0f, 2f)] public float interiorVolume = 0.75f;
            public Vector2 pitchRange = new Vector2(0.85f, 1.25f);
        }

        [Serializable]
        public sealed class AudioSweepLayer
        {
            public AudioClip exteriorClip;
            public AudioClip interiorClip;
            [Range(0f, 2f)] public float exteriorVolume = 1f;
            [Range(0f, 2f)] public float interiorVolume = 0.75f;
            [Tooltip("Normalized clip region that contains the usable idle-to-redline sweep.")]
            public Vector2 playbackWindow01 = new Vector2(0.03f, 0.97f);
            [Tooltip("Sweep recordings already contain the RPM rise/fall, so keep this near 1.0.")]
            public Vector2 pitchRange = new Vector2(0.98f, 1.02f);

            internal void EnsureDefaults()
            {
                playbackWindow01.x = Mathf.Clamp01(playbackWindow01.x);
                playbackWindow01.y = Mathf.Clamp01(playbackWindow01.y);
                if (playbackWindow01.y <= playbackWindow01.x + 0.01f)
                    playbackWindow01.y = Mathf.Min(1f, playbackWindow01.x + 0.01f);

                pitchRange.x = Mathf.Clamp(pitchRange.x, 0.9f, 1.1f);
                pitchRange.y = Mathf.Clamp(pitchRange.y, 0.9f, 1.1f);
            }
        }

        [Serializable]
        public sealed class AudioOneShotLayer
        {
            public AudioClip clip;
            [Range(0f, 2f)] public float volume = 1f;
            public Vector2 pitchRange = new Vector2(0.96f, 1.04f);
        }

        [Serializable]
        public sealed class ShiftAudioPackage
        {
            [Range(0f, 1f)] public float duckAmount = 0.28f;
            [Min(0f)] public float duckDuration = 0.12f;
            [Min(0f)] public float cooldown = 0.08f;
            [Range(0f, 0.5f)] public float pitchDrop = 0.12f;
            public AudioOneShotLayer shiftUp = new AudioOneShotLayer();
            public AudioOneShotLayer shiftDown = new AudioOneShotLayer();

            internal void EnsureDefaults()
            {
                shiftUp ??= new AudioOneShotLayer();
                shiftDown ??= new AudioOneShotLayer();
            }
        }

        [Serializable]
        public sealed class TurboAudioPackage
        {
            [Range(0f, 2f)] public float strength = 0.65f;
            [Min(0.01f)] public float spoolAttack = 8f;
            [Min(0.01f)] public float spoolRelease = 5f;
            [Range(0f, 1f)] public float whistleStartRpm01 = 0.48f;
            [Range(0f, 1f)] public float whistleStartThrottle = 0.42f;
            [Range(0f, 1f)] public float blowOffThrottleDrop = 0.38f;
            [Range(0f, 1f)] public float blowOffMinSpool = 0.32f;
            public AudioLoopLayer spool = new AudioLoopLayer { exteriorVolume = 0.65f, interiorVolume = 0.35f };
            public AudioLoopLayer whistle = new AudioLoopLayer { exteriorVolume = 0.55f, interiorVolume = 0.25f };
            public AudioOneShotLayer blowOff = new AudioOneShotLayer { volume = 0.8f };

            internal void EnsureDefaults()
            {
                spool ??= new AudioLoopLayer { exteriorVolume = 0.65f, interiorVolume = 0.35f };
                whistle ??= new AudioLoopLayer { exteriorVolume = 0.55f, interiorVolume = 0.25f };
                blowOff ??= new AudioOneShotLayer { volume = 0.8f };
            }
        }

        [Serializable]
        public sealed class SweetenerAudioPackage
        {
            public AudioLoopLayer intake = new AudioLoopLayer { exteriorVolume = 0.5f, interiorVolume = 0.7f };
            public AudioLoopLayer drivetrain = new AudioLoopLayer { exteriorVolume = 0.45f, interiorVolume = 0.55f };
            public AudioLoopLayer sputter = new AudioLoopLayer { exteriorVolume = 0.35f, interiorVolume = 0.3f };
            public AudioOneShotLayer crackle = new AudioOneShotLayer { volume = 0.65f };
            public AudioOneShotLayer sparkChatter = new AudioOneShotLayer { volume = 0.45f };
            public bool enableLiftOffCrackles = true;
            [Range(0f, 1f)] public float crackleMinRpm01 = 0.45f;
            [Min(0f)] public float crackleChancePerSecond = 2.5f;
            [Range(0f, 1f)] public float sputterMinRpm01 = 0.35f;
            [Range(0f, 1f)] public float sputterThrottleUpper = 0.18f;
            [Range(0f, 1f)] public float sputterVolume = 0f;
            [Range(0f, 1f)] public float sparkChatterChancePerSecond = 0f;

            internal void EnsureDefaults()
            {
                intake ??= new AudioLoopLayer { exteriorVolume = 0.5f, interiorVolume = 0.7f };
                drivetrain ??= new AudioLoopLayer { exteriorVolume = 0.45f, interiorVolume = 0.55f };
                sputter ??= new AudioLoopLayer { exteriorVolume = 0.35f, interiorVolume = 0.3f };
                crackle ??= new AudioOneShotLayer { volume = 0.65f };
                sparkChatter ??= new AudioOneShotLayer { volume = 0.45f };
            }
        }

        [Serializable]
        public sealed class SkidAudioPackage
        {
            public AudioLoopLayer skid = new AudioLoopLayer { exteriorVolume = 1f, interiorVolume = 0.35f };
            [Min(0f)] public float slipThreshold = 0.18f;
            [Min(0.01f)] public float fullSlip = 0.75f;

            internal void EnsureDefaults()
            {
                skid ??= new AudioLoopLayer { exteriorVolume = 1f, interiorVolume = 0.35f };
                fullSlip = Mathf.Max(fullSlip, slipThreshold + 0.01f);
            }
        }

        [Serializable]
        public sealed class TierAudioPackage
        {
            [Range(0f, 2f)] public float tierMasterVolume = 1f;

            [Header("Engine Core")]
            public AudioLoopLayer[] engineBands = CreateEngineBands();
            public AudioLoopLayer idle = new AudioLoopLayer { pitchRange = new Vector2(0.92f, 1.08f) };
            public AudioSweepLayer accelSweep = new AudioSweepLayer();
            public AudioSweepLayer decelSweep = new AudioSweepLayer { exteriorVolume = 0.85f, interiorVolume = 0.65f };
            public AudioLoopLayer accelLow = new AudioLoopLayer();
            public AudioLoopLayer accelMid = new AudioLoopLayer();
            public AudioLoopLayer accelHigh = new AudioLoopLayer();
            public AudioLoopLayer decelLow = new AudioLoopLayer { exteriorVolume = 0.85f, interiorVolume = 0.65f };
            public AudioLoopLayer decelMid = new AudioLoopLayer { exteriorVolume = 0.85f, interiorVolume = 0.65f };
            public AudioLoopLayer decelHigh = new AudioLoopLayer { exteriorVolume = 0.8f, interiorVolume = 0.6f };
            public AudioLoopLayer limiter = new AudioLoopLayer { pitchRange = new Vector2(0.96f, 1.04f) };
            public AudioLoopLayer reverse = new AudioLoopLayer { pitchRange = new Vector2(0.75f, 1.15f) };

            [Header("Transient Packages")]
            public AudioOneShotLayer accelFromIdle = new AudioOneShotLayer { volume = 0.8f };
            public ShiftAudioPackage shift = new ShiftAudioPackage();
            public TurboAudioPackage turbo = new TurboAudioPackage();
            public SweetenerAudioPackage sweetener = new SweetenerAudioPackage();
            public SkidAudioPackage skid = new SkidAudioPackage();

            internal void EnsureDefaults()
            {
                EnsureEngineBands();
                idle ??= new AudioLoopLayer { pitchRange = new Vector2(0.92f, 1.08f) };
                accelSweep ??= new AudioSweepLayer();
                decelSweep ??= new AudioSweepLayer { exteriorVolume = 0.85f, interiorVolume = 0.65f };
                accelLow ??= new AudioLoopLayer();
                accelMid ??= new AudioLoopLayer();
                accelHigh ??= new AudioLoopLayer();
                decelLow ??= new AudioLoopLayer { exteriorVolume = 0.85f, interiorVolume = 0.65f };
                decelMid ??= new AudioLoopLayer { exteriorVolume = 0.85f, interiorVolume = 0.65f };
                decelHigh ??= new AudioLoopLayer { exteriorVolume = 0.8f, interiorVolume = 0.6f };
                limiter ??= new AudioLoopLayer { pitchRange = new Vector2(0.96f, 1.04f) };
                reverse ??= new AudioLoopLayer { pitchRange = new Vector2(0.75f, 1.15f) };
                accelFromIdle ??= new AudioOneShotLayer { volume = 0.8f };
                shift ??= new ShiftAudioPackage();
                turbo ??= new TurboAudioPackage();
                sweetener ??= new SweetenerAudioPackage();
                skid ??= new SkidAudioPackage();

                shift.EnsureDefaults();
                turbo.EnsureDefaults();
                sweetener.EnsureDefaults();
                skid.EnsureDefaults();
                accelSweep.EnsureDefaults();
                decelSweep.EnsureDefaults();
            }

            public AudioLoopLayer GetEngineBand(int index)
            {
                EnsureEngineBands();
                return engineBands[Mathf.Clamp(index, 0, engineBands.Length - 1)];
            }

            private void EnsureEngineBands()
            {
                if (engineBands == null || engineBands.Length != 8)
                {
                    AudioLoopLayer[] resized = CreateEngineBands();
                    if (engineBands != null)
                    {
                        int copyCount = Mathf.Min(engineBands.Length, resized.Length);
                        for (int i = 0; i < copyCount; i++)
                            resized[i] = engineBands[i] ?? resized[i];
                    }

                    engineBands = resized;
                }

                for (int i = 0; i < engineBands.Length; i++)
                    engineBands[i] ??= CreateEngineBand();
            }

            private static AudioLoopLayer[] CreateEngineBands()
            {
                AudioLoopLayer[] bands = new AudioLoopLayer[8];
                for (int i = 0; i < bands.Length; i++)
                    bands[i] = CreateEngineBand();

                return bands;
            }

            private static AudioLoopLayer CreateEngineBand()
            {
                return new AudioLoopLayer
                {
                    exteriorVolume = 1f,
                    interiorVolume = 0.82f,
                    pitchRange = new Vector2(0.96f, 1.04f)
                };
            }
        }

        [Serializable]
        public sealed class RuntimeTuning
        {
            [Header("Output")]
            [Range(0f, 2f)] public float masterVolume = 1f;
            [Range(0f, 1f)] public float spatialBlend = 1f;
            [Min(0f)] public float minDistance = 4f;
            [Min(0.01f)] public float maxDistance = 85f;
            [Min(0.01f)] public float layerFadeSpeed = 9f;

            [Header("Telemetry Smoothing")]
            [Min(0.01f)] public float rpmRiseResponse = 12f;
            [Min(0.01f)] public float rpmFallResponse = 8f;
            [Min(0.01f)] public float throttleResponse = 16f;
            [Min(0.01f)] public float brakeResponse = 16f;
            [Min(0.01f)] public float speedResponse = 8f;

            [Header("State Thresholds")]
            [Range(0f, 1f)] public float idleUpper01 = 0.18f;
            [Range(0f, 1f)] public float decelSweepLowRpmCutoff01 = 0.12f;
            [Range(0f, 1f)] public float lowUpper01 = 0.42f;
            [Range(0f, 1f)] public float midUpper01 = 0.72f;
            [Range(0f, 1f)] public float limiterEnter01 = 0.94f;
            [Range(0f, 1f)] public float throttleOnThreshold = 0.12f;
            [Range(0f, 1f)] public float throttleOffThreshold = 0.06f;
            [Min(0f)] public float idleSpeedThresholdKph = 6f;

            [Header("Launch")]
            [Range(0f, 1f)] public float launchThrottleThreshold = 0.62f;
            [Range(0f, 1f)] public float launchRpmUpper01 = 0.36f;
            [Min(0f)] public float launchSpeedUpperKph = 9f;
            [Min(0f)] public float launchHoldTime = 0.22f;

            [Header("Engine Band Bank")]
            [Range(0f, 1f)] public float band01Center = 0.08f;
            [Range(0f, 1f)] public float band02Center = 0.16f;
            [Range(0f, 1f)] public float band03Center = 0.28f;
            [Range(0f, 1f)] public float band04Center = 0.40f;
            [Range(0f, 1f)] public float band05Center = 0.54f;
            [Range(0f, 1f)] public float band06Center = 0.68f;
            [Range(0f, 1f)] public float band07Center = 0.82f;
            [Range(0f, 1f)] public float band08Center = 0.94f;
            [Range(0.02f, 0.35f)] public float engineBandWidth = 0.16f;
            [Range(0f, 1f)] public float loopBankDominantVolume = 0.55f;
            [Range(0f, 1f)] public float accelCharacterVolume = 0f;
            [Range(0f, 1f)] public float decelCharacterVolume = 0f;

            [Header("Generated Mid/High Roar")]
            [Range(0f, 1f)] public float midHighRoarVolume = 0.42f;
            [Range(0f, 1f)] public float midHighRoarStart01 = 0.34f;
            [Range(0f, 1f)] public float highRoarFull01 = 0.82f;
            [Range(0f, 1f)] public float roarThrottleFloor = 0.28f;
            public Vector2 roarPitchRange = new Vector2(0.88f, 1.56f);
            [Range(0f, 1f)] public float highRpmScreamVolume = 0.36f;
            [Range(0f, 1f)] public float highRpmScreamStart01 = 0.72f;
            [Range(0f, 1f)] public float highRpmScreamFull01 = 0.96f;
            [Range(0f, 1f)] public float highRpmScreamThrottleFloor = 0.18f;
            public Vector2 highRpmScreamPitchRange = new Vector2(0.92f, 1.82f);

            [Header("Sweep Scrubbing")]
            [Min(0.01f)] public float sweepSeekResponse = 6f;
            [Range(0f, 1f)] public float sweepDominantVolume = 0.75f;
            [Min(0.01f)] public float sweepResyncThresholdSeconds = 0.75f;
            [Range(0f, 0.1f)] public float sweepPitchCorrectionLimit = 0.025f;

            [Header("Interior Detection")]
            public Vector3 interiorDetectionExtents = new Vector3(1.2f, 1.1f, 1.7f);
            [Min(0.01f)] public float interiorTransitionSpeed = 6f;

            public float GetBandCenter(int index)
            {
                return index switch
                {
                    0 => band01Center,
                    1 => band02Center,
                    2 => band03Center,
                    3 => band04Center,
                    4 => band05Center,
                    5 => band06Center,
                    6 => band07Center,
                    _ => band08Center
                };
            }
        }

        public VehicleAudioTier defaultTier = VehicleAudioTier.Stock;
        public RuntimeTuning tuning = new RuntimeTuning();

        [Header("Tier Packages")]
        public TierAudioPackage stock = new TierAudioPackage();
        public TierAudioPackage street = new TierAudioPackage { tierMasterVolume = 1.05f };
        public TierAudioPackage pro = new TierAudioPackage { tierMasterVolume = 1.1f };
        public TierAudioPackage extreme = new TierAudioPackage { tierMasterVolume = 1.15f };

        public TierAudioPackage GetTier(VehicleAudioTier tier)
        {
            EnsureDefaults();

            return tier switch
            {
                VehicleAudioTier.Street => street,
                VehicleAudioTier.Pro => pro,
                VehicleAudioTier.Extreme => extreme,
                _ => stock
            };
        }

        private void OnValidate()
        {
            EnsureDefaults();
            defaultTier = (VehicleAudioTier)Mathf.Clamp((int)defaultTier, 0, 3);
        }

        private void EnsureDefaults()
        {
            tuning ??= new RuntimeTuning();
            stock ??= new TierAudioPackage();
            street ??= new TierAudioPackage { tierMasterVolume = 1.05f };
            pro ??= new TierAudioPackage { tierMasterVolume = 1.1f };
            extreme ??= new TierAudioPackage { tierMasterVolume = 1.15f };

            stock.EnsureDefaults();
            street.EnsureDefaults();
            pro.EnsureDefaults();
            extreme.EnsureDefaults();
        }
    }
}

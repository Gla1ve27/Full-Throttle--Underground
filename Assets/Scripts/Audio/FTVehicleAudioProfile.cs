using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace FullThrottle.SacredCore.Audio
{
    [CreateAssetMenu(menuName = "Full Throttle/Sacred Core/Vehicle Audio Profile", fileName = "FT_VehicleAudioProfile")]
    public sealed class FTVehicleAudioProfile : FTAudioProfile
    {
        [Header("Family Inheritance")]
        public FTVehicleAudioProfile familyBaseProfile;
        [Tooltip("Playable and named rival cars must be meaningfully overridden if this is enabled.")]
        public bool inheritsFamily;

        [Header("Engine Bed")]
        public FTAudioLoopLayer idle = new FTAudioLoopLayer { referenceRPM = 900f, minRPM = 650f, maxRPM = 1500f, volume = 0.75f };
        public FTAudioLoopLayer lowAccel = new FTAudioLoopLayer { referenceRPM = 1900f, minRPM = 1050f, maxRPM = 2950f, volume = 0.74f };
        public FTAudioLoopLayer midAccel = new FTAudioLoopLayer { referenceRPM = 3650f, minRPM = 2750f, maxRPM = 5450f, volume = 0.78f };
        public FTAudioLoopLayer highAccel = new FTAudioLoopLayer { referenceRPM = 5700f, minRPM = 4700f, maxRPM = 7100f, volume = 0.76f };
        public FTAudioLoopLayer topRedline = new FTAudioLoopLayer { referenceRPM = 7400f, minRPM = 7000f, maxRPM = 8150f, volume = 0.62f };
        public FTAudioLoopLayer lowDecel = new FTAudioLoopLayer { referenceRPM = 1700f, minRPM = 900f, maxRPM = 2450f, volume = 0.38f };
        public FTAudioLoopLayer midDecel = new FTAudioLoopLayer { referenceRPM = 3500f, minRPM = 2700f, maxRPM = 5000f, volume = 0.40f };
        public FTAudioLoopLayer highDecel = new FTAudioLoopLayer { referenceRPM = 6750f, minRPM = 6300f, maxRPM = 7200f, volume = 0.20f };

        [Header("Transients")]
        public FTAudioTransientLayer shiftUp = new FTAudioTransientLayer { volume = 0.72f };
        public FTAudioTransientLayer shiftDown = new FTAudioTransientLayer { volume = 0.66f };
        public FTAudioTransientLayer throttleLift = new FTAudioTransientLayer { volume = 0.20f, cooldown = 0.18f };

        [Header("Support Layers")]
        public FTAudioLoopLayer intake = new FTAudioLoopLayer { volume = 0.08f };
        public FTAudioLoopLayer turboSpool = new FTAudioLoopLayer { volume = 0.18f };
        public FTAudioTransientLayer turboBlowoff = new FTAudioTransientLayer { volume = 0.36f, cooldown = 0.45f };
        public FTAudioLoopLayer drivetrainWhine = new FTAudioLoopLayer { volume = 0.055f };
        public FTAudioLoopLayer skidTire = new FTAudioLoopLayer { volume = 0.34f };

        [Header("UG2-Style Sweeteners")]
        public FTAudioLoopLayer turboWhistle = new FTAudioLoopLayer { volume = 0.08f };
        public FTAudioLoopLayer sweetenerSputterLoop = new FTAudioLoopLayer { volume = 0.035f, referenceRPM = 3600f, minRPM = 2200f, maxRPM = 7600f };
        public FTAudioTransientLayer sweetenerCrackle = new FTAudioTransientLayer { volume = 0.18f, cooldown = 0.08f };
        public FTAudioTransientLayer sparkChatter = new FTAudioTransientLayer { volume = 0.08f, cooldown = 0.12f };
        [Range(0f, 1f)] public float crackleMinRPM01 = 0.48f;
        [Range(0f, 8f)] public float crackleChancePerSecond = 1.8f;
        [Range(0f, 1f)] public float sputterMinRPM01 = 0.34f;
        [Range(0f, 1f)] public float sputterThrottleUpper = 0.16f;
        [Range(0f, 2f)] public float sparkChatterChancePerSecond = 0.18f;

        [Header("Optional Sweep Support")]
        public FTAudioSweepLayer accelSweep = new FTAudioSweepLayer();
        public FTAudioSweepLayer decelSweep = new FTAudioSweepLayer { volume = 0.14f };

        [Header("Mix Context")]
        public FTAudioInteriorExteriorSettings interiorExterior = new FTAudioInteriorExteriorSettings();
        public FTAudioModePresetOverrides modePresets = new FTAudioModePresetOverrides();

        [Header("Behavior")]
        public FTAudioLimiterSettings limiter = new FTAudioLimiterSettings();
        public FTAudioResponseSettings response = new FTAudioResponseSettings();
        public FTGaragePreviewSettings garagePreview = new FTGaragePreviewSettings();
        public List<FTAudioUpgradeModifier> upgradeModifiers = new List<FTAudioUpgradeModifier>
        {
            new FTAudioUpgradeModifier { stage = FTAudioUpgradeStage.Stock },
            new FTAudioUpgradeModifier { stage = FTAudioUpgradeStage.Street, toneFullness = 1.05f, inductionLoudness = 1.08f },
            new FTAudioUpgradeModifier { stage = FTAudioUpgradeStage.Sport, toneFullness = 1.10f, shiftAggression = 1.12f, inductionLoudness = 1.18f },
            new FTAudioUpgradeModifier { stage = FTAudioUpgradeStage.Race, toneFullness = 1.18f, shiftAggression = 1.25f, inductionLoudness = 1.35f, limiterHarshness = 1.18f },
            new FTAudioUpgradeModifier { stage = FTAudioUpgradeStage.Elite, toneFullness = 1.24f, shiftAggression = 1.35f, inductionLoudness = 1.45f, turboResponse = 1.22f, limiterHarshness = 1.28f }
        };

        public bool ValidateRequiredLayers(out string report)
        {
            StringBuilder sb = new StringBuilder();
            AppendMissing(sb, "idle", idle);
            AppendMissing(sb, "lowAccel", lowAccel);
            AppendMissing(sb, "midAccel", midAccel);
            AppendMissing(sb, "highAccel", highAccel);
            AppendMissing(sb, "topRedline", topRedline);
            AppendMissing(sb, "lowDecel", lowDecel);
            AppendMissing(sb, "midDecel", midDecel);
            AppendMissing(sb, "highDecel", highDecel);
            if (shiftUp.clip == null) sb.Append(" shiftUp");
            if (shiftDown.clip == null) sb.Append(" shiftDown");
            if (throttleLift.clip == null) sb.Append(" throttleLift");
            AppendUnsafeLoopClip(sb, "idle", idle);
            AppendUnsafeLoopClip(sb, "lowAccel", lowAccel);
            AppendUnsafeLoopClip(sb, "midAccel", midAccel);
            AppendUnsafeLoopClip(sb, "highAccel", highAccel);
            AppendUnsafeLoopClip(sb, "topRedline", topRedline);
            AppendUnsafeLoopClip(sb, "lowDecel", lowDecel);
            AppendUnsafeLoopClip(sb, "midDecel", midDecel);
            AppendUnsafeLoopClip(sb, "highDecel", highDecel);
            AppendUnsafeTransientClip(sb, "shiftUp", shiftUp);
            AppendUnsafeTransientClip(sb, "shiftDown", shiftDown);
            AppendUnsafeTransientClip(sb, "throttleLift", throttleLift);

            int repeatedRequiredClips = CountRequiredEngineLoopsUsingSameClip();
            if (repeatedRequiredClips >= 6)
            {
                sb.Append(" unsafeEngineBedRepeatedClip(").Append(repeatedRequiredClips).Append("/8)");
            }

            report = sb.Length == 0
                ? $"Audio profile '{audioProfileId}' validated."
                : $"Audio profile '{audioProfileId}' missing:{sb}";
            return sb.Length == 0;
        }

        public bool HasMeaningfulOverrides()
        {
            if (!inheritsFamily || familyBaseProfile == null)
            {
                return true;
            }

            int differences = 0;
            differences += DifferentClip(idle, familyBaseProfile.idle) ? 1 : 0;
            differences += DifferentClip(lowAccel, familyBaseProfile.lowAccel) ? 1 : 0;
            differences += DifferentClip(highAccel, familyBaseProfile.highAccel) ? 1 : 0;
            differences += shiftUp.clip != familyBaseProfile.shiftUp.clip ? 1 : 0;
            differences += throttleLift.clip != familyBaseProfile.throttleLift.clip ? 1 : 0;
            return differences >= 3;
        }

        public FTAudioUpgradeModifier GetUpgradeModifier(FTAudioUpgradeStage stage)
        {
            for (int i = 0; i < upgradeModifiers.Count; i++)
            {
                FTAudioUpgradeModifier modifier = upgradeModifiers[i];
                if (modifier != null && modifier.stage == stage)
                {
                    return modifier;
                }
            }

            return upgradeModifiers.Count > 0 ? upgradeModifiers[0] : null;
        }

        private static bool DifferentClip(FTAudioLoopLayer a, FTAudioLoopLayer b)
        {
            return a != null && b != null && a.clip != b.clip;
        }

        private static void AppendMissing(StringBuilder sb, string label, FTAudioLoopLayer layer)
        {
            if (layer == null || layer.clip == null)
            {
                sb.Append(' ').Append(label);
            }
        }

        private static void AppendUnsafeLoopClip(StringBuilder sb, string label, FTAudioLoopLayer layer)
        {
            if (layer == null || layer.clip == null)
            {
                return;
            }

            if (layer.clip.length > 4f || layer.clip.name.StartsWith("GIN_"))
            {
                sb.Append(' ')
                    .Append("unsafeLoopClip(")
                    .Append(label)
                    .Append(':')
                    .Append(layer.clip.name)
                    .Append(')');
            }
        }

        private static void AppendUnsafeTransientClip(StringBuilder sb, string label, FTAudioTransientLayer layer)
        {
            if (layer == null || layer.clip == null)
            {
                return;
            }

            if (layer.clip.length > 2.5f || layer.clip.name.StartsWith("GIN_"))
            {
                sb.Append(' ')
                    .Append("unsafeTransientClip(")
                    .Append(label)
                    .Append(':')
                    .Append(layer.clip.name)
                    .Append(')');
            }
        }

        private int CountRequiredEngineLoopsUsingSameClip()
        {
            AudioClip[] clips =
            {
                idle?.clip,
                lowAccel?.clip,
                midAccel?.clip,
                highAccel?.clip,
                topRedline?.clip,
                lowDecel?.clip,
                midDecel?.clip,
                highDecel?.clip
            };

            int bestCount = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[i];
                if (clip == null)
                {
                    continue;
                }

                int count = 0;
                for (int j = 0; j < clips.Length; j++)
                {
                    if (clips[j] == clip)
                    {
                        count++;
                    }
                }

                bestCount = Mathf.Max(bestCount, count);
            }

            return bestCount;
        }
    }
}

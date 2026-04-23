using System.Collections.Generic;
using UnityEngine;

namespace FullThrottle.SacredCore.Audio
{
    public sealed class FTEngineLoopMixer : MonoBehaviour
    {
        private const float MaxEffectiveDecelOverlapExpansion = 0.045f;
        private const float MinEffectiveHighDecelEntryRPM01 = 0.88f;
        private const float MaxEffectiveDecelBandRpmBias = 0.88f;

        private sealed class RuntimeLoop
        {
            public string label;
            public FTAudioLoopLayer layer;
            public AudioSource source;
            public float targetVolume;
            public float targetPitch = 1f;
        }

        private sealed class RuntimeSweep
        {
            public string label;
            public FTAudioSweepLayer layer;
            public AudioSource source;
            public float targetVolume;
            public float targetPitch = 1f;
            public float position01;
        }

        [SerializeField] private int dominantLoopLimit = 3;
        [SerializeField] private float overlapExpansion = 0.36f;
        [SerializeField] private float topEntryRPM01 = 0.91f;
        [Header("Off-Throttle Placement")]
        [SerializeField, Range(0f, 1f)] private float offThrottleEnterThreshold = 0.13f;
        [SerializeField, Range(0f, 1f)] private float offThrottleExitThreshold = 0.24f;
        [SerializeField] private float offThrottleBlendResponse = 10f;
        [SerializeField] private float decelSpeedGateKph = 12f;
        [SerializeField, Range(0f, 1f)] private float decelMinRPM01 = 0.18f;
        [SerializeField, Range(0f, 0.45f)] private float decelOverlapExpansion = 0.14f;
        [SerializeField, Range(0f, 1f)] private float highDecelEntryRPM01 = 0.68f;
        [SerializeField, Range(0f, 0.35f)] private float accelBleedOnCoast = 0.045f;
        [SerializeField, Range(0f, 0.35f)] private float decelBleedOnThrottle = 0.035f;
        [SerializeField, Range(0.75f, 1f)] private float decelBandRpmBias = 0.93f;
        [SerializeField] private float decelSelectionLeadRPM = 260f;
        [Header("Debug")]
        [SerializeField] private bool logRuntimeWarnings;
        [SerializeField] private float warningCooldown = 1.25f;

        private readonly List<RuntimeLoop> loops = new();
        private readonly List<RuntimeSweep> sweeps = new();
        private FTVehicleAudioProfile profile;
        private FTEngineAudioFeed feed;
        private Transform sourceRoot;
        private string lastDominantSummary = "";
        private bool wasTopSuppressedByShift;
        private bool offThrottleLatched;
        private float offThrottleBlend;
        private float lastOffThrottleWarningTime = -999f;
        private float lastBedWarningTime = -999f;
        private float lastClampWarningTime = -999f;
        private float lastShiftWarningTime = -999f;

        public float TotalEngineVolume { get; private set; }
        public string ActiveLoopSummary { get; private set; } = "";

        public void Configure(FTVehicleAudioProfile audioProfile, FTEngineAudioFeed audioFeed, Transform root)
        {
            profile = audioProfile;
            feed = audioFeed;
            sourceRoot = root != null ? root : transform;
            RebuildSources();
        }

        private void Update()
        {
            if (profile == null || feed == null || loops.Count == 0)
            {
                return;
            }

            UpdateOffThrottleBlend();

            float throttleTone = Smooth01(Mathf.InverseLerp(0.08f, 0.82f, feed.Throttle));
            float decelRpmGate = Smooth01(Mathf.InverseLerp(decelMinRPM01, decelMinRPM01 + 0.12f, feed.NormalizedRPM));
            float decelSpeedGate = Smooth01(Mathf.InverseLerp(decelSpeedGateKph, decelSpeedGateKph + 12f, feed.SpeedKph));
            float decelGate = decelRpmGate * decelSpeedGate;
            float onLoad = Mathf.Lerp(Mathf.Lerp(0.42f, 1f, throttleTone), accelBleedOnCoast, offThrottleBlend * decelGate);
            float offLoad = Mathf.Lerp(decelBleedOnThrottle, 1f, offThrottleBlend) * decelGate;
            float shiftGain = feed.IsShifting ? 1f - profile.response.shiftDuck : 1f;
            float limiterGain = GetLimiterGain();
            TotalEngineVolume = 0f;
            float effectiveDecelOverlap = Mathf.Min(decelOverlapExpansion, MaxEffectiveDecelOverlapExpansion);
            float effectiveHighDecelEntry = Mathf.Max(highDecelEntryRPM01, MinEffectiveHighDecelEntryRPM01);
            float effectiveDecelBandRpmBias = Mathf.Min(decelBandRpmBias, MaxEffectiveDecelBandRpmBias);
            float rawGuardedDecelRPM = Mathf.Min(feed.AudioRPM, feed.RawRPM + Mathf.Max(0f, decelSelectionLeadRPM));
            float decelSelectionRPM = offThrottleLatched ? rawGuardedDecelRPM : Mathf.Lerp(feed.AudioRPM, rawGuardedDecelRPM, offThrottleBlend);
            float decelPlacementRPM01 = Mathf.Clamp01(Mathf.Min(feed.NormalizedRPM, feed.RawNormalizedRPM + 0.045f));

            for (int i = 0; i < loops.Count; i++)
            {
                RuntimeLoop loop = loops[i];
                bool decel = loop.label.Contains("Decel");
                bool top = loop.label.Contains("Top");
                float bandRPM = decel ? decelSelectionRPM * Mathf.Lerp(1f, effectiveDecelBandRpmBias, offThrottleBlend) : feed.AudioRPM;
                float band = GetBandWeight(loop.layer, bandRPM, decel ? effectiveDecelOverlap : overlapExpansion);

                if (loop.label == "LowDecel")
                {
                    band *= 1f - Smooth01(Mathf.InverseLerp(0.42f, 0.58f, decelPlacementRPM01));
                }
                else if (loop.label == "MidDecel")
                {
                    float midEnter = Smooth01(Mathf.InverseLerp(0.30f, 0.42f, decelPlacementRPM01));
                    float midExit = 1f - Smooth01(Mathf.InverseLerp(0.68f, 0.80f, decelPlacementRPM01));
                    band *= midEnter * midExit;
                }
                else if (loop.label == "HighDecel")
                {
                    band *= Smooth01(Mathf.InverseLerp(effectiveHighDecelEntry, effectiveHighDecelEntry + 0.045f, decelPlacementRPM01));
                }

                float load = decel ? offLoad : onLoad;

                if (loop.label == "Idle")
                {
                    load = Mathf.Lerp(1f, 0.42f, Mathf.Max(throttleTone, offThrottleBlend * decelGate));
                }

                if (top)
                {
                    bool allowed = feed.NormalizedRPM >= topEntryRPM01 && feed.Throttle > 0.72f && !feed.IsShifting;
                    if (!allowed)
                    {
                        if (logRuntimeWarnings && feed.IsShifting && !wasTopSuppressedByShift)
                        {
                            Debug.LogWarning("[SacredCore] Top loop suppressed by shift.");
                            wasTopSuppressedByShift = true;
                        }

                        load = 0f;
                    }
                    else
                    {
                        wasTopSuppressedByShift = false;
                    }
                }

                loop.targetVolume = band * load * loop.layer.volume * shiftGain * limiterGain;
                float pitchRPM = decel ? Mathf.Lerp(feed.AudioRPM, bandRPM, 0.7f) : feed.AudioRPM;
                loop.targetPitch = GetPitch(loop.layer, pitchRPM);
            }

            ApplyDominance();
            ProtectBed();
            UpdateSweeps(onLoad, offLoad, shiftGain);
            WarnForBadOffThrottlePlacement(offLoad);
            ApplySources();
            BuildDebugSummary();
        }

        private void UpdateOffThrottleBlend()
        {
            bool canUseDecel = feed.Grounded && feed.SpeedKph >= decelSpeedGateKph;
            if (!canUseDecel || feed.IsShifting)
            {
                offThrottleLatched = false;
            }
            else if (feed.Throttle <= offThrottleEnterThreshold)
            {
                offThrottleLatched = true;
            }
            else if (feed.Throttle >= offThrottleExitThreshold)
            {
                offThrottleLatched = false;
            }

            float target = offThrottleLatched ? 1f : 0f;
            offThrottleBlend = Exp(offThrottleBlend, target, offThrottleBlendResponse, Time.deltaTime);
        }

        private void OnValidate()
        {
            decelOverlapExpansion = Mathf.Min(decelOverlapExpansion, MaxEffectiveDecelOverlapExpansion);
            highDecelEntryRPM01 = Mathf.Max(highDecelEntryRPM01, MinEffectiveHighDecelEntryRPM01);
            decelBandRpmBias = Mathf.Min(decelBandRpmBias, MaxEffectiveDecelBandRpmBias);
            decelSelectionLeadRPM = Mathf.Max(0f, decelSelectionLeadRPM);
        }

        private void RebuildSources()
        {
            for (int i = sourceRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = sourceRoot.GetChild(i);
                if (child.name.StartsWith("FT_EngineLoop_") || child.name.StartsWith("FT_EngineSweep_"))
                {
                    Destroy(child.gameObject);
                }
            }

            loops.Clear();
            sweeps.Clear();
            if (profile == null)
            {
                return;
            }

            AddLoop("Idle", profile.idle);
            AddLoop("LowAccel", profile.lowAccel);
            AddLoop("MidAccel", profile.midAccel);
            AddLoop("HighAccel", profile.highAccel);
            AddLoop("Top", profile.topRedline);
            AddLoop("LowDecel", profile.lowDecel);
            AddLoop("MidDecel", profile.midDecel);
            AddLoop("HighDecel", profile.highDecel);
            AddSweep("Accel", profile.accelSweep);
            AddSweep("Decel", profile.decelSweep);
        }

        private void AddLoop(string label, FTAudioLoopLayer layer)
        {
            if (layer == null || layer.clip == null)
            {
                Debug.LogWarning($"[SacredCore] Engine loop '{label}' has no clip in profile {profile.audioProfileId}.");
                return;
            }

            GameObject go = new GameObject($"FT_EngineLoop_{label}");
            go.transform.SetParent(sourceRoot, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.clip = layer.clip;
            source.volume = 0f;
            source.pitch = 1f;
            source.spatialBlend = 0.65f;
            source.dopplerLevel = 0f;
            source.Play();
            loops.Add(new RuntimeLoop { label = label, layer = layer, source = source });
        }

        private void AddSweep(string label, FTAudioSweepLayer layer)
        {
            if (layer == null || !layer.enabled || layer.clip == null)
            {
                return;
            }

            GameObject go = new GameObject($"FT_EngineSweep_{label}");
            go.transform.SetParent(sourceRoot, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.clip = layer.clip;
            source.volume = 0f;
            source.pitch = 1f;
            source.spatialBlend = 0.65f;
            source.dopplerLevel = 0f;
            source.Play();
            sweeps.Add(new RuntimeSweep { label = label, layer = layer, source = source });
        }

        private void ApplyDominance()
        {
            int keep = Mathf.Clamp(dominantLoopLimit, 1, Mathf.Max(1, loops.Count));
            for (int rank = 0; rank < loops.Count; rank++)
            {
                loops[rank].source.mute = false;
            }

            for (int i = 0; i < loops.Count; i++)
            {
                int stronger = 0;
                for (int j = 0; j < loops.Count; j++)
                {
                    if (loops[j].targetVolume > loops[i].targetVolume)
                    {
                        stronger++;
                    }
                }

                if (stronger >= keep)
                {
                    loops[i].targetVolume = 0f;
                }
            }
        }

        private void ProtectBed()
        {
            float total = 0f;
            for (int i = 0; i < loops.Count; i++)
            {
                total += loops[i].targetVolume;
            }

            float floor = profile.response.minimumBedVolume;
            if (total > 0.001f && total < floor)
            {
                float gain = floor / total;
                for (int i = 0; i < loops.Count; i++)
                {
                    loops[i].targetVolume *= gain;
                }

                if (logRuntimeWarnings && Time.time - lastBedWarningTime >= warningCooldown)
                {
                    lastBedWarningTime = Time.time;
                    Debug.LogWarning($"[SacredCore] Engine bed too low during transition. Boosting bed {total:0.00}->{floor:0.00}.");
                }
            }
        }

        private void UpdateSweeps(float onLoad, float offLoad, float shiftGain)
        {
            for (int i = 0; i < sweeps.Count; i++)
            {
                RuntimeSweep sweep = sweeps[i];
                if (sweep.source == null || sweep.layer == null || sweep.layer.clip == null)
                {
                    continue;
                }

                bool decel = sweep.label == "Decel";
                float load = decel ? offLoad : onLoad * Smooth01(Mathf.InverseLerp(0.10f, 0.35f, feed.Throttle));
                sweep.targetVolume = sweep.layer.volume * load * shiftGain;
                sweep.targetPitch = Mathf.Lerp(sweep.layer.pitchCorrectionRange.x, sweep.layer.pitchCorrectionRange.y, feed.NormalizedRPM);

                float minWindow = Mathf.Min(sweep.layer.playbackWindow01.x, sweep.layer.playbackWindow01.y);
                float maxWindow = Mathf.Max(sweep.layer.playbackWindow01.x, sweep.layer.playbackWindow01.y);
                float targetPosition01 = Mathf.Lerp(minWindow, maxWindow, feed.NormalizedRPM);
                sweep.position01 = Exp(sweep.position01 <= 0f ? targetPosition01 : sweep.position01, targetPosition01, sweep.layer.seekResponse, Time.deltaTime);

                float targetTime = sweep.layer.clip.length * Mathf.Clamp01(sweep.position01);
                if (Mathf.Abs(sweep.source.time - targetTime) > sweep.layer.resyncThresholdSeconds)
                {
                    sweep.source.time = Mathf.Clamp(targetTime, 0f, Mathf.Max(0f, sweep.layer.clip.length - 0.02f));
                }
            }
        }

        private void WarnForBadOffThrottlePlacement(float offLoad)
        {
            if (!logRuntimeWarnings)
            {
                return;
            }

            if (offThrottleBlend < 0.72f || offLoad < 0.25f || Time.time - lastOffThrottleWarningTime < 0.75f)
            {
                return;
            }

            float strongestAccel = 0f;
            float strongestDecel = 0f;
            string accelLabel = "";
            string decelLabel = "";

            for (int i = 0; i < loops.Count; i++)
            {
                RuntimeLoop loop = loops[i];
                if (loop.label == "Idle" || loop.label == "Top")
                {
                    continue;
                }

                bool decel = loop.label.Contains("Decel");
                if (decel && loop.targetVolume > strongestDecel)
                {
                    strongestDecel = loop.targetVolume;
                    decelLabel = loop.label;
                }
                else if (!decel && loop.targetVolume > strongestAccel)
                {
                    strongestAccel = loop.targetVolume;
                    accelLabel = loop.label;
                }
            }

            if (strongestDecel < 0.04f)
            {
                lastOffThrottleWarningTime = Time.time;
                Debug.LogWarning($"[SacredCore] Off-throttle active but no decel loop is carrying the bed. rpm={feed.AudioRPM:0} speed={feed.SpeedKph:0} throttle={feed.Throttle:0.00}");
                return;
            }

            if (strongestAccel > strongestDecel * 1.35f)
            {
                lastOffThrottleWarningTime = Time.time;
                Debug.LogWarning($"[SacredCore] Off-throttle overlap sounds suspect: accel={accelLabel}:{strongestAccel:0.00}, decel={decelLabel}:{strongestDecel:0.00}, rpm={feed.AudioRPM:0}.");
            }
        }

        private void ApplySources()
        {
            float total = 0f;
            for (int i = 0; i < loops.Count; i++)
            {
                RuntimeLoop loop = loops[i];
                if (loop.source == null) continue;
                total += loop.targetVolume;
                loop.source.volume = Exp(loop.source.volume, Mathf.Clamp01(loop.targetVolume), profile.response.volumeResponse, Time.deltaTime);
                loop.source.pitch = Exp(loop.source.pitch, Mathf.Clamp(loop.targetPitch, 0.25f, 2.5f), profile.response.pitchResponse, Time.deltaTime);

                if (logRuntimeWarnings
                    && Time.time - lastClampWarningTime >= warningCooldown
                    && loop.source.volume > 0.05f
                    && Mathf.Abs(loop.targetPitch - profile.response.pitchClamp.y) < 0.025f)
                {
                    lastClampWarningTime = Time.time;
                    Debug.LogWarning($"[SacredCore] Clamp-pinned loop: {loop.label} clip={loop.layer.clip.name} pitch={loop.targetPitch:0.00}");
                }
            }

            for (int i = 0; i < sweeps.Count; i++)
            {
                RuntimeSweep sweep = sweeps[i];
                if (sweep.source == null || sweep.layer == null || sweep.layer.clip == null) continue;
                total += sweep.targetVolume;
                sweep.source.volume = Exp(sweep.source.volume, Mathf.Clamp01(sweep.targetVolume), profile.response.volumeResponse, Time.deltaTime);
                sweep.source.pitch = Exp(sweep.source.pitch, Mathf.Clamp(sweep.targetPitch, 0.8f, 1.2f), profile.response.pitchResponse, Time.deltaTime);
            }

            TotalEngineVolume = total;
        }

        private void BuildDebugSummary()
        {
            ActiveLoopSummary = "";
            for (int i = 0; i < loops.Count; i++)
            {
                RuntimeLoop loop = loops[i];
                if (loop.targetVolume < 0.03f) continue;
                if (ActiveLoopSummary.Length > 0) ActiveLoopSummary += " | ";
                ActiveLoopSummary += $"{loop.label}({loop.layer.clip.name} v={loop.targetVolume:0.00} p={loop.targetPitch:0.00})";
            }

            for (int i = 0; i < sweeps.Count; i++)
            {
                RuntimeSweep sweep = sweeps[i];
                if (sweep.targetVolume < 0.03f || sweep.layer == null || sweep.layer.clip == null) continue;
                if (ActiveLoopSummary.Length > 0) ActiveLoopSummary += " | ";
                ActiveLoopSummary += $"{sweep.label}Sweep({sweep.layer.clip.name} v={sweep.targetVolume:0.00} p={sweep.targetPitch:0.00})";
            }

            if (logRuntimeWarnings
                && feed.IsShifting
                && ActiveLoopSummary != lastDominantSummary
                && Time.time - lastShiftWarningTime >= warningCooldown)
            {
                lastShiftWarningTime = Time.time;
                Debug.LogWarning($"[SacredCore] Dominant loop changed during shift: {ActiveLoopSummary}");
            }

            lastDominantSummary = ActiveLoopSummary;
        }

        private float GetBandWeight(FTAudioLoopLayer layer, float rpm, float expansion)
        {
            float min = Mathf.Min(layer.minRPM, layer.maxRPM);
            float max = Mathf.Max(layer.minRPM, layer.maxRPM);
            float expand = (max - min) * expansion;
            min = Mathf.Max(1f, min - expand);
            max += expand;
            float reference = Mathf.Clamp(layer.referenceRPM, min + 1f, max - 1f);

            if (rpm <= min || rpm >= max)
            {
                return 0f;
            }

            return rpm <= reference
                ? Smooth01(Mathf.InverseLerp(min, reference, rpm))
                : 1f - Smooth01(Mathf.InverseLerp(reference, max, rpm));
        }

        private float GetPitch(FTAudioLoopLayer layer, float rpm)
        {
            float raw = layer.basePitch * rpm / Mathf.Max(1f, layer.referenceRPM);
            float min = Mathf.Min(profile.response.pitchClamp.x, profile.response.pitchClamp.y);
            float max = Mathf.Max(profile.response.pitchClamp.x, profile.response.pitchClamp.y);
            return Mathf.Clamp(raw, min, max);
        }

        private float GetLimiterGain()
        {
            if (feed.NormalizedRPM < profile.limiter.enterRPM01 || feed.Throttle < 0.2f)
            {
                return 1f;
            }

            float pulse = Mathf.Sin(Time.time * Mathf.PI * 2f * profile.limiter.pulseHz) * 0.5f + 0.5f;
            return 1f - pulse * profile.limiter.volumePulse;
        }

        private static float Exp(float current, float target, float response, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-Mathf.Max(0.01f, response) * dt));
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}

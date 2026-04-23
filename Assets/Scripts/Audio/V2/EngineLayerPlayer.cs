using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Audio;

namespace Underground.Audio.V2
{
    /// <summary>
    /// Manages engine loop crossfading: idle, on-throttle bands, off-throttle bands,
    /// and top/redline loop. Reads exclusively from <see cref="EngineAudioStateFeed"/>.
    ///
    /// This version is tuned for continuity first: controlled overlap, safer shift
    /// behavior, softer clamp exposure, and stronger diagnostics.
    /// </summary>
    public sealed class EngineLayerPlayer : MonoBehaviour
    {
        private enum LoopBank { Idle, OnThrottle, OffThrottle, Top }

        [Serializable]
        public sealed class EngineLoopDef
        {
            public string label = "Loop";
            public AudioClip clip;
            public float referenceRPM = 3000f;
            public float minRPM = 1800f;
            public float maxRPM = 4200f;
            [Range(0f, 1.5f)] public float baseVolume = 0.65f;
            [Range(0.25f, 2.5f)] public float basePitch = 1f;
            [Range(0f, 1f)] public float throttleAggression = 0.5f;
        }

        private sealed class Runtime
        {
            public EngineLoopDef Settings;
            public LoopBank Bank;
            public AudioSource Source;
            public float TargetVolume;
            public float TargetPitch = 1f;
            public float RawPitch;
            public float HighClampTime;
            public float LowClampTime;
            public float LastClampWarningTime = -999f;
        }

        [Header("Main Loops")]
        [SerializeField] private EngineLoopDef idleLoop;
        [SerializeField] private EngineLoopDef[] onThrottleLoops;
        [SerializeField] private EngineLoopDef topLoop;
        [SerializeField] private EngineLoopDef[] offThrottleLoops;

        [Header("Blend")]
        [SerializeField, Range(1, 4)] private int dominantLoopLimit = 3;
        [SerializeField, Range(0.1f, 1.2f)] private float busVolumeCeiling = 0.92f;
        [SerializeField, Range(0f, 1f)] private float minimumBedVolume = 0.34f;
        [SerializeField, Range(0f, 0.45f)] private float rpmBandOverlap = 0.38f;
        [SerializeField, Range(0f, 1f)] private float offThrottleFloor = 0.34f;
        [SerializeField, Range(0f, 1f)] private float idleThrottleSuppression = 0.36f;
        [SerializeField, Range(0f, 1f)] private float throttleFullTone = 0.78f;
        [SerializeField, Range(0f, 1f)] private float topLoopThrottleThreshold = 0.72f;
        [SerializeField, Range(0f, 1f)] private float highRpmAggressionBoost = 0.16f;
        [SerializeField] private Vector2 pitchClamp = new Vector2(0.74f, 1.24f);
        [SerializeField, Range(0.02f, 0.4f)] private float pitchClampFadeRange = 0.18f;
        [SerializeField, Range(80f, 250f)] private float shiftSelectionRpmMargin = 130f;
        [SerializeField, Range(0f, 0.2f)] private float topLoopStableGearDelay = 0.14f;
        [SerializeField, Range(0f, 1f)] private float shiftingHighOnReduction = 0.10f;
        [SerializeField, Range(0f, 1f)] private float bedRecoveryBoostLimit = 0.22f;

        [Header("Smoothing")]
        [SerializeField] private float volumeResponse = 13f;
        [SerializeField] private float pitchResponse = 6.5f;

        [Header("Spatial")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.78f;
        [SerializeField] private float minDistance = 3f;
        [SerializeField] private float maxDistance = 55f;
        [SerializeField] private AudioMixerGroup outputGroup;

        [Header("Shift")]
        [SerializeField, Range(0f, 1f)] private float shiftVolumeDuck = 0.12f;
        [SerializeField] private float shiftDuckDuration = 0.08f;
        [SerializeField, Range(0f, 0.1f)] private float shiftPitchDip = 0.008f;
        [SerializeField] private float loopSelectionFreezeDuration = 0.035f;

        [Header("Limiter")]
        [SerializeField] private bool enableSoftLimiter = true;
        [SerializeField, Range(0f, 1f)] private float limiterEnterRpm01 = 0.955f;
        [SerializeField, Range(0f, 0.25f)] private float limiterVolumePulse = 0.08f;
        [SerializeField, Range(4f, 30f)] private float limiterPulseHz = 13f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private float debugLogInterval = 0.2f;
        [SerializeField] private float clampWarningDelay = 0.18f;
        [SerializeField] private float dissonanceWarningCooldown = 0.3f;
        [SerializeField] private float bedTooLowWarningCooldown = 0.35f;

        private readonly List<Runtime> runtimes = new List<Runtime>(10);
        private Transform sourceRoot;
        private float shiftDuckTimer;
        private float loopSelectionFreezeTimer;
        private float frozenSelectionRpm;
        private bool hasOffBank;
        private bool initialized;
        private float debugTimer;
        private float lastDissonanceWarningTime = -999f;
        private float lastBedWarningTime = -999f;
        private float lastTopSuppressionTime = -999f;
        private string lastDominantLoopLabel = string.Empty;
        private Runtime lowOnRuntime;
        private Runtime midOnRuntime;
        private Runtime highOnRuntime;
        private Runtime topRuntime;

        public void Initialize(Transform root)
        {
            sourceRoot = root;
            RebuildSources();
            initialized = true;
        }

        public void ApplyFromBank(NFSU2CarAudioBank.TierAudioPackage tier, bool isExterior)
        {
            if (tier == null) return;

            if (idleLoop == null) idleLoop = new EngineLoopDef();
            idleLoop.label = "Idle";
            idleLoop.clip = isExterior ? tier.idle.exteriorClip : tier.idle.interiorClip;
            idleLoop.baseVolume = Mathf.Clamp(isExterior ? tier.idle.exteriorVolume : tier.idle.interiorVolume, 0.62f, 0.82f);
            idleLoop.referenceRPM = 900f;
            idleLoop.minRPM = 650f;
            idleLoop.maxRPM = 1500f;

            onThrottleLoops = new[]
            {
                MakeLoopDef("Low On", tier.accelLow, isExterior, 1900f, 1050f, 2950f, 0.74f),
                MakeLoopDef("Mid On", tier.accelMid, isExterior, 3650f, 2750f, 5450f, 0.77f),
                MakeLoopDef("High On", tier.accelHigh, isExterior, 5700f, 4700f, 7100f, 0.78f)
            };

            if (topLoop == null) topLoop = new EngineLoopDef();
            topLoop.label = "Top On";
            topLoop.clip = isExterior ? tier.limiter.exteriorClip : tier.limiter.interiorClip;
            topLoop.baseVolume = Mathf.Clamp(isExterior ? tier.limiter.exteriorVolume : tier.limiter.interiorVolume, 0.72f, 0.76f);
            topLoop.referenceRPM = 7400f;
            topLoop.minRPM = 7000f;
            topLoop.maxRPM = 8150f;

            offThrottleLoops = new[]
            {
                MakeLoopDef("Low Off", tier.decelLow, isExterior, 1900f, 1150f, 3000f, 0.52f),
                MakeLoopDef("Mid Off", tier.decelMid, isExterior, 3650f, 2850f, 5450f, 0.50f),
                MakeLoopDef("High Off", tier.decelHigh, isExterior, 5700f, 4850f, 7100f, 0.46f)
            };

            if (tier.shift != null)
            {
                shiftVolumeDuck = Mathf.Clamp01(tier.shift.duckAmount);
                shiftDuckDuration = Mathf.Max(0.02f, tier.shift.duckDuration);
                shiftPitchDip = Mathf.Clamp(tier.shift.pitchDrop, 0f, 0.04f);
            }

            if (initialized) RebuildSources();
        }

        public void ApplyTuning(NFSU2CarAudioBank.RuntimeTuning tuning)
        {
            if (tuning == null)
            {
                return;
            }

            masterVolume = Mathf.Clamp(tuning.masterVolume, 0f, 2f);
            spatialBlend = Mathf.Clamp01(tuning.spatialBlend);
            minDistance = Mathf.Max(0f, tuning.minDistance);
            maxDistance = Mathf.Max(minDistance + 0.01f, tuning.maxDistance);
            topLoopThrottleThreshold = Mathf.Clamp(tuning.throttleOnThreshold + 0.56f, 0.68f, 0.82f);
            limiterEnterRpm01 = Mathf.Clamp01(tuning.limiterEnter01);
            rpmBandOverlap = Mathf.Clamp(tuning.engineBandWidth * 2.1f, 0.32f, 0.42f);
            volumeResponse = Mathf.Clamp(tuning.layerFadeSpeed * 2.25f, 10f, 16f);
            pitchResponse = Mathf.Clamp(tuning.sweepSeekResponse * 1.2f, 5.5f, 8.5f);
        }

        public void UpdateLayers(EngineAudioStateFeed feed, float dt)
        {
            if (!initialized || feed == null) return;

            shiftDuckTimer = Mathf.Max(0f, shiftDuckTimer - dt);
            loopSelectionFreezeTimer = Mathf.Max(0f, loopSelectionFreezeTimer - dt);

            if (feed.ShiftJustStarted)
            {
                shiftDuckTimer = shiftDuckDuration;
                loopSelectionFreezeTimer = loopSelectionFreezeDuration;
                frozenSelectionRpm = feed.SmoothedRPM;
                if (enableDebugLogs) Debug.Log($"[AudioV2][Shift] Start gear={feed.PreviousGear}->{feed.Gear} rpm={feed.SmoothedRPM:F0}");
            }

            if (feed.ShiftJustEnded && enableDebugLogs)
            {
                Debug.Log($"[AudioV2][Shift] End gear={feed.Gear} rpm={feed.SmoothedRPM:F0}");
            }

            float smoothedRpm = feed.SmoothedRPM;
            float selectionRpm = feed.IsShifting
                ? Mathf.Min(Mathf.Max(frozenSelectionRpm, smoothedRpm), smoothedRpm + shiftSelectionRpmMargin)
                : smoothedRpm;

            float rpm01 = feed.NormalizedRPM;
            float throttle = feed.SmoothedThrottle;
            float onBlend = feed.OnThrottleBlend;
            float throttleTone = Mathf.InverseLerp(0.1f, Mathf.Max(0.11f, throttleFullTone), throttle);
            float onLoad = hasOffBank ? Mathf.Lerp(0.10f, 1f, onBlend) : Mathf.Lerp(offThrottleFloor, 1f, throttleTone);
            float offLoad = hasOffBank ? Mathf.Lerp(1f, 0.10f, onBlend) : 0f;
            float highRpmBoost = 1f + highRpmAggressionBoost * Mathf.InverseLerp(0.58f, 0.96f, rpm01) * throttleTone;
            float shiftDuckT = shiftDuckDuration > 0f ? shiftDuckTimer / shiftDuckDuration : 0f;
            float shiftGain = 1f - shiftVolumeDuck * shiftDuckT;
            float limiterGain = GetLimiterGain(rpm01);
            bool topAllowed = !feed.IsShifting
                && loopSelectionFreezeTimer <= 0f
                && feed.TimeSinceLastGearChange >= topLoopStableGearDelay
                && throttle >= topLoopThrottleThreshold
                && rpm01 >= 0.92f;

            for (int i = 0; i < runtimes.Count; i++)
            {
                Runtime r = runtimes[i];
                EngineLoopDef s = r.Settings;
                float bandWeight = GetBandWeight(s, selectionRpm, r.Bank == LoopBank.Top);
                float bankLoad = ResolveBankLoad(r.Bank, onLoad, offLoad, throttleTone);
                float idleGain = r.Bank == LoopBank.Idle
                    ? Mathf.Lerp(1f, 1f - idleThrottleSuppression, throttleTone)
                    : 1f;

                float pitchRpm = feed.IsShifting ? selectionRpm : smoothedRpm;
                float rawPitch = s.referenceRPM > 0f ? pitchRpm / s.referenceRPM * s.basePitch : s.basePitch;
                float shiftPitch = feed.IsShifting ? -shiftPitchDip * Mathf.Clamp01(shiftDuckT + 0.15f) : 0f;
                r.RawPitch = rawPitch + shiftPitch;
                r.TargetPitch = Mathf.Clamp(r.RawPitch, pitchClamp.x, pitchClamp.y);

                float clampGain = GetPitchClampSoftGain(r);
                float baseVolume = bandWeight * clampGain * bankLoad * s.baseVolume * idleGain * highRpmBoost * shiftGain * limiterGain * masterVolume;

                if (r.Bank == LoopBank.Top && !topAllowed)
                {
                    baseVolume = 0f;
                    if (enableDebugLogs && Time.time - lastTopSuppressionTime > 0.2f && topRuntime != null && topRuntime.Source != null)
                    {
                        string reason = feed.IsShifting ? "shifting" : (feed.TimeSinceLastGearChange < topLoopStableGearDelay ? "unstable gear" : "below throttle/rpm gate");
                        Debug.Log($"[AudioV2][Top] Suppressed because {reason}.");
                        lastTopSuppressionTime = Time.time;
                    }
                }

                if (feed.IsShifting && r == highOnRuntime)
                {
                    baseVolume *= 1f - shiftingHighOnReduction;
                }

                r.TargetVolume = baseVolume;
                UpdateClampExposure(r, dt);
            }

            ApplyNeighborProtection(lowOnRuntime, midOnRuntime, 1.15f, 0.84f, 0.24f, 0.10f, "02->03");
            ApplyNeighborProtection(midOnRuntime, highOnRuntime, 1.13f, 0.88f, 0.20f, 0.08f, "03->04");

            ApplyDominancePass(feed, throttle, selectionRpm);
            MaintainEngineBed(feed, throttle, selectionRpm);
            ApplySources(dt);
            LogDiagnostics(feed, selectionRpm, shiftDuckT);
        }

        private void RebuildSources()
        {
            runtimes.Clear();
            lowOnRuntime = null;
            midOnRuntime = null;
            highOnRuntime = null;
            topRuntime = null;

            if (sourceRoot != null)
            {
                for (int i = sourceRoot.childCount - 1; i >= 0; i--)
                {
                    Transform child = sourceRoot.GetChild(i);
                    if (child.name.StartsWith("EL_"))
                    {
                        Destroy(child.gameObject);
                    }
                }
            }

            hasOffBank = offThrottleLoops != null && offThrottleLoops.Length > 0;
            AddRuntime(idleLoop, LoopBank.Idle, "EL_Idle");
            AddRuntimes(onThrottleLoops, LoopBank.OnThrottle, "EL_On");
            AddRuntime(topLoop, LoopBank.Top, "EL_Top");
            AddRuntimes(offThrottleLoops, LoopBank.OffThrottle, "EL_Off");
        }

        private void AddRuntimes(EngineLoopDef[] loops, LoopBank bank, string prefix)
        {
            if (loops == null) return;
            for (int i = 0; i < loops.Length; i++)
            {
                AddRuntime(loops[i], bank, $"{prefix}_{i:00}");
            }
        }

        private void AddRuntime(EngineLoopDef loop, LoopBank bank, string name)
        {
            if (loop == null || sourceRoot == null) return;

            GameObject go = new GameObject(name);
            go.transform.SetParent(sourceRoot, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.volume = 0f;
            src.pitch = 1f;
            src.priority = 96;
            src.dopplerLevel = 0f;
            src.spatialBlend = spatialBlend;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.outputAudioMixerGroup = outputGroup;
            if (loop.clip != null)
            {
                src.clip = loop.clip;
                src.Play();
            }

            Runtime runtime = new Runtime { Settings = loop, Bank = bank, Source = src };
            runtimes.Add(runtime);

            if (loop.label == "Low On") lowOnRuntime = runtime;
            else if (loop.label == "Mid On") midOnRuntime = runtime;
            else if (loop.label == "High On") highOnRuntime = runtime;
            else if (bank == LoopBank.Top) topRuntime = runtime;
        }

        private float GetBandWeight(EngineLoopDef s, float rpm, bool isTopLoop)
        {
            float range = Mathf.Max(1f, s.maxRPM - s.minRPM);
            float overlap = range * rpmBandOverlap;
            float effectiveMin = s.minRPM - overlap;
            float effectiveMax = s.maxRPM + overlap;
            if (rpm <= effectiveMin || rpm >= effectiveMax)
            {
                return 0f;
            }

            if (isTopLoop)
            {
                float up = Mathf.InverseLerp(effectiveMin, s.minRPM, rpm);
                float down = 1f - Mathf.InverseLerp(s.maxRPM, effectiveMax, rpm);
                return Mathf.Clamp01(Mathf.Min(up, down));
            }

            float center = s.referenceRPM > 0f ? s.referenceRPM : (s.minRPM + s.maxRPM) * 0.5f;
            float half = Mathf.Max(1f, (effectiveMax - effectiveMin) * 0.5f);
            float distance = Mathf.Abs(rpm - center);
            float t = 1f - distance / half;
            return Mathf.Clamp01(t * t * (3f - 2f * t));
        }

        private float GetPitchClampSoftGain(Runtime r)
        {
            float distLow = r.RawPitch - pitchClamp.x;
            float distHigh = pitchClamp.y - r.RawPitch;
            float dist = Mathf.Min(distLow, distHigh);
            if (dist >= pitchClampFadeRange)
            {
                return 1f;
            }

            float soft = Mathf.Clamp01(dist / Mathf.Max(0.001f, pitchClampFadeRange));
            return Mathf.Lerp(0.70f, 1f, soft);
        }

        private float ResolveBankLoad(LoopBank bank, float onLoad, float offLoad, float throttleTone)
        {
            switch (bank)
            {
                case LoopBank.Idle: return 1f;
                case LoopBank.OnThrottle: return onLoad;
                case LoopBank.OffThrottle: return offLoad;
                case LoopBank.Top: return throttleTone >= topLoopThrottleThreshold ? onLoad : onLoad * 0.15f;
                default: return 1f;
            }
        }

        private float GetLimiterGain(float rpm01)
        {
            if (!enableSoftLimiter || rpm01 < limiterEnterRpm01) return 1f;
            float pulse = Mathf.Sin(Time.time * limiterPulseHz * Mathf.PI * 2f) * 0.5f + 0.5f;
            return 1f - pulse * limiterVolumePulse;
        }

        private void UpdateClampExposure(Runtime r, float dt)
        {
            bool highPinned = r.RawPitch >= 1.24f;
            bool lowPinned = r.RawPitch <= 0.74f;
            r.HighClampTime = highPinned ? r.HighClampTime + dt : 0f;
            r.LowClampTime = lowPinned ? r.LowClampTime + dt : 0f;

            if (r.HighClampTime > clampWarningDelay)
            {
                r.TargetVolume *= 0.82f;
                if (enableDebugLogs && Time.time - r.LastClampWarningTime > 0.5f)
                {
                    Debug.LogWarning($"[AudioV2][Clamp] {r.Settings.label} exposed high clamp pitch={r.TargetPitch:F2} clip={r.Source?.clip?.name ?? "None"}");
                    r.LastClampWarningTime = Time.time;
                }
            }

            if (r.LowClampTime > clampWarningDelay)
            {
                r.TargetVolume *= 0.88f;
                if (enableDebugLogs && Time.time - r.LastClampWarningTime > 0.5f)
                {
                    Debug.LogWarning($"[AudioV2][Clamp] {r.Settings.label} exposed low clamp pitch={r.TargetPitch:F2} clip={r.Source?.clip?.name ?? "None"}");
                    r.LastClampWarningTime = Time.time;
                }
            }
        }

        private void ApplyNeighborProtection(Runtime outgoing, Runtime incoming, float outgoingWarnPitch, float incomingWarnPitch,
            float outgoingReduction, float incomingReduction, string label)
        {
            if (outgoing == null || incoming == null)
            {
                return;
            }

            bool dissonant = outgoing.TargetVolume > 0.01f
                && incoming.TargetVolume > 0.01f
                && outgoing.TargetPitch > outgoingWarnPitch
                && incoming.TargetPitch < incomingWarnPitch;

            if (!dissonant)
            {
                return;
            }

            float outgoingT = Mathf.InverseLerp(outgoingWarnPitch, pitchClamp.y, outgoing.TargetPitch);
            float incomingT = 1f - Mathf.InverseLerp(pitchClamp.x, incomingWarnPitch, incoming.TargetPitch);
            float protect = Mathf.Clamp01(Mathf.Min(outgoingT, incomingT));

            outgoing.TargetVolume *= 1f - outgoingReduction * protect;
            incoming.TargetVolume *= 1f - incomingReduction * protect;

            if (enableDebugLogs && Time.time - lastDissonanceWarningTime > dissonanceWarningCooldown)
            {
                Debug.LogWarning($"[AudioV2][Dissonance] {label} outgoing={outgoing.Settings.label}@{outgoing.TargetPitch:F2} incoming={incoming.Settings.label}@{incoming.TargetPitch:F2}");
                lastDissonanceWarningTime = Time.time;
            }
        }

        private void ApplyDominancePass(EngineAudioStateFeed feed, float throttle, float selectionRpm)
        {
            if (runtimes.Count == 0)
            {
                return;
            }

            List<float> volumes = new List<float>(runtimes.Count);
            for (int i = 0; i < runtimes.Count; i++)
            {
                volumes.Add(runtimes[i].TargetVolume);
            }
            volumes.Sort((a, b) => b.CompareTo(a));
            float nth = volumes[Mathf.Clamp(dominantLoopLimit - 1, 0, volumes.Count - 1)];

            for (int i = 0; i < runtimes.Count; i++)
            {
                Runtime r = runtimes[i];
                if (r.Bank == LoopBank.Idle)
                {
                    continue;
                }

                if (r.TargetVolume < nth * 0.45f)
                {
                    r.TargetVolume *= 0.58f;
                }
            }

            float total = 0f;
            for (int i = 0; i < runtimes.Count; i++)
            {
                total += runtimes[i].TargetVolume;
            }
            if (total > busVolumeCeiling)
            {
                float scale = busVolumeCeiling / total;
                for (int i = 0; i < runtimes.Count; i++)
                {
                    runtimes[i].TargetVolume *= scale;
                }
            }

            if (feed.IsShifting)
            {
                Runtime dominant = GetDominantRuntime();
                string label = dominant?.Settings?.label ?? "None";
                if (!string.Equals(label, lastDominantLoopLabel, StringComparison.Ordinal))
                {
                    lastDominantLoopLabel = label;
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[AudioV2][Shift] Dominant loop now {label} at selectionRPM={selectionRpm:F0}");
                    }
                }
            }
        }

        private void MaintainEngineBed(EngineAudioStateFeed feed, float throttle, float selectionRpm)
        {
            float bed = GetMainBedVolume();
            bool shouldProtect = throttle > 0.18f || feed.IsShifting;
            float bedFloor = minimumBedVolume * masterVolume;
            if (!shouldProtect || bed >= bedFloor)
            {
                return;
            }

            float deficit = bedFloor - bed;
            float boostRatio = Mathf.Clamp(deficit / Mathf.Max(0.01f, bedFloor), 0f, bedRecoveryBoostLimit);
            for (int i = 0; i < runtimes.Count; i++)
            {
                Runtime r = runtimes[i];
                if (r.Bank == LoopBank.Idle || r.Bank == LoopBank.OnThrottle)
                {
                    r.TargetVolume *= 1f + boostRatio;
                }
            }

            if (enableDebugLogs && Time.time - lastBedWarningTime > bedTooLowWarningCooldown)
            {
                Debug.LogWarning($"[AudioV2][BedLow] gear={feed.Gear} bed={bed:F2} throttle={throttle:F2} shift={feed.IsShifting} selectionRPM={selectionRpm:F0}");
                lastBedWarningTime = Time.time;
            }
        }

        private void ApplySources(float dt)
        {
            for (int i = 0; i < runtimes.Count; i++)
            {
                Runtime r = runtimes[i];
                if (r.Source == null)
                {
                    continue;
                }

                r.Source.volume = ExpSmooth(r.Source.volume, r.TargetVolume, volumeResponse, dt);
                r.Source.pitch = ExpSmooth(r.Source.pitch, r.TargetPitch, pitchResponse, dt);
                if (!r.Source.isPlaying && r.Source.clip != null)
                {
                    r.Source.Play();
                }
            }
        }

        private float GetMainBedVolume()
        {
            float total = 0f;
            for (int i = 0; i < runtimes.Count; i++)
            {
                Runtime r = runtimes[i];
                if (r.Bank == LoopBank.Idle || r.Bank == LoopBank.OnThrottle)
                {
                    total += r.TargetVolume;
                }
            }
            return total;
        }

        private Runtime GetDominantRuntime()
        {
            Runtime dominant = null;
            float best = -1f;
            for (int i = 0; i < runtimes.Count; i++)
            {
                if (runtimes[i].TargetVolume > best)
                {
                    best = runtimes[i].TargetVolume;
                    dominant = runtimes[i];
                }
            }
            return dominant;
        }

        private void LogDiagnostics(EngineAudioStateFeed feed, float selectionRpm, float shiftDuckT)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            debugTimer -= Time.deltaTime;
            if (debugTimer > 0f)
            {
                return;
            }
            debugTimer = Mathf.Max(0.05f, debugLogInterval);

            StringBuilder active = new StringBuilder();
            for (int i = 0; i < runtimes.Count; i++)
            {
                Runtime r = runtimes[i];
                if (r.Source == null || r.Source.volume < 0.025f)
                {
                    continue;
                }

                if (active.Length > 0)
                {
                    active.Append(" | ");
                }

                active.Append(r.Settings.label);
                active.Append(" clip=");
                active.Append(r.Source.clip != null ? r.Source.clip.name : "None");
                active.Append(" v=");
                active.Append(r.Source.volume.ToString("F2"));
                active.Append(" p=");
                active.Append(r.Source.pitch.ToString("F2"));
            }

            Debug.Log(
                $"[AudioV2][State] gear={feed.Gear} rawRPM={feed.RawEngineRPM:F0} smoothedRPM={feed.SmoothedRPM:F0} selectionRPM={selectionRpm:F0} wheelRPM={feed.RawWheelDrivenRPM:F0} throttle={feed.SmoothedThrottle:F2} shift={feed.IsShifting} duck={shiftDuckT * shiftVolumeDuck:F2} bed={GetMainBedVolume():F2} active={active}");
        }

        private static EngineLoopDef MakeLoopDef(string label, NFSU2CarAudioBank.AudioLoopLayer layer, bool exterior,
            float refRpm, float minRpm, float maxRpm, float defaultVolume)
        {
            return new EngineLoopDef
            {
                label = label,
                clip = exterior ? layer?.exteriorClip : layer?.interiorClip,
                baseVolume = Mathf.Clamp(exterior ? (layer?.exteriorVolume ?? defaultVolume) : (layer?.interiorVolume ?? defaultVolume), defaultVolume - 0.04f, defaultVolume + 0.04f),
                referenceRPM = refRpm,
                minRPM = minRpm,
                maxRPM = maxRpm,
                basePitch = 1f,
                throttleAggression = 0.5f
            };
        }

        private static float ExpSmooth(float a, float b, float r, float dt)
        {
            return Mathf.Lerp(a, b, 1f - Mathf.Exp(-r * dt));
        }
    }
}

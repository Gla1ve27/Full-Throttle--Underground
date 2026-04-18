using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Underground.UI;
using Underground.Vehicle;

namespace Underground.Audio
{
    public enum CarAudioCameraMode
    {
        Exterior,
        Interior
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Full Throttle/Audio/Car Engine Audio")]
    public class CarEngineAudio : MonoBehaviour
    {
        [Serializable]
        public sealed class EngineLoop
        {
            public string label = "Loop";
            public AudioClip clip;
            public float referenceRPM = 3000f;
            public float minRPM = 1800f;
            public float maxRPM = 4200f;
            [Range(0f, 1.5f)] public float baseVolume = 0.65f;
            [Range(0.25f, 2.5f)] public float basePitch = 1f;
            [Range(0f, 1f)] public float throttleAggression = 0.5f;

            public EngineLoop()
            {
            }

            public EngineLoop(string label, float referenceRPM, float minRPM, float maxRPM, float baseVolume)
            {
                this.label = label;
                this.referenceRPM = referenceRPM;
                this.minRPM = minRPM;
                this.maxRPM = maxRPM;
                this.baseVolume = baseVolume;
                basePitch = 1f;
                throttleAggression = 0.5f;
            }
        }

        [Serializable]
        public sealed class AuxLoop
        {
            public string label = "Aux";
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.25f;
            [Range(0.25f, 2.5f)] public float minPitch = 0.85f;
            [Range(0.25f, 2.5f)] public float maxPitch = 1.2f;
            public float fadeResponse = 10f;
        }

        [Serializable]
        public sealed class OneShotLayer
        {
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.65f;
            public Vector2 pitchRange = new Vector2(0.96f, 1.04f);
            public float minInterval = 0.08f;
        }

        private enum LoopBank
        {
            Idle,
            OnThrottle,
            OffThrottle,
            Top
        }

        private sealed class EngineLoopRuntime
        {
            public EngineLoop Settings;
            public LoopBank Bank;
            public AudioSource Source;
            public float TargetVolume;
            public float TargetPitch = 1f;
            public float NormalizedWeight;
        }

        private sealed class AuxLoopRuntime
        {
            public AuxLoop Settings;
            public AudioSource Source;
            public float TargetVolume;
            public float TargetPitch = 1f;
        }

        [Header("References")]
        [SerializeField] protected GearboxSystem gearbox;
        [SerializeField] protected VehicleDynamicsController vehicle;
        [SerializeField] protected InputReader input;
        [SerializeField] protected Transform sourceRoot;

        [Header("Manual/AI Telemetry")]
        [SerializeField] protected bool useExternalTelemetry;
        [SerializeField] protected float externalCurrentRPM = 900f;
        [SerializeField, Range(0f, 1f)] protected float externalNormalizedRPM;
        [SerializeField, Range(0f, 1f)] protected float externalThrottleInput;
        [SerializeField] protected int externalGear = 1;
        [SerializeField] protected float externalSpeedKph;
        [SerializeField] protected bool externalGrounded = true;
        [SerializeField, Range(0f, 1f)] protected float externalSlipAmount;

        [Header("RPM Range")]
        [SerializeField] protected float fallbackIdleRPM = 900f;
        [SerializeField] protected float fallbackMaxRPM = 7800f;
        [SerializeField] protected float fallbackRedlineRPM = 7300f;

        [Header("Main Engine Loops")]
        [SerializeField] protected EngineLoop idleLoop = new EngineLoop("Idle", 900f, 650f, 1500f, 0.72f);
        [SerializeField] protected EngineLoop[] onThrottleLoops =
        {
            new EngineLoop("Low On", 1800f, 1050f, 3000f, 0.74f),
            new EngineLoop("Mid On", 4400f, 2500f, 5200f, 0.78f),
            new EngineLoop("High On", 5900f, 4700f, 7200f, 0.80f)
        };
        [SerializeField] protected EngineLoop topLoop = new EngineLoop("Top/Redline On", 7300f, 6800f, 8200f, 0.74f);

        [Header("Optional Off-Throttle Loops")]
        [SerializeField] protected EngineLoop[] offThrottleLoops =
        {
            new EngineLoop("Low Off", 1800f, 1100f, 3000f, 0.48f),
            new EngineLoop("Mid Off", 4400f, 2500f, 5200f, 0.55f),
            new EngineLoop("High Off", 5900f, 4700f, 7200f, 0.58f)
        };

        [Header("Blend Behavior")]
        [SerializeField, Range(1, 4)] protected int dominantEngineLoopLimit = 3;
        [SerializeField, Range(0.1f, 1.2f)] protected float engineBusVolumeCeiling = 0.9f;
        [SerializeField, Range(0f, 1f)] protected float minimumEngineBedVolume = 0.32f;
        [SerializeField, Range(0f, 0.45f)] protected float rpmBandOverlapExpansion = 0.32f;
        [SerializeField, Range(0f, 1f)] protected float offThrottleEngineFloor = 0.34f;
        [SerializeField, Range(0f, 1f)] protected float idleThrottleSuppression = 0.36f;
        [SerializeField, Range(0f, 1f)] protected float throttleOnThreshold = 0.18f;
        [SerializeField, Range(0f, 1f)] protected float throttleOffThreshold = 0.1f;
        [SerializeField] protected float onOffBankBlendResponse = 11f;
        [SerializeField, Range(0f, 1f)] protected float throttleFullTone = 0.78f;
        [SerializeField, Range(0f, 1f)] protected float topLoopThrottleThreshold = 0.7f;
        [SerializeField, Range(0f, 1f)] protected float highRpmAggressionBoost = 0.18f;
        [SerializeField] protected Vector2 loopPitchClamp = new Vector2(0.72f, 1.28f);
        [SerializeField, Range(0.02f, 0.4f)] protected float pitchClampFadeRange = 0.16f;

        [Header("Continuity")]
        [SerializeField] protected bool keepEngineLoopsRunning = true;
        [SerializeField] protected bool repairLoopSeamsAtRuntime = true;
        [SerializeField, Range(2f, 16f)] protected float loopBoundaryCrossfadeMs = 8f;
        [SerializeField] protected float loopSelectionFreezeDuration = 0.05f;
        [SerializeField] protected float audioRpmDeadband = 18f;

        [Header("Smoothing")]
        [SerializeField] protected float rpmRiseResponse = 12f;
        [SerializeField] protected float rpmFallResponse = 10f;
        [SerializeField] protected float throttleResponse = 18f;
        [SerializeField] protected float speedResponse = 10f;
        [SerializeField] protected float volumeResponse = 16f;
        [SerializeField] protected float pitchResponse = 9f;

        [Header("Shift Effect")]
        [SerializeField, Range(0f, 1f)] protected float shiftVolumeDuck = 0.16f;
        [SerializeField] protected float shiftDuckDuration = 0.11f;
        [SerializeField] protected float shiftPitchDip = 0.012f;
        [SerializeField] protected OneShotLayer shiftUp = new OneShotLayer();
        [SerializeField] protected OneShotLayer shiftDown = new OneShotLayer();
        [SerializeField] protected OneShotLayer throttleBlip = new OneShotLayer();

        [Header("Limiter")]
        [SerializeField] protected bool enableSoftLimiter = true;
        [SerializeField, Range(0f, 1f)] protected float limiterEnter01 = 0.955f;
        [SerializeField, Range(0f, 0.25f)] protected float limiterVolumePulse = 0.08f;
        [SerializeField, Range(4f, 30f)] protected float limiterPulseHz = 13f;

        [Header("Optional Extra Layers")]
        [SerializeField] protected AuxLoop turboSpool = new AuxLoop { label = "Turbo Spool", volume = 0.2f, minPitch = 0.85f, maxPitch = 1.35f };
        [SerializeField] protected OneShotLayer turboBlowOff = new OneShotLayer();
        [SerializeField] protected AuxLoop intake = new AuxLoop { label = "Intake", volume = 0.18f, minPitch = 0.9f, maxPitch = 1.3f };
        [SerializeField] protected AuxLoop gearWhine = new AuxLoop { label = "Gear Whine", volume = 0.12f, minPitch = 0.8f, maxPitch = 1.45f };
        [SerializeField] protected OneShotLayer exhaustPop = new OneShotLayer();
        [SerializeField, Range(0f, 1f)] protected float exhaustPopChancePerSecond = 0.25f;
        [SerializeField, Range(0f, 1f)] protected float blowOffThrottleDrop = 0.38f;
        [SerializeField, Range(0f, 1f)] protected float blowOffMinSpool = 0.42f;
        [SerializeField] protected float oneShotGlobalCooldown = 0.12f;

        [Header("Tire/Road Layers")]
        [SerializeField] protected AuxLoop tireRolling = new AuxLoop { label = "Tire Rolling", volume = 0.17f, minPitch = 0.8f, maxPitch = 1.2f };
        [SerializeField] protected AuxLoop skidLoop = new AuxLoop { label = "Skid", volume = 0.28f, minPitch = 0.92f, maxPitch = 1.08f };
        [SerializeField] protected float minimumSkidSpeedKph = 36f;
        [SerializeField] protected float handbrakeSkidMinSpeedKph = 18f;
        [SerializeField] protected float launchSkidMuteSpeedKph = 42f;
        [SerializeField, Range(0f, 1f)] protected float launchThrottleSuppression = 0.42f;
        [SerializeField, Range(0f, 1f)] protected float skidSlipThreshold = 0.38f;
        [SerializeField, Range(0f, 1.5f)] protected float fullSkidSlip = 0.95f;
        [SerializeField] protected float skidAttackResponse = 8f;
        [SerializeField] protected float skidReleaseResponse = 20f;

        [Header("Spatial/Mix")]
        [SerializeField, Range(0f, 1f)] protected float masterVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] protected float spatialBlend = 0.78f;
        [SerializeField] protected float minDistance = 3f;
        [SerializeField] protected float maxDistance = 55f;
        [SerializeField] protected AudioMixerGroup outputGroup;
        [SerializeField] protected string fallbackMixerGroupName = "SFX";
        [SerializeField] protected bool disableCompetingVehicleSources = true;

        [Header("Camera Mix")]
        [SerializeField] protected CarAudioCameraMode cameraMode = CarAudioCameraMode.Exterior;
        [SerializeField, Range(0f, 1f)] protected float interiorVolumeScale = 0.82f;
        [SerializeField, Range(0.75f, 1.15f)] protected float interiorPitchScale = 0.96f;
        [SerializeField, Range(0f, 1f)] protected float interiorAuxScale = 0.58f;

        [Header("Debug")]
        [SerializeField] protected bool showDebugState;
        [SerializeField] protected bool logConflictScan;
        [SerializeField] protected string currentStateName;
        [SerializeField] protected string engineMixDiagnostic;
        [SerializeField] protected int activeEngineLoopCount;
        [SerializeField] protected string dominantEngineLoops;
        [SerializeField] protected float debugCurrentRPM;
        [SerializeField] protected float debugAudioRPM;
        [SerializeField] protected float debugOnThrottleBlend;
        [SerializeField] protected float debugShiftDuckAmount;
        [SerializeField] protected bool debugShiftInProgress;
        [SerializeField] protected string debugLoopWeights;
        [SerializeField] protected string debugContinuityHint;
        [SerializeField] protected bool logLoopDiagnostics;
        [SerializeField] protected float loopLogInterval = 0.12f;

        private string lastLoggedDominantLoops;
        private int lastLoggedGear = 1;
        private bool lastLoggedShiftState;
        private string lastLoggedStateName;
        private string lastLoopLogSummary = "";
        private float lastLoopLogTime = -999f;
        private float clip04DominantTime;
        private bool wasClip04Dominant;
        private float upshiftMidBoostTimer;
        private float[] loopPitchAtLimitTimers = new float[16];
        private float timeSinceLastGearChange;
        private bool wasTopLoopActive;

        private readonly List<EngineLoopRuntime> engineRuntimes = new List<EngineLoopRuntime>(10);
        private readonly List<AuxLoopRuntime> auxRuntimes = new List<AuxLoopRuntime>(6);
        private readonly List<AudioSource> oneShotSources = new List<AudioSource>(3);
        private WheelCollider[] sampledWheelColliders = Array.Empty<WheelCollider>();
        private GameSettingsManager settingsManager;
        private AudioSource shiftSourceA;
        private AudioSource shiftSourceB;
        private int oneShotFlip;
        private int lastGear = 1;
        private float lastThrottle;
        private float currentDrivetrainRPM;
        private float smoothedRPM;
        private float smoothedThrottle;
        private float smoothedSpeedKph;
        private float smoothedSlip;
        private float shiftDuckTimer;
        private float loopSelectionFreezeTimer;
        private float frozenSelectionRPM;
        private float onThrottleBankTarget;
        private float onThrottleBlend;
        private float turboSpool01;
        private bool competingSourcesDisabled;
        private float lastAnyOneShotTime = -999f;
        private float lastBlowOffTime = -999f;
        private float lastThrottleBlipTime = -999f;
        private float lastExhaustPopTime = -999f;
        private bool hasOffThrottleBank;

        public float CurrentRPM => smoothedRPM;
        public float NormalizedRPM => GetRpm01(smoothedRPM);
        public float ThrottleInput => smoothedThrottle;
        public int Gear => useExternalTelemetry ? externalGear : gearbox != null ? gearbox.CurrentGear : 1;
        public float SpeedKph => smoothedSpeedKph;
        public bool Grounded => useExternalTelemetry ? externalGrounded : vehicle == null || vehicle.IsGrounded;
        public float SlipAmount => smoothedSlip;

        protected virtual void Awake()
        {
            ResolveReferences();
            settingsManager = GameSettingsManager.Instance != null ? GameSettingsManager.Instance : FindFirstObjectByType<GameSettingsManager>();
            sampledWheelColliders = GetWheelColliders();
            EnsureSourceRoot();
            RebuildAudioSources();
            InitializeTelemetry();
            RouteAudioSources();
            DisableCompetingAudioSources();
        }

        protected virtual void OnEnable()
        {
            ResolveReferences();
            if (gearbox != null)
            {
                gearbox.GearChanged -= HandleGearChanged;
                gearbox.GearChanged += HandleGearChanged;
                lastGear = gearbox.CurrentGear;
            }
        }

        protected virtual void OnDisable()
        {
            if (gearbox != null)
            {
                gearbox.GearChanged -= HandleGearChanged;
            }
        }

        protected virtual void Update()
        {
            ResolveReferences();

            float dt = Time.deltaTime;
            shiftDuckTimer = Mathf.Max(0f, shiftDuckTimer - dt);
            loopSelectionFreezeTimer = Mathf.Max(0f, loopSelectionFreezeTimer - dt);
            upshiftMidBoostTimer = Mathf.Max(0f, upshiftMidBoostTimer - dt);
            timeSinceLastGearChange += dt;
            UpdateTelemetry(dt);
            DetectGearChangeFallback();
            UpdateMainEngine(dt);
            UpdateAuxLayers(dt);
            DetectOneShots(dt);
            UpdateDebugContinuityState();
            UpdateDiagnosticLogging(dt);

            lastThrottle = smoothedThrottle;
            lastGear = Gear;
        }

        private void UpdateDiagnosticLogging(float dt)
        {
            if (!logLoopDiagnostics) return;

            bool isShifting = gearbox != null && gearbox.IsShifting;
            
            // Explicit Shift Start/End logging
            if (isShifting && !lastLoggedShiftState)
            {
                Debug.Log($"[CarEngineAudio] === SHIFT START (Gear {lastGear}) ===");
            }
            else if (!isShifting && lastLoggedShiftState)
            {
                Debug.Log($"[CarEngineAudio] === SHIFT END (Gear {Gear}) ===");
            }

            bool stateChanged = Gear != lastLoggedGear 
                || isShifting != lastLoggedShiftState 
                || currentStateName != lastLoggedStateName 
                || dominantEngineLoops != lastLoggedDominantLoops;

            bool periodicLog = (isShifting || smoothedThrottle > 0.8f) && (Time.time - lastLoopLogTime >= loopLogInterval);

            if (stateChanged || periodicLog)
            {
                lastLoopLogTime = Time.time;
                lastLoggedGear = Gear;
                lastLoggedShiftState = isShifting;
                lastLoggedStateName = currentStateName;
                lastLoggedDominantLoops = dominantEngineLoops;

                string summary = BuildLoopConsoleSummary();
                if (summary != lastLoopLogSummary)
                {
                    Debug.Log(summary);
                    lastLoopLogSummary = summary;
                }
            }

            UpdateClip04SpikeDetector(dt, isShifting);
            UpdatePitchLimitWarnings(dt);
            UpdateTopLoopStatusLogs();
        }

        private void UpdateTopLoopStatusLogs()
        {
            if (!logLoopDiagnostics) return;

            bool isShifting = gearbox != null && gearbox.IsShifting;
            bool isTopActive = false;
            float topVol = 0f;

            for (int i = 0; i < engineRuntimes.Count; i++)
            {
                if (engineRuntimes[i].Bank == LoopBank.Top)
                {
                    topVol = engineRuntimes[i].TargetVolume;
                    if (topVol > 0.05f) isTopActive = true;
                    break;
                }
            }

            if (isTopActive && !wasTopLoopActive)
            {
                Debug.Log($"[CarEngineAudio] Top loop ENTER gear={Gear} rpm={smoothedRPM:0} throttle={smoothedThrottle:0.00}");
            }
            else if (!isTopActive && wasTopLoopActive)
            {
                if (isShifting || loopSelectionFreezeTimer > 0f)
                {
                    Debug.Log($"[CarEngineAudio] Top loop SUPPRESSED (shift) gear={Gear} rpm={smoothedRPM:0}");
                }
                else if (timeSinceLastGearChange < 0.12f)
                {
                    Debug.Log($"[CarEngineAudio] Top loop SUPPRESSED (not stable) gear={Gear} rpm={smoothedRPM:0}");
                }
                else
                {
                    Debug.Log($"[CarEngineAudio] Top loop EXIT gear={Gear} rpm={smoothedRPM:0}");
                }
            }

            wasTopLoopActive = isTopActive;
        }

        private void UpdatePitchLimitWarnings(float dt)
        {
            for (int i = 0; i < engineRuntimes.Count; i++)
            {
                var r = engineRuntimes[i];
                if (r.TargetVolume > 0.05f && r.TargetPitch >= loopPitchClamp.y - 0.04f)
                {
                    loopPitchAtLimitTimers[i] += dt;
                    if (loopPitchAtLimitTimers[i] >= 0.08f)
                    {
                        // Periodically warn to avoid spam but catch long whines
                        if (Time.frameCount % 60 == 0)
                        {
                            string clipName = r.Source != null && r.Source.clip != null ? r.Source.clip.name : "null";
                            Debug.LogWarning($"[CarEngineAudio] WARNING: loop near pitch clamp -> {r.Settings.label} / {clipName} pitch={r.TargetPitch:0.00}");
                        }
                    }
                }
                else
                {
                    loopPitchAtLimitTimers[i] = 0f;
                }
            }
        }

        private void UpdateClip04SpikeDetector(float dt, bool isShifting)
        {
            bool is04Dominant = false;
            for (int i = 0; i < engineRuntimes.Count; i++)
            {
                var r = engineRuntimes[i];
                if (r.TargetVolume > 0.1f && r.Source != null && r.Source.clip != null && r.Source.clip.name.Contains("_04"))
                {
                    // Check if it's the most dominant
                    bool mostDominant = true;
                    for (int j = 0; j < engineRuntimes.Count; j++)
                    {
                        if (i != j && engineRuntimes[j].TargetVolume > r.TargetVolume)
                        {
                            mostDominant = false;
                            break;
                        }
                    }
                    if (mostDominant)
                    {
                        is04Dominant = true;
                        break;
                    }
                }
            }

            if (is04Dominant)
            {
                clip04DominantTime += dt;
            }
            else
            {
                if (wasClip04Dominant && isShifting && clip04DominantTime > 0.01f && clip04DominantTime < 0.15f)
                {
                    Debug.LogWarning($"[CarEngineAudio] WARNING: brief High On (_04) spike during shift! (Dominant for {clip04DominantTime:0.000}s)");
                }
                clip04DominantTime = 0f;
            }
            wasClip04Dominant = is04Dominant;
        }

        public void SetCameraMode(CarAudioCameraMode mode)
        {
            cameraMode = mode;
        }

        public void SetExternalTelemetry(float currentRPM, float normalizedRPM, float throttleInput, int gear, float speedKph, bool grounded, float slipAmount)
        {
            useExternalTelemetry = true;
            externalCurrentRPM = currentRPM;
            externalNormalizedRPM = Mathf.Clamp01(normalizedRPM);
            externalThrottleInput = Mathf.Clamp01(throttleInput);
            externalGear = Mathf.Max(0, gear);
            externalSpeedKph = Mathf.Max(0f, speedKph);
            externalGrounded = grounded;
            externalSlipAmount = Mathf.Clamp01(slipAmount);
        }

        [ContextMenu("Apply Sport Turbo Defaults")]
        protected void ApplySportTurboDefaults()
        {
            fallbackIdleRPM = 900f;
            fallbackMaxRPM = 7800f;
            fallbackRedlineRPM = 7300f;
            idleLoop = new EngineLoop("Idle", 900f, 650f, 1500f, 0.72f);
            onThrottleLoops = new[]
            {
                new EngineLoop("Low On", 1800f, 1050f, 3000f, 0.74f),
                new EngineLoop("Mid On", 3900f, 2400f, 5600f, 0.82f),
                new EngineLoop("High On", 6100f, 4700f, 7600f, 0.9f)
            };
            topLoop = new EngineLoop("Top/Redline On", 7350f, 6500f, 8200f, 0.82f);
            offThrottleLoops = new[]
            {
                new EngineLoop("Low Off", 1800f, 1100f, 3000f, 0.48f),
                new EngineLoop("Mid Off", 3900f, 2500f, 5600f, 0.55f),
                new EngineLoop("High Off", 6100f, 4800f, 7600f, 0.58f)
            };
            dominantEngineLoopLimit = 2;
            engineBusVolumeCeiling = 0.9f;
            loopPitchClamp = new Vector2(0.72f, 1.38f);
            rpmRiseResponse = 24f;
            rpmFallResponse = 10f;
            throttleResponse = 18f;
            volumeResponse = 16f;
            pitchResponse = 18f;
            shiftVolumeDuck = 0.24f;
            shiftDuckDuration = 0.11f;
            highRpmAggressionBoost = 0.36f;
        }

        protected virtual void ResolveReferences()
        {
            if (gearbox == null)
            {
                gearbox = GetComponentInParent<GearboxSystem>();
            }

            if (vehicle == null)
            {
                vehicle = GetComponentInParent<VehicleDynamicsController>();
            }

            if (input == null)
            {
                input = GetComponentInParent<InputReader>();
            }
        }

        private void EnsureSourceRoot()
        {
            if (sourceRoot == null)
            {
                Transform existing = transform.Find("CarEngineAudio");
                if (existing != null)
                {
                    sourceRoot = existing;
                }
                else
                {
                    GameObject root = new GameObject("CarEngineAudio");
                    root.transform.SetParent(transform, false);
                    sourceRoot = root.transform;
                }
            }
        }

        private void RebuildAudioSources()
        {
            engineRuntimes.Clear();
            auxRuntimes.Clear();
            oneShotSources.Clear();

            ClearSourceRoot();

            hasOffThrottleBank = HasAnyClip(offThrottleLoops);
            AddEngineRuntime(idleLoop, LoopBank.Idle, "Engine_Idle");
            AddEngineRuntimes(onThrottleLoops, LoopBank.OnThrottle, "Engine_On");
            AddEngineRuntime(topLoop, LoopBank.Top, "Engine_Top");
            AddEngineRuntimes(offThrottleLoops, LoopBank.OffThrottle, "Engine_Off");

            AddAuxRuntime(turboSpool, "Aux_Turbo");
            AddAuxRuntime(intake, "Aux_Intake");
            AddAuxRuntime(gearWhine, "Aux_GearWhine");
            AddAuxRuntime(tireRolling, "Aux_TireRolling");
            AddAuxRuntime(skidLoop, "Aux_Skid");

            shiftSourceA = CreateOneShotSource("OneShot_A");
            shiftSourceB = CreateOneShotSource("OneShot_B");
            oneShotSources.Add(shiftSourceA);
            oneShotSources.Add(shiftSourceB);
        }

        private void ClearSourceRoot()
        {
            if (sourceRoot == null)
            {
                return;
            }

            for (int i = sourceRoot.childCount - 1; i >= 0; i--)
            {
                DestroyAudioChild(sourceRoot.GetChild(i).gameObject);
            }
        }

        private void AddEngineRuntimes(EngineLoop[] loops, LoopBank bank, string prefix)
        {
            if (loops == null)
            {
                return;
            }

            for (int i = 0; i < loops.Length; i++)
            {
                AddEngineRuntime(loops[i], bank, $"{prefix}_{i:00}");
            }
        }

        private void AddEngineRuntime(EngineLoop loop, LoopBank bank, string sourceName)
        {
            if (loop == null)
            {
                return;
            }

            AudioSource source = CreateLoopSource(sourceName);
            AssignClip(source, BuildRuntimeLoopClip(loop.clip));
            StartLoopIfReady(source);
            engineRuntimes.Add(new EngineLoopRuntime
            {
                Settings = loop,
                Bank = bank,
                Source = source,
                TargetPitch = 1f
            });
        }

        private void AddAuxRuntime(AuxLoop loop, string sourceName)
        {
            if (loop == null)
            {
                return;
            }

            AudioSource source = CreateLoopSource(sourceName);
            AssignClip(source, loop.clip);
            StartLoopIfReady(source);
            auxRuntimes.Add(new AuxLoopRuntime
            {
                Settings = loop,
                Source = source,
                TargetPitch = 1f
            });
        }

        private AudioSource CreateLoopSource(string sourceName)
        {
            GameObject go = new GameObject(sourceName);
            go.transform.SetParent(sourceRoot, false);
            AudioSource source = go.AddComponent<AudioSource>();
            ConfigureSource(source, loop: true, priority: 96);
            return source;
        }

        private AudioSource CreateOneShotSource(string sourceName)
        {
            GameObject go = new GameObject(sourceName);
            go.transform.SetParent(sourceRoot, false);
            AudioSource source = go.AddComponent<AudioSource>();
            ConfigureSource(source, loop: false, priority: 90);
            return source;
        }

        private void ConfigureSource(AudioSource source, bool loop, int priority)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = 0f;
            source.pitch = 1f;
            source.priority = priority;
            source.dopplerLevel = 0f;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.outputAudioMixerGroup = outputGroup;
        }

        private void InitializeTelemetry()
        {
            ResolveRpmRange(out float idleRPM, out _);
            smoothedRPM = Mathf.Max(1f, idleRPM);
            currentDrivetrainRPM = smoothedRPM;
            smoothedThrottle = 0f;
            smoothedSpeedKph = 0f;
            smoothedSlip = 0f;
            turboSpool01 = 0f;
            lastThrottle = 0f;
            lastGear = Gear;
            onThrottleBankTarget = 0f;
            onThrottleBlend = 0f;
            frozenSelectionRPM = smoothedRPM;
        }

        private void UpdateTelemetry(float dt)
        {
            ResolveRpmRange(out float idleRPM, out float maxRPM);

            float rawRPM;
            float rawThrottle;
            float rawSpeed;
            float rawSlip;

            if (useExternalTelemetry)
            {
                rawRPM = externalCurrentRPM > 0f
                    ? externalCurrentRPM
                    : Mathf.Lerp(idleRPM, maxRPM, externalNormalizedRPM);
                rawThrottle = externalThrottleInput;
                rawSpeed = externalSpeedKph;
                rawSlip = externalSlipAmount;
            }
            else
            {
                rawRPM = gearbox != null ? gearbox.CurrentRPM : idleRPM;
                rawThrottle = input != null ? input.Throttle : 0f;
                if (vehicle != null && vehicle.IsReversing && input != null && input.ReverseHeld)
                {
                    rawThrottle = input.Brake;
                }

                rawSpeed = vehicle != null ? Mathf.Abs(vehicle.ForwardSpeedKph) : 0f;
                rawSlip = CalculateSlipAmount(dt);
            }

            rawRPM = Mathf.Clamp(Mathf.Max(rawRPM, idleRPM), idleRPM, maxRPM);
            currentDrivetrainRPM = rawRPM;
            if (Mathf.Abs(rawRPM - smoothedRPM) <= audioRpmDeadband)
            {
                rawRPM = smoothedRPM;
            }

            float rpmResponse = rawRPM >= smoothedRPM ? rpmRiseResponse : rpmFallResponse;
            smoothedRPM = ExpSmoothing(smoothedRPM, rawRPM, rpmResponse, dt);
            smoothedThrottle = ExpSmoothing(smoothedThrottle, Mathf.Clamp01(rawThrottle), throttleResponse, dt);
            smoothedSpeedKph = ExpSmoothing(smoothedSpeedKph, Mathf.Max(0f, rawSpeed), speedResponse, dt);
            smoothedSlip = ExpSmoothing(smoothedSlip, Mathf.Clamp01(rawSlip), rawSlip >= smoothedSlip ? skidAttackResponse : skidReleaseResponse, dt);
        }

        private void UpdateMainEngine(float dt)
        {
            // Classic racing-game illusion:
            // every steady-RPM loop keeps its own tone, only pitch-corrected near its
            // reference RPM. RPM bands decide the crossfade, throttle decides whether
            // the on-load or off-load tone owns the band. The dominance pass below
            // keeps this from becoming the old "many loops stacked together" problem.
            if (smoothedThrottle > throttleOnThreshold)
            {
                onThrottleBankTarget = 1f;
            }
            else if (smoothedThrottle < throttleOffThreshold)
            {
                onThrottleBankTarget = 0f;
            }

            onThrottleBlend = ExpSmoothing(onThrottleBlend, onThrottleBankTarget, onOffBankBlendResponse, dt);

            float maxShiftLeadRpm = 100f;
            float selectionRPM = loopSelectionFreezeTimer > 0f 
                ? Mathf.Min(frozenSelectionRPM, smoothedRPM + maxShiftLeadRpm) 
                : smoothedRPM;
            float rpm01 = GetRpm01(smoothedRPM);
            float throttleTone = Smooth01(Mathf.InverseLerp(throttleOffThreshold, Mathf.Max(throttleOnThreshold + 0.01f, throttleFullTone), smoothedThrottle));
            float onLoad = hasOffThrottleBank ? Mathf.Lerp(0.08f, 1f, onThrottleBlend) : Mathf.Lerp(offThrottleEngineFloor, 1f, throttleTone);
            float offLoad = hasOffThrottleBank ? Mathf.Lerp(1f, 0.08f, onThrottleBlend) : 0f;
            float highRpmBoost = 1f + highRpmAggressionBoost * Smooth01(Mathf.InverseLerp(0.58f, 0.96f, rpm01)) * throttleTone;
            float shiftGain = shiftDuckTimer > 0f ? 1f - shiftVolumeDuck : 1f;
            float limiterGain = GetLimiterGain(rpm01);
            float cameraGain = cameraMode == CarAudioCameraMode.Interior ? interiorVolumeScale : 1f;
            float cameraPitch = cameraMode == CarAudioCameraMode.Interior ? interiorPitchScale : 1f;
            float engineFloor = minimumEngineBedVolume
                * masterVolume
                * cameraGain
                * shiftGain
                * Mathf.Lerp(0.72f, 1f, Mathf.Max(throttleTone, onThrottleBlend));

            activeEngineLoopCount = 0;
            dominantEngineLoops = string.Empty;

            for (int i = 0; i < engineRuntimes.Count; i++)
            {
                EngineLoopRuntime runtime = engineRuntimes[i];
                EngineLoop settings = runtime.Settings;
                float bandWeight = GetBandWeight(settings, selectionRPM) * GetPitchClampSoftGain(settings, smoothedRPM);
                float bankLoad = ResolveBankLoad(runtime.Bank, onLoad, offLoad, throttleTone);
                float idleGain = runtime.Bank == LoopBank.Idle
                    ? Mathf.Lerp(1f, 1f - idleThrottleSuppression, throttleTone)
                    : 1f;

                runtime.NormalizedWeight = bandWeight * bankLoad;
                
                // Aggressive fade for pinned loops (near pitch clamp max)
                float pitchRamp = GetLoopPitch(settings, smoothedRPM);
                float pinFade = 1f;
                if (pitchRamp > 1.24f)
                {
                    pinFade = Mathf.Clamp01(1f - (pitchRamp - 1.24f) / (loopPitchClamp.y - 1.24f));
                    // Extra aggressive curve
                    pinFade *= pinFade;
                }

                runtime.TargetVolume = bandWeight
                    * bankLoad
                    * idleGain
                    * settings.baseVolume
                    * Mathf.Lerp(1f, 1f + settings.throttleAggression * 0.18f, throttleTone)
                    * highRpmBoost
                    * shiftGain
                    * limiterGain
                    * masterVolume
                    * cameraGain
                    * pinFade;

                runtime.TargetPitch = pitchRamp * cameraPitch;
                if (shiftDuckTimer > 0f)
                {
                    runtime.TargetPitch *= 1f - shiftPitchDip;
                }

                // Mid On Boost right after upshift
                if (upshiftMidBoostTimer > 0f && (settings.label.Contains("Mid On") || settings.label.Contains("Mid Off")))
                {
                    runtime.TargetVolume *= 1.18f;
                }
            }

            ApplyDominantLoopLimit();
            LimitEngineBus(engineFloor);

            for (int i = 0; i < engineRuntimes.Count; i++)
            {
                ApplyLoop(engineRuntimes[i].Source, engineRuntimes[i].TargetVolume, engineRuntimes[i].TargetPitch, dt, volumeResponse, pitchResponse);
            }
        }

        private float ResolveBankLoad(LoopBank bank, float onLoad, float offLoad, float throttleTone)
        {
            bool isShifting = gearbox != null && gearbox.IsShifting;

            switch (bank)
            {
                case LoopBank.Idle:
                    return 1f;
                case LoopBank.OnThrottle:
                    return onLoad;
                case LoopBank.OffThrottle:
                    return offLoad;
                case LoopBank.Top:
                    bool isFreeze = loopSelectionFreezeTimer > 0f;
                    float rpm01 = NormalizedRPM;
                    bool gearStable = timeSinceLastGearChange >= 0.12f;
                    bool topAllowed = !isShifting && !isFreeze && gearStable && rpm01 > 0.90f && smoothedThrottle > 0.70f;

                    if (!topAllowed)
                    {
                        return 0f;
                    }

                    float topThrottle = Smooth01(Mathf.InverseLerp(topLoopThrottleThreshold, 1f, smoothedThrottle));
                    return onLoad * topThrottle;
                default:
                    // Soften OnThrottle loops during shifts
                    float finalWeight = throttleTone;
                    if (isShifting)
                    {
                        if (bank == LoopBank.OnThrottle)
                        {
                            // Soften High On especially
                            finalWeight *= 0.72f;
                            
                            // Extra protection for Mid On if near pitch clamp
                            float rawPitch = GetLoopPitch(null, smoothedRPM); // Wait, need generic pitch
                            // Actually, ResolveBankLoad doesn't have loop reference. 
                            // Let's rely on the label check or the loop in UpdateMainEngine.
                        }
                    }
                    return finalWeight;
            }
        }

        private void UpdateAuxLayers(float dt)
        {
            float rpm01 = NormalizedRPM;
            float speed01 = Mathf.InverseLerp(0f, 280f, smoothedSpeedKph);
            float turboTarget = smoothedThrottle * Smooth01(Mathf.InverseLerp(0.28f, 0.88f, rpm01));
            turboSpool01 = ExpSmoothing(turboSpool01, turboTarget, turboTarget >= turboSpool01 ? 9f : 16f, dt);

            for (int i = 0; i < auxRuntimes.Count; i++)
            {
                AuxLoopRuntime runtime = auxRuntimes[i];
                AuxLoop settings = runtime.Settings;
                float auxScale = cameraMode == CarAudioCameraMode.Interior ? interiorAuxScale : 1f;
                float target = 0f;
                float pitch01 = rpm01;

                if (settings == turboSpool)
                {
                    target = turboSpool01;
                    pitch01 = turboSpool01;
                }
                else if (settings == intake)
                {
                    target = smoothedThrottle * Smooth01(Mathf.InverseLerp(0.2f, 0.9f, rpm01));
                }
                else if (settings == gearWhine)
                {
                    target = speed01 * Mathf.Lerp(0.28f, 1f, smoothedThrottle);
                    pitch01 = Mathf.Max(speed01, rpm01 * 0.65f);
                }
                else if (settings == tireRolling)
                {
                    target = Grounded ? Smooth01(speed01) : 0f;
                    pitch01 = speed01;
                }
                else if (settings == skidLoop)
                {
                    target = smoothedSlip;
                    pitch01 = smoothedSlip;
                }

                runtime.TargetVolume = target * settings.volume * masterVolume * auxScale;
                runtime.TargetPitch = Mathf.Lerp(settings.minPitch, settings.maxPitch, Mathf.Clamp01(pitch01));
                ApplyLoop(runtime.Source, runtime.TargetVolume, runtime.TargetPitch, dt, settings.fadeResponse, pitchResponse);
            }
        }

        private void ApplyDominantLoopLimit()
        {
            int limit = Mathf.Clamp(dominantEngineLoopLimit, 1, Mathf.Max(1, engineRuntimes.Count));
            for (int keep = 0; keep < limit; keep++)
            {
                int bestIndex = -1;
                float bestVolume = 0f;
                for (int i = 0; i < engineRuntimes.Count; i++)
                {
                    EngineLoopRuntime runtime = engineRuntimes[i];
                    if (runtime.TargetVolume > bestVolume && !IsAlreadyKept(i, keep))
                    {
                        bestVolume = runtime.TargetVolume;
                        bestIndex = i;
                    }
                }

                if (bestIndex >= 0)
                {
                    keptIndices[keep] = bestIndex;
                }
                else
                {
                    keptIndices[keep] = -1;
                }
            }

            for (int i = 0; i < engineRuntimes.Count; i++)
            {
                bool keepRuntime = false;
                for (int j = 0; j < limit; j++)
                {
                    if (keptIndices[j] == i)
                    {
                        keepRuntime = true;
                        break;
                    }
                }

                if (!keepRuntime)
                {
                    engineRuntimes[i].TargetVolume = 0f;
                }
            }
        }

        private readonly int[] keptIndices = { -1, -1, -1, -1 };

        private bool IsAlreadyKept(int index, int keptCount)
        {
            for (int i = 0; i < keptCount; i++)
            {
                if (keptIndices[i] == index)
                {
                    return true;
                }
            }

            return false;
        }

        private void LimitEngineBus(float floorVolume)
        {
            float total = 0f;
            activeEngineLoopCount = 0;
            dominantEngineLoops = string.Empty;
            debugLoopWeights = string.Empty;

            for (int i = 0; i < engineRuntimes.Count; i++)
            {
                float volume = engineRuntimes[i].TargetVolume;
                total += volume;
                if (volume > 0.001f)
                {
                    activeEngineLoopCount++;
                    if (dominantEngineLoops.Length > 0)
                    {
                        dominantEngineLoops += ", ";
                        debugLoopWeights += " | ";
                    }

                    dominantEngineLoops += engineRuntimes[i].Settings.label;
                    debugLoopWeights += $"{engineRuntimes[i].Settings.label}:{volume:0.00}";
                }
            }

            if (total > 0.001f && total < floorVolume)
            {
                float floorGain = floorVolume / total;
                for (int i = 0; i < engineRuntimes.Count; i++)
                {
                    engineRuntimes[i].TargetVolume *= floorGain;
                }

                total = floorVolume;
            }

            if (total > engineBusVolumeCeiling && total > 0f)
            {
                float gain = engineBusVolumeCeiling / total;
                for (int i = 0; i < engineRuntimes.Count; i++)
                {
                    engineRuntimes[i].TargetVolume *= gain;
                }

                total = engineBusVolumeCeiling;
            }

            engineMixDiagnostic = $"rpm {smoothedRPM:0}, throttle {smoothedThrottle:0.00}, engine bus {total:0.00}/{engineBusVolumeCeiling:0.00}, loops {activeEngineLoopCount}";
        }

        private void ApplyLoop(AudioSource source, float targetVolume, float targetPitch, float dt, float fadeResponse, float pitchFadeResponse)
        {
            if (source == null)
            {
                return;
            }

            if (source.clip == null)
            {
                source.volume = 0f;
                return;
            }

            source.volume = ExpSmoothing(source.volume, Mathf.Clamp01(targetVolume), fadeResponse, dt);
            source.pitch = ExpSmoothing(source.pitch, Mathf.Clamp(targetPitch, 0.25f, 2.5f), pitchFadeResponse, dt);

            if (source.volume > 0.001f && !source.isPlaying)
            {
                source.Play();
            }
        }

        private void DetectGearChangeFallback()
        {
            if (Gear != lastGear)
            {
                HandleGearChanged(lastGear, Gear);
            }
        }

        private void HandleGearChanged(int previousGear, int currentGear)
        {
            lastGear = currentGear;
            shiftDuckTimer = shiftDuckDuration;
            loopSelectionFreezeTimer = loopSelectionFreezeDuration;
            frozenSelectionRPM = smoothedRPM;
            timeSinceLastGearChange = 0f;

            if (logLoopDiagnostics)
            {
                Debug.Log($"[CarEngineAudio] Gear Shift: {previousGear} -> {currentGear} at {smoothedRPM:0} RPM (Audio: {smoothedRPM:0})");
            }

            if (currentGear > previousGear)
            {
                PlayOneShot(shiftUp);
                upshiftMidBoostTimer = 0.12f;
            }
            else if (currentGear < previousGear)
            {
                PlayOneShot(shiftDown);
            }
        }

        private void DetectOneShots(float dt)
        {
            float throttleDelta = smoothedThrottle - lastThrottle;
            float throttleDrop = lastThrottle - smoothedThrottle;

            if (throttleDelta > 0.45f && Time.time - lastThrottleBlipTime >= throttleBlip.minInterval)
            {
                lastThrottleBlipTime = Time.time;
                PlayOneShot(throttleBlip);
            }

            if (throttleDrop >= blowOffThrottleDrop && turboSpool01 >= blowOffMinSpool && Time.time - lastBlowOffTime >= turboBlowOff.minInterval)
            {
                lastBlowOffTime = Time.time;
                PlayOneShot(turboBlowOff);
            }

            bool liftoffPopWindow = throttleDrop > 0.2f || smoothedThrottle < 0.08f && lastThrottle > 0.28f;
            if (liftoffPopWindow && NormalizedRPM > 0.45f && Time.time - lastExhaustPopTime >= exhaustPop.minInterval)
            {
                if (UnityEngine.Random.value < exhaustPopChancePerSecond * dt)
                {
                    lastExhaustPopTime = Time.time;
                    PlayOneShot(exhaustPop);
                }
            }
        }

        private void PlayOneShot(OneShotLayer layer)
        {
            if (layer == null || layer.clip == null || oneShotSources.Count == 0)
            {
                return;
            }

            if (Time.time - lastAnyOneShotTime < oneShotGlobalCooldown)
            {
                return;
            }

            lastAnyOneShotTime = Time.time;
            AudioSource source = oneShotSources[oneShotFlip % oneShotSources.Count];
            oneShotFlip++;
            source.pitch = UnityEngine.Random.Range(layer.pitchRange.x, layer.pitchRange.y);
            source.volume = 1f;
            source.PlayOneShot(layer.clip, Mathf.Clamp01(layer.volume * masterVolume));
        }

        private float CalculateSlipAmount(float dt)
        {
            if (vehicle == null)
            {
                return 0f;
            }

            float speedKph = Mathf.Max(Mathf.Abs(vehicle.ForwardSpeedKph), vehicle.SpeedKph);
            bool handbrakeSkid = input != null && input.Handbrake && speedKph >= handbrakeSkidMinSpeedKph;
            bool slideSkid = vehicle.IsSliding && speedKph >= minimumSkidSpeedKph;

            if (!handbrakeSkid && !slideSkid)
            {
                return 0f;
            }

            bool launchSuppressed = input != null
                && input.Throttle >= launchThrottleSuppression
                && speedKph < launchSkidMuteSpeedKph
                && !slideSkid
                && !handbrakeSkid;

            if (launchSuppressed)
            {
                return 0f;
            }

            if (sampledWheelColliders == null || sampledWheelColliders.Length == 0)
            {
                sampledWheelColliders = GetWheelColliders();
            }

            float maxSlip = vehicle.IsSliding ? 0.3f : 0f;
            for (int i = 0; i < sampledWheelColliders.Length; i++)
            {
                WheelCollider wheel = sampledWheelColliders[i];
                if (wheel == null || !wheel.GetGroundHit(out WheelHit hit))
                {
                    continue;
                }

                maxSlip = Mathf.Max(maxSlip, Mathf.Abs(hit.sidewaysSlip));
            }

            if (handbrakeSkid)
            {
                maxSlip = Mathf.Max(maxSlip, skidSlipThreshold + 0.12f);
            }

            return Mathf.InverseLerp(skidSlipThreshold, Mathf.Max(skidSlipThreshold + 0.01f, fullSkidSlip), maxSlip);
        }

        private WheelCollider[] GetWheelColliders()
        {
            if (vehicle != null)
            {
                return vehicle.GetComponentsInChildren<WheelCollider>(true);
            }

            return GetComponentsInChildren<WheelCollider>(true);
        }

        private float GetBandWeight(EngineLoop loop, float rpm)
        {
            if (loop == null || loop.clip == null || loop.baseVolume <= 0f)
            {
                return 0f;
            }

            float originalMin = Mathf.Min(loop.minRPM, loop.maxRPM);
            float originalMax = Mathf.Max(loop.minRPM, loop.maxRPM);
            float expansion = (originalMax - originalMin) * rpmBandOverlapExpansion;
            float min = Mathf.Max(1f, originalMin - expansion);
            float max = originalMax + expansion;
            float reference = Mathf.Clamp(loop.referenceRPM, min + 1f, max - 1f);

            if (rpm <= min || rpm >= max)
            {
                return 0f;
            }

            if (rpm <= reference)
            {
                return Smooth01(Mathf.InverseLerp(min, reference, rpm));
            }

            return 1f - Smooth01(Mathf.InverseLerp(reference, max, rpm));
        }

        private float GetPitchClampSoftGain(EngineLoop loop, float rpm)
        {
            if (loop == null)
            {
                return 1f;
            }

            float reference = Mathf.Max(1f, loop.referenceRPM);
            float rawPitch = loop.basePitch * rpm / reference;
            float minPitch = Mathf.Min(loopPitchClamp.x, loopPitchClamp.y);
            float maxPitch = Mathf.Max(loopPitchClamp.x, loopPitchClamp.y);
            float fadeRange = Mathf.Max(0.01f, pitchClampFadeRange);

            if (rawPitch > maxPitch)
            {
                return 1f - Smooth01(Mathf.InverseLerp(maxPitch, maxPitch + fadeRange, rawPitch));
            }

            if (rawPitch < minPitch)
            {
                return Smooth01(Mathf.InverseLerp(minPitch - fadeRange, minPitch, rawPitch));
            }

            return 1f;
        }

        private float GetLoopPitch(EngineLoop loop, float rpm)
        {
            if (loop == null)
            {
                return 1f;
            }

            float reference = Mathf.Max(1f, loop.referenceRPM);
            float rawPitch = loop.basePitch * rpm / reference;
            float minPitch = Mathf.Min(loopPitchClamp.x, loopPitchClamp.y);
            float maxPitch = Mathf.Max(loopPitchClamp.x, loopPitchClamp.y);
            return Mathf.Clamp(rawPitch, minPitch, maxPitch);
        }

        private float GetLimiterGain(float rpm01)
        {
            if (!enableSoftLimiter || rpm01 < limiterEnter01 || smoothedThrottle < throttleOnThreshold)
            {
                return 1f;
            }

            float pulse = Mathf.Sin(Time.time * Mathf.PI * 2f * limiterPulseHz) * 0.5f + 0.5f;
            return 1f - pulse * limiterVolumePulse;
        }

        private void ResolveRpmRange(out float idleRPM, out float maxRPM)
        {
            idleRPM = fallbackIdleRPM;
            maxRPM = fallbackMaxRPM;

            if (vehicle != null && vehicle.RuntimeStats != null)
            {
                idleRPM = Mathf.Max(1f, vehicle.RuntimeStats.IdleRPM);
                maxRPM = Mathf.Max(idleRPM + 1f, vehicle.RuntimeStats.MaxRPM);
            }

            idleRPM = Mathf.Max(1f, idleRPM);
            maxRPM = Mathf.Max(idleRPM + 1f, maxRPM);
        }

        private float GetRpm01(float rpm)
        {
            ResolveRpmRange(out float idleRPM, out float maxRPM);
            return Mathf.InverseLerp(idleRPM, maxRPM, rpm);
        }

        private void RouteAudioSources()
        {
            if (outputGroup == null)
            {
                if (settingsManager == null)
                {
                    settingsManager = GameSettingsManager.Instance != null ? GameSettingsManager.Instance : FindFirstObjectByType<GameSettingsManager>();
                }

                if (settingsManager != null)
                {
                    outputGroup = settingsManager.GetMixerGroup(fallbackMixerGroupName);
                }
            }

            if (sourceRoot == null)
            {
                return;
            }

            AudioSource[] sources = sourceRoot.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i].outputAudioMixerGroup = outputGroup;
            }
        }

        private void DisableCompetingAudioSources()
        {
            VehicleDynamicsController rootVehicle = GetComponentInParent<VehicleDynamicsController>();
            if (rootVehicle == null || sourceRoot == null)
            {
                return;
            }

            if (competingSourcesDisabled)
            {
                return;
            }

            competingSourcesDisabled = true;

            AudioSource[] sources = rootVehicle.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source == null || source.transform.IsChildOf(sourceRoot))
                {
                    continue;
                }

                source.Stop();
                source.volume = 0f;
                source.enabled = false;

                if (logConflictScan)
                {
                    Debug.Log($"[CarEngineAudio] Disabled competing AudioSource '{source.gameObject.name}'.", source);
                }
            }
        }

        private string ResolveStateName()
        {
            if (!Grounded)
            {
                return "Airborne";
            }

            if (enableSoftLimiter && NormalizedRPM >= limiterEnter01 && smoothedThrottle > throttleOnThreshold)
            {
                return "Limiter";
            }

            if (smoothedSpeedKph < 2f && NormalizedRPM < 0.12f)
            {
                return "Idle";
            }

            return smoothedThrottle >= throttleOnThreshold ? "On-Throttle" : "Off-Throttle";
        }

        private void UpdateDebugContinuityState()
        {
            debugCurrentRPM = currentDrivetrainRPM;
            debugAudioRPM = smoothedRPM;
            debugOnThrottleBlend = onThrottleBlend;
            debugShiftDuckAmount = shiftDuckTimer > 0f ? shiftVolumeDuck : 0f;
            debugShiftInProgress = loopSelectionFreezeTimer > 0f || gearbox != null && gearbox.IsShifting;

            float throttleDelta = Mathf.Abs(smoothedThrottle - lastThrottle);
            float rpmDelta = Mathf.Abs(currentDrivetrainRPM - smoothedRPM);
            bool transitionEvent = debugShiftInProgress
                || shiftDuckTimer > 0f
                || throttleDelta > 0.035f
                || rpmDelta > 180f
                || NormalizedRPM > limiterEnter01;

            debugContinuityHint = transitionEvent
                ? "B likely: blend/throttle/shift/redline transition active"
                : "A candidate if lapse repeats steadily: loop seam";

            currentStateName = ResolveStateName();
            if (showDebugState)
            {
                // Already updated above
            }
        }

        private static void AssignClip(AudioSource source, AudioClip clip)
        {
            if (source == null)
            {
                return;
            }

            source.clip = clip;
            if (clip != null)
            {
                source.time = 0f;
            }
        }

        private void StartLoopIfReady(AudioSource source)
        {
            if (!keepEngineLoopsRunning || source == null || source.clip == null || source.isPlaying)
            {
                return;
            }

            source.volume = 0f;
            source.Play();
        }

        private AudioClip BuildRuntimeLoopClip(AudioClip source)
        {
            if (!repairLoopSeamsAtRuntime || source == null)
            {
                return source;
            }

            int sampleCount = source.samples * source.channels;
            if (sampleCount <= source.channels * 16)
            {
                return source;
            }

            float[] samples = new float[sampleCount];
            try
            {
                source.GetData(samples, 0);
            }
            catch (Exception)
            {
                return source;
            }

            int channels = Mathf.Max(1, source.channels);
            int frames = source.samples;
            int fadeFrames = Mathf.Clamp(
                Mathf.RoundToInt(source.frequency * loopBoundaryCrossfadeMs / 1000f),
                8,
                Mathf.Max(8, frames / 8));

            for (int channel = 0; channel < channels; channel++)
            {
                float dc = 0f;
                for (int frame = 0; frame < frames; frame++)
                {
                    dc += samples[frame * channels + channel];
                }

                dc /= frames;
                for (int frame = 0; frame < frames; frame++)
                {
                    samples[frame * channels + channel] -= dc;
                }

                float first = samples[channel];
                float last = samples[(frames - 1) * channels + channel];
                float boundary = (first + last) * 0.5f;
                for (int frame = 0; frame < fadeFrames; frame++)
                {
                    float t = Smooth01(frame / Mathf.Max(1f, fadeFrames - 1f));
                    int startIndex = frame * channels + channel;
                    int endIndex = (frames - fadeFrames + frame) * channels + channel;
                    samples[startIndex] = Mathf.Lerp(boundary, samples[startIndex], t);
                    samples[endIndex] = Mathf.Lerp(samples[endIndex], boundary, t);
                }
            }

            AudioClip repaired = AudioClip.Create(
                source.name + "_RuntimeLoopRepaired",
                source.samples,
                source.channels,
                source.frequency,
                false);
            repaired.SetData(samples, 0);
            return repaired;
        }

        private static bool HasAnyClip(EngineLoop[] loops)
        {
            if (loops == null)
            {
                return false;
            }

            for (int i = 0; i < loops.Length; i++)
            {
                if (loops[i] != null && loops[i].clip != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static float ExpSmoothing(float current, float target, float response, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-Mathf.Max(0.01f, response) * dt));
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static void DestroyAudioChild(GameObject child)
        {
            if (child == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }

        private string BuildLoopConsoleSummary()
        {
            bool isShifting = gearbox != null && gearbox.IsShifting;
            float duck = shiftDuckTimer > 0f ? shiftVolumeDuck : 0f;
            
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append($"[CarEngineAudio] gear={Gear} rawRPM={currentDrivetrainRPM:0} audioRPM={smoothedRPM:0} ");
            
            float selectionRPM = loopSelectionFreezeTimer > 0f 
                ? Mathf.Min(frozenSelectionRPM, smoothedRPM + 200f) 
                : smoothedRPM;
            sb.Append($"selectRPM={selectionRPM:0} throttle={smoothedThrottle:0.00} shift={isShifting} duck={duck:0.00} loops=");

            bool first = true;
            for (int i = 0; i < engineRuntimes.Count; i++)
            {
                EngineLoopRuntime runtime = engineRuntimes[i];
                if (runtime.TargetVolume > 0.05f)
                {
                    if (!first) sb.Append(" | ");
                    string clipName = runtime.Source != null && runtime.Source.clip != null ? runtime.Source.clip.name : "null";
                    sb.Append($"{runtime.Settings.label}({clipName} vol={runtime.TargetVolume:0.00} pitch={runtime.TargetPitch:0.00})");
                    first = false;
                }
            }

            return sb.ToString();
        }
    }
}

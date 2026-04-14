using UnityEngine;
using Underground.UI;
using Underground.Vehicle;

namespace Underground.Audio
{
    [DisallowMultipleComponent]
    public class VehicleAudioController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GearboxSystem gearbox;
        [SerializeField] private VehicleDynamicsController vehicle;
        [SerializeField] private InputReader input;
        [SerializeField] private RealisticEngineSound engineSound;

        [Header("Interior Clips")]
        [SerializeField] private AudioClip interiorIdleClip;
        [SerializeField] private AudioClip interiorLowOnClip;
        [SerializeField] private AudioClip interiorMidOnClip;
        [SerializeField] private AudioClip interiorHighOnClip;
        [SerializeField] private AudioClip interiorLowOffClip;
        [SerializeField] private AudioClip interiorMidOffClip;
        [SerializeField] private AudioClip interiorHighOffClip;

        [Header("Turbo And Shift")]
        [SerializeField] private AudioClip turboSpoolClip;
        [SerializeField] private AudioClip turboWhistleClip;
        [SerializeField] private AudioClip blowOffClip;
        [SerializeField] private AudioClip shiftUpClip;
        [SerializeField] private AudioClip shiftDownClip;
        [SerializeField] private AudioClip liftOffCrackleClip;

        [Header("Engine Model")]
        [SerializeField] private float masterVolume = 0.78f;
        [SerializeField] private float audioUpdateRate = 40f;
        [SerializeField] private float rpmRiseResponse = 14f;
        [SerializeField] private float rpmFallResponse = 8.5f;
        [SerializeField] private float coastRpmFallResponse = 4.5f;
        [SerializeField] private float throttleResponse = 9f;
        [SerializeField] private float brakeResponse = 10f;
        [SerializeField] private float speedResponse = 6f;
        [SerializeField] private float loadResponse = 8f;
        [SerializeField] private float engineRpmInfluenceOnThrottle = 0.9f;
        [SerializeField] private float engineRpmInfluenceOnCoast = 0.32f;
        [SerializeField] private float wheelRpmBiasOnCoast = 0.8f;
        [SerializeField] private float accelToLoadScale = 0.03f;
        [SerializeField] private float pitchVariationAmount = 0.012f;
        [SerializeField] private float movingIdleFadeStartKph = 7f;
        [SerializeField] private float movingIdleFadeEndKph = 24f;
        [SerializeField] private float highBandEntrySpeedKph = 30f;
        [SerializeField] private float highBandFullSpeedKph = 90f;
        [SerializeField] private float highBandLoadThreshold = 0.26f;

        [Header("Turbo")]
        [SerializeField] private float turboAmount = 0.72f;
        [SerializeField] private float spoolAttack = 3.2f;
        [SerializeField] private float spoolRelease = 4.4f;
        [SerializeField] private float whistleStartRpm01 = 0.54f;
        [SerializeField] private float whistleStartLoad = 0.42f;
        [SerializeField] private float blowOffMinSpool = 0.28f;
        [SerializeField] private float blowOffThrottleDrop = 0.22f;

        [Header("Shift")]
        [SerializeField] private float shiftDuckDuration = 0.09f;
        [SerializeField] private float shiftDuckAmount = 0.3f;
        [SerializeField] private float shiftCooldown = 0.08f;
        [SerializeField] private float shiftPitchDrop = 0.085f;

        [Header("Lift-Off Crackles")]
        [SerializeField] private bool enableLiftOffCrackles = true;
        [SerializeField] private float crackleMinRpm01 = 0.7f;
        [SerializeField] private float crackleChancePerSecond = 1.15f;

        [Header("Interior Detection")]
        [SerializeField] private Vector3 interiorDetectionExtents = new Vector3(1.15f, 1.25f, 2.3f);
        [SerializeField] private float interiorTransitionSpeed = 5f;

        [Header("Spatial")]
        [SerializeField] private float spatialBlend = 1f;
        [SerializeField] private float minDistance = 3f;
        [SerializeField] private float maxDistance = 48f;

        private GameSettingsManager settingsManager;
        private Camera mainCamera;
        private Transform generatedRoot;

        private AudioSource idleExteriorSource;
        private AudioSource lowOnExteriorSource;
        private AudioSource midOnExteriorSource;
        private AudioSource highOnExteriorSource;
        private AudioSource maxExteriorSource;
        private AudioSource lowOffExteriorSource;
        private AudioSource midOffExteriorSource;
        private AudioSource highOffExteriorSource;

        private AudioSource idleInteriorSource;
        private AudioSource lowOnInteriorSource;
        private AudioSource midOnInteriorSource;
        private AudioSource highOnInteriorSource;
        private AudioSource maxInteriorSource;
        private AudioSource lowOffInteriorSource;
        private AudioSource midOffInteriorSource;
        private AudioSource highOffInteriorSource;

        private AudioSource turboSpoolSource;
        private AudioSource turboWhistleSource;
        private AudioSource oneShotSource;

        private float smoothedRpm;
        private float smoothedThrottle;
        private float smoothedBrake;
        private float smoothedSpeedKph;
        private float smoothedLoad;
        private float turboSpool01;
        private float interiorBlend;
        private float lastThrottle;
        private float lastForwardSpeedKph;
        private float updateTimer;
        private float nextMixerRouteTime;
        private float shiftDuckTimer;
        private float shiftCooldownTimer;
        private int lastGear = 1;

        private void Awake()
        {
            ResolveReferences();
            settingsManager = FindFirstObjectByType<GameSettingsManager>();
            EnsureStockPlaybackActive();
            EnsureOneShotSource();
            InitializeState();
            RouteAudioSources();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureStockPlaybackActive();
            EnsureOneShotSource();

            if (gearbox != null)
            {
                gearbox.GearChanged -= HandleGearChanged;
                gearbox.GearChanged += HandleGearChanged;
            }
        }

        private void OnDisable()
        {
            if (gearbox != null)
            {
                gearbox.GearChanged -= HandleGearChanged;
            }

            if (engineSound != null)
            {
                engineSound.enabled = false;
            }
        }

        private void Update()
        {
            if (!TryResolveRuntimeState())
            {
                return;
            }

            updateTimer += Time.deltaTime;
            shiftDuckTimer = Mathf.Max(0f, shiftDuckTimer - Time.deltaTime);
            shiftCooldownTimer = Mathf.Max(0f, shiftCooldownTimer - Time.deltaTime);

            float minRate = Mathf.Max(8f, audioUpdateRate);
            if (updateTimer < 1f / minRate)
            {
                return;
            }

            float dt = updateTimer;
            updateTimer = 0f;

            UpdateTelemetry(dt);
            UpdateTurbo(dt);
            UpdateInteriorBlend(dt);
            SyncRealisticEngineSound();
            DetectLiftOffEffects(dt);

            lastThrottle = smoothedThrottle;
            lastGear = gearbox != null ? gearbox.CurrentGear : lastGear;

            if (Time.unscaledTime >= nextMixerRouteTime)
            {
                RouteAudioSources();
                nextMixerRouteTime = Time.unscaledTime + 0.5f;
            }
        }

        private bool TryResolveRuntimeState()
        {
            ResolveReferences();
            if (vehicle == null || gearbox == null || engineSound == null)
            {
                return false;
            }

            EnsureStockPlaybackActive();
            EnsureOneShotSource();
            return true;
        }

        private void ResolveReferences()
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

            if (engineSound == null)
            {
                engineSound = GetComponent<RealisticEngineSound>();
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        private void EnsureStockPlaybackActive()
        {
            if (engineSound == null)
            {
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            engineSound.mainCamera = mainCamera;
            engineSound.gasPedalValueSetting = RealisticEngineSound.GasPedalValue.NotSimulated;
            engineSound.maxRPMLimit = vehicle != null && vehicle.RuntimeStats != null ? vehicle.RuntimeStats.MaxRPM : Mathf.Max(1000f, engineSound.maxRPMLimit);
            engineSound.minDistance = minDistance;
            engineSound.maxDistance = maxDistance;
            engineSound.spatialBlend = spatialBlend;
            engineSound.enabled = true;
        }

        private void EnsureOneShotSource()
        {
            if (oneShotSource != null)
            {
                return;
            }

            oneShotSource = CreateOneShotSource("OneShots", transform);
        }

        private void SyncRealisticEngineSound()
        {
            if (engineSound == null || vehicle == null || vehicle.RuntimeStats == null)
            {
                return;
            }

            float shiftDuck = shiftDuckTimer > 0f ? 1f - shiftDuckAmount : 1f;
            float pedalValue = Mathf.Clamp01(smoothedThrottle * shiftDuck);

            engineSound.maxRPMLimit = vehicle.RuntimeStats.MaxRPM;
            engineSound.engineCurrentRPM = Mathf.Clamp(smoothedRpm, vehicle.RuntimeStats.IdleRPM, vehicle.RuntimeStats.MaxRPM);
            engineSound.gasPedalPressing = smoothedThrottle > 0.08f;
            engineSound.gasPedalValue = pedalValue;
            engineSound.isReversing = vehicle.IsReversing;
            engineSound.carCurrentSpeed = Mathf.Abs(vehicle.ForwardSpeedKph);
            engineSound.carMaxSpeed = Mathf.Max(1f, vehicle.RuntimeStats.MaxSpeedKph);
            engineSound.isShifting = shiftDuckTimer > 0f;
        }

        private void InitializeState()
        {
            float idleRpm = vehicle != null && vehicle.RuntimeStats != null ? vehicle.RuntimeStats.IdleRPM : 900f;
            smoothedRpm = idleRpm;
            smoothedThrottle = 0f;
            smoothedBrake = 0f;
            smoothedSpeedKph = 0f;
            smoothedLoad = 0f;
            turboSpool01 = 0f;
            interiorBlend = 0f;
            lastThrottle = 0f;
            lastForwardSpeedKph = vehicle != null ? vehicle.ForwardSpeedKph : 0f;
            lastGear = gearbox != null ? gearbox.CurrentGear : 1;
        }

        private void BuildSources()
        {
            if (generatedRoot == null)
            {
                Transform existing = transform.Find("HybridEngineAudio");
                generatedRoot = existing != null ? existing : new GameObject("HybridEngineAudio").transform;
                generatedRoot.SetParent(transform, false);
            }

            if (idleExteriorSource == null) idleExteriorSource = CreateLoopSource("IdleExterior", generatedRoot, engineSound != null ? engineSound.idleClip : null);
            if (lowOnExteriorSource == null) lowOnExteriorSource = CreateLoopSource("LowOnExterior", generatedRoot, engineSound != null ? engineSound.lowOnClip : null);
            if (midOnExteriorSource == null) midOnExteriorSource = CreateLoopSource("MidOnExterior", generatedRoot, engineSound != null ? engineSound.medOnClip : null);
            if (highOnExteriorSource == null) highOnExteriorSource = CreateLoopSource("HighOnExterior", generatedRoot, engineSound != null ? engineSound.highOnClip : null);
            if (maxExteriorSource == null) maxExteriorSource = CreateLoopSource("MaxExterior", generatedRoot, engineSound != null ? engineSound.maxRPMClip : null);
            if (lowOffExteriorSource == null) lowOffExteriorSource = CreateLoopSource("LowOffExterior", generatedRoot, engineSound != null ? engineSound.lowOffClip : null);
            if (midOffExteriorSource == null) midOffExteriorSource = CreateLoopSource("MidOffExterior", generatedRoot, engineSound != null ? engineSound.medOffClip : null);
            if (highOffExteriorSource == null) highOffExteriorSource = CreateLoopSource("HighOffExterior", generatedRoot, engineSound != null ? engineSound.highOffClip : null);

            if (idleInteriorSource == null) idleInteriorSource = CreateLoopSource("IdleInterior", generatedRoot, interiorIdleClip);
            if (lowOnInteriorSource == null) lowOnInteriorSource = CreateLoopSource("LowOnInterior", generatedRoot, interiorLowOnClip);
            if (midOnInteriorSource == null) midOnInteriorSource = CreateLoopSource("MidOnInterior", generatedRoot, interiorMidOnClip);
            if (highOnInteriorSource == null) highOnInteriorSource = CreateLoopSource("HighOnInterior", generatedRoot, interiorHighOnClip);
            if (maxInteriorSource == null) maxInteriorSource = CreateLoopSource("MaxInterior", generatedRoot, engineSound != null ? engineSound.maxRPMClip : null);
            if (lowOffInteriorSource == null) lowOffInteriorSource = CreateLoopSource("LowOffInterior", generatedRoot, interiorLowOffClip);
            if (midOffInteriorSource == null) midOffInteriorSource = CreateLoopSource("MidOffInterior", generatedRoot, interiorMidOffClip);
            if (highOffInteriorSource == null) highOffInteriorSource = CreateLoopSource("HighOffInterior", generatedRoot, interiorHighOffClip);

            if (turboSpoolSource == null) turboSpoolSource = CreateLoopSource("TurboSpool", generatedRoot, turboSpoolClip);
            if (turboWhistleSource == null) turboWhistleSource = CreateLoopSource("TurboWhistle", generatedRoot, turboWhistleClip);
            if (oneShotSource == null) oneShotSource = CreateOneShotSource("OneShots", generatedRoot);
        }

        private void UpdateTelemetry(float dt)
        {
            RuntimeVehicleStats stats = vehicle.RuntimeStats;
            float idleRpm = stats != null ? stats.IdleRPM : 900f;
            float maxRpm = stats != null ? stats.MaxRPM : 7200f;
            float maxSpeed = stats != null ? Mathf.Max(80f, stats.MaxSpeedKph) : 220f;

            float rawThrottle = ResolveDriveThrottle();
            float rawBrake = ResolveBrake();
            float rawSpeedKph = Mathf.Abs(vehicle.ForwardSpeedKph);
            float physicsRpm = Mathf.Clamp(Mathf.Max(idleRpm, gearbox.CurrentRPM), idleRpm, maxRpm);

            smoothedThrottle = ExpSmoothing(smoothedThrottle, rawThrottle, throttleResponse, dt);
            smoothedBrake = ExpSmoothing(smoothedBrake, rawBrake, brakeResponse, dt);
            smoothedSpeedKph = ExpSmoothing(smoothedSpeedKph, rawSpeedKph, speedResponse, dt);

            float deltaSpeed = vehicle.ForwardSpeedKph - lastForwardSpeedKph;
            float longitudinalAccel = deltaSpeed / Mathf.Max(0.001f, dt);
            lastForwardSpeedKph = vehicle.ForwardSpeedKph;

            float speed01 = Mathf.InverseLerp(0f, maxSpeed, smoothedSpeedKph);
            float wheelRpmTarget = Mathf.Lerp(idleRpm, maxRpm * 0.92f, Mathf.Pow(speed01, 0.86f));
            float accelerationLoad = Mathf.Clamp01(longitudinalAccel * accelToLoadScale);
            float rawLoad = Mathf.Clamp01(Mathf.Max(smoothedThrottle, accelerationLoad) - (smoothedBrake * 0.15f));
            smoothedLoad = ExpSmoothing(smoothedLoad, rawLoad, loadResponse, dt);

            float engineInfluence = Mathf.Lerp(engineRpmInfluenceOnCoast, engineRpmInfluenceOnThrottle, smoothedThrottle);
            float coastWheelInfluence = Mathf.Lerp(wheelRpmBiasOnCoast, 0.25f, smoothedThrottle);
            float coastRpmTarget = Mathf.Lerp(idleRpm, wheelRpmTarget, coastWheelInfluence);
            float blendedTargetRpm = Mathf.Lerp(coastRpmTarget, physicsRpm, engineInfluence);
            blendedTargetRpm = Mathf.Clamp(blendedTargetRpm, idleRpm, maxRpm);

            float rpmResponse = blendedTargetRpm >= smoothedRpm
                ? rpmRiseResponse
                : Mathf.Lerp(coastRpmFallResponse, rpmFallResponse, Mathf.Clamp01(smoothedThrottle + (smoothedBrake * 0.4f)));

            smoothedRpm = ExpSmoothing(smoothedRpm, blendedTargetRpm, rpmResponse, dt);
        }

        private void UpdateTurbo(float dt)
        {
            float rpm01 = GetRpm01();
            float desiredSpool = turboAmount * smoothedLoad * Mathf.SmoothStep(0f, 1f, rpm01);
            if (shiftDuckTimer > 0f)
            {
                desiredSpool *= 0.78f;
            }

            float response = desiredSpool >= turboSpool01 ? spoolAttack : spoolRelease;
            turboSpool01 = ExpSmoothing(turboSpool01, desiredSpool, response, dt);
        }

        private void UpdateInteriorBlend(float dt)
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            float targetBlend = 0f;
            if (mainCamera != null && vehicle != null)
            {
                Vector3 localCameraPosition = vehicle.transform.InverseTransformPoint(mainCamera.transform.position);
                bool insideBounds =
                    Mathf.Abs(localCameraPosition.x) <= interiorDetectionExtents.x &&
                    Mathf.Abs(localCameraPosition.y) <= interiorDetectionExtents.y &&
                    Mathf.Abs(localCameraPosition.z) <= interiorDetectionExtents.z;

                targetBlend = insideBounds ? 1f : 0f;
            }

            interiorBlend = Mathf.MoveTowards(interiorBlend, targetBlend, interiorTransitionSpeed * dt);
        }

        private void UpdateCoreBlend()
        {
            float rpm01 = GetRpm01();
            float speed01 = GetSpeed01();
            float roadPresence = Mathf.SmoothStep(0f, 1f, speed01);
            float gasBlend = Mathf.Clamp01(smoothedThrottle);
            float drivePresence = Mathf.Clamp01(Mathf.Max(smoothedLoad, gasBlend * 0.78f));
            float cruiseSupport = roadPresence * gasBlend * 0.35f;
            float onThrottle = Mathf.Clamp01(Mathf.Max(drivePresence, cruiseSupport));
            float coastPresence = Mathf.Clamp01(Mathf.Max(Mathf.SmoothStep(0.12f, 0.92f, rpm01), roadPresence * 0.9f));
            float offThrottleBase = 1f - gasBlend;
            float offThrottle = Mathf.Clamp01(Mathf.Max(offThrottleBase, coastPresence * offThrottleBase) + (smoothedBrake * 0.45f));
            float shiftDuck = shiftDuckTimer > 0f ? 1f - shiftDuckAmount : 1f;
            float exteriorWeight = 1f - interiorBlend;
            float interiorWeight = interiorBlend;

            float highBandSpeedGate = Mathf.SmoothStep(highBandEntrySpeedKph, highBandFullSpeedKph, smoothedSpeedKph);
            float highBandLoadGate = Mathf.SmoothStep(highBandLoadThreshold, 1f, Mathf.Max(smoothedLoad, smoothedThrottle));
            float highBandCoastGate = Mathf.SmoothStep(0.55f, 0.95f, rpm01) * Mathf.Lerp(0.3f, 1f, Mathf.Max(highBandSpeedGate, roadPresence));
            float highBandGate = Mathf.Max(highBandCoastGate, Mathf.Lerp(0.22f, 1f, Mathf.Max(highBandSpeedGate, highBandLoadGate)));

            float idleCurve = EvaluateCurve(engineSound != null ? engineSound.idleVolCurve : null, rpm01, 1f - rpm01);
            float lowCurve = EvaluateCurve(engineSound != null ? engineSound.lowVolCurve : null, rpm01);
            float midCurve = EvaluateCurve(engineSound != null ? engineSound.medVolCurve : null, rpm01);
            float highCurve = EvaluateCurve(engineSound != null ? engineSound.highVolCurve : null, rpm01);
            float maxCurve = EvaluateCurve(engineSound != null ? engineSound.maxRPMVolCurve : null, rpm01);

            float lowBand = lowCurve * Mathf.Lerp(1f, 0.74f, roadPresence);
            float midBand = midCurve * Mathf.Lerp(0.92f, 1f, roadPresence);
            float highBand = highCurve * highBandGate;
            float maxBand = maxCurve * Mathf.Lerp(0.35f, 1f, Mathf.Max(smoothedLoad, highBandSpeedGate));
            float maxCoastBand = maxCurve * Mathf.SmoothStep(0.82f, 1f, rpm01) * Mathf.Lerp(0.18f, 0.52f, coastPresence);

            float idleSpeedFade = 1f - Mathf.SmoothStep(movingIdleFadeStartKph, movingIdleFadeEndKph, smoothedSpeedKph);
            float idleLoadFade = 1f - Mathf.Clamp01(gasBlend * 0.8f);
            float idlePresence = idleCurve * idleSpeedFade * Mathf.Lerp(1f, 0.45f, roadPresence) * idleLoadFade;
            idlePresence = Mathf.Max(idlePresence, idleCurve * coastPresence * 0.12f * offThrottle);

            float idlePitch = EvaluatePitchCurve(engineSound != null ? engineSound.idlePitchCurve : null, rpm01, 1f);
            float lowPitch = EvaluatePitchCurve(engineSound != null ? engineSound.lowPitchCurve : null, rpm01, 1f);
            float midPitch = EvaluatePitchCurve(engineSound != null ? engineSound.medPitchCurve : null, rpm01, 1f);
            float highPitch = EvaluatePitchCurve(engineSound != null ? engineSound.highPitchCurve : null, rpm01, 1f);
            float microPitch = 1f + ((Mathf.PerlinNoise(Time.time * 0.71f, 0f) * 2f) - 1f) * pitchVariationAmount;

            SetLoop(idleExteriorSource, idlePresence * 0.78f * shiftDuck * masterVolume * exteriorWeight, idlePitch * microPitch);
            SetLoop(lowOnExteriorSource, lowBand * onThrottle * 0.94f * shiftDuck * masterVolume * exteriorWeight, lowPitch * microPitch);
            SetLoop(midOnExteriorSource, midBand * onThrottle * 1f * shiftDuck * masterVolume * exteriorWeight, midPitch * microPitch);
            SetLoop(highOnExteriorSource, highBand * onThrottle * 0.92f * shiftDuck * masterVolume * exteriorWeight, highPitch * Mathf.Lerp(0.98f, 1.03f, highBandSpeedGate) * microPitch);
            SetLoop(maxExteriorSource, (maxBand * Mathf.Lerp(0.45f, 1f, onThrottle) + (maxCoastBand * offThrottle)) * shiftDuck * masterVolume * exteriorWeight, highPitch * microPitch);

            SetLoop(lowOffExteriorSource, lowBand * offThrottle * 0.64f * masterVolume * exteriorWeight, lowPitch * microPitch);
            SetLoop(midOffExteriorSource, midBand * offThrottle * 0.74f * masterVolume * exteriorWeight, midPitch * microPitch);
            SetLoop(highOffExteriorSource, highBand * offThrottle * 0.58f * masterVolume * exteriorWeight, highPitch * Mathf.Lerp(0.98f, 1.02f, highBandSpeedGate) * microPitch);

            SetLoop(idleInteriorSource, idlePresence * 0.84f * shiftDuck * masterVolume * interiorWeight, idlePitch);
            SetLoop(lowOnInteriorSource, lowBand * onThrottle * 0.84f * shiftDuck * masterVolume * interiorWeight, lowPitch);
            SetLoop(midOnInteriorSource, midBand * onThrottle * 0.96f * shiftDuck * masterVolume * interiorWeight, midPitch);
            SetLoop(highOnInteriorSource, highBand * onThrottle * 0.82f * shiftDuck * masterVolume * interiorWeight, highPitch * Mathf.Lerp(0.98f, 1.02f, highBandSpeedGate));
            SetLoop(maxInteriorSource, ((maxBand * Mathf.Lerp(0.45f, 1f, onThrottle)) + (maxCoastBand * offThrottle * 0.7f)) * 0.42f * shiftDuck * masterVolume * interiorWeight, highPitch);

            SetLoop(lowOffInteriorSource, lowBand * offThrottle * 0.54f * masterVolume * interiorWeight, lowPitch);
            SetLoop(midOffInteriorSource, midBand * offThrottle * 0.64f * masterVolume * interiorWeight, midPitch);
            SetLoop(highOffInteriorSource, highBand * offThrottle * 0.48f * masterVolume * interiorWeight, highPitch * Mathf.Lerp(0.98f, 1.01f, highBandSpeedGate));
        }

        private void UpdateTurboBlend()
        {
            float rpm01 = GetRpm01();
            float whistleGate = rpm01 >= whistleStartRpm01 && smoothedLoad >= whistleStartLoad ? 1f : 0f;
            float exteriorWeight = 1f - interiorBlend;
            float interiorWeight = interiorBlend;

            SetLoop(turboSpoolSource, turboSpool01 * 0.32f * masterVolume * Mathf.Lerp(exteriorWeight, 0.65f, interiorWeight), Mathf.Lerp(0.88f, 1.22f, rpm01));
            SetLoop(turboWhistleSource, whistleGate * turboSpool01 * 0.18f * masterVolume * Mathf.Lerp(exteriorWeight, 0.55f, interiorWeight), Mathf.Lerp(0.95f, 1.28f, rpm01));
        }

        private void DetectLiftOffEffects(float dt)
        {
            if (shiftCooldownTimer > 0f)
            {
                return;
            }

            float rpm01 = GetRpm01();
            bool throttleDropped = (lastThrottle - smoothedThrottle) >= blowOffThrottleDrop;
            if (throttleDropped && turboSpool01 >= blowOffMinSpool)
            {
                float volume = Mathf.Lerp(0.28f, 0.78f, turboSpool01) * masterVolume;
                PlayOneShot(blowOffClip, volume, 0.98f + Random.Range(-0.03f, 0.04f));
            }

            if (!enableLiftOffCrackles || liftOffCrackleClip == null)
            {
                return;
            }

            bool liftoff = lastThrottle > 0.38f && smoothedThrottle < 0.14f;
            if (!liftoff || rpm01 < crackleMinRpm01)
            {
                return;
            }

            if (Random.value < crackleChancePerSecond * dt)
            {
                PlayOneShot(liftOffCrackleClip, 0.2f * masterVolume, 0.96f + Random.Range(-0.08f, 0.08f));
            }
        }

        private void HandleGearChanged(int previousGear, int currentGear)
        {
            shiftDuckTimer = shiftDuckDuration;
            shiftCooldownTimer = shiftCooldown;

            float pitchMultiplier = currentGear > previousGear ? 1f - shiftPitchDrop : 1f + (shiftPitchDrop * 0.45f);
            if (vehicle != null && vehicle.RuntimeStats != null)
            {
                smoothedRpm = Mathf.Clamp(smoothedRpm * pitchMultiplier, vehicle.RuntimeStats.IdleRPM, vehicle.RuntimeStats.MaxRPM);
            }

            if (currentGear > previousGear)
            {
                PlayOneShot(shiftUpClip, Mathf.Lerp(0.48f, 0.72f, smoothedLoad) * masterVolume, 0.98f + Random.Range(-0.02f, 0.03f));
            }
            else if (currentGear < previousGear)
            {
                PlayOneShot(shiftDownClip, 0.62f * masterVolume, 0.96f + Random.Range(-0.03f, 0.04f));
            }
        }

        private float ResolveDriveThrottle()
        {
            if (input == null)
            {
                return 0f;
            }

            if (vehicle != null && vehicle.IsReversing && input.ReverseHeld)
            {
                return input.Brake;
            }

            return input.Throttle;
        }

        private float ResolveBrake()
        {
            if (input == null)
            {
                return 0f;
            }

            return vehicle != null && vehicle.IsReversing && input.ReverseHeld ? 0f : input.Brake;
        }

        private void RouteAudioSources()
        {
            if (settingsManager == null)
            {
                settingsManager = FindFirstObjectByType<GameSettingsManager>();
            }

            if (settingsManager == null)
            {
                return;
            }

            AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                settingsManager.RouteAudioSource(sources[i], "SFX");
            }
        }

        private AudioSource CreateLoopSource(string sourceName, Transform parent, AudioClip clip)
        {
            if (clip == null)
            {
                return null;
            }

            Transform existing = parent.Find(sourceName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(sourceName);
            go.transform.SetParent(parent, false);

            AudioSource source = go.GetComponent<AudioSource>();
            if (source == null)
            {
                source = go.AddComponent<AudioSource>();
            }

            source.loop = true;
            source.playOnAwake = false;
            source.clip = clip;
            source.volume = 0f;
            source.pitch = 1f;
            source.spatialBlend = spatialBlend;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.reverbZoneMix = 0.6f;
            source.priority = 96;

            if (!source.isPlaying)
            {
                source.Play();
            }

            return source;
        }

        private AudioSource CreateOneShotSource(string sourceName, Transform parent)
        {
            Transform existing = parent.Find(sourceName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(sourceName);
            go.transform.SetParent(parent, false);

            AudioSource source = go.GetComponent<AudioSource>();
            if (source == null)
            {
                source = go.AddComponent<AudioSource>();
            }

            source.loop = false;
            source.playOnAwake = false;
            source.volume = 0f;
            source.pitch = 1f;
            source.spatialBlend = spatialBlend;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.reverbZoneMix = 0.6f;
            source.priority = 92;
            return source;
        }

        private void SetLoop(AudioSource source, float volume, float pitch)
        {
            if (source == null || source.clip == null)
            {
                return;
            }

            source.volume = Mathf.Clamp01(volume);
            source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        }

        private void PlayOneShot(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || oneShotSource == null || volume <= 0f)
            {
                return;
            }

            oneShotSource.pitch = Mathf.Clamp(pitch, 0.75f, 1.35f);
            oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private float GetRpm01()
        {
            if (vehicle == null || vehicle.RuntimeStats == null)
            {
                return 0f;
            }

            return Mathf.InverseLerp(vehicle.RuntimeStats.IdleRPM, vehicle.RuntimeStats.MaxRPM, smoothedRpm);
        }

        private float GetSpeed01()
        {
            if (vehicle == null || vehicle.RuntimeStats == null)
            {
                return 0f;
            }

            float maxSpeed = Mathf.Max(1f, vehicle.RuntimeStats.MaxSpeedKph);
            return Mathf.Clamp01(smoothedSpeedKph / maxSpeed);
        }

        private static float EvaluateCurve(AnimationCurve curve, float x, float fallback = 0f)
        {
            if (curve == null || curve.length == 0)
            {
                return Mathf.Max(0f, fallback);
            }

            return Mathf.Max(0f, curve.Evaluate(Mathf.Clamp01(x)));
        }

        private static float EvaluatePitchCurve(AnimationCurve curve, float x, float fallback = 1f)
        {
            if (curve == null || curve.length == 0)
            {
                return Mathf.Max(0.1f, fallback);
            }

            return Mathf.Max(0.1f, curve.Evaluate(Mathf.Clamp01(x)));
        }

        private static float ExpSmoothing(float current, float target, float response, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-Mathf.Max(0.01f, response) * dt));
        }

        private static float TriBand(float x, float a, float b, float c)
        {
            if (x <= a || x >= c)
            {
                return 0f;
            }

            if (Mathf.Approximately(x, b))
            {
                return 1f;
            }

            return x < b ? Mathf.InverseLerp(a, b, x) : 1f - Mathf.InverseLerp(b, c, x);
        }
    }
}

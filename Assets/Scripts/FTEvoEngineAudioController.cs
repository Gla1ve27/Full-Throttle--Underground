
using UnityEngine;

namespace FullThrottle.Audio
{
    /// <summary>
    /// Evo-inspired turbo I4 prototype audio controller.
    /// Uses layered loops + one-shots:
    /// - engine start
    /// - idle / low / mid / high on-throttle loops
    /// - decel loop
    /// - turbo spool loop
    /// - turbo whistle loop
    /// - shift up one-shot
    /// - shift down rev-blip one-shot
    /// - blow-off valve one-shot
    ///
    /// Works in:
    /// 1) Auto mode: estimates state from WheelCollider + Rigidbody + basic input
    /// 2) External mode: feed real drivetrain data from your vehicle controller
    /// </summary>
    [DisallowMultipleComponent]
    public class FTEvoEngineAudioController : MonoBehaviour
    {
        [Header("Clips")]
        public AudioClip engineStart;
        public AudioClip idleLoop;
        public AudioClip lowOnLoop;
        public AudioClip midOnLoop;
        public AudioClip highOnLoop;
        public AudioClip decelLoop;
        public AudioClip turboSpoolLoop;
        public AudioClip turboWhistleLoop;
        public AudioClip shiftUpClip;
        public AudioClip shiftDownBlipClip;
        public AudioClip blowOffClip;

        [Header("Vehicle Detection")]
        public Rigidbody vehicleRigidbody;
        public WheelCollider[] drivenWheels;
        public bool autoFindRigidbody = true;
        public bool autoFindWheels = true;
        public bool useExternalInputs = false;

        [Header("Engine Range")]
        public float idleRPM = 900f;
        public float maxRPM = 7800f;
        public float rpmSmoothing = 8f;
        public float throttleSmoothing = 9f;
        public float brakeSmoothing = 12f;

        [Header("3D Audio")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float spatialBlend = 1f;
        public float minDistance = 6f;
        public float maxDistance = 70f;

        [Header("Pitch")]
        public float idlePitchMin = 0.90f;
        public float idlePitchMax = 1.08f;
        public float enginePitchMin = 0.86f;
        public float enginePitchMax = 1.30f;
        public float decelPitchDrop = 0.10f;
        public float turboPitchMin = 0.86f;
        public float turboPitchMax = 1.36f;

        [Header("Turbo / Shift Behaviour")]
        public float boostBuildSpeed = 6f;
        public float boostFallSpeed = 10f;
        [Range(0f, 1f)] public float bovThrottleDropThreshold = 0.30f;
        public float shiftHeuristicDropRPM = 850f;
        public float shiftCooldown = 0.22f;
        public float shiftDuckTime = 0.09f;
        public float brakeAudibleThreshold = 0.08f;

        [Header("Optional External Gear Input")]
        public bool useExternalGearIndex = false;

        [Header("Debug")]
        public bool showDebug = false;
        public float debugRPM;
        [Range(0f, 1f)] public float debugThrottle;
        [Range(0f, 1f)] public float debugBrake;
        public float debugSpeedKph;
        [Range(0f, 1f)] public float debugBoost;
        public bool debugGrounded = true;

        private AudioSource _startSource;
        private AudioSource _idleSource;
        private AudioSource _lowSource;
        private AudioSource _midSource;
        private AudioSource _highSource;
        private AudioSource _decelSource;
        private AudioSource _turboSource;
        private AudioSource _whistleSource;
        private AudioSource _shiftUpSource;
        private AudioSource _shiftDownSource;
        private AudioSource _bovSource;

        private float _smoothedRPM;
        private float _smoothedThrottle;
        private float _smoothedBrake;
        private float _smoothedSpeedKph;
        private bool _isGrounded = true;
        private bool _engineStarted;

        private float _externalRPM;
        private float _externalThrottle;
        private float _externalBrake;
        private float _externalSpeedKph;
        private bool _externalGrounded = true;
        private int _externalGearIndex = -1;

        private float _lastRPM;
        private float _lastThrottle;
        private int _lastGearIndex = -1;
        private float _shiftCooldownTimer;
        private float _shiftDuckTimer;
        private float _boost;

        public float CurrentRPM => _smoothedRPM;
        public float CurrentBoost01 => _boost;

        private void Awake()
        {
            if (autoFindRigidbody && vehicleRigidbody == null)
                vehicleRigidbody = GetComponentInParent<Rigidbody>();

            if (autoFindWheels && (drivenWheels == null || drivenWheels.Length == 0))
                drivenWheels = GetComponentsInChildren<WheelCollider>(true);

            CreateSources();
        }

        private void Start()
        {
            StartEngine();
        }

        private void CreateSources()
        {
            _startSource = CreateChildSource("EngineStart", false);
            _idleSource = CreateChildSource("IdleLoop", true);
            _lowSource = CreateChildSource("LowOnLoop", true);
            _midSource = CreateChildSource("MidOnLoop", true);
            _highSource = CreateChildSource("HighOnLoop", true);
            _decelSource = CreateChildSource("DecelLoop", true);
            _turboSource = CreateChildSource("TurboSpoolLoop", true);
            _whistleSource = CreateChildSource("TurboWhistleLoop", true);
            _shiftUpSource = CreateChildSource("ShiftUp", false);
            _shiftDownSource = CreateChildSource("ShiftDownBlip", false);
            _bovSource = CreateChildSource("BlowOffValve", false);

            _startSource.clip = engineStart;
            _idleSource.clip = idleLoop;
            _lowSource.clip = lowOnLoop;
            _midSource.clip = midOnLoop;
            _highSource.clip = highOnLoop;
            _decelSource.clip = decelLoop;
            _turboSource.clip = turboSpoolLoop;
            _whistleSource.clip = turboWhistleLoop;
            _shiftUpSource.clip = shiftUpClip;
            _shiftDownSource.clip = shiftDownBlipClip;
            _bovSource.clip = blowOffClip;
        }

        private AudioSource CreateChildSource(string childName, bool loop)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);

            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = spatialBlend;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            return source;
        }

        public void StartEngine()
        {
            if (_engineStarted) return;
            _engineStarted = true;

            if (_startSource.clip != null)
                _startSource.Play();

            Invoke(nameof(BeginLoops), 0.12f);
        }

        public void StopEngine()
        {
            _engineStarted = false;
            CancelInvoke(nameof(BeginLoops));

            StopSource(_startSource);
            StopSource(_idleSource);
            StopSource(_lowSource);
            StopSource(_midSource);
            StopSource(_highSource);
            StopSource(_decelSource);
            StopSource(_turboSource);
            StopSource(_whistleSource);
            StopSource(_shiftUpSource);
            StopSource(_shiftDownSource);
            StopSource(_bovSource);
        }

        private void StopSource(AudioSource src)
        {
            if (src != null)
                src.Stop();
        }

        public void SetExternalInputs(float rpm, float throttle01, float brake01, float speedKph, bool grounded = true, int gearIndex = -1)
        {
            useExternalInputs = true;
            _externalRPM = Mathf.Clamp(rpm, idleRPM, maxRPM);
            _externalThrottle = Mathf.Clamp01(throttle01);
            _externalBrake = Mathf.Clamp01(brake01);
            _externalSpeedKph = Mathf.Max(0f, speedKph);
            _externalGrounded = grounded;
            _externalGearIndex = gearIndex;
        }

        private void BeginLoops()
        {
            PlayLoop(_idleSource);
            PlayLoop(_lowSource);
            PlayLoop(_midSource);
            PlayLoop(_highSource);
            PlayLoop(_decelSource);
            PlayLoop(_turboSource);
            PlayLoop(_whistleSource);
        }

        private void PlayLoop(AudioSource src)
        {
            if (src != null && src.clip != null && !src.isPlaying)
                src.Play();
        }

        private void Update()
        {
            PullVehicleState();
            UpdateBoost(Time.deltaTime);
            DetectTransients(Time.deltaTime);
            UpdateMix(Time.deltaTime);
        }

        private void PullVehicleState()
        {
            float targetRPM;
            float targetThrottle;
            float targetBrake;
            float targetSpeedKph;
            bool targetGrounded;
            int targetGear = -1;

            if (useExternalInputs)
            {
                targetRPM = _externalRPM;
                targetThrottle = _externalThrottle;
                targetBrake = _externalBrake;
                targetSpeedKph = _externalSpeedKph;
                targetGrounded = _externalGrounded;
                targetGear = _externalGearIndex;
            }
            else
            {
                targetSpeedKph = vehicleRigidbody != null ? vehicleRigidbody.linearVelocity.magnitude * 3.6f : 0f;
                targetGrounded = false;

                float averageAbsWheelRPM = 0f;
                int validWheels = 0;

                if (drivenWheels != null)
                {
                    foreach (var wheel in drivenWheels)
                    {
                        if (wheel == null) continue;
                        averageAbsWheelRPM += Mathf.Abs(wheel.rpm);
                        validWheels++;
                        if (wheel.isGrounded)
                            targetGrounded = true;
                    }
                }

                averageAbsWheelRPM = validWheels > 0 ? averageAbsWheelRPM / validWheels : 0f;

                float wheelBasedRPM = Mathf.Lerp(idleRPM, maxRPM, Mathf.InverseLerp(0f, 1200f, averageAbsWheelRPM));
                float speedBasedRPM = Mathf.Lerp(idleRPM, maxRPM * 0.88f, Mathf.InverseLerp(0f, 280f, targetSpeedKph));
                targetRPM = Mathf.Max(idleRPM, Mathf.Lerp(wheelBasedRPM, speedBasedRPM, targetGrounded ? 0.28f : 0.72f));

                float rawVertical = Input.GetAxisRaw("Vertical");
                targetThrottle = Mathf.Clamp01(Mathf.Max(0f, rawVertical));
                targetBrake = Mathf.Clamp01(Mathf.Max(0f, -rawVertical));

                if (Input.GetKey(KeyCode.Space))
                    targetBrake = Mathf.Max(targetBrake, 0.7f);
            }

            _smoothedRPM = Mathf.Lerp(_smoothedRPM <= 0f ? idleRPM : _smoothedRPM, targetRPM, Time.deltaTime * rpmSmoothing);
            _smoothedThrottle = Mathf.Lerp(_smoothedThrottle, targetThrottle, Time.deltaTime * throttleSmoothing);
            _smoothedBrake = Mathf.Lerp(_smoothedBrake, targetBrake, Time.deltaTime * brakeSmoothing);
            _smoothedSpeedKph = Mathf.Lerp(_smoothedSpeedKph, targetSpeedKph, Time.deltaTime * 10f);
            _isGrounded = targetGrounded;

            if (useExternalGearIndex)
                _externalGearIndex = targetGear;

            debugRPM = _smoothedRPM;
            debugThrottle = _smoothedThrottle;
            debugBrake = _smoothedBrake;
            debugSpeedKph = _smoothedSpeedKph;
            debugGrounded = _isGrounded;
        }

        private void UpdateBoost(float dt)
        {
            float rpm01 = Mathf.InverseLerp(idleRPM, maxRPM, _smoothedRPM);
            float targetBoost =
                Mathf.SmoothStep(0f, 1f, _smoothedThrottle) *
                Mathf.SmoothStep(0.15f, 1f, rpm01) *
                (1f - _smoothedBrake * 0.35f);

            float speed = targetBoost > _boost ? boostBuildSpeed : boostFallSpeed;
            _boost = Mathf.Lerp(_boost, targetBoost, dt * speed);
            debugBoost = _boost;
        }

        private void DetectTransients(float dt)
        {
            if (_shiftCooldownTimer > 0f)
                _shiftCooldownTimer -= dt;

            if (_shiftDuckTimer > 0f)
                _shiftDuckTimer -= dt;

            float rpmDrop = _lastRPM - _smoothedRPM;
            float throttleDrop = _lastThrottle - _smoothedThrottle;
            bool canTrigger = _shiftCooldownTimer <= 0f;

            bool externalShiftUp = false;
            bool externalShiftDown = false;

            if (useExternalInputs && useExternalGearIndex && _externalGearIndex >= 0)
            {
                if (_lastGearIndex >= 0 && _externalGearIndex != _lastGearIndex)
                {
                    externalShiftUp = _externalGearIndex > _lastGearIndex;
                    externalShiftDown = _externalGearIndex < _lastGearIndex;
                }
                _lastGearIndex = _externalGearIndex;
            }

            bool heuristicShiftUp =
                canTrigger &&
                !externalShiftDown &&
                !externalShiftUp &&
                _smoothedThrottle > 0.45f &&
                _smoothedSpeedKph > 18f &&
                rpmDrop > shiftHeuristicDropRPM;

            bool shouldShiftUp = externalShiftUp || heuristicShiftUp;

            if (shouldShiftUp)
            {
                PlayOneShot(_shiftUpSource, Mathf.Lerp(0.42f, 0.70f, _boost), Mathf.Lerp(0.95f, 1.10f, Mathf.InverseLerp(idleRPM, maxRPM, _smoothedRPM)));
                _shiftCooldownTimer = shiftCooldown;
                _shiftDuckTimer = shiftDuckTime;
            }

            bool shouldBov =
                canTrigger &&
                throttleDrop > bovThrottleDropThreshold &&
                _boost > 0.32f &&
                _smoothedRPM > idleRPM + 1000f;

            if (shouldBov)
            {
                PlayOneShot(_bovSource, Mathf.Lerp(0.30f, 0.75f, _boost), Mathf.Lerp(0.94f, 1.08f, _boost));
                _shiftCooldownTimer = Mathf.Max(_shiftCooldownTimer, 0.12f);
            }

            bool heuristicShiftDown =
                canTrigger &&
                !externalShiftUp &&
                (_smoothedBrake > 0.16f || externalShiftDown) &&
                _smoothedThrottle < 0.20f &&
                (_smoothedRPM - _lastRPM) > 220f &&
                _smoothedSpeedKph > 12f;

            if (heuristicShiftDown || externalShiftDown)
            {
                PlayOneShot(_shiftDownSource, 0.42f, Mathf.Lerp(0.94f, 1.10f, Mathf.InverseLerp(idleRPM, maxRPM, _smoothedRPM)));
                _shiftCooldownTimer = shiftCooldown;
            }

            _lastRPM = _smoothedRPM;
            _lastThrottle = _smoothedThrottle;
        }

        private void PlayOneShot(AudioSource src, float volume, float pitch)
        {
            if (src == null || src.clip == null)
                return;

            src.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            src.volume = Mathf.Clamp01(volume * masterVolume);
            src.Play();
        }

        private void UpdateMix(float dt)
        {
            if (!_engineStarted)
                return;

            float rpm01 = Mathf.InverseLerp(idleRPM, maxRPM, _smoothedRPM);
            float onThrottle = Mathf.Clamp01(_smoothedThrottle * (1f - _smoothedBrake * 0.25f));
            float offThrottle = Mathf.Clamp01((1f - _smoothedThrottle) * Mathf.Max(_smoothedBrake, 0.45f));
            float shiftDuck = _shiftDuckTimer > 0f ? 0.58f : 1f;
            float groundedGain = _isGrounded ? 1f : 0.88f;
            float baseGain = masterVolume * groundedGain;

            float idleW = 1f - Mathf.SmoothStep(0.05f, 0.28f, rpm01);
            float lowW = Band(rpm01, 0.08f, 0.24f, 0.46f);
            float midW = Band(rpm01, 0.28f, 0.50f, 0.76f);
            float highW = Mathf.SmoothStep(0.56f, 0.94f, rpm01);

            float enginePitch = Mathf.Lerp(enginePitchMin, enginePitchMax, rpm01);
            float idlePitch = Mathf.Lerp(idlePitchMin, idlePitchMax, rpm01 * 0.65f);
            float turboPitch = Mathf.Lerp(turboPitchMin, turboPitchMax, _boost * 0.65f + rpm01 * 0.35f);

            SetLoop(_idleSource,
                idleW * Mathf.Lerp(0.92f, 0.52f, onThrottle) * baseGain,
                idlePitch);

            SetLoop(_lowSource,
                lowW * Mathf.Lerp(0.28f, 0.98f, onThrottle) * baseGain * shiftDuck,
                enginePitch * 0.96f);

            SetLoop(_midSource,
                midW * Mathf.Lerp(0.24f, 1.00f, onThrottle) * baseGain * shiftDuck,
                enginePitch * 1.02f);

            SetLoop(_highSource,
                highW * Mathf.Lerp(0.18f, 1.00f, onThrottle) * baseGain * shiftDuck,
                enginePitch * 1.08f);

            SetLoop(_decelSource,
                offThrottle * Mathf.SmoothStep(0.16f, 0.90f, rpm01) * baseGain * 0.82f,
                Mathf.Max(0.75f, enginePitch - decelPitchDrop));

            float turboVol = _boost * Mathf.Lerp(0.12f, 0.60f, rpm01) * baseGain;
            float whistleVol = _boost * Mathf.SmoothStep(0.35f, 1f, rpm01) * baseGain * 0.72f;

            SetLoop(_turboSource, turboVol, turboPitch);
            SetLoop(_whistleSource, whistleVol, turboPitch * 1.08f);
        }

        private void SetLoop(AudioSource src, float volume, float pitch)
        {
            if (src == null)
                return;

            src.spatialBlend = spatialBlend;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        }

        private float Band(float x, float inMin, float peak, float outMax)
        {
            if (x <= inMin || x >= outMax)
                return 0f;

            if (x < peak)
                return Mathf.InverseLerp(inMin, peak, x);

            return 1f - Mathf.InverseLerp(peak, outMax, x);
        }

        private void OnValidate()
        {
            maxRPM = Mathf.Max(maxRPM, idleRPM + 1000f);
            masterVolume = Mathf.Clamp01(masterVolume);
            spatialBlend = Mathf.Clamp01(spatialBlend);
            minDistance = Mathf.Max(0.1f, minDistance);
            maxDistance = Mathf.Max(minDistance + 0.1f, maxDistance);
            boostBuildSpeed = Mathf.Max(0.1f, boostBuildSpeed);
            boostFallSpeed = Mathf.Max(0.1f, boostFallSpeed);
            shiftCooldown = Mathf.Max(0.01f, shiftCooldown);
            shiftDuckTime = Mathf.Max(0.01f, shiftDuckTime);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebug)
                return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, minDistance);

            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, maxDistance);
        }
    }
}

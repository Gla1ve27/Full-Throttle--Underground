
using UnityEngine;

namespace FullThrottle.Audio
{
    /// <summary>
    /// Drop this on the vehicle root. It creates and manages engine AudioSources
    /// for startup, idle, low, mid, high, decel, and rev-blip layers.
    ///
    /// Works in two modes:
    /// 1) Auto mode: estimates RPM from WheelCollider RPM + Rigidbody speed.
    /// 2) External mode: another script calls SetExternalInputs(rpm, throttle, brake, speedKph, grounded).
    ///
    /// Goal:
    /// - believable placeholder engine audio now
    /// - easy to replace clips later without changing logic
    /// </summary>
    [DisallowMultipleComponent]
    public class FT_EngineAudioController : MonoBehaviour
    {
        [Header("Audio Clips")]
        public AudioClip engineStart;
        public AudioClip idleLoop;
        public AudioClip lowLoop;
        public AudioClip midLoop;
        public AudioClip highLoop;
        public AudioClip decelLoop;
        public AudioClip revBlip;

        [Header("Vehicle Detection")]
        public Rigidbody vehicleRigidbody;
        public WheelCollider[] drivenWheels;
        public bool autoFindRigidbody = true;
        public bool autoFindWheels = true;
        public bool useExternalInputs = false;

        [Header("RPM Range")]
        public float idleRPM = 850f;
        public float maxRPM = 7600f;
        public float rpmSmoothing = 7f;

        [Header("Mixing")]
        [Range(0f, 1f)] public float masterVolume = 0.9f;
        [Range(0f, 1f)] public float spatialBlend = 1f;
        public float minDistance = 6f;
        public float maxDistance = 65f;

        [Header("Pitch")]
        public float idlePitchMin = 0.85f;
        public float idlePitchMax = 1.08f;
        public float bandPitchMin = 0.82f;
        public float bandPitchMax = 1.25f;
        public float offThrottlePitchDrop = 0.08f;

        [Header("Behavior")]
        public float onThrottleResponse = 8f;
        public float offThrottleResponse = 6f;
        public float startupDelay = 0.10f;
        public float revBlipMinThrottleDelta = 0.42f;
        public float decelBrakeThreshold = 0.08f;
        public float speedAudibilityKph = 4f;

        [Header("Debug")]
        public bool showDebug = false;
        public float debugRPM;
        [Range(0f,1f)] public float debugThrottle;
        [Range(0f,1f)] public float debugBrake;
        public float debugSpeedKph;
        public bool debugGrounded = true;

        private AudioSource _startSource;
        private AudioSource _idleSource;
        private AudioSource _lowSource;
        private AudioSource _midSource;
        private AudioSource _highSource;
        private AudioSource _decelSource;
        private AudioSource _blipSource;

        private float _smoothedRPM;
        private float _smoothedThrottle;
        private float _smoothedBrake;
        private float _speedKph;
        private bool _isGrounded = true;
        private bool _engineStarted;
        private bool _externalGrounded = true;
        private float _externalRPM;
        private float _externalThrottle;
        private float _externalBrake;
        private float _externalSpeedKph;
        private float _lastThrottle;
        private float _lastAutoRPM;
        private float _gearJoltTimer;

        public float CurrentRPM => _smoothedRPM;
        public float NormalizedRPM => Mathf.InverseLerp(idleRPM, maxRPM, _smoothedRPM);

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
            _idleSource = CreateChildSource("EngineIdle", true);
            _lowSource = CreateChildSource("EngineLow", true);
            _midSource = CreateChildSource("EngineMid", true);
            _highSource = CreateChildSource("EngineHigh", true);
            _decelSource = CreateChildSource("EngineDecel", true);
            _blipSource = CreateChildSource("EngineRevBlip", false);

            _startSource.clip = engineStart;
            _idleSource.clip = idleLoop;
            _lowSource.clip = lowLoop;
            _midSource.clip = midLoop;
            _highSource.clip = highLoop;
            _decelSource.clip = decelLoop;
            _blipSource.clip = revBlip;
        }

        private AudioSource CreateChildSource(string childName, bool loop)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);

            var src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = spatialBlend;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.dopplerLevel = 0f;
            src.volume = 0f;
            return src;
        }

        public void StartEngine()
        {
            if (_engineStarted) return;
            _engineStarted = true;

            if (_startSource.clip != null)
                _startSource.Play();

            Invoke(nameof(BeginLoops), startupDelay);
        }

        public void StopEngine()
        {
            _engineStarted = false;
            CancelInvoke(nameof(BeginLoops));

            _startSource.Stop();
            _idleSource.Stop();
            _lowSource.Stop();
            _midSource.Stop();
            _highSource.Stop();
            _decelSource.Stop();
            _blipSource.Stop();
        }

        public void SetExternalInputs(float rpm, float throttle01, float brake01, float speedKph, bool grounded = true)
        {
            _externalRPM = Mathf.Clamp(rpm, idleRPM, maxRPM);
            _externalThrottle = Mathf.Clamp01(throttle01);
            _externalBrake = Mathf.Clamp01(brake01);
            _externalSpeedKph = Mathf.Max(0f, speedKph);
            _externalGrounded = grounded;
            useExternalInputs = true;
        }

        private void BeginLoops()
        {
            PlayLoopIfValid(_idleSource);
            PlayLoopIfValid(_lowSource);
            PlayLoopIfValid(_midSource);
            PlayLoopIfValid(_highSource);
            PlayLoopIfValid(_decelSource);
        }

        private void PlayLoopIfValid(AudioSource src)
        {
            if (src != null && src.clip != null && !src.isPlaying)
                src.Play();
        }

        private void Update()
        {
            PullVehicleState();
            UpdateMix(Time.deltaTime);
        }

        private void PullVehicleState()
        {
            float targetRPM;
            float targetThrottle;
            float targetBrake;
            float targetSpeed;
            bool targetGrounded;

            if (useExternalInputs)
            {
                targetRPM = _externalRPM;
                targetThrottle = _externalThrottle;
                targetBrake = _externalBrake;
                targetSpeed = _externalSpeedKph;
                targetGrounded = _externalGrounded;
            }
            else
            {
                targetSpeed = vehicleRigidbody != null ? vehicleRigidbody.linearVelocity.magnitude * 3.6f : 0f;
                targetGrounded = false;

                float avgAbsWheelRPM = 0f;
                int wheelCount = 0;

                if (drivenWheels != null)
                {
                    foreach (var wheel in drivenWheels)
                    {
                        if (wheel == null) continue;
                        avgAbsWheelRPM += Mathf.Abs(wheel.rpm);
                        wheelCount++;
                        if (wheel.isGrounded) targetGrounded = true;
                    }
                }

                avgAbsWheelRPM = wheelCount > 0 ? avgAbsWheelRPM / wheelCount : 0f;

                // Estimate engine RPM from wheel RPM, then blend in speed to keep audio alive during slip/airtime.
                float wheelBasedRPM = Mathf.Lerp(idleRPM, maxRPM, Mathf.InverseLerp(0f, 1100f, avgAbsWheelRPM));
                float speedBasedRPM = Mathf.Lerp(idleRPM, maxRPM * 0.88f, Mathf.InverseLerp(0f, 260f, targetSpeed));
                targetRPM = Mathf.Max(idleRPM, Mathf.Lerp(wheelBasedRPM, speedBasedRPM, targetGrounded ? 0.25f : 0.7f));

                // Try to infer throttle/brake from RPM trend and basic keyboard input as fallback.
                float rpmDelta = targetRPM - _lastAutoRPM;
                float vAxis = Mathf.Abs(Input.GetAxisRaw("Vertical"));
                targetThrottle = Mathf.Clamp01(Mathf.Max(vAxis, rpmDelta > 15f ? 0.7f : 0f));
                targetBrake = Mathf.Clamp01(Input.GetKey(KeyCode.Space) || rpmDelta < -25f ? 0.5f : 0f);

                _lastAutoRPM = targetRPM;
            }

            float throttleLerp = _smoothedThrottle < targetThrottle ? onThrottleResponse : offThrottleResponse;
            _smoothedThrottle = Mathf.Lerp(_smoothedThrottle, targetThrottle, Time.deltaTime * throttleLerp);
            _smoothedBrake = Mathf.Lerp(_smoothedBrake, targetBrake, Time.deltaTime * 10f);
            _smoothedRPM = Mathf.Lerp(_smoothedRPM <= 0f ? idleRPM : _smoothedRPM, targetRPM, Time.deltaTime * rpmSmoothing);
            _speedKph = Mathf.Lerp(_speedKph, targetSpeed, Time.deltaTime * 8f);
            _isGrounded = targetGrounded;

            debugRPM = _smoothedRPM;
            debugThrottle = _smoothedThrottle;
            debugBrake = _smoothedBrake;
            debugSpeedKph = _speedKph;
            debugGrounded = _isGrounded;
        }

        private void UpdateMix(float dt)
        {
            if (!_engineStarted) return;

            float rpm01 = Mathf.InverseLerp(idleRPM, maxRPM, _smoothedRPM);
            float audible = _speedKph > speedAudibilityKph || _smoothedThrottle > 0.05f ? 1f : 0.75f;

            // RPM band weights
            float idleW = 1f - Mathf.SmoothStep(0.05f, 0.28f, rpm01);
            float lowW = Band(rpm01, 0.08f, 0.25f, 0.48f);
            float midW = Band(rpm01, 0.30f, 0.52f, 0.78f);
            float highW = Mathf.SmoothStep(0.58f, 0.95f, rpm01);

            // Throttle / off-throttle behavior
            float onThrottle = Mathf.Clamp01(_smoothedThrottle * (1f - _smoothedBrake * 0.4f));
            float offThrottle = Mathf.Clamp01((1f - _smoothedThrottle) * Mathf.Max(_smoothedBrake, 0.45f));

            float baseGain = masterVolume * audible * (_isGrounded ? 1f : 0.82f);

            float idlePitch = Mathf.Lerp(idlePitchMin, idlePitchMax, rpm01 * 0.7f);
            float bandPitch = Mathf.Lerp(bandPitchMin, bandPitchMax, rpm01);

            SetSource(_idleSource, idleW * (0.75f + (1f - onThrottle) * 0.2f) * baseGain, idlePitch - _smoothedBrake * 0.03f);
            SetSource(_lowSource, lowW * Mathf.Lerp(0.4f, 1.0f, onThrottle) * baseGain, bandPitch);
            SetSource(_midSource, midW * Mathf.Lerp(0.28f, 1.0f, onThrottle) * baseGain, bandPitch + 0.04f);
            SetSource(_highSource, highW * Mathf.Lerp(0.22f, 1.0f, onThrottle) * baseGain, bandPitch + 0.08f);
            SetSource(_decelSource,
                offThrottle * Mathf.SmoothStep(0.18f, 0.92f, rpm01) * baseGain * 0.75f,
                Mathf.Max(0.7f, bandPitch - offThrottlePitchDrop));

            // Small artificial "shift jolt" when RPM drops suddenly during throttle.
            float rpmVelocity = Mathf.Abs(Mathf.DeltaAngle(_smoothedRPM, _smoothedRPM)); // placeholder noop, kept for clarity
            if ((_lastThrottle - _smoothedThrottle) > revBlipMinThrottleDelta && _blipSource.clip != null)
            {
                if (!_blipSource.isPlaying && _smoothedRPM > idleRPM + 800f)
                {
                    _blipSource.pitch = Mathf.Lerp(0.95f, 1.18f, rpm01);
                    _blipSource.volume = baseGain * 0.55f;
                    _blipSource.Play();
                }
            }

            _lastThrottle = _smoothedThrottle;
        }

        private void SetSource(AudioSource src, float volume, float pitch)
        {
            if (src == null) return;
            src.spatialBlend = spatialBlend;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        }

        private float Band(float x, float inMin, float mid, float outMax)
        {
            if (x <= inMin || x >= outMax) return 0f;
            if (x < mid) return Mathf.InverseLerp(inMin, mid, x);
            return 1f - Mathf.InverseLerp(mid, outMax, x);
        }

        private void OnValidate()
        {
            maxRPM = Mathf.Max(maxRPM, idleRPM + 1000f);
            masterVolume = Mathf.Clamp01(masterVolume);
            spatialBlend = Mathf.Clamp01(spatialBlend);
            minDistance = Mathf.Max(0.1f, minDistance);
            maxDistance = Mathf.Max(minDistance + 0.1f, maxDistance);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebug) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, minDistance);

            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, maxDistance);
        }
    }
}

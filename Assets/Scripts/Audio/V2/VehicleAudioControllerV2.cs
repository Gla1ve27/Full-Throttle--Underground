using UnityEngine;
using Underground.Vehicle.V2;

namespace Underground.Audio.V2
{
    /// <summary>
    /// Top-level V2 audio orchestrator. Binds to <see cref="VehicleControllerV2"/>
    /// via its <see cref="VehicleState"/>, initializes all audio sub-players,
    /// and drives the per-frame update pipeline.
    /// </summary>
    public enum CarAudioCameraMode { Exterior, Interior, Cinematic }

    [DisallowMultipleComponent]
    [AddComponentMenu("Full Throttle/Audio/V2 Vehicle Audio Controller")]
    public sealed class VehicleAudioControllerV2 : MonoBehaviour
    {
        [Header("V2 Controller")]
        [SerializeField] private VehicleControllerV2 vehicleController;

        [Header("Audio Bank")]
        [SerializeField] private NFSU2CarAudioBank audioBank;
        [SerializeField] private VehicleAudioTier audioTier = VehicleAudioTier.Stock;

        [Header("Sub-Players")]
        [SerializeField] private EngineAudioStateFeed stateFeed;
        [SerializeField] private EngineLayerPlayer engineLayers;
        [SerializeField] private EngineTransientPlayer transients;
        [SerializeField] private VehicleAuxAudioPlayer auxPlayer;
        [SerializeField] private VehicleSurfaceAudioPlayer surfacePlayer;

        [Header("Camera")]
        [SerializeField] private CarAudioCameraMode cameraMode = CarAudioCameraMode.Exterior;

        [Header("Debug")]
        [SerializeField] private bool logInit = true;
        [SerializeField] private int maxInitializeRetries = 30;
        [SerializeField] private float initializeRetryDelay = 0.1f;

        private Transform sourceRoot;
        private bool initialized;
        private int initializeRetryCount;

        private void Start()
        {
            ResolveReferences();
            Initialize();
        }

        private void Update()
        {
            if (!initialized) return;

            float dt = Time.deltaTime;
            stateFeed?.UpdateFeed(dt);
            engineLayers?.UpdateLayers(stateFeed, dt);
            transients?.UpdateTransients(stateFeed, dt);
            auxPlayer?.UpdateAux(stateFeed, dt);
            surfacePlayer?.UpdateSurface(stateFeed, dt);
        }

        public void SetCameraMode(CarAudioCameraMode mode)
        {
            cameraMode = mode;
        }

        public void ApplyAudioBank(NFSU2CarAudioBank bank, int tier = 0)
        {
            audioBank = bank;
            audioTier = (VehicleAudioTier)tier;
            if (!initialized) return;
            ApplyBankToPlayers();
        }

        private void Initialize()
        {
            if (vehicleController == null || !vehicleController.IsInitialized)
            {
                ScheduleRetry();
                return;
            }

            CompleteInitialization();
        }

        private void RetryInitialize()
        {
            if (initialized) return;
            ResolveReferences();

            if (vehicleController == null || !vehicleController.IsInitialized)
            {
                ScheduleRetry();
                return;
            }

            CompleteInitialization();
        }

        private void ScheduleRetry()
        {
            initializeRetryCount++;
            if (initializeRetryCount > maxInitializeRetries)
            {
                if (logInit) Debug.LogWarning("[AudioV2] VehicleControllerV2 not initialized after retries. Audio disabled.");
                return;
            }

            Invoke(nameof(RetryInitialize), initializeRetryDelay);
        }

        private void CompleteInitialization()
        {
            EnsureSourceRoot();

            if (stateFeed == null) stateFeed = GetComponent<EngineAudioStateFeed>();
            if (stateFeed == null) stateFeed = gameObject.AddComponent<EngineAudioStateFeed>();

            var stats = vehicleController.RuntimeStats;
            float idle = stats?.IdleRPM ?? 900f;
            float max = stats?.MaxRPM ?? 7800f;
            stateFeed.Bind(vehicleController.State, idle, max);

            if (engineLayers == null) engineLayers = GetComponent<EngineLayerPlayer>();
            if (engineLayers == null) engineLayers = gameObject.AddComponent<EngineLayerPlayer>();
            engineLayers.Initialize(sourceRoot);

            if (transients == null) transients = GetComponent<EngineTransientPlayer>();
            if (transients == null) transients = gameObject.AddComponent<EngineTransientPlayer>();
            transients.Initialize(sourceRoot);

            if (auxPlayer == null) auxPlayer = GetComponent<VehicleAuxAudioPlayer>();
            if (auxPlayer == null) auxPlayer = gameObject.AddComponent<VehicleAuxAudioPlayer>();
            auxPlayer.Initialize(sourceRoot);

            if (surfacePlayer == null) surfacePlayer = GetComponent<VehicleSurfaceAudioPlayer>();
            if (surfacePlayer == null) surfacePlayer = gameObject.AddComponent<VehicleSurfaceAudioPlayer>();
            surfacePlayer.Initialize(sourceRoot);

            if (audioBank != null) ApplyBankToPlayers();

            initialized = true;
            if (logInit) Debug.Log("[AudioV2] VehicleAudioControllerV2 initialized.");
        }

        private void ApplyBankToPlayers()
        {
            if (audioBank == null) return;

            NFSU2CarAudioBank.TierAudioPackage tier = audioBank.GetTier(audioTier);
            if (tier == null)
            {
                if (logInit) Debug.LogWarning($"[AudioV2] Audio bank tier {audioTier} not found.");
                return;
            }

            bool isExterior = cameraMode == CarAudioCameraMode.Exterior;
            stateFeed?.ApplyTuning(audioBank.tuning);
            engineLayers?.ApplyTuning(audioBank.tuning);
            engineLayers?.ApplyFromBank(tier, isExterior);
            transients?.ApplyFromBank(tier, isExterior);
            auxPlayer?.ApplyFromBank(tier, isExterior);
            surfacePlayer?.ApplyFromBank(tier, isExterior);

            if (logInit) Debug.Log($"[AudioV2] Applied audio bank '{audioBank.name}' tier {audioTier}.");
        }

        private void EnsureSourceRoot()
        {
            Transform existing = transform.Find("AudioV2");
            if (existing != null)
            {
                sourceRoot = existing;
                return;
            }

            GameObject go = new GameObject("AudioV2");
            go.transform.SetParent(transform, false);
            sourceRoot = go.transform;
        }

        private void ResolveReferences()
        {
            if (vehicleController == null)
            {
                vehicleController = GetComponent<VehicleControllerV2>();
            }
        }
    }
}

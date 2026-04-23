using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Underground.Vehicle;

namespace Underground.World
{
    /// <summary>
    /// Runtime safety pass for the generated city. It caps frame pacing to a stable target,
    /// then adapts the heaviest world rendering systems before the player falls below 60 FPS.
    /// </summary>
    [DefaultExecutionOrder(-8500)]
    public sealed class WorldPerformanceGuard : MonoBehaviour
    {
        private const string GuardObjectName = "WorldPerformanceGuard";

        [Header("Scope")]
        [SerializeField] private bool onlyWorldScenes = true;
        [SerializeField] private bool applyInEditorPlayMode = true;

        [Header("Frame Pacing")]
        [SerializeField] private bool forceWorldFramePacing = true;
        [SerializeField] private int safeMaxFrameRate = 90;
        [SerializeField] private float physicsTickRate = 60f;
        [SerializeField] private float maxFrameCatchupSeconds = 0.0667f;

        [Header("Render Caps")]
        [SerializeField] private float maxShadowDistance = 35f;
        [SerializeField] private int maxShadowCascades = 1;
        [SerializeField] private int maxVisibleTraffic = 16;
        [SerializeField] private float maxCameraFarClip = 450f;
        [SerializeField] private bool disableNonPlayerShadowCasters = true;
        [SerializeField] private bool disablePointAndSpotLightShadows = true;
        [SerializeField] private bool disableNonPlayerReflectionProbeUsage = true;
        [SerializeField] private bool disableNonPlayerMotionVectors = true;
        [SerializeField] private bool disableRealtimeReflectionProbes = true;
        [SerializeField] private bool capTrafficCount = true;

        [Header("Adaptive Performance")]
        [SerializeField] private bool adaptivePerformance = true;
        [SerializeField] private float minimumTargetFps = 60f;
        [SerializeField] private float recoverFps = 74f;
        [SerializeField] private float lowFpsHoldSeconds = 0.65f;
        [SerializeField] private float recoverHoldSeconds = 6f;
        [SerializeField] private float adaptCooldownSeconds = 1.5f;
        [SerializeField, Range(0, 3)] private int baselineAdaptiveTier = 1;
        [SerializeField, Range(0, 3)] private int maxAdaptiveTier = 3;

        private static WorldPerformanceGuard instance;
        private bool pendingPass;
        private float smoothedFrameTime = -1f;
        private float lowFpsTimer;
        private float highFpsTimer;
        private float nextAdaptTime;
        private int adaptiveTier = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
            {
                return;
            }

            GameObject guardObject = new GameObject(GuardObjectName);
            DontDestroyOnLoad(guardObject);
            instance = guardObject.AddComponent<WorldPerformanceGuard>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            QueuePass();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Update()
        {
            if (!ShouldRunInCurrentScene())
            {
                return;
            }

            ApplyFramePacing();

            if (!adaptivePerformance)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
            {
                return;
            }

            smoothedFrameTime = smoothedFrameTime < 0f
                ? dt
                : Mathf.Lerp(smoothedFrameTime, dt, 1f - Mathf.Exp(-3.5f * dt));

            float fps = smoothedFrameTime > 0.0001f ? 1f / smoothedFrameTime : safeMaxFrameRate;
            if (Time.unscaledTime < nextAdaptTime)
            {
                return;
            }

            if (fps < minimumTargetFps && adaptiveTier < maxAdaptiveTier)
            {
                lowFpsTimer += dt;
                highFpsTimer = 0f;
                if (lowFpsTimer >= lowFpsHoldSeconds)
                {
                    SetAdaptiveTier(adaptiveTier + 1, fps);
                }

                return;
            }

            if (fps > recoverFps && adaptiveTier > 0)
            {
                highFpsTimer += dt;
                lowFpsTimer = 0f;
                if (highFpsTimer >= recoverHoldSeconds)
                {
                    SetAdaptiveTier(adaptiveTier - 1, fps);
                }

                return;
            }

            lowFpsTimer = 0f;
            highFpsTimer = 0f;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            adaptiveTier = Mathf.Clamp(baselineAdaptiveTier, 0, maxAdaptiveTier);
            smoothedFrameTime = -1f;
            lowFpsTimer = 0f;
            highFpsTimer = 0f;
            QueuePass();
        }

        private void QueuePass()
        {
            if (pendingPass)
            {
                return;
            }

            pendingPass = true;
            StartCoroutine(ApplyAfterSceneSettles());
        }

        private IEnumerator ApplyAfterSceneSettles()
        {
            yield return null;
            yield return new WaitForSecondsRealtime(0.25f);
            pendingPass = false;
            ApplyPerformancePass();
        }

        private void ApplyPerformancePass()
        {
            if (!ShouldRunInCurrentScene())
            {
                return;
            }

            ApplyFramePacing();
            int cameraChanges = ApplyCameraCaps();

            int dayNightSystems = ApplyDayNightBudget();
            int shadowCasterChanges = disableNonPlayerShadowCasters ? DisableNonPlayerShadowCasters() : 0;
            int lightShadowChanges = disablePointAndSpotLightShadows ? DisableExpensiveLightShadows() : 0;
            int reflectionUsageChanges = disableNonPlayerReflectionProbeUsage ? DisableNonPlayerReflectionProbeUsage() : 0;
            int motionVectorChanges = disableNonPlayerMotionVectors ? DisableNonPlayerMotionVectors() : 0;
            int probeChanges = disableRealtimeReflectionProbes ? DisableRealtimeReflectionProbes() : 0;
            int trafficDisabled = capTrafficCount ? CapTrafficCount(ResolveTrafficLimit()) : 0;

            float shadowDistance = ResolveShadowDistance();
            QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance <= 0f ? shadowDistance : QualitySettings.shadowDistance, shadowDistance);
            QualitySettings.shadowCascades = Mathf.Min(Mathf.Max(0, QualitySettings.shadowCascades), ResolveShadowCascades());
            QualitySettings.antiAliasing = Mathf.Min(QualitySettings.antiAliasing, ResolveAntiAliasing());
            QualitySettings.lodBias = Mathf.Min(QualitySettings.lodBias <= 0f ? ResolveLodBias() : QualitySettings.lodBias, ResolveLodBias());

            Debug.Log($"[PerformanceGuard] Applied world caps. tier={adaptiveTier}, fpsCap={Application.targetFrameRate}, fixedDt={Time.fixedDeltaTime:0.000}, camerasCapped={cameraChanges}, dayNight={dayNightSystems}, shadowCastersOff={shadowCasterChanges}, lightShadowsOff={lightShadowChanges}, reflectionUsageOff={reflectionUsageChanges}, motionVectorsOff={motionVectorChanges}, probesOff={probeChanges}, trafficDisabled={trafficDisabled}, trafficLimit={ResolveTrafficLimit()}, shadowDistance={QualitySettings.shadowDistance:0}.");
        }

        private bool ShouldRunInCurrentScene()
        {
            if (!Application.isPlaying)
            {
                return false;
            }

            if (Application.isEditor && !applyInEditorPlayMode)
            {
                return false;
            }

            return !onlyWorldScenes || IsWorldScene(SceneManager.GetActiveScene());
        }

        private void ApplyFramePacing()
        {
            if (!forceWorldFramePacing)
            {
                return;
            }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Mathf.Clamp(safeMaxFrameRate, 60, 144);
            Time.fixedDeltaTime = 1f / Mathf.Max(30f, physicsTickRate);
            Time.maximumDeltaTime = Mathf.Clamp(maxFrameCatchupSeconds, Time.fixedDeltaTime, 0.1f);
        }

        private void SetAdaptiveTier(int tier, float fps)
        {
            int nextTier = Mathf.Clamp(tier, 0, maxAdaptiveTier);
            if (nextTier == adaptiveTier)
            {
                return;
            }

            adaptiveTier = nextTier;
            lowFpsTimer = 0f;
            highFpsTimer = 0f;
            nextAdaptTime = Time.unscaledTime + Mathf.Max(0.5f, adaptCooldownSeconds);
            Debug.Log($"[PerformanceGuard] Adaptive tier changed to {adaptiveTier} at {fps:0.0} FPS.");
            ApplyPerformancePass();
        }

        private int ApplyDayNightBudget()
        {
            global::DayNight[] systems = FindObjectsByType<global::DayNight>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (systems == null || systems.Length == 0)
            {
                return 0;
            }

            NightLightBudget budget = ResolveNightLightBudget();
            for (int i = 0; i < systems.Length; i++)
            {
                global::DayNight dayNight = systems[i];
                if (dayNight == null)
                {
                    continue;
                }

                dayNight.streetLightRefreshInterval = budget.RefreshInterval;
                dayNight.streetLightCacheRefreshInterval = budget.CacheRefreshInterval;
                dayNight.activeStreetLightDistance = budget.Distance;
                dayNight.maxActiveRealtimeStreetLights = budget.MaxLights;
                dayNight.maxActiveStreetLightBeams = budget.MaxBeams;
                dayNight.streetLightIntensity = budget.Intensity;
                dayNight.streetLightRange = budget.Range;
                dayNight.streetLightVolumetricDimmer = budget.VolumetricDimmer;
                dayNight.streetLightShadows = false;
                dayNight.duskNightOnly = true;
                dayNight.SetStreetLights(true);
            }

            return systems.Length;
        }

        private NightLightBudget ResolveNightLightBudget()
        {
            switch (adaptiveTier)
            {
                case 0:
                    return new NightLightBudget(0.9f, 14f, 85f, 24, 6, 2200f, 14f, 0.08f);
                case 1:
                    return new NightLightBudget(1.05f, 16f, 70f, 16, 3, 1800f, 12f, 0f);
                case 2:
                    return new NightLightBudget(1.25f, 18f, 55f, 8, 0, 1300f, 9f, 0f);
                default:
                    return new NightLightBudget(1.5f, 22f, 40f, 0, 0, 0f, 8f, 0f);
            }
        }

        private float ResolveShadowDistance()
        {
            switch (adaptiveTier)
            {
                case 0:
                    return maxShadowDistance;
                case 1:
                    return Mathf.Min(maxShadowDistance, 24f);
                case 2:
                    return Mathf.Min(maxShadowDistance, 12f);
                default:
                    return 0f;
            }
        }

        private int ResolveShadowCascades()
        {
            return adaptiveTier >= 2 ? 0 : Mathf.Min(maxShadowCascades, 1);
        }

        private int ResolveAntiAliasing()
        {
            return adaptiveTier >= 2 ? 0 : 2;
        }

        private float ResolveLodBias()
        {
            switch (adaptiveTier)
            {
                case 0:
                    return 0.75f;
                case 1:
                    return 0.6f;
                case 2:
                    return 0.45f;
                default:
                    return 0.35f;
            }
        }

        private int ResolveTrafficLimit()
        {
            return Mathf.Max(2, maxVisibleTraffic - adaptiveTier * 5);
        }

        private int ApplyCameraCaps()
        {
            int changed = 0;
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null)
                {
                    continue;
                }

                float targetFarClip = ResolveCameraFarClip();
                if (camera.farClipPlane > targetFarClip)
                {
                    camera.farClipPlane = targetFarClip;
                    changed++;
                }

                if (camera.allowDynamicResolution)
                {
                    camera.allowDynamicResolution = false;
                    changed++;
                }
            }

            return changed;
        }

        private float ResolveCameraFarClip()
        {
            switch (adaptiveTier)
            {
                case 0:
                    return Mathf.Min(maxCameraFarClip, 450f);
                case 1:
                    return Mathf.Min(maxCameraFarClip, 360f);
                case 2:
                    return Mathf.Min(maxCameraFarClip, 280f);
                default:
                    return Mathf.Min(maxCameraFarClip, 220f);
            }
        }

        private static bool IsWorldScene(Scene scene)
        {
            return scene.IsValid()
                && scene.isLoaded
                && scene.name.IndexOf("World", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int DisableNonPlayerShadowCasters()
        {
            int changed = 0;
            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer || IsPlayerVehicleRenderer(renderer))
                {
                    continue;
                }

                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    changed++;
                }

                if (renderer.receiveShadows)
                {
                    renderer.receiveShadows = false;
                    changed++;
                }
            }

            return changed;
        }

        private static int DisableExpensiveLightShadows()
        {
            int changed = 0;
            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light == null || light.type == LightType.Directional || light.shadows == LightShadows.None)
                {
                    continue;
                }

                light.shadows = LightShadows.None;
                changed++;
            }

            return changed;
        }

        private static int DisableNonPlayerReflectionProbeUsage()
        {
            int changed = 0;
            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer || IsPlayerVehicleRenderer(renderer))
                {
                    continue;
                }

                if (renderer.reflectionProbeUsage != ReflectionProbeUsage.Off)
                {
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    changed++;
                }
            }

            return changed;
        }

        private static int DisableNonPlayerMotionVectors()
        {
            int changed = 0;
            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer || IsPlayerVehicleRenderer(renderer))
                {
                    continue;
                }

                if (renderer.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
                {
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    changed++;
                }
            }

            return changed;
        }

        private static int DisableRealtimeReflectionProbes()
        {
            int changed = 0;
            ReflectionProbe[] probes = FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < probes.Length; i++)
            {
                ReflectionProbe probe = probes[i];
                if (probe == null || !probe.enabled)
                {
                    continue;
                }

                if (probe.mode == ReflectionProbeMode.Realtime || probe.name.Contains("RuntimeVehicleReflectionProbe"))
                {
                    probe.enabled = false;
                    changed++;
                }
            }

            return changed;
        }

        private int CapTrafficCount(int visibleLimit)
        {
            GameObject[] trafficObjects;
            try
            {
                trafficObjects = GameObject.FindGameObjectsWithTag("Traffic");
            }
            catch (UnityException)
            {
                return 0;
            }

            if (trafficObjects == null || trafficObjects.Length <= visibleLimit)
            {
                return 0;
            }

            Vector3 focus = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            List<GameObject> traffic = new List<GameObject>(trafficObjects);
            traffic.Sort((left, right) =>
            {
                float leftDistance = left != null ? (left.transform.position - focus).sqrMagnitude : float.MaxValue;
                float rightDistance = right != null ? (right.transform.position - focus).sqrMagnitude : float.MaxValue;
                return leftDistance.CompareTo(rightDistance);
            });

            int disabled = 0;
            for (int i = visibleLimit; i < traffic.Count; i++)
            {
                if (traffic[i] == null || !traffic[i].activeSelf)
                {
                    continue;
                }

                traffic[i].SetActive(false);
                disabled++;
            }

            return disabled;
        }

        private static bool IsPlayerVehicleRenderer(Renderer renderer)
        {
            if (renderer.GetComponentInParent<FullThrottle.SacredCore.Vehicle.FTVehicleController>() != null)
            {
                return true;
            }

            if (renderer.GetComponentInParent<PlayerCarAppearanceController>() != null)
            {
                return true;
            }

            Transform root = renderer.transform.root;
            return root != null && root.name.IndexOf("PlayerCar", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private readonly struct NightLightBudget
        {
            public readonly float RefreshInterval;
            public readonly float CacheRefreshInterval;
            public readonly float Distance;
            public readonly int MaxLights;
            public readonly int MaxBeams;
            public readonly float Intensity;
            public readonly float Range;
            public readonly float VolumetricDimmer;

            public NightLightBudget(float refreshInterval, float cacheRefreshInterval, float distance, int maxLights, int maxBeams, float intensity, float range, float volumetricDimmer)
            {
                RefreshInterval = refreshInterval;
                CacheRefreshInterval = cacheRefreshInterval;
                Distance = distance;
                MaxLights = maxLights;
                MaxBeams = maxBeams;
                Intensity = intensity;
                Range = range;
                VolumetricDimmer = volumetricDimmer;
            }
        }
    }
}

using FCG;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Underground.TimeSystem;
using Underground.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Underground.Vehicle
{
    /// <summary>
    /// Modular vehicle lighting controller — Part 4 of the Full Throttle architecture.
    /// 
    /// Supports:
    ///  - Headlights (toggled by day/night + settings)
    ///  - Tail lights (on when headlights on)
    ///  - Brake lights (active on brake/handbrake input, independent of headlights)
    ///  - Reverse lights (active when reversing)
    ///  - HDRP emissive material intensity support
    ///  - Prefab-authored light rigs (HeadlightsRoot, TailLightsRoot, etc.)
    ///  - Traffic car support
    /// 
    /// Prefab Convention:
    ///  Place child GameObjects named HeadlightsRoot, TailLightsRoot, BrakeLightsRoot,
    ///  ReverseLightsRoot under the vehicle. Lights within those roots will be discovered
    ///  automatically. The old auto-generated NightLightingRig fallback is intentionally removed.
    /// </summary>
    public class VehicleNightLightingController : MonoBehaviour
    {
        private const string LegacyNightLightingRigName = "NightLightingRig";

        // ─────────────────────────────────────────────────────────────────────
        //  Configuration
        // ─────────────────────────────────────────────────────────────────────

        [SerializeField] private Transform modelRoot;
        [SerializeField] private TimeOfDay packageTimeOfDay;
        [SerializeField] private InputReader input;
        [SerializeField] private Rigidbody vehicleBody;
        [SerializeField] private bool trafficLighting;
        [SerializeField] private bool headlightShadows = true;
        [SerializeField] private float headlightIntensity = 18f;
        [SerializeField] private float headlightRange = 36f;
        [SerializeField] private float headlightSpotAngle = 76f;
        [SerializeField] private float taillightIntensity = 2f;
        [SerializeField] private float brakeLightIntensity = 4.6f;
        [SerializeField] private float reverseLightIntensity = 3.2f;

        [Header("HDRP Emissive")]
        [Tooltip("If assigned, emissive intensity is modulated on these renderers for brake lights.")]
        [SerializeField] private Renderer[] brakeLightEmissiveRenderers;
        [Tooltip("If assigned, emissive intensity is modulated on these renderers for tail lights.")]
        [SerializeField] private Renderer[] tailLightEmissiveRenderers;
        [Tooltip("If assigned, emissive intensity is modulated on these renderers for reverse lights.")]
        [SerializeField] private Renderer[] reverseLightEmissiveRenderers;
        [SerializeField] private float emissiveBrakeMultiplier = 6f;
        [SerializeField] private float emissiveTailMultiplier = 2f;
        [SerializeField] private float emissiveReverseMultiplier = 4f;

        [Header("Day/Night Sensitivity")]
        [Tooltip("Additional intensity multiplier applied during daytime headlight override.")]
        [SerializeField] private float daytimeDimFactor = 0.35f;

        [Header("Effects")]
        [Tooltip("Optional prefab (like FX_Light_Beam_01) to spawn on headlights.")]
        [SerializeField] private GameObject headlightBeamPrefab;
        [SerializeField] private bool autoUseDefaultHeadlightBeamPrefab = true;
        [SerializeField] private string defaultHeadlightBeamPrefabPath = "Assets/PolygonStreetRacer/Prefabs/FX/FX_Light_Beam_01.prefab";
        [SerializeField] private Vector3 headlightBeamLocalScale = new Vector3(0.72f, 0.72f, 1.75f);
        [SerializeField] private bool enableHeadlightVolumetrics = true;
        [SerializeField, Range(0f, 4f)] private float headlightVolumetricDimmer = 1.35f;
        [SerializeField] private float referenceLookupInterval = 5f;
        [SerializeField] private float trafficLightingUpdateInterval = 0.12f;

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime State
        // ─────────────────────────────────────────────────────────────────────

        private TrafficCar trafficCar;
        private GameSettingsManager settingsManager;
        
        private V2.VehicleControllerV2 dynamicsV2;

        // Light arrays (support N lights per group)
        private Light[] headlights;
        private Light[] taillights;
        private Light[] brakelights;
        private Light[] reverselights;

        private GameObject[] headlightBeams;

        private bool rigCreated;
        private float nextLookupTime;
        private float nextTrafficLightingUpdateTime;
        private bool _headlightsForceOn;
        private bool lightingStateInitialized;
        private bool lastHeadlightsOn;
        private bool lastTailsOn;
        private bool lastBrakesOn;
        private bool lastReverseOn;
        private float lastHeadIntensity = -1f;
        private float lastBrakeIntensity = -1f;
        private float lastTailEmissive = -1f;
        private float lastBrakeEmissive = -1f;
        private float lastReverseEmissive = -1f;
        private LightShadows lastHeadlightShadowMode = (LightShadows)(-1);
        private bool warnedMissingAuthoredRig;

        // Emissive material caching
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private MaterialPropertyBlock _emissivePropBlock;

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Force headlights on regardless of day/night state.</summary>
        public bool HeadlightsForceOn
        {
            get => _headlightsForceOn;
            set => _headlightsForceOn = value;
        }

        /// <summary>Configure simplified lighting for traffic cars.</summary>
        public void ConfigureForTraffic(bool enableHeadlightShadows)
        {
            trafficLighting = true;
            headlightShadows = enableHeadlightShadows;
            headlightIntensity = 10f;
            headlightRange = 24f;
            headlightSpotAngle = 80f;
            taillightIntensity = 1.25f;
            brakeLightIntensity = 2.8f;
            reverseLightIntensity = 1.8f;
        }

        /// <summary>
        /// Forces the lighting rig to be rebuilt from scratch.
        /// Called automatically when PlayerCarAppearanceController swaps the model.
        /// </summary>
        public void RebuildLightingRig()
        {
            DestroyLightingRig();
            ResetLightingStateCache();
            EnsureLightingRig();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _emissivePropBlock = new MaterialPropertyBlock();
            ResolveReferences();
            ResolveDefaultHeadlightBeamPrefab();
            DestroyLegacyNightLightingRigChildren();
            SubscribeToAppearanceChanges();
            EnsureLightingRig();
            ApplyLighting(false);
        }

        private void OnDestroy()
        {
            UnsubscribeFromAppearanceChanges();
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextLookupTime)
            {
                nextLookupTime = Time.unscaledTime + Mathf.Max(1f, referenceLookupInterval);
                ResolveReferences();
            }

            if (trafficLighting && Time.unscaledTime < nextTrafficLightingUpdateTime)
            {
                return;
            }

            if (trafficLighting)
            {
                nextTrafficLightingUpdateTime = Time.unscaledTime + Mathf.Max(0.03f, trafficLightingUpdateInterval);
            }

            DestroyLegacyNightLightingRigChildren();
            EnsureLightingRig();
            bool nightActive = PackageTimeOfDayUtility.IsNight(packageTimeOfDay);
            ApplyLighting(nightActive);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Appearance Change Subscription
        // ─────────────────────────────────────────────────────────────────────

        private void SubscribeToAppearanceChanges()
        {
            PlayerCarAppearanceController appearance = GetComponent<PlayerCarAppearanceController>();
            if (appearance != null)
            {
                appearance.AppearanceChanged += RebuildLightingRig;
            }
        }

        private void UnsubscribeFromAppearanceChanges()
        {
            PlayerCarAppearanceController appearance = GetComponent<PlayerCarAppearanceController>();
            if (appearance != null)
            {
                appearance.AppearanceChanged -= RebuildLightingRig;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Reference Resolution
        // ─────────────────────────────────────────────────────────────────────

        private void ResolveReferences()
        {
            if (modelRoot == null)
            {
                Transform candidate = transform.Find("ModelRoot");
                modelRoot = candidate != null ? candidate : transform;
            }

            if (packageTimeOfDay == null)
            {
                packageTimeOfDay = PackageTimeOfDayUtility.FindPackageTimeOfDay();
            }

            if (input == null)
            {
                input = GetComponent<InputReader>();
            }

            if (vehicleBody == null)
            {
                vehicleBody = GetComponent<Rigidbody>();
            }

            if (trafficCar == null)
            {
                trafficCar = GetComponent<TrafficCar>();
            }

            if (settingsManager == null)
            {
                settingsManager = FindFirstObjectByType<GameSettingsManager>();
            }

            

            if (dynamicsV2 == null)
            {
                dynamicsV2 = GetComponent<V2.VehicleControllerV2>();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Lighting Rig — Build / Destroy
        // ─────────────────────────────────────────────────────────────────────

        private void DestroyLightingRig()
        {
            DestroyLegacyNightLightingRigChildren();
            headlights = null;
            taillights = null;
            brakelights = null;
            reverselights = null;
            headlightBeams = null;
            rigCreated = false;
            warnedMissingAuthoredRig = false;
            ResetLightingStateCache();
        }

        private void DestroyLegacyNightLightingRigChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == null || child.name != LegacyNightLightingRigName)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void EnsureLightingRig()
        {
            if (rigCreated)
            {
                return;
            }

            DestroyLegacyNightLightingRigChildren();

            // Try to discover prefab-authored light rigs first
            if (TryDiscoverAuthoredLightRig())
            {
                rigCreated = true;
                ConfigureHeadlightsForNightUse();
                SpawnLightBeams();
                return;
            }

            headlights = System.Array.Empty<Light>();
            taillights = System.Array.Empty<Light>();
            brakelights = System.Array.Empty<Light>();
            reverselights = System.Array.Empty<Light>();
            headlightBeams = System.Array.Empty<GameObject>();
            rigCreated = true;

            if (!warnedMissingAuthoredRig && !trafficLighting)
            {
                warnedMissingAuthoredRig = true;
                Debug.LogWarning($"[VehicleNightLightingController] No authored light roots found on '{name}'. Old '{LegacyNightLightingRigName}' fallback is disabled; add HeadlightsRoot/TailLightsRoot/BrakeLightsRoot/ReverseLightsRoot if this vehicle needs runtime lights.", this);
            }
        }

        /// <summary>
        /// Looks for child objects named HeadlightsRoot, TailLightsRoot, etc.
        /// If found, collects their Light children and uses them instead of auto-generating.
        /// </summary>
        private bool TryDiscoverAuthoredLightRig()
        {
            Transform searchRoot = modelRoot != null ? modelRoot : transform;

            Transform headRoot = FindDeep(searchRoot, VehicleConstants.HeadlightsRootName);
            Transform tailRoot = FindDeep(searchRoot, VehicleConstants.TailLightsRootName);
            Transform brakeRoot = FindDeep(searchRoot, VehicleConstants.BrakeLightsRootName);
            Transform reverseRoot = FindDeep(searchRoot, VehicleConstants.ReverseLightsRootName);

            // Need at least headlights to consider the rig "authored"
            if (headRoot == null)
            {
                return false;
            }

            headlights = headRoot.GetComponentsInChildren<Light>(true);
            taillights = tailRoot != null ? tailRoot.GetComponentsInChildren<Light>(true) : new Light[0];
            brakelights = brakeRoot != null ? brakeRoot.GetComponentsInChildren<Light>(true) : new Light[0];
            reverselights = reverseRoot != null ? reverseRoot.GetComponentsInChildren<Light>(true) : new Light[0];

            // Discover emissive renderers from authored rig if not manually assigned
            if ((brakeLightEmissiveRenderers == null || brakeLightEmissiveRenderers.Length == 0) && brakeRoot != null)
            {
                brakeLightEmissiveRenderers = brakeRoot.GetComponentsInChildren<Renderer>(true);
            }

            if ((tailLightEmissiveRenderers == null || tailLightEmissiveRenderers.Length == 0) && tailRoot != null)
            {
                tailLightEmissiveRenderers = tailRoot.GetComponentsInChildren<Renderer>(true);
            }

            if ((reverseLightEmissiveRenderers == null || reverseLightEmissiveRenderers.Length == 0) && reverseRoot != null)
            {
                reverseLightEmissiveRenderers = reverseRoot.GetComponentsInChildren<Renderer>(true);
            }

            return headlights.Length > 0;
        }

        private void SpawnLightBeams()
        {
            if (headlights == null || headlights.Length == 0)
            {
                return;
            }

            headlightBeams = new GameObject[headlights.Length];
            for (int i = 0; i < headlights.Length; i++)
            {
                if (headlights[i] == null) continue;

                Transform existing = headlights[i].transform.Find("HeadlightBeam_Runtime");
                if (existing != null)
                {
                    existing.localScale = headlightBeamLocalScale;
                    existing.gameObject.SetActive(false);
                    headlightBeams[i] = existing.gameObject;
                    continue;
                }

                if (headlightBeamPrefab == null)
                {
                    continue;
                }
                
                GameObject beam = Instantiate(headlightBeamPrefab, headlights[i].transform);
                beam.name = "HeadlightBeam_Runtime";
                beam.transform.localPosition = Vector3.zero;
                beam.transform.localRotation = Quaternion.identity;
                beam.transform.localScale = headlightBeamLocalScale;
                beam.SetActive(false);
                headlightBeams[i] = beam;
            }
        }



        // ─────────────────────────────────────────────────────────────────────
        //  Lighting Application — Every Frame
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyLighting(bool nightActive)
        {
            if (!rigCreated)
            {
                return;
            }

            bool headlightsEnabled = settingsManager == null || settingsManager.CarHeadlightsEnabled;
            bool headlightsOn = (nightActive || _headlightsForceOn) && headlightsEnabled;
            float brakeFactor = GetBrakeFactor();
            bool isReversing = GetIsReversing();

            // ── Headlights ──
            float headIntensity = headlightsOn
                ? (nightActive ? headlightIntensity : headlightIntensity * daytimeDimFactor)
                : 0f;
            if (!lightingStateInitialized || headlightsOn != lastHeadlightsOn || !Approximately(headIntensity, lastHeadIntensity))
            {
                ApplyLightGroup(headlights, headlightsOn, headIntensity);
                lastHeadlightsOn = headlightsOn;
                lastHeadIntensity = headIntensity;
            }

            UpdateHeadlightShadows();
            
            // Toggle physical light beams
            if (headlightBeams != null)
            {
                for (int i = 0; i < headlightBeams.Length; i++)
                {
                    if (headlightBeams[i] != null && headlightBeams[i].activeSelf != headlightsOn)
                    {
                        headlightBeams[i].SetActive(headlightsOn);
                    }
                }
            }

            // ── Tail lights (on when headlights are on) ──
            bool tailsOn = headlightsOn;
            float tailEmissive = tailsOn ? emissiveTailMultiplier : 0f;
            if (!lightingStateInitialized || tailsOn != lastTailsOn)
            {
                ApplyLightGroup(taillights, tailsOn, taillightIntensity);
                lastTailsOn = tailsOn;
            }

            if (!lightingStateInitialized || !Approximately(tailEmissive, lastTailEmissive))
            {
                ApplyEmissiveGroup(tailLightEmissiveRenderers, tailEmissive);
                lastTailEmissive = tailEmissive;
            }

            // ── Brake lights (independent of headlights — always visible when braking) ──
            bool brakesOn = brakeFactor > 0.01f;
            float brakeIntensity = brakeLightIntensity * brakeFactor;
            float brakeEmissive = brakesOn ? emissiveBrakeMultiplier * brakeFactor : 0f;
            if (!lightingStateInitialized || brakesOn != lastBrakesOn || !Approximately(brakeIntensity, lastBrakeIntensity))
            {
                ApplyLightGroup(brakelights, brakesOn, brakeIntensity);
                lastBrakesOn = brakesOn;
                lastBrakeIntensity = brakeIntensity;
            }

            if (!lightingStateInitialized || !Approximately(brakeEmissive, lastBrakeEmissive))
            {
                ApplyEmissiveGroup(brakeLightEmissiveRenderers, brakeEmissive);
                lastBrakeEmissive = brakeEmissive;
            }

            // ── Reverse lights ──
            float reverseEmissive = isReversing ? emissiveReverseMultiplier : 0f;
            if (!lightingStateInitialized || isReversing != lastReverseOn)
            {
                ApplyLightGroup(reverselights, isReversing, reverseLightIntensity);
                lastReverseOn = isReversing;
            }

            if (!lightingStateInitialized || !Approximately(reverseEmissive, lastReverseEmissive))
            {
                ApplyEmissiveGroup(reverseLightEmissiveRenderers, reverseEmissive);
                lastReverseEmissive = reverseEmissive;
            }

            lightingStateInitialized = true;
        }

        private void ApplyLightGroup(Light[] group, bool enabled, float intensity)
        {
            if (group == null)
            {
                return;
            }

            for (int i = 0; i < group.Length; i++)
            {
                if (group[i] == null) continue;
                group[i].enabled = enabled;
                group[i].intensity = intensity;
            }
        }

        private void UpdateHeadlightShadows()
        {
            if (headlights == null)
            {
                return;
            }

            LightShadows mode = ResolveHeadlightShadowMode();
            if (lightingStateInitialized && mode == lastHeadlightShadowMode)
            {
                return;
            }

            for (int i = 0; i < headlights.Length; i++)
            {
                if (headlights[i] != null)
                {
                    headlights[i].shadows = mode;
                }
            }

            lastHeadlightShadowMode = mode;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  HDRP Emissive Material Support
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyEmissiveGroup(Renderer[] renderers, float emissiveIntensity)
        {
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer rend = renderers[i];
                if (rend == null) continue;

                rend.GetPropertyBlock(_emissivePropBlock);
                Color emissiveColor = Color.white * emissiveIntensity;
                _emissivePropBlock.SetColor(EmissiveColorId, emissiveColor);
                rend.SetPropertyBlock(_emissivePropBlock);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Input Detection
        // ─────────────────────────────────────────────────────────────────────

        private void ResetLightingStateCache()
        {
            lightingStateInitialized = false;
            lastHeadlightsOn = false;
            lastTailsOn = false;
            lastBrakesOn = false;
            lastReverseOn = false;
            lastHeadIntensity = -1f;
            lastBrakeIntensity = -1f;
            lastTailEmissive = -1f;
            lastBrakeEmissive = -1f;
            lastReverseEmissive = -1f;
            lastHeadlightShadowMode = (LightShadows)(-1);
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.01f;
        }

        private float GetBrakeFactor()
        {
            if (input != null)
            {
                float playerBrake = Mathf.Max(input.Brake, input.Handbrake ? 1f : 0f);
                if (playerBrake > 0.01f)
                {
                    return playerBrake;
                }
            }

            if (trafficCar != null)
            {
                return trafficCar.status == TrafficCar.StatusCar.stoppedAtTrafficLights ||
                       trafficCar.status == TrafficCar.StatusCar.waitingForAnotherVehicleToPass ||
                       trafficCar.status == TrafficCar.StatusCar.bloked ||
                       trafficCar.status == TrafficCar.StatusCar.crashed
                    ? 1f
                    : 0f;
            }

            if (trafficLighting && vehicleBody != null)
            {
                return vehicleBody.linearVelocity.sqrMagnitude < 1f ? 0.4f : 0f;
            }

            return 0f;
        }

        private bool GetIsReversing()
        {
            // V2 controller first
            if (dynamicsV2 != null && dynamicsV2.IsInitialized && dynamicsV2.enabled)
            {
                return dynamicsV2.State.IsReversing;
            }

            

            // Traffic car: check negative forward velocity
            if (vehicleBody != null)
            {
                float forwardSpeed = Vector3.Dot(vehicleBody.linearVelocity, transform.forward);
                return forwardSpeed < -0.5f;
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Shadow Resolution
        // ─────────────────────────────────────────────────────────────────────

        private LightShadows ResolveHeadlightShadowMode()
        {
            if (!headlightShadows)
            {
                return LightShadows.None;
            }

            int shadowDetail = settingsManager != null ? settingsManager.CarShadowDetail : 2;
            return shadowDetail switch
            {
                0 => LightShadows.None,
                1 => LightShadows.Hard,
                _ => LightShadows.Soft
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Light Creation Helpers
        // ─────────────────────────────────────────────────────────────────────



        private void ConfigureHeadlightsForNightUse()
        {
            if (headlights == null)
            {
                return;
            }

            for (int i = 0; i < headlights.Length; i++)
            {
                Light headlight = headlights[i];
                if (headlight == null)
                {
                    continue;
                }

                headlight.type = LightType.Spot;
                headlight.range = Mathf.Max(headlight.range, headlightRange);
                headlight.spotAngle = Mathf.Max(headlight.spotAngle, headlightSpotAngle);
                headlight.innerSpotAngle = Mathf.Max(headlight.innerSpotAngle, headlightSpotAngle * 0.55f);
                headlight.color = new Color(1f, 0.96f, 0.9f);
                headlight.shadows = ResolveHeadlightShadowMode();

                if (enableHeadlightVolumetrics)
                {
                    HDAdditionalLightData hd = headlight.GetComponent<HDAdditionalLightData>();
                    if (hd == null)
                    {
                        hd = headlight.gameObject.AddComponent<HDAdditionalLightData>();
                    }

                    hd.volumetricDimmer = headlightVolumetricDimmer;
                    hd.fadeDistance = Mathf.Max(hd.fadeDistance, headlightRange * 1.6f);
                    hd.volumetricFadeDistance = Mathf.Max(hd.volumetricFadeDistance, headlightRange * 1.25f);
                }
            }
        }

        private void ResolveDefaultHeadlightBeamPrefab()
        {
#if UNITY_EDITOR
            if (!autoUseDefaultHeadlightBeamPrefab || headlightBeamPrefab != null || string.IsNullOrEmpty(defaultHeadlightBeamPrefabPath))
            {
                return;
            }

            headlightBeamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(defaultHeadlightBeamPrefabPath);
#endif
        }


        // ─────────────────────────────────────────────────────────────────────
        //  Model Bounds Discovery
        // ─────────────────────────────────────────────────────────────────────


        // ─────────────────────────────────────────────────────────────────────
        //  Deep Find Helper
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Recursively finds a child transform by name.</summary>
        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                Transform result = FindDeep(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}


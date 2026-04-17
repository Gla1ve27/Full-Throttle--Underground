using FCG;
using UnityEngine;
using UnityEngine.Rendering;
using Underground.TimeSystem;
using Underground.UI;

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
    ///  - Auto-generated lighting rig fallback from model bounds
    ///  - Traffic car support
    /// 
    /// Prefab Convention:
    ///  Place child GameObjects named HeadlightsRoot, TailLightsRoot, BrakeLightsRoot,
    ///  ReverseLightsRoot under the vehicle. Lights within those roots will be discovered
    ///  automatically. If no authored roots exist, lights are generated from model bounds.
    /// </summary>
    public class VehicleNightLightingController : MonoBehaviour
    {
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

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime State
        // ─────────────────────────────────────────────────────────────────────

        private TrafficCar trafficCar;
        private GameSettingsManager settingsManager;
        private VehicleDynamicsController dynamics;
        private Transform lightingRig;

        // Light arrays (support N lights per group)
        private Light[] headlights;
        private Light[] taillights;
        private Light[] brakelights;
        private Light[] reverselights;

        private GameObject[] headlightBeams;

        private bool rigCreated;
        private float nextLookupTime;
        private bool _headlightsForceOn;

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
            EnsureLightingRig();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _emissivePropBlock = new MaterialPropertyBlock();
            ResolveReferences();
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
                nextLookupTime = Time.unscaledTime + 1f;
                ResolveReferences();
            }

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

            if (dynamics == null)
            {
                dynamics = GetComponent<VehicleDynamicsController>();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Lighting Rig — Build / Destroy
        // ─────────────────────────────────────────────────────────────────────

        private void DestroyLightingRig()
        {
            if (lightingRig != null)
            {
                Object.Destroy(lightingRig.gameObject);
            }

            lightingRig = null;
            headlights = null;
            taillights = null;
            brakelights = null;
            reverselights = null;
            headlightBeams = null;
            rigCreated = false;
        }

        private void EnsureLightingRig()
        {
            if (rigCreated)
            {
                return;
            }

            // Try to discover prefab-authored light rigs first
            if (TryDiscoverAuthoredLightRig())
            {
                rigCreated = true;
                return;
            }

            // Fallback: create auto-generated rig from model bounds
            CreateAutoGeneratedRig();
            rigCreated = true;
            
            SpawnLightBeams();
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
            if (headlightBeamPrefab == null || headlights == null || headlights.Length == 0) return;

            headlightBeams = new GameObject[headlights.Length];
            for (int i = 0; i < headlights.Length; i++)
            {
                if (headlights[i] == null) continue;
                
                // Instantiate the beam as a child of the headlight
                GameObject beam = Instantiate(headlightBeamPrefab, headlights[i].transform);
                beam.transform.localPosition = Vector3.zero;
                beam.transform.localRotation = Quaternion.identity;
                beam.SetActive(false);
                headlightBeams[i] = beam;
            }
        }

        private void CreateAutoGeneratedRig()
        {
            if (lightingRig != null)
            {
                Object.Destroy(lightingRig.gameObject);
            }

            GameObject rigObject = new GameObject("NightLightingRig");
            lightingRig = rigObject.transform;
            lightingRig.SetParent(transform, false);

            if (!TryGetModelBounds(out Vector3 localMin, out Vector3 localMax))
            {
                localMin = new Vector3(-0.8f, 0.1f, -1.8f);
                localMax = new Vector3(0.8f, 1.1f, 1.8f);
            }

            float width = localMax.x - localMin.x;
            float height = localMax.y - localMin.y;
            float frontZ = localMax.z;
            float rearZ = localMin.z;
            
            // Default placements (clamped to the far corners for realism)
            float leftX = localMin.x + (width * 0.12f);
            float rightX = localMax.x - (width * 0.12f);
            float headlightY = localMin.y + (height * 0.35f);
            float taillightY = localMin.y + (height * 0.40f);
            float headZ = localMax.z + 0.1f;
            float tailZ = localMin.z - 0.1f;

            // DYNAMIC SEARCH: Try to find actual Light meshes in the model
            Vector3? meshHeadL = FindBestMeshCenter("head", true, true);
            Vector3? meshHeadR = FindBestMeshCenter("head", false, true);
            Vector3? meshBrakeL = FindBestMeshCenter("brake", true, false) ?? FindBestMeshCenter("tail", true, false);
            Vector3? meshBrakeR = FindBestMeshCenter("brake", false, false) ?? FindBestMeshCenter("tail", false, false);
            Vector3? meshReverseL = FindBestMeshCenter("reverse", true, false);
            Vector3? meshReverseR = FindBestMeshCenter("reverse", false, false);

            // ── Headlights ──
            Vector3 posHeadL = meshHeadL ?? new Vector3(leftX, headlightY, headZ);
            Vector3 posHeadR = meshHeadR ?? new Vector3(rightX, headlightY, headZ);
            
            Light lHead = CreateLight("HeadlightLeft", LightType.Spot, posHeadL,
                Quaternion.Euler(2f, 0f, 0f), new Color(1f, 0.96f, 0.95f), headlightIntensity, headlightRange);
            Light rHead = CreateLight("HeadlightRight", LightType.Spot, posHeadR,
                Quaternion.Euler(2f, 0f, 0f), new Color(1f, 0.96f, 0.95f), headlightIntensity, headlightRange);

            ConfigureSpotLight(lHead);
            ConfigureSpotLight(rHead);
            headlights = new[] { lHead, rHead };

            // ── Tail lights (Soft ambient glow) ──
            Vector3 posTailL = meshBrakeL ?? new Vector3(leftX, taillightY, tailZ);
            Vector3 posTailR = meshBrakeR ?? new Vector3(rightX, taillightY, tailZ);

            // Shift them slightly back so they don't clip inside the bumper
            if (meshBrakeL.HasValue) posTailL += Vector3.back * 0.05f;
            if (meshBrakeR.HasValue) posTailR += Vector3.back * 0.05f;

            Light lTail = CreateLight("TaillightLeft", LightType.Point, posTailL,
                Quaternion.identity, new Color(1f, 0.12f, 0.05f), taillightIntensity * 1.5f, 6.0f);
            Light rTail = CreateLight("TaillightRight", LightType.Point, posTailR,
                Quaternion.identity, new Color(1f, 0.12f, 0.05f), taillightIntensity * 1.5f, 6.0f);
            SetNoShadows(lTail);
            SetNoShadows(rTail);
            taillights = new[] { lTail, rTail };

            // ── Brake lights (Wider brighter flood) ──
            Light lBrake = CreateLight("BrakelightLeft", LightType.Point, posTailL,
                Quaternion.identity, new Color(1f, 0.05f, 0.02f), brakeLightIntensity * 1.8f, 10.0f);
            Light rBrake = CreateLight("BrakelightRight", LightType.Point, posTailR,
                Quaternion.identity, new Color(1f, 0.05f, 0.02f), brakeLightIntensity * 1.8f, 10.0f);
            SetNoShadows(lBrake);
            SetNoShadows(rBrake);
            brakelights = new[] { lBrake, rBrake };

            // ── Reverse lights ──
            Vector3 posRevL = meshReverseL ?? new Vector3(leftX, taillightY - 0.1f, tailZ);
            Vector3 posRevR = meshReverseR ?? new Vector3(rightX, taillightY - 0.1f, tailZ);
            
            Light lReverse = CreateLight("ReverseLightLeft", LightType.Point, posRevL,
                Quaternion.identity, new Color(1f, 1f, 1f), reverseLightIntensity * 1.5f, 5.0f);
            Light rReverse = CreateLight("ReverseLightRight", LightType.Point, posRevR,
                Quaternion.identity, new Color(1f, 1f, 1f), reverseLightIntensity * 1.5f, 5.0f);
            SetNoShadows(lReverse);
            SetNoShadows(rReverse);
            reverselights = new[] { lReverse, rReverse };
        }

        private Vector3? FindBestMeshCenter(string keyword, bool leftSide, bool front)
        {
            Renderer[] renderers = (modelRoot != null ? modelRoot : transform).GetComponentsInChildren<Renderer>(true);
            float bestScore = -1f;
            Vector3? bestCenter = null;

            foreach (var r in renderers)
            {
                string n = r.name.ToLowerInvariant();
                if (!n.Contains(keyword)) continue;

                // Simple parity check
                bool meshIsLeft = r.transform.localPosition.x < -0.01f || n.Contains("left") || n.Contains("_l");
                bool meshIsFront = r.transform.localPosition.z > 0.01f || n.Contains("front") || n.Contains("head");

                if (meshIsLeft != leftSide || (keyword != "head" && meshIsFront != front)) continue;

                // Score based on name clarity
                float score = 0;
                if (n.Contains("light")) score += 10;
                if (n.Contains("glass") || n.Contains("lens")) score += 5;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCenter = transform.InverseTransformPoint(r.bounds.center);
                }
            }

            return bestCenter;
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
            ApplyLightGroup(headlights, headlightsOn, headIntensity);
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
            ApplyLightGroup(taillights, tailsOn, taillightIntensity);
            ApplyEmissiveGroup(tailLightEmissiveRenderers, tailsOn ? emissiveTailMultiplier : 0f);

            // ── Brake lights (independent of headlights — always visible when braking) ──
            bool brakesOn = brakeFactor > 0.01f;
            float brakeIntensity = brakeLightIntensity * brakeFactor;
            ApplyLightGroup(brakelights, brakesOn, brakeIntensity);
            ApplyEmissiveGroup(brakeLightEmissiveRenderers, brakesOn ? emissiveBrakeMultiplier * brakeFactor : 0f);

            // ── Reverse lights ──
            ApplyLightGroup(reverselights, isReversing, reverseLightIntensity);
            ApplyEmissiveGroup(reverseLightEmissiveRenderers, isReversing ? emissiveReverseMultiplier : 0f);
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
            for (int i = 0; i < headlights.Length; i++)
            {
                if (headlights[i] != null)
                {
                    headlights[i].shadows = mode;
                }
            }
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
            // Player car: check dynamics controller
            if (dynamics != null)
            {
                return dynamics.IsReversing;
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

        private Light CreateLight(string name, LightType type, Vector3 localPosition,
            Quaternion localRotation, Color color, float intensity, float range)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(lightingRig, false);
            lightObject.transform.localPosition = localPosition;
            lightObject.transform.localRotation = localRotation;

            Light lightComponent = lightObject.AddComponent<Light>();
            if (lightComponent == null)
            {
                Debug.LogWarning($"[VehicleNightLightingController] Failed to add Light to {name}.");
                return null;
            }

            lightComponent.type = type;
            lightComponent.color = color;
            lightComponent.intensity = intensity;
            lightComponent.range = range;
            lightComponent.enabled = false;
            return lightComponent;
        }

        private void ConfigureSpotLight(Light spot)
        {
            if (spot == null) return;
            spot.spotAngle = headlightSpotAngle;
            spot.innerSpotAngle = headlightSpotAngle * 0.6f;
            spot.shadows = ResolveHeadlightShadowMode();
        }

        private static void SetNoShadows(Light light)
        {
            if (light != null)
            {
                light.shadows = LightShadows.None;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Model Bounds Discovery
        // ─────────────────────────────────────────────────────────────────────

        private bool TryGetModelBounds(out Vector3 localMin, out Vector3 localMax)
        {
            Renderer[] renderers = (modelRoot != null ? modelRoot : transform).GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            localMin = Vector3.zero;
            localMax = Vector3.zero;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                Vector3[] corners =
                {
                    new Vector3(bounds.min.x, bounds.min.y, bounds.min.z),
                    new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
                    new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
                    new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
                    new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                    new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
                    new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
                    new Vector3(bounds.max.x, bounds.max.y, bounds.max.z)
                };

                for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    Vector3 localCorner = transform.InverseTransformPoint(corners[cornerIndex]);
                    if (!hasBounds)
                    {
                        localMin = localCorner;
                        localMax = localCorner;
                        hasBounds = true;
                    }
                    else
                    {
                        localMin = Vector3.Min(localMin, localCorner);
                        localMax = Vector3.Max(localMax, localCorner);
                    }
                }
            }

            return hasBounds;
        }

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

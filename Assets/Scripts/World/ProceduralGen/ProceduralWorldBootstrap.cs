using UnityEngine;

namespace Underground.World
{
    /// <summary>
    /// NEW orchestrator for the AAA Procedural World Generation pipeline.
    /// 
    /// This does NOT replace the existing WorldGenerationBootstrap — that script
    /// still works for the legacy 3-FCG-zone + bezier highway pipeline.
    /// 
    /// ProceduralWorldBootstrap implements the spec from FULL_THROTTLE_WORLD_GEN_AAA_PROCEDURAL.md:
    ///   1. Build seeded world plan (districts, connections, landmarks)
    ///   2. Generate EasyRoads3D road network (highway, arterials, mountain, city)
    ///   3. Populate city districts using FCG (Phase 3)
    ///   4. Ensure bridge connectivity (Phase 4)
    ///   5. Validate driveability
    ///   6. Apply gameplay tags (Phase 5)
    ///
    /// Usage: Add this component to a GameObject in the scene, assign a WorldGenerationConfig
    /// asset, then use the context menu "Execute AAA World Pipeline" to generate.
    /// </summary>
    public class ProceduralWorldBootstrap : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        // CONFIG
        // ─────────────────────────────────────────────────────────────────────

        [Header("Config")]
        [SerializeField, Tooltip("ScriptableObject with all world generation parameters")]
        private WorldGenerationConfig config;

        // ─────────────────────────────────────────────────────────────────────
        // SYSTEMS — assign in inspector or auto-find
        // ─────────────────────────────────────────────────────────────────────

        [Header("Systems")]
        [SerializeField] private GeneratedWorldRegistry registry;
        [SerializeField] private ProceduralWorldPlanner planner;
        [SerializeField] private EasyRoadsNetworkBuilder roadBuilder;
        [SerializeField] private FCGPopulationIntegrator populationIntegrator;
        [SerializeField] private DistrictAssetPopulator assetPopulator;
        [SerializeField] private BridgeConnectorGenerator bridgeConnector;
        [SerializeField] private WorldValidationSuite validationSuite;
        [SerializeField] private GameplayTagSystem gameplayTags;
        [SerializeField] private WorldDebugOverlay debugOverlay;

        // ─────────────────────────────────────────────────────────────────────
        // RUNTIME STATE
        // ─────────────────────────────────────────────────────────────────────

        [Header("State (Read-Only)")]
        [SerializeField] private bool hasGeneratedWorld;
        [SerializeField] private int lastSeed;

        private WorldPlan _currentPlan;

        /// <summary>
        /// The current world plan (null if no generation has run).
        /// </summary>
        public WorldPlan CurrentPlan => _currentPlan;

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Execute the full AAA procedural world generation pipeline.
        /// Can be called from a context menu, editor button, or script.
        /// </summary>
        [ContextMenu("Execute AAA World Pipeline")]
        public void GenerateWorld()
        {
            if (config == null)
            {
                Debug.LogError("[ProceduralBootstrap] ❌ Missing WorldGenerationConfig! " +
                               "Create one via Assets → Create → FullThrottle → World Generation Config");
                return;
            }

            float startTime = Time.realtimeSinceStartup;

            AutoFindSystems();

            // ── STEP 0: Clean previous generation ────────────────────────────
            if (config.clearPreviousGeneration && registry != null)
            {
                Debug.Log("[ProceduralBootstrap] Clearing previous generation...");
                registry.ClearAll();
            }

            // ── STEP 1: Build World Plan ─────────────────────────────────────
            Debug.Log("[ProceduralBootstrap] ══════════════════════════════════════");
            Debug.Log("[ProceduralBootstrap] STEP 1: Building world plan...");
            _currentPlan = planner.BuildPlan(config);
            planner.StashPlanForGizmos(_currentPlan);
            lastSeed = _currentPlan.seed;

            // ── STEP 2: Setup Scene Hierarchy ────────────────────────────────
            Debug.Log("[ProceduralBootstrap] STEP 2: Setting up hierarchy...");
            SetupHierarchy();

            // ── STEP 3: Generate Road Network ────────────────────────────────
            Debug.Log("[ProceduralBootstrap] STEP 3: Generating road network...");
            if (roadBuilder != null)
                roadBuilder.BuildFromPlan(_currentPlan, config);
            else
                Debug.LogWarning("[ProceduralBootstrap] ⚠️ No EasyRoadsNetworkBuilder — skipping roads");

            // ── STEP 4: Populate Districts (Phase 3) ─────────────────────────
            Debug.Log("[ProceduralBootstrap] STEP 4: Populating districts...");
            if (populationIntegrator != null)
                populationIntegrator.Populate(_currentPlan, config);
            
            if (assetPopulator != null)
                assetPopulator.PopulateDistricts(_currentPlan, config);

            // ── STEP 5: Ensure Connectivity (Phase 4) ────────────────────────
            Debug.Log("[ProceduralBootstrap] STEP 5: Checking connectivity...");
            if (bridgeConnector != null)
                bridgeConnector.EnsureConnectivity(_currentPlan, config);

            // ── STEP 6: Validate ─────────────────────────────────────────────
            if (config.runValidationAfterGeneration && validationSuite != null)
            {
                Debug.Log("[ProceduralBootstrap] STEP 6: Validating world...");
                hasGeneratedWorld = validationSuite.Validate(_currentPlan, config);
            }
            else
            {
                hasGeneratedWorld = true;
            }

            // ── STEP 7: Apply Gameplay Metadata (Phase 5) ────────────────────
            Debug.Log("[ProceduralBootstrap] STEP 7: Applying gameplay metadata...");
            if (gameplayTags != null)
                gameplayTags.ApplyGameplayMetadata(_currentPlan, config);

            // ── STEP 8: Attach debug overlay ─────────────────────────────────
            if (config.attachDebugOverlay && debugOverlay != null)
                debugOverlay.SetPlan(_currentPlan);

            float elapsed = Time.realtimeSinceStartup - startTime;
            Debug.Log("[ProceduralBootstrap] ══════════════════════════════════════");
            Debug.Log($"[ProceduralBootstrap] ✅ AAA WORLD GENERATED in {elapsed:F2}s (seed={lastSeed})");
            Debug.Log("[ProceduralBootstrap] ══════════════════════════════════════");
        }

        /// <summary>
        /// Destroy all generated content and reset state.
        /// </summary>
        [ContextMenu("Clear Generated World")]
        public void ClearWorld()
        {
            if (registry != null)
                registry.ClearAll();

            _currentPlan = null;
            hasGeneratedWorld = false;

            Debug.Log("[ProceduralBootstrap] World cleared.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // HIERARCHY SETUP
        //
        // Creates the target scene hierarchy from the spec:
        //   Generated/
        //     RoadNetwork/ (Highway, Arterials, CityRoads, MountainRoads)
        //     Districts/ (MountainDistrict, CityCoreDistrict, ArterialDistrict, HighwayDistrict)
        //     BridgesAndTransitions/
        //     Buildings/, Props/, Foliage/, Lights/, Barriers/, Landmarks/
        //   Debug/
        //     DistrictBounds/, ValidationMarkers/
        // ─────────────────────────────────────────────────────────────────────

        private void SetupHierarchy()
        {
            // Generated root
            GameObject generated = CreateOrFindChild(transform, "Generated");
            if (registry != null) registry.Register(generated);

            // Road network sub-roots
            GameObject roadNetwork = CreateOrFindChild(generated.transform, "RoadNetwork");
            CreateOrFindChild(roadNetwork.transform, "Highway");
            CreateOrFindChild(roadNetwork.transform, "Arterials");
            CreateOrFindChild(roadNetwork.transform, "CityRoads");
            CreateOrFindChild(roadNetwork.transform, "MountainRoads");

            // District roots
            GameObject districts = CreateOrFindChild(generated.transform, "Districts");
            foreach (var d in _currentPlan.districts)
            {
                string districtName = d.districtType + "District";
                GameObject districtObj = CreateOrFindChild(districts.transform, districtName);
                districtObj.transform.position = d.center;
            }

            // Population roots
            CreateOrFindChild(generated.transform, "BridgesAndTransitions");
            CreateOrFindChild(generated.transform, "Buildings");
            CreateOrFindChild(generated.transform, "Props");
            CreateOrFindChild(generated.transform, "Foliage");
            CreateOrFindChild(generated.transform, "Lights");
            CreateOrFindChild(generated.transform, "Barriers");
            CreateOrFindChild(generated.transform, "Landmarks");

            // Debug root
            if (config.drawDebugGizmos)
            {
                GameObject debugRoot = CreateOrFindChild(transform, "Debug");
                CreateOrFindChild(debugRoot.transform, "DistrictBounds");
                CreateOrFindChild(debugRoot.transform, "ValidationMarkers");

                // Place district bound markers
                Transform boundsParent = debugRoot.transform.Find("DistrictBounds");
                foreach (var d in _currentPlan.districts)
                {
                    GameObject marker = CreateOrFindChild(boundsParent, $"Bounds_{d.id}");
                    marker.transform.position = d.center;
                }
            }

            Debug.Log("[ProceduralBootstrap] Scene hierarchy established.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private void AutoFindSystems()
        {
            if (registry == null)
                registry = GetComponentInChildren<GeneratedWorldRegistry>()
                           ?? gameObject.AddComponent<GeneratedWorldRegistry>();

            if (planner == null)
                planner = GetComponentInChildren<ProceduralWorldPlanner>()
                          ?? gameObject.AddComponent<ProceduralWorldPlanner>();

            if (roadBuilder == null)
                roadBuilder = GetComponentInChildren<EasyRoadsNetworkBuilder>()
                              ?? gameObject.AddComponent<EasyRoadsNetworkBuilder>();

            if (populationIntegrator == null)
                populationIntegrator = GetComponentInChildren<FCGPopulationIntegrator>()
                                      ?? gameObject.AddComponent<FCGPopulationIntegrator>();

            if (assetPopulator == null)
                assetPopulator = GetComponentInChildren<DistrictAssetPopulator>()
                                 ?? gameObject.AddComponent<DistrictAssetPopulator>();

            if (bridgeConnector == null)
                bridgeConnector = GetComponentInChildren<BridgeConnectorGenerator>()
                                  ?? gameObject.AddComponent<BridgeConnectorGenerator>();

            if (validationSuite == null)
                validationSuite = GetComponentInChildren<WorldValidationSuite>()
                                  ?? gameObject.AddComponent<WorldValidationSuite>();

            if (gameplayTags == null)
                gameplayTags = GetComponentInChildren<GameplayTagSystem>()
                               ?? gameObject.AddComponent<GameplayTagSystem>();

            if (debugOverlay == null)
                debugOverlay = GetComponentInChildren<WorldDebugOverlay>()
                               ?? gameObject.AddComponent<WorldDebugOverlay>();
        }

        private GameObject CreateOrFindChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            return go;
        }

#if UNITY_EDITOR
        // ─────────────────────────────────────────────────────────────────────
        // GIZMOS — draw world bounds and district overview
        // ─────────────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (_currentPlan == null) return;

            // World bounds
            Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
            Gizmos.DrawWireCube(Vector3.zero,
                new Vector3(_currentPlan.worldSize.x, 100f, _currentPlan.worldSize.y));

            // Seed label
            UnityEditor.Handles.Label(
                Vector3.up * 200f,
                $"Seed: {_currentPlan.seed}\n" +
                $"Districts: {_currentPlan.districts.Count}\n" +
                $"Connections: {_currentPlan.connections.Count}\n" +
                $"Landmarks: {_currentPlan.landmarks.Count}");
        }
#endif
    }
}

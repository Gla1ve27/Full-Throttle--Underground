using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    /// <summary>
    /// PHASE 3: Full FCG integration within bounded city district zones.
    ///
    /// Key rules from the spec (§2.1, §8):
    /// - FCG is used for city massing (buildings, blocks, props) ONLY
    /// - FCG does NOT own final road logic — roads are EasyRoads3D
    /// - FCG-generated road surfaces inside block prefabs must be IGNORED as driving surfaces
    /// - Uses the confirmed FCG API:
    ///     GenerateCity(int size, bool satCity, bool borderFlat)
    ///     GenerateAllBuildings(bool downtown, float downTownSize)
    ///     ClearCity()
    ///   Output root is the "City-Maker" GameObject.
    ///
    /// Pattern copied from the WORKING existing WorldGenerationBootstrap.GenerateFCGZone()
    /// to ensure compatibility, then extended with road suppression and bounded clipping.
    /// </summary>
    public class FCGPopulationIntegrator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform buildingsRoot;
        [SerializeField] private Transform propsRoot;

        [Header("FCG Settings")]
        [Tooltip("FCG city size: 1=VerySmall, 2=Small, 3=Medium, 4=Large")]
        [Range(1, 4)]
        [SerializeField] private int cityCoreSize = 4;

        [Tooltip("Enable downtown high-rise buildings in the city core")]
        [SerializeField] private bool enableHighRises = true;

        [Tooltip("Downtown radius (FCG internal parameter)")]
        [SerializeField] private float downTownSize = 500f;

        [Header("Road Suppression")]
        [Tooltip("Disable colliders on FCG road surfaces. Keep FALSE to let FCG handle city streets — EasyRoads only handles highway/arterial/mountain roads.")]
        [SerializeField] private bool suppressFCGRoads = false;

        [Tooltip("Keywords in object names that identify FCG road surfaces")]
        [SerializeField] private string[] roadKeywords = { "road", "street", "asphalt", "pavement", "crossing", "intersection" };

        [Header("Bounds Clipping")]
        [Tooltip("Remove FCG objects that fall outside the city district bounds")]
        [SerializeField] private bool clipToBounds = true;

        [Tooltip("Padding in metres beyond district radius before clipping")]
        [SerializeField] private float clipPadding = 50f;

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Populate the city core district using FCG, then suppress FCG roads
        /// and clip objects outside district bounds.
        /// </summary>
        public void Populate(WorldPlan plan, WorldGenerationConfig config)
        {
            if (!config.generateCityWithFCG)
            {
                Debug.Log("[FCGIntegrator] FCG generation disabled in config.");
                return;
            }

            DistrictPlan city = plan.GetDistrictByType(DistrictType.CityCore);
            if (city == null)
            {
                Debug.LogWarning("[FCGIntegrator] No CityCore district in plan — skipping FCG.");
                return;
            }

            // Find the FCG CityGenerator in the scene
            FCG.CityGenerator generator = FindFirstObjectByType<FCG.CityGenerator>();
            if (generator == null)
            {
                Debug.LogError("[FCGIntegrator] FCG CityGenerator not found in scene! " +
                               "Make sure the 'Generate' prefab is in the scene.");
                return;
            }

            // Resolve root transforms
            ResolveRoots();

            if (config.logGeneration)
                Debug.Log($"[FCGIntegrator] ═══ Phase 3: FCG city generation ═══");

            // ── Step 1: Generate the city using FCG ──────────────────────────
            GenerateFCGCity(generator, city, config);

            // ── Step 2: Suppress FCG road surfaces ───────────────────────────
            if (suppressFCGRoads)
                SuppressFCGRoads(config);

            // ── Step 3: Clip objects outside district bounds ──────────────────
            if (clipToBounds)
                ClipToBounds(city, config);

            if (config.logGeneration)
                Debug.Log("[FCGIntegrator] ═══ FCG integration complete ═══");
        }

        // ─────────────────────────────────────────────────────────────────────
        // STEP 1: GENERATE CITY
        //
        // This follows the exact proven pattern from the existing
        // WorldGenerationBootstrap.GenerateFCGZone() — reset generator
        // position, call GenerateCity + GenerateAllBuildings, then
        // Instantiate and reposition the output.
        // ─────────────────────────────────────────────────────────────────────

        private void GenerateFCGCity(FCG.CityGenerator generator, DistrictPlan city, WorldGenerationConfig config)
        {
            if (config.logGeneration)
                Debug.Log($"[FCGIntegrator] Generating city: size={cityCoreSize}, " +
                          $"highRises={enableHighRises}, downtown={downTownSize}m");

            // Reset generator to world origin (FCG generates relative to its transform)
            generator.transform.position = Vector3.zero;

            // Generate streets/blocks
            generator.GenerateCity(cityCoreSize, false, false);

            // Generate buildings on the blocks
            generator.GenerateAllBuildings(enableHighRises, enableHighRises ? downTownSize : 0f);

            // Find the FCG output root
            GameObject cityMaker = GameObject.Find("City-Maker");
            if (cityMaker == null)
            {
                Debug.LogError("[FCGIntegrator] City-Maker not found after FCG generation!");
                return;
            }

            // Clone and reposition to the city district center
            GameObject cityClone = Instantiate(cityMaker);
            cityClone.name = "FCG_CityCore";
            cityClone.transform.SetParent(buildingsRoot);
            cityClone.transform.position = city.center;

            // Clean up the original FCG artifact
            cityMaker.SetActive(false);
            cityMaker.name = "City-Maker-Garbage";

            // Also check for any leftover City-Maker-Garbage from previous runs
            CleanupFCGGarbage();

            if (config.logGeneration)
            {
                int childCount = CountChildrenRecursive(cityClone.transform);
                Debug.Log($"[FCGIntegrator] FCG city generated: {childCount} objects at {city.center}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // STEP 2: SUPPRESS FCG ROADS
        //
        // FCG bakes road surfaces into block prefabs. These roads must NOT
        // be used as driving surfaces — EasyRoads3D owns all gameplay roads.
        //
        // Strategy: Find all objects with road-related names and:
        //   - Disable their MeshColliders (so cars don't drive on FCG roads)
        //   - Keep their MeshRenderers (visual filler between buildings is fine)
        //   - Tag them for identification
        // ─────────────────────────────────────────────────────────────────────

        private void SuppressFCGRoads(WorldGenerationConfig config)
        {
            if (buildingsRoot == null) return;

            int suppressedColliders = 0;
            int suppressedRenderers = 0;

            MeshCollider[] colliders = buildingsRoot.GetComponentsInChildren<MeshCollider>(true);
            foreach (MeshCollider mc in colliders)
            {
                string objName = mc.gameObject.name.ToLower();
                if (IsRoadKeyword(objName))
                {
                    mc.enabled = false;
                    suppressedColliders++;
                }
            }

            // Also disable BoxColliders on road objects
            BoxCollider[] boxColliders = buildingsRoot.GetComponentsInChildren<BoxCollider>(true);
            foreach (BoxCollider bc in boxColliders)
            {
                string objName = bc.gameObject.name.ToLower();
                if (IsRoadKeyword(objName))
                {
                    bc.enabled = false;
                    suppressedColliders++;
                }
            }

            if (config.logGeneration)
                Debug.Log($"[FCGIntegrator] Suppressed {suppressedColliders} FCG road colliders " +
                          $"(EasyRoads3D owns all driving surfaces)");
        }

        private bool IsRoadKeyword(string name)
        {
            foreach (string kw in roadKeywords)
            {
                if (name.Contains(kw)) return true;
            }
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // STEP 3: CLIP TO BOUNDS
        //
        // Remove any FCG-generated objects that fall outside the city district
        // bounds. This keeps the city massing contained and prevents buildings
        // from overlapping with other districts.
        // ─────────────────────────────────────────────────────────────────────

        private void ClipToBounds(DistrictPlan city, WorldGenerationConfig config)
        {
            if (buildingsRoot == null) return;

            float clipRadius = city.radius + clipPadding;
            float clipRadiusSq = clipRadius * clipRadius;
            Vector2 cityCenter2D = new Vector2(city.center.x, city.center.z);

            int clipped = 0;
            var toDestroy = new List<GameObject>();

            // Check top-level children of the FCG output (block roots)
            foreach (Transform child in buildingsRoot)
            {
                if (child == null) continue;

                // Check if block center is outside clip radius
                Vector2 childPos2D = new Vector2(child.position.x, child.position.z);
                if ((childPos2D - cityCenter2D).sqrMagnitude > clipRadiusSq)
                {
                    toDestroy.Add(child.gameObject);
                    clipped++;
                }
            }

            // Also clip deeply nested objects (individual buildings)
            Transform fcgCity = buildingsRoot.Find("FCG_CityCore");
            if (fcgCity != null)
            {
                foreach (Transform block in fcgCity)
                {
                    if (block == null) continue;

                    Vector2 blockPos2D = new Vector2(block.position.x, block.position.z);
                    if ((blockPos2D - cityCenter2D).sqrMagnitude > clipRadiusSq)
                    {
                        toDestroy.Add(block.gameObject);
                        clipped++;
                    }
                }
            }

            foreach (var go in toDestroy)
            {
                if (go != null) DestroyImmediate(go);
            }

            if (config.logGeneration)
                Debug.Log($"[FCGIntegrator] Clipped {clipped} objects outside city radius " +
                          $"({clipRadius:F0}m from center)");
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private void ResolveRoots()
        {
            // Auto-find hierarchy roots created by ProceduralWorldBootstrap
            if (buildingsRoot == null)
            {
                Transform generated = transform.parent?.Find("Generated");
                if (generated != null)
                    buildingsRoot = generated.Find("Buildings");
            }

            if (propsRoot == null)
            {
                Transform generated = transform.parent?.Find("Generated");
                if (generated != null)
                    propsRoot = generated.Find("Props");
            }

            // Fallback: create roots if not found
            if (buildingsRoot == null)
            {
                GameObject go = new GameObject("Buildings");
                go.transform.SetParent(transform);
                buildingsRoot = go.transform;
            }

            if (propsRoot == null)
            {
                GameObject go = new GameObject("Props");
                go.transform.SetParent(transform);
                propsRoot = go.transform;
            }
        }

        private void CleanupFCGGarbage()
        {
            // Find and destroy any leftover City-Maker-Garbage from previous runs
            while (true)
            {
                GameObject garbage = GameObject.Find("City-Maker-Garbage");
                if (garbage == null) break;
                DestroyImmediate(garbage);
            }

            // Also destroy any active City-Maker that shouldn't exist
            GameObject cm = GameObject.Find("City-Maker");
            if (cm != null) DestroyImmediate(cm);
        }

        private int CountChildrenRecursive(Transform root)
        {
            int count = root.childCount;
            foreach (Transform child in root)
                count += CountChildrenRecursive(child);
            return count;
        }
    }
}

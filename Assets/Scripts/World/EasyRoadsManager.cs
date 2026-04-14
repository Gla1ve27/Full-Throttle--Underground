using UnityEngine;
using EasyRoads3Dv3;
using System.Collections.Generic;

namespace Underground.World
{
    public class EasyRoadsManager : MonoBehaviour
    {
        [Header("EasyRoads System")]
        public bool rebuildNetworkOnGenerate = true;
        public Material roadMaterial;

        [Header("Infrastructure Tuning")]
        [Tooltip("Vertical threshold (world Y) above which pillars are spawned")]
        public float pillarHeightThreshold = 15f;
        [Tooltip("Arc-length distance between each bridge pillar")]
        public float pillarSpacing = 150f;
        [Tooltip("Arc-length distance between each street light pair")]
        public float lightSpacing = 100f;
        [Tooltip("Arc-length distance between each median barrier block")]
        public float barrierSpacing = 60f;

        private ERRoadNetwork roadNetwork;
        private ERRoadType defaultRoadType;

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        [ContextMenu("Initialize EasyRoads HDRP Network")]
        public void InitializeNetwork()
        {
            roadNetwork = new ERRoadNetwork();

            if (roadMaterial == null)
                roadMaterial = Resources.Load("Materials/roads/road material") as Material;

            ERRoadType[] types = roadNetwork.GetRoadTypes();
            defaultRoadType = (types != null && types.Length > 0) ? types[0] : new ERRoadType();

            // Create a reliable HDRP Lit material for the road to avoid internal EasyRoads shader crashes
            if (roadMaterial == null) 
            {
                roadMaterial = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
                roadMaterial.name = "Autobahn_Asphalt_HDRP";
                roadMaterial.SetColor("_BaseColor", new Color(0.28f, 0.27f, 0.26f));
                if (roadMaterial.HasProperty("_Smoothness")) roadMaterial.SetFloat("_Smoothness", 0.2f);
            }

            defaultRoadType.roadWidth = 45f;
            defaultRoadType.layer    = 0;
            defaultRoadType.tag      = "Untagged";
            // defaultRoadType.roadPhysicsMaterial = roadPhysicsMaterial; // Suppress assignment to avoid set_material exception in modern Unity
            if (roadMaterial != null) defaultRoadType.roadMaterial = roadMaterial;

            Debug.Log($"[ERM] Network initialised. Material: {(roadMaterial != null ? roadMaterial.name : "NULL")}");
        }

        public void Cleanup()
        {
            GameObject erRoot = GameObject.Find("Road Network");
            if (erRoot != null)
            {
                Debug.Log("[ERM] Destroying old Road Network.");
                DestroyImmediate(erRoot);
            }
            roadNetwork = null;
        }

        public void AddProceduralRoad(string name, Vector3[] markers)
        {
            AddProceduralRoad(name, markers, defaultRoadType.roadWidth);
        }

        /// <summary>
        /// Create a procedural road with a custom width.
        /// Used by EasyRoadsNetworkBuilder to set per-road-class widths
        /// (highway=45m, arterial=28m, city=18m, mountain=14m).
        /// </summary>
        public void AddProceduralRoad(string name, Vector3[] markers, float width)
        {
            if (roadNetwork == null) InitializeNetwork();
            if (markers == null || markers.Length < 2)
            {
                Debug.LogWarning($"[ERM] Skipping '{name}': need ≥ 2 markers.");
                return;
            }

            Debug.Log($"[ERM] Creating '{name}' with {markers.Length} markers, width={width:F1}m.");
            ERRoad road = roadNetwork.CreateRoad(name, defaultRoadType, markers);
            if (roadMaterial != null) road.SetMaterial(roadMaterial);
            road.SetWidth(width);
            road.SetTerrainDeformation(false);
            road.SetMeshCollider(false); // BYPASS: Prevent EasyRoads from breaking on Unity 6 PhysicMaterial rename

            for (int i = 0; i < markers.Length; i++)
                road.SetMarkerControlType(i, ERMarkerControlType.Spline);
        }

        public void BuildRoadNetwork()
        {
            if (roadNetwork == null) return;
            Debug.Log("[ERM] Building road network...");
            roadNetwork.BuildRoadNetwork();

            // BYPASS FIX: Add colliders manually after mesh generation to avoid the Unity 6 PhysicMaterial crash in the EasyRoads DLL
            ApplyCollidersManually();

            SpawnHighwayInfrastructure();
        }

        // ─────────────────────────────────────────────────────────────────────
        // PHASE 4 API — Bridge Connector Recovery
        //
        // BridgeConnectorGenerator calls these after Phase 2 road generation to
        // add connector roads that close gaps between districts, then rebuild
        // without duplicating the highway infrastructure pass.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Return all road endpoint positions (spline first + last point) from
        /// every road in the current network.  Used by BridgeConnectorGenerator
        /// to locate existing road terminals and measure gaps between districts.
        /// Returns an empty list — never null — when no network is present.
        /// </summary>
        public List<(string roadName, Vector3 start, Vector3 end)> GetAllRoadEndpoints()
        {
            var result = new List<(string, Vector3, Vector3)>();
            if (roadNetwork == null) return result;

            ERRoad[] roads = roadNetwork.GetRoads();
            if (roads == null) return result;

            foreach (ERRoad road in roads)
            {
                if (road == null) continue;
                Vector3[] spline = road.GetSplinePointsCenter();
                if (spline == null || spline.Length < 2) continue;
                result.Add((road.GetName(), spline[0], spline[spline.Length - 1]));
            }

            Debug.Log($"[ERM] GetAllRoadEndpoints: {result.Count} road(s) sampled.");
            return result;
        }

        /// <summary>
        /// Whether the network has been built (is in build / mesh-generated mode).
        /// </summary>
        public bool HasBuiltNetwork => roadNetwork != null && roadNetwork.isInBuildMode;

        /// <summary>
        /// Re-enter network edit mode, rebuild, and re-apply colliders WITHOUT
        /// re-running SpawnHighwayInfrastructure (avoids duplicate props).
        /// Called by BridgeConnectorGenerator after queuing all connector roads.
        /// </summary>
        public void RebuildNetworkOnly()
        {
            if (roadNetwork == null)
            {
                Debug.LogWarning("[ERM] RebuildNetworkOnly: no active road network — aborting.");
                return;
            }

            // Remove old Highway_Infrastructure before rebuild so it is re-created clean
            CleanupExistingInfrastructure();

            // Restore to edit mode if the network mesh is already built
            if (roadNetwork.isInBuildMode)
            {
                Debug.Log("[ERM] Restoring road network to edit mode for connector rebuild...");
                roadNetwork.RestoreRoadNetwork();
            }

            Debug.Log("[ERM] Rebuilding road network (connector roads included)...");
            roadNetwork.BuildRoadNetwork();
            ApplyCollidersManually();

            // Re-run infra pass — connector roads are NOT named 'Autobahn' so they
            // are skipped by SpawnHighwayInfrastructure's name filter.
            SpawnHighwayInfrastructure();

            Debug.Log("[ERM] RebuildNetworkOnly complete.");
        }

        /// <summary>
        /// Remove Highway_Infrastructure from the Road Network root so it can
        /// be cleanly re-created on the next build without prop duplication.
        /// </summary>
        private void CleanupExistingInfrastructure()
        {
            GameObject erRoot = GameObject.Find("Road Network");
            if (erRoot == null) return;

            Transform infra = erRoot.transform.Find("Highway_Infrastructure");
            if (infra != null)
            {
                Debug.Log("[ERM] Removing stale Highway_Infrastructure before rebuild.");
                DestroyImmediate(infra.gameObject);
            }
        }

        private void ApplyCollidersManually()
        {
            GameObject erRoot = GameObject.Find("Road Network");
            if (erRoot == null) return;

            MeshFilter[] meshFilters = erRoot.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.gameObject.GetComponent<Collider>() == null)
                {
                    MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
            }
            Debug.Log($"[ERM] Applied MeshColliders manually to {meshFilters.Length} road segments.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // INFRASTRUCTURE PASS
        // ─────────────────────────────────────────────────────────────────────

        private void SpawnHighwayInfrastructure()
        {
            GameObject roadNetworkRoot = GameObject.Find("Road Network");
            GameObject infraRoot = new GameObject("Highway_Infrastructure");
            infraRoot.transform.SetParent(roadNetworkRoot != null ? roadNetworkRoot.transform : transform);

            // Create shared materials once (not per-point)
            Material pillarMat = CreateInfraMaterial(new Color(0.22f, 0.22f, 0.25f), 0f);
            Material lightMat  = CreateInfraMaterial(new Color(0.1f,  0.8f,  1.0f), 5f);

            ERRoad[] allRoads = roadNetwork.GetRoads();
            foreach (ERRoad road in allRoads)
            {
                if (!road.GetName().Contains("Autobahn")) continue;

                Vector3[] pts = road.GetSplinePointsCenter();
                if (pts == null || pts.Length < 2) continue;

                Debug.Log($"[ERM] Infrastructure pass on '{road.GetName()}' ({pts.Length} spline pts).");

                // Per-road infrastructure sub-root keeps the hierarchy tidy
                GameObject roadInfra = new GameObject(road.GetName() + "_Infra");
                roadInfra.transform.SetParent(infraRoot.transform);

                SpawnPillarsDistanced(pts, roadInfra.transform, pillarMat);
                SpawnLightsDistanced(pts, roadInfra.transform, lightMat);
                SpawnMedianBarriersDistanced(pts, roadInfra.transform, pillarMat);
            }
        }

        // ── Pillars ─────────────────────────────────────────────────────────
        private void SpawnPillarsDistanced(Vector3[] pts, Transform parent, Material mat)
        {
            float acc = 0f;

            for (int i = 1; i < pts.Length; i++)
            {
                acc += Vector3.Distance(pts[i], pts[i - 1]);
                if (acc < pillarSpacing) continue;
                acc = 0f;

                Vector3 pos = pts[i];
                if (pos.y <= pillarHeightThreshold) continue; // only when elevated

                // Raycast straight down to find the real ground / water surface
                float groundY = 0f;
                if (Physics.Raycast(pos + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 600f,
                                    ~LayerMask.GetMask("Road", "EasyRoads")))
                {
                    groundY = hit.point.y;
                }

                float pillarHeight = pos.y - groundY;
                if (pillarHeight < 4f) continue; // barely elevated — skip

                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = "Bridge_Pillar";
                pillar.transform.SetParent(parent);
                // Centre the cylinder vertically between ground and road underside
                pillar.transform.position = new Vector3(pos.x, groundY + pillarHeight * 0.5f, pos.z);
                // Cylinder height = localScale.y * 2  →  so y-scale = pillarHeight / 2
                pillar.transform.localScale = new Vector3(3.5f, pillarHeight * 0.5f, 3.5f);
                pillar.GetComponent<Renderer>().sharedMaterial = mat;
                // Remove physics from decoration
                Object.DestroyImmediate(pillar.GetComponent<CapsuleCollider>());
            }
        }

        // ── Street Lights ────────────────────────────────────────────────────
        private void SpawnLightsDistanced(Vector3[] pts, Transform parent, Material glowMat)
        {
            float acc = 0f;

            for (int i = 1; i < pts.Length; i++)
            {
                acc += Vector3.Distance(pts[i], pts[i - 1]);
                if (acc < lightSpacing) continue;
                acc = 0f;

                // Local tangent from adjacent points (central difference where possible)
                Vector3 tangent;
                if (i < pts.Length - 1)
                    tangent = (pts[i + 1] - pts[i - 1]).normalized;  // central diff → smooth
                else
                    tangent = (pts[i] - pts[i - 1]).normalized;

                SpawnStreetLightPair(pts[i], tangent, parent, glowMat);
            }
        }

        private void SpawnStreetLightPair(Vector3 pos, Vector3 tangent, Transform parent, Material glow)
        {
            // Right vector perpendicular to road in the horizontal plane
            Vector3 right = Vector3.Cross(tangent, Vector3.up).normalized;

            float poleHeight = 12f;
            float[] sides = { -26f, 26f }; // ± half road-width (road = 45m → edge at ±22.5m)

            foreach (float side in sides)
            {
                Vector3 basePos = pos + right * side;

                // Pole
                GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Light_Pole";
                pole.transform.SetParent(parent);
                pole.transform.position = basePos + Vector3.up * (poleHeight * 0.5f);
                pole.transform.localScale = new Vector3(0.35f, poleHeight * 0.5f, 0.35f);
                pole.GetComponent<Renderer>().sharedMaterial = glow; // reuse (will just glow faintly)
                Object.DestroyImmediate(pole.GetComponent<CapsuleCollider>());

                // Lamp head — offset forward slightly along road so it hangs over lane
                GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lamp.name = "Light_Lamp";
                lamp.transform.SetParent(parent);
                lamp.transform.position = basePos + Vector3.up * poleHeight + tangent * (side > 0 ? 1.5f : -1.5f);
                lamp.transform.localScale = new Vector3(1.4f, 0.6f, 1.4f);
                lamp.GetComponent<Renderer>().sharedMaterial = glow;
                Object.DestroyImmediate(lamp.GetComponent<SphereCollider>());

                // Real HDRP spot light pointing straight down
                Light light = lamp.AddComponent<Light>();
                light.type       = LightType.Spot;
                light.color      = new Color(0.1f, 0.85f, 1f);
                light.intensity  = 2500f;    // HDRP lux
                light.range      = 80f;
                light.spotAngle  = 95f;
                light.transform.rotation = Quaternion.LookRotation(Vector3.down);
            }
        }

        // ── Median Barriers ──────────────────────────────────────────────────
        private void SpawnMedianBarriersDistanced(Vector3[] pts, Transform parent, Material mat)
        {
            float acc = 0f;

            for (int i = 1; i < pts.Length; i++)
            {
                Vector3 prev = pts[i - 1];
                Vector3 curr = pts[i];
                float segLen = Vector3.Distance(prev, curr);
                acc += segLen;
                if (acc < barrierSpacing) continue;
                acc = 0f;

                Vector3 dir = (curr - prev).normalized;
                if (dir == Vector3.zero) continue;

                GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barrier.name = "Median_Barrier";
                barrier.transform.SetParent(parent);
                barrier.transform.position = curr + Vector3.up * 0.6f;
                barrier.transform.rotation = Quaternion.LookRotation(dir);
                barrier.transform.localScale = new Vector3(0.8f, 1.2f, barrierSpacing * 0.5f);
                barrier.GetComponent<Renderer>().sharedMaterial = mat;
                Object.DestroyImmediate(barrier.GetComponent<BoxCollider>());
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // MATERIAL HELPER
        // ─────────────────────────────────────────────────────────────────────

        private Material CreateInfraMaterial(Color color, float emissiveIntensity)
        {
            Material mat = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.color = color;

            if (emissiveIntensity > 0f && mat.HasProperty("_EmissiveColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissiveColor", color * emissiveIntensity);
            }
            return mat;
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    public class WorldGenerationBootstrap : MonoBehaviour
    {
        public FCGZoneController zoneController;
        public PropIntegrationLayer propIntegration;
        public WorldValidationSuite validationSuite;
        public EasyRoadsManager easyRoads;
        public bool useEasyRoads = true;

        [ContextMenu("Execute NFS Heat Pipeline")]
        public void GenerateWorld()
        {
            if (easyRoads == null) easyRoads = FindObjectOfType<EasyRoadsManager>();
            if (easyRoads == null) easyRoads = gameObject.AddComponent<EasyRoadsManager>();

            // ── 1. CLEAN UP PREVIOUS RUNS ────────────────────────────────────
            GameObject oldLegacy = GameObject.Find("Epic_Archipelago_Map");
            if (oldLegacy) DestroyImmediate(oldLegacy);

            GameObject oldMap = GameObject.Find("NFS_Heat_World_Map");
            if (oldMap) DestroyImmediate(oldMap);

            easyRoads?.Cleanup();

            GameObject mapRoot = new GameObject("NFS_Heat_World_Map");

            FCG.CityGenerator cityGenerator = FindObjectOfType<FCG.CityGenerator>();
            if (cityGenerator == null)
            {
                Debug.LogError("[Bootstrap] FCG CityGenerator not found!");
                return;
            }

            // ── 2. FCG ZONES ─────────────────────────────────────────────────
            Debug.Log("[Bootstrap] Spawning Downtown Core...");
            GenerateFCGZone(cityGenerator, "Zone_Downtown",    mapRoot.transform, new Vector3(    0,  0,     0), 4, true);

            Debug.Log("[Bootstrap] Spawning Hills/Drift Zone...");
            GenerateFCGZone(cityGenerator, "Zone_Hills",       mapRoot.transform, new Vector3(-1500, 30,  1500), 2, false);

            Debug.Log("[Bootstrap] Spawning Industrial Zone...");
            GenerateFCGZone(cityGenerator, "Zone_Industrial",  mapRoot.transform, new Vector3( 1500,  0, -1500), 3, false);

            // ── 3. HIGHWAY NETWORK ────────────────────────────────────────────
            Debug.Log("[Bootstrap] Building Autobahn Bridge Network...");
            GameObject highwayRoot = new GameObject("Autobahn_Bridge_Network");
            highwayRoot.transform.SetParent(mapRoot.transform);

            easyRoads?.InitializeNetwork();

            // Control points push deep into each island's urban tissue.
            // Each highway is defined by 4 Cubic Bezier control points:
            //   p0 = island exit  |  p1,p2 = curve handles  |  p3 = island entry

            // Bridge 1: Downtown → Hills  (graceful left-climbing S-arc)
            Vector3[] b1 = {
                new Vector3( -800,   0,   800),   // Downtown west exit
                new Vector3( -500, 140,  1100),   // lift off, sweep north
                new Vector3(-1200, 110,   700),   // descend, sweep west
                new Vector3(-1300,  30,  1300),   // Hills east entry
            };

            // Bridge 2: Downtown → Industrial  (right-descending S-arc)
            Vector3[] b2 = {
                new Vector3(  800,   0,  -800),   // Downtown east exit
                new Vector3( 1100, 140,  -500),   // lift off, sweep east
                new Vector3(  700, 110, -1200),   // descend, sweep south
                new Vector3( 1300,   0, -1300),   // Industrial north entry
            };

            // Bridge 3: Hills → Industrial  (the Outer Ring — highest arc)
            Vector3[] b3 = {
                new Vector3(-1500,  35,  1500),   // Hills south exit
                new Vector3(  200, 185,  1600),   // high apex, sweep east
                new Vector3( 1600, 185,   200),   // still high, sweep south
                new Vector3( 1500,   0, -1500),   // Industrial west entry
            };

            SpawnDividedHighway("Autobahn_Downtown_Hills",       b1, highwayRoot.transform);
            SpawnDividedHighway("Autobahn_Downtown_Industrial",  b2, highwayRoot.transform);
            SpawnDividedHighway("Autobahn_Hills_Industrial",     b3, highwayRoot.transform);

            // Perimeter ring roads
            SpawnIslandRing("Downtown_Ring",    new Vector3(    0,  0,     0), 650f);
            SpawnIslandRing("Hills_Ring",       new Vector3(-1500, 30,  1500), 550f);
            SpawnIslandRing("Industrial_Ring",  new Vector3( 1500,  0, -1500), 550f);

            if (useEasyRoads && easyRoads != null)
                easyRoads.BuildRoadNetwork();

            // ── 4. SURGICAL BULLDOZER ─────────────────────────────────────────
            Debug.Log("[Bootstrap] Running XZ-projected building clearance...");
            float bRadius = 100f;
            int   bSteps  = 80;
            ClearBuildingsAlongPath(GetSBezierPoints(b1, bSteps), bRadius);
            ClearBuildingsAlongPath(GetSBezierPoints(b2, bSteps), bRadius);
            ClearBuildingsAlongPath(GetSBezierPoints(b3, bSteps), bRadius);

            // Endpoint radial clearance
            foreach (Vector3[] bridge in new[] { b1, b2, b3 })
            {
                ClearBuildingsAt(bridge[0],                   130f);
                ClearBuildingsAt(bridge[bridge.Length - 1],   130f);
            }

            // Clean FCG generator artefact
            GameObject leftOver = GameObject.Find("City-Maker");
            if (leftOver) DestroyImmediate(leftOver);

            Debug.Log("[Bootstrap] ✅ NFS HEAT WORLD GENERATED SUCCESSFULLY!");
        }

        // ─────────────────────────────────────────────────────────────────────
        // FCG ZONE GENERATOR
        // ─────────────────────────────────────────────────────────────────────

        private void GenerateFCGZone(FCG.CityGenerator generator, string name, Transform root,
                                      Vector3 position, int size, bool hasHighRises)
        {
            generator.transform.position = Vector3.zero;
            generator.GenerateCity(size, false, false);
            generator.GenerateAllBuildings(hasHighRises, hasHighRises ? 500f : 0f);

            GameObject origin = GameObject.Find("City-Maker");
            if (origin != null)
            {
                GameObject city = Instantiate(origin);
                city.name = name;
                city.transform.SetParent(root);
                city.transform.position = position;

                origin.SetActive(false);
                origin.name = "City-Maker-Garbage";
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // DIVIDED HIGHWAY  ← THE KEY FIX IS HERE
        //
        // The old code computed a single "right" vector from start→end, then
        // applied that same offset to every control point.  On an S-curve the
        // inner control points are NOT on that straight line, so the left/right
        // offsets pinch together and produce "W" kinks.
        //
        // Fix: compute the local tangent at each control point independently
        // (central-difference for interior points, forward/backward for ends),
        // then derive a per-point right vector.  This keeps the two carriageways
        // truly parallel at every waypoint.
        // ─────────────────────────────────────────────────────────────────────

        private void SpawnDividedHighway(string name, Vector3[] pts, Transform parent)
        {
            if (pts == null || pts.Length < 2) return;

            const float sideOffset = 45f;   // half the median gap (road width = 45m each)

            Vector3[] leftPts  = new Vector3[pts.Length];
            Vector3[] rightPts = new Vector3[pts.Length];

            for (int i = 0; i < pts.Length; i++)
            {
                // ── Local tangent via finite differences ──────────────────────
                Vector3 tangent;
                if (i == 0)
                    tangent = pts[1] - pts[0];                          // forward diff
                else if (i == pts.Length - 1)
                    tangent = pts[i] - pts[i - 1];                      // backward diff
                else
                    tangent = pts[i + 1] - pts[i - 1];                  // central diff  ← smoothest

                tangent.Normalize();
                if (tangent == Vector3.zero) tangent = Vector3.forward; // safety fallback

                // Perpendicular in the horizontal plane
                Vector3 right = Vector3.Cross(tangent, Vector3.up).normalized;

                leftPts[i]  = pts[i] - right * sideOffset;
                rightPts[i] = pts[i] + right * sideOffset;
            }

            if (useEasyRoads && easyRoads != null)
            {
                easyRoads.AddProceduralRoad(name + "_Left",  leftPts);
                easyRoads.AddProceduralRoad(name + "_Right", rightPts);
            }
            else
            {
                // Legacy fallback — straight span between first/last
                BuildBridgeSpan(parent, pts[0], (pts[0] + pts[pts.Length - 1]) / 2f,
                                pts[pts.Length - 1], name);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ISLAND RING ROAD
        // ─────────────────────────────────────────────────────────────────────

        private void SpawnIslandRing(string name, Vector3 center, float radius)
        {
            if (!useEasyRoads || easyRoads == null) return;

            const int pts = 12; // More points = rounder ring
            Vector3[] ring = new Vector3[pts + 1];
            for (int i = 0; i < pts; i++)
            {
                float angle = i * Mathf.PI * 2f / pts;
                ring[i] = center + new Vector3(Mathf.Cos(angle) * radius, 2f, Mathf.Sin(angle) * radius);
            }
            ring[pts] = ring[0]; // close loop
            easyRoads.AddProceduralRoad(name, ring);
        }

        // ─────────────────────────────────────────────────────────────────────
        // BEZIER SAMPLERS
        // ─────────────────────────────────────────────────────────────────────

        private List<Vector3> GetSBezierPoints(Vector3[] p, int steps)
        {
            var pts = new List<Vector3>(steps + 1);
            if (p.Length < 4) return pts;

            for (int i = 0; i <= steps; i++)
            {
                float t  = i / (float)steps;
                // Cubic Bernstein evaluation
                float u  = 1f - t;
                float b0 = u * u * u;
                float b1 = 3f * u * u * t;
                float b2 = 3f * u * t * t;
                float b3 = t * t * t;
                pts.Add(b0 * p[0] + b1 * p[1] + b2 * p[2] + b3 * p[3]);
            }
            return pts;
        }

        private List<Vector3> GetBezierPoints(Vector3 p0, Vector3 mid, Vector3 p1, int steps)
        {
            var pts = new List<Vector3>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float t  = i / (float)steps;
                Vector3 m0 = Vector3.Lerp(p0, mid, t);
                Vector3 m1 = Vector3.Lerp(mid, p1, t);
                pts.Add(Vector3.Lerp(m0, m1, t));
            }
            return pts;
        }

        // ─────────────────────────────────────────────────────────────────────
        // SURGICAL BULLDOZER
        // ─────────────────────────────────────────────────────────────────────

        private void ClearBuildingsAlongPath(List<Vector3> points, float radius)
        {
            foreach (Vector3 p in points) ClearBuildingsAt(p, radius);
        }

        private void ClearBuildingsAt(Vector3 point, float radius)
        {
            float sqrR    = radius * radius;
            Vector2 p2D   = new Vector2(point.x, point.z);

            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (Renderer rr in renderers)
            {
                if (rr == null || rr.gameObject == null) continue;

                string n = rr.gameObject.name.ToLower();
                if (!n.Contains("block") && !n.Contains("build") && !n.Contains("prop") &&
                    !n.Contains("fcg")   && !n.Contains("mesh")  && !n.Contains("house") &&
                    !n.Contains("structure")) continue;

                Vector2 bPos = new Vector2(rr.transform.position.x, rr.transform.position.z);
                if ((bPos - p2D).sqrMagnitude > sqrR) continue;

                Transform root = rr.transform;
                // Never destroy road or highway objects
                if (root.name.Contains("Road Network") || root.name.Contains("Autobahn")) continue;

                // Walk up to the building root (one level of FCG hierarchy)
                if (root.parent != null)
                {
                    string pn = root.parent.name.ToLower();
                    if (pn.Contains("building") || pn.Contains("prop") || pn.Contains("fcg"))
                        root = root.parent;
                }

                if (root != null && root.gameObject != null)
                    DestroyImmediate(root.gameObject);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // LEGACY BRIDGE BUILDER (fallback when EasyRoads is disabled)
        // ─────────────────────────────────────────────────────────────────────

        private void BuildBridgeSpan(Transform root, Vector3 p0, Vector3 mid, Vector3 p1, string name)
        {
            GameObject bridgeObj = new GameObject(name);
            bridgeObj.transform.SetParent(root);

            List<Vector3> points = GetBezierPoints(p0, mid, p1, 80);
            Material asphaltMat  = CreateHDRPMaterial(new Color(0.12f, 0.12f, 0.12f));
            Material barrierMat  = CreateHDRPMaterial(new Color(0.70f, 0.70f, 0.70f));

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 start = points[i];
                Vector3 end   = points[i + 1];
                Vector3 dir   = end - start;
                float   dist  = dir.magnitude;
                if (dist < 0.1f) continue;

                Vector3 right = Vector3.Cross(dir.normalized, Vector3.up);

                // Road surface
                SpawnAlignedCube(bridgeObj.transform, start + dir * 0.5f, dir.normalized, new Vector3(50, 2, dist), asphaltMat, false);
                // Barriers
                SpawnAlignedCube(bridgeObj.transform, start + dir * 0.5f - right * 24f + Vector3.up * 2f, dir.normalized, new Vector3(2, 4, dist), barrierMat, false);
                SpawnAlignedCube(bridgeObj.transform, start + dir * 0.5f + right * 24f + Vector3.up * 2f, dir.normalized, new Vector3(2, 4, dist), barrierMat, false);
            }
        }

        private void SpawnAlignedCube(Transform parent, Vector3 pos, Vector3 forward,
                                       Vector3 scale, Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent);
            go.transform.position   = pos;
            go.transform.forward    = forward;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        }

        private Material CreateHDRPMaterial(Color color)
        {
            Material mat = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else mat.color = color;
            return mat;
        }
    }
}
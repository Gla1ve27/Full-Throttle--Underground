using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    // ─────────────────────────────────────────────────────────────────────────
    // PHASE 4 — BRIDGE CONNECTOR RECOVERY
    //
    // After Phase 2 generates the EasyRoads network from the WorldPlan, this
    // system performs two passes:
    //
    //   Pass A — Gap Detection
    //     For every planned RoadConnectionPlan, find the actual ER3D road
    //     endpoints closest to the connection's planned start / end positions.
    //     If those endpoints are further apart than maxGapThreshold, a gap
    //     exists and a connector must be built.
    //
    //   Pass B — Connector Generation
    //     Track 1 (preferred): queue the gap as a new ER3D road, then call
    //       EasyRoadsManager.AddConnectorsAndRebuild() once at the end so the
    //       whole network rebuilds in one shot instead of N times.
    //     Track 2 (fallback / elevated spans): generate physical road-deck
    //       geometry (cubes WITH their BoxColliders kept) so a car can
    //       physically drive across the gap even without ER3D.
    //
    // Spec refs: §6 Steps 7-8, §8 FCG Bridging Rule, §4.4 Layer D, §2.2.
    // ─────────────────────────────────────────────────────────────────────────
    public class BridgeConnectorGenerator : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Gap Detection")]
        [Tooltip("Gaps wider than this trigger connector generation (metres)")]
        [SerializeField] private float maxGapThreshold = 150f;

        [Tooltip("Radius around a planned connection endpoint used to find nearby road endpoints")]
        [SerializeField] private float endpointSearchRadius = 400f;

        [Tooltip("Two endpoints closer than this are considered 'touching' — no connector needed")]
        [SerializeField] private float connectionTolerance = 30f;

        [Header("ER3D Connector Roads (Track 1)")]
        [Tooltip("Attempt to add connector roads into the ER3D network before rebuilding")]
        [SerializeField] private bool useERConnectors = true;

        [Tooltip("Road width for ER3D connector roads (metres)")]
        [SerializeField] private float erConnectorWidth = 20f;

        [Tooltip("Curve handle strength for connector splines (0 = straight, 1 = strongly curved)")]
        [Range(0f, 0.5f)]
        [SerializeField] private float connectorCurveStrength = 0.2f;

        [Header("Physical Bridge Decks (Track 2)")]
        [Tooltip("Generate physical road-deck cubes (colliders kept) as driveable fallback")]
        [SerializeField] private bool generatePhysicalBridges = true;

        [Tooltip("Width of a physical bridge deck — must be wide enough for a car")]
        [SerializeField] private float bridgeDeckWidth = 30f;

        [Tooltip("How many deck segments the bridge is divided into")]
        [SerializeField] private int deckSegments = 24;

        [Tooltip("Thickness of the bridge deck cubes")]
        [SerializeField] private float deckThickness = 2f;

        [Tooltip("Height of side barrier rails on physical bridges")]
        [SerializeField] private float railHeight = 1.2f;

        [Header("Approach Ramps")]
        [Tooltip("Elevation difference above which approach ramps are generated at each bridge end")]
        [SerializeField] private float rampElevationThreshold = 4f;

        [Tooltip("Horizontal length of each approach ramp")]
        [SerializeField] private float rampLength = 60f;

        [Tooltip("Number of tilted segments in each ramp")]
        [SerializeField] private int rampSegments = 5;

        [Header("State (Read-Only Debug)")]
        [SerializeField] private int gapsDetected;
        [SerializeField] private int erConnectorsQueued;
        [SerializeField] private int physicalBridgesBuilt;

        // Shared runtime materials — allocated once per run
        private Material deckMat;
        private Material barrierMat;
        private Material rampMat;

        // ── Internal record type ──────────────────────────────────────────────

        private struct EndpointRecord
        {
            public string roadName;
            public Vector3 start;
            public Vector3 end;
        }

        private enum GapResult { Connected, GapDetected, NoEndpoints }

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC ENTRY POINT
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Check all planned connections for physical gaps and repair them.
        /// Called by ProceduralWorldBootstrap after Phase 2 road generation.
        /// </summary>
        public void EnsureConnectivity(WorldPlan plan, WorldGenerationConfig config)
        {
            gapsDetected        = 0;
            erConnectorsQueued  = 0;
            physicalBridgesBuilt = 0;

            if (plan == null)
            {
                Debug.LogError("[BridgeConnector] WorldPlan is null — aborting.");
                return;
            }

            if (config.logGeneration)
                Debug.Log("[BridgeConnector] ═══ Phase 4: Bridge connector recovery ═══");

            InitMaterials();

            Transform bridgeRoot = ResolveBridgeRoot();
            EasyRoadsManager erm = FindFirstObjectByType<EasyRoadsManager>();

            // Collect all road endpoint positions from the ER3D network
            List<EndpointRecord> endpoints = CollectEndpoints(erm, config);

            if (config.logGeneration)
                Debug.Log($"[BridgeConnector] Collected {endpoints.Count} road endpoints " +
                          $"from ER3D network.");

            // Accumulated ER3D connector roads — committed in one rebuild at end
            var pendingERRoads = new List<(string name, Vector3[] markers, float width)>();

            // ── Scan every planned connection ─────────────────────────────────
            foreach (var conn in plan.connections)
            {
                var (result, nearStart, nearEnd) = EvaluateConnection(conn, endpoints);

                if (config.logGeneration)
                {
                    string icon = result == GapResult.Connected ? "✅" : "⚠️ ";
                    Debug.Log($"[BridgeConnector] {icon} {conn}  →  result={result}  " +
                              $"gap={(result == GapResult.Connected ? 0f : Vector3.Distance(nearStart, nearEnd)):F0}m");
                }

                if (result == GapResult.Connected)
                    continue;

                gapsDetected++;

                float elevDiff = Mathf.Abs(nearEnd.y - nearStart.y);
                float gap      = Vector3.Distance(nearStart, nearEnd);

                // ── Track 1: ER3D connector road ──────────────────────────────
                if (useERConnectors && erm != null)
                {
                    string connName = $"Connector_{conn.fromDistrictId}_{conn.toDistrictId}_{erConnectorsQueued}";
                    Vector3[] spline = BuildConnectorSpline(nearStart, nearEnd);
                    pendingERRoads.Add((connName, spline, erConnectorWidth));
                    erConnectorsQueued++;

                    if (config.logGeneration)
                        Debug.Log($"[BridgeConnector] Queued ER3D connector '{connName}' " +
                                  $"({gap:F0}m, elevDiff={elevDiff:F1}m)");
                }

                // ── Track 2: Physical bridge deck ─────────────────────────────
                //    Always build physical geometry for elevated spans.
                //    Also build as sole solution when ER3D connectors are disabled.
                bool needsPhysical = generatePhysicalBridges &&
                                     (!useERConnectors || erm == null || elevDiff > rampElevationThreshold);

                if (needsPhysical)
                {
                    string bridgeName = $"Bridge_{conn.fromDistrictId}_{conn.toDistrictId}_{physicalBridgesBuilt}";
                    BuildPhysicalBridge(nearStart, nearEnd, bridgeRoot, bridgeName, config);
                    physicalBridgesBuilt++;
                }
            }

            // ── Commit all ER3D connectors in a single rebuild ────────────────
            if (pendingERRoads.Count > 0 && erm != null)
            {
                if (config.logGeneration)
                    Debug.Log($"[BridgeConnector] Committing {pendingERRoads.Count} ER3D connector(s) " +
                              $"via single network rebuild...");

                foreach (var (name, markers, width) in pendingERRoads)
                    erm.AddProceduralRoad(name, markers, width);

                erm.RebuildNetworkOnly();

                if (config.logGeneration)
                    Debug.Log("[BridgeConnector] ER3D rebuild complete.");
            }

            // ── Final summary ─────────────────────────────────────────────────
            if (config.logGeneration)
            {
                Debug.Log("[BridgeConnector] ═══ Recovery summary ═══");
                Debug.Log($"[BridgeConnector]  Gaps detected:          {gapsDetected}");
                Debug.Log($"[BridgeConnector]  ER3D connectors queued: {erConnectorsQueued}");
                Debug.Log($"[BridgeConnector]  Physical bridges built: {physicalBridgesBuilt}");

                int totalConnectors = erConnectorsQueued + physicalBridgesBuilt;
                if (gapsDetected == 0)
                    Debug.Log("[BridgeConnector] ✅ No gaps found — network is fully connected.");
                else if (totalConnectors >= gapsDetected)
                    Debug.Log("[BridgeConnector] ✅ All gaps have been bridged.");
                else
                    Debug.LogWarning($"[BridgeConnector] ⚠️  {gapsDetected - totalConnectors} " +
                                     $"gap(s) remain — check scene for disconnected roads.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // GAP EVALUATION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluate a single planned connection against the actual road endpoint set.
        /// Returns the gap result and the best candidate bridge start/end positions.
        /// </summary>
        private (GapResult result, Vector3 nearStart, Vector3 nearEnd)
            EvaluateConnection(RoadConnectionPlan conn, List<EndpointRecord> endpoints)
        {
            // Find the road endpoint nearest to each planned connection terminal
            bool foundStart = FindNearestEndpoint(conn.start, endpoints, endpointSearchRadius,
                                                   out Vector3 nearStart);
            bool foundEnd   = FindNearestEndpoint(conn.end,   endpoints, endpointSearchRadius,
                                                   out Vector3 nearEnd);

            // Fall back to plan positions if no road endpoint is nearby
            if (!foundStart) nearStart = conn.start;
            if (!foundEnd)   nearEnd   = conn.end;

            // If neither side has road endpoints, flag as no-endpoints (Phase 2 may have failed)
            if (!foundStart && !foundEnd)
                return (GapResult.NoEndpoints, nearStart, nearEnd);

            float dist = Vector3.Distance(nearStart, nearEnd);

            if (dist <= connectionTolerance)
                return (GapResult.Connected, nearStart, nearEnd);

            if (dist <= maxGapThreshold)
                return (GapResult.Connected, nearStart, nearEnd); // close enough — within spec tolerance

            return (GapResult.GapDetected, nearStart, nearEnd);
        }

        /// <summary>
        /// Find the road endpoint in the list closest to searchCenter within searchRadius.
        /// Checks both the start and end position of each endpoint record.
        /// </summary>
        private bool FindNearestEndpoint(Vector3 searchCenter, List<EndpointRecord> endpoints,
                                          float searchRadius, out Vector3 result)
        {
            result = Vector3.zero;
            float bestSqr = searchRadius * searchRadius;
            bool found = false;

            foreach (var ep in endpoints)
            {
                float dStart = (ep.start - searchCenter).sqrMagnitude;
                if (dStart < bestSqr)
                {
                    bestSqr = dStart;
                    result  = ep.start;
                    found   = true;
                }

                float dEnd = (ep.end - searchCenter).sqrMagnitude;
                if (dEnd < bestSqr)
                {
                    bestSqr = dEnd;
                    result  = ep.end;
                    found   = true;
                }
            }

            return found;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ENDPOINT COLLECTION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Query EasyRoadsManager for all built road spline endpoints.
        /// Returns empty list (not null) if no network is available.
        /// </summary>
        private List<EndpointRecord> CollectEndpoints(EasyRoadsManager erm, WorldGenerationConfig config)
        {
            var list = new List<EndpointRecord>();
            if (erm == null) return list;

            var rawEndpoints = erm.GetAllRoadEndpoints();
            foreach (var (roadName, start, end) in rawEndpoints)
            {
                list.Add(new EndpointRecord { roadName = roadName, start = start, end = end });
            }

            if (list.Count == 0 && config.logGeneration)
                Debug.LogWarning("[BridgeConnector] No road endpoints found — " +
                                 "Phase 2 may not have run or ER3D network is in edit mode.");

            return list;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ER3D CONNECTOR SPLINE BUILDER
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Build a smooth cubic Bezier spline between two endpoints for an ER3D connector road.
        /// Uses perpendicular curve handles proportional to gap distance so the road
        /// sweeps naturally rather than cutting across as a straight line.
        /// </summary>
        private Vector3[] BuildConnectorSpline(Vector3 start, Vector3 end)
        {
            Vector3 dir = end - start;
            float   dist = dir.magnitude;

            if (dist < 0.1f)
                return new[] { start, end };

            Vector3 dirN = dir.normalized;
            Vector3 perp = Vector3.Cross(dirN, Vector3.up).normalized;

            // Curve handles offset laterally for organic S-curve feel
            float handleDist  = dist * 0.33f;
            float sideOffset1 = dist * connectorCurveStrength;
            float sideOffset2 = -sideOffset1;

            Vector3 h1 = start + dirN * handleDist + perp * sideOffset1;
            Vector3 h2 = end   - dirN * handleDist + perp * sideOffset2;

            // Elevation mid-point: blend start and end heights smoothly
            h1.y = Mathf.Lerp(start.y, end.y, 0.33f);
            h2.y = Mathf.Lerp(start.y, end.y, 0.66f);

            // Sample cubic Bezier to 12 uniform-t points
            var samples = new List<Vector3>(13);
            for (int i = 0; i <= 12; i++)
            {
                float t  = i / 12f;
                float u  = 1f - t;
                float u2 = u * u;
                float t2 = t * t;
                samples.Add(u2 * u * start
                          + 3f * u2 * t * h1
                          + 3f * u * t2 * h2
                          + t2 * t * end);
            }

            // Resample to uniform arc-length spacing (8 final markers)
            return SplineUtils.ResampleUniform(samples, 8);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PHYSICAL BRIDGE BUILDER
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Build a driveable physical bridge between two world positions.
        ///
        /// Structure (per bridge):
        ///   BridgeName/
        ///     Approach_Start/      — tilted ramp cubes climbing to deck height (if elevDiff > threshold)
        ///     Deck_Segments/       — flat deck cubes WITH BoxColliders (car drives on these)
        ///     Approach_End/        — tilted ramp cubes descending from deck height
        ///     Rail_Left/           — barrier rail cubes along deck (NO collider — visual only)
        ///     Rail_Right/          — barrier rail cubes along deck (NO collider — visual only)
        ///
        /// The deck cubes keep their BoxColliders so the car can physically drive across.
        /// The rail cubes have their BoxColliders removed — they are visual dressing only.
        /// This matches the spec requirement (§8): "road entry point exists, road exit point
        /// exists, collider continuity exists, player can enter, pass through, and exit."
        /// </summary>
        private void BuildPhysicalBridge(Vector3 start, Vector3 end, Transform bridgeRoot,
                                          string bridgeName, WorldGenerationConfig config)
        {
            float elevDiff = end.y - start.y; // positive = uphill toward end
            bool needRamps = Mathf.Abs(elevDiff) > rampElevationThreshold;

            // Bridge deck runs at the higher of the two endpoint heights
            float deckY = Mathf.Max(start.y, end.y);

            // Ramp endpoints: where the flat deck meets the approach slope
            Vector3 dir    = (end - start);
            float   distXZ = new Vector2(dir.x, dir.z).magnitude;
            Vector3 dirN   = new Vector3(dir.x, 0f, dir.z).normalized;

            Vector3 deckStart = new Vector3(start.x + dirN.x * rampLength, deckY,
                                            start.z + dirN.z * rampLength);
            Vector3 deckEnd   = new Vector3(end.x   - dirN.x * rampLength, deckY,
                                            end.z   - dirN.z * rampLength);

            if (!needRamps)
            {
                deckStart = start;
                deckEnd   = end;
                deckStart.y = deckY;
                deckEnd.y   = deckY;
            }

            // Create bridge root object
            GameObject bridgeObj = new GameObject(bridgeName);
            bridgeObj.transform.SetParent(bridgeRoot);
            bridgeObj.transform.position = Vector3.zero;

            if (config.logGeneration)
                Debug.Log($"[BridgeConnector] Building physical bridge '{bridgeName}' " +
                          $"{Vector3.Distance(start, end):F0}m, elevDiff={elevDiff:F1}m, " +
                          $"ramps={needRamps}");

            // ── Approach ramp: Start side ─────────────────────────────────────
            if (needRamps)
                BuildApproachRamp(start, deckStart, bridgeObj.transform, "Approach_Start");

            // ── Main deck ─────────────────────────────────────────────────────
            BuildDeckSegments(deckStart, deckEnd, bridgeObj.transform, "Deck");

            // ── Approach ramp: End side ───────────────────────────────────────
            if (needRamps)
                BuildApproachRamp(deckEnd, end, bridgeObj.transform, "Approach_End");

            // ── Barrier rails (visual) ────────────────────────────────────────
            BuildBarrierRails(deckStart, deckEnd, bridgeObj.transform);
        }

        // ── Deck segments ────────────────────────────────────────────────────

        private void BuildDeckSegments(Vector3 deckStart, Vector3 deckEnd,
                                        Transform parent, string rootName)
        {
            GameObject deckRoot = new GameObject(rootName);
            deckRoot.transform.SetParent(parent);

            Vector3 dir    = deckEnd - deckStart;
            float   length = dir.magnitude;
            if (length < 0.01f) return;

            Vector3 forward  = dir.normalized;
            float   segLen   = length / deckSegments;

            for (int i = 0; i < deckSegments; i++)
            {
                float t = (i + 0.5f) / deckSegments;
                Vector3 segCenter = Vector3.Lerp(deckStart, deckEnd, t);

                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"Deck_{i:00}";
                seg.transform.SetParent(deckRoot.transform);
                seg.transform.position   = segCenter + Vector3.down * (deckThickness * 0.5f);
                seg.transform.rotation   = Quaternion.LookRotation(forward);
                seg.transform.localScale = new Vector3(bridgeDeckWidth, deckThickness, segLen + 0.05f);
                seg.GetComponent<Renderer>().sharedMaterial = deckMat;

                // KEEP BoxCollider — this is what cars drive on
                // (do NOT DestroyImmediate the BoxCollider here)
            }
        }

        // ── Approach ramp ────────────────────────────────────────────────────

        private void BuildApproachRamp(Vector3 rampBase, Vector3 rampTop,
                                        Transform parent, string rootName)
        {
            GameObject rampRoot = new GameObject(rootName);
            rampRoot.transform.SetParent(parent);

            float heightDiff = rampTop.y - rampBase.y;
            float horizDist  = Vector3.Distance(
                new Vector3(rampBase.x, 0f, rampBase.z),
                new Vector3(rampTop.x,  0f, rampTop.z));

            if (horizDist < 0.01f) return;

            Vector3 horizDir = new Vector3(rampTop.x - rampBase.x, 0f, rampTop.z - rampBase.z).normalized;

            for (int i = 0; i < rampSegments; i++)
            {
                float t0 = i         / (float)rampSegments;
                float t1 = (i + 1f)  / (float)rampSegments;

                Vector3 segStart = Vector3.Lerp(rampBase, rampTop, t0);
                Vector3 segEnd   = Vector3.Lerp(rampBase, rampTop, t1);
                Vector3 segDir   = (segEnd - segStart).normalized;
                float   segLen   = Vector3.Distance(segStart, segEnd);

                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"Ramp_{i:00}";
                seg.transform.SetParent(rampRoot.transform);
                seg.transform.position   = (segStart + segEnd) * 0.5f - Vector3.up * (deckThickness * 0.5f);
                seg.transform.rotation   = Quaternion.LookRotation(segDir);
                seg.transform.localScale = new Vector3(bridgeDeckWidth, deckThickness, segLen + 0.05f);
                seg.GetComponent<Renderer>().sharedMaterial = rampMat;

                // KEEP BoxCollider on ramps too — they are driveable
            }
        }

        // ── Barrier rails (visual, no collider) ─────────────────────────────

        private void BuildBarrierRails(Vector3 deckStart, Vector3 deckEnd, Transform parent)
        {
            Vector3 dir   = (deckEnd - deckStart);
            float   len   = dir.magnitude;
            if (len < 0.01f) return;

            Vector3 forward = dir.normalized;
            Vector3 right   = Vector3.Cross(forward, Vector3.up).normalized;
            Vector3 center  = (deckStart + deckEnd) * 0.5f + Vector3.up * railHeight;

            float sideOffset = bridgeDeckWidth * 0.5f + 0.5f;

            foreach (float side in new[] { -sideOffset, sideOffset })
            {
                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = side < 0 ? "Rail_Left" : "Rail_Right";
                rail.transform.SetParent(parent);
                rail.transform.position   = center + right * side;
                rail.transform.rotation   = Quaternion.LookRotation(forward);
                rail.transform.localScale = new Vector3(0.3f, railHeight, len);
                rail.GetComponent<Renderer>().sharedMaterial = barrierMat;
                DestroyImmediate(rail.GetComponent<BoxCollider>()); // visual-only
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // HIERARCHY HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private Transform ResolveBridgeRoot()
        {
            // Walk up looking for the Generated root created by ProceduralWorldBootstrap
            Transform t = transform;
            while (t.parent != null)
                t = t.parent;

            // Check common names in the scene hierarchy
            foreach (string rootName in new[] { "ProceduralWorldBootstrap", "GenerationBootstrap", "World" })
            {
                Transform candidate = t.Find(rootName) ?? GameObject.Find(rootName)?.transform;
                if (candidate != null)
                {
                    Transform gen = candidate.Find("Generated");
                    if (gen != null)
                    {
                        Transform bridges = gen.Find("BridgesAndTransitions");
                        if (bridges != null) return bridges;
                    }
                }
            }

            // Direct search in scene
            GameObject genObj = GameObject.Find("Generated");
            if (genObj != null)
            {
                Transform bridges = genObj.transform.Find("BridgesAndTransitions");
                if (bridges != null) return bridges;

                // Create it under Generated if missing
                GameObject bt = new GameObject("BridgesAndTransitions");
                bt.transform.SetParent(genObj.transform);
                bt.transform.localPosition = Vector3.zero;
                return bt.transform;
            }

            // Ultimate fallback: attach to this component's parent or transform
            Debug.LogWarning("[BridgeConnector] Could not find 'Generated' root — " +
                             "bridge objects will be parented to this component's transform.");
            GameObject fallback = new GameObject("BridgesAndTransitions");
            fallback.transform.SetParent(transform);
            return fallback.transform;
        }

        // ─────────────────────────────────────────────────────────────────────
        // MATERIAL FACTORY
        // ─────────────────────────────────────────────────────────────────────

        private void InitMaterials()
        {
            deckMat    = CreateHDRP(new Color(0.15f, 0.15f, 0.15f), 0.25f); // dark asphalt
            barrierMat = CreateHDRP(new Color(0.65f, 0.65f, 0.68f), 0.35f); // concrete grey
            rampMat    = CreateHDRP(new Color(0.20f, 0.18f, 0.15f), 0.20f); // slightly lighter asphalt
        }

        private Material CreateHDRP(Color color, float smoothness)
        {
            Material mat = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", color);
            else                                 mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

#if UNITY_EDITOR
        // ─────────────────────────────────────────────────────────────────────
        // SCENE-VIEW GIZMOS
        //
        // Shows where bridge connectors were (or need to be) placed.
        // Visible only when this GameObject is selected.
        // ─────────────────────────────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            // Draw gap detection threshold sphere in scene view
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, maxGapThreshold);

            // Draw endpoint search sphere
            Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, endpointSearchRadius);
        }
#endif
    }
}

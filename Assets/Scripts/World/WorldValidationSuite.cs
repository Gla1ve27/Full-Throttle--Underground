using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    /// <summary>
    /// Validates the procedural world plan for driveability, connectivity,
    /// and completeness. Reports issues as warnings/errors in the console.
    ///
    /// Phases covered:
    ///   Checks 1-7  — Plan-level data validation (structure, BFS graph, distances)
    ///   Check  8    — Phase 4 physical scene validation (colliders, bridges, coverage)
    /// </summary>
    public class WorldValidationSuite : MonoBehaviour
    {
        [Header("Validation Results")]
        [SerializeField] private bool lastValidationPassed;
        [SerializeField] private int lastIssueCount;

        // ─────────────────────────────────────────────────────────────────────
        // LEGACY ENTRY POINT
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Legacy entry point — runs basic scene checks without a plan.
        /// </summary>
        public void Run()
        {
            Debug.Log("[Validation] Running legacy checks...");
            CheckSceneIntegrity();
            Debug.Log("[Validation] Legacy check complete.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // FULL VALIDATION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Full validation against a WorldPlan. Returns true if all critical checks pass.
        /// Now includes Phase 4 physical scene checks (Check 8).
        /// </summary>
        public bool Validate(WorldPlan plan, WorldGenerationConfig config = null)
        {
            bool log = config == null || config.logGeneration;
            if (log) Debug.Log("[Validation] ═══ Running full world validation ═══");

            bool valid = true;
            int issues = 0;

            // ── CHECK 1: Plan exists ─────────────────────────────────────────
            if (plan == null)
            {
                Debug.LogError("[Validation] FAIL: WorldPlan is null!");
                lastValidationPassed = false;
                lastIssueCount = 1;
                return false;
            }

            // ── CHECK 2: District count ──────────────────────────────────────
            if (plan.districts == null || plan.districts.Count < 4)
            {
                Debug.LogError($"[Validation] FAIL: Expected ≥4 districts, found {plan.districts?.Count ?? 0}");
                valid = false;
                issues++;
            }
            else if (log)
            {
                Debug.Log($"[Validation] ✅ District count: {plan.districts.Count}");
            }

            // ── CHECK 3: Required district types present ─────────────────────
            bool hasCity     = plan.GetDistrictByType(DistrictType.CityCore) != null;
            bool hasMountain = plan.GetDistrictByType(DistrictType.Mountain) != null;
            bool hasArterial = plan.GetDistrictByType(DistrictType.Arterial) != null;
            bool hasHighway  = plan.GetDistrictByType(DistrictType.Highway)  != null;

            if (!hasCity)     { Debug.LogError("[Validation] FAIL: Missing CityCore district");  valid = false; issues++; }
            if (!hasMountain) { Debug.LogError("[Validation] FAIL: Missing Mountain district");   valid = false; issues++; }
            if (!hasArterial) { Debug.LogError("[Validation] FAIL: Missing Arterial district");   valid = false; issues++; }
            if (!hasHighway)  { Debug.LogError("[Validation] FAIL: Missing Highway district");    valid = false; issues++; }

            if (hasCity && hasMountain && hasArterial && hasHighway && log)
                Debug.Log("[Validation] ✅ All 4 district types present");

            // ── CHECK 4: Connection graph integrity ──────────────────────────
            if (plan.connections == null || plan.connections.Count == 0)
            {
                Debug.LogError("[Validation] FAIL: No connections in plan!");
                valid = false;
                issues++;
            }
            else
            {
                HashSet<string> districtIds = new();
                foreach (var d in plan.districts)
                    districtIds.Add(d.id);

                foreach (var conn in plan.connections)
                {
                    if (!districtIds.Contains(conn.fromDistrictId))
                    {
                        Debug.LogError($"[Validation] FAIL: Connection references unknown district " +
                                       $"'{conn.fromDistrictId}'");
                        valid = false;
                        issues++;
                    }
                    if (!districtIds.Contains(conn.toDistrictId))
                    {
                        Debug.LogError($"[Validation] FAIL: Connection references unknown district " +
                                       $"'{conn.toDistrictId}'");
                        valid = false;
                        issues++;
                    }
                }

                if (log)
                    Debug.Log($"[Validation] ✅ Connection integrity: " +
                              $"{plan.connections.Count} connections checked");
            }

            // ── CHECK 5: All districts reachable (BFS) ──────────────────────
            if (plan.districts != null && plan.connections != null && plan.districts.Count > 0)
            {
                var visited = new HashSet<string>();
                var queue   = new Queue<string>();

                string startId = plan.districts[0].id;
                queue.Enqueue(startId);
                visited.Add(startId);

                while (queue.Count > 0)
                {
                    string current = queue.Dequeue();
                    foreach (var conn in plan.connections)
                    {
                        string neighbor = null;
                        if      (conn.fromDistrictId == current) neighbor = conn.toDistrictId;
                        else if (conn.toDistrictId   == current) neighbor = conn.fromDistrictId;

                        if (neighbor != null && !visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                foreach (var d in plan.districts)
                {
                    if (!visited.Contains(d.id))
                    {
                        Debug.LogError($"[Validation] FAIL: District '{d.id}' is UNREACHABLE " +
                                       $"from '{startId}'!");
                        valid = false;
                        issues++;
                    }
                }

                if (visited.Count == plan.districts.Count && log)
                    Debug.Log($"[Validation] ✅ All {plan.districts.Count} districts reachable via BFS");
            }

            // ── CHECK 6: Road class coverage ─────────────────────────────────
            if (plan.connections != null)
            {
                int hwCount      = plan.GetConnectionsByClass(RoadClass.Highway).Count;
                int artCount     = plan.GetConnectionsByClass(RoadClass.Arterial).Count;
                int cityCount    = plan.GetConnectionsByClass(RoadClass.CityLocal).Count;
                int mtnCount     = plan.GetConnectionsByClass(RoadClass.Mountain).Count;

                if (hwCount == 0)
                {
                    Debug.LogWarning("[Validation] WARNING: No Highway connections in plan");
                    issues++;
                }
                if (artCount == 0)
                {
                    Debug.LogWarning("[Validation] WARNING: No Arterial connections in plan");
                    issues++;
                }

                if (log)
                    Debug.Log($"[Validation] Road coverage: HW={hwCount} ART={artCount} " +
                              $"CITY={cityCount} MTN={mtnCount}");
            }

            // ── CHECK 7: Connection distances ────────────────────────────────
            if (plan.connections != null)
            {
                float longestHighway = 0f;
                foreach (var conn in plan.connections)
                {
                    if (conn.roadClass == RoadClass.Highway)
                        longestHighway = Mathf.Max(longestHighway, conn.StraightLineDistance);

                    if (conn.StraightLineDistance < 10f)
                    {
                        Debug.LogWarning($"[Validation] WARNING: Very short connection " +
                                         $"({conn.StraightLineDistance:F0}m): {conn}");
                        issues++;
                    }
                }

                if (longestHighway < 700f)
                {
                    Debug.LogWarning($"[Validation] WARNING: Longest highway span is only " +
                                     $"{longestHighway:F0}m (target ≥700m)");
                    issues++;
                }
            }

            // ── CHECK 8 (Phase 4): Physical scene connectivity ───────────────
            //   Complements plan-level BFS with geometry-level collider checks.
            //   Non-critical: issues here are warnings, not validation failures.
            issues += ValidatePhysicalConnectivity(plan, config);

            // ── SUMMARY ──────────────────────────────────────────────────────
            lastValidationPassed = valid;
            lastIssueCount = issues;

            if (valid)
                Debug.Log($"[Validation] ✅ WORLD VALIDATION PASSED ({issues} non-critical warnings)");
            else
                Debug.LogError($"[Validation] ❌ WORLD VALIDATION FAILED — {issues} issues found");

            return valid;
        }

        // ─────────────────────────────────────────────────────────────────────
        // SCENE-LEVEL CHECKS (legacy compatibility)
        // ─────────────────────────────────────────────────────────────────────

        private void CheckSceneIntegrity()
        {
            if (GameObject.Find("Road Network") == null)
                Debug.LogWarning("[Validation] No 'Road Network' root found in scene.");

            GameObject erRoot = GameObject.Find("Road Network");
            if (erRoot != null)
            {
                Collider[] colliders = erRoot.GetComponentsInChildren<Collider>(true);
                Debug.Log($"[Validation] Road Network has {colliders.Length} colliders.");

                if (colliders.Length == 0)
                    Debug.LogWarning("[Validation] WARNING: Road Network has ZERO colliders — " +
                                     "cars will fall through!");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // PHASE 4 — PHYSICAL CONNECTIVITY CHECKS
        //
        // Operate on ACTUAL SCENE OBJECTS produced by Phases 2-4, complementing
        // the plan-level BFS checks above.  Five sub-checks:
        //
        //   1. Road Network root exists
        //   2. Road Network has colliders (ApplyCollidersManually ran)
        //   3. BridgesAndTransitions root exists
        //   4. Every bridge deck segment has its BoxCollider intact
        //   5. Each district (except Highway) has at least one road collider nearby
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Run all Phase 4 scene-level physical connectivity checks.
        /// Returns the number of issues found (0 = clean).
        /// Also callable standalone via the context menu.
        /// </summary>
        [ContextMenu("Run Physical Connectivity Checks")]
        public int ValidatePhysicalConnectivity(WorldPlan plan = null, WorldGenerationConfig config = null)
        {
            bool log = config == null || config.logGeneration;

            if (log)
                Debug.Log("[Validation] ═══ Phase 4: Physical connectivity checks ═══");

            int issues = 0;

            issues += CheckRoadNetworkRoot(log);
            issues += CheckRoadColliders(log);
            issues += CheckBridgesAndTransitions(log);
            issues += CheckBridgeDeckColliders(log);

            if (plan != null)
                issues += CheckDistrictRoadCoverage(plan, log);

            if (log)
            {
                if (issues == 0)
                    Debug.Log("[Validation] ✅ Physical connectivity: all checks passed.");
                else
                    Debug.LogWarning($"[Validation] ⚠️  Physical connectivity: {issues} issue(s) found.");
            }

            return issues;
        }

        // ── Sub-check 1: Road Network root ────────────────────────────────────

        private int CheckRoadNetworkRoot(bool log)
        {
            GameObject erRoot = GameObject.Find("Road Network");
            if (erRoot == null)
            {
                Debug.LogError("[Validation] FAIL (physical): 'Road Network' root not found. " +
                               "Phase 2 may not have run.");
                return 1;
            }

            if (log)
                Debug.Log($"[Validation] ✅ 'Road Network' root present — " +
                          $"{erRoot.transform.childCount} direct children.");
            return 0;
        }

        // ── Sub-check 2: Collider count ───────────────────────────────────────

        private int CheckRoadColliders(bool log)
        {
            int issues = 0;

            GameObject erRoot = GameObject.Find("Road Network");
            if (erRoot != null)
            {
                Collider[] roadColliders = erRoot.GetComponentsInChildren<Collider>(true);
                if (roadColliders.Length == 0)
                {
                    Debug.LogError("[Validation] FAIL (physical): Road Network has ZERO colliders. " +
                                   "Cars will fall through! ApplyCollidersManually may have failed.");
                    issues++;
                }
                else if (log)
                {
                    Debug.Log($"[Validation] ✅ Road Network: {roadColliders.Length} collider(s).");
                }
            }

            // Also check bridge deck colliders
            GameObject generated = GameObject.Find("Generated");
            if (generated != null)
            {
                Transform bridgesRoot = generated.transform.Find("BridgesAndTransitions");
                if (bridgesRoot != null && bridgesRoot.childCount > 0)
                {
                    Collider[] bridgeColliders = bridgesRoot.GetComponentsInChildren<Collider>(true);
                    if (bridgeColliders.Length == 0)
                    {
                        Debug.LogWarning("[Validation] WARNING (physical): Bridge geometry exists " +
                                         "but has NO colliders — bridges are visual-only!");
                        issues++;
                    }
                    else if (log)
                    {
                        Debug.Log($"[Validation] ✅ BridgesAndTransitions: " +
                                  $"{bridgeColliders.Length} deck collider(s).");
                    }
                }
            }

            return issues;
        }

        // ── Sub-check 3: BridgesAndTransitions root ───────────────────────────

        private int CheckBridgesAndTransitions(bool log)
        {
            GameObject generated = GameObject.Find("Generated");
            if (generated == null)
            {
                if (log)
                    Debug.LogWarning("[Validation] WARNING (physical): 'Generated' root not found — " +
                                     "was ProceduralWorldBootstrap run?");
                return 1;
            }

            Transform bridgesRoot = generated.transform.Find("BridgesAndTransitions");
            if (bridgesRoot == null)
            {
                if (log)
                    Debug.LogWarning("[Validation] WARNING (physical): " +
                                     "'Generated/BridgesAndTransitions' not found — " +
                                     "Phase 4 connector may not have run.");
                return 1;
            }

            if (log)
            {
                int count = bridgesRoot.childCount;
                Debug.Log($"[Validation] ✅ BridgesAndTransitions root: {count} bridge(s). " +
                          (count == 0 ? "(Zero is OK if plan had no gaps.)" : ""));
            }

            return 0;
        }

        // ── Sub-check 4: Bridge deck BoxColliders intact ──────────────────────

        private int CheckBridgeDeckColliders(bool log)
        {
            int issues = 0;

            GameObject generated = GameObject.Find("Generated");
            if (generated == null) return 0;

            Transform bridgesRoot = generated.transform.Find("BridgesAndTransitions");
            if (bridgesRoot == null || bridgesRoot.childCount == 0) return 0;

            foreach (Transform bridge in bridgesRoot)
            {
                Transform deckRoot = bridge.Find("Deck");
                if (deckRoot == null) continue;

                int segCount    = 0;
                int missingCols = 0;

                foreach (Transform seg in deckRoot)
                {
                    segCount++;
                    if (seg.GetComponent<BoxCollider>() == null)
                        missingCols++;
                }

                if (missingCols > 0)
                {
                    Debug.LogError($"[Validation] FAIL (physical): Bridge '{bridge.name}' — " +
                                   $"{missingCols}/{segCount} deck segment(s) missing BoxCollider. " +
                                   $"Car will fall through!");
                    issues++;
                }
                else if (log && segCount > 0)
                {
                    Debug.Log($"[Validation] ✅ Bridge '{bridge.name}': " +
                              $"{segCount} segment(s), all collideable.");
                }
            }

            return issues;
        }

        // ── Sub-check 5: Each district has road coverage ──────────────────────

        private int CheckDistrictRoadCoverage(WorldPlan plan, bool log)
        {
            if (plan?.districts == null) return 0;

            int issues = 0;

            // Gather all relevant colliders from the scene
            var allColliders = new List<Collider>();

            GameObject erRoot = GameObject.Find("Road Network");
            if (erRoot != null)
                allColliders.AddRange(erRoot.GetComponentsInChildren<Collider>(true));

            GameObject generated = GameObject.Find("Generated");
            if (generated != null)
            {
                Transform bridgesRoot = generated.transform.Find("BridgesAndTransitions");
                if (bridgesRoot != null)
                    allColliders.AddRange(bridgesRoot.GetComponentsInChildren<Collider>(true));
            }

            if (allColliders.Count == 0)
            {
                if (log)
                    Debug.LogWarning("[Validation] WARNING (physical): No road/bridge colliders found — " +
                                     "skipping district coverage check.");
                return 0;
            }

            foreach (var district in plan.districts)
            {
                // Highway is world-scale and always has coverage — skip
                if (district.districtType == DistrictType.Highway) continue;

                float   searchRadiusSq = district.radius * district.radius;
                Vector2 center2D       = new Vector2(district.center.x, district.center.z);
                bool    foundNearby    = false;

                foreach (var col in allColliders)
                {
                    if (col == null) continue;
                    Vector2 pos2D = new Vector2(col.transform.position.x, col.transform.position.z);
                    if ((pos2D - center2D).sqrMagnitude <= searchRadiusSq)
                    {
                        foundNearby = true;
                        break;
                    }
                }

                if (!foundNearby)
                {
                    Debug.LogError($"[Validation] FAIL (physical): District '{district.id}' " +
                                   $"({district.districtType}) — no road/bridge collider within " +
                                   $"{district.radius:F0}m. District may be physically unreachable!");
                    issues++;
                }
                else if (log)
                {
                    Debug.Log($"[Validation] ✅ District '{district.id}' has road coverage.");
                }
            }

            return issues;
        }
    }
}

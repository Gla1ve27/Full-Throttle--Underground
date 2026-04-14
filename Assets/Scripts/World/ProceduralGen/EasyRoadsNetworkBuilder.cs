using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    /// <summary>
    /// PHASE 2 v3: NFS Heat-style road network builder.
    ///
    /// CRITICAL DESIGN CHANGE: FCG generates all city streets.
    /// EasyRoads3D only builds:
    ///   1. Highway loop — organic perimeter wrapping the FCG city
    ///   2. Arterials — connect FCG city boundary exits to the highway
    ///   3. Mountain roads — switchback climbs in the mountain district
    ///
    /// The city grid generation has been REMOVED because FCG's internal
    /// road system (visible in ExitCity, Border-Mini, HW-R/L segments)
    /// already creates connected, integrated city streets.
    ///
    /// This matches NFS Heat's approach:
    ///   - Dense city streets = authored (FCG for us)
    ///   - Highway loop = wraps around the city perimeter
    ///   - Mountain roads = separate winding roads in the hills
    ///   - Causeways/bridges = connect separate landmasses
    /// </summary>
    public class EasyRoadsNetworkBuilder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EasyRoadsManager easyRoadsManager;

        [Header("Highway")]
        [Tooltip("Catmull-Rom subdivision per control point")]
        [SerializeField] private int highwaySubdivisionSteps = 5;
        [Tooltip("Half-width of highway median gap (divided carriageway)")]
        [SerializeField] private float highwayMedianHalfWidth = 12f;
        [SerializeField] private bool dividedHighway = true;

        [Header("Arterial")]
        [SerializeField] private int arterialSmoothIterations = 2;
        [SerializeField] private int arterialMarkerCount = 10;

        [Header("Mountain")]
        [Tooltip("Peak elevation for mountain climbs")]
        [SerializeField] private float mountainPeakElevation = 120f;
        [Tooltip("Number of clean switchback turns per road")]
        [SerializeField] private int switchbackCount = 4;

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        public void BuildFromPlan(WorldPlan plan, WorldGenerationConfig config)
        {
            if (easyRoadsManager == null)
                easyRoadsManager = FindFirstObjectByType<EasyRoadsManager>();

            if (easyRoadsManager == null)
            {
                Debug.LogError("[ERBuilder] No EasyRoadsManager found!");
                return;
            }

            if (config.logGeneration)
                Debug.Log("[ERBuilder] ═══ Building NFS Heat-style roads (no city grid — FCG owns city streets) ═══");

            easyRoadsManager.Cleanup();
            easyRoadsManager.InitializeNetwork();

            // Only 3 road types — FCG handles all city roads
            int hwCount  = BuildHighwayLoop(plan, config);
            int artCount = BuildArterials(plan, config);
            int mtnCount = BuildMountainRoads(plan, config);

            easyRoadsManager.BuildRoadNetwork();

            if (config.logGeneration)
                Debug.Log($"[ERBuilder] ═══ Done: {hwCount} highway, {artCount} arterial, " +
                          $"{mtnCount} mountain — city streets handled by FCG ═══");
        }

        // ─────────────────────────────────────────────────────────────────────
        // 1. HIGHWAY LOOP — Organic coastline perimeter
        //
        // Like the I-10/I-95 loop in NFS Heat Palm City:
        //   - Wraps around the OUTSIDE of the FCG city
        //   - Irregular peninsula shape, not a perfect oval
        //   - On-ramps connect to FCG city exits and to mountain area
        //   - Flat, wide, high-speed divided carriageway
        // ─────────────────────────────────────────────────────────────────────

        private int BuildHighwayLoop(WorldPlan plan, WorldGenerationConfig config)
        {
            DistrictPlan hw = plan.GetDistrictByType(DistrictType.Highway);
            if (hw == null) return 0;

            float baseRadius = hw.radius - config.highwayInset;
            int numPts = Mathf.Max(config.highwayControlPointCount, 8);

            if (config.logGeneration)
                Debug.Log($"[ERBuilder] Highway: baseRadius={baseRadius:F0}, pts={numPts}");

            var controlPts = new List<Vector3>();

            for (int i = 0; i < numPts; i++)
            {
                float angle = i * Mathf.PI * 2f / numPts;

                // Peninsula-shaped radius modifiers (inspired by Palm City map)
                float shapeModifier = 1f;

                // South extends further (peninsula tip)
                if (Mathf.Sin(angle) < -0.3f)
                    shapeModifier = 1.15f + 0.08f * Mathf.Sin(angle * 2f);

                // Northwest indentation (harbour bay)
                if (angle > Mathf.PI * 0.55f && angle < Mathf.PI * 0.85f)
                    shapeModifier = 0.85f;

                // East side slight bulge (port area)
                if (Mathf.Cos(angle) > 0.5f && Mathf.Sin(angle) > 0f)
                    shapeModifier = 1.08f;

                // Small organic perturbation
                float perturb = Random.Range(-0.03f, 0.03f);
                float r = baseRadius * (shapeModifier + perturb);

                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;
                float y = 1f + 2f * Mathf.Sin(angle * 2f); // very subtle undulation

                controlPts.Add(hw.center + new Vector3(x, y, z));
            }

            List<Vector3> smoothLoop = SplineUtils.SampleCatmullRomLoop(controlPts, highwaySubdivisionSteps);
            int finalMarkers = numPts * highwaySubdivisionSteps;
            Vector3[] uniformLoop = SplineUtils.ResampleUniform(smoothLoop, finalMarkers);

            if (dividedHighway)
            {
                SplineUtils.SplitDividedHighway(uniformLoop, highwayMedianHalfWidth,
                    out Vector3[] left, out Vector3[] right);

                easyRoadsManager.AddProceduralRoad("Autobahn_Left", left, config.highwayWidth);
                easyRoadsManager.AddProceduralRoad("Autobahn_Right", right, config.highwayWidth);

                if (config.logGeneration)
                {
                    float len = SplineUtils.TotalLength(uniformLoop);
                    Debug.Log($"[ERBuilder] Highway loop: {len:F0}m, {left.Length} markers/side");
                }
                return 2;
            }
            else
            {
                easyRoadsManager.AddProceduralRoad("Autobahn_Loop", uniformLoop, config.highwayWidth);
                return 1;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. ARTERIALS — Connect FCG city exits to the highway
        //
        // In NFS Heat, arterials are the wide boulevards that lead FROM
        // the dense city streets OUT to the highway system. They're
        // readable, gently curved, and pass through the space between
        // the FCG city boundary and the highway loop.
        //
        // These connect:
        //   - FCG city edge → highway on-ramp (south, east, northwest)
        //   - FCG city edge → mountain base
        // ─────────────────────────────────────────────────────────────────────

        private int BuildArterials(WorldPlan plan, WorldGenerationConfig config)
        {
            var arterials = plan.GetConnectionsByClass(RoadClass.Arterial);
            if (config.logGeneration)
                Debug.Log($"[ERBuilder] Arterials: {arterials.Count} connections (city exit → highway/mountain)");

            int count = 0;
            foreach (var conn in arterials)
            {
                Vector3 dir = (conn.end - conn.start);
                float dist = dir.magnitude;
                Vector3 dirN = dir.normalized;
                Vector3 perp = Vector3.Cross(dirN, Vector3.up).normalized;

                // Gentle single-curve — like a real boulevard
                float curveOffset = dist * Random.Range(0.03f, 0.08f);
                float curveSign = Random.value > 0.5f ? 1f : -1f;

                Vector3 mid = (conn.start + conn.end) * 0.5f + perp * curveOffset * curveSign;
                mid.y = Mathf.Lerp(conn.start.y, conn.end.y, 0.5f);

                var rawPath = new List<Vector3> { conn.start, mid, conn.end };
                List<Vector3> smooth = SplineUtils.ChaikinSubdivide(rawPath, arterialSmoothIterations);
                Vector3[] markers = SplineUtils.ResampleUniform(smooth, arterialMarkerCount);

                string name = $"Arterial_{conn.fromDistrictId}_{conn.toDistrictId}_{count}";
                easyRoadsManager.AddProceduralRoad(name, markers, config.arterialWidth);

                if (config.logGeneration)
                    Debug.Log($"[ERBuilder] {name}: {SplineUtils.TotalLength(markers):F0}m");

                count++;
            }
            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3. MOUNTAIN ROADS — Clean switchback climbs
        //
        // Like Blackwood Hills in NFS Heat:
        //   - Clean zigzag switchbacks going uphill
        //   - Wide enough hairpins for drifting
        //   - Single clear path: base → switchback left → right → peak
        //   - Mountain feeder road back to FCG city edge
        // ─────────────────────────────────────────────────────────────────────

        private int BuildMountainRoads(WorldPlan plan, WorldGenerationConfig config)
        {
            DistrictPlan mtn = plan.GetDistrictByType(DistrictType.Mountain);
            if (mtn == null) return 0;

            if (config.logGeneration)
                Debug.Log($"[ERBuilder] Mountain: {config.mountainRoadCount} roads, " +
                          $"peak={mountainPeakElevation}m");

            int count = 0;

            for (int r = 0; r < config.mountainRoadCount; r++)
            {
                float baseAngle = r * Mathf.PI * 2f / config.mountainRoadCount;
                float roadRadius = mtn.radius * 0.7f;

                // Start at mountain edge (low)
                Vector3 roadStart = mtn.center + new Vector3(
                    Mathf.Cos(baseAngle) * roadRadius,
                    5f,
                    Mathf.Sin(baseAngle) * roadRadius);

                // End near mountain center (peak)
                Vector3 roadEnd = mtn.center + new Vector3(
                    Mathf.Cos(baseAngle + Mathf.PI) * mtn.radius * 0.2f,
                    mountainPeakElevation,
                    Mathf.Sin(baseAngle + Mathf.PI) * mtn.radius * 0.2f);

                var waypoints = GenerateSwitchbackPath(roadStart, roadEnd, switchbackCount, roadRadius * 0.4f);

                List<Vector3> smooth = SplineUtils.SampleCatmullRom(waypoints, 3);
                Vector3[] markers = SplineUtils.ResampleUniform(smooth, switchbackCount * 4 + 4);

                string name = $"Mountain_Road_{r}";
                easyRoadsManager.AddProceduralRoad(name, markers, config.mountainRoadWidth);

                if (config.logGeneration)
                    Debug.Log($"[ERBuilder] {name}: {SplineUtils.TotalLength(markers):F0}m");

                count++;
            }

            // Mountain feeder — connects mountain base back towards FCG city edge
            BuildMountainFeeder(plan, config, mtn);
            count++;

            return count;
        }

        /// <summary>
        /// Clean zigzag switchback: alternates left-right while climbing steadily.
        /// </summary>
        private List<Vector3> GenerateSwitchbackPath(Vector3 start, Vector3 end,
                                                      int numSwitchbacks, float switchWidth)
        {
            var path = new List<Vector3>();
            path.Add(start);

            Vector3 climbDir = (end - start);
            Vector3 climbDirFlat = new Vector3(climbDir.x, 0f, climbDir.z).normalized;
            Vector3 sideDir = Vector3.Cross(climbDirFlat, Vector3.up).normalized;

            for (int i = 1; i <= numSwitchbacks; i++)
            {
                float t = i / (float)(numSwitchbacks + 1);
                Vector3 basePos = Vector3.Lerp(start, end, t);
                float side = (i % 2 == 0) ? switchWidth : -switchWidth;
                side += Random.Range(-switchWidth * 0.1f, switchWidth * 0.1f);
                path.Add(basePos + sideDir * side);
            }

            path.Add(end);
            return path;
        }

        private void BuildMountainFeeder(WorldPlan plan, WorldGenerationConfig config,
                                          DistrictPlan mtn)
        {
            DistrictPlan city = plan.GetDistrictByType(DistrictType.CityCore);
            if (city == null) return;

            // From mountain edge towards FCG city edge
            Vector3 dir = (city.center - mtn.center).normalized;
            Vector3 feederStart = mtn.center + dir * (mtn.radius * 0.4f);
            feederStart.y = 15f;

            Vector3 feederEnd = city.center - dir * (config.cityCoreRadius * 0.7f);
            feederEnd.y = 0f;

            Vector3 mid = (feederStart + feederEnd) * 0.5f;
            mid.y = feederStart.y * 0.4f;
            mid += Vector3.Cross(dir, Vector3.up) * Random.Range(-30f, 30f);

            var rawPath = new List<Vector3> { feederStart, mid, feederEnd };
            List<Vector3> smooth = SplineUtils.ChaikinSubdivide(rawPath, 2);
            Vector3[] markers = SplineUtils.ResampleUniform(smooth, 8);

            easyRoadsManager.AddProceduralRoad("Mountain_Feeder", markers, config.arterialWidth);

            if (config.logGeneration)
                Debug.Log($"[ERBuilder] Mountain feeder: {SplineUtils.TotalLength(markers):F0}m");
        }
    }
}

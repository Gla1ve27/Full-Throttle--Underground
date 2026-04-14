using UnityEngine;

namespace Underground.World
{
    /// <summary>
    /// Builds a WorldPlan that copies the NFS Heat Palm City layout:
    ///
    ///   Looking top-down (+Z = north, +X = east):
    ///
    ///       MOUNTAIN ← winding roads
    ///       (0, 0, 800)
    ///          ╲
    ///           ╲ arterial
    ///            ╲
    ///    ┌──── CITY CORE ────┐
    ///    │  (0, 0, 0)  FCG   │ ← FCG handles all streets
    ///    │  ~500m across     │
    ///    └───────────────────┘
    ///         │         ╲
    ///    south arterial   east arterial → PORT AREA
    ///         │                           (500, 0, 100)
    ///         ↓
    ///    SOUTH EXIT
    ///    (0, 0, -600)
    ///
    ///    ═══ HIGHWAY LOOP wraps around everything at ~550m radius ═══
    ///
    /// ALL positions calibrated so the highway loop HUGS the FCG city,
    /// not floating 2km away in the ocean.
    /// </summary>
    public class ProceduralWorldPlanner : MonoBehaviour
    {
        public WorldPlan BuildPlan(WorldGenerationConfig config)
        {
            int seed = config.randomizeSeed
                ? Random.Range(int.MinValue, int.MaxValue)
                : config.seed;

            Random.InitState(seed);

            WorldPlan plan = new WorldPlan
            {
                seed = seed,
                worldSize = config.worldSize
            };

            if (config.logGeneration)
                Debug.Log($"[Planner] Building NFS Heat layout, seed={seed}");

            PlaceDistricts(plan, config);
            BuildConnectionGraph(plan, config);
            PlaceLandmarks(plan, config);

            if (config.logGeneration)
                Debug.Log($"[Planner] {plan.GetSummary()}");

            return plan;
        }

        // ─────────────────────────────────────────────────────────────────────
        // DISTRICT PLACEMENT — traced from NFS Heat satellite map
        //
        // Key insight: in the NFS Heat map (image 2), using the 1km scale bar:
        //   - Downtown is ~2.5km across (our FCG ≈ 500m → scale 1:5)
        //   - Highway loop hugs the city edge tightly
        //   - Mountain is directly NW, touching the city
        //   - Port/industrial is directly E of city
        //
        // At our scale (1:5 of NFS Heat):
        //   - City center = origin, radius = 300m (FCG fills this)
        //   - Mountain center = 700m northwest, radius = 400m
        //   - Arterial/Port = 400m east
        //   - Highway loop = 550m radius (just 250m outside city edge)
        // ─────────────────────────────────────────────────────────────────────

        private void PlaceDistricts(WorldPlan plan, WorldGenerationConfig config)
        {
            // ── CITY CORE — centered at origin where FCG generates ──────────
            plan.districts.Add(CreateDistrict(
                "CITY_CORE", DistrictType.CityCore,
                Vector3.zero, config.cityCoreRadius));

            // ── MOUNTAIN — northwest, ADJACENT to city ──────────────────────
            // In NFS Heat: Blackwood Hills is directly north-northwest
            // At our scale: 700m northwest of origin
            Vector3 mtnCenter = new Vector3(-350f, 0f, 600f);
            plan.districts.Add(CreateDistrict(
                "MOUNTAIN", DistrictType.Mountain,
                mtnCenter, config.mountainRadius));

            // ── ARTERIAL/PORT — east side ────────────────────────────────────
            // In NFS Heat: Port Murphy is east of downtown
            Vector3 artCenter = new Vector3(400f, 0f, 100f);
            plan.districts.Add(CreateDistrict(
                "ARTERIAL", DistrictType.Arterial,
                artCenter, config.arterialSpan * 0.3f));

            // ── HIGHWAY — wraps everything ──────────────────────────────────
            // Radius = city radius + 250m buffer = 550m
            // This puts the highway JUST outside the FCG city boundary
            float hwRadius = config.cityCoreRadius + 250f;
            plan.districts.Add(CreateDistrict(
                "HIGHWAY", DistrictType.Highway,
                Vector3.zero, hwRadius));

            if (config.logGeneration)
            {
                Debug.Log($"[Planner]  City:     origin r={config.cityCoreRadius}");
                Debug.Log($"[Planner]  Mountain: {mtnCenter} r={config.mountainRadius}");
                Debug.Log($"[Planner]  Port:     {artCenter} r={config.arterialSpan * 0.3f:F0}");
                Debug.Log($"[Planner]  Highway:  r={hwRadius:F0} (250m outside city)");
            }
        }

        private DistrictPlan CreateDistrict(string id, DistrictType type,
                                             Vector3 center, float radius)
        {
            return new DistrictPlan
            {
                id = id,
                districtType = type,
                center = center,
                radius = radius,
                bounds = new Bounds(center, new Vector3(radius * 2f, 500f, radius * 2f))
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // CONNECTION GRAPH — traced from NFS Heat map roads
        //
        // FCG handles ALL city streets. We only create:
        //   1. City north exit → Mountain base (arterial boulevard)
        //   2. City east exit → Port area (arterial)
        //   3. City south exit → Highway on-ramp (south)
        //   4. Port → Highway on-ramp (east)
        //   5. Mountain → Highway on-ramp (northwest)
        //   6. Mountain internal switchback definition
        //
        // All connections START at the FCG city boundary edge and go OUTWARD.
        // ─────────────────────────────────────────────────────────────────────

        private void BuildConnectionGraph(WorldPlan plan, WorldGenerationConfig config)
        {
            DistrictPlan city     = plan.GetDistrict("CITY_CORE");
            DistrictPlan mountain = plan.GetDistrict("MOUNTAIN");
            DistrictPlan arterial = plan.GetDistrict("ARTERIAL");
            DistrictPlan highway  = plan.GetDistrict("HIGHWAY");

            float cityR = config.cityCoreRadius;
            float hwR   = highway.radius;

            // ── 1. City north edge → Mountain base ──────────────────────────
            // Main road leaving the FCG city going northwest to the mountains
            Vector3 cityNorthExit = new Vector3(-100f, 0f, cityR * 0.9f);
            Vector3 mountainEntry = mountain.center + new Vector3(150f, 0f, -mountain.radius * 0.6f);
            AddConnection(plan, city, mountain, RoadClass.Arterial, cityNorthExit, mountainEntry);

            // ── 2. City east edge → Port area ───────────────────────────────
            Vector3 cityEastExit = new Vector3(cityR * 0.9f, 0f, 50f);
            Vector3 portEntry = arterial.center + new Vector3(-arterial.radius * 0.5f, 0f, 0f);
            AddConnection(plan, city, arterial, RoadClass.Arterial, cityEastExit, portEntry);

            // ── 3. City south edge → Highway on-ramp ────────────────────────
            // Like the I-10 south exit from downtown Palm City
            Vector3 citySouthExit = new Vector3(0f, 0f, -cityR * 0.9f);
            Vector3 hwSouth = new Vector3(0f, 0f, -hwR * 0.9f);
            AddConnection(plan, city, highway, RoadClass.Highway, citySouthExit, hwSouth);

            // ── 4. Port → Highway on-ramp (east) ────────────────────────────
            Vector3 portExit = arterial.center + new Vector3(arterial.radius * 0.6f, 0f, 0f);
            Vector3 hwEast = new Vector3(hwR * 0.85f, 0f, 100f);
            AddConnection(plan, arterial, highway, RoadClass.Highway, portExit, hwEast);

            // ── 5. Mountain → Highway on-ramp (northwest) ───────────────────
            Vector3 mtnExit = mountain.center + new Vector3(-mountain.radius * 0.5f, 0f, -100f);
            Vector3 hwNW = new Vector3(-hwR * 0.8f, 0f, hwR * 0.5f);
            AddConnection(plan, mountain, highway, RoadClass.Highway, mtnExit, hwNW);

            // ── 6. Mountain internal switchback road ─────────────────────────
            Vector3 mtnBase = mountain.center + new Vector3(100f, 0f, -mountain.radius * 0.4f);
            Vector3 mtnPeak = mountain.center + new Vector3(-100f, 0f, mountain.radius * 0.4f);
            AddConnection(plan, mountain, mountain, RoadClass.Mountain, mtnBase, mtnPeak);

            if (config.logGeneration)
                Debug.Log($"[Planner] {plan.connections.Count} connections (FCG handles city streets)");
        }

        private void AddConnection(WorldPlan plan, DistrictPlan from, DistrictPlan to,
                                    RoadClass roadClass, Vector3 start, Vector3 end)
        {
            plan.connections.Add(new RoadConnectionPlan
            {
                fromDistrictId = from.id,
                toDistrictId = to.id,
                roadClass = roadClass,
                start = start,
                end = end
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // LANDMARKS — placed near FCG city, not in the ocean
        // ─────────────────────────────────────────────────────────────────────

        private void PlaceLandmarks(WorldPlan plan, WorldGenerationConfig config)
        {
            DistrictPlan mountain = plan.GetDistrict("MOUNTAIN");

            plan.landmarks.Add(new LandmarkPlan
            {
                id = "MAIN_GARAGE",
                position = new Vector3(80f, 0f, -60f),
                category = "garage",
                reservedRadius = 40f
            });

            plan.landmarks.Add(new LandmarkPlan
            {
                id = "MOUNTAIN_OVERLOOK",
                position = mountain.center + Vector3.up * 60f,
                category = "overlook",
                reservedRadius = 50f
            });

            plan.landmarks.Add(new LandmarkPlan
            {
                id = "ARTERIAL_GAS",
                position = new Vector3(350f, 0f, -50f),
                category = "gas_station",
                reservedRadius = 30f
            });

            plan.landmarks.Add(new LandmarkPlan
            {
                id = "HIGHWAY_RACE_HUB",
                position = new Vector3(50f, 0f, -400f),
                category = "race_hub",
                reservedRadius = 60f
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // DEBUG GIZMOS
        // ─────────────────────────────────────────────────────────────────────

        [SerializeField, HideInInspector]
        private WorldPlan _lastPlan;

        public void StashPlanForGizmos(WorldPlan plan) => _lastPlan = plan;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_lastPlan == null) return;

            foreach (var d in _lastPlan.districts)
            {
                switch (d.districtType)
                {
                    case DistrictType.CityCore: Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.25f); break;
                    case DistrictType.Mountain: Gizmos.color = new Color(0.3f, 0.8f, 0.3f, 0.25f); break;
                    case DistrictType.Arterial: Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.25f); break;
                    case DistrictType.Highway:  Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.15f); break;
                }
                Gizmos.DrawWireSphere(d.center, d.radius);
            }

            Gizmos.color = Color.cyan;
            foreach (var c in _lastPlan.connections)
                Gizmos.DrawLine(c.start, c.end);

            Gizmos.color = Color.magenta;
            foreach (var l in _lastPlan.landmarks)
                Gizmos.DrawWireSphere(l.position, l.reservedRadius);
        }
#endif
    }
}

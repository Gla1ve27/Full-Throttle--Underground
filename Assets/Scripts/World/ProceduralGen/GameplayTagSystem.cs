using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    /// <summary>
    /// PHASE 5: Gameplay metadata tagging and race/traffic prep.
    ///
    /// After all road generation, population, and bridging is done, this system
    /// performs a final pass that:
    ///
    ///   1. Tags district root GameObjects with their DistrictType
    ///   2. Tags EasyRoads segments with their RoadClass (via naming convention)
    ///   3. Places race start/finish lines at landmark positions
    ///   4. Places checkpoint trigger zones along planned connections
    ///   5. Reserves traffic spawn points at district edges
    ///   6. Adds DistrictZone components to district roots for runtime queries
    ///
    /// Spec refs: §6 Step 10 ("metadata pass"), §2.2, §4.4 Layer D, §12.
    /// </summary>
    public class GameplayTagSystem : MonoBehaviour
    {
        [Header("Tagging")]
        [Tooltip("Unity tag applied to all highway road objects")]
        [SerializeField] private string highwayTag = "HighwayRoad";

        [Tooltip("Unity tag applied to all city local road objects")]
        [SerializeField] private string cityRoadTag = "CityRoad";

        [Tooltip("Unity tag applied to all arterial road objects")]
        [SerializeField] private string arterialTag = "ArterialRoad";

        [Tooltip("Unity tag applied to all mountain road objects")]
        [SerializeField] private string mountainTag = "MountainRoad";

        [Header("Race Prep")]
        [Tooltip("Spawn race start/finish markers at landmark positions")]
        [SerializeField] private bool placeRaceMarkers = true;

        [Tooltip("Width of a start/finish line trigger (metres)")]
        [SerializeField] private float startLineWidth = 30f;

        [Tooltip("Number of checkpoint triggers placed along each arterial")]
        [SerializeField] private int checkpointsPerArterial = 3;

        [Header("Traffic Prep")]
        [Tooltip("Place traffic spawn points at district perimeter edges")]
        [SerializeField] private bool placeTrafficSpawns = true;

        [Tooltip("Number of traffic spawn points per non-highway district")]
        [SerializeField] private int trafficSpawnsPerDistrict = 4;

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Execute the full Phase 5 metadata pass.
        /// </summary>
        public void ApplyGameplayMetadata(WorldPlan plan, WorldGenerationConfig config)
        {
            if (!config.applyGameplayTags)
            {
                Debug.Log("[GameplayTags] Gameplay tagging disabled in config.");
                return;
            }

            if (config.logGeneration)
                Debug.Log("[GameplayTags] ═══ Phase 5: Gameplay metadata pass ═══");

            // ── Step 1: Tag districts and roads ──────────────────────────────
            int districtTags = TagDistricts(plan, config);
            int roadTags     = TagRoads(config);

            // ── Step 2: Add runtime district zone components ─────────────────
            int zones = AddDistrictZoneComponents(plan, config);

            // ── Step 3: Race start/finish and checkpoints ────────────────────
            int raceMarkers = 0;
            if (placeRaceMarkers)
                raceMarkers = PlaceRaceInfrastructure(plan, config);

            // ── Step 4: Traffic spawn points ─────────────────────────────────
            int trafficPts = 0;
            if (placeTrafficSpawns)
                trafficPts = PlaceTrafficSpawnPoints(plan, config);

            if (config.logGeneration)
            {
                Debug.Log($"[GameplayTags] ═══ Metadata pass complete ═══");
                Debug.Log($"[GameplayTags]  District tags:     {districtTags}");
                Debug.Log($"[GameplayTags]  Road tags:         {roadTags}");
                Debug.Log($"[GameplayTags]  District zones:    {zones}");
                Debug.Log($"[GameplayTags]  Race markers:      {raceMarkers}");
                Debug.Log($"[GameplayTags]  Traffic spawns:    {trafficPts}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // STEP 1: TAG DISTRICTS
        //
        // Find district root GameObjects created by SetupHierarchy and
        // tag them with their DistrictType name. Also set the static flag
        // for rendering optimisations.
        // ─────────────────────────────────────────────────────────────────────

        private int TagDistricts(WorldPlan plan, WorldGenerationConfig config)
        {
            int count = 0;
            GameObject generated = GameObject.Find("Generated");
            if (generated == null) return 0;

            Transform districts = generated.transform.Find("Districts");
            if (districts == null) return 0;

            foreach (var d in plan.districts)
            {
                string districtObjName = d.districtType + "District";
                Transform districtObj = districts.Find(districtObjName);
                if (districtObj == null) continue;

                // Store metadata on the GameObject for runtime queries
                var meta = districtObj.gameObject.GetComponent<DistrictMetadata>();
                if (meta == null)
                    meta = districtObj.gameObject.AddComponent<DistrictMetadata>();

                meta.districtId   = d.id;
                meta.districtType = d.districtType;
                meta.center       = d.center;
                meta.radius       = d.radius;

                count++;

                if (config.logGeneration)
                    Debug.Log($"[GameplayTags] Tagged '{districtObjName}' → {d.districtType}");
            }

            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // STEP 1B: TAG ROADS BY NAME CONVENTION
        //
        // EasyRoads3D roads are named with prefixes from Phase 2:
        //   "Autobahn_"  → Highway
        //   "Arterial_"  → Arterial
        //   "Mountain_"  → Mountain
        //   "City_"      → CityLocal
        //   "Connector_" → Connector (treated as Arterial for gameplay)
        //
        // We find the road mesh objects under "Road Network" and tag them.
        // ─────────────────────────────────────────────────────────────────────

        private int TagRoads(WorldGenerationConfig config)
        {
            int count = 0;
            GameObject roadNetworkRoot = GameObject.Find("Road Network");
            if (roadNetworkRoot == null) return 0;

            foreach (Transform child in roadNetworkRoot.transform)
            {
                string name = child.name;
                string tag = DetermineRoadTag(name);

                if (tag != null)
                {
                    // Apply tag if it exists, otherwise log a warning
                    try { child.gameObject.tag = tag; }
                    catch { /* Tag not created in project settings — that's OK for now */ }

                    // Also store road class as a component for reliable runtime querying
                    var roadMeta = child.gameObject.GetComponent<RoadMetadata>();
                    if (roadMeta == null)
                        roadMeta = child.gameObject.AddComponent<RoadMetadata>();

                    roadMeta.roadClass = DetermineRoadClass(name);
                    roadMeta.roadName = name;

                    count++;
                }
            }

            if (config.logGeneration)
                Debug.Log($"[GameplayTags] Tagged {count} road objects under 'Road Network'");

            return count;
        }

        private string DetermineRoadTag(string name)
        {
            if (name.Contains("Autobahn"))  return highwayTag;
            if (name.Contains("Arterial"))  return arterialTag;
            if (name.Contains("Mountain"))  return mountainTag;
            if (name.Contains("City"))      return cityRoadTag;
            if (name.Contains("Connector")) return arterialTag;
            return null;
        }

        private RoadClass DetermineRoadClass(string name)
        {
            if (name.Contains("Autobahn"))  return RoadClass.Highway;
            if (name.Contains("Arterial"))  return RoadClass.Arterial;
            if (name.Contains("Mountain"))  return RoadClass.Mountain;
            if (name.Contains("City"))      return RoadClass.CityLocal;
            if (name.Contains("Connector")) return RoadClass.Arterial;
            return RoadClass.CityLocal;
        }

        // ─────────────────────────────────────────────────────────────────────
        // STEP 2: DISTRICT ZONE COMPONENTS
        //
        // Add DistrictMetadata components to district roots. These are
        // lightweight runtime queryable components that race/traffic
        // systems can use with FindObjectsOfType<DistrictMetadata>()
        // to discover which district the player is in.
        // ─────────────────────────────────────────────────────────────────────

        private int AddDistrictZoneComponents(WorldPlan plan, WorldGenerationConfig config)
        {
            // Already done in TagDistricts — count existing ones
            return Object.FindObjectsByType<DistrictMetadata>(FindObjectsSortMode.None).Length;
        }

        // ─────────────────────────────────────────────────────────────────────
        // STEP 3: RACE INFRASTRUCTURE
        //
        // Place start/finish lines and checkpoints for the race system.
        // Start / Finish: placed at the HIGHWAY_RACE_HUB landmark.
        // Checkpoints: spaced evenly along each arterial connection.
        // ─────────────────────────────────────────────────────────────────────

        private int PlaceRaceInfrastructure(WorldPlan plan, WorldGenerationConfig config)
        {
            int count = 0;

            Transform generated = GameObject.Find("Generated")?.transform;
            if (generated == null) return 0;

            Transform landmarkRoot = generated.Find("Landmarks")
                                     ?? CreateChild(generated, "Landmarks");

            // Start/Finish line at the race hub landmark
            LandmarkPlan raceHub = plan.GetLandmark("HIGHWAY_RACE_HUB");
            if (raceHub != null)
            {
                GameObject startFinish = new GameObject("RaceStartFinish");
                startFinish.transform.SetParent(landmarkRoot);
                startFinish.transform.position = raceHub.position;

                // Trigger zone for detecting cars crossing the line
                BoxCollider trigger = startFinish.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(startLineWidth, 5f, 3f);

                // Visual marker
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                visual.name = "StartLine_Visual";
                visual.transform.SetParent(startFinish.transform);
                visual.transform.localPosition = Vector3.up * 0.05f;
                visual.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                visual.transform.localScale = new Vector3(startLineWidth, 3f, 1f);
                DestroyImmediate(visual.GetComponent<MeshCollider>());

                // Checkerboard-ish material
                var mat = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", Color.white);
                visual.GetComponent<Renderer>().sharedMaterial = mat;

                count++;
                if (config.logGeneration)
                    Debug.Log($"[GameplayTags] Start/Finish line placed at {raceHub.position}");
            }

            // Checkpoints along arterial connections
            var arterials = plan.GetConnectionsByClass(RoadClass.Arterial);
            foreach (var conn in arterials)
            {
                for (int cp = 1; cp <= checkpointsPerArterial; cp++)
                {
                    float t = cp / (float)(checkpointsPerArterial + 1);
                    Vector3 cpPos = Vector3.Lerp(conn.start, conn.end, t);

                    GameObject checkpoint = new GameObject($"Checkpoint_{conn.fromDistrictId}_{cp}");
                    checkpoint.transform.SetParent(landmarkRoot);
                    checkpoint.transform.position = cpPos;

                    // Trigger zone
                    BoxCollider cpTrigger = checkpoint.AddComponent<BoxCollider>();
                    cpTrigger.isTrigger = true;
                    cpTrigger.size = new Vector3(25f, 5f, 3f);

                    // Visual gate posts
                    Vector3 dir = (conn.end - conn.start).normalized;
                    Vector3 right = Vector3.Cross(dir, Vector3.up).normalized;

                    foreach (float side in new[] { -12f, 12f })
                    {
                        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        post.name = "Checkpoint_Post";
                        post.transform.SetParent(checkpoint.transform);
                        post.transform.localPosition = right * side + Vector3.up * 3f;
                        post.transform.localScale = new Vector3(0.5f, 3f, 0.5f);
                        DestroyImmediate(post.GetComponent<CapsuleCollider>());

                        var postMat = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
                        if (postMat.HasProperty("_BaseColor"))
                            postMat.SetColor("_BaseColor", new Color(1f, 0.6f, 0f)); // orange
                        if (postMat.HasProperty("_EmissiveColor"))
                        {
                            postMat.EnableKeyword("_EMISSION");
                            postMat.SetColor("_EmissiveColor", new Color(1f, 0.6f, 0f) * 2f);
                        }
                        post.GetComponent<Renderer>().sharedMaterial = postMat;
                    }

                    count++;
                }
            }

            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // STEP 4: TRAFFIC SPAWN POINTS
        //
        // Place invisible spawn markers at district perimeter edges.
        // Traffic system can query these with FindObjectsOfType<TrafficSpawnPoint>
        // to know where to instantiate NPC vehicles.
        // ─────────────────────────────────────────────────────────────────────

        private int PlaceTrafficSpawnPoints(WorldPlan plan, WorldGenerationConfig config)
        {
            int count = 0;

            Transform generated = GameObject.Find("Generated")?.transform;
            if (generated == null) return 0;

            Transform propsRoot = generated.Find("Props") ?? CreateChild(generated, "Props");

            foreach (var district in plan.districts)
            {
                if (district.districtType == DistrictType.Highway) continue;

                for (int i = 0; i < trafficSpawnsPerDistrict; i++)
                {
                    float angle = i * Mathf.PI * 2f / trafficSpawnsPerDistrict;
                    float spawnR = district.radius * 0.85f;

                    Vector3 pos = district.center + new Vector3(
                        Mathf.Cos(angle) * spawnR, 0f,
                        Mathf.Sin(angle) * spawnR);

                    // Direction: tangent to the perimeter (clockwise)
                    Vector3 forward = new Vector3(
                        -Mathf.Sin(angle), 0f,
                        Mathf.Cos(angle));

                    GameObject spawnObj = new GameObject($"TrafficSpawn_{district.id}_{i}");
                    spawnObj.transform.SetParent(propsRoot);
                    spawnObj.transform.position = pos;
                    spawnObj.transform.rotation = Quaternion.LookRotation(forward);

                    var spawn = spawnObj.AddComponent<TrafficSpawnPoint>();
                    spawn.districtId      = district.id;
                    spawn.districtType    = district.districtType;
                    spawn.spawnDirection  = forward;

                    count++;
                }
            }

            if (config.logGeneration)
                Debug.Log($"[GameplayTags] Placed {count} traffic spawn points");

            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private Transform CreateChild(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            return go.transform;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // RUNTIME METADATA COMPONENTS
    //
    // Lightweight MonoBehaviours attached to scene objects for runtime queries.
    // Race/Traffic systems use FindObjectsOfType<T> to discover these.
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attached to each district root — tells the game which district this is.
    /// Queryable at runtime: FindObjectsOfType<DistrictMetadata>()
    /// </summary>
    public class DistrictMetadata : MonoBehaviour
    {
        public string districtId;
        public DistrictType districtType;
        public Vector3 center;
        public float radius;

        /// <summary>
        /// Check if a world position is inside this district.
        /// </summary>
        public bool ContainsPoint(Vector3 worldPos)
        {
            Vector2 center2D = new Vector2(center.x, center.z);
            Vector2 pos2D    = new Vector2(worldPos.x, worldPos.z);
            return (pos2D - center2D).sqrMagnitude <= radius * radius;
        }
    }

    /// <summary>
    /// Attached to each road mesh object — tells the game which road class it is.
    /// </summary>
    public class RoadMetadata : MonoBehaviour
    {
        public string roadName;
        public RoadClass roadClass;
    }

    /// <summary>
    /// Attached to traffic spawn point markers — used by the traffic system
    /// to know where to instantiate NPC vehicles and which direction they face.
    /// </summary>
    public class TrafficSpawnPoint : MonoBehaviour
    {
        public string districtId;
        public DistrictType districtType;
        public Vector3 spawnDirection;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.6f);
            Gizmos.DrawSphere(transform.position, 3f);
            Gizmos.DrawRay(transform.position, spawnDirection * 10f);
        }
#endif
    }
}

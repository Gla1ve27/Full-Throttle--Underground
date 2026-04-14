# FULL THROTTLE — AAA PROCEDURAL WORLD GENERATION SYSTEM
**Target Scene:** `FastWorld.unity`  
**Engine:** Unity 6000.x / HDRP  
**Road Stack:** EasyRoads3D Pro v3  
**City / Block Population:** Fantastic City Generator (FCG)  
**Execution Mode:** Claude / Codex implementation spec

---

# 1. ROLE

You are implementing a **fully procedural open-world generation pipeline** for **Full Throttle**.

The result must produce a **driveable racing world** with these procedural districts:

1. **Mountain District**  
   Uphill / downhill roads, drift corners, scenic elevation, sparse structures, rocks, foliage.

2. **City Core District**  
   Dense urban grid-like local streets, high visual density, buildings, props, lights, intersections.

3. **Arterial District**  
   Speed-friendly medium-width connectors with readable curves and controlled intersections.

4. **Highway / Motorway District**  
   Outer loop and major fast corridors for top-speed driving, long curves, smooth merges, minimal hard stops.

The world must feel like a **racing map first**, not a simulation city.

---

# 2. PRIMARY DESIGN LAW

## 2.1 Ownership
- **EasyRoads3D owns ALL final drivable roads**
- **FCG does NOT own final road logic**
- **FCG is used for city blocks, buildings, props, and optionally visual bridge massing**
- If FCG generates roads internally, those roads must be disabled, hidden, ignored, or replaced by EasyRoads output

## 2.2 Connectivity
Every place in the generated world must be reachable by driving a car.

This includes:
- City Core to Arterial
- Arterial to Highway
- Highway to Mountain
- Mountain back to City or Highway
- Any FCG-generated block cluster used as a “bridge” between places

No visual-only adjacency is allowed.

## 2.3 Racing Flow
Do NOT generate generic GTA-like dense random road spam.

Prioritize:
- long readable curves
- lane continuity
- controlled elevation
- sustained speed
- district identity
- landmark readability
- drift-capable mountain sections
- fast arterial connectors
- high-speed motorway segments

---

# 3. SYSTEM GOAL

Build a procedural system that can:
- generate a valid macro world layout
- allocate district bounds
- build EasyRoads spline networks procedurally
- populate city and side areas using FCG and available environment assets
- create traversable bridging between generated zones
- validate driveability
- regenerate when requested
- keep hierarchy organized
- remain extensible for future event districts, garages, gas stations, race hubs, and landmarks

---

# 4. FINAL WORLD MODEL

The procedural world is composed of the following layers:

## 4.1 Layer A — World Plan
This is the abstract plan of the world:
- seed
- world bounds
- district placement
- connection graph
- landmark points
- road classes
- district priorities

## 4.2 Layer B — Road Skeleton
EasyRoads3D spline network generates:
- motorway loop
- arterial connectors
- local city roads
- mountain roads
- ramps / merges / transitions

## 4.3 Layer C — District Population
FCG and environment placement generate:
- building blocks
- sidewalks
- props
- signs
- foliage
- rocks
- barriers
- lighting
- city dressing
- highway roadside dressing

## 4.4 Layer D — Traversal Validation
The system validates:
- road continuity
- collider continuity
- district accessibility
- no broken road joins
- no fake bridges
- playable circulation

## 4.5 Layer E — Runtime Services
Supporting systems:
- minimap district tagging
- checkpoint / traffic spawn zones
- race route authoring support
- event space reservation
- future streaming compatibility

---

# 5. TARGET SCENE HIERARCHY

Use or refactor toward this structure:

```text
World
├── RuntimeRoot
│   ├── GenerationBootstrap
│   ├── WorldState
│   ├── WorldSeedController
│   └── ValidationSystem
├── Generated
│   ├── RoadNetwork
│   │   ├── Highway
│   │   ├── Arterials
│   │   ├── CityRoads
│   │   └── MountainRoads
│   ├── Districts
│   │   ├── MountainDistrict
│   │   ├── CityCoreDistrict
│   │   ├── ArterialDistrict
│   │   └── HighwayDistrict
│   ├── BridgesAndTransitions
│   ├── Buildings
│   ├── Props
│   ├── Foliage
│   ├── Lights
│   ├── Barriers
│   └── Landmarks
├── Gameplay
│   ├── PlayerCar
│   ├── TrafficSystem
│   ├── RaceSystem
│   ├── Checkpoints
│   └── SpawnPoints
└── Debug
    ├── Gizmos
    ├── DistrictBounds
    ├── RoutePreview
    └── ValidationMarkers
```

---

# 6. PROCEDURAL GENERATION ORDER

Generation must run in this order.

## Step 1 — Build World Plan
Create a seeded plan containing:
- world extents
- district centers
- district radii / bounds
- district relationship graph
- target connection count
- landmark anchors
- mountain elevation zone
- city density zone
- arterial corridors
- motorway loop envelope

## Step 2 — Generate Highway / Motorway
This is the first drivable backbone.
Requirements:
- wide lanes
- long loops / semi-loops
- large radii
- few interruptions
- supports top speed
- can surround or partially surround the world

## Step 3 — Generate Arterials
Arterials connect the world.
Requirements:
- medium width
- smooth high-speed curves
- district-to-district readability
- avoid over-intersection
- serve as transition roads

## Step 4 — Generate Mountain Network
Mountain roads should:
- use elevation changes
- support drift lines
- include scenic loops, climbs, descents
- avoid tiny city-like block spacing
- allow return to arterial or highway

## Step 5 — Generate City Core Road Overlay
City local roads are still EasyRoads roads.
Requirements:
- denser pattern than outer districts
- more intersections than other districts
- still race-playable
- avoid frustrating stop-start micro blocks

## Step 6 — Populate Districts
After roads exist:
- use FCG to generate city massing within bounded zones
- disable or ignore FCG roads
- place environment assets around district-specific roads
- use palm trees, foliage, lights, barriers, rocks, signage, and props where relevant

## Step 7 — Build Bridges / Transitional Connectors
If disconnected district clusters exist:
- generate true driveable corridors
- road endpoints must physically meet
- bridge areas must support actual traversal
- no decorative gaps

## Step 8 — Validate Driveability
Run validation:
- all districts reachable
- no road endpoint holes
- no collider breaks
- no floating connector failures
- no impossible transitions

## Step 9 — Apply Gameplay Tags
Assign metadata:
- district IDs
- road class IDs
- speed zone types
- traffic eligibility
- race suitability
- drift area flags
- scenic route flags

---

# 7. DISTRICT RULES

## 7.1 Mountain District
Purpose:
- drift gameplay
- scenic runs
- elevation identity

Requirements:
- strong elevation variance
- broad switchbacks
- readable hairpins only where intentional
- cliffs, rocks, sparse buildings
- lower urban density
- optional lookouts or event turnouts

Use:
- rocks
- cliff meshes
- sparse foliage
- guard rails
- mountain barriers
- signs
- occasional industrial or roadside structures

## 7.2 City Core
Purpose:
- urban racing
- intersections
- visual spectacle
- dense environment

Requirements:
- EasyRoads local streets
- FCG for buildings and city massing
- denser prop placement
- lighting density higher than other districts
- signage, sidewalks, urban furniture
- enough openness for racing lines

Use:
- FCG city blocks
- lights
- barriers
- ads / signs
- parked props if available
- palm trees if district style supports it

## 7.3 Arterial District
Purpose:
- readable connectors
- medium-speed flow
- links across world

Requirements:
- broad curves
- fewer hard stops
- lanes feel transitional between city and highway
- moderate dressing
- visible route rhythm

Use:
- roadside props
- trees
- signs
- barriers
- occasional low-density structures

## 7.4 Highway / Motorway
Purpose:
- top-speed runs
- long pursuits
- major circulation

Requirements:
- wide carriageways
- controlled merges
- long sightlines
- no excessive sharp curves
- medians / barriers where appropriate

Use:
- barriers
- signs
- lamp posts
- overpasses if possible
- side dressing for speed illusion

---

# 8. FCG BRIDGING RULE

If FCG is used as a “bridge” between zones, the result must behave like a real traversable connection.

That means:
- road entry point exists
- road exit point exists
- collider continuity exists
- the player can enter, pass through, and exit by car
- surrounding geometry does not trap the player
- visual block connectors support the drivable path

Never place two districts near each other and pretend they are connected if the car cannot actually drive between them.

---

# 9. PROCEDURAL ARCHITECTURE

Implement the system using the following runtime classes.

## Core Runtime Classes
- `WorldGenerationBootstrap`
- `ProceduralWorldPlanner`
- `DistrictLayoutGenerator`
- `EasyRoadsNetworkBuilder`
- `FCGPopulationIntegrator`
- `BridgeConnectorGenerator`
- `WorldValidationSystem`
- `GeneratedWorldRegistry`

## Data Classes
- `WorldGenerationConfig`
- `WorldPlan`
- `DistrictPlan`
- `RoadConnectionPlan`
- `LandmarkPlan`
- `GenerationResult`

## Optional Future Classes
- `TrafficZoneAuthoring`
- `RaceRouteAuthoring`
- `StreamingChunkPlanner`
- `LandmarkSpawner`
- `DebugGizmoDrawer`

---

# 10. SCRIPT SPECIFICATION

Below are the scripts to implement first. Keep code modular and editor-friendly.

---

## 10.1 WorldGenerationConfig.cs

```csharp
using System;
using UnityEngine;

[CreateAssetMenu(menuName = "FullThrottle/World Generation Config")]
public class WorldGenerationConfig : ScriptableObject
{
    [Header("Seed")]
    public int seed = 12345;
    public bool randomizeSeed = false;

    [Header("World Size")]
    public Vector2 worldSize = new Vector2(6000f, 6000f);

    [Header("District Sizes")]
    public float cityCoreRadius = 500f;
    public float mountainRadius = 900f;
    public float arterialSpan = 2200f;
    public float highwayInset = 350f;

    [Header("Road Shape")]
    public int highwayControlPointCount = 10;
    public int arterialConnectionCount = 6;
    public int mountainRoadCount = 3;
    public int cityRoadDensity = 16;

    [Header("Validation")]
    public bool runValidationAfterGeneration = true;
    public bool clearPreviousGeneration = true;

    [Header("Population")]
    public bool generateCityWithFCG = true;
    public bool placeEnvironmentProps = true;
    public bool placeDistrictFoliage = true;

    [Header("Debug")]
    public bool logGeneration = true;
    public bool drawDebugGizmos = true;
}
```

---

## 10.2 WorldPlan.cs

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WorldPlan
{
    public int seed;
    public Vector2 worldSize;
    public List<DistrictPlan> districts = new();
    public List<RoadConnectionPlan> connections = new();
    public List<LandmarkPlan> landmarks = new();
}
```

---

## 10.3 DistrictPlan.cs

```csharp
using System;
using UnityEngine;

public enum DistrictType
{
    Mountain,
    CityCore,
    Arterial,
    Highway
}

[Serializable]
public class DistrictPlan
{
    public string id;
    public DistrictType districtType;
    public Vector3 center;
    public float radius;
    public Bounds bounds;
}
```

---

## 10.4 RoadConnectionPlan.cs

```csharp
using System;
using UnityEngine;

public enum RoadClass
{
    Highway,
    Arterial,
    CityLocal,
    Mountain
}

[Serializable]
public class RoadConnectionPlan
{
    public string fromDistrictId;
    public string toDistrictId;
    public RoadClass roadClass;
    public Vector3 start;
    public Vector3 end;
}
```

---

## 10.5 LandmarkPlan.cs

```csharp
using System;
using UnityEngine;

[Serializable]
public class LandmarkPlan
{
    public string id;
    public Vector3 position;
    public string category;
}
```

---

## 10.6 GeneratedWorldRegistry.cs

```csharp
using System.Collections.Generic;
using UnityEngine;

public class GeneratedWorldRegistry : MonoBehaviour
{
    public readonly List<GameObject> spawnedRoots = new();

    public void Register(GameObject go)
    {
        if (go != null && !spawnedRoots.Contains(go))
            spawnedRoots.Add(go);
    }

    public void ClearAll()
    {
        for (int i = spawnedRoots.Count - 1; i >= 0; i--)
        {
            if (spawnedRoots[i] != null)
                DestroyImmediate(spawnedRoots[i]);
        }

        spawnedRoots.Clear();
    }
}
```

---

## 10.7 ProceduralWorldPlanner.cs

```csharp
using UnityEngine;

public class ProceduralWorldPlanner : MonoBehaviour
{
    public WorldPlan BuildPlan(WorldGenerationConfig config)
    {
        int seed = config.randomizeSeed ? Random.Range(int.MinValue, int.MaxValue) : config.seed;
        Random.InitState(seed);

        WorldPlan plan = new WorldPlan
        {
            seed = seed,
            worldSize = config.worldSize
        };

        Vector3 cityCenter = new Vector3(0f, 0f, 0f);
        Vector3 mountainCenter = new Vector3(config.worldSize.x * 0.25f, 0f, config.worldSize.y * 0.20f);
        Vector3 arterialCenter = new Vector3(-config.worldSize.x * 0.15f, 0f, 0f);
        Vector3 highwayCenter = Vector3.zero;

        plan.districts.Add(CreateDistrict("CITY_CORE", DistrictType.CityCore, cityCenter, config.cityCoreRadius));
        plan.districts.Add(CreateDistrict("MOUNTAIN", DistrictType.Mountain, mountainCenter, config.mountainRadius));
        plan.districts.Add(CreateDistrict("ARTERIAL", DistrictType.Arterial, arterialCenter, config.arterialSpan * 0.35f));
        plan.districts.Add(CreateDistrict("HIGHWAY", DistrictType.Highway, highwayCenter, Mathf.Max(config.worldSize.x, config.worldSize.y) * 0.45f));

        AddCoreConnections(plan);

        return plan;
    }

    private DistrictPlan CreateDistrict(string id, DistrictType type, Vector3 center, float radius)
    {
        DistrictPlan d = new DistrictPlan
        {
            id = id,
            districtType = type,
            center = center,
            radius = radius
        };

        d.bounds = new Bounds(center, new Vector3(radius * 2f, 500f, radius * 2f));
        return d;
    }

    private void AddCoreConnections(WorldPlan plan)
    {
        DistrictPlan city = plan.districts.Find(d => d.id == "CITY_CORE");
        DistrictPlan mountain = plan.districts.Find(d => d.id == "MOUNTAIN");
        DistrictPlan arterial = plan.districts.Find(d => d.id == "ARTERIAL");
        DistrictPlan highway = plan.districts.Find(d => d.id == "HIGHWAY");

        plan.connections.Add(new RoadConnectionPlan
        {
            fromDistrictId = city.id,
            toDistrictId = arterial.id,
            roadClass = RoadClass.Arterial,
            start = city.center,
            end = arterial.center
        });

        plan.connections.Add(new RoadConnectionPlan
        {
            fromDistrictId = arterial.id,
            toDistrictId = mountain.id,
            roadClass = RoadClass.Arterial,
            start = arterial.center,
            end = mountain.center
        });

        plan.connections.Add(new RoadConnectionPlan
        {
            fromDistrictId = city.id,
            toDistrictId = highway.id,
            roadClass = RoadClass.Highway,
            start = city.center,
            end = highway.center + new Vector3(600f, 0f, 0f)
        });

        plan.connections.Add(new RoadConnectionPlan
        {
            fromDistrictId = mountain.id,
            toDistrictId = highway.id,
            roadClass = RoadClass.Highway,
            start = mountain.center,
            end = highway.center + new Vector3(-700f, 0f, 700f)
        });
    }
}
```

---

## 10.8 EasyRoadsNetworkBuilder.cs

Note: adapt this class to the actual EasyRoads3D API already present in the project.  
Do not invent unsupported API calls. Inspect installed EasyRoads components first.

```csharp
using System.Collections.Generic;
using UnityEngine;

public class EasyRoadsNetworkBuilder : MonoBehaviour
{
    [SerializeField] private Transform roadRoot;

    public void BuildFromPlan(WorldPlan plan, WorldGenerationConfig config)
    {
        Debug.Log("EasyRoadsNetworkBuilder: Begin road generation");

        BuildHighway(plan, config);
        BuildArterials(plan, config);
        BuildMountainRoads(plan, config);
        BuildCityRoads(plan, config);
    }

    private void BuildHighway(WorldPlan plan, WorldGenerationConfig config)
    {
        Debug.Log("BuildHighway: Implement with EasyRoads spline API");
        // Create outer loop / ring using sampled points and smooth tangents.
    }

    private void BuildArterials(WorldPlan plan, WorldGenerationConfig config)
    {
        foreach (var connection in plan.connections)
        {
            if (connection.roadClass != RoadClass.Arterial)
                continue;

            Debug.Log($"BuildArterials: {connection.fromDistrictId} -> {connection.toDistrictId}");
            // Build smooth connection spline from start to end.
        }
    }

    private void BuildMountainRoads(WorldPlan plan, WorldGenerationConfig config)
    {
        Debug.Log("BuildMountainRoads: Generate elevation-based splines");
        // Add climb/descent loops near mountain district.
    }

    private void BuildCityRoads(WorldPlan plan, WorldGenerationConfig config)
    {
        Debug.Log("BuildCityRoads: Generate local downtown road set");
        // Create smaller local roads inside city bounds.
    }
}
```

---

## 10.9 FCGPopulationIntegrator.cs

Note: inspect actual FCG components in the project before coding integration.

```csharp
using UnityEngine;

public class FCGPopulationIntegrator : MonoBehaviour
{
    [SerializeField] private Transform buildingsRoot;
    [SerializeField] private Transform propsRoot;

    public void Populate(WorldPlan plan, WorldGenerationConfig config)
    {
        if (!config.generateCityWithFCG)
            return;

        DistrictPlan city = plan.districts.Find(d => d.districtType == DistrictType.CityCore);
        if (city == null)
        {
            Debug.LogWarning("FCGPopulationIntegrator: City district not found");
            return;
        }

        Debug.Log("FCGPopulationIntegrator: Generate bounded city blocks here");
        Debug.Log("FCGPopulationIntegrator: Disable or ignore FCG-generated road output");
    }
}
```

---

## 10.10 BridgeConnectorGenerator.cs

```csharp
using UnityEngine;

public class BridgeConnectorGenerator : MonoBehaviour
{
    public void EnsureConnectivity(WorldPlan plan)
    {
        Debug.Log("BridgeConnectorGenerator: Ensuring district traversal");

        foreach (var connection in plan.connections)
        {
            Debug.Log($"Validate connection {connection.fromDistrictId} -> {connection.toDistrictId}");
            // If a spline or corridor is missing, build a transitional connector.
        }
    }
}
```

---

## 10.11 WorldValidationSystem.cs

```csharp
using System.Collections.Generic;
using UnityEngine;

public class WorldValidationSystem : MonoBehaviour
{
    public bool Validate(WorldPlan plan)
    {
        bool valid = true;

        if (plan == null)
        {
            Debug.LogError("Validation failed: plan is null");
            return false;
        }

        if (plan.districts == null || plan.districts.Count < 4)
        {
            Debug.LogError("Validation failed: missing districts");
            valid = false;
        }

        if (plan.connections == null || plan.connections.Count == 0)
        {
            Debug.LogError("Validation failed: missing connections");
            valid = false;
        }

        HashSet<string> districtIds = new();
        foreach (var district in plan.districts)
            districtIds.Add(district.id);

        foreach (var connection in plan.connections)
        {
            if (!districtIds.Contains(connection.fromDistrictId) || !districtIds.Contains(connection.toDistrictId))
            {
                Debug.LogError($"Validation failed: bad connection {connection.fromDistrictId} -> {connection.toDistrictId}");
                valid = false;
            }
        }

        Debug.Log(valid ? "World validation passed" : "World validation failed");
        return valid;
    }
}
```

---

## 10.12 WorldGenerationBootstrap.cs

```csharp
using UnityEngine;

public class WorldGenerationBootstrap : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private WorldGenerationConfig config;

    [Header("Systems")]
    [SerializeField] private GeneratedWorldRegistry registry;
    [SerializeField] private ProceduralWorldPlanner planner;
    [SerializeField] private EasyRoadsNetworkBuilder roadBuilder;
    [SerializeField] private FCGPopulationIntegrator populationIntegrator;
    [SerializeField] private BridgeConnectorGenerator bridgeConnector;
    [SerializeField] private WorldValidationSystem validationSystem;

    private WorldPlan _currentPlan;

    private void Start()
    {
        GenerateWorld();
    }

    [ContextMenu("Generate World")]
    public void GenerateWorld()
    {
        if (config == null)
        {
            Debug.LogError("WorldGenerationBootstrap: Missing config");
            return;
        }

        if (registry != null && config.clearPreviousGeneration)
            registry.ClearAll();

        _currentPlan = planner.BuildPlan(config);

        roadBuilder.BuildFromPlan(_currentPlan, config);
        populationIntegrator.Populate(_currentPlan, config);
        bridgeConnector.EnsureConnectivity(_currentPlan);

        if (config.runValidationAfterGeneration)
            validationSystem.Validate(_currentPlan);

        Debug.Log("WorldGenerationBootstrap: Generation complete");
    }
}
```

---

# 11. EDITOR / EXECUTION RULES FOR CLAUDE

Claude must follow these rules when implementing:

1. **Inspect existing EasyRoads3D API usage in the project before writing builder code**
2. **Inspect existing FCG generator scripts before integrating city generation**
3. **Reuse existing scene structure where safe**
4. **Do not delete unrelated gameplay systems**
5. **Do not replace working car, camera, race, or traffic systems**
6. **Do not make assumptions about third-party API method names**
7. **Wrap risky integrations behind clear adapters**
8. **Prefer additive implementation over destructive rewrite**
9. **Keep generated content under dedicated parent roots**
10. **Expose key values in inspector**
11. **Add debug logs and gizmos**
12. **Keep future streaming support in mind**

---

# 12. ASSET USAGE POLICY

Use all relevant existing assets found under `Assets/` when suitable.

Eligible asset categories include:
- palm trees
- general trees
- roadside foliage
- rocks
- barriers
- signs
- lights
- urban props
- environmental props
- bridge / guardrail assets
- highway dressing assets
- mountain dressing assets

Apply by district:

## Mountain
- rocks
- cliffs
- sparse foliage
- rails
- warning signs

## City
- buildings
- streetlights
- urban props
- signs
- palms / decorative vegetation if appropriate

## Arterial
- medium roadside dressing
- signage
- barriers
- occasional low-density foliage / structures

## Highway
- barriers
- signs
- lights
- tightly spaced side objects for speed sensation

---

# 13. ACCEPTANCE CRITERIA

The implementation is accepted only if all of the following are true:

## Functional
- FastWorld generates a world procedurally
- all 4 districts exist
- all 4 districts are reachable by driving
- no disconnected fake bridge areas
- road generation runs without major errors

## Playability
- city is dense enough to feel urban
- mountain feels elevated and drift-oriented
- arterials feel fast and transitional
- highway feels like top-speed space
- world circulation is intuitive

## Technical
- generated objects are organized under hierarchy roots
- generation can be rerun
- config is exposed in inspector
- validation reports connection problems
- integration does not destroy unrelated systems

---

# 14. PHASED IMPLEMENTATION PLAN

Implement in this exact sequence.

## Phase 1
- add config/data classes
- add planner
- add bootstrap
- add validation
- establish hierarchy

## Phase 2
- implement EasyRoads builder adapter
- generate motorway backbone
- generate arterial connectors
- generate basic mountain roads
- generate city local roads

## Phase 3
- integrate FCG inside city bounds
- disable FCG road ownership
- add district-based asset population

## Phase 4
- implement bridge connector recovery
- run accessibility checks
- fix disconnected paths

## Phase 5
- add metadata tags
- improve district dressing
- add debug gizmos
- prep for traffic/race spawn integration

---

# 15. IMPORTANT NON-GOALS

Do NOT:
- build a perfect real-world city sim
- flood the map with tiny intersections
- rely on FCG roads as final gameplay roads
- create non-driveable decorative district chunks
- rewrite unrelated systems without necessity
- hardcode third-party API calls without inspection

---

# 16. FINAL DIRECTIVE TO CLAUDE

Execute this as a project modification task.

You must:
- inspect current scripts and third-party integrations first
- implement the architecture above in safe phases
- produce working modular scripts
- preserve existing project systems where possible
- prioritize drivable connectivity and racing flow over realism

When uncertain, choose the option that improves:
1. driveability
2. readability
3. speed flow
4. modularity
5. future expandability

END OF FILE

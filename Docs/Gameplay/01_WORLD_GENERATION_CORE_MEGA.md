# 01_WORLD_GENERATION_CORE_MEGA — CLAUDE EXECUTION VERSION

## ROLE
You are implementing the world-generation architecture for **Full Throttle** in **Unity**.
Target scene: `FastWorld.unity`

This document is an **execution specification**, not a brainstorming brief.
Do not rewrite the design into a different concept.
Do not replace the toolchain.
Do not simplify the map into a generic procedural city.

---

## PRIMARY OBJECTIVE
Build or refactor the `FastWorld.unity` world-generation pipeline so the final map contains **four distinct drivable environments**:

1. **Mountainous Drift District**
2. **City Core**
3. **Arterial Network**
4. **Highway / Motorway Network**

The map must feel like a **curated arcade racing world**.
It must **not** feel like a flat GTA-style procedural grid.

---

## MANDATORY TOOL OWNERSHIP
Use the project’s existing assets and systems.

### EasyRoads3D Pro v3 is the master system for:
- road spline authoring
- terrain-conforming roads
- road width
- shoulders
- intersections
- ramps
- bridges
- banking where appropriate
- elevation changes
- mountain routes
- arterials
- highways / motorways
- city primary road paths

### Fantastic City Generator (FCG) is support-only for:
- city block population
- building placement
- urban massing
- skyline density
- city props / city dressing
- optional local side streets only when they do not damage road flow

### Existing `Assets/` folder content must be used where relevant for:
- trees
- palms
- shrubs
- foliage
- rocks
- cliffs
- barriers
- guardrails
- street lights
- utility poles
- fences
- signage
- billboards
- decals
- sidewalks
- medians
- highway props
- industrial props
- urban props
- scenic props
- roadside clutter
- other compatible environment prefabs already present in the project

### Hard Rule
**FCG must not become the primary road-authoring system.**
If FCG generates roads that conflict with intended racing flow, they must be disabled, hidden, reduced, or restricted to safe local-use cases inside the City Core only.

---

## REQUIRED FINAL MAP EXPERIENCE
The player must be able to do all of the following inside `FastWorld.unity`:

- drive through a dense urban core
- exit the city through fast connector roads
- merge into wide, high-speed highways
- transition into mountain roads with uphill and downhill drift sections
- loop back into the city through a different connector or motorway route
- access major places by driving, including any bridged district connectors generated with FCG or EasyRoads

The world must support **flow**, **speed**, **district readability**, and **driving variety**.

---

## REQUIRED WORLD STRUCTURE

### 1. Mountainous Drift District
Purpose:
- drifting
- technical handling
- uphill/downhill driving
- scenic contrast

Required geometry:
- elevation-rich roads
- uphill and downhill sections
- medium curves
- tight curves
- selected hairpins
- switchback-like rhythm where suitable
- recovery straights between technical sections
- readable road edges

Required dressing:
- rocks
- cliff or slope dressing
- natural foliage
- guardrails on dangerous edges
- warning signs
- sparse structures only when appropriate
- scenic pull-offs / overlooks if feasible

Restrictions:
- do not urbanize this zone heavily
- do not make it flat
- do not treat it like a suburban filler area

### 2. City Core
Purpose:
- main downtown driving space
- free roam
- event-friendly urban district
- dense spectacle

Required geometry:
- broad primary avenues
- secondary urban connectors
- controlled intersections
- shortcuts and route choices
- avoid excessive dead-stop gridlock
- maintain arcade readability

Required dressing:
- dense FCG buildings
- street lights
- signs
- poles
- sidewalks
- urban clutter
- parking/service/commercial details where supported by existing assets
- skyline presence

Restrictions:
- do not allow city density to choke all vehicle flow
- do not produce an overly tiny block pattern with nonstop stop-start driving

### 3. Arterial Network
Purpose:
- connect major districts
- maintain speed between destinations
- feel smoother than dense city streets

Required geometry:
- long readable curves
- higher average corner speed
- wider lanes than local city roads
- gentle elevation transitions
- fewer harsh 90-degree turns

Required dressing:
- medians
- lamp rhythm
- palms where biome/style appropriate
- barriers / guardrails where needed
- transitional greenery
- low-to-mid roadside development where appropriate

Restrictions:
- do not make arterials feel like local side streets
- do not clutter edges so heavily that readability is lost at speed

### 4. Highway / Motorway Network
Purpose:
- top-speed runs
- long travel routes
- pursuit routes
- map circulation backbone

Required geometry:
- widest roads in the map
- long uninterrupted sections
- sweeping curves
- merges and splits
- on-ramps / off-ramps
- interchanges with arterials
- overpasses / underpasses where feasible

Required dressing:
- barriers
- fencing
- highway lights
- overhead signs
- large-scale roadside visuals
- controlled, sparse clutter

Restrictions:
- do not over-detail highway edges with slow-speed urban clutter
- do not let highways collapse into normal city-road proportions

---

## REQUIRED SYSTEM ARCHITECTURE
The world-generation pipeline must be district-first and road-first.

### Required ownership model
- **EasyRoadsNetworkController**: owns primary drivable road hierarchy
- **TerrainMacroShapeController**: owns terrain macro forms and mountain/city/road corridor shaping
- **DistrictBoundsController**: defines zone masks / inclusion areas
- **FCGCityCoreController**: populates bounded city areas with FCG
- **DistrictPropSpawner**: places props per district profile
- **BiomeFoliageDistributor**: places foliage according to district identity
- **RoadsideSafetyLayer**: ensures barriers / safety dressing on dangerous road edges
- **WorldGenerationBootstrap**: orchestrates order of operations and dependency setup

You may adapt the exact class names only if the project already uses different established naming, but the same responsibilities must exist.

---

## REQUIRED SCENE ORGANIZATION
`FastWorld.unity` should be organized close to the following structure:

```text
FastWorld
 ├── WorldGenerationBootstrap
 ├── TerrainRoot
 │    ├── MainTerrain
 │    └── TerrainBiomeMasks
 ├── RoadNetwork
 │    ├── ER3D_Motorways
 │    ├── ER3D_Arterials
 │    ├── ER3D_MountainRoads
 │    └── ER3D_CityRoads
 ├── Districts
 │    ├── CityCoreZone
 │    ├── MountainDriftZone
 │    ├── ArterialZones
 │    └── HighwayZones
 ├── FCG
 │    ├── FCG_CityCore
 │    ├── FCG_CityEdge
 │    └── FCG_OptionalIndustrialPockets
 ├── Props
 │    ├── HighwayProps
 │    ├── ArterialProps
 │    ├── CityProps
 │    ├── MountainProps
 │    └── SharedProps
 ├── Foliage
 │    ├── PalmTrees
 │    ├── Trees
 │    ├── Bushes
 │    └── GrassDetails
 └── Landmarks
      ├── EventHubs
      ├── Overlooks
      └── SpecialLocations
```

Minor deviations are acceptable only if the resulting structure is equally clear and maintainable.

---

## REQUIRED EXECUTION ORDER
Implement or refactor the pipeline in this order.
Do not skip the order unless the current codebase absolutely requires a different dependency order.

### Phase 0 — Inspect Existing World Generation
Before changing logic:
- inspect existing `WorldGenerationBootstrap`
- inspect all existing road generation scripts
- inspect how FCG is currently spawned
- inspect how terrain is currently shaped
- inspect current scene object organization
- inspect relevant environment prefabs inside `Assets/`
- identify conflicts where FCG roads or old procedural logic fight the intended EasyRoads hierarchy

### Phase 1 — Define District-First Layout
Create or refactor data/config so `FastWorld.unity` is built around these explicit zones:
- Mountainous Drift District
- City Core
- Arterial Network
- Highway / Motorway Network

This layout must be spatially coherent, not random.
Recommended macro arrangement:
- City Core near center / slightly off-center
- Mountain district on an outer side with major elevation
- Arterials connecting district transitions
- Highways / motorways as perimeter loop, bypass, or top-speed backbone

### Phase 2 — Terrain Macro Shaping
Before full prop or building population:
- create mountain mass / slope region
- create flatter city basin / platform
- create suitable corridors for arterials
- create suitable corridors for highways
- ensure mountain roads can support visible uphill/downhill travel

### Phase 3 — EasyRoads Primary Network Authoring
Build or refactor in this order:
1. highways / motorways
2. arterials
3. mountain roads
4. city primary avenues
5. city secondary roads only where necessary
6. ramps / interchanges / connectors

Requirements:
- highways remain the fastest class
- arterials remain speed-friendly
- mountain roads remain technical and elevation-rich
- city roads remain readable and urban

### Phase 4 — District Bounds and Masks
After road layout is stable:
- compute city bounds
- compute mountain bounds
- compute arterial corridor masks
- compute highway corridor masks

Use these bounds/masks to control:
- FCG inclusion
- foliage placement
- prop selection
- district dressing behavior
- road-edge cleanup logic

### Phase 5 — FCG Integration
Refactor FCG usage so:
- FCG primarily fills the City Core
- FCG can optionally support city-edge or light industrial pockets
- FCG does not overwrite the main drivable road identity
- FCG roads are disabled, minimized, hidden, or restricted where necessary

Preferred implementation:
- EasyRoads defines drivable roads
- FCG fills parcels between or around those roads

### Phase 5A — FCG Bridge Corridor Rule
If FCG is used as a bridging element between major places or districts, it must behave like a **drivable connector block**, not a disconnected visual island.

Required bridging behavior:
- each major place must be reachable by driving the car directly from another place
- bridge corridors must physically connect road endpoints from one district to the next
- FCG bridge blocks must align to the active drivable network instead of sitting near it decoratively
- the player must be able to exit one zone, drive across the bridge corridor, and enter the next zone without teleporting, resetting, or leaving the intended route
- bridge corridors may use repeated FCG road/block modules only when they preserve clean vehicle traversal

Layout interpretation based on the target structure:
- a district or place may be generated as a bounded cluster
- that cluster may be connected to another cluster using a narrow or medium-width bridging corridor
- the corridor may be straight or gently curved
- the corridor must remain car-drivable for normal travel speed
- the corridor must not terminate into dead geometry, non-drivable decorative meshes, or missing intersections

Ownership rule for bridging:
- EasyRoads3D remains the preferred owner for major bridge roads, long connectors, ramps, mountain access roads, arterials, and highways
- FCG bridging is allowed only when it is intentionally used to stitch bounded place-clusters together in a readable, drivable way
- if FCG bridge modules are used, they must be validated so wheel contact, collider continuity, lane readability, and entry/exit alignment are correct

Required connectivity rule:
- no major generated place should feel isolated unless it is a deliberate scenic dead-end
- City Core, Mountain district, arterial corridors, and highway network must all be accessible through actual road travel
- if a place is visually present on the map, the implementation should prefer making it reachable by car

Validation requirements for bridge corridors:
- test driving from City Core into another place through the bridge corridor
- test driving back from that place into the main network
- verify no gaps, height seams, invalid colliders, blocked joins, or broken lane transitions
- verify AI traffic compatibility if traffic uses the same route set

Do not:
- use FCG bridging as decoration only
- place disconnected city chunks that look connected but are not drivable
- create map islands that can only be reached by spawn or teleport unless explicitly intended for a special event use-case

### Phase 6 — District-Specific Prop and Foliage Pass
Use relevant existing assets from `Assets/`.

#### Mountain pass uses more:
- rocks
- slope dressing
- natural trees
- guardrails
- warning signs
- sparse utility props

#### City pass uses more:
- FCG buildings
- lights
- signs
- sidewalks
- urban clutter
- parking/service/commercial support assets
- decorative palms where biome/style appropriate

#### Arterial pass uses more:
- palms where appropriate
- medians
- lamps
- barriers
- transitional foliage
- low-to-mid roadside structures

#### Highway pass uses more:
- barriers
- fences
- highway signs
- overhead sign structures
- lights
- sparse vegetation masses
- sound walls if available

### Phase 7 — Safety, Cleanup, and Road Readability
After major generation:
- remove overlapping props
- remove vegetation from carriageways
- ensure dangerous mountain edges have safety treatment
- reduce clutter on fastest roads
- maintain readable road edges in all zones
- eliminate prop placements that break driving lines or camera readability

### Phase 8 — Validation and Tuning
Review the generated or assembled world and verify all four zones are distinct in:
- road geometry
- speed profile
- scenery
- prop language
- lane width impression
- density
- terrain profile

---

## REQUIRED ASSET USAGE INSTRUCTION
When implementing this document, do **not** hardcode the world around only one narrow set of prefabs.
You must inspect and use compatible content from the existing project `Assets/` folder.

Use all relevant owned content already available, especially:
- palms
- foliage
- terrain props
- road barriers
- signs
- lamps
- city props
- industrial props
- roadside props
- decorative environment meshes

Only exclude assets when they are technically incompatible, broken, or clearly wrong for the target district.

---

## DO NOT DO THESE THINGS
Do not:
- replace EasyRoads3D with FCG roads as the primary solution
- generate a uniform flat city across the entire map
- treat all districts as the same prop palette
- make the mountain zone mostly flat
- make arterials feel like cramped city streets
- make highways too narrow for top-speed fantasy
- overpopulate the motorway edge with dense sidewalk clutter
- let FCG produce the dominant gameplay road network
- ignore already-owned useful environment assets in `Assets/`

---

## ACCEPTANCE CRITERIA
The work is only considered correct if the final result satisfies all of the following.

### District identity acceptance
- `FastWorld.unity` clearly contains Mountain, City, Arterial, and Highway driving spaces
- each district is visually and geometrically distinct

### Road hierarchy acceptance
- highways are the fastest and widest roads
- arterials are smoother and faster than city roads
- mountain roads have meaningful elevation and drift-friendly sections
- city roads support urban driving without becoming endless dead-stop gridlock

### Tool ownership acceptance
- EasyRoads3D clearly owns the primary drivable road hierarchy
- FCG is clearly constrained to city population and bounded support roles

### Asset usage acceptance
- relevant environment content from `Assets/` is actually used across districts
- palms, foliage, props, barriers, and other environmental assets are not ignored when appropriate

### Flow acceptance
- the player can move naturally between city, arterial, highway, and mountain experiences
- the map supports looped driving routes rather than disconnected segments- bridged place-clusters are truly car-accessible and not just visually adjacent


### Cleanup acceptance
- roads are not blocked by obvious prop overlap
- dangerous mountain edges have safety treatment where needed
- fastest roads remain readable at speed

---

## IMPLEMENTATION PRIORITY
If time or scope forces prioritization, prioritize in this order:
1. correct district layout
2. correct EasyRoads road hierarchy
3. correct FCG restriction to City Core
4. correct mountain elevation design
5. correct arterial / highway flow
6. district-specific asset dressing
7. cleanup and readability improvements

Do not sacrifice the road hierarchy just to increase prop density.

---

## OUTPUT EXPECTATION
The implementation should result in:
- a refactored or newly structured `FastWorld.unity`
- improved world-generation control scripts or controllers
- FCG constrained to proper district usage
- EasyRoads-led road hierarchy
- district-specific dressing using existing assets
- a world that feels closer to an arcade racing map than a generic procedural city

---

## DIRECTIVE TO EXECUTION MODEL
Execute this as a **refactor and implementation task**.
Do not answer with only theory.
Do not collapse this into a short summary.
Inspect the current project structure, preserve what is useful, and modify the system so the final world matches this specification.

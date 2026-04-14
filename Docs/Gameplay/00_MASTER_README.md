# Full Throttle — AAA Build Bible
This pack replaces the thin draft pack. This version is intentionally dense, implementation-oriented, and split by system so an execution agent can work one module at a time without losing the design intent.

## How to use this pack
1. Read this file first.
2. Implement modules in numerical order.
3. Do not start the next module until the current module passes its validation gate.
4. Keep every module in its own branch or commit set.
5. Profile after each module, not only at the end.

## Design North Star
Full Throttle should feel like:
- the night-time intensity, speed fantasy, and pursuit pressure of a modern arcade street racer
- the road readability and open-world accessibility of a festival/open-road racer
- a handcrafted-feeling world built with procedural assistance, not a random city

## Hard production rules
- No system may be added if it makes the map less readable at speed.
- No environment prop may be placed if it breaks corner readability.
- No AI system may cheat by teleporting grip, instant rotation, or impossible acceleration.
- No event may be generated on a route that fails free-roam fun testing.
- No police escalation may create unavoidable frustration loops.
- No traffic spawn may appear inside player forward view cone at close range.
- Every main road must support at least one fun thing: sprinting, drifting, overtaking, evasion, or scenic cruising.

## Required module order
1. 01_WORLD_GENERATION_CORE_MEGA.md
2. 02_RACE_EVENT_ROUTE_SYSTEM_MEGA.md
3. 03_CAMERA_SPEED_DAYNIGHT_MEGA.md
4. 04_TRAFFIC_POLICE_RACE_AI_MEGA.md
5. 05_VEHICLE_HANDLING_AUDIO_MEGA.md
6. 06_META_PROGRESSION_UI_SAVE_MEGA.md
7. 07_INTEGRATION_TESTING_POLISH_MEGA.md

## Production mindset
Do not try to “finish the game” in one pass.
The correct order is:
- foundation
- drivability
- event generation
- AI
- progression
- polish
- optimization
- balancing

## Folder baseline
```text
Assets/
  FullThrottle/
    Art/
    Audio/
    Materials/
    Prefabs/
      AI/
      Events/
      Roads/
      Traffic/
      World/
      UI/
    Resources/
    Scenes/
    Scripts/
      AI/
      Audio/
      Camera/
      Events/
      Meta/
      Vehicle/
      World/
    Settings/
    Shaders/
    ScriptableObjects/
      AI/
      Events/
      Vehicle/
      World/
```

## Success definition
A valid vertical slice is reached only when:
- the player can free-roam through city, bridge, industrial, and outskirts zones
- at least 6 auto-generated events work end-to-end
- one full night cycle functions with correct lighting transitions
- traffic and police are active without collapsing performance
- at least 3 AI racers can complete a race with believable mistakes
- the car feels controllable, expressive, and fast

## Important correction from previous pack
The previous pack was too thin. This one is the real replacement. Use this pack instead.

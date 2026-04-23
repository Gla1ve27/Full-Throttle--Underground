# Full Throttle Sacred Core v1

This is a **clean-room foundation** for Full Throttle.
It is not a direct patch of the old stack.
It is the new core the game can start leaning on.

## Why this exists
The game direction should be built around:
- strong identity
- earned progression
- real risk and consequences
- structured career mode
- controlled chaos instead of sterile safety

That came directly from the transcript notes you gave for the racing-game direction of Full Throttle.

## What is inside
### Runtime
- `FTBootstrap.cs`
- `FTServices.cs`
- `FTSignals.cs`

### Save
- `FTProfileData.cs`
- `FTSaveGateway.cs`

### Story / Career
- `FTStoryActDefinition.cs`
- `FTRivalDefinition.cs`
- `FTCareerDirector.cs`

### Economy
- `FTRiskEconomyDirector.cs`

### Garage
- `FTSelectedCarRuntime.cs`
- `FTGarageDirector.cs`

### Vehicle
- `FTCarDefinition.cs`
- `FTCarRegistry.cs`
- `FTPlayerVehicleBinder.cs`
- `FTVehiclePhysicsGuard.cs`
- `FTVehicleSpawnDirector.cs`

### World
- `FTWorldTravelDirector.cs`
- `FTSpawnPoint.cs`

### Race
- `FTRaceDefinition.cs`
- `FTRaceDirector.cs`

### Audio
- `FTVehicleAudioProfile.cs`
- `FTAudioIdentityDirector.cs`

## What these scripts are meant to become
These are the **sacred scripts**.
They are the architectural truth for:
- Gla1ve's career state
- garage selection truth
- world handoff truth
- risk / payout / repair pressure
- act progression
- rival wins
- player-car spawn truth
- audio identity truth

## Gla1ve story direction built into this foundation
The save profile uses `playerAlias = "Gla1ve"` by default.
The first story act is `act_01_unknown_name`.
The intended story structure is:
1. Unknown Name
2. Territory Pressure
3. The City Starts Watching
4. Crown of the Night

## Important honesty note
This is a foundation, not the whole finished game.
It is intentionally the **backbone layer**.
It still needs your actual:
- prefabs
- UI bindings
- race route content
- camera setup
- audio mixer / loop player hookup
- custom vehicle feel implementation

## Recommended integration order
1. Bootstrap + Save
2. Car Registry + Selected Car Runtime
3. Garage Director
4. World Travel Director + Vehicle Spawn Director
5. Race Director + Risk Economy Director
6. Career Director + Story Acts + Rivals
7. Audio Identity Director

## Rule for future AI use
Future AI should usually extend this architecture with:
- adapters
- tooling
- content authoring helpers
- editor utilities
- diagnostics

Future AI should **not casually rewrite**:
- save truth
- selected car truth
- world handoff truth
- risk economy truth
- act progression truth
- race resolution truth

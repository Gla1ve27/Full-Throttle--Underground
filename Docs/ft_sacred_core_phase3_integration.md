# Full Throttle Sacred Core - Phase 3 Unity Integration

This phase wires the FT core into Unity scenes and assets. It assumes Phase 1 and Phase 2 scripts exist under `Assets/Scripts`.

An editor helper now exists at:

`Full Throttle > Sacred Core`

Use it to create the asset folders, runtime root, garage directors, and world directors. Still review every inspector assignment afterward because car/audio/rival assets must be deliberately chosen.

## 1. Persistent Runtime Scene

Create one persistent GameObject:

`FT_RuntimeRoot`

Add these components to it:

- `FTBootstrap`
- `FTRuntimeRoot`
- `FTServiceRegistry`
- `FTSaveGateway`
- `FTCarRegistry`
- `FTAudioProfileRegistry`
- `FTSelectedCarRuntime`
- `FTWorldTravelDirector`
- `FTCareerDirector`
- `FTRiskEconomyDirector`
- `FTHeatDirector`
- `FTWagerDirector`
- `FTRivalDirector`
- `FTProgressionDirector`
- `FTAudioIdentityDirector`
- `FTAudioRosterValidator`
- `FTSacredCoreHealthCheck`
- `FTGameContext`

Reference assignments:

- `FTBootstrap.saveGateway` -> the same `FTSaveGateway` component.
- `FTCarRegistry.cars` -> every player-usable car and every named rival signature car.
- `FTAudioProfileRegistry.profiles` -> every playable starter profile and named rival profile first.
- `FTAudioProfileRegistry.devEmergencyFallbackProfile` -> leave empty unless explicitly testing. Do not ship with this silently assigned.
- `FTCareerDirector.acts` -> all `FTStoryActDefinition` assets.
- `FTCareerDirector.rivals` -> all named `FTRivalDefinition` assets.
- `FTRivalDirector.rivals` -> all named `FTRivalDefinition` assets.
- `FTAudioRosterValidator.namedRivals` -> all named rival definitions.
- `FTWorldTravelDirector.dontDestroyOnLoad` -> enabled.
- `FTBootstrap.dontDestroyOnLoad` -> enabled.

The scripts now have execution order attributes so `FTBootstrap` registers the event bus and save gateway before dependent services wake up.

## 2. ScriptableObject Assets

Recommended folder layout:

- `Assets/ScriptableObjects/FullThrottle/Cars`
- `Assets/ScriptableObjects/FullThrottle/AudioProfiles`
- `Assets/ScriptableObjects/FullThrottle/Races`
- `Assets/ScriptableObjects/FullThrottle/Routes`
- `Assets/ScriptableObjects/FullThrottle/Rivals`
- `Assets/ScriptableObjects/FullThrottle/StoryActs`

Create car definitions through:

`Create > Full Throttle > Sacred Core > Car Definition`

Each `FTCarDefinition` must have:

- `carId`: stable id, never display text.
- `displayName`: player-facing car name.
- `vehicleClass`: starter, street, pro, halo, rival, etc.
- `driveType`: FWD, RWD, AWD.
- `engineCharacterTag`: rough starter, bright turbo, heavy V8, etc.
- `audioProfileId`: exact matching `FTVehicleAudioProfile.audioProfileId`.
- `garagePreviewRevStyle`: restrained, sharp, angry, prestige, etc.
- `forcedInductionType`: empty, turbo, supercharger, twin_turbo.
- `audioFamilyTag`: optional family tag for inheritance grouping.
- `starterOwned`: true only for the initial owned car or cars.
- `worldPrefab`: the drivable vehicle prefab.
- `feel`: starter values should be modest; progression cars should earn their power.

Create audio profiles through:

`Create > Full Throttle > Sacred Core > Vehicle Audio Profile`

Each player or named rival `FTVehicleAudioProfile` must have required clips assigned:

- `idle`
- `lowAccel`
- `midAccel`
- `highAccel`
- `topRedline`
- `lowDecel`
- `midDecel`
- `highDecel`
- `shiftUp`
- `shiftDown`
- `throttleLift`

Support clips should be assigned when available:

- `intake`
- `turboSpool`
- `turboBlowoff`
- `drivetrainWhine`
- `skidTire`

Per-car identity rules:

- Playable starter cars and named rival cars should have `dedicatedHeroProfile` enabled.
- Family inheritance is allowed only when `HasMeaningfulOverrides()` remains true.
- Do not use `devEmergencyFallback` for a real playable or rival car.
- Run `FTAudioRosterValidator` after assigning the starter and rival roster.

Create route/race/story assets through:

- `Create > Full Throttle > Sacred Core > Route Definition`
- `Create > Full Throttle > Sacred Core > Race Definition`
- `Create > Full Throttle > Sacred Core > Rival`
- `Create > Full Throttle > Sacred Core > Story Act`

## 3. Garage Scene

Create one scene GameObject:

`FT_GarageDirectors`

Add:

- `FTGarageDirector`
- `FTGarageShowroomDirector`
- `FTGarageCameraDirector`
- `FTGarageAudioPreviewDirector`

Create one showroom anchor:

`FT_ShowroomAnchor`

Recommended hierarchy:

```text
FT_GarageDirectors
FT_ShowroomAnchor
Garage Camera
```

Reference assignments:

- `FTGarageDirector.worldSceneName` -> the world scene name.
- `FTGarageDirector.defaultSpawnPointId` -> `garage_exit`.
- `FTGarageShowroomDirector.displayAnchor` -> `FT_ShowroomAnchor`.
- `FTGarageShowroomDirector.displayParent` -> an empty `FT_ShowroomCars` transform if you want cleaner hierarchy.
- `FTGarageCameraDirector.showroomAnchor` -> `FT_ShowroomAnchor`.
- `FTGarageAudioPreviewDirector.sourceAnchor` -> `FT_ShowroomAnchor` or a nearby exhaust/engine preview transform.

Expected garage behavior:

- Switching owned cars calls `FTSelectedCarRuntime`.
- The showroom prefab updates from the selected `FTCarDefinition`.
- The garage preview resolves the exact same `FTVehicleAudioProfile` that world driving uses.
- Continuing to world calls `FTWorldTravelDirector.QueueWorldEntry(currentCarId, "garage_exit")`.

## 4. World Scene

Create one scene GameObject:

`FT_WorldDirectors`

Add:

- `FTSpawnPointResolver`
- `FTVehicleSpawnDirector`
- `FTRaceDirector`
- `FTVehicleCameraDirector`

Create spawn points:

- `FTSpawnPoint` with `spawnPointId = player_start`
- `FTSpawnPoint` with `spawnPointId = garage_exit`
- Mark one spawn point `defaultForScene = true`

Reference assignments:

- `FTVehicleSpawnDirector.vehicleParent` -> optional `FT_PlayerVehicleRoot`.
- `FTVehicleCameraDirector.cameraTarget` -> the gameplay camera.
- `FTSpawnPointResolver.fallbackSpawnPointId` -> `player_start`.

Expected world behavior:

- `FTVehicleSpawnDirector` consumes the pending car id from `FTWorldTravelDirector`.
- If no pending id exists, it uses `FTSaveGateway.Profile.currentCarId`.
- The car id is validated by `FTCarRegistry`.
- The world prefab spawns at the resolved `FTSpawnPoint`.
- `FTPlayerVehicleBinder` applies the same `FTCarDefinition` to vehicle, audio, and definition-aware components.

## 5. Vehicle Prefab

Each player car prefab should have this root setup:

- `Rigidbody`
- `FTPlayerVehicleBinder`
- `FTVehiclePhysicsGuard`
- `FTDriverInput`
- `FTVehicleController`
- `FTVehicleTelemetry`
- `FTRespawnDirector`
- `FTVehicleAudioDirector`

Wheel setup:

- WheelCollider children are required.
- If `FTVehicleController.wheels` is empty, it auto-discovers WheelColliders.
- Name rear wheels with `rear`, `back`, `rl`, or `rr` so auto-detection can mark rear wheels.
- Assign visual wheel transforms in `FTWheelState.visual` for polished rotation.

Binder setup:

- `FTPlayerVehicleBinder.rigidBody` -> prefab root Rigidbody.
- `FTPlayerVehicleBinder.wheelColliders` -> all wheel colliders.
- `FTPlayerVehicleBinder.definitionReceivers` can be empty because it now auto-finds `IFTVehicleDefinitionReceiver` components.

Physics guard setup:

- `FTVehiclePhysicsGuard.wheelColliders` -> all wheel colliders, or leave empty for auto-discovery.
- Keep `zeroVelocityOnEnable` enabled.
- Use this to stop launch-at-spawn issues from bad wheel radius or stale velocity.

Audio setup:

- `FTVehicleAudioDirector.telemetry` -> same prefab `FTVehicleTelemetry`, or leave empty for lookup.
- `FTVehicleAudioDirector.audioRoot` -> optional child transform named `FTVehicleAudio`.
- Enable `logLoopState` while tuning, then disable for normal play.

## 6. HUD Scene Objects

On the HUD Canvas, add:

`FT_HUD`

Components:

- `FTHUDDirector`

Optional child display components:

- `FTSpeedDisplay`
- `FTHeatDisplay`
- `FTRaceStatusDisplay`
- `FTMinimapDirector`

Reference assignments:

- `FTSpeedDisplay.speedText` -> speed text.
- `FTSpeedDisplay.gearText` -> gear text.
- `FTHeatDisplay.heatText` -> heat text.
- `FTRaceStatusDisplay.statusText` -> race status text.
- `FTMinimapDirector.playerMarker` -> minimap player marker RectTransform.

`FTHUDDirector` will bind to the active spawned `FTVehicleTelemetry`.

## 7. Required Startup Log Signals

Healthy startup should produce logs similar to:

- `[SacredCore] Bootstrap initialized runtime service graph.`
- `[SacredCore] Runtime root ready.`
- `[SacredCore] Audio profile registry rebuilt.`
- `[SacredCore] Progression director online.`
- `[SacredCore] Context selected car=...`
- `[SacredCore] Showroom displaying ...`
- `[SacredCore] Garage audio preview car=..., profile=...`
- `[SacredCore] Spawn resolver matched ...`
- `[SacredCore] Spawned '...' at '...'`
- `[SacredCore] Vehicle binder applied definition ...`
- `[SacredCore] Vehicle audio loaded: chosenCar=..., chosenProfile=...`

Any missing selected car, missing audio profile, missing prefab, or emergency fallback should be treated as a setup error.

## 8. Phase 3 Integration Order

Use this order:

1. Add `FT_RuntimeRoot` to the bootstrap or first loaded scene.
2. Create starter `FTCarDefinition` assets.
3. Create starter `FTVehicleAudioProfile` assets.
4. Assign car registry and audio registry lists.
5. Add `FTAudioRosterValidator` named rival list.
6. Prepare one garage scene and verify showroom selection.
7. Prepare one world scene and verify deterministic spawn.
8. Add FT vehicle prefab components to one starter car.
9. Verify garage preview and world runtime resolve the same audio profile id.
10. Only then add named rival cars and their audio profiles.

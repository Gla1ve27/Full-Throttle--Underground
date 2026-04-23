# Full Throttle Sacred Core - Phase 1 Architecture

## System Map

The new FT-prefixed core is organized around one runtime spine:

- Runtime truth: `FTBootstrap`, `FTRuntimeRoot`, `FTGameContext`, `FTEventBus`, `FTServiceRegistry`
- Save and progression truth: `FTSaveGateway`, `FTProfileData`, `FTProgressionDirector`, `FTCareerDirector`, `FTRiskEconomyDirector`
- Car and garage truth: `FTCarDefinition`, `FTCarRegistry`, `FTSelectedCarRuntime`, `FTGarageDirector`, `FTGarageShowroomDirector`
- World and spawn truth: `FTWorldTravelDirector`, `FTVehicleSpawnDirector`, `FTSpawnPointResolver`, `FTPlayerVehicleBinder`, `FTVehiclePhysicsGuard`
- Driving feel core: `FTVehicleController`, `FTDriverInput`, `FTPowertrain`, `FTGearbox`, `FTWheelState`, `FTGripModel`, `FTDriftModel`, `FTBrakeModel`, `FTSteeringModel`, `FTVehicleTelemetry`, `FTRespawnDirector`
- Per-car audio identity: `FTVehicleAudioProfile`, `FTAudioProfileRegistry`, `FTVehicleAudioDirector`, `FTEngineAudioFeed`, `FTEngineLoopMixer`, `FTShiftAudioDirector`, `FTSurfaceAudioDirector`, `FTTurboAudioDirector`, `FTGarageAudioPreviewDirector`
- Camera core: `FTVehicleCameraDirector`, `FTGarageCameraDirector`, `FTHighSpeedCameraMode`, `FTDriftCameraMode`
- Race, route, rival, pressure: `FTRouteDefinition`, `FTRaceDefinition`, `FTRaceDirector`, `FTRivalDefinition`, `FTRivalDirector`, `FTHeatDirector`, `FTWagerDirector`
- HUD feedback: `FTHUDDirector`, `FTSpeedDisplay`, `FTHeatDisplay`, `FTRaceStatusDisplay`, `FTMinimapDirector`

## Ownership Map

- `FTSaveGateway` owns persisted player truth: selected car, owned cars, money, reputation, heat, unlocked districts, beaten rivals, and active session risk.
- `FTSelectedCarRuntime` owns the current runtime selected car id. Garage, world, audio, and HUD read this instead of keeping their own separate selection.
- `FTCarRegistry` owns valid car definitions and fallback validation. It is the only place that converts a car id into an `FTCarDefinition`.
- `FTAudioProfileRegistry` owns profile resolution. Every playable or named rival car must resolve through its `audioProfileId`.
- `FTVehicleSpawnDirector` owns world vehicle instantiation. It consumes `FTWorldTravelDirector` handoff data, validates against `FTCarRegistry`, and binds the spawned prefab.
- `FTPlayerVehicleBinder` is the bridge between car definition and vehicle prefab components. Definition-aware components implement `IFTVehicleDefinitionReceiver`.
- `FTVehicleController` owns runtime wheel forces and drive feel. It produces `FTVehicleTelemetry` for audio, camera, HUD, and race systems.
- `FTRiskEconomyDirector`, `FTHeatDirector`, and `FTWagerDirector` own consequence rules so losses are memorable but not arbitrary.

## Scene Flow

1. Bootstrap scene or first loaded scene contains `FTBootstrap` or `FTRuntimeRoot`.
2. Runtime services register in deterministic order.
3. `FTSaveGateway` loads or creates `FTProfileData`.
4. `FTCarRegistry` validates car definitions.
5. `FTSelectedCarRuntime` syncs `Profile.currentCarId` into runtime truth.
6. Garage scene shows the selected car through `FTGarageShowroomDirector`.
7. World scene spawns the selected car through `FTVehicleSpawnDirector`.

## Save Flow

1. `FTSaveGateway.LoadOrCreate()` loads the profile.
2. `FTProfileData.EnsureDefaults()` chooses the starter car if the profile has no valid car.
3. Car selection, money, rep, heat, rival wins, and race session data are written back through `FTSaveGateway.Save()`.
4. Runtime-only systems never invent persistent truth. They request updates through directors.

## Garage-To-World Flow

1. Player browses owned cars in `FTGarageDirector`.
2. `FTSelectedCarRuntime.TrySetCurrentCar()` validates ownership, updates the profile, and raises selection events.
3. `FTGarageShowroomDirector` rebuilds the showroom car from the same selected `FTCarDefinition`.
4. `FTGarageAudioPreviewDirector` resolves the exact same `FTVehicleAudioProfile`.
5. On exit, `FTGarageDirector.ContinueToWorld()` queues `carId` and spawn id in `FTWorldTravelDirector`.
6. World scene `FTVehicleSpawnDirector` consumes that handoff and spawns the exact selected car.

## Vehicle Initialization Flow

1. `FTVehicleSpawnDirector` resolves the spawn point with `FTSpawnPointResolver`.
2. The selected car prefab is instantiated at that pose.
3. `FTPlayerVehicleBinder` receives the `FTCarDefinition`.
4. Definition-aware systems apply feel and audio identity.
5. `FTVehiclePhysicsGuard` clamps dangerous collider settings, clears velocity, and logs guard actions.
6. The vehicle is briefly frozen, snapped near ground, then released stable.

## Audio Flow

1. `FTVehicleAudioDirector` receives the active `FTCarDefinition`.
2. It resolves the exact `FTVehicleAudioProfile` through `FTAudioProfileRegistry`.
3. The profile is validated. Missing required driving layers produce explicit warnings.
4. `FTEngineAudioFeed` reads telemetry and creates smoothed RPM, throttle, speed, slip, shift, and limiter state.
5. `FTEngineLoopMixer` keeps the engine bed alive with idle, accel, decel, and top/redline loops.
6. `FTShiftAudioDirector`, `FTTurboAudioDirector`, and `FTSurfaceAudioDirector` add transients and support layers without replacing the engine bed.
7. Garage preview uses the same car/profile resolution path as world runtime.

## Race / Career Flow

1. `FTRaceDirector` begins a race only when entry and profile state allow it.
2. `FTWagerDirector` reserves wager exposure for risky events.
3. Race resolution flows into `FTRiskEconomyDirector`, `FTHeatDirector`, and `FTCareerDirector`.
4. Money, repair debt, heat, reputation, and rival wins are persisted.
5. `FTProgressionDirector` checks unlocks and keeps the career climb structured.

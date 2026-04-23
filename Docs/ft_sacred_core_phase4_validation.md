# Full Throttle Sacred Core - Phase 4 Validation

This phase proves the new FT core is healthy before migration continues. Keep `FTSacredCoreHealthCheck` on `FT_RuntimeRoot` during integration.

## 1. Runtime Health Check

Component:

`FTSacredCoreHealthCheck`

Recommended location:

`FT_RuntimeRoot`

Recommended settings while integrating:

- `runOnStart`: enabled
- `repeatIntervalSeconds`: `0` for manual validation, `5` while debugging scene transitions
- `validateSceneSpawnPoints`: enabled in garage/world scenes
- `validateActiveVehicle`: enabled in world scenes, optional in garage scenes
- `logPassedChecks`: enabled only while setting up

Manual run:

Use the component context menu:

`Run Sacred Core Validation`

Healthy result:

```text
[SacredCore][Health] PASS. warnings=0
```

Warnings are acceptable only when the current scene intentionally lacks that system. Example: no active vehicle in a pure bootstrap scene.

Errors are never acceptable for gameplay scenes.

## 2. Bootstrap Validation

Scene:

- Bootstrap scene
- Garage scene if it can be first-loaded
- World scene if it can be first-loaded

Test:

1. Press Play.
2. Watch startup logs.
3. Run `FTSacredCoreHealthCheck`.

Expected logs:

```text
[SacredCore] Bootstrap initialized runtime service graph.
[SacredCore] Runtime root ready.
[SacredCore] Service registry online.
[SacredCore] Audio profile registry rebuilt. profiles=...
[SacredCore] Progression director online.
[SacredCore][Health] PASS.
```

Healthy signals:

- `FTSaveGateway.Profile` exists.
- `FTCarRegistry` has at least one car.
- `FTSelectedCarRuntime.CurrentCarId` is not empty.
- Runtime selected car and profile selected car match.
- No emergency audio fallback is used.

Failure meanings:

- Missing `FTSaveGateway`: runtime root is incomplete or bootstrap did not wake first.
- Empty selected car id: no starter car or save default exists.
- Missing `FTCarRegistry`: car truth is not registered.
- Missing `FTAudioProfileRegistry`: audio identity cannot be enforced.

## 3. Car / Garage / World Truth Validation

Test:

1. Enter Garage.
2. Select each owned starter car.
3. Confirm showroom model changes.
4. Continue to World.
5. Confirm spawned car is the same car.

Expected logs:

```text
[SacredCore] Showroom displaying starter_...
[SacredCore] Garage audio preview car=starter_..., profile=...
[SacredCore] Spawn resolver matched 'garage_exit' ...
[SacredCore] Spawned 'starter_...' at 'garage_exit'.
[SacredCore] Vehicle binder applied definition starter_...
```

Healthy signals:

- `FTSelectedCarRuntime.CurrentCarId`
- `FTSaveGateway.Profile.currentCarId`
- `FTGarageShowroomDirector` displayed car
- `FTVehicleSpawnDirector.ActiveVehicle.Definition.carId`

all point to the same `carId`.

Failure meanings:

- Showroom differs from world spawn: duplicate or legacy selection path is still controlling a scene.
- Spawned prefab is wrong: `FTCarDefinition.worldPrefab` is wrong or stale.
- Placeholder appears: `FTCarRegistry.ValidateOrFallback()` had to recover from an invalid car id.

## 4. Spawn Stability Validation

World scene requirements:

- At least one `FTSpawnPoint`
- One `FTSpawnPoint.defaultForScene = true`
- `garage_exit` spawn point for garage exit flow
- `player_start` spawn point for direct world load

Test:

1. Load world directly.
2. Load world from garage.
3. Respawn with `R`.
4. Repeat after changing selected car.

Expected logs:

```text
[SacredCore] Spawn resolver matched 'player_start' ...
[SacredCore] Spawn resolver matched 'garage_exit' ...
[SacredCore] Vehicle respawned at ...
```

Healthy signals:

- Vehicle lands near ground.
- Rigidbody velocity is reset at spawn.
- No sky launch.
- No stale rotation from showroom.
- `FTVehiclePhysicsGuard` reports no wheel-radius clamps after final setup.

Failure meanings:

- No default spawn point: direct world load can fall to origin.
- Duplicate spawn id: world entry may resolve to the wrong transform.
- Wheel radius clamp warning: prefab WheelCollider setup needs correction.

## 5. Vehicle Control Validation

Test:

1. WASD drive.
2. Brake from speed.
3. Hold reverse after stopping.
4. Handbrake at 40+ kph.
5. Overdrive a corner.
6. Press `R` to respawn.

Expected behavior:

- `W` accelerates.
- `S` brakes, then reverses when slow.
- `A/D` steer.
- Space applies rear-biased handbrake behavior.
- Drift is possible but not self-driving.
- `FTVehicleTelemetry.SpeedKph`, `EngineRPM`, `Gear`, `Slip01`, and `Grounded` update.

Healthy signals:

- No NaN speed/RPM.
- Gear changes occur.
- Slip rises during handbrake/drift only.
- Vehicle remains controllable after respawn.

Failure meanings:

- No motion: wheel motor flags, WheelColliders, Rigidbody, or input are not wired.
- No telemetry: `FTVehicleTelemetry` missing or controller is not updating it.
- Constant skid: wheel friction/drift bias too loose or skid slip source is wrong.

## 6. Per-Car Audio Identity Validation

Required component:

`FTAudioRosterValidator`

Test:

1. Assign all starter cars to `FTCarRegistry`.
2. Assign their audio profiles to `FTAudioProfileRegistry`.
3. Assign named rivals to `FTAudioRosterValidator.namedRivals`.
4. Run `Validate Playable And Rival Audio`.
5. Switch cars in Garage and enter World.

Expected logs:

```text
[SacredCore] Audio identity accepted for car=...
[SacredCore] Garage audio preview car=..., profile=...
[SacredCore] Vehicle audio loaded: chosenCar=..., chosenProfile=...
```

Healthy signals:

- Every starter car resolves one exact `FTVehicleAudioProfile`.
- Every named rival signature car resolves one exact `FTVehicleAudioProfile`.
- `idle`, `lowAccel`, `midAccel`, `highAccel`, `topRedline`, `lowDecel`, `midDecel`, `highDecel`, `shiftUp`, `shiftDown`, and `throttleLift` are assigned.
- `devEmergencyFallback` is never used for playable/rival cars.
- Garage profile id and world profile id match.

Failure meanings:

- Missing required clip: profile is not playable-ready.
- Fallback profile used: car has no real identity yet.
- Inherited profile rejected: not enough unique clip overrides.
- Garage/world profile mismatch: duplicate audio path still exists.

## 7. Engine Audio Runtime Validation

On the active vehicle, temporarily enable:

`FTVehicleAudioDirector.logLoopState`

Test:

1. Idle for 5 seconds.
2. Launch gently.
3. Full-throttle 2nd gear pull.
4. High-rpm pull.
5. Shift near redline.
6. Lift throttle.
7. Handbrake skid.

Expected logs:

```text
[SacredCore] Audio gear=... rawRPM=... audioRPM=... throttle=... shift=... bed=... loops=...
```

Healthy signals:

- Engine bed volume does not collapse to zero while driving.
- Top/redline loop appears only near redline.
- Top loop suppresses when shifting.
- Shift transients play without killing the engine bed.
- Skid layer appears only with speed and slip.

Failure meanings:

- `bed=0.00` under load: missing loops or bad RPM bands.
- Top loop active during shift: profile or mixer gate needs tuning.
- Constant skid: surface gate or telemetry slip is wrong.
- Robotic pull: pitch clamp is overexposed or RPM smoothing is too fast.

## 8. Save / Economy / Career Validation

Test:

1. Start with fresh profile.
2. Confirm starter car is owned.
3. Enter a race with entry fee.
4. Win once.
5. Lose once with crash severity.
6. Bank the session.
7. Quit and relaunch.

Expected logs:

```text
[SacredCore] Race started: ...
[SacredCore] Race resolved: ...
[SacredCore] Progression checked. act=..., rep=..., heat=...
[SacredCore] Heat=... reason=...
```

Healthy signals:

- Money decreases by entry fee.
- Rewards are session-based before banking.
- Repair debt and wager exposure affect net money.
- Reputation persists.
- Current act can advance only when requirements are met.
- Selected car survives relaunch.

Failure meanings:

- Race live but profile not live: race session state is split.
- Profile live but director not live: scene changed without cleanup or resume handling.
- Money changes without signal: UI and save can drift.

## 9. HUD Validation

Test:

1. Spawn world vehicle.
2. Drive.
3. Start and resolve a race.
4. Change heat.

Expected behavior:

- Speed text updates.
- Gear text updates.
- Heat text updates after `FTHeatChangedSignal`.
- Race status appears while race is live.
- Minimap marker follows active vehicle.

Healthy signals:

- `FTHUDDirector` logs telemetry bind once.
- HUD does not search every frame after binding unless vehicle changes.

Failure meanings:

- Speed stuck at zero: no active `FTVehicleTelemetry`.
- Heat not updating: missing `FTHeatDisplay` or missing event bus.
- Race status stale: race director/profile state mismatch.

## 10. Validation Stop Rules

Do not continue migration if any of these are true:

- Selected garage car and spawned world car differ.
- Any playable starter or named rival car uses fallback audio.
- A required active driving audio layer has `clip = None`.
- Vehicle launches into the sky after spawn.
- Save profile selected car changes without `FTSelectedCarRuntime`.
- Race session state splits between profile and director.
- `FTSacredCoreHealthCheck` reports errors in Garage or World.

## 11. Phase 4 Pass Criteria

Phase 4 is considered passed when:

- Runtime root health passes in bootstrap scene.
- Garage scene health passes with no errors.
- World scene health passes with no errors.
- One starter car can be selected, previewed, spawned, driven, respawned, and heard.
- Garage and world use the same `carId` and same `audioProfileId`.
- One race can start and resolve with money, heat, reputation, and session state changing coherently.

# Full Throttle Sacred Core - Phase 5 Migration Notes

Phase 5 moves existing content onto the new FT-prefixed core without letting the old runtime keep ownership. The old project is now a reference source for assets, scene layout, UI hookups, prefabs, and data. It is not the authority for selected car truth, spawn truth, garage truth, audio identity, race state, or save state.

Use the older `full_throttle_legacy_elimination_migration_documentation.md` only as background context. This Phase 5 document is the current migration contract for the sacred core.

## 1. Migration Policy

The FT core owns the future runtime.

Rules:

- Do not run old and FT owners for the same responsibility in a final migrated scene.
- Do not let placeholder car selection override `FTSelectedCarRuntime`.
- Do not let old garage/world bridge state override `FTWorldTravelDirector`.
- Do not let generic audio banks override `FTVehicleAudioProfile`.
- Do not delete legacy scripts immediately. Quarantine them by removing or disabling them from migrated scenes and prefabs first.
- Keep old assets, meshes, clips, UI prefabs, and route geometry unless they are broken content.

Temporary compatibility is allowed only when it is explicit, logged, and isolated. A temporary adapter must never silently become the new truth.

## 2. Migration Order

Follow this order so the game never has two live truths fighting each other:

1. Create FT ScriptableObject data.
2. Create the persistent `FT_RuntimeRoot`.
3. Migrate car selection and garage display.
4. Migrate world travel and spawn.
5. Migrate one player vehicle prefab to the FT vehicle stack.
6. Migrate the selected car's audio profile.
7. Migrate races, routes, rivals, heat, and wager data.
8. Migrate HUD displays.
9. Run Phase 4 validation and the Phase 5 legacy auditor.
10. Remove legacy owners from migrated scenes and prefabs.

Do not migrate every car first. Prove the whole loop with the starter car, one upgraded player car, and one named rival car. Then expand.

## 3. Legacy To FT Ownership Map

| Legacy system | New FT owner | Migration action |
| --- | --- | --- |
| `PersistentProgressManager` | `FTSaveGateway` + `FTProfileData` | Copy money, rep, heat, owned cars, selected car, completed races into the FT profile shape. Remove old progress manager from migrated runtime scenes. |
| `PlayerCarCatalog` | `FTCarRegistry` + `FTCarDefinition` | Convert each player/rival vehicle entry into an `FTCarDefinition`. Assign stable `carId`, `worldPrefab`, and `audioProfileId`. |
| `VehicleDefinitionCatalog` | `FTCarRegistry` + `FTCarDefinition` | Migrate useful stat and prefab references into FT car assets. Do not keep duplicate runtime lookup in migrated scenes. |
| `VehicleSceneSelectionBridge` | `FTWorldTravelDirector` + `FTSelectedCarRuntime` | Stop using pending static car ids for new flow. Garage exits through `FTWorldTravelDirector.RequestTravelToWorld`. |
| `GarageManager` | `FTGarageDirector` | Replace purchase/select/exit calls with FT garage APIs. Old garage manager should not exist in migrated garage scenes. |
| `GarageShowroomController` | `FTGarageShowroomDirector` | Move showroom anchors and display parent references to FT showroom. Preview car comes from `FTSelectedCarRuntime`. |
| `VehicleOwnershipSystem` | `FTProfileData.ownedCarIds` + `FTGarageDirector` | Ownership is save data, not a separate scene-local truth. Purchase paths should mutate `FTProfileData` through FT systems. |
| `VehicleSetupBridge` | `FTVehicleSpawnDirector` + `FTPlayerVehicleBinder` | Spawn and bind through FT. Do not let old setup bridge consume pending car ids after migration. |
| `CarRespawn` | `FTRespawnDirector` | Use FT respawn so velocity reset, upright pose, and physics guard behavior stay consistent. |
| `VehicleInput` / `VehicleInputAdapter` | `FTDriverInput` | Map keyboard/controller input into the FT input component. Keep old input only for unmigrated prefabs. |
| `VehicleDynamicsController` / V2 vehicle stack | `FTVehicleController` + `FTPowertrain` + `FTGearbox` + handling models | Migrate one prefab at a time. Remove old physics owners once the FT controller is driving. |
| `VehicleAudioControllerV2` | `FTVehicleAudioDirector` | Runtime profile is selected from `FTCarDefinition.audioProfileId`. No generic bank fallback for playable cars. |
| `NFSU2CarAudioBank` | `FTVehicleAudioProfile` | Move clips and tuning into dedicated or meaningfully overridden FT audio profiles. |
| `VehicleAudioTierSelector` | `FTAudioIdentityDirector` + `FTAudioProfileRegistry` | Audio identity follows the selected car, not a separate tier selector. |
| `RaceManager` | `FTRaceDirector` | Use FT race sessions and outcome events. Old race manager should not own state in migrated world scenes. |
| `RaceStartTrigger` / `RaceFinishTrigger` | FT race triggers calling `FTRaceDirector` | Keep collider placement if useful, but replace behavior with FT session calls. |
| `Speedometer` | `FTSpeedDisplay` | Bind to `FTVehicleTelemetry` through `FTHUDDirector`. |
| old heat/race HUD scripts | `FTHeatDisplay` + `FTRaceStatusDisplay` | HUD reads FT state/events only. |

## 4. Data Migration

### Cars

For every playable starter car and named rival car:

- Create an `FTCarDefinition`.
- Set `carId` to a stable lowercase id, for example `gla1ve_starter_coupe`.
- Set `displayName` to the player-facing name.
- Set `vehicleClass`, `driveType`, `engineCharacterTag`, `garagePreviewRevStyle`, and optional forced-induction fields.
- Assign the migrated drivable prefab to `worldPrefab`.
- Assign the matching `audioProfileId`.
- Add the asset to `FTCarRegistry.cars`.

Playable and named rival cars must not rely on an emergency fallback audio profile.

### Audio

For every playable starter car and named rival car:

- Create a dedicated `FTVehicleAudioProfile`, or a family-derived profile with meaningful overrides.
- Fill all required driving loops: idle, low/mid/high accel, top, low/mid/high decel.
- Fill shift up, shift down, lift, induction, turbo, drivetrain, and surface support where appropriate.
- Run `FTAudioRosterValidator` after assignment.
- Keep Car_27 identity profiles separate from other hero/rival identities.

Family inheritance is only a workload tool. The final resolved profile must still be audibly distinct.

### Saves

Map old save values into `FTProfileData`:

- selected car -> `selectedCarId`
- owned cars -> `ownedCarIds`
- money -> `money`
- reputation -> `reputation`
- heat -> `heat`
- completed races -> `completedRaceIds`
- unlocked districts or acts -> FT career fields

After migration, the selected car should be changed only through `FTSelectedCarRuntime` / `FTGarageDirector`.

### Races And Rivals

Create:

- `FTRouteDefinition` for each route.
- `FTRaceDefinition` for each race.
- `FTRivalDefinition` for each named rival and signature car.
- `FTStoryActDefinition` for career arcs.

Add them to the runtime root directors. Keep old route meshes, checkpoints, and trigger positions as placement reference.

## 5. Scene Migration

### Bootstrap Scene

The scene should contain only one persistent FT runtime root:

- `FTBootstrap`
- `FTRuntimeRoot`
- `FTServiceRegistry`
- `FTSaveGateway`
- `FTCarRegistry`
- `FTAudioProfileRegistry`
- `FTSelectedCarRuntime`
- `FTWorldTravelDirector`
- progression, economy, race pressure, audio identity, and validation components

Remove old persistent progress managers and static bridge objects from migrated startup scenes.

### Garage Scene

Use:

- `FTGarageDirector`
- `FTGarageShowroomDirector`
- `FTGarageCameraDirector`
- `FTGarageAudioPreviewDirector`

The showroom car must come from `FTSelectedCarRuntime.CurrentCar`. It must not come from `GarageManager`, `GarageShowroomController`, or a static pending id.

When exiting the garage, call `FTGarageDirector.EnterWorld()` or route the button to the FT world travel call.

### World Scene

Use:

- `FTSpawnPointResolver`
- `FTVehicleSpawnDirector`
- `FTPlayerVehicleBinder`
- `FTVehiclePhysicsGuard`
- `FTVehicleCameraDirector`
- `FTRaceDirector`
- `FTHUDDirector`

Place at least one `FTSpawnPoint` and mark the default. Spawn is valid only when the selected `FTCarDefinition.worldPrefab` is instantiated, grounded, velocity-reset, bound, and validated.

## 6. Vehicle Prefab Migration

Migrate one prefab at a time.

Required FT components on the new player vehicle prefab:

- `Rigidbody`
- wheel colliders or wheel transforms as needed by the FT vehicle controller
- `FTDriverInput`
- `FTVehicleController`
- `FTPowertrain`
- `FTGearbox`
- `FTGripModel`
- `FTDriftModel`
- `FTBrakeModel`
- `FTSteeringModel`
- `FTVehicleTelemetry`
- `FTVehiclePhysicsGuard`
- `FTVehicleAudioDirector`
- `FTEngineAudioFeed`
- `FTEngineLoopMixer`
- `FTShiftAudioDirector`
- `FTSurfaceAudioDirector`
- `FTTurboAudioDirector` when applicable

Remove or disable legacy movement and audio owners after the FT controller is responsible for the prefab. Do not leave two vehicle controllers or two engine audio directors active.

## 7. Quarantine List

Do not delete these immediately, but remove them from migrated gameplay scenes and migrated player/rival prefabs:

- `GarageManager`
- `GarageShowroomController`
- `GarageUIController` until rewritten to call FT garage APIs
- `PersistentProgressManager`
- `VehicleOwnershipSystem`
- `VehicleSceneSelectionBridge`
- `VehicleSetupBridge`
- `VehicleInput`
- `VehicleInputAdapter`
- `VehicleDynamicsController` or V2 vehicle controllers after FT driving is live
- `VehicleAudioControllerV2`
- `VehicleAudioTierSelector`
- `RaceManager`
- `RaceStartTrigger`
- `RaceFinishTrigger`
- `CarRespawn`
- `Speedometer`

Assets they reference may still be useful. Runtime ownership is what gets quarantined.

## 8. Phase 5 Auditor

An editor auditor now exists at:

`Full Throttle > Sacred Core > Audit Legacy Migration In Loaded Scenes`

and:

`Full Throttle > Sacred Core > Audit Legacy Migration In Selected Objects`

Use it after every scene/prefab migration pass. It reports old owner components, missing scripts, and the FT replacement responsibility. A migrated garage or world scene should show:

```text
[SacredCore][Migration] PASS. No legacy owner components found.
```

Warnings mean the scene is not clean yet. They are not automatically fatal for old scenes, but they are blockers for scenes you claim are migrated.

## 9. Acceptance For Phase 5

Phase 5 is complete for a scene when:

- `FTSacredCoreHealthCheck` has no errors.
- The Phase 5 auditor finds no legacy owner components.
- The garage selected car, showroom car, world spawned car, and audio profile all share one `carId`.
- The world car spawns from `FTVehicleSpawnDirector`, not old setup bridges.
- No emergency audio fallback is used for playable or named rival cars.
- No duplicate progress manager or duplicate selected-car source exists.
- Race start/finish state is owned by `FTRaceDirector`.

Phase 5 is complete for a vehicle prefab when:

- It has exactly one active driving controller stack.
- It has exactly one active vehicle audio identity stack.
- Its audio profile validates with no missing required clips.
- Its telemetry feeds camera, HUD, audio, and race systems from the same FT source.
- Respawn uses `FTRespawnDirector`.

## 10. Safe Rollout

Recommended rollout:

1. Duplicate the garage scene and migrate the duplicate.
2. Duplicate one world test scene and migrate the duplicate.
3. Migrate one starter car prefab.
4. Migrate one named rival car prefab.
5. Validate garage -> world -> race -> garage.
6. Only then migrate the rest of the starter/rival roster.

Keep legacy scenes playable until the FT loop proves itself. Once a scene passes Phase 4 validation and Phase 5 migration audit, the FT scene becomes the authority.

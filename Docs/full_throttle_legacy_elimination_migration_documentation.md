# Full Throttle Legacy Elimination and New-System Migration Documentation

## Purpose

This document is the full migration plan to remove the legacy player-car stack and move Full Throttle onto the new modular vehicle and audio architecture.

It is based on:
- the current legacy runtime behavior
- the modular rework blueprint already defined earlier
- the practical failures found during live migration attempts in the current project

---

## 1. Current state of the project

### 1.1 Legacy vehicle stack that currently works
The current gameplay-ready stack is still centered on:
- `VehicleDynamicsController`
- `GearboxSystem`
- `VehicleAudioController` / `CarEngineAudio`
- `InputReader`

`VehicleDynamicsController` is still the main owner of vehicle physics, telemetry, steering, drive, braking, assists, drift state, aero, and wheel sync. It also initializes itself in `Awake()` and immediately calls `Initialize(baseStats, startupUpgrades)`.  
`GearboxSystem` still computes RPM largely from wheel RPM and drive ratio.  
`CarEngineAudio` still resolves references to the legacy gearbox, legacy vehicle controller, and `InputReader`, and then drives its own loop/aux/one-shot runtime.  

### 1.2 Why legacy must be removed
The legacy stack works, but it is too monolithic:
- movement and handling are tightly packed into one large controller
- audio is tightly packed into one large runtime
- startup ownership is ambiguous when old and new systems coexist
- the old stack auto-initializes too aggressively for clean migration

### 1.3 Why the first migration attempts failed
The failed live migration exposed a startup ownership problem:
- garage selection, save data, prefab defaults, appearance application, bridge init, and V2 init were all able to influence the player car
- the placeholder prefab could win startup instead of the saved selected car
- legacy and V2 systems could both become active
- V2 was not yet mature enough to replace movement and audio at the same time

---

## 2. Target architecture

The intended replacement architecture is modular.

### 2.1 Runtime top level
- `VehicleControllerV2`
- `VehicleState`
- `VehicleTelemetry`
- `VehicleInputAdapter`

### 2.2 Driving and physics
- `VehicleChassisController`
- `VehicleSteeringSystem`
- `VehicleBrakeSystem`
- `VehicleGripSystem`
- `VehicleDriftSystem`
- `VehicleAssistSystem`
- `VehicleAeroSystem`
- `VehicleWallResponseSystem`
- `WheelVisualSynchronizer`

### 2.3 Powertrain
- `PowertrainSystem`
- `GearboxSystemV2`
- `EngineRPMModel`
- `TorqueDistributor`

### 2.4 Audio
- `VehicleAudioControllerV2`
- `EngineAudioStateFeed`
- `EngineLayerPlayer`
- `EngineTransientPlayer`
- `VehicleAuxAudioPlayer`
- `VehicleSurfaceAudioPlayer`
- `VehicleAudioMixerRouter`

### 2.5 Data
- `VehicleHandlingProfile`
- `VehiclePowertrainProfile`
- `VehicleAudioProfile`
- `EngineLayerDefinition`
- `EngineTransientDefinition`

### 2.6 Core design rule
Vehicle physics computes state. Audio only reads state.

That means:
- audio never owns physics logic
- startup never allows two active driving owners
- the player car identity comes from progress/save, not from a prefab default

---

## 3. Migration principles

### 3.1 One authoritative selected-car source
The current car must come from `PersistentProgressManager.CurrentOwnedCarId`.

That selected car ID must drive:
- garage display state
- world appearance state
- stats selection
- player-car initialization

### 3.2 One authoritative runtime initializer
The player car must be initialized by one system only.

Target owner:
- `VehicleSetupBridge`

Not allowed:
- `VehicleControllerV2` self-selecting a car identity
- legacy controller auto-owning the same player car while V2 is active
- garage UI visuals becoming the selected car without saving the ID

### 3.3 Neutral player-car prefab
The world `PlayerCar.prefab` must become a neutral shell.

It should hold:
- rigidbody
- wheel colliders
- root transforms
- appearance sockets
- audio root
- controller scripts

It must not hard-own:
- the final selected car identity
- a baked-in active stats asset that overrides runtime selection
- a second active controller stack

---

## 4. Full migration phases

## Phase 0 — Freeze and baseline
Goal: stop breakage while migration continues.

Actions:
- keep legacy movement live for the player car until V2 reaches feature parity
- ensure garage-to-world selected car persistence is correct
- stop mixed ownership between legacy and V2

Exit criteria:
- selected car in garage always matches selected car in world
- player car moves reliably
- no launch-into-air spawn failures
- no duplicate controller ownership

---

## Phase 1 — Startup and ownership cleanup
Goal: remove ambiguous initialization.

### Work items
1. Make `PersistentProgressManager.CurrentOwnedCarId` the only selected-car authority.
2. Ensure garage exit always writes the actively shown car to save.
3. Ensure world bootstrap always reads the saved current car ID.
4. Ensure `PlayerCarAppearanceController` applies visuals only.
5. Ensure `VehicleSetupBridge` initializes the player car.
6. Remove all self-initializing car-identity logic from `VehicleControllerV2`.
7. Disable legacy controller when V2 becomes active.
8. Prevent both old and new audio owners from running together.

### Required result
Startup order becomes:
1. load save
2. read selected car ID
3. apply appearance
4. resolve stats
5. initialize active controller
6. bind audio
7. gameplay begins

Exit criteria:
- no placeholder prefab identity wins startup
- no wrong-car world spawn
- runtime inspector always shows matching selected ID / appearance / stats

---

## Phase 2 — Vehicle state backbone
Goal: create the central read-only vehicle state.

### Build
- `VehicleState`
- `DriverCommand`
- `VehicleInputAdapter`
- `VehicleTelemetry`

### What `VehicleState` must own
- speed
- forward speed
- engine RPM
- normalized RPM
- wheel-driven RPM
- free-rev RPM
- engine load
- throttle / brake / steer
- clutch
- shift progress
- gear
- grounded state
- reversing state
- sliding state
- slip angle
- front/rear slip
- average driven wheel RPM
- limiter amount
- turbo spool amount

### Why this matters
This is the seam that allows:
- physics to stay modular
- audio to stop poking directly into wheel and controller logic
- UI/VFX to read a clean runtime state

Exit criteria:
- all new driving systems read/write `VehicleState`
- audio does not read the old monolithic controller directly

---

## Phase 3 — Powertrain replacement
Goal: replace the legacy gearbox + RPM model.

### Build
- `EngineRPMModel`
- `GearboxSystemV2`
- `PowertrainSystem`
- `TorqueDistributor`

### Problems being fixed
Legacy `GearboxSystem` computes RPM from:
- `abs(wheelRpm) * abs(driveRatio)`

That is serviceable, but it is too wheel-locked and contributes to stretched or fake audio transitions.

### New model requirements
Engine RPM must blend:
- free-rev target
- wheel-coupled RPM

Formula direction:
- free-rev RPM from throttle
- wheel-driven RPM from drivetrain
- coupling based on clutch, slip, shift state, handbrake, grounded state

### Required new gearbox features
- explicit shift state
- shift progress
- torque cut during shift
- post-shift recouple behavior
- better kickdown
- better downshift rev recovery
- no gear hunting

### Torque distribution goals
- support FWD / RWD / AWD
- pseudo-LSD behavior
- stable launches
- predictable drift exits

Exit criteria:
- V2 acceleration, braking transitions, shifting, reverse, and wheel torque match or exceed legacy feel
- RPM is stable enough for premium audio

---

## Phase 4 — Handling replacement
Goal: split the monolithic driving behavior into systems.

### Replace with
- `VehicleSteeringSystem`
- `VehicleBrakeSystem`
- `VehicleGripSystem`
- `VehicleDriftSystem`
- `VehicleAssistSystem`
- `VehicleAeroSystem`
- `VehicleWallResponseSystem`
- `WheelVisualSynchronizer`

### Steering targets
- low-speed steering lock around 34–36
- high-speed lock around 8–10
- quick but stable steering
- drift steer bonus without twitchiness

### Brake targets
- proper foot brake torque
- handbrake drift initiation
- reverse braking support
- roll resistance support

### Grip targets
Grip must be layered:
- base grip
- transfer grip
- drift grip
- handbrake grip
- recovery grip

### Drift targets
- easy entry
- sustained angle under throttle
- controllable rear slip
- heroic recovery
- no random snap-back

Recommended drift states:
- None
- Entry
- Sustain
- Donut
- Recovery

### Assist targets
Assists must support intent, not fight it:
- more stability in normal drive
- less interference during drift
- stronger recovery support when regaining control

### Aero/wall targets
- simple modular downforce and drag
- arcade wall-glance behavior
- no dead-stop wall punishment

Exit criteria:
- V2 handling equals or beats current NFSU-like feel
- no dependence on `VehicleDynamicsController`

---

## Phase 5 — Audio replacement
Goal: replace the legacy audio monolith.

### Why replacement is needed
`CarEngineAudio` currently owns:
- telemetry smoothing
- source building
- engine loop management
- pitch clamping
- shift ducking
- limiter behavior
- turbo/intake/skid helpers
- diagnostics

That is too much ownership in one runtime.

### Build
- `VehicleAudioControllerV2`
- `EngineAudioStateFeed`
- `EngineLayerPlayer`
- `EngineTransientPlayer`
- `VehicleAuxAudioPlayer`
- `VehicleSurfaceAudioPlayer`
- `VehicleAudioMixerRouter`

### State feed responsibilities
Convert `VehicleState` into stable audio values:
- smoothed RPM
- RPM01
- load
- on-throttle blend
- off-throttle blend
- shift duck
- limiter amount
- turbo spool
- slip
- speed factor

### Engine layer player responsibilities
- idle
- on-throttle bands
- off-throttle bands
- reverse
- volume crossfades
- narrow pitch windows

Key rule:
Pitch is fine control only. Crossfades do most of the work.

### Transient player responsibilities
- shift up
- shift down
- throttle blip
- limiter chatter
- crackle / spark chatter
- blow-off / one-shots

### Aux player responsibilities
- intake
- drivetrain whine
- turbo spool
- tire roll
- wind

### Surface player responsibilities
- skid
- scrub
- handbrake squeal
- road hiss

### Mixer routing target
- Master
  - SFX
    - Vehicles
      - Engine
      - EngineTransient
      - Drivetrain
      - Tires
      - Wind
      - Surface
  - Music
  - UI
  - Ambience

Exit criteria:
- V2 audio no longer depends on legacy controller references
- no stretched low-to-mid transitions
- no top-loop whine from over-pitching
- car audio responds cleanly to RPM, load, shift, slip, and speed

---

## Phase 6 — Data split and authoring cleanup
Goal: remove dependence on monolithic legacy data layout.

### Keep temporarily
- `InputReader`
- `WheelSet`
- `RuntimeVehicleStats`
- `VehicleStatsData`
- `NFSU2CarAudioBank`

### Long-term split
- `VehiclePowertrainProfile`
- `VehicleHandlingProfile`
- `VehicleAudioProfile`

### Why this matters
Legacy data currently mixes:
- engine
- gearing
- suspension
- steering
- assists
- UI ratings

The new system needs clearer domains.

Exit criteria:
- per-car tuning is modular
- audio tuning is independent from suspension tuning
- handling tuning is independent from UI stats

---

## Phase 7 — Prefab and scene conversion
Goal: remove legacy ownership from prefabs and scenes.

### PlayerCar prefab target
Keep:
- rigidbody
- wheel colliders
- center-of-mass marker
- model root
- audio root
- appearance controller
- setup bridge
- V2 controller stack
- world bootstrap

Remove/disable:
- active `VehicleDynamicsController`
- active legacy `GearboxSystem`
- active legacy `VehicleAudioController` / `CarEngineAudio`
- any placeholder baked current-car behavior

### Scene conversion
Update:
- world scene player-car root
- garage showroom usage
- bootstrap order
- race scene player car if separate
- minimap/camera dependencies that rely on old controller references

Exit criteria:
- prefab does not need legacy scripts for live gameplay
- world scene uses only the new startup path

---

## Phase 8 — Legacy shutdown
Goal: delete the old runtime safely.

### Remove after parity is proven
- `VehicleDynamicsController`
- `GearboxSystem`
- `CarEngineAudio`
- `VehicleAudioController`
- old bridge compatibility shims
- old fallback player-car binders

### Preconditions
Do not remove them until:
- V2 world spawn is correct
- V2 movement is correct
- V2 audio is correct
- garage → world persistence is stable
- no legacy type is still referenced by UI, VFX, or gameplay systems

Exit criteria:
- project builds and runs with legacy vehicle/audio stack removed
- no missing references in prefabs or scenes

---

## 5. What specifically stays temporary during migration

Temporary keep list:
- `InputReader`
- `WheelSet`
- `RuntimeVehicleStats`
- `VehicleStatsData`
- `NFSU2CarAudioBank`
- temporary compatibility wrapper if needed for existing prefab names

These stay because they already match your prefab wiring and authored assets.

---

## 6. Risks and failure modes to avoid

### 6.1 Two active driving owners
Never allow:
- `VehicleDynamicsController`
- and `VehicleControllerV2`

to both control the same player car.

### 6.2 Two active audio owners
Never allow:
- `CarEngineAudio` / `VehicleAudioController`
- and `VehicleAudioControllerV2`

to both own the same car audio runtime.

### 6.3 Placeholder prefab identity winning
The player car prefab must not self-select its final runtime car.
The selected car must come from saved progress.

### 6.4 Audio reading dirty physics state
Audio must stop reading mixed legacy controller internals directly once V2 is active.

### 6.5 Manual inspector wiring dependence
Critical scene/prefab refs should be explicit, but the system must not rely on brittle temporary manual fixes forever.

---

## 7. Recommended implementation order from here

### Stage A — Stabilize startup
1. finalize save authority
2. finalize world bootstrap
3. finalize setup bridge ownership
4. make selected car consistent everywhere

### Stage B — Stabilize V2 movement offline
1. `VehicleState`
2. `VehicleTelemetry`
3. `VehicleInputAdapter`
4. `EngineRPMModel`
5. `GearboxSystemV2`
6. `PowertrainSystem`
7. `VehicleSteeringSystem`
8. `VehicleBrakeSystem`
9. `VehicleGripSystem`
10. `VehicleDriftSystem`
11. `VehicleAssistSystem`

### Stage C — Stabilize V2 audio
1. `EngineAudioStateFeed`
2. `EngineLayerPlayer`
3. `EngineTransientPlayer`
4. `VehicleAuxAudioPlayer`
5. `VehicleSurfaceAudioPlayer`
6. mixer routing
7. per-car profile authoring

### Stage D — Convert prefabs and remove legacy
1. convert player car prefab
2. convert world scene
3. convert race scene
4. delete legacy scripts after parity

---

## 8. Definition of done

The migration is complete only when all of these are true:

- garage selection, save state, and world player car always match
- player car prefab no longer depends on legacy runtime scripts
- no old controller/audio owner is active in live gameplay
- V2 movement equals or beats current handling
- V2 audio equals or beats current audio
- all selected cars spawn correctly
- wheel visuals, drift, reverse, shifting, and audio all function in the new stack
- legacy classes can be removed without breaking scenes or prefabs

---

## 9. Practical recommendation

Do not switch everything at once again.

Use this sequence:
1. keep stable baseline
2. finish startup ownership
3. finish V2 movement until it matches legacy
4. finish V2 audio on top of stable V2 movement
5. then remove legacy

That is the clean path to eliminate legacy without breaking the game every time.

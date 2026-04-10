# Part 3 — Runtime Vehicle Physics

## Objective
Implement the hybrid driving model for Full Throttle.

## Handling philosophy
This is not a pure sim and not a pure arcade model.
The handling should combine:
- responsive turn-in and stylish street feel from NFS
- believable weight transfer and drivetrain character from Forza

## Scope
Implement only the runtime driving layer.

## Main runtime script
Create or update a main vehicle controller, for example:
- VehicleController.cs

## Responsibilities
The controller should:
- read VehicleStatsData
- apply acceleration and braking
- apply steering
- simulate grip changes
- support drivetrain behavior
- simulate weight transfer
- support mild arcade assists

## Required systems

### 1. Torque curve driven acceleration
Do not use only a flat acceleration value.
Acceleration should be influenced by:
- current RPM
- torque curve
- gearing
- final drive

### 2. Drivetrain identity
Support at minimum:
- FWD: easier stability, more understeer
- RWD: more throttle rotation, drift-friendly
- AWD: better launch, more planted feel

### 3. Weight transfer
Implement a lightweight simcade version of:
- front weight load under braking
- rear squat under acceleration
- lateral lean during turn load

This can be used both for grip calculations and visual feel.

### 4. Dynamic grip model
Grip should not be static.
Blend or adjust grip based on:
- speed
- steering input
- slip angle
- throttle load
- braking load

### 5. Assist layer
Implement subtle assists only:
- drift initiation forgiveness
- countersteer assistance
- yaw stabilization
- high-speed stability

These assists should support the player, not fully drive the car for them.

## Non-goals
Do not implement:
- nitrous system if it is separate
- lighting
- garage UI
- editor creation tools
- visual kit swapping

## Tuning expectations by archetype
Use archetype baselines so each car family feels distinct before per-car tuning.

Examples:
- StreetCompact = agile, lower speed ceiling
- Muscle = heavy torque, less graceful cornering
- Executive = stable and heavier
- Sports = balanced and expressive
- Hero = best blend of style and control

## Deliverables
- VehicleController or equivalent
- drivetrain-aware handling
- torque curve usage
- dynamic grip behavior
- assist layer hooks

## Done criteria
This part is complete when:
- cars no longer feel identical
- drivetrain type changes behavior
- steering and grip feel progressive
- the foundation exists for per-car tuning

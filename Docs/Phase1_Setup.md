# Phase 1 Setup

This repo now starts at **Phase 1 - Core Vehicle** from the build document:

- Rigidbody setup
- WheelColliders
- Steering, throttle, brake, and handbrake
- Chase camera follow

The implementation assumes a **fresh Unity 2022.3 LTS 3D project using the built-in render pipeline** because the repo started empty. The gameplay scripts are render-pipeline agnostic, so they can still be moved into URP or HDRP later.

## Files Added For Phase 1

- `Assets/Scripts/Vehicle/VehicleInput.cs`
- `Assets/Scripts/Vehicle/VehicleDynamicsController.cs`
- `Assets/Scripts/Vehicle/VehicleCameraFollow.cs`
- `Assets/Scripts/Editor/Phase1SceneBuilder.cs`

## Fastest Way To Build The Scene

1. Open the repo in Unity `2022.3.21f1` or a newer compatible editor.
2. Wait for script compilation to finish.
3. Make sure `Edit > Project Settings > Player > Active Input Handling` includes `Input Manager` or `Both`.
4. In the top menu, click `Underground > Phase 1 > Create Vehicle Test Scene`.
5. Open `Assets/Scenes/Phase1_VehicleTest.unity` if Unity does not focus it automatically.
6. Press Play.

The editor tool creates:

- a flat test ground
- a drivable `PlayerCar`
- four `WheelCollider` wheels
- simple body and wheel visuals
- a `Main Camera` with `VehicleCameraFollow`
- a saved prefab at `Assets/Prefabs/PlayerCar.prefab`

## Controls

- `W` or `Up Arrow`: throttle
- `S` or `Down Arrow`: brake
- `A / D` or `Left / Right Arrow`: steer
- `Space`: handbrake
- `R`: reset car upright

## Manual Inspector Setup Reference

If you want to rebuild or replace the generated primitive car by hand, use these values as the starting baseline.

### PlayerCar

GameObject:

- Position: `(0, 0.65, 0)`

Components:

- `Rigidbody`
- `BoxCollider`
- `VehicleInput`
- `VehicleDynamicsController`

`Rigidbody`

- Mass: `1350`
- Drag: `0.02`
- Angular Drag: `0.5`
- Interpolate: `Interpolate`
- Collision Detection: `Continuous Dynamic`

`BoxCollider`

- Center: `(0, 0.45, 0)`
- Size: `(1.9, 0.9, 4.2)`

Child transforms:

- `CenterOfMass` at `(0, -0.3, 0.15)`
- `CameraTarget` at `(0, 1.25, -0.15)`

### WheelCollider Layout

Wheel local positions:

- Front Left: `(-0.85, 0.2, 1.38)`
- Front Right: `(0.85, 0.2, 1.38)`
- Rear Left: `(-0.85, 0.2, -1.35)`
- Rear Right: `(0.85, 0.2, -1.35)`

Each `WheelCollider`

- Radius: `0.34`
- Mass: `25`
- Wheel Damping Rate: `1`
- Force App Point Distance: `0.1`
- Suspension Distance: `0.2`

Suspension Spring:

- Spring: `35000`
- Damper: `4500`
- Target Position: `0.45`

Forward Friction:

- Extremum Slip: `0.4`
- Extremum Value: `1.3`
- Asymptote Slip: `0.8`
- Asymptote Value: `0.95`
- Stiffness: `1.55`

Sideways Friction:

- Extremum Slip: `0.22`
- Extremum Value: `1.2`
- Asymptote Slip: `0.5`
- Asymptote Value: `0.9`
- Stiffness: `1.9`

### VehicleDynamicsController

References:

- `Input Source`: `PlayerCar/VehicleInput`
- `Center Of Mass Override`: `PlayerCar/CenterOfMass`

Drive:

- `Max Motor Torque`: `2400`
- `Max Brake Torque`: `3600`
- `Max Handbrake Torque`: `6000`
- `Top Speed Kph`: `180`

Steering:

- `Max Steer Angle`: `32`
- `Steering Speed Reference Kph`: `160`
- `Steering By Speed`: keep the default curve from the script

Stability:

- `Downforce`: `90`
- `Lateral Grip Assist`: `2.5`
- `Anti Roll Force`: `4500`
- `Reset Lift`: `1.2`

Axles:

- Front Axle:
  Left Wheel = `Wheel_FL`
  Right Wheel = `Wheel_FR`
  Left Visual = `Wheel_FL_Visual`
  Right Visual = `Wheel_FR_Visual`
  Steering = `true`
  Powered = `true`
  Handbrake = `false`
- Rear Axle:
  Left Wheel = `Wheel_RL`
  Right Wheel = `Wheel_RR`
  Left Visual = `Wheel_RL_Visual`
  Right Visual = `Wheel_RR_Visual`
  Steering = `false`
  Powered = `true`
  Handbrake = `true`

### Main Camera

Components:

- `Camera`
- `AudioListener`
- `VehicleCameraFollow`

`VehicleCameraFollow`

- `Target Vehicle`: `PlayerCar`
- `Target`: `PlayerCar/CameraTarget`
- `Target Body`: `PlayerCar/Rigidbody`
- `Follow Distance`: `6.5`
- `Follow Height`: `2.2`
- `Follow Smooth Time`: `0.12`
- `Rotation Sharpness`: `10`
- `Look Ahead Distance`: `4`
- `Min Field Of View`: `60`
- `Max Field Of View`: `78`
- `Speed For Max Field Of View`: `180`

## Tuning Notes

The current baseline is intentionally **simcade-first**, matching the build doc:

- low-speed steering is responsive
- steering tapers off at speed for stability
- the rear handbrake loosens the car without making it uncontrollable
- anti-roll and lateral grip assist reduce the worst WheelCollider wobble from a fresh setup

## Phase 1 Exit Criteria

Phase 1 is complete when:

- the car accelerates, steers, brakes, and handbrake-turns reliably
- the body stays reasonably stable under quick transitions
- the follow camera keeps the car readable at low and high speed
- you can tune values in the inspector without rewriting the scripts

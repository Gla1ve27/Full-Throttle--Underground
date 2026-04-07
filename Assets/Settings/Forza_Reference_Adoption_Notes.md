# Forza Reference Adoption Notes

This project is not attempting to clone ForzaTech directly. The goal is to adopt the parts of the reference that improve feel, stability, and readability within the current Unity 6 prototype.

## Adopted Adjustments

### 1. Physics-First Handling Direction
- Keep vehicle tuning as the first-order gameplay system.
- Prefer stable frame-time behavior and predictable control response over flashy but unstable effects.
- Favor conservative simcade defaults over exaggerated arcade steering and acceleration.

### 2. Tire-Centric Feel
- The runtime vehicle controller now applies dynamic grip modulation per wheel based on:
  - suspension travel as a lightweight load proxy
  - combined slip as a lightweight contact-patch stress proxy
- This is not a full thermal/pressure tire model.
- It is a practical Unity `WheelCollider` approximation intended to reduce toy-like grip behavior.

Implementation:
- `Assets/Scripts/Vehicle/VehicleDynamicsController.cs`

### 3. Speed Perception
- The chase camera already uses dynamic FOV.
- Added subtle high-speed aerodynamic buffeting above a threshold speed.
- The effect is intentionally mild and should support speed perception without making the camera feel arcade-like.

Implementation:
- `Assets/Scripts/Vehicle/VehicleCameraFollow.cs`

## Not Yet Implemented

### 1. ForzaTech-Style Budgeted Streaming
- No strict per-system millisecond budgeting.
- No sampler feedback streaming equivalent.
- No mesh-shader or VRS-specific optimization layer.

### 2. Full Calspan-Style Tire Model
- No thermal layers.
- No pressure simulation.
- No wear model.
- No full contact-patch solver.

### 3. Suspension Kinematics
- No true camber gain or roll-center simulation.
- Current setup uses `WheelCollider` suspension plus anti-roll and grip/load approximation.

### 4. Drivatar-Like AI
- No cloud-trained opponent behavior.
- No Bayesian or reinforcement-learning driver model.
- Current traffic and simple AI remain deterministic.

### 5. Forza-Grade Capture Pipeline
- No photogrammetry pipeline integration.
- No 12K HDR sky capture workflow.
- Current world uses imported environment assets and procedural/generator-based assembly.

## Guidance For Future Work

If this project continues toward a more premium simcade feel, the next high-value improvements should be:

1. Add per-wheel temperature and wear approximations.
2. Add a more advanced opponent driver model with alternative racing lines.
3. Add a global post-processing/volume stack tuned for nighttime contrast and speed readability.
4. Separate handling profiles for road, grip, and drift-oriented builds.

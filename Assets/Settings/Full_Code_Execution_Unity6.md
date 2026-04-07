# FULL CODE EXECUTION MD — UNITY 6 (6000.1.6f1) BEST-PRACTICE BUILD
## Simcade Driving + Underground Progression + Day/Night + Session Risk

---

## 0. EXECUTION ROLE

You are the lead Unity 6 gameplay engineer, technical designer, tools integrator, and systems architect for this project. Your task is to build a production-grade racing game prototype in Unity 6 using the best-fit modern stack for this version.

---

## 1. UNITY VERSION + BEST STACK

### Required Engine Version
- Unity 6 (6000.1.6f1)

### Best Project Template
- 3D (URP)

### Best Package Stack
- Input System: Modern handling for controllers and keyboards.
- Cinemachine: Dynamic racing camera behavior.
- TextMeshPro: Standard for all UI elements.
- AI Navigation: For traffic and AI opponent routing.
- Universal RP: Balance of performance and high-fidelity night visuals.

Current project status:
- `com.unity.inputsystem` is installed.
- `com.unity.cinemachine` is installed.
- `com.unity.ai.navigation` is installed.
- `com.unity.render-pipelines.universal` is installed.
- TMP is present in the project.

---

## 2. GAME VISION & LOOP

Vision: An open-world street racing game with simcade handling, inspired by underground car culture, featuring a continuous day/night cycle and session-based risk.

Core loop:
1. Garage: Safe zone for upgrades, customization, and saving.
2. Free Roam: Exit to the world; time passes naturally.
3. Events: Find races. Night unlocks higher-stakes wager races.
4. The Risk: Returns to the garage bank your money and rep. If the car is totalled before banking, session progress is lost.

---

## 3. DATA MODELS

### Vehicle Stats Data

Authoritative baseline:
- Power: motor torque, brake torque, max speed.
- Steering: steer angle, high-speed reduction.
- Stability: downforce, center of mass.
- Suspension: spring, damper, suspension distance.
- Friction: forward and sideways stiffness.
- Transmission: idle/max/shift RPM, final drive, gear ratios.

Current implementation lives in:
- `Assets/Scripts/Vehicle/VehicleStatsData.cs`

Project note:
- The current implementation also includes identity, economy defaults, anti-roll, grip assist, handbrake grip multiplier, reset lift, and default mass.
- Those additions are intentional extensions on top of the baseline brief.

---

## 4. CORE VEHICLE SYSTEMS

### Vehicle Dynamics Controller

Responsibilities:
- Apply motor torque to driven wheels.
- Apply steering to steerable wheels.
- Apply braking.
- Apply downforce.
- Use `VehicleStatsData` as the source of truth.

Current implementation lives in:
- `Assets/Scripts/Vehicle/VehicleDynamicsController.cs`

Project note:
- The current controller is already beyond the baseline brief.
- It includes runtime stats, upgrades, gearbox integration, reverse logic, anti-roll, grip assist, and wheel visual syncing.

---

## 5. WORLD & SESSION SYSTEMS

### Day/Night Cycle Controller

Required behavior:
- Full day length is configurable.
- Time loops from 24 back to 0.
- Night window is `20:00` to `06:00`.
- Sun rotation and lighting change with time.

Current implementation lives in:
- `Assets/Scripts/TimeSystem/DayNightCycleController.cs`

Alignment note:
- The project now uses the exact `20:00-06:00` night gate from this brief.

### Session Manager

Responsibilities:
- Track session money and reputation.
- Lose unbanked session gains when the vehicle is totalled.
- Bank session progress only when returning to the garage.

Current implementation lives in:
- `Assets/Scripts/Session/SessionManager.cs`
- `Assets/Scripts/Save/PersistentProgressManager.cs`

Project note:
- The current implementation also updates risk, persists world time, and banks reputation alongside money.

---

## 6. CAMERA EFFECTS

Preferred direction:
- Dynamic FOV for speed sensation.
- Base FOV around `55`.
- Max FOV around `75` at top speed.
- Smooth transitions.

Current implementation lives in:
- `Assets/Scripts/Vehicle/VehicleCameraFollow.cs`

Project note:
- The live project is currently using a more custom tuned follow profile based on the latest requested inspector defaults.
- If this document becomes the new final camera authority, update `VehicleCameraFollow` and any builder-generated defaults together.

---

## 7. DEVELOPMENT PHASES

1. Phase 1: Physics tuning in `VehicleTest`.
2. Phase 2: World setup with day/night and garage transitions.
3. Phase 3: Progression, economy, upgrades, and save systems.
4. Phase 4: Polish with AI traffic, HUD, and engine audio.

---

## 8. TEST CHECKLIST

- [ ] Vehicle accelerates, brakes, and steers correctly.
- [ ] Time of day progresses and affects lighting.
- [ ] Money earned in races is lost if the car crashes before banking.
- [ ] Money is saved only when returning to the garage.
- [ ] Upgrades purchased in the garage persist after reloading.

---

## 9. CURRENT IMPLEMENTATION MAP

Primary runtime systems:
- `Assets/Scripts/Vehicle/VehicleDynamicsController.cs`
- `Assets/Scripts/Vehicle/VehicleCameraFollow.cs`
- `Assets/Scripts/TimeSystem/DayNightCycleController.cs`
- `Assets/Scripts/Session/SessionManager.cs`
- `Assets/Scripts/Save/PersistentProgressManager.cs`
- `Assets/Scripts/Race/RaceManager.cs`
- `Assets/Scripts/Garage/GarageManager.cs`

Primary editor/build pipeline:
- `Assets/Scripts/Editor/UndergroundPrototypeBuilder.cs`
- `Assets/Scripts/Editor/UndergroundPrototypeBuilder.Automation.cs`
- `Assets/Scripts/Editor/UndergroundPrototypeBuilder.ScenesA.cs`
- `Assets/Scripts/Editor/UndergroundPrototypeBuilder.ScenesB.cs`
- `Assets/Scripts/Editor/UndergroundPrototypeBuilder.Garage.cs`
- `Assets/Scripts/Editor/UndergroundPrototypeBuilder.Prefab.cs`
- `Assets/Scripts/Editor/UndergroundPrototypeBuilder.ScenePrefabs.cs`

---

## Final Brief

Build this in Unity 6 using URP. Focus on the core driving feel first, because all other systems depend on the player enjoying movement. Preserve the high-stakes atmosphere by ensuring the session-risk loop remains meaningful.

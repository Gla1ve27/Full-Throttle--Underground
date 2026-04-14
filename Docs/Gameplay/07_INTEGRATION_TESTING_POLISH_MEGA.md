# 07 — Integration, Testing, Optimization, and Polish Mega
This final module defines when systems are allowed to ship together.

## 1. Integration order
1. World generation
2. Vehicle controller
3. Camera/speed/day-night
4. Event generation
5. Traffic
6. Race AI
7. Police
8. Progression/save/UI
9. Polish/optimization

## 2. Test scenes required
Create:
- VehicleTest.unity
- CameraFeelTest.unity
- WorldGenTest.unity
- EventFlowTest.unity
- TrafficPoliceTest.unity
- FullSlice_Night.unity

## 3. Performance rules
- traffic and chevrons must be pooled where possible
- event props spawn only during active events
- AI update rate can be tiered by distance
- bridge and skyline vistas should use LODs
- profile CPU and GPU separately

## 4. QA checklist
### World
- no broken road meshes
- no unreachable event starts
- no stuck traffic spawn points

### Vehicle
- no random flipping under normal driving
- no steering deadzone that makes city impossible

### Camera
- no jitter
- no clipping through road constantly
- no FOV pop

### Day/Night
- emissive intensity ramps properly
- night events feel visually distinct

### Events
- checkpoints ordered
- finish always reachable
- chevrons face correct direction

### Police
- escalation works
- cooldown works
- no infinite chase lock after escape

### AI
- can finish events
- can recover from small mistakes
- does not rubber-band unrealistically

## 5. Polish backlog
- camera collision avoidance
- cinematic event intros
- richer police behaviors
- crowd/ambient details in hotspots
- advanced route preview UI
- better garage visuals
- more nuanced traffic lane logic

## 6. Ship gate for first playable alpha
You may call the build a valid alpha only if:
- one map exists and is enjoyable in free roam
- at least six generated events are stable
- one full day/night cycle works
- police can chase and lose player
- traffic coexists with player and AI races
- save system preserves profile and current car

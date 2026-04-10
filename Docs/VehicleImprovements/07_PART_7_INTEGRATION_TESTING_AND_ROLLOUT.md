# Part 7 — Integration, Testing, and Rollout

## Objective
Connect the prior parts into a stable rollout sequence for Full Throttle.

## Scope
This part is for integration only.
Do not redesign earlier parts unless required for compatibility.

## Integration order
1. Confirm architecture and enums
2. Confirm all vehicle data assets load
3. Connect runtime controller to VehicleStatsData
4. Connect VehicleLightController to prefabs
5. Connect visual kit logic to Solstice Type-S
6. Enable Underground car creator
7. Verify legacy ID mapping
8. Run regression tests

## Required tests

### Data tests
- each roster car has a valid definition asset
- each definition points to valid stats
- no broken prefab references
- no duplicate old RMCar26 entries remain in active roster

### Runtime tests
- FWD / RWD / AWD feel different
- acceleration responds to torque curve
- braking affects behavior
- grip changes progressively
- hero car feels distinct

### Lighting tests
- headlights toggle cleanly
- brake lights activate under braking
- reverse lights activate in reverse
- emissive materials update correctly

### Upgrade tests
- Solstice visual kits apply correctly
- highest valid kit is selected if using auto-priority
- visual kit does not create duplicate roster entry

### Editor tooling tests
- Underground creator makes correct folders and assets
- generated content follows naming standards
- a new test car can be created without manual file duplication

## Rollout guidance
Use a staged rollout:
- stage 1: one test car only
- stage 2: hero car + one AWD tuner + one muscle car
- stage 3: full roster migration

## Suggested staging cars
- Solstice Type-S
- Reizan GT-RB
- Maverick Vengeance SRT

## Non-goals
Do not add unrelated content like dealership UI, decals, or multiplayer syncing in this phase.

## Deliverables
- all prior parts connected
- test checklist completed
- staged rollout plan completed

## Done criteria
This part is complete when:
- the vehicle system works end-to-end
- new cars can be added sustainably
- the system is clean enough for future tuning and expansion

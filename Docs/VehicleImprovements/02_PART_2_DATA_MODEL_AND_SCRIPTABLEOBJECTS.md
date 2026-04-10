# Part 2 — Data Model and ScriptableObjects

## Objective
Build the data-driven foundation for the Full Throttle vehicle system.

## Scope
Implement only the data assets and structures needed to define cars cleanly.

## Required assets
Create or update these core data structures:

### VehicleDefinition
Purpose:
Defines one car entry used by the game catalog and runtime loading.

Suggested fields:
- Id
- DisplayName
- VisualPrefabPath
- Stats
- Archetype
- Drivetrain
- AvailableUpgrades
- ManufacturerLoreName (optional)
- Description (optional)

### VehicleStatsData
Purpose:
Stores driving behavior and tuning values.

Suggested groups:

#### Powertrain
- Horsepower
- Torque
- TorqueCurve
- RedlineRPM
- IdleRPM
- GearRatios
- FinalDrive

#### Chassis
- Mass
- WeightDistributionFront
- CenterOfMassHeight
- SuspensionStiffness
- SuspensionDamping
- AntiRollBias

#### Grip and handling
- FrontGrip
- RearGrip
- Traction
- BrakeGrip
- SlipAngle
- RecoveryRate
- SteeringResponse
- HighSpeedStability

#### Assist layer
- DriftAssist
- CounterSteerAssist
- YawStability
- NitrousGripAssist

### UpgradeDefinition
Add or confirm support for:
- Category
- VisualPrefabOverridePath
- HandlingModifier or StatsModifier reference
- ReputationRequired
- Price
- UpgradeId

## Important rule
Do not flatten everything into only Speed / Handling / Torque / Mass.
The system needs enough depth to support different car identities.

## Create starter assets
Create one VehicleStatsData asset and one VehicleDefinition asset for each core roster vehicle.

The values can be placeholder-tuned, but the assets must exist.

## Initial roster asset list
- zodic_s_classic
- maverick_vengeance_srt
- protoso_c16
- weaver_pup_s
- stratos_element_9
- reizan_gt_rb
- reizan_icon_iv
- uruk_grinder_4x4
- reizan_vanguard_34
- cyro_monolith
- hanse_executive
- solstice_type_s

## Legacy support
Update the save migration or legacy ID map so old IDs can still resolve to the new base vehicle IDs.

## Non-goals for this part
Do not:
- apply physics
- instantiate prefabs
- implement lights
- implement upgrade logic

## Deliverables
- ScriptableObject class definitions
- starter assets for all roster vehicles
- clean naming conventions
- old-to-new ID mapping support

## Done criteria
This part is complete when:
- every car can exist as a data asset
- the roster is no longer dependent on old scattered IDs
- future runtime systems can read all required data from assets

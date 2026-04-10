# Part 6 — Underground Modular Car Creator

## Objective
Create editor tooling that makes adding a new car modular, fast, and consistent.

## Scope
Implement an editor workflow under the Underground menu that creates the required files and folder structure for a new vehicle.

## Desired user experience
From the Unity top menu:
- Underground -> Vehicles -> Create New Vehicle

The tool should create the base content for a new car so future additions are not manual and error-prone.

## Main editor script
Create:
- UndergroundVehicleCreator.cs

## Minimum generated content
When creating a new vehicle, generate:
- vehicle folder
- VehicleDefinition asset
- VehicleStatsData asset
- Prefabs folder
- Materials folder
- optional placeholder prefab template
- optional light rig placeholders
- optional default wheel mount structure

## Suggested folder output
```text
Assets/FullThrottle/Vehicles/<vehicle_id>/
├── Data/
│   ├── <vehicle_id>_Definition.asset
│   └── <vehicle_id>_Stats.asset
├── Prefabs/
├── Materials/
├── Meshes/
└── Textures/
```

## Recommended inputs
The editor window or creator flow may ask for:
- vehicle ID
- display name
- archetype
- drivetrain
- whether to generate a default prefab template
- whether to generate a light rig template

## Strong recommendation
Prefer an EditorWindow flow instead of only a simple MenuItem if that gives cleaner UX.

## Optional automation
If practical, also support:
- adding the new car to PlayerCarCatalog
- creating a starter upgrade list
- creating a default light binding component
- creating a test scene spawn entry

## Important rule
The tool must produce standardized, future-proof content.
No one-off file naming.

## Non-goals
Do not fully import meshes automatically unless your pipeline already supports that safely.

## Deliverables
- Underground menu entry
- creation tool or editor window
- generated files/folders
- standardized naming and layout

## Done criteria
This part is complete when:
- adding a new car becomes mostly one-click
- new cars automatically get the critical files they need
- future roster expansion becomes sustainable

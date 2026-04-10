# Part 1 — Architecture and Guardrails

## Objective
Establish the vehicle system architecture for Full Throttle using a modular, expandable design that supports:
- hybrid NFS + Forza-inspired handling
- lore-friendly vehicle roster
- future vehicle additions
- visual kit upgrades
- independent lighting system
- editor automation under the Underground menu

## Design target
Full Throttle should feel like:
- NFS in excitement, responsiveness, and style
- early Forza Horizon in mass, grip transition, and drivetrain character

Target formula:
- 70% grounded simcade
- 30% NFS-style dramatized street feel

## Non-goals for this part
Do not implement:
- full vehicle physics
- light logic
- prefab spawning
- upgrade application
- editor tools

This part only defines the architecture and implementation boundaries.

## Required system modules
Create or confirm these module boundaries:

### Data layer
Stores all vehicle definitions and tuning data through ScriptableObjects.

### Runtime layer
Handles vehicle simulation, player input response, grip, torque, steering, braking, and assists.

### Visual layer
Handles meshes, wheel visuals, materials, lights, and model overrides.

### Upgrade layer
Handles performance upgrades and visual kits.

### Editor tooling layer
Provides one-click creation tools in the Underground menu for adding new cars.

## Core rules
1. Every car must be data-driven.
2. No hardcoded car-specific tuning in gameplay scripts.
3. Car visuals and car stats must be separable.
4. New vehicles must be creatable without manually duplicating many files.
5. Car additions must support future batch scaling.

## Required enums
Define or confirm these enums:
- VehicleArchetype
- DrivetrainType
- UpgradeCategory

### Suggested VehicleArchetype values
- StreetCompact
- Muscle
- Executive
- Sports
- Supercar
- Offroad
- Hero

### Suggested DrivetrainType values
- FWD
- RWD
- AWD

### Suggested UpgradeCategory values
- Performance
- VisualKit
- Cosmetic

## Roster direction
Use lore-friendly names and keep the existing consolidation plan.

Core roster:
- Zodic S-Classic
- Maverick Vengeance SRT
- Protoso C-16
- Weaver Pup S
- Stratos Element 9
- Reizan GT-RB
- Reizan Icon IV
- Uruk Grinder 4x4
- Reizan Vanguard 34
- Cyro Monolith
- Hanse Executive
- Solstice Type-S

## Hero car consolidation rule
The old RMCar26 variants B, C, and D are no longer separate cars.
They become visual kit upgrades for Solstice Type-S.

## Deliverables for this part
- architecture confirmation
- enums created or updated
- clear folder/module naming rules
- no gameplay behavior yet

## Folder suggestion
```text
Assets/FullThrottle/Vehicles/
Assets/FullThrottle/Vehicles/Data/
Assets/FullThrottle/Vehicles/Runtime/
Assets/FullThrottle/Vehicles/Visual/
Assets/FullThrottle/Vehicles/Upgrades/
Assets/FullThrottle/Vehicles/Editor/
```

## Done criteria
This part is complete when:
- architecture is clean
- enums exist
- there is a clear separation of data/runtime/visual/editor concerns
- future parts can build on top of it without rework

# Part 5 — Visual Kits and Appearance Overrides

## Objective
Convert old RMCar26 variant logic into a proper visual kit system for Solstice Type-S and establish a reusable appearance override framework for future cars.

## Scope
Implement only appearance override logic and visual kit support.

## Required upgrade behavior
Visual kits should:
- swap the visual prefab or visual submodel
- optionally apply a mild handling modifier
- not create duplicate car entries in the base roster

## Main systems
Create or update:
- UpgradeDefinition.cs
- PlayerCarAppearanceController.cs

## Required logic
### UpgradeDefinition
Ensure support for:
- Category = VisualKit
- VisualPrefabOverridePath
- optional HandlingModifier
- ReputationRequired
- Price

### PlayerCarAppearanceController
When a player owns one or more visual kits for the active vehicle:
- find valid owned visual kits
- determine which one should apply
- use the override prefab or visual model
- optionally apply a light handling modifier layer

## Priority rule
If multiple visual kits are owned, either:
- apply the highest-tier one automatically
or
- support explicit player selection in future UI

For now, automatic highest unlocked is acceptable.

## Solstice Type-S conversion
Old entries:
- rmcar26_b
- rmcar26_c
- rmcar26_d

These should become:
- Solstice Type-S Kit B
- Solstice Type-S Kit C
- Solstice Type-S Kit D

## Important rule
Visual kits are not separate cars in the roster anymore.

## Non-goals
Do not implement a full garage UI selection flow in this part unless the project already has a safe hook for it.

## Deliverables
- visual kit upgrade support
- appearance override logic
- Solstice hero-car conversion to kits
- optional handling modifier hook

## Done criteria
This part is complete when:
- the hero car variants no longer bloat the car list
- visual upgrade ownership can change the active model
- the system can later support other cars with kit variants

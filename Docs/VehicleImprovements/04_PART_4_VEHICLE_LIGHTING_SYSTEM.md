# Part 4 — Vehicle Lighting System

## Objective
Implement a modular lighting system for each vehicle.

## Scope
Support:
- headlights
- tail lights
- brake lights
- reverse lights

## Main runtime script
Create or update:
- VehicleLightController.cs

## Responsibilities
The light controller should:
- toggle headlights on demand
- toggle brake lights while braking
- toggle reverse lights when reversing
- optionally switch emissive materials for tail/brake meshes

## Required setup
Each vehicle prefab should support named references or assigned references for:
- left headlight
- right headlight
- tail light meshes or renderers
- brake light meshes or renderers
- reverse lights

## Minimum functionality
### Headlights
- can be turned on/off globally or by time-of-day system
- support both actual Light components and emissive material response

### Tail lights
- may remain on when headlights are on
- should support emissive material response

### Brake lights
- brighten when brake input is active
- should not require headlights to be on if braking logic wants stronger readability

### Reverse lights
- turn on while the car is moving in reverse gear or reverse state

## Important rule
The light system must be reusable across all future vehicles.
Do not build per-car custom code branches.

## Suggested prefab convention
Each vehicle prefab should expose a light rig with consistent naming:
- HeadlightsRoot
- TailLightsRoot
- BrakeLightsRoot
- ReverseLightsRoot

## Nice-to-have
- HDRP emissive intensity support
- bloom-friendly material setup
- day/night sensitivity multiplier

## Non-goals
Do not build police light bars, neon kits, or complex turn signals in this part.

## Deliverables
- VehicleLightController
- prefab binding pattern
- emissive material support
- brake/reverse/headlight logic hooks

## Done criteria
This part is complete when:
- every car can use the same light controller
- braking visibly lights rear brake lamps
- headlights can be toggled cleanly
- reverse lights behave correctly

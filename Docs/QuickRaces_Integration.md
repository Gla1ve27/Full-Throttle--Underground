# Redesigned Quick Race Flow Implementation Plan

This plan outlines the changes required to link **Quick Race** with **MyCareer** progression and implement the new multi-step selection flow. Quick Race will now only allow tracks and cars unlocked/finished in MyCareer.

## User Review Required

> [!IMPORTANT]
> **Drift & Drag Physics**: You mentioned that driving physics for Drift/Drag are not good yet. I will implement the selection logic and placeholders for these modes, but the actual physics refinement is out of scope for this specific task unless requested.
> **"My Cars" Definition**: I am assuming "My Cars" refers to the customized versions of cars already owned in MyCareer. I will ensure the Quick Race system uses the saved appearance and stats for these vehicles.

## Proposed Changes

### 1. Data Structure & Progression Tracking

I will update the data models to track which races have been completed in MyCareer.

#### [MODIFY] [RaceDefinition.cs](file:///c:/Users/Marc%20Badua/Documents/GitHub/Full-Throttle--Underground/Assets/Scripts/Race/RaceDefinition.cs)
- Update `RaceType` enum to include `Drift` and `Drag`.
- Ensure all races have a unique `raceId`.

#### [MODIFY] [SaveGameData.cs](file:///c:/Users/Marc%20Badua/Documents/GitHub/Full-Throttle--Underground/Assets/Scripts/Save/SaveGameData.cs)
- Add `public List<string> completedRaceIds` to track finished races.

#### [MODIFY] [PersistentProgressManager.cs](file:///c:/Users/Marc%20Badua/Documents/GitHub/Full-Throttle--Underground/Assets/Scripts/Save/PersistentProgressManager.cs)
- Add methods to register completed races: `RegisterRaceCompletion(string raceId)`.
- Add a check method: `IsRaceUnlocked(string raceId)`.
- Ensure these are saved/loaded correctly.

### 2. Race Completion Integration

The game needs to know when a race is "finished/won" in Career mode to unlock it for Quick Race.

#### [MODIFY] [RaceManager.cs](file:///c:/Users/Marc%20Badua/Documents/GitHub/Full-Throttle--Underground/Assets/Scripts/Race/RaceManager.cs)
- Update `CompleteRace(bool playerWon)` to notify the `PersistentProgressManager` when the player wins.
- I will likely use the `EventBus` to publish a `RaceCompletedEvent`.

### 3. Quick Race UI & Flow (MainMenuNew Style)

The Selection UI will be integrated into the existing `MainMenuNew` structure, following the established glassmorphism and panel-based design.

#### [NEW] [QuickRaceSelectionPanelManager.cs](file:///c:/Users/Marc%20Badua/Documents/GitHub/Full-Throttle--Underground/Assets/Scripts/UI/QuickRaceSelectionPanelManager.cs)
- A new component that resides on the `Canvas/MainMenuUI` or a sub-panel.
- Manages the state transitions between the 4 selection steps.
- Uses the same `CanvasGroup` fading logic as `MainMenuFlowManager`.

#### [MODIFY] [QuickRaceFlowManager.cs](file:///c:/Users/Marc%20Badua/Documents/GitHub/Full-Throttle--Underground/Assets/Scripts/UI/QuickRaceFlowManager.cs)
- Update to coordinate with the new `QuickRaceSelectionPanelManager`.
- It will trigger the transition from the `QuickRacePanel` to the first selection step.

#### [MODIFY] [UndergroundPrototypeBuilder.MainMenuNew.cs](file:///c:/Users/Marc%20Badua/Documents/GitHub/Full-Throttle--Underground/Assets/Scripts/Editor/UndergroundPrototypeBuilder.MainMenuNew.cs)
- Update the scene builder to automatically create the new selection panels:
    - `QuickRace_TypePanel`
    - `QuickRace_MapPanel`
    - `QuickRace_CarPanel`
    - `QuickRace_TransmissionPanel`
- These panels will use the `BuildPanelChrome` and `CreateSubmenuStrip` patterns.
- Implement specialized "Tile" builders for Map and Car selection (similar to `CreateCarouselItem`).

### 4. Vehicle & Transmission Logic

#### [MODIFY] [GearboxSystem.cs](file:///c:/Users/Marc%20Badua/Documents/GitHub/Full-Throttle--Underground/Assets/Scripts/Vehicle/GearboxSystem.cs)
- Add a `bool useManualTransmission` flag.
- Implement manual gear shifting logic (e.g., listening for upshift/downshift inputs).
- Update `UpdateAutomatic` to only run if `!useManualTransmission`.

#### [MODIFY] [VehicleDynamicsController.cs](file:///c:/Users/Marc%20Badua/Documents/GitHub/Full-Throttle--Underground/Assets/Scripts/Vehicle/VehicleDynamicsController.cs)
- Expose a way to set the transmission type during initialization.

### 5. Session Initialization

#### [NEW] [QuickRaceSessionData.cs](file:///c:/Users/Marc%20Badua/Documents/GitHub/Full-Throttle--Underground/Assets/Scripts/Session/QuickRaceSessionData.cs)
- A simple static or scriptable object to hold the choices made in the menu (Race ID, Car ID, Transmission) to be used when the race scene loads.

## Open Questions

1. **Menu Aesthetics**: The Quick Race selection menu will follow the `MainMenuNew` style (Canvas/MainMenuUI/MainMenuPanel).
2. **Drift/Drag Scene**: Do you already have specific scenes or track layouts for Drift and Drag, or should I create placeholders?
3. **Manual Shifting Inputs**: Which keys do you prefer for Manual shifting (e.g., Left Shift/Ctrl, or Mouse buttons)?

## Verification Plan

### Automated Tests
- N/A (UI and Game Flow focused).

### Manual Verification
1. **Career Unlock**: Win a race in "Career" mode and verify it appears in the "Quick Race" map selection.
2. **Selection Flow**: Navigate through all 4 steps in the Quick Race menu.
3. **Transmission**: Verify that selecting "Manual" requires manual gear changes during the race.
4. **Car Visuals**: Verify that "My Car" in Quick Race matches the customization from Career.

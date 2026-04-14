# Stray Dog Foundation Copy Plan

This plan outlines the process of extracting the reusable core systems from your **CyberSiege** project and migrating them into your new **D'Stray** project repository. We will ensure all references to "CyberSiege" are omitted from folder structures.

## Proposed Changes

We will copy the following highly-reusable directories directly into the root `Assets` folder of `D'Stray`, maintaining their original folder names so Unity can easily resolve the scripts and assets.

### Target Destination
`C:\Users\Marc Badua\Documents\GitHub\D'Stray\Assets\`

### Core Systems to Migrate
1. **Character Movement & Camera**
   - `Scripts/Fort/` (Contains the `FortCharacterController`, Weapon logic, and Camera setup)
   - `Scripts/Core/` (Contains `CoreCameraSystem` and culling components)

2. **Narrative & Interaction**
   - `Scripts/Dialogue System/` (Contains all the NPC, Arrow Indicator, and UI trigger logic)
  
3. **Visuals & Atmosphere**
   - `FX/` (Effects and particles)
   - `Shader/` & `Shaders/` (Toon Shaders and graphs)
   - `UI/` (Canvas assets needed by the Dialogue System)

## User Review Required

> [!IMPORTANT]  
> Please confirm if you want me to proceed with this copy operation. Once you approve, I will run the required PowerShell scripts to transfer all the files directly to `D'Stray\Assets\<Folder Name>`.

## Verification Plan
- Execute PowerShell commands to recursively copy these specific directories (using `robocopy` or `Copy-Item`).
- List the contents of `C:\Users\Marc Badua\Documents\GitHub\D'Stray\Assets\` to verify the files arrived correctly.

# Full Throttle Cinematic Campaign Spine

## Phase 1 - Architecture Map

### Gameplay Spine
`FTBootstrap` creates the service graph, `FTSaveGateway` owns profile truth, `FTCampaignDirector` owns chapter truth, `FTRaceDirector` resolves race stakes, `FTRiskEconomyDirector` applies money/heat pressure, and `FTVehicleSpawnDirector` owns world vehicle truth.

The game loop is:

1. Garage selects one `FTCarDefinition`.
2. `FTSelectedCarRuntime` and `FTSaveGateway.Profile.currentCarId` agree.
3. `FTWorldTravelDirector` queues world entry.
4. `FTVehicleSpawnDirector` spawns the selected car.
5. `FTCampaignDirector` reads current chapter and unlock state.
6. Races, outruns, garage scenes, and narrative beats update the profile.
7. `FTSaveGateway` persists the result.

### Ownership Map
- Selected car truth: `FTSelectedCarRuntime`, backed by `FTSaveGateway.Profile.currentCarId`.
- Vehicle prefab truth: `FTCarDefinition.worldPrefab` and optional `visualPrefab`.
- Audio truth: `FTCarDefinition.audioProfileId` through `FTAudioProfileRegistry`.
- Campaign truth: `FTCampaignDefinition` plus `FTProfileData.currentChapterId`.
- Cutscene/narrative truth: `FTNarrativeBeatDefinition`.
- Race truth: `FTRaceDefinition`, resolved by `FTRaceDirector`.
- Rival truth: `FTRivalDefinition` for story, `FTOutrunRivalDefinition` for roaming rivals.
- Consequence truth: `FTRiskEconomyDirector`, `FTHeatDirector`, `FTWagerDirector`, and saved profile fields.

### Selected Car Truth Map
Garage and world never choose cars independently. Garage changes `FTSelectedCarRuntime`, save stores `currentCarId`, and world consumes the pending car through `FTWorldTravelDirector`. Any placeholder vehicle is temporary only if the selected `FTCarDefinition` explicitly points to it.

### Garage To World Flow
Garage continue button calls `FTWorldTravelDirector.QueueWorldEntry(carId, spawnPointId)`, then loads World. World spawn reads the pending car first, then save fallback, validates through `FTCarRegistry`, spawns or reuses the player vehicle, applies `FTCarDefinition`, sanitizes physics, and binds audio/camera.

### Race/Campaign Progression Flow
Winning a race raises `FTRaceResolvedSignal`. `FTCampaignDirector` records `completedRaceIds`, records rival wins when relevant, then checks current chapter completion and next chapter gates. Chapter completion can award money, reputation, district unlocks, reward ids, and next chapter ids.

### Cutscene Trigger Architecture
Use `FTCampaignTrigger` for world/garage trigger volumes and interaction points. It can:
- start a chapter
- complete a chapter
- play a specific `FTNarrativeBeatDefinition`
- play the current chapter garage beat
- play the current chapter radio follow-up

`FTNarrativeDirector` plays beats through assigned UI, or a fallback debug overlay if no UI is assigned yet.

### Audio Identity Flow
Each playable/rival car resolves:
`FTCarDefinition.carId -> audioProfileId -> FTAudioProfileRegistry -> FTVehicleAudioProfile -> FTVehicleAudioDirector`.

Garage preview and world runtime use the same `FTCarDefinition` and same `FTVehicleAudioProfile`.

## Phase 2 - Actual C# Scripts Added

Campaign runtime:
- `Assets/Scripts/Campaign/FTCampaignDefinition.cs`
- `Assets/Scripts/Campaign/FTCampaignChapterDefinition.cs`
- `Assets/Scripts/Campaign/FTCampaignDirector.cs`
- `Assets/Scripts/Campaign/FTCampaignSignals.cs`

Narrative/cutscene runtime:
- `Assets/Scripts/Campaign/FTNarrativeBeatDefinition.cs`
- `Assets/Scripts/Campaign/FTNarrativeDirector.cs`
- `Assets/Scripts/Campaign/FTCampaignTrigger.cs`

Profile extensions:
- `currentChapterId`
- `completedRaceIds`
- `completedChapterIds`
- `unlockedChapterIds`
- `seenNarrativeBeatIds`
- `unlockedRewardIds`

Editor package:
- `Assets/Scripts/Editor/FTCampaignAssetGenerator.cs`

## Phase 3 - Campaign Implementation Package

Generate the campaign assets from:
`Full Throttle/Sacred Core/Generate Gla1ve Cinematic Campaign Assets`

This creates:
- `campaign_full_throttle_gla1ve`
- chapter assets for chapters 0-20
- intro/post/garage/radio narrative beats for each chapter
- story act assets for Prologue, Acts I-IV, and Epilogue

### Chapter Table
| # | Chapter | District | Purpose |
|---|---|---|---|
| 0 | The Name Before The City | City Core | Arrival, Ross, first garage entry |
| 1 | Filler On The Grid | City Core | Player feels small and broke |
| 2 | The City Notices | City Core | King Serrano conflict begins |
| 3 | Lockup 13 | City Core | Garage becomes sacred |
| 4 | First Blood | City Core | King runner is humiliated |
| 5 | King Of The Core | City Core | First district boss |
| 6 | Better Roads, Worse People | Arterial Zone | Bigger money, colder scene |
| 7 | Edd's Offer | Arterial Zone | Risk economy temptation |
| 8 | Mav Cruz | Arterial Zone | Status rival introduced |
| 9 | Collapse | Arterial Zone | Costly setback |
| 10 | Back From The Concrete | Arterial Zone | Recovery arc |
| 11 | Gold Can Bleed | Arterial Zone | Mav defeated |
| 12 | Everybody's Watching | Mountain Fringe | City-wide attention |
| 13 | Thomas Cabanit | Mountain Fringe | Deep mirror rival |
| 14 | Home Is Not Safe | Mountain Fringe | Garage threatened |
| 15 | Edge Of The Mountain | Mountain Fringe | Thomas boss race |
| 16 | Lance D.C. | Highway Belt | Speed becomes terrifying |
| 17 | The Upper World | Highway Belt | Ray and city power |
| 18 | Last Night In Lockup 13 | City Core | Final garage scene |
| 19 | Crown Of The Night | Highway Belt | Final city-spanning race |
| 20 | Impossible To Erase | City Core | Epilogue/postgame |

### Rival Progression
- King Serrano: end of City Core.
- Mav Cruz: end of Arterial Zone.
- Thomas Cabanit: end of Mountain Fringe.
- Lance D.C.: Highway pressure wall.
- Ray: final power structure and final rival force.

### Cutscene Trigger Tables
Use intro beats when a chapter starts. Use garage beats after repairs, major upgrades, collapse, district wins, and final prep. Use radio beats after major races and chapter completion. Use post beats when `FTCampaignDirector.CompleteChapter` fires.

### Radio Chatter Tables
Generated radio beats are intentionally short. They should be expanded only after routes/rivals are playable, so chatter reacts to real player progress instead of becoming lore spam.

## Phase 4 - Unity Integration Guide

Runtime root:
- Add `FTCampaignDirector`.
- Add `FTNarrativeDirector`.
- Assign the generated `FTCampaignDefinition`.
- Optional: assign UI text and canvas group to `FTNarrativeDirector`.

World:
- Add `FTCampaignTrigger` to garage entrances, major route starts, district gates, or cinematic trigger volumes.
- Use `FTCampaignTriggerAction.StartChapter` for controlled chapter starts.
- Use `CompleteChapter` after final chapter objectives if not auto-completed by race gates.

Garage:
- Add a trigger/button event for `PlayCurrentGarageBeat`.
- Keep garage car selection driven by `FTSelectedCarRuntime`.

Race assets:
- Assign required `FTRaceDefinition` assets into each `FTCampaignChapterDefinition.requiredRaces`.
- Assign boss race `rivalId` so campaign can record beaten rivals.

Outrun:
- Use `FTOutrunRivalDefinition` for roaming enemy racers.
- Add `FTOutrunRivalDriver` to an FT vehicle prefab.
- Add `FTOutrunRoute` to a waypoint parent.

## Phase 5 - Validation Checklist

Story progression:
- Fresh save starts on `chapter_00_name_before_city`.
- Completing a chapter unlocks the next chapter.
- Required rep/race/rival gates prevent sequence skipping.
- Profile saves current chapter and completed chapters.

Selected car:
- Garage selected car and world spawned car match.
- Audio profile id in `FTCarDefinition` resolves through registry.
- No placeholder car is spawned unless selected car explicitly uses it.

Spawn:
- Spawned car has `FTPlayerVehicleBinder`.
- Spawned car has valid wheel colliders and rigidbody.
- Physics guard logs clean spawn.

Audio:
- Garage and world log the same car id and audio profile id.
- No hero/rival car has missing required audio profile.
- Rival cars receive dedicated or meaningfully overridden profiles.

Campaign/cutscene:
- `FTNarrativeDirector` logs every beat.
- A seen beat id is stored in save.
- Chapter intro, post, garage, and radio beats can all be played.

Economy/consequence:
- Race loss affects money/heat/wager exposure.
- Chapter reward changes bank/rep only once.
- Collapse chapter can be wired to repair debt and recovery missions.

## Phase 6 - Migration Guide

Reuse:
- Existing vehicle prefabs, model assets, audio clips, race route content, garage visuals, HUD, and minimap.
- Existing `FTRaceDefinition`, `FTRivalDefinition`, `FTCarDefinition`, and audio profile assets.

Discard or quarantine:
- Duplicate car selection logic.
- Placeholder fallback flows that override selected cars.
- Legacy day/night live cycle during gameplay.
- Traffic AI for named rivals.
- Any audio system that bypasses SacredCore vehicle audio.

Migration path:
1. Generate campaign assets.
2. Assign campaign asset to `FTCampaignDirector`.
3. Assign race definitions into chapter assets.
4. Assign rival ids on boss races.
5. Add chapter triggers to garage/world.
6. Replace story advancement scripts with `FTCampaignDirector` calls.
7. Keep old content assets, but stop using old progression ownership.

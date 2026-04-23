# Full Throttle - Phase 1 To Phase 3 Campaign Package

Scope: Phase 1, Phase 2, and Phase 3 only.

This package defines the new FT-prefixed campaign spine for Marc "Gla1ve" Badua. It does not preserve weak legacy progression as authority. Legacy content can be reused later, but the FT systems below are the future truth.

## Phase 1 - Architecture Map

### Core Gameplay Spine
The sacred core is built around one profile, one selected car, one campaign state, and one world spawn truth.

Runtime order:
1. `FTBootstrap` creates the service graph.
2. `FTSaveGateway` loads or creates `FTProfileData`.
3. `FTCarRegistry` validates car ids.
4. `FTSelectedCarRuntime` owns current selected car truth.
5. `FTCampaignDirector` owns current chapter truth.
6. `FTRaceDirector` owns active race truth.
7. `FTRiskEconomyDirector`, `FTHeatDirector`, and `FTWagerDirector` apply consequence.
8. `FTWorldTravelDirector` carries garage-to-world handoff.
9. `FTVehicleSpawnDirector` spawns the selected car deterministically.
10. `FTVehicleAudioDirector` resolves the selected car's exact audio identity.

### Ownership Map
- Profile/save truth: `FTSaveGateway`
- Campaign/chapter truth: `FTCampaignDirector`
- Chapter data: `FTCampaignChapterDefinition`
- Narrative/cutscene data: `FTNarrativeBeatDefinition`
- Race truth: `FTRaceDirector`
- Race data: `FTRaceDefinition`
- Rival story truth: `FTRivalDefinition`
- Roaming Outrun rival truth: `FTOutrunRivalDefinition`
- Car identity truth: `FTCarDefinition`
- Current selected car truth: `FTSelectedCarRuntime`
- World spawn truth: `FTVehicleSpawnDirector`
- Audio identity truth: `FTVehicleAudioProfile` through `FTAudioProfileRegistry`

### Selected Car Truth Map
There is no separate garage car and world car truth.

Flow:
`Garage UI -> FTSelectedCarRuntime -> FTSaveGateway.Profile.currentCarId -> FTWorldTravelDirector -> FTVehicleSpawnDirector -> FTPlayerVehicleBinder -> FTCarDefinition receivers`

Rules:
- No silent placeholder override.
- No showroom car contaminates world spawn.
- No duplicate selected-car logic.
- The selected car's audio profile id must resolve in garage and world.

### Garage To World Flow
1. Garage selects a car through `FTGarageDirector`.
2. `FTSelectedCarRuntime` updates current car id.
3. Continue queues world entry with `FTWorldTravelDirector.QueueWorldEntry(carId, spawnPointId)`.
4. World scene loads.
5. `FTVehicleSpawnDirector` consumes pending car id.
6. `FTCarRegistry` validates it.
7. The chosen car prefab is spawned or the scene player car is reused safely.
8. `FTPlayerVehicleBinder.ApplyDefinition` pushes car definition to vehicle, visuals, audio, and feel systems.
9. `FTVehiclePhysicsGuard` sanitizes rigidbody/wheels before play resumes.

### Race And Campaign Progression Flow
Race flow:
`Race marker -> FTRaceDirector.TryBeginRace -> race gameplay -> FTRaceDirector.CompleteRace -> FTRaceResolvedSignal`

Campaign flow:
`FTRaceResolvedSignal -> FTCampaignDirector records completed race/rival -> chapter gates checked -> chapter completed -> rewards/unlocks applied -> next chapter unlocked`

Chapter completion can apply:
- money reward
- reputation reward
- district unlocks
- reward ids
- next chapter unlocks

### Cutscene Trigger Architecture
`FTCampaignTrigger` is the scene-facing trigger bridge.

It supports:
- starting a chapter
- completing a chapter
- playing a specific narrative beat
- playing the current chapter garage beat
- playing the current chapter radio follow-up

`FTNarrativeDirector` plays the beat through assigned UI. If no UI is wired yet, it shows a temporary fallback overlay so the campaign can be tested immediately.

### Audio Identity Flow
Every meaningful car must resolve:
`FTCarDefinition.carId -> FTCarDefinition.audioProfileId -> FTAudioProfileRegistry -> FTVehicleAudioProfile -> FTVehicleAudioDirector`

Rules:
- Garage preview and world runtime use the same car/audio truth.
- No hero/rival car may silently fall back to generic audio.
- Starter, rival, boss, and halo cars must have dedicated or meaningfully overridden profiles.

## Phase 2 - Actual FT Sacred Core C# Scripts

The following scripts exist as the Phase 2 implementation layer.

### Campaign Runtime
`Assets/Scripts/Campaign/FTCampaignDefinition.cs`
- ScriptableObject root for the whole campaign.
- Stores protagonist identity and ordered chapter list.

`Assets/Scripts/Campaign/FTCampaignChapterDefinition.cs`
- ScriptableObject chapter unit.
- Stores act id, chapter id, district, gates, required races, Outrun rivals, narrative beats, and rewards.

`Assets/Scripts/Campaign/FTCampaignDirector.cs`
- Runtime authority for current chapter.
- Registers with `FTServices`.
- Reads/writes `FTSaveGateway.Profile.currentChapterId`.
- Records completed races and beaten rivals from `FTRaceResolvedSignal`.
- Applies chapter rewards and unlocks.
- Triggers intro/post/garage/radio beats through `FTNarrativeDirector`.

`Assets/Scripts/Campaign/FTCampaignSignals.cs`
- Signals:
  - `FTCampaignChapterStartedSignal`
  - `FTCampaignChapterCompletedSignal`
  - `FTNarrativeBeatTriggeredSignal`

### Narrative And Cutscene Runtime
`Assets/Scripts/Campaign/FTNarrativeBeatDefinition.cs`
- ScriptableObject for cinematics, pre-race intros, post-race scenes, garage scenes, radio calls, messages, montages, district takeover scenes, and epilogue beats.

`Assets/Scripts/Campaign/FTNarrativeDirector.cs`
- Runtime playback bridge for narrative beats.
- Stores seen beat ids in save profile.
- Supports assigned UI text/canvas.
- Has fallback overlay for immediate playtesting.

`Assets/Scripts/Campaign/FTCampaignTrigger.cs`
- Scene trigger for campaign actions.
- Can fire automatically on trigger enter or through an interaction key.

### Save Data Extension
`Assets/Scripts/Save/FTProfileData.cs`

Added fields:
- `currentChapterId`
- `completedRaceIds`
- `completedChapterIds`
- `unlockedChapterIds`
- `seenNarrativeBeatIds`
- `unlockedRewardIds`

### Outrun And Free-Roam Rival Foundation
`Assets/Scripts/Race/FTOutrunRivalDefinition.cs`
- Data for a roaming rival, its car, reward, challenge distances, and aggression.

`Assets/Scripts/Race/FTOutrunRivalDriver.cs`
- AI driver that feeds `FTDriverInput.SetManual`.
- It drives through the same FT vehicle controller path as the player, not through traffic logic.

`Assets/Scripts/Race/FTOutrunRoute.cs`
- Waypoint route source for roaming rivals.

`Assets/Scripts/Race/FTOutrunChallengeDirector.cs`
- Detects nearby rivals.
- Shows challenge prompt.
- Starts Outrun with Enter.
- Tracks player lead and rival lead.
- Resolves win/loss.

### World Time Direction
`Assets/Scripts/World/FTWorldTimePreset.cs`
- Fixed dusk/night world time preset data.

`Assets/Scripts/World/FTWorldTimePresetDirector.cs`
- Applies preset lighting, HDRP volume, skybox, fog, and shadow budget.
- Disables old live time movement during gameplay.

### Editor Generation
`Assets/Scripts/Editor/FTCampaignAssetGenerator.cs`
- Menu:
  `Full Throttle/Sacred Core/Generate Gla1ve Cinematic Campaign Assets`
- Generates campaign, acts, chapters, and narrative beats for Chapters 0-20.

`Assets/Scripts/Editor/FTSacredCoreSetupWizard.cs`
- Runtime Root now includes `FTCampaignDirector` and `FTNarrativeDirector`.
- Asset folder creation includes Campaign and Narrative folders.

## Phase 3 - Chapter-By-Chapter Campaign Implementation Package

Generated campaign root:
`campaign_full_throttle_gla1ve`

Protagonist:
- Real name: Marc Badua
- Street name: Gla1ve

Campaign thesis:
A nobody becomes a name that changes what the city respects.

### Chapter 0 - The Name Before The City
Act: Prologue  
District: City Core  
Purpose: Introduce Marc Badua, Ross, the city tone, and Lockup 13.

Intro beat:
Night rain, weak headlights, wet asphalt. Marc enters the city in a tired machine.

Key line:
Ross: "You sure this is the city you want?"

Gameplay:
- Arrival drive
- Basic control onboarding hidden inside the drive
- First garage entry

Garage beat:
JM checks the starter car and calls it rough, but alive.

Radio follow-up:
A new plate rolled into the city tonight. Nobody knows if it survives.

Reward:
- Garage unlocked
- Starter car ownership confirmed
- Low-tier race access

### Chapter 1 - Filler On The Grid
Act: Unknown Name  
District: City Core  
Purpose: Make the player feel small, broke, and underestimated.

Intro beat:
A low-tier lineup barely notices Marc. His car looks outclassed.

Key line:
Marc: "Talk all you want. I'm still here."

Gameplay:
- One sprint race
- One tight circuit
- One optional cash run

Garage beat:
Ross and JM push the idea that survival matters more than image.

Radio follow-up:
City Core has a new filler. Filler just won.

Reward:
- Small cash
- First rep increase
- City attention flag

### Chapter 2 - The City Notices
Act: Unknown Name  
District: City Core  
Purpose: Begin conflict with King Serrano.

Intro beat:
King watches clips of Marc's win and laughs like it means nothing.

Key line:
King Serrano: "These streets don't hand out names. They bury them."

Gameplay:
- Checkpoint sprint
- Aggressive pack race
- Alley run

Garage beat:
Ross explains City Core's hierarchy.

Radio follow-up:
King's runners are asking about the quiet new driver.

Reward:
- King pressure begins
- City Core boss ladder starts

### Chapter 3 - Lockup 13
Act: Unknown Name  
District: City Core  
Purpose: Make the garage the emotional center and deepen the starter car bond.

Intro beat:
Quiet garage after a hard night. Tools, damaged panels, low money.

Key line:
JM Ormido: "It doesn't need to be pretty. It needs to survive tonight."

Gameplay:
- First real upgrade choice
- Repair economy pressure
- Visual identity unlock
- Garage audio preview moment

Garage beat:
First real garage identity scene.

Radio follow-up:
Lockup 13 is small, but people are starting to look toward it.

Reward:
- Level 1 upgrades
- Visual identity system
- Garage preview rev feature

### Chapter 4 - First Blood
Act: Unknown Name  
District: City Core  
Purpose: Marc humiliates one of King's trusted runners.

Intro beat:
Crowded street start. King's runner calls Marc a tourist.

Key line:
Ross: "Let the road answer."

Gameplay:
- High-pressure 1v1
- Escape-style drive after event
- Optional wager side event

Garage beat:
The starter car is repaired like a weapon, not a trophy.

Radio follow-up:
A trusted runner got dropped tonight. King is not laughing now.

Reward:
- Wager races unlock
- King boss chain opens

### Chapter 5 - King Of The Core
Act: Unknown Name  
District: City Core  
Purpose: First district boss and Act I climax.

Intro beat:
King arrives like City Core belongs to him. The crowd parts.

Key line:
King Serrano: "You wanted a name? Come take the weight that comes with it."

Gameplay:
- Multi-stage district boss race
- Traffic-heavy sprint
- City center final straight

Garage beat:
Lockup 13 prepares the car like this race decides the shop's future.

Radio follow-up:
City Core changed hands tonight.

Reward:
- City Core status win
- Arterial Zone access
- Better parts
- New city chatter pool

### Chapter 6 - Better Roads, Worse People
Act: Pressure Of The Climb  
District: Arterial Zone  
Purpose: Introduce bigger money and colder people.

Intro beat:
Wider roads, cleaner events, colder people.

Key line:
Ross: "Upper roads aren't friendlier. They're just better lit."

Gameplay:
- Two arterial races
- First brokered Edd event
- Optional delivery/challenge run

Garage beat:
JM talks about building for speed without losing the car's soul.

Radio follow-up:
New money is moving through Arterial tonight.

Reward:
- Higher stakes economy
- Edd mission chain
- New upgrade tier

### Chapter 7 - Edd's Offer
Act: Pressure Of The Climb  
District: Arterial Zone  
Purpose: Tempt Marc with dangerous advancement.

Intro beat:
Edd offers access to a bigger event with a price hidden under it.

Key line:
Edd Ricapor: "There's money in the city tonight. Question is how much of yourself you're willing to spend getting it."

Gameplay:
- Stake-based race
- Risk/reward choice
- Damage-heavy route

Garage beat:
Ross distrusts the offer. Marc takes it anyway.

Radio follow-up:
Gla1ve bought access. Access always sends a bill.

Reward:
- Higher stakes events
- Stronger repair and wager pressure

### Chapter 8 - Mav Cruz
Act: Pressure Of The Climb  
District: Arterial Zone  
Purpose: Introduce status, polish, and image as a rival force.

Intro beat:
Mav arrives like attention is part of his car's aero.

Key line:
Mav Cruz: "You drive like you've got nothing to lose. That only works until you actually do."

Gameplay:
- Mav qualifier chain
- Invitational race
- Public humiliation event if player loses

Garage beat:
JM frames presentation as pressure, not vanity.

Radio follow-up:
Mav Cruz noticed the new name. That's not the same as respect.

Reward:
- Arterial ladder opens

### Chapter 9 - Collapse
Act: Pressure Of The Climb  
District: Arterial Zone  
Purpose: Marc suffers a costly downfall.

Intro beat:
A bad deal, bad timing, and too much pressure break against Marc.

Key line:
Marc: "How much?"

Gameplay:
- High-risk loss-compatible event
- Heavy repair/debt consequence
- Recovery events unlock

Garage beat:
The garage is quiet. Damage speaks for everyone.

Radio follow-up:
The climb bent tonight. It did not break.

Reward:
- Emotional reset
- Rebuild phase begins

### Chapter 10 - Back From The Concrete
Act: Pressure Of The Climb  
District: Arterial Zone  
Purpose: Rebuild Marc through grind and discipline.

Intro beat:
Tools, cheap parts, tired eyes. James Louis enters without pity.

Key line:
James Louis: "Winning's easy to talk about when you haven't paid for it yet."

Gameplay:
- Recovery races
- Lower-tier redemption events
- Repeatable cash races
- One proof-of-belonging challenge

Garage beat:
JM and Ross rebuild the car around discipline.

Radio follow-up:
Gla1ve is back running small events. That is not retreat. That is repair.

Reward:
- Access restored
- Mav boss path opens

### Chapter 11 - Gold Can Bleed
Act: Pressure Of The Climb  
District: Arterial Zone  
Purpose: Marc defeats Mav and breaks the image wall.

Intro beat:
Prestige event. Mav tries to make Marc look temporary.

Key line:
Marc: "I don't need your room. I need the road."

Gameplay:
- Arterial boss route
- Long sweepers
- Two-part final race

Garage beat:
The garage sees a car that finally looks like Marc's rise.

Radio follow-up:
Gold can bleed. Arterial saw it.

Reward:
- Arterial Zone conquered
- Mountain access
- Higher upgrade tier

### Chapter 12 - Everybody's Watching
Act: The Roads Get Darker  
District: Mountain Fringe  
Purpose: Show Marc's rise becoming city-wide.

Intro beat:
Radio chatter, clips, boards, whispers. Gla1ve is now a city problem.

Key line:
Ross: "You don't need the city to love you. You need it to stop overlooking you."

Gameplay:
- Heat-linked free-roam content
- Rival interrupt events
- Police pressure escalation

Garage beat:
JM warns that attention breaks weak builds.

Radio follow-up:
The whole city is watching now.

Reward:
- Mountain access
- Highway rumor chain
- Named rival heat system

### Chapter 13 - Thomas Cabanit
Act: The Roads Get Darker  
District: Mountain Fringe  
Purpose: Introduce Marc's deepest mirror.

Intro beat:
Fog, elevation, silence. Thomas appears without performance.

Key line:
Thomas Cabanit: "The road doesn't care who you are. That's why I trust it more than people."

Gameplay:
- Mountain trials
- Technical downhill
- Uphill discipline event
- Edge-control challenges

Garage beat:
The garage prepares for roads that punish ego.

Radio follow-up:
Mountain people are quiet about Thomas. That says enough.

Reward:
- Mountain event chain
- Drift/technical mastery content

### Chapter 14 - Home Is Not Safe
Act: The Roads Get Darker  
District: Mountain Fringe  
Purpose: Threaten Lockup 13 and raise stakes beyond racing.

Intro beat:
Lockup 13 feels vulnerable for the first time.

Key line:
Ross: "They didn't come for the car. They came for your peace."

Gameplay:
- Urgent recovery mission
- Protect/relocate asset run
- Timed drive
- Retaliatory challenge

Garage beat:
The garage scene is protective and restrained.

Radio follow-up:
Somebody touched Lockup 13. The city felt that mistake.

Reward:
- Thomas boss chain opens

### Chapter 15 - Edge Of The Mountain
Act: The Roads Get Darker  
District: Mountain Fringe  
Purpose: Marc faces Thomas without losing himself to obsession.

Intro beat:
Night mountain run. No crowd needed. The road is enough.

Key line:
Thomas Cabanit: "Don't chase the edge unless you know what you are willing to leave there."

Gameplay:
- Multi-section mountain boss race
- Fog
- Elevation
- Drop-offs
- Technical rhythm

Garage beat:
The car is quiet before the hardest technical run.

Radio follow-up:
Thomas gave respect. Mountain Fringe gave way.

Reward:
- Mountain Fringe conquered
- Highway Belt opens

### Chapter 16 - Lance D.C.
Act: Crown Of The Night  
District: Highway Belt  
Purpose: Make speed terrifying.

Intro beat:
Late highway. Violent speed. Lance does not speak softly.

Key line:
Lance D.C.: "If you blink at this speed, you don't lose. You disappear."

Gameplay:
- High-speed route events
- Draft battles
- Long pulls
- Police complications

Garage beat:
JM builds for stability because speed punishes lies.

Radio follow-up:
Highway Belt is awake. Lance wants blood.

Reward:
- Upper-tier speed content
- Ray chain proximity

### Chapter 17 - The Upper World
Act: Crown Of The Night  
District: Highway Belt  
Purpose: Introduce Ray and the city power structure.

Intro beat:
Cleaner, colder, controlled. Ray is not just another racer.

Key line:
Ray: "Don't mistake attention for power."

Gameplay:
- Elite invitational events
- Gated race sequence
- Mixed district gauntlets

Garage beat:
James warns Marc about winning without becoming owned.

Radio follow-up:
Ray finally spoke. The city heard the leash in his voice.

Reward:
- Final chain proximity

### Chapter 18 - Last Night In Lockup 13
Act: Crown Of The Night  
District: City Core  
Purpose: Final sacred garage scene.

Intro beat:
Marc, Ross, JM, James. No speech. The car says enough.

Key line:
Marc: "Finish it."

Gameplay:
- Final prep
- Final purchase/upgrade window
- Cosmetic/audio identity lock-in

Garage beat:
The final build locks in. The garage becomes a chapel for speed.

Radio follow-up:
Lockup 13 is quiet tonight. That means something is coming.

Reward:
- Final readiness state

### Chapter 19 - Crown Of The Night
Act: Crown Of The Night  
District: Highway Belt  
Purpose: Final city-spanning showdown.

Intro beat:
The whole city watches a route that crosses every scar Marc earned.

Key line:
Ray: "The city remembers winners. It obeys owners."

Gameplay:
- City Core to Arterial
- Arterial to Mountain
- Mountain exit to Highway Belt
- Maximum tension, traffic, heat, and rival pressure

Garage beat:
The garage has nothing left to add. Only the road remains.

Radio follow-up:
Marc Badua is the man. Gla1ve is the name the city will remember.

Reward:
- Campaign completion
- Postgame reputation state
- Legend-tier events

### Chapter 20 - Impossible To Erase
Act: Epilogue  
District: City Core  
Purpose: Show the city after Marc's rise.

Intro beat:
The city is still dangerous. The hierarchy changed anyway.

Key line:
James Louis: "Permanent doesn't mean safe. It means they can't erase you."

Gameplay:
- Postgame free roam
- Legend-tier races
- Rival rematches
- Outrun world state

Garage beat:
Lockup 13 is no longer a shelter. It is a landmark.

Radio follow-up:
The night did not end. It learned a new name.

Reward:
- Postgame state opens

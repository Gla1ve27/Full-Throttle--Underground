# UG2 Style Car Audio Replication Plan

This package is intentionally asset-graph-first. It does not flatten the
original game audio into a manual pile of clips, and it does not treat `GIN_*`
files as the whole engine sound.

## Local Source Of Truth

The local UG2 install contains:

- `speed2.exe` with an embedded profile table mapping `GIN_*` files to numbered
  `CAR_##_ENG_MB_SPU.abk` and `CAR_##_ENG_MB_EE.abk` banks.
- `SOUND/ENGINE` with 84 `.gin` files and 126 `.abk` files.
- `SOUND/EVT_SYS` with `.csi` event registries.
- `SOUND/MIXMAPS` with mode-specific `.mxb` mix maps.
- `SOUND/FXEDIT` with environmental `.fx` routing/reverb data.
- Accessory banks under `SOUND/TURBO`, `SOUND/SHIFTING`, `SOUND/SKIDS`,
  `SOUND/NOS`, and `SOUND/IG_GLOBAL`.

No local `CarSoundData/` folder was present during reconnaissance.

## Preserved Engine Mapping Shape

Each engine profile preserves:

- profile number from the numbered bank pair
- accel `GIN_*` reference
- best-effort decel `GIN_*_DCL` or `*_Decel` reference
- `CAR_##_ENG_MB_SPU.abk`
- `CAR_##_ENG_MB_EE.abk`
- `SWTN_CAR_##_MB.abk`
- event names discovered in `.abk` and `.csi` files
- original absolute and relative source paths

Example:

```text
Profile 18
GIN_Nissan_240SX.gin
CAR_18_ENG_MB_SPU.abk
CAR_18_ENG_MB_EE.abk
SWTN_CAR_18_MB.abk
```

## Unity Import Pipeline

Use `Tools/UG2 Audio/Import Metadata From UG2 Root` inside Unity, then select the
local UG2 root folder. The importer creates generated assets under:

```text
Assets/UG2Audio/Generated
```

Generated assets include:

- `UG2StyleEventRegistry`
- one `UG2StyleEnginePackage` per discovered profile
- one `UG2StyleCarAudioProfile` per discovered profile
- one `UG2StyleProfileDebugReport` per imported profile
- shared shift, turbo, sweetener, and skid package assets
- `UG2AudioImportReport.txt`

The first pass preserves structure and metadata. Proprietary payload decoding is
isolated behind `IUG2AudioDecoder`, so decoded `AudioClip` generation can improve
without changing the asset graph.

For the first vertical slice, use:

```text
Tools/UG2 Audio/Import Profile 18 Vertical Slice
```

This imports only profile `18`, decodes that profile's accel/decel GIN files when
`vgmstream-cli` is available, assigns the decoded `AudioClip` refs to the engine
package, and writes a profile debug report showing:

- profile number
- accel and decel GIN refs
- SPU and EE bank refs
- sweetener ref
- shift, turbo, and skid candidates
- resolved decoded clip refs when available
- warnings or decoder diagnostics

`ABK` handling remains split into two passes:

- Pass A: metadata, cue, event, and relationship parsing.
- Pass B: sample extraction and playable clip output.

## Runtime Mixer Shape

`UG2StyleCarAudioController` consumes a `UG2StyleCarAudioProfile` and keeps
separate runtime layers:

- engine body from `CAR_##_ENG_MB_*`
- accel character from `GIN_*`
- decel character from `GIN_*_DCL` or `*_Decel`
- sweetener/sputter from `SWTN_CAR_##_MB`
- shift from `GEAR_*`
- turbo from `TURBO_*`
- skid from `SKID*` and `SKIDS_DRIFT*`
- road/wind from `ROADNOISE_00_MB`, `WIND_00_MB`, and `WIND_01_MB`
- interior/exterior `AudioMixerGroup` routing

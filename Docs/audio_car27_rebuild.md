# Car_27 Vehicle Audio Rebuild

## Primary Identity

`CAR_27_ENG_MB_EE` is the exterior engine bank. `CAR_27_ENG_MB_SPU` mirrors the same numbered loop structure and is used only as the interior/cabin bank. They are not stacked together, because stacking matching EE/SPU clips would make combing and phase clutter more likely than a cleaner engine identity.

## Car_27 Engine Audit

| File | Runtime role | Decision |
| --- | --- | --- |
| `CAR_27_ENG_MB_EE_01.wav` / `CAR_27_ENG_MB_SPU_01.wav` | Idle, reverse low-pitch fallback, engine band 01 | Used |
| `CAR_27_ENG_MB_EE_02.wav` / `CAR_27_ENG_MB_SPU_02.wav` | Low on-throttle, low off-throttle, engine band 02 | Used |
| `CAR_27_ENG_MB_EE_03.wav` / `CAR_27_ENG_MB_SPU_03.wav` | Mid on-throttle, mid off-throttle, engine band 03 | Used |
| `CAR_27_ENG_MB_EE_04.wav` / `CAR_27_ENG_MB_SPU_04.wav` | High on-throttle, high off-throttle, engine band 04 | Used with lower pitch exposure |
| `CAR_27_ENG_MB_EE_05.wav` / `CAR_27_ENG_MB_SPU_05.wav` | Higher reference band | Kept in `engineBands`; rejected as a dominant loop to avoid extra overlap |
| `CAR_27_ENG_MB_EE_06.wav` / `CAR_27_ENG_MB_SPU_06.wav` | Higher reference band | Kept in `engineBands`; rejected as a dominant loop to avoid robotic high-RPM stacking |
| `CAR_27_ENG_MB_EE_07.wav` / `CAR_27_ENG_MB_SPU_07.wav` | Pre-redline reference band | Kept in `engineBands`; rejected as primary top layer because it is too broad for the gated redline role |
| `CAR_27_ENG_MB_EE_08.wav` / `CAR_27_ENG_MB_SPU_08.wav` | Top/redline | Used only near redline, same gear, not shifting |

## SWTN_CAR_27_MB Audit

All `SWTN_CAR_27_MB` files are short one-shots, not continuous loops. They are used as transient support only.

| File | Decision |
| --- | --- |
| `SWTN_CAR_27_MB_01.wav` | Rejected for now; short utility hit, no clear continuous role |
| `SWTN_CAR_27_MB_02.wav` | Used as restrained lift-off crackle/exhaust pop |
| `SWTN_CAR_27_MB_03.wav` | Rejected for now; overlaps with gear shift role |
| `SWTN_CAR_27_MB_04.wav` | Used as throttle blip / switch support |
| `SWTN_CAR_27_MB_05.wav` | Rejected for now; short utility hit |
| `SWTN_CAR_27_MB_06.wav` | Rejected; too short to loop safely |
| `SWTN_CAR_27_MB_07.wav` | Used as rare mechanical chatter |
| `SWTN_CAR_27_MB_08.wav` | Rejected for now; short utility hit |
| `SWTN_CAR_27_MB_09.wav` | Rejected for now; short utility hit |
| `SWTN_CAR_27_MB_10.wav` | Rejected for now; short utility hit |

## Support Library Choices

| Library | Choice |
| --- | --- |
| Turbo | `TURBO_MED_1_MB_01` spool, `_02` whistle, `_03` blow-off. Medium turbo fits Car_27 better than large/truck-heavy sets or very small thin sets. |
| Gear/mechanical | `GEAR_MED_Base_01/_02` for shift support and `GEAR_MED_Lev2_01` as quiet drivetrain whine. Large gear sets were rejected as too heavy; small/TK sets were rejected as too light or truck-flavored. |
| Skid | `SKIDS_DRIFT2_MB_03` as the main skid layer. Pavement skid sets remain available but are not assigned as constant rolling sound. |
| GIN donors | None assigned. They remain reference/donor material only. |

## Runtime Tuning Direction

The rebuild favors continuity over loud stacking:

- 3 dominant engine loops.
- Wider RPM overlap with explicit 02->03 and 03->04 dissonance protection.
- Lower high pitch clamp exposure.
- Slower RPM rise and pitch smoothing.
- Light shift duck and short selection freeze.
- Top loop is gated by high throttle, high RPM, stable gear, and no active shift.
- Skid layer is speed/slip gated and muted during normal hard launches.

## Setup

Run `Full Throttle/Audio/Rebuild Exact Car_27 Audio Bank` after Unity recompiles. The full-game prefab builder also uses this same Car_27 bank, so future rebuilds should not return to the old CAR_00/GIN starter mapping.

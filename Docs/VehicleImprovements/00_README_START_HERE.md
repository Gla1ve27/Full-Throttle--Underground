# Full Throttle Vehicle System — Split Implementation Pack

This pack breaks the original vehicle system plan into smaller execution chunks so Claude Opus / Antigravity can work with less context overload.

## Recommended order
1. Part 1 — Architecture & rules
2. Part 2 — Core data model
3. Part 3 — Runtime vehicle physics
4. Part 4 — Vehicle lights system
5. Part 5 — Visual kits & upgrades
6. Part 6 — Underground modular car creator
7. Part 7 — Integration, testing, and rollout

## How to use this with Opus / Antigravity
- Send only **one part at a time**
- Ask it to **implement only that part**
- After each part is done, test in Unity before moving on
- Do not ask for full-game rebuild in the same prompt
- Keep each conversation focused on one subsystem

## Best prompt pattern
Use this structure:

```text
Read this implementation part and execute only what is defined inside it.
Do not redesign the whole architecture.
Do not touch unrelated systems.
Preserve compatibility with prior parts.
Output:
1. changed files
2. new files
3. summary of what was implemented
4. any editor setup steps
```

## Suggested execution sequence
- First finish Part 1 and Part 2
- Then Part 3
- Then Part 4
- Then Part 5
- Then Part 6
- Finally Part 7

## Notes
This split is designed to reduce agent crashes, context bloat, and implementation drift.

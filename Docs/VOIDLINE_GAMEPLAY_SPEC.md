# VOIDLINE — Gameplay Specification

**Status:** playable prototype; tuning values are provisional until external playtests.

## Game identity
- **Genre:** 3D sci-fi survival runner / obstacle dodger.
- **View:** fixed perspective camera behind the craft, down an octagonal space corridor.
- **Player goal:** survive as long as possible, avoid debris, collect repair cells, and beat the saved best score.
- **Design pillars:** readable hazards, responsive 2-axis movement, short repeatable runs, minimal UI, clear damage feedback.

## Core loop
1. Move through the corridor using two-axis movement.
2. Read and dodge incoming asteroids.
3. Earn survival and pass-through score.
4. Collect repair cells to restore hull and earn a bonus.
5. Recover from mistakes, or end the run when hull reaches zero.
6. Restart immediately and try to beat the persistent best score.

## Controls
| Action | Input |
|---|---|
| Move | WASD or arrow keys; Player/Move action |
| Pause / resume | Escape |
| Restart after run end | R or Enter |
| Overlay button | Resume while paused; restart after run end |

The input actions are in Assets/InputSystem_Actions.inputactions. Prefer adding bindings there rather than adding more hard-coded keyboard polling.

## Rules and provisional tuning
- Player remains at world Z = 0 and moves within X = -3.35…3.35 and Y = -2.25…2.25.
- Starting hull: 3.
- A hazard collision removes one hull point, subject to a short damage cooldown.
- Hull at zero ends the run.
- Survival score: 10 points per elapsed second.
- Passing an asteroid: +25 points.
- Repair cell: +100 points and restores one hull point, capped at starting hull. Collecting one at full hull still grants the score bonus.
- First repair cell appears after 4.5 seconds; subsequent cells use the run-settings interval (default 16 seconds).
- Hazard spawn pacing starts at 1.45 seconds and ramps toward a 0.7-second floor.
- World speed defaults to 12 units/second.
- Best score is persisted under the VOIDLINE_BEST_SCORE PlayerPrefs key.

All numbers above are prototype values, not validated balance claims. Tune them in Assets/VOIDLINE/Settings/VoidlineRunSettings.asset, then playtest before treating them as final.

## Route-generation status
The active game uses one continuous seeded route spine with paced hazard formations. The multi-route graph was rejected and archived outside `Assets`; the serialized scene temporarily retains an inert `VoidlineRouteBranchController` shim because Unity Editor main-thread commands have timed out. The shim now creates no objects and makes no route choices; remove that component through the Editor API when it recovers. Do not add parallel corridors, generated route rails or branch-specific rewards. The opening corridor is a one-time intro and must never recycle back into view. See `Docs/VOIDLINE_IMPLEMENTATION_ROADMAP.md` for the current single-route plan.

## Feedback
- Damage: short synthesized impact sound, red screen flash, decaying camera shake.
- Asteroid pass: short rising confirmation tone.
- Repair cell: rising pickup tone and small camera response.
- Run end: low falling tone, final score, and best score.
- HUD shows hull, current score, best score, repair cells collected, and speed.

## Acceptance criteria
- Movement feels predictable and stops at the defined bounds.
- Asteroids are visible early enough to react to and use simple trigger colliders.
- A hit reduces hull once, not repeatedly during one overlap.
- Repair cells can be collected, are pooled after collection or passing the player, and never damage the player.
- Pause freezes gameplay and Escape resumes it.
- Restart resets the run without resetting the saved best score.
- The run can be played repeatedly without growing the number of active pooled hazards/collectibles without bound.
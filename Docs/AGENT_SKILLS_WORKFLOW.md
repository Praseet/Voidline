# Agent Skill Audit and Application

This is a selective, project-specific application of the installed skills, not a blind execution of every unrelated global skill.

## Catalog inspection
- The skills lockfile records 73 entries from gamedev-skills/awesome-gamedev-agent-skills.
- The skills lockfile records 48 entries from poorvith-mp/skills-gamedev.
- The lockfile does not attribute any entries to n2g7/agent-skills. The global Claude skills directory contains additional skill documents, including relevant 3D-game, PC-game, audio, testing, and Unity build guidance; the entire global directory is not assumed to belong to that one repository.
- Selected skills were read where they materially affected the project. Irrelevant framework-specific skills were not loaded into the game workflow.

## Skills applied
| Skill | Project decision / implementation |
|---|---|
| game-design | Documented the core loop, control scheme, scoring, failure states, provisional tunables, and acceptance criteria. |
| game-feel | Added brief damage feedback using synthesized sound, a decaying screen flash, and camera-only trauma shake. |
| game-ui-ux | Kept a reference-resolution CanvasScaler, anchored HUD elements, and a visible control hint; recorded generated-UI extraction as future debt. |
| input-systems / unity-input-system | Added Pause and Restart actions to the existing Player action map and moved movement to the existing Move action. |
| unity-scriptableobjects | Kept run speed, hull, spawn pacing, lane bounds, pickup cadence, and pickup reward in a designer-facing ScriptableObject. |
| 3d-games / unity-physics | Kept visual meshes separate from simple trigger colliders; used a kinematic Rigidbody on reusable moving actors. |
| create-game-assets | Inspected existing Kenney models before creating new visuals, normalized URP material slots, and recorded asset provenance/license. |
| performance-optimization | Reused asteroid and pickup actors through pools; used a measured editor sample rather than claiming unmeasured optimizations. |
| audio-design | Added event-specific one-shot SFX; recorded AudioMixer buses and user volume controls as a later audio pass. |
| playtesting | Separated observed runtime behavior from checks still requiring a controlled collision/playtest. |
| unity-build-pipeline | Build validation must inspect the BuildReport and launch the resulting executable; Editor Play Mode alone is not release QA. |
| save-systems | Used PlayerPrefs only for a single local best-score integer; a versioned save system is unnecessary for this prototype stage. |

## Guardrails
- The game is still a prototype; all balance numbers remain provisional until external playtests.
- Do not add multiplayer, monetization, complex progression, or other unrelated subsystems before the core run is reliable.
- Prefer focused components and shared settings over growing the game manager with unrelated responsibilities.
- Never mark a test passed merely because compilation succeeded.

## 2026-10-12 — Expanded local skill review
This pass read and applied the locally installed, project-relevant game skills from `C:\Users\HP\.agents\skills`, `C:\Users\HP\.claude\skills`, and project-local `.codex/skills/unity-mcp-workflow`. The install is broad, so unrelated Godot, web, RPG, multiplayer, and storefront skills were not applied to this Unity arcade runner.

| Reviewed skill family | How it shapes VOIDLINE |
|---|---|
| `game-design`, `game-designer`, `level-design` | Keep the 30-second survival loop small; give the existing transit gate a gameplay-readable sector-crossing role rather than adding unrelated features. |
| `3d-games`, `blender-modeling`, `blender-animation`, `create-game-assets`, `tech-art` | Preserve strong silhouette and value hierarchy; use low-cost bespoke set pieces, normalized transforms, clean imports, and simple collision proxies. Do not accept generated/beauty imagery without in-game-scale QA. |
| `game-feel`, `camera-systems`, `unity-animation`, `physics-tuning` | Layer short transient feedback; shake only the camera; keep the player collider upright while the ship's visual rolls/banks; test recovery and dodge timing. A skeletal Animator is unnecessary for simple transform-driven craft motion unless the animation set grows. |
| `unity-csharp-scripting`, `unity-architecture`, `unity-scriptableobjects` | Keep the manager as orchestrator, delegate gate detection to the environment system, keep run tunables in `VoidlineRunSettings`, and reset per-run state deliberately. |
| `unity-input-system`, `input-systems` | Keep discrete actions and movement on the existing Input Actions; avoid duplicate keyboard polling when a bound action exists; verify real overlay focus with keyboard and gamepad. |
| `unity-physics` | Maintain simple trigger shapes with a Rigidbody on a participant; resolve each actor once and return it to the pool. Code review is not a substitute for an observed hit/pickup test. |
| `game-audio`, `audio-design`, `sound-effects` | Preserve distinct event-specific synthesized SFX; keep impacts and pickups prominent; defer mixer buses and external generated samples until the run is reliable and licensed assets are tracked. |
| `game-ui-ux` | Keep edge-anchored responsive HUD, a clear state label, explicit initial menu focus, and mouse + keyboard/gamepad usability. |
| `game-performance`, `performance-optimization` | Optimize only measured bottlenecks; use the previous Editor performance sample as context only, never as a standalone benchmark. Audit allocations, render work, and pooling when the live Editor responds. |
| `playtesting`, `unity-build-pipeline`, `game-build-pipeline` | Separate static/code checks, Editor observation, successful build report, and standalone launch. Keep unverified tests open rather than promoting code paths to passes. |
| `unity-tooling`, `unity-mcp-workflow`, `unity-cli` | Batch independent reads/edits, use Editor APIs for Unity-owned serialized assets, and stop on repeated main-thread timeouts instead of retrying, spawning another Editor, or hand-editing scene/prefab/asset YAML. |

The canonical execution order and current phase are in `Docs/VOIDLINE_IMPLEMENTATION_ROADMAP.md`. The dated `VOIDLINE_DESIGN_DIRECTION.md` is archived at `D:\dump\VOIDLINE_Backups\2026-10-12_route_graph_phase1\Docs\VOIDLINE_DESIGN_DIRECTION.md`. The later route-graph/fork experiment was explicitly de-scoped and its source archived at `D:\dump\VOIDLINE_Backups\2026-10-12_remove_branch_system`; do not treat branching as pending work.


## 2026-10-12 ? Route-architecture implementation note

- Used Unity C# scripting, game-design, game-feel, level-design, physics/controls, camera-systems, game-performance and Unity tooling principles.
- Decision: keep the route and encounter directors separate. VoidlineRouteDirector owns the seeded spline and track frame; VoidlineEncounterDirector owns validated short-horizon formations and recovery pacing; VoidlineGameManager owns run state/scoring.
- Rule carried into code: variations must preserve a clear lane and cannot schedule peak patterns consecutively; low-hull runs favor readable singles. The route seed is available for reproduction.
- Saved scene wiring through Unity Editor API; no scene or settings YAML was hand-edited.

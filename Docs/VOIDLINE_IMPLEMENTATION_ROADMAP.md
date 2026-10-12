# VOIDLINE — Implementation Roadmap

**Updated:** 2026-10-13  
**Canonical plan:** this file  
**Engine:** Unity 6 / URP / Windows x64  
**Current focus:** Phase 3 — deep-space composition, longer object approach, and score/structure-driven sector transitions. Keep encounter and collision tuning in step with the environment; creature work follows after sector readability is established.

## North star

VOIDLINE is a fast, cinematic 2.5D survival-flight game: steer a fragile courier craft through exposed space, asteroid fields and monumental ancient transit relics. The run is about reading a line through hazards, banking/rolling, managing boost, risking close passes, collecting repairs, and surviving increasingly expressive encounters.

**There is one continuous route. No multiple routes, forks, parallel corridors, branch-reward systems, branch cue objects, or route-selection prompts.** Meaningful decisions come from the flight line the player takes through the same world, including whether to boost, roll, take a near miss, or thread a difficult opening.

## Visual rules

- Space must read as space: dark starfield/panorama, sparse distant scale cues, believable asteroid silhouettes and large negative space.
- The starting corridor is only an opening beat. It must retire permanently behind the camera and must never wrap or reappear.
- No generated beam lattices, unexplained black/metallic hoops, parallel tunnel scaffolding, neon guide rails or repeated structural frames just to decorate the view.
- Every environment object must be identifiable at gameplay scale, serve a clear gameplay/readability purpose, or be removed.
- Prefer existing imported assets and correct materials over primitive placeholder shapes. No smooth black oval debris clutter.
- Use the darkened SpaceSkies Free Purple cubemap for the local working environment. If its licensed source pack is absent in a clean checkout, use the project-owned procedural star/nebula panorama fallback; do not include Asset Store source textures in Git. The lighter Space_AnotherPlanet backdrop was rejected.
- No external music files were found in the project asset scan. The current adaptive score is a procedurally generated fallback, layered under event SFX; do not claim an imported soundtrack exists unless an actual audio asset is located and assigned.
- **Superseded:** the Space Threat (Free Action Music) pack is now imported and wired into `VoidlineAdaptiveMusic`. See the Current baseline section and `Docs/QA_CHECKLIST.md`; treat this line as historical.

## Flight and encounter rules

- Player, hazards, pickups, scrolling geometry and landmarks share the seeded route frame.
- Preserve damped two-axis steering, visible bank/roll, boost resource management and a stable upright collision root.
- Use singles, offset pairs, cross-weaves and vertical weaves with telegraphed safe lanes. Do not solve difficulty merely by adding more hazards.
- Include recovery beats after peak formations, on low hull and after a damaging collision.
- Impacts must have a single clear gameplay consequence and bounded recoil/flash/audio/camera response; a collision must not also be rewarded as a near miss.
- Keep score, gate/arch milestones, repair cells, clean passes, rolls and flow chain as the prototype reward set. Do not add shops, currency, meta-progression or combat until the core flight loop is reliable.

## Phases and acceptance gates

### Phase 0 — Baseline, documentation and safe workflow
- Keep this roadmap canonical; Creative Bible for intent, Technical Architecture for code ownership, Gameplay Spec for gameplay rules, Asset Manifest for provenance, Research Playbook for evidence/rationale and QA Checklist for observed results.
- Apply relevant locally installed skills (level design, game feel, Unity C#, physics, camera, Blender, art integration, audio and performance) only where they improve this game.
- Batch independent edits; avoid repeated scene reload/domain reload. Back up before touching serialized scenes or removing assets.
**Gate:** current state and unresolved checks are truthful and easy to locate.

### Phase 1 — Seeded single-route foundation (implemented)
- Keep the seeded Catmull-Rom route spine, route-local player/camera placement, pooled hazards/pickups, encounter pacing, passage scoring and intro segment retirement.
- Keep the EnsureControlPoint cache-recovery fix.
**Gate:** same-seed route reproducibility and route-frame alignment remain stable in a fresh Editor run.

### Phase 2 — Remove route-fork prototype and establish the actual space look (implemented; see Phase 3 checks)
- Remove all functional multi-route behavior, branch offsets, branch rewards, route-choice callouts, generated motes and added oval debris.
- Preserve a temporary no-op compatibility component only if needed to avoid a missing scene script while the Unity Editor pipeline is unresponsive; remove its scene component with the Editor API when the pipeline becomes available.
- Keep SpaceSkies Free Purple as the preferred local sky through the project-owned M_VOIDLINE_DarkSpace resource; keep a self-contained procedural panorama fallback so public checkouts do not depend on licensed Asset Store textures. The active scene must not directly serialize a reference to an excluded source asset.
- Keep the intro tunnel hidden in open space and retire its segments behind the camera. It must never recycle into view.
- Keep real gates/arches as occasional large, traversable landmarks; avoid additional beam structures.
- Keep the adaptive score layered by ambient exploration, boost, pressure/critical hull and pause/game-over state. Existing impact/pickup/roll sounds remain one-shot effects, not a substitute for music.
**Gate:** no fork logic or cue objects remain active; the dark asset is loaded by the main game; the opening corridor never returns; a fresh compile and Editor run show no new errors.

### Phase 3 — Space environment and asset pass (active)
- Review the scene through the real game camera, not only the Scene view.
- Compose a dark near-black/navy starfield with one or two giant scale cues, sparse debris silhouettes and deliberate negative space around the player/hazards.
- Use imported Kenney MoonRock/Meteor/Crystal models and existing Ancient Transit Gate/Hollow Asteroid Arch assets where appropriate; do not keep smooth placeholder ovals if the silhouette is wrong.
- Use Blender for only a small number of bespoke hero/landmark assets when imported content cannot deliver the desired silhouette.
- Tune sky, ambient light, exposure and post-processing together; avoid bright fog that flattens the starfield.
**Gate:** captures at startup, after intro retirement and at the next landmark read as deep space; no repeated tunnel, black blob clutter or unexplained geometry.

### Phase 4 — Encounter grammar and pacing
- Keep authored/seeded encounter patterns with safe-lane metadata and pattern history.
- Sample seeds at normal and boost speed; verify no unavoidable overlap, blocked pickup or three identical major patterns in succession.
- Adapt pressure to elapsed time, current hull and recent damage; create recovery beats after peaks.
**Gate:** the run gets harder through decisions/combination, not sheer object spam.

### Phase 5 — Collision and traversal feel
- Verify collision cooldowns, hull loss, near-miss exclusion, directional response and pooling/reset ownership.
- Tune speed loss, brief VFX/audio/camera cues and recovery windows with controlled hits/passes/pickups.
**Gate:** each event fires exactly once; players retain control after a glancing hit; pools and restart reset cleanly.

### Phase 6 — Creature: Clinger and scrape-off
- Add a distant Witness cue only after the basic space/landmark composition works.
- Prototype a Clinger attached to a ship anchor with bounded drag/roll bias, clear telegraphing and a visible removal goal.
- Use an existing forgiving asteroid/scrape gap to remove it in stages; a miss should not cause an unavoidable death chain.
**Gate:** attachment, scrape-off, death, pause, pooling and restart all work; no essential HUD/hazard cues are obscured.

### Phase 7 — Camera, art, audio and accessibility polish
- Compose camera anticipation, speed response, landmark framing and brief bounded impact feedback.
- Integrate music/SFX so transitions are intentional: calm exploration, boosted movement, pressure, critical hull, landmark reveal and game over.
- Add reduced camera shake/flash intensity and verify 16:9 plus ultrawide HUD fit.
**Gate:** actual gameplay captures and audio state checks support any completion claims.

### Phase 8 — Reliability, performance and release
- Test deterministic seeds, prolonged route progression, high speed/low hull, pause/restart/game-over, all rewards and pooling.
- Measure real CPU/GPU timings, allocation/memory behavior and rendering load; do not invent performance claims.
- Build a fresh Windows x64 player; check output timestamps, BuildReport/logs and standalone behavior separately from Editor behavior.
**Gate:** no fresh Console exceptions, no reappearing tunnel or visual junk, all gameplay tests pass, and a new standalone build is independently verified.

## Current baseline and verification constraints

- Existing: seeded spline, route-local ship/camera, starter corridor retirement, space landmarks, encounter pacing, asteroid/repair pooling, boost/roll, passage scoring and near-miss/flow feedback.
- Experimental branching was rejected and is being removed. No multiple routes are part of the design.
- The local development tree uses the SpaceSkies Free Purple cubemap through M_VOIDLINE_DarkSpace (kept local-only); a clean checkout falls back to a procedural dark panorama. The active main scene does not directly reference the excluded licensed asset.
- The authored Blender courier (`Resources/VOIDLINE_Courier.fbx`, 10,084 tris) is the player ship and is confirmed rendering in Play Mode at scale 0.42. It previously never appeared because a serialized Kenney placeholder short-circuited the build path; see `Docs/QA_CHECKLIST.md` for the fix and the measured evidence. Bespoke creature encounters remain pending.
- The smooth black oval cue objects were generated by the experimental branch controller; that controller has been replaced by a no-op compatibility shim pending Editor API removal of its serialized component. Its visual-generation code is no longer present.
- The soundtrack is the imported **Space Threat (Free Action Music)** pack. `VoidlineAdaptiveMusic`
  crossfades three authored "Looped" stems from `Assets/Resources`: Slow Ambient (132s) as the cruise
  bed, Electronic Dramatic (81.2s) for tension and the critical-hull layer, and Energetic Powerful
  (66s) for boost. The looped variants are used for their authored loop points; the non-looped masters
  are intentionally not loaded. Verified in Play Mode: all three load at 44.1 kHz stereo, loop, play 2D,
  and respond to boost/hull/pause/game-over. The earlier procedural four-layer synth has been removed.
- Unity CLI previously connected to the correct Editor but then recompile, run_script and Editor main-thread queries timed out at 30 seconds. Until a fresh compile/Play Mode probe succeeds, current source edits remain unverified. The existing Windows player executable is stale and not release-verified.
- Do not delete user project modifications or reset the repository. Check git status before destructive work, and do not commit/push unless requested.

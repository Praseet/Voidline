# VOIDLINE Research Playbook

**Updated:** 2026-10-12  
**Scope:** Source-backed design and engineering decisions for the Unity 6.6 / URP Windows prototype. Revisit before major changes; these are guidelines, not claims that every technique is always optimal.

## Product north star

VOIDLINE is a short-session 3D sci-fi corridor survival runner. The craft stays near the camera/player plane while an octagonal corridor scrolls past. The player steers on two axes, dodges hazards, collects repair cells, survives as long as possible, and improves a score. The broader original pitch also included checkpoints, a timer, optional shooting, and a space/planet backdrop.

Target: authored low-poly industrial sci-fi; readable movement and hazard silhouettes; restrained cyan/teal emissive accents; no gratuitous bloom, generic AI iconography, or feature creep. Graphics concepts should be naturally visible: 3D coordinates, camera/view transform, animation, lighting/materials, collision, triggers, and the frame loop.

## Research-backed principles

### 1. Game feel is a system, not a particle effect
- Pair important events with a few consistent cues: motion, sound, visual response, and state change.
- A hit communicates contact, damage, and recovery. A repair pickup has a distinct silhouette, collect response, sound, and HUD update.
- Use restrained camera trauma, short screen flash, and one-shot audio. Each effect must communicate an event, not merely decorate the screen.
- Keep cues readable at gameplay speed and test at normal window resolution.
- References:
  - Game Developer, “The single most useful advice I can give for making any game better: feedback” — https://www.gamedeveloper.com/design/the-single-most-useful-advice-i-can-give-for-making-any-game-better-feedback
  - Game Developer, “Game Feel: The Secret Ingredient” — https://www.gamedeveloper.com/design/game-feel-the-secret-ingredient
  - GDC Vault, Lee Perry / Epic Games, “Prototyping Based Design: A Better, Faster Way to Design Your Game” — https://www.gdcvault.com/play/1012473/Prototyping-Based-Design-A-Better
- Application: make hit, pass/near-miss, pickup, pause, and run-end states unmistakable. Do not add particles without a readability purpose.

### 2. Speed and rhythm must serve legibility
- A fast runner needs early, legible silhouettes and stable screen-space cues; difficulty should come from decisions and timing, not hazards that appear too late or blend into the corridor.
- Use perspective and lighting to guide attention. Restrained focal contrast is more useful than indiscriminate bloom or motion blur.
- Thumper is a reference for coordinating camera, contrast, speed cues, and readability.
- Reference: Game Developer, “How Thumper uses striking visuals to complement gameplay at every turn” — https://www.gamedeveloper.com/design/how-i-thumper-i-uses-striking-visuals-to-complement-gameplay-at-every-turn
- Application: keep the central travel path readable, vary hazard timing/lanes deliberately, and validate reaction time at real speed. Any FOV/blur change must be tested for readability and comfort.

### 3. Profile first; optimize the actual bottleneck
- Measure CPU/GPU frame times, render-thread cost, draw calls/batches, overdraw, allocations, and memory before broad quality changes.
- URP guidance recommends reducing unnecessary rendering work, render passes, texture/buffer costs, real-time shadow cost, and draw calls according to the measured bottleneck.
- In Unity 6 URP, SRP Batcher and GPU Resident Drawer are preferred for suitable renderers. Do not blindly enable every batching mode: compatibility and priority matter, and dynamic batching is obsolete in Unity 6.6.
- Use Frame Debugger/Rendering Statistics to establish draw-call causes and Profiler to separate CPU-bound from GPU-bound behavior.
- References:
  - Unity 6.6 URP performance overview — https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling/in-urp/understand-performance
  - Unity draw-call optimization selection — https://docs.unity.com/en-us/engine/6000.5/manual/analysis/graphics-performance-profiling/reduce-draw-calls/optimizing-draw-calls-choose-method
  - Unity 6.6 profiler modules — https://docs.unity.com/en-us/engine/6000.6/manual/analysis/profiler/visualizing-data
- Application: editor FPS is not a release benchmark. Capture repeatable Editor and standalone build results. Prefer shared materials, modest mesh complexity, limited real-time lights/shadows, sensible camera far clipping, and pooling. Adopt GPU Resident Drawer/instancing only if compatible and measurably beneficial.

### 4. Pool recurring actors and bound their lifetime
- Repeated creation/destruction adds CPU and garbage-collection pressure. Reuse hazards/pickups with explicit get/release reset logic and a maximum pool size.
- Reset transform, state, collider/trigger, effects, timers, and per-run references. Prevent double-release and stale state.
- References:
  - Unity 6.6, “Pooling and reusing objects” — https://docs.unity.com/en-us/engine/6000.6/manual/analysis/performance-memory/managed-memory/optimizing-code-managed-memory/reusable-code
  - Unity 6.6 ObjectPool — https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/pool/objectpool1
- Application: verify asteroid and repair-cell pool counts stay bounded across repeated runs, with deterministic run resets.

### 5. Use input actions as the source of truth
- Centralize keyboard/gamepad bindings in the Input Actions asset; use action callbacks for discrete events and a Vector2 action for movement. Avoid competing polling paths that make pause/restart unreliable.
- References:
  - Unity Input System action callbacks — https://github.com/Unity-Technologies/InputSystem/blob/develop/Packages/com.unity.inputsystem/Documentation~/set-callbacks-on-actions.md
  - Unity PlayerInput — https://docs.unity.cn/Packages/com.unity.inputsystem%401.19/manual/PlayerInput.html
- Application: validate controls in a focused Game View and standalone build. Successful simulated key injection alone does not prove pause/resume worked.

### 6. Blender-to-Unity asset pipeline
- Model for the gameplay camera and silhouette, not just a beauty render. Apply transforms, keep topology economical, separate visual mesh from simple gameplay colliders, and avoid unnecessary material slots.
- Use binary glTF (.glb) as a portable interchange option when it preserves required mesh/material data; validate the imported result in URP. FBX remains viable where the project workflow requires it.
- Blender glTF export supports meshes, Principled BSDF materials, textures, cameras, lights, and animation. Embedded Base64 glTF is the least efficient variant; avoid it unless needed.
- Reference: Blender 5.2 Manual, glTF 2.0 — https://docs.blender.org/manual/en/latest/addons/scene_gltf2.html
- Application: create bespoke hero/landmark assets only when they materially improve the game. Keep silhouettes clean, material count low, texture sizes proportionate, and exported mesh collision-free; add simple Unity colliders separately.

## Reference games / visual studies
- **Thumper** — focused visual hierarchy, controlled contrast, speed communicated through framing and rhythm.
- **Arcade runners / tunnel racers** — study hazard telegraphing, timing, lane decisions, and restart time; do not copy proprietary art.
- **Classic space-flight games** — study ship silhouette against environment and readable navigation landmarks.
- **Kenney Space Kit** — practical low-poly source already in the project. Existing third-party models are preferable to needless duplicate custom models; custom work should fill a real visual/gameplay gap.

## Engineering guardrails
1. Keep the current Unity 6.6 / URP baseline unless a measured requirement justifies changing it.
2. Do not upgrade editor versions or major packages just because newer versions exist.
3. Do not add multiplayer, monetization, inventory, elaborate progression, or network services before the core loop is robust.
4. Make changes testable/reversible; back up before risky scene/script changes.
5. Do not modify imported third-party source assets. Put project-owned assets under Assets/VOIDLINE.
6. Compilation is not proof of functionality. Run the game, inspect Console, capture the actual view, and test state transitions.
7. Test the standalone Windows build; Editor Play Mode can hide focus, input, frame pacing, and packaging issues.
8. Record measurements and unresolved tests honestly. Never claim a performance win without before/after data.
9. Keep docs updated when controls, gameplay rules, assets, or QA results change.
10. Preserve existing uncommitted work; inspect Git status before any Git operation.

## Prioritized roadmap

### P0 — reliable playable loop
- Resolve manager/lifecycle behavior under current Enter Play Mode settings.
- Verify movement, pause, resume, restart, collision damage, repair-cell collection, scoring, and best-score persistence.
- Verify pool reset/bounds, segment recycling, and no recurring Console errors.
- Build and launch Windows x64; repeat core checks outside Editor.

### P1 — visual identity
- Improve corridor depth/readability with authored landmarks and restrained material/emissive variation.
- Add a consistent star/planet backdrop and depth cues without high-cost geometry.
- Improve ship silhouette/engine response only where visible from the actual camera.
- Evaluate bespoke Blender assets against existing Kenney assets before adding them.

### P2 — gameplay depth after stability
- Add checkpoint/sector gates as meaningful gameplay events, not decorative rings alone.
- Add a timer/sector split or survival milestone.
- Consider optional shooting only if it creates a real decision loop; it is not required for the core runner.

### P3 — measured polish and release QA
- Profile Editor and standalone builds at target resolution.
- Inspect render passes, material count, draw calls, overdraw, allocations, and memory.
- Verify multiple aspect ratios and HUD anchors.
- Capture gameplay and update README/screenshots only after the experience is genuinely playable.

## Tooling status (2026-10-12)
- Unity CLI: 1.0.0-beta.13; self-update check reported this is the latest version.
- Added official Unity Pipeline package com.unity.pipeline version 0.8.0-exp.1 to Packages/manifest.json for live Editor commands. The currently running Editor was not yet visible to unity status; do not assume CLI control works until status reports a connected instance.
- KitWright Unity MCP previously worked through the existing project configuration; retain it as a secondary bridge after reconnection.
- Blender 5.2.2 executable: D:\dump\blender.exe. Blender MCP add-on is installed in the user profile but is not enabled in the GUI; Blender background Python mode can still generate/export assets without the add-on.
- blender-cli doctor requires BLENDER_BIN=D:\dump\blender.exe in the current process/environment; initial ENOENT was PATH discovery, not proof Blender is absent.
- skillfish repository discovery hit GitHub API rate limiting. Do not repeatedly retry until reset; use already-installed global skills and authoritative docs.

## Review cadence
Revisit this playbook before adding a gameplay system, generating/importing a custom model, changing rendering settings, changing pooling/input architecture, making performance claims, or preparing a release build. Append measurements/findings rather than rewriting history.

## Research log
### 2026-10-12
- Confirmed Unity CLI beta.13 is current.
- Added official Unity Pipeline package to enable CLI Editor automation. unity status had not detected the Editor yet; live CLI control remains unverified.
- Confirmed Blender executable at D:\dump\blender.exe (registered Blender 5.2.2).
- Reviewed Unity 6.6 URP profiling/draw-call guidance, Unity pooling documentation, Input System callbacks, Blender glTF export documentation, Game Developer feedback articles, and GDC prototyping guidance.
- Next: reconnect editor automation, complete controlled gameplay state-transition tests, then build a bespoke Blender asset only where it materially improves gameplay readability.
## Research Update — High-Speed Flight Feel and Space Set Pieces (2026-10-12)

### Design thesis
VOIDLINE should feel fast because the world has scale, close objects move past with readable parallax, the ship responds immediately but retains a little momentum, and the camera/engines amplify the change in speed. Do not make permanent blur the primary speed mechanic. The key state changes must be visually legible: cruising -> boost spool -> sustained boost -> release; normal steering -> pronounced bank -> settled return; roll input -> clear 360-degree roll -> brief dodge window -> cooldown. Use short, layered feedback and return to a clean resting state.

### Research: Need for Speed Most Wanted (2005) and EA's racing art direction
- GameSpot's 2005 *Most Wanted* review specifically calls out its heavy use of blur at high speeds and describes it as supporting a believable sense of speed: https://www.gamespot.com/reviews/need-for-speed-most-wanted-review/1900-6140037/ (accessed 2026-10-12).
- Computer Graphics World's 2004 article *Like in the Movies* documents EA senior art director Habib Zargarpour's design work on the earlier *Need for Speed Underground*, not specifically the 2005 release. It describes velocity-oriented blur, HDR-style streaking lights that also guide navigation, selective pools of light, deliberate black levels, and a camera that is not rigidly locked to the vehicle. Treat these as credible EA-series design principles, not proof of the exact internal implementation of *Most Wanted*: https://www.cgw.com/Publications/CGW/2004/Volume-27-Issue-9-September-2004-/Like-in-the-Movies.aspx.
- GameSpot's May 2005 *Most Wanted* E3 hands-on report notes gradual visual adaptation when emerging from a tunnel; lighting transition is a strong, readable design cue for VOIDLINE's tunnel-to-open-space reveal: https://www.gamespot.com/articles/need-for-speed-most-wanted-e3-2005-hands-on-report/1100-6125443/.
- Practical VOIDLINE translation: (1) make boost an actual smoothed world-speed increase; (2) widen camera FOV subtly while boosting, then ease back; (3) intensify short, directional engine trails and sparse linear environmental streaks during boost; (4) make light paths and structure silhouettes readable rather than smearing the whole screen; (5) give landmarks foreground parallax and a little camera lag/anticipation; (6) stage a transition from enclosed dark tunnel through an enormous aperture into open nebula/planet scenery; (7) tune for stable readability, not maximum effects.

### Current Unity rendering references
- Unity 6.6 URP Motion Blur docs: https://docs.unity.com/zh-cn/engine/6000.6/manual/post-processing-and-full-screen-effects/urp/effect-list/motion-blur. URP motion blur primarily responds to camera movement; Camera Only mode is cheaper than Camera and Objects; intensity/clamp are adjustable, and excessive blur can cost performance. Prefer a restrained/optional camera-response pass over heavy default blur.
- Unity URP post-processing overview: https://docs.unity.com/en-us/engine/6000.3/manual/post-processing-and-full-screen-effects/urp/integration-with-post-processing. Use Volume overrides deliberately; measure rather than stacking expensive fullscreen effects.
- Unity pooling guidance: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Pool.ObjectPool_1.html. Reuse dynamic hazards/effects where creation churn is present; reset all state when reused. VOIDLINE already uses asteroid/pickup pools, so preserve them.
- Blender official glTF export manual: https://docs.blender.org/manual/en/latest/addons/scene_gltf2.html. Keep transforms normalized, hierarchy/pivots sensible, materials simple, and export only game-ready objects.

### Visual direction: VOIDLINE
- Palette roles: deep navy/ink for negative space; cold steel and low-saturation indigo for rock/structure mass; cyan/teal as navigational guide and engine light; icy near-white on the ship silhouette; use one restrained warm accent (amber/rose) for rare distant astronomical features. Do not make everything emissive.
- Frame composition: keep the ship silhouette readable against dark midtones; light the leading edges of the ship and structures rather than flattening everything with ambient brightness. Establish foreground speed cues, midground hazards, and a very distant planet/ring/nebula layer.
- Giant set piece: create an irregular, ancient, engineered ring/canyon aperture, outer silhouette 2–3 times the tunnel width, with a clear central hole and asymmetrical chipped ribs. Dark non-emissive mass owns most of the area; broken teal guide segments point toward the route. Pass through the aperture, then spend a short segment exposed to open space before enclosed architecture returns. This is an authored reveal, not a constant barrage of giant props.
- Procedural environment props must not obstruct the safe flight envelope unless they are intentionally authored hazards. Decorative structures use no expensive rigidbody physics; colliders should be absent or simple and disabled unless needed for gameplay.

### Performance and implementation rules
- Target 60 FPS on a mid-range PC (16.7 ms/frame) as a project budget goal; do not report compliance until measured in Play Mode/build. Current baseline was Edit Mode only and cannot establish gameplay performance.
- Retain the existing Input System, run-settings ScriptableObject, object pools, and scene bootstrap. Keep movement authority separate from VFX. Avoid adding a second gameplay manager or reimplementing score/hull logic.
- Cache references on initialization; no Find/GetComponent loops or heap allocations in per-frame paths. Avoid material instantiation each frame. Two small particle systems with low overdraw beat a giant transparent fog wall; use ParticleSystem rather than VFX Graph for these simple twin exhausts.
- Roll/boost feedback uses short-lived visuals. Any invulnerability must have an explicit, short window and be blocked while game over/disabled; it must reset on scene reload. Visual roll stays on the model root; collision/body stays upright and stable.
- Set pieces use a deliberate cadence and clear framing. Build/greybox gameplay spaces before adding detailed dressing; test the view approaching, crossing, and leaving the gate, not only the static asset.
- After edits: compile in the actual Unity Editor, check Console errors, test keyboard and controller bindings, verify cooldown/disable/restart behavior, measure frame timing in Play Mode, inspect the rendered scene at gameplay resolution, and produce a Windows build only after the Editor state is stable.

### Priority roadmap
1. Flight feel: smoothed lateral velocity, 360-degree roll and explicit cooldown, hold-to-boost acceleration, synchronized world speed, speed-responsive FOV/camera lean.
2. Engine feel: twin world-space exhaust ParticleSystems, brighter/longer only during boost, restrained camera/audio feedback; no unnecessary package additions.
3. Environment identity: authored deep-space skybox, a custom optimized Blender aperture/gate asset, measured LOD/materials, segment-based scheduling so the gate opens a space-exposure beat and then returns to the tunnel.
4. QA: inspect frame captures, verify 15–30 seconds of recycle continuity, dodge state and invulnerability, paused/dead control, rest state after boost, build configuration, Console and frame-time snapshots.
5. Only after these pass: expand hazards/patterns/checkpoints/shooting; avoid feature sprawl before the core 30-second loop is compelling.


## 2026-10-12 — Expanded skill pass and sector-loop direction

### Local skill application
- Reviewed project-local `unity-mcp-workflow` plus installed `game-design`, `game-feel`, `level-design`, `playtesting`, `3d-games`, `camera-systems`, `unity-csharp-scripting`, `unity-input-system`, `unity-physics`, `physics-tuning`, `unity-animation`, `unity-scriptableobjects`, `unity-architecture`, `unity-build-pipeline`, `game-build-pipeline`, `game-performance`, `performance-optimization`, `game-ui-ux`, `game-audio`, `audio-design`, `sound-effects`, `create-game-assets`, `blender-modeling`, `blender-animation`, `tech-art`, and `unity-tooling` skills.
- The project uses the relevant techniques already: simple collision proxies, reusable hazards/pickups, camera-only trauma, transient synthesized SFX, designer-facing run settings, anchored HUD, and a measured Editor sample. The next pass must verify those behaviours at runtime; compilation and static review are not substitutes.
- `Docs/AGENT_SKILLS_WORKFLOW.md` maps those skill families to project choices. Non-Unity and unrelated systems were intentionally excluded rather than blindly forcing every installed skill into the game.

### Design decision
- Historical decision notes from `Docs/VOIDLINE_DESIGN_DIRECTION.md` are archived outside the project. The later multi-route graph/fork experiment has also been rejected and archived. Preserve the original 3D sci-fi survival-flight identity as a single continuous route; vary space, curvature, hazard grammar, landmarks and pacing, not parallel branches.
- The intended short-loop is `read hazard → route/roll/collect → optionally boost for risk → cross gate → reset attention → next sector`. This uses existing systems and landmarks rather than adding weapons, crafting, long tutorials, or meta-progression.
- Gate/arch rewards, formation pacing, near-miss scoring and the timed flow chain are implemented in C# but not yet tuned from observed play. Boost itself still has no separate score bonus; do not add one without comparing the extra hazard pressure.

### Fresh-Play-Mode state and tool recovery
- `ProjectSettings/EditorSettings.asset` has Enter Play Mode Options enabled with `DisableDomainReload` + `DisableSceneReload` (flags value 3). Unity documents this as a faster entry path which avoids recreating the application domain and avoids reloading/destroying/awakening all scene objects; runtime fields initialized only in `Awake` therefore need a deliberate fresh-run reset when fast iteration is enabled. Sources: https://docs.unity3d.com/6000.6/Documentation/ScriptReference/EnterPlayModeOptions.html and https://docs.unity3d.com/6000.6/Documentation/ScriptReference/EditorSettings-enterPlayModeOptionsEnabled.html.
- Preferred remedy is to make the run lifecycle explicitly resettable and test it, while retaining fast iteration if safe. If that is not feasible, change Enter Play Mode Options through Unity's Editor API/UI. Do not patch `EditorSettings.asset` text directly.
- `unity status` detects the correct Editor, but main-thread Pipeline operations (`editor_status`, scene hierarchy, and a no-op eval) time out. Unity's log records the matching `/api/exec` timeouts. The next run should start with one low-cost command and then one batch of state/console reads only after the bridge has recovered; avoid concurrent command floods, repeated Play Mode toggles, or opening a second Editor.

### Official Unity references retained for the next pass
- URP performance must be profiled against measured CPU/GPU costs before changing rendering features: https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling/in-urp/understand-performance
- Unity pooling reduces repeated instantiate/destroy churn, but pool callbacks and object access remain main-thread work: https://docs.unity.com/en-us/engine/6000.6/manual/analysis/performance-memory/managed-memory/optimizing-code-managed-memory/reusable-code
- Runtime initialization phases are distinct; choose the appropriate phase when resetting state for a new Play Mode session: https://docs.unity3d.com/6000.6/Documentation/ScriptReference/RuntimeInitializeLoadType.html


## 2026-10-12 ? Sector/flow gameplay implementation

- VoidlineSpaceSequence now reports the single transition when an active landmark crosses world Z=0. VoidlineGameManager awards +250 for a transit gate and +100 for a hollow arch, advances the sector, and shows the gate/arch count in the HUD.
- Asteroid spawn logic now periodically creates a staggered cross-weave and, after three cleared passages, a vertical weave; single hazards remain in the pattern rotation so it does not turn into a constant wall.
- A safe asteroid close-pass can award a near-miss bonus. Near misses and roll-dodges build a capped flow chain that expires after 4.5 seconds; hull damage clears it. The HUD shows the live chain and total close passes. Distinct transient synthesized cues were added for passage clear and near miss.
- Game Over now stops corridor motion as well as flight/hazard motion, preventing the end overlay and far-field environment from drifting.
- These are source-level gameplay changes. Unity CLI recompile returned exit code 0 and Library/ScriptAssemblies/Assembly-CSharp.dll was updated afterward; this is compile evidence only. The existing Builds/Windows64/VOIDLINE.exe is older than the new source/assembly and must be rebuilt before release. Controlled runtime checks remain open.


### First controlled sector-loop observation (2026-10-12)

- At elapsed 17.52 s, the live manager reported gameOver=False, hull=2, passagesCleared=1, gatesCleared=1, archesCleared=0, bonusPoints=460, nearMisses=5, flowChain=4, encounterIndex=12. This verified the single gate-crossing callback and progression HUD in that specific source state.
- After reviewing that capture, HUD metrics were split across separate rows and slightly reduced in size. A follow-up 6.46 s run showed the fresh state correctly (hull=3, passagesCleared=0, bonusPoints=0, nearMisses=0, flowChain=0), with compilationFailed=false, zero Console errors and zero warnings. This was a focused HUD inspection, not full-run QA.
- The near-miss evaluation plane was subsequently moved behind the collision envelope so a hit cannot be paid as a near miss. That final threshold compiles, but the 6.46 s run ended too early to observe a complete safe hazard pass.
- The 1280x720 HUD capture is retained outside Unity's Assets import tree at D:\dump\VOIDLINE_QA_Captures\2026-10-12-sector-loop-hud.png.


## 2026-10-12 ? Build pipeline recovery

- The async Unity Pipeline build did not complete within 12m45s and did not update Builds/Windows64/VOIDLINE.exe. Its Editor was closed after the graceful close path reported no method. The prior 150.53 MB build tree is preserved at D:\dump\VOIDLINE_Backups\2026-10-12_pre_sector_gameplay_build\Windows64.
- Official Unity CLI batch mode was then attempted once to a separate candidate output. It failed before compilation with Package Manager local IPC failure (Upm-19564, 30-second connection timeout). Full log retained at Logs/VOIDLINE_CandidateBuild.log. No player executable was emitted; the original working build is intact.
- Recovery rule: reopen the GUI Editor and read its fresh Console/compile status; troubleshoot Package Manager startup once before another build. No simultaneous Editors, no repeated retry loop.


## 2026-10-12 ? Final UPM startup diagnosis

- Live Pipeline player build build_824727d9fe3b stayed in building state for 12m45s and never updated the output. Its Editor was closed via Unity CLI after its graceful-close command reported no close method; the original build was preserved first.
- A single official Unity CLI batch build then exited before project compilation with UPM local IPC error (Upm-19564, 30-second connection timeout). The candidate has no executable, and its full log remains at Logs/VOIDLINE_CandidateBuild.log.
- A single GUI reopening also failed at Package Manager startup. The built-in Diagnose action logged a path-undefined internal error. The installed UnityPackageManager.exe exists, has a valid Unity signature, and no matching Defender/AppLocker/Code Integrity event was found in the queried window. Root cause is unresolved; do not assert antivirus is responsible.
- Final working state for this pass: source edits and runtime capture are saved; the original Windows x64 build remains intact and stale; Unity Editor is currently closed; do not restart it again without a concrete UPM recovery fix.


## 2026-10-12 ? Candidate Player build retry

- The GUI Editor later recovered on a Hub launch with a live ready status and a running Package Manager service. Fresh Console state: no compilation failure, zero errors, two environment/legacy-input warnings.
- One Windows x64 Pipeline build was queued to Builds/Windows64_Candidate/VOIDLINE.exe after its dry run validated. Current job ID: build_126f721d98f6, active with no executable output as of approximately 3 minutes elapsed. The status query itself remains available, while an Editor-main-thread status operation waits during BuildPlayer. No second Editor has been launched.
- Retain the existing Build/QA facts above: no new player build is considered complete until the job reports completed, errors/warnings are reviewed, and the candidate file tree is verified.


## 2026-10-12 ? Final build result

- In-Editor Pipeline job build_126f721d98f6 showed the same main-thread stall after approximately five minutes, with no candidate executable output; the stuck Editor was closed.
- A single official CLI batch attempt with -noUpm failed C# compilation because UnityEngine.UI, UnityEngine.EventSystems and UnityEngine.InputSystem assemblies were unavailable without the Package Manager resolving project dependencies. No player executable was emitted. Full log is Logs/VOIDLINE_BatchCandidateBuild.log.
- The Package Manager-backed GUI Editor had been able to report ready, run source compilation, and execute a real gate-crossing test; however the Player build path still stalls. Original Windows x64 build remains intact and stale, with a full backup at D:\dump\VOIDLINE_Backups\2026-10-12_pre_sector_gameplay_build\Windows64.
- End this pass with the source edits retained, no unverified executable claimed, and Unity Editor closed. Do not repeat Editor/build loops without a concrete Package Manager or Pipeline fix.


## 2026-10-12 ? Seeded route spine and fair encounter scheduling

- The confirmed design is authored route grammar + seeded variation, not unrestricted random placement. See Docs/VOIDLINE_CREATIVE_BIBLE.md.
- First implementation: VoidlineRouteDirector supplies seeded X/Y route knots, smooth centre offsets and track tangents. CorridorScroller applies them to the repeating tunnel geometry; the player stays route-local; hazards, pickups, gates and the camera share the same world frame.
- Second implementation: VoidlineEncounterDirector chooses among Single, OffsetPair, CrossWeave and a later VerticalWeave using elapsed time, sector progression and hull state. A plan contains a safe lane, hazard specs and spawn-delay multiplier. Peak patterns trigger a recovery single, and low hull suppresses peak patterns.
- First runtime observation after both systems were active: seed=-1089701328, distance=216.87, section=2, player frame error=0.000, hull=2, passagesCleared=1, gatesCleared=1, current plan CrossWeave, safe lane (0,0). The fresh Console reported zero errors and zero warnings.
- A 4-second fresh-session check after changing Enter Play Mode options to DisableDomainReload only confirmed hull=3, zero run bonus/clears/near misses, lane (0,0), frame error 0.000, no Console errors/warnings. This is a focused runtime check, not full gameplay QA.
- Enter Play Mode setting changed through Unity Editor API: enterPlayModeOptionsEnabled=true, options=DisableDomainReload, sceneReloadDisabled=false. Keep domain reload disabled and scene reload enabled unless a robust reset hook supersedes it.
- The route itself is continuous and seeded now; the existing 3 corridor meshes still repeat the same architectural module. Next production stage must author real distinct modules and route sockets rather than call the route spine a finished procedural level system.
- Camera baseline clarified: the currently active main scene serializes FOV=65 and runtime reads 65.0. The earlier FOV?76 read came from SampleScene while it was the active scene. No camera override bug has been demonstrated; tune boost response from the actual main scene only.
- Current Windows x64 build is still the previous build. A previous build attempt failed before compiling because Unity Package Manager could not start/connect over local IPC. Do not call the updated game build-verified until Package Manager recovery, fresh BuildReport success and standalone launch are observed.

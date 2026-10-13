# VOIDLINE — QA Checklist

Last automated/editor/build pass: 2026-10-13 (source-level encounter fuzzing; live audio/propulsion checks remain open). Re-run after further gameplay changes.

## 2026-10-12 — Space Threat soundtrack integrated (original layered implementation; superseded 2026-10-13)

- [x] Imported pack: `Assets/Space Threat (Action Cinematic Music)` — 6 WAV files (three full alternate mixes and three looped versions), all 44.1 kHz
  stereo 16-bit. Durations: Slow Ambient 144.0s / looped 132.0s; Electronic Dramatic 88.6s / looped
  81.2s; Energetic Powerful 70.5s / looped 66.0s.
- [x] The three **"Looped"** versions were copied (with their `.meta` files, preserving GUIDs and import
  settings) into `Assets/Resources` so `Resources.Load` can resolve them:
  `Space Threat  (Slow Ambient Version, Looped).wav`,
  `Space Threat (Electronic Dramatic Version, Looped).wav`,
  `Space Threat (Energetic Powerful Version, Looped).wav`.
  The three non-looped masters are deliberately not loaded.
- [x] `VoidlineAdaptiveMusic` rewritten to play the real alternate versions and drop the procedural synth entirely
  (`CreateMusicStem` and `AudioClip.Create` confirmed absent from the source). Layer mapping:
  ambient=Slow Ambient, tension=Electronic Dramatic, boost=Energetic Powerful, critical=Electronic
  Dramatic (reused so the score stays coherent instead of adding an unrelated fourth track).
- [x] Runtime readback in Play Mode: all four layers report the expected clip, `loop=True`,
  `spatialBlend=0` (2D bed), `isPlaying=True`. All three `Resources.Load` probes return
  `loadState=Loaded` at 44100 Hz, 2 channels.
- [x] Original mix response measured across scenarios (settled volumes): cruise 0.550 ambient / 0.150 tension;
  boosting adds 0.447 boost; critical hull adds 0.402 tension and 0.519 critical; pause ducks the bed.
  These readings describe the original layer mix, not the newer crossfade design.
- [x] Game Over zeroes every layer target so the bed fades rather than cutting.
- [x] Fixed a bug in this pass: `CreateSource` previously assigned `source.name`, which renames the
  **host GameObject** — the shared `GameManager` had been renamed to `Music_Critical`. Sources are now
  tracked by field and the object is left named `GameManager`.
- [x] Fixed a pre-existing crash unrelated to music: `VoidlineFlightEffects.UpdateAuthoredPlumes`
  threw `ArgumentNullException: dest` from `Renderer.GetPropertyBlock` every frame because
  `plumePropertyBlock` was null (component reached `LateUpdate` without `Awake` having created it).
  Now lazily initialised. Console reports zero errors and zero warnings after a fresh run.
- [ ] Balance is unverified by ear: layer levels (ambient 0.55 / tension 0.50 / boost 0.62 /
  critical 0.72) are starting values exposed as `[SerializeField]`, not tuned against listening.
- [ ] No SFX pass yet: event sounds are still generated in `VoidlineAudioFeedback`; the pack contains
  no separate one-shots, so impact/pickup cues remain synthetic.
- [ ] Check the Space Threat licence terms before shipping or redistributing.


## 2026-10-12 — Authored courier ship actually reaches the screen

- [x] **Root cause fixed:** `VoidlineGameManager.EnsurePlayer` found the serialized Kenney `craft_speederD`
  placeholder, satisfied its "a ship already exists" check and returned early, so `BuildShipVisual` — the
  only path that loads `Resources/VOIDLINE_Courier.fbx` — never ran. The bespoke craft was imported and
  textured but had never been displayed at runtime. `EnsurePlayer` now always removes legacy visuals and
  builds the authored craft unless `VOIDLINE_Courier` is already present.
- [x] The placeholder carried a scene-level material override to `M_Asteroid_Crystal`, so the player ship
  had been rendering with an **asteroid** material. Confirmed removed from both scenes on disk.
- [x] Stray empty `EngineLeft` / `EngineRight` anchors (children of `Player`, siblings of `Model`) removed
  from `main.unity` and `main_open_space.unity` via the Unity Editor API; both scenes saved,
  `Is Dirty: False`. `RemoveLegacyShipVisuals` also strips them at runtime.
- [x] Runtime readback in Play Mode: `Player/Model/VOIDLINE_Courier` present, 13 renderers, 10,084 tris,
  localScale 0.42, world bounds 1.507 x 0.268 x 1.620 against the 1.55 x 0.65 x 1.80 trigger collider
  (not wider or longer than the collision envelope).
- [x] Framing measured: craft at viewport centre (0.49, 0.37), ~16% of Game-view height, 7.76 units from
  the chase camera at 74.5° FOV. Authored hull and cyan/blue exhaust plumes are visible in the dark
  corridor against the nebula skybox.
- [x] Materials now assign per authored material slot, so the FBX's multi-material renderers
  (`Engine_Nozzles` → nozzles/titanium/armor, `Titanium_Details` → titanium/nozzle, `Armor_Panels` →
  armor/nozzle) keep their layered split instead of collapsing to one flat material.
- [x] `T_VOIDLINE_Courier_ORM.png` (authored but previously unused) now feeds URP's occlusion channel;
  `T_VOIDLINE_Courier_Mask.png` feeds metallic/smoothness. Occlusion verified present on hull, armor,
  nozzle and titanium slots.
- [x] Recompile exited cleanly; `get_compilation_errors` reports none. Fresh Console shows zero errors and
  zero warnings for this pass (the only logged errors are the historical `main.unity` save failures,
  since resolved).
- [x] Captures: `D:\dump\VOIDLINE_QA_Captures\2026-10-12_authored_courier_inplay.png` (live run) and
  `2026-10-12_authored_courier.png` (post-gameover).
- [ ] **Environmental caveat:** saving `main.unity` intermittently fails with "Access is denied" on the
  Temp-file move. Verified as environmental, not a project defect — in-place writes and delete-then-create
  both succeed while atomic rename-over of that file is denied. Clicking "Try Again" on the dialog
  completes the save. Expect one retry when saving `main.unity`.
- [ ] Windows x64 build/standalone launch still unverified; the existing executable predates these changes.


## 2026-10-12 follow-up changes
- Fixed landmark FBX orientation and effective scale; imported bounds now match the intended ~28–30 unit set pieces.
- Reworked the space passage into a distance-synchronized tunnel fade-out/fade-in and extended landmark cleanup clearance.
- Added project-owned tiled steel and 2K basalt base/normal maps; corrected normal-map import flags and rock/tunnel material assignments.
- Set PC quality to 4x MSAA, higher-resolution shadows, anisotropic filtering, and 1920x1080 default output; global texture mip limit remains 0.
- Restored the intended 3-point starting hull and fixed the keyboard Pause/Restart action callbacks.
- Added .mcp.json to .gitignore so local KitWright connection credentials are not staged by accident.
- Windows x64 build completed successfully to Builds/Windows64/VOIDLINE.exe (150.5 MB build tree). The remote environment blocked launching the executable for smoke testing, so standalone runtime verification is still open.


## Compile and scene integrity
- [x] main.unity is the enabled scene in Build Settings.
- [x] External script refresh/recompile completed successfully after the input, feedback, pickup, and lifecycle changes.
- [x] Unity Console reported no C# compile errors after the successful recompile.
- [ ] Remove four non-fatal CS0618 obsolescence warnings at `VoidlineGameManager.cs` lines 620 and 710 (`FindObjectsByType` overload with `FindObjectsSortMode.None`).
- [x] Windows x64 player build completed successfully (150.5 MB build tree; `Builds/Windows64/VOIDLINE.exe`).
- [ ] Standalone executable launch/smoke test; the remote command layer blocked launch attempts, so no launch success is claimed.
- [x] VoidlineRunSettings.asset, the Input Actions asset, both asteroid prefabs, and the repair pickup prefab exist.
- [x] Scene references were inspected: run settings, Input Actions, two hazard prefabs, and repair pickup prefab are assigned.
- [x] Crystal prefab's three material slots were normalized to the project-owned URP material after a magenta fallback was observed.
- [x] Corridor accent rails are present on all 24 panels and use the project-owned emissive material.

## Runtime checks
- [x] Play Mode starts and the HUD reports hull, score, best score, cells, and speed.
- [x] Input System Player/Move, Player/Pause, and Player/Restart actions are enabled at runtime.
- [x] WASD input was simulated once and moved the player to the expected horizontal clamp.
- [x] Runtime asteroid actors spawn and use the two prefab variants.
- [x] A repair pickup spawned during a run and a cell collection was observed; controlled reward/collision validation remains open.
- [x] Editor performance snapshot was about 101 FPS, 9.9 ms CPU frame and 0.43 ms GPU frame in that specific Play Mode sample; this is an editor-only observation, not a release-build benchmark.

## Remaining verification
- [x] Escape toggles pause/resume with the Game View focused; observed `paused=True/timeScale=0` and `paused=False/timeScale=1` through KitWright runtime state checks.
- [ ] Trigger a real asteroid collision and verify one hull point is removed, flash/audio/shake play, and damage cooldown prevents repeat hits.
- [ ] Move the player through a repair cell and verify hull restoration, +100 bonus, cell count, sound, and pool return.
- [x] Restart reset score/hull/cell count while retaining best score in the Play Mode state check.
- [ ] Verify the Game Over overlay's button works with mouse and keyboard focus.
- [ ] Capture the pickup at its intended approach distance and confirm the halo is legible at normal desktop resolution.
- [ ] Check corridor recycling across several complete segment cycles and confirm no visible gap/pop.
- [ ] Test at 16:9 and a wider aspect ratio; confirm HUD anchors remain legible.
- [ ] Launch the successful Windows x64 executable and repeat critical gameplay checks outside the Editor; remote process launch was blocked by the tool safety layer in this pass.

## Regression rules
- Never mark an item passed only because the code compiles.
- Record build output, console errors, and observed gameplay separately.
- Do not delete temporary captures until the final visual comparison is complete.
## 2026-10-12 — Unity CLI / editor-control follow-up
- Unity CLI `1.0.0-beta.13` is installed. `unity status --format json` confirms the intended project `D:\dump\Voidline`, Editor `6000.6.5f1`, PID `9132`, as `ready`.
- The CLI tool catalog can be read and `console_status` returned, but live editor operations are currently not dependable: `editor_status` and `get_scene_hierarchy` timed out after 30 seconds, while a no-op `eval` hit the Pipeline's main-thread timeout. `Logs/Editor.log` records corresponding `/api/exec` main-thread timeouts.
- The captured console-status sample was about 8.7 minutes old when read. Its `compilationFailed=false` field is useful context but is not a fresh Console audit; do not treat it as current compile/runtime verification.
- `ProjectSettings/EditorSettings.asset` has `m_EnterPlayModeOptionsEnabled: 1` and `m_EnterPlayModeOptions: 3` (both domain reload and scene reload disabled); these values are present in the repository baseline, not a new change from this pass. Repeated Editor sessions can therefore retain scene-instance state and must not be assumed to start fresh. Treat the prior “run already over” test as contaminated by session reuse until it is repeated with a known-fresh run.
- No Unity Editor restart, Play Mode toggle, scene edit, prefab edit, or build was performed during this tooling investigation. No previously open scene state was deliberately discarded.
- Next action: restore reliable live Editor command execution, then do one batched current-state/Console capture before resuming targeted collision, repair reward, recycling, and resolution checks. Avoid repeated polling/reloads.


## 2026-10-12 ? Gameplay loop upgrade (compiled; runtime checks open)

- Added landmark-plane scoring and sectors: each active gate/arch crossing is guarded against duplicate rewards; gates award +250, arches +100; HUD shows sectors, gate/arch clears and a short milestone callout.
- Replaced purely independent random asteroid points with a paced formation rotation (mostly singles, occasional staggered cross-weave, vertical weave unlocked after three passage clears).
- Added safe near-miss scoring and a capped, 4.5-second flow chain shared by near misses and roll-dodges; damage breaks the chain. Added distinct generated SFX cues, with flow/close-pass values in HUD.
- Game Over stops corridor motion in addition to the existing hazard and player shutdown.
- Unity CLI recompile exited with code 0 after the batched source edits; Library/ScriptAssemblies/Assembly-CSharp.dll timestamp is newer than the edited C# sources. dotnet build was unavailable because this Windows installation has no .NET SDK. Treat Unity compilation and gameplay runtime verification separately.
- Do not reuse the existing Windows x64 build as if current: Builds/Windows64/VOIDLINE.exe has an older timestamp. Rebuild before release. The last known standalone launch attempt was blocked by the remote tool safety layer.
- Open gameplay checks: crossing rewards fire exactly once with the correct gate/arch amount; close-pass thresholds avoid awarding on a true collision; flow chain expires and resets on damage; pattern fairness at boost speed; HUD fit; and frozen world after Game Over.


## 2026-10-12 ? Controlled sector-loop and HUD observation

- [x] Gate crossing callback advances to Sector 02 and adds the +250 gate bonus in a live 17.52 s Editor run (passagesCleared=1, gatesCleared=1, archesCleared=0, bonusPoints=460 at inspection).
- [x] Fresh-run HUD values: a second 6.46 s run showed hull 3, sector 1, score 59, cells 00, close passes 00, flow x00, no gate reward yet.
- [x] HUD row collision fixed after inspecting the 1280x720 capture: cells/close count, speed/flow, and boost now get their own rows; capture retained at D:\dump\VOIDLINE_QA_Captures\2026-10-12-sector-loop-hud.png.
- [x] Fresh Console snapshot after the short HUD-focused run: compilationFailed=false, compiling=false, zero Console errors, zero Console warnings.
- [ ] Controlled close-pass must complete after the revised post-collision-envelope plane; the earlier five near-miss count came from the pre-tuning evaluation plane and is not proof for the final threshold.
- [ ] Fresh Windows x64 build: Pipeline job build_824727d9fe3b remained in building state for 12m45s with no new executable output; the Editor was closed to recover from the stuck build. The pre-build 150.53 MB tree is backed up at D:\dump\VOIDLINE_Backups\2026-10-12_pre_sector_gameplay_build\Windows64.


## 2026-10-12 ? Build recovery outcome

- The Unity Pipeline build job remained in building state for more than 12 minutes, beyond the earlier full build duration, while the Windows64 executable timestamp stayed at 10:05:19. A graceful Editor close reported Closed=false, Method=none; Unity CLI's documented forced close was used to release the stuck Editor before attempting batch mode. All edited C# files and scene settings are on disk; the previous build tree was backed up before any output change.
- The official CLI batch build to Builds/Windows64_Candidate/VOIDLINE.exe exited early. Logs/VOIDLINE_CandidateBuild.log records: Could not connect to IPC stream Upm-19564 after 30.0 seconds and Failed to start the Unity Package Manager local server process. No candidate player executable was produced; the original Builds/Windows64/VOIDLINE.exe remains unchanged.
- Build is not verified. The next appropriate path is to reopen the GUI Editor, confirm fresh compile/Console status, then resolve Package Manager startup before attempting another build. Do not rerun repeated build loops or launch multiple Editor instances.


## 2026-10-12 ? Package Manager startup failure remains unresolved

- After the batch build failed with UPM local IPC timeout, the GUI Editor was opened once. It first displayed the elevated-privilege warning; the Restart as Standard User choice returned to the same warning in this environment, and Ignore Warning allowed startup to continue.
- Unity then displayed the Package Manager error dialog: it could not start its local server process. The Diagnose action was selected, but the user-level upm-diag.log only logged two errors stating that the path argument must be a string but was undefined. No useful diagnostic bundle was produced.
- Read-only checks: UnityPackageManager.exe exists at the installed Editor path, is 96,002,984 bytes (version 24.18.0), has a valid Unity Technologies code signature, and has no Zone.Identifier stream. No matching Defender, AppLocker or Code Integrity events were found in the checked time window. This does not prove the root cause; it only means an AV block was not evidenced by those checks.
- The GUI Editor process then exited. unity status --format json has no connected instances. Do not launch additional copies or repeatedly retry startup. Resolve the UPM server startup failure before any more Editor verification or Player builds.
- Final state: gameplay C# and design/QA notes remain on disk; the prior Windows64 build remains untouched and backed up; no updated Player executable is available. Do not claim a fresh Windows build or final release verification.


## 2026-10-12 ? Candidate build retry through recovered Editor

- The Editor/UPM did recover on a later Hub launch: live Editor status reported ready; UnityPackageManager.exe was running; Console compilationFailed=false and 0 errors. Two current warnings were the existing Input Manager deprecation and elevated-privilege notice.
- A dry run for a candidate Windows x64 build validated successfully. Pipeline job build_126f721d98f6 is now active at roughly three minutes elapsed. It has not produced an executable yet. The main-thread editor_status request waits during BuildPlayer, so no per-frame editor inspection will be attempted while the build runs.
- This is in progress, not a verified build. The old Builds/Windows64/VOIDLINE.exe remains unchanged and backed up. Check job/output once at a reasonable interval; do not launch a second Editor or restart the build loop.


## 2026-10-12 ? Final build attempts and handoff state

- The second in-Editor candidate build (build_126f721d98f6) repeated the main-thread stall pattern: build_status remained building at about five minutes, candidate output was still empty, and live editor_status stopped responding. It was ended with Unity CLI's forced-close path to avoid another indefinite block.
- One final official CLI batch attempt used the -noUpm argument to test whether cached package files were sufficient without the failed package-manager server. It failed before build with C# reference errors: UnityEngine.UI, EventSystems, and InputSystem assemblies were absent from compilation, and Unity exited with code 1. This confirms that bypassing UPM is not a safe workaround for this project.
- No candidate executable was emitted by either failed route. The source changes remain saved; the existing Windows64 executable is unchanged at its 2026-10-12 10:05:19 timestamp, and the original 150.53 MB build tree remains backed up.
- Current handoff state: source/logic work is present, sector crossing was observed in an Editor run, latest near-miss threshold is compile-checked but not runtime-checked, and no fresh Windows x64 build is available. Unity Editor is closed after recovery. The blocker is reliable Package Manager initialization and the synchronous Pipeline build stall; do not claim a release build.


## 2026-10-12 ? Route spine + encounter director

- [x] Unity C# recompile completed with exit code 0 after adding the route spine and seeded encounter director.
- [x] Active game scene Assets/VOIDLINE/Scenes/main.unity now attaches VoidlineRouteDirector to CorridorRoot; saved via Unity Editor API.
- [x] Runtime route alignment: at route distance 216.87, the player position matched route centre plus lane coordinates with error 0.000. The gate had crossed and awarded +250; passagesCleared=1 and gatesCleared=1.
- [x] Encounter planner observed CrossWeave with safe lane (0,0); the fresh Console reported zero errors/warnings.
- [x] Fast Play Mode no longer disables scene reload. Editor API set enterPlayModeOptions=DisableDomainReload only. Fresh 4-second run started with hull 3, score/clears/near-misses/flow at zero and lane (0,0).
- [ ] Full fairness sampling remains open: review representative deterministic seeds, especially pair and vertical patterns, to confirm the planned safe corridor stays collision-free at peak boost.
- [x] Camera FOV discrepancy resolved: active main scene Camera serializes to 65 and runtime remains at 65; the earlier ~76 measurement was from SampleScene, not main.unity.
- [ ] Distinct authored corridor modules / safe-risk branching are not implemented yet; only the centreline curves while the 3 current tunnel meshes repeat the same architectural module.
- [ ] Bespoke hero ship and Clinger scrape-off encounter remain design targets, not implemented features.
- [ ] Fresh Windows build and standalone launch remain unverified; current official batch build failed before compilation due Unity Package Manager local IPC startup failure.


## 2026-10-12 — Route graph foundation / Phase 1

- [x] Created `Docs/VOIDLINE_IMPLEMENTATION_ROADMAP.md` as the single execution plan and `Docs/README.md` as the documentation index.
- [x] Archived the superseded dated `Docs/VOIDLINE_DESIGN_DIRECTION.md` outside the project at `D:\\dump\\VOIDLINE_Backups\\2026-10-12_route_graph_phase1\\Docs\\VOIDLINE_DESIGN_DIRECTION.md`. Active references now point to the roadmap; core design, QA, asset provenance, technical ownership and skills guidance documents remain.
- [x] Added `Assets/VOIDLINE/Scripts/Environment/VoidlineRouteGraph.cs`: typed module nodes, branch-role/reward intent, explicit two-edge Gate Junctions, equal-length SafeBoost/RiskyRepair alternatives, shared rejoin nodes, per-node distances, forward-looking commit zones 10–46 route units before each gate, dangling-edge/reachability/contract validation, and a deterministic fingerprint.
- [x] `VoidlineRouteDirector` generates and validates graph metadata at route creation; default planned junction count is 7. Visible spatial forks and player branch selection are not implemented yet (Phase 2).
- [x] Unity recompile exited with code 0; live `console_status` reported `compilationFailed=false`, `compiling=false`, and `consoleErrors=0` after the update.
- [x] Unity Pipeline `run_script` smoke test completed successfully with no diagnostics: 64 seeds, 2,368 graph nodes checked, 448 junction records checked, same-seed fingerprint stable, 64 distinct mainline module sequences, all generated graphs passing `Validate()`.
- [x] Fixed `VoidlineRouteDirector.EnsureControlPoint` so a cleared non-serialized spline cache is rebuilt from the current seed instead of indexing an empty list. After clearing old Console history, the post-fix Editor observation recorded zero new exceptions. An earlier run produced repeated `ArgumentOutOfRangeException` entries from `EnsureControlPoint`; these are historical pre-fix evidence, not a current compile error.
- [x] Fresh Play Mode time-advance probe after the cache fix: Unity time moved from 7.981s to 26.529s, distance from 112.868 to 358.089, player route-frame error stayed at 0.000, graph validation stayed valid with 7 junctions, and fingerprint stayed `6C7CDD5A`. After stopping Play Mode, live Console ground truth was `compilationFailed=false`, `compiling=false`, `consoleErrors=0`, `consoleWarnings=0`.
- [ ] Rendered capture and full visual/gameplay testing remain open; the probe verifies time advancement, route-frame alignment and graph metadata, not the quality of a visible route fork.
- [ ] Still open: Phase 2 must render actual branch entrances, honor in-flight player choice, route hazards/pickups/landmarks to the selected branch, and reconverge in geometry without teleporting.
- [ ] Existing Windows x64 executable remains stale; player build and standalone launch are still unverified due previously recorded Unity Package Manager/Pipeline build failures.


## 2026-10-12 — Multi-route prototype removed; single-route continuity verified

- [x] User explicitly rejected the multi-route system. Removed `VoidlineRouteBranchController` from the active `/CorridorRoot` scene via Unity Editor API and saved `Assets/VOIDLINE/Scenes/main.unity`.
- [x] Removed the route-branch controller/graph scripts and the branch-graph smoke test from the active project. Preserved these files at `D:\\dump\\VOIDLINE_Backups\\2026-10-12_remove_branch_system`; no branch source remains under `Assets/VOIDLINE/Scripts`.
- [x] Removed branch choice state/offset APIs from `VoidlineRouteDirector`, removed branch hooks from `CorridorScroller`, and removed branch-spawn/reward/callout logic from `VoidlineGameManager`.
- [x] Removed the stale HUD instruction `FORK: STEER LEFT OR RIGHT`; controls now describe only flight, boost, roll and pause.
- [x] Single-route runtime probe after cleanup: at time 6.133s, distance 73.842 and 1 intro segment remained active; at time 27.451s, distance 329.649 and `activeIntroSegments=0`. Player-to-route-frame error remained `0.000` throughout the later probe. Another fresh run reached time 41.685s and distance 500.466 with `activeIntroSegments=0` and frame error `0.000`. The intro pieces are retired rather than wrapped back into view.
- [x] A 1280x720 post-intro Game View capture was reviewed and moved outside the Unity import tree to `D:\\dump\\VOIDLINE_QA_Captures\\2026-10-12_single_route_after_intro.png`; the updated HUD capture is `D:\\dump\\VOIDLINE_QA_Captures\\2026-10-12_single_route_clean_hud.png`.
- [x] Fresh Console sample during a 41-second run recorded `compilationFailed=false`, `compiling=false`, zero errors and zero warnings.
- [x] Recompile exited with code 0 after removing the branch logic and again after the Game Over text edit. The active source's Game Over message now ends at the sector/route line; the button itself is the sole visible `RESTART RUN` action.
- [ ] Game Over overlay layout after the final text edit is **not** visually verified yet. A screenshot taken from an already-running Play Mode session still contained the old instruction because the loaded runtime assembly had not refreshed; a subsequent fresh `editor_play` command timed out. Do not mark the overlay layout fix visually passed until a fresh Editor Play Mode session can be entered and captured.
- [ ] Gate crossing was observed previously (`gatesCleared=1` in a controlled run), but the second landmark's full crossing and transition behavior were not fully sampled in this cleanup pass.
- [ ] Windows x64 build/standalone launch remain unverified; the existing executable is stale and previous build pipeline attempts hit Unity Package Manager/BuildPlayer issues.

### Phase 2 / next phase
- Single-route continuity exit checks passed for the intro corridor and no-branch HUD/system cleanup. The route-graph concept is cancelled, not future work.
- Next active phase is Phase 3: improve the single-route environment rhythm and art direction using existing space/landmark assets, with fewer repeated corridor visuals and no newly generated beam lattices, rails or branch cue structures.


## 2026-10-12 — User correction: single route and darker space visuals

- [x] Reverted route branch offsets and graph ownership from `VoidlineRouteDirector`; restored one seeded spline centerline for all actors.
- [x] Removed `VoidlineRouteGraph.cs` from `Assets` and archived its source/meta plus temporary tests outside the project. Archived obsolete generated branch cue logic; `VoidlineRouteBranchController.cs` is temporarily a no-op shim to avoid a missing script on the serialized `CorridorRoot` component while Unity Editor main-thread operations are timing out.
- [x] Removed the generated Resources copy of `PF_Asteroid_MoonRock` and the unused branch-only skybox resource. That copied model existed solely for generated branch silhouettes; the original actual gameplay asteroid prefab remains intact.
- [x] Replaced the contents of `Assets/Resources/M_VOIDLINE_SpaceSkybox.mat` with the imported AllSky Free `Night MoonBurst` material while preserving the existing target `.meta` GUID. `VoidlineSpaceSequence` now loads that dark sky as the primary space panorama and sets ambient intensity lower with fog off.
- [x] Removed GameManager branch-choice, branch-reward, branch-callout and branch-specific spawn logic. Music receives single-route state only.
- [ ] Still open: Unity Editor/Pipeline `recompile` and Editor main-thread commands timed out at 30 seconds. Need fresh compile/Console and Game View confirmation once the Editor service responds; do not call these latest changes compile-verified yet.
- [ ] Still open: remove the inert no-op `VoidlineRouteBranchController` scene component using the Unity Editor API, then remove the compatibility shim. Do not hand-edit `main.unity` YAML unless the Editor API is proven unrecoverable and a backup/recovery path is agreed.
- [ ] Still open: visually inspect the actual gameplay camera for the dark MoonBurst sky, verify the disliked smooth oval objects are gone after a fresh scene run, confirm the starter tunnel remains permanently retired, and test ambient/boost/critical music layer changes.


## 2026-10-13 — Music-intensity memory and formation timing (source changes)

- [x] Confirmed the Space Threat pack presents three different complete versions (Energetic Powerful 1:10, Electronic Dramatic 1:30, Slow Ambient 2:24), not three stems. Product reference: https://www.gamedevmarket.net/asset/space-threat-free-action-music.
- [x] Reworked VoidlineAdaptiveMusic so boost intensity is remembered independently of boost fuel: 6.5-second hold plus an 8.5-second easing release. Near-miss flow, rolls and landmark crossings can refresh it.
- [x] Energetic and dramatic arrangements are demand-started from their authored beginning, then fade out and stop. Ambient and energetic states crossfade rather than layering two complete mixes. Critical-hull audio uses the dramatic variant, not a duplicate second AudioSource of the same clip.
- [ ] Still open: listen in a fresh live run. The energetic phrase must persist after boost fuel expires, return to cruise without a sudden dip, refresh correctly during a second boost/near-miss, and stay intelligible at critical hull.
- [x] Changed pair depth to speed-scaled stagger and limited encounter-centre Z jitter to ±2.5 m.
- [x] Formation pacing now budgeted from pair depth, worst-case centre-jitter delta, base cruise speed, a small margin and a short recovery beat, rather than the generic minimum spawn interval alone.
- [x] Ran the actual VoidlineEncounterDirector source in an isolated seeded fuzz harness for 500,000 plans across 1,000 seeds, alternating cruise and boosted movement speeds. Zero safe-lane clearance violations, out-of-bounds hazards, peak/recovery violations, repeated peak patterns or invalid hazard counts. Pair member arrival gap was 0.75–1.0 seconds for tested speed inputs.
- [ ] Still open: one Unity Play Mode observation must verify the next formation does not overtake the second member of an earlier pair and that recovery beats feel intentional. The isolated harness is not a physics/collision simulation.
- [ ] Still open: Unity source compile and shader/material import after the latest changes; earlier import log reported a SourceDataChangedDuringImport rescan. No fresh compile error was observed in the last filtered log sample, but this is not a substitute for a clean current Console.






## 2026-10-13 — Bounded impact response (Phase 5 source change)

- [x] A survivable collision now applies a short, bounded displacement away from the impact source (0.42 world units over 0.22 seconds by default).
- [x] Steering input remains active; the displacement uses the existing horizontal/vertical lane clamps and does not add a control lock or alter boost fuel.
- [x] Lethal hits skip the nudge; control disable/reconfigure clears any pending displacement.
- [ ] Still open: perform one controlled side hit and one upper/lower hit at middle lane and near each lane edge. Confirm visible displacement direction, clamp at edges, one hull loss per cooldown, immediate steering recovery, and no nudge after Game Over.

## 2026-10-13 — Runtime camera effects survive script refresh

- [x] VoidlineSpeedEffects now searches the existing camera child and uniquely named runtime Volume profile before constructing another velocity streak mesh or URP post-processing profile.
- [x] The recovery path rebinds MeshFilter/MeshRenderer, falloff texture, MotionBlur, Vignette, ColorAdjustments, Bloom, ChromaticAberration and FilmGrain from the existing objects.
- [x] If it finds a partial stale rig, it renames the generated objects before deferred destruction so an immediate second Configure call cannot rediscover them.
- [ ] Still open: after the next successful compile, enter Play Mode twice and inspect that only one “VOIDLINE / Peripheral Velocity” child and one “VOIDLINE Runtime Speed Profile” Volume exist, and that boost blur/streak response still operates.

## 2026-10-13 — Environment lifecycle and runtime allocations

- [x] Replaced per-rock Destroy-on-cleanup with a reusable pool for distant backdrop asteroid scenery.
- [x] Reset restores poolable scenery and discards sector monuments by role; a HashSet protects large sector cue objects from accidentally entering the ordinary asteroid pool.
- [x] The pool caches each prefab instance's original local scale, uses deterministic route-relative placement/rotation, and makes the scenery non-interactive before reactivating a newly created instance.
- [ ] Still open: run a sustained 30–60 second session and at least one restart. Watch for stale scale/position, duplicate instances, sector-monument reuse as rocks, visible pop-in, or collider activity. Source changes do not count as runtime acceptance.
- [ ] Windows x64 build remains stale; do not release until Package Manager/build recovery, fresh BuildReport success, and a standalone gameplay run are completed.


## 2026-10-13 — Cruise/boost exhaust silhouette, obstacle fairness and fatal-impact beat

- [x] Changed plume membranes from a fixed 2.45× transverse scale to a continuous 0.90× cruise → 2.68× boost width envelope.
- [x] Reduced sheet idle alpha from 0.76 to 0.42 and idle glow from 0.82 to 0.54, while slightly strengthening travelling deformation; boosted glow changes only 2.35→2.23 (about 5%), deliberately a small reduction.
- [x] Sheet cache recovery now reacquires the original FBX mesh by child name before creating a fresh runtime dynamic mesh, rather than treating an already animated copy as its rest pose. Prior runtime mesh copies are retired on rebuild.
- [ ] **Visual gate still open:** the most recent boost screenshot still reads as thin engine needles with membranes barely visible. The code changes are present, but the current capture did not demonstrate the intended silhouette improvement. Re-enter Play Mode after the current compile, inspect ribbon scale in the actual boosted frame, and adjust geometry/visibility only from that evidence.
- [x] Near-miss clearance now scales with the actual pooled asteroid size; larger obstacles require wider clean clearance, while the reward still begins outside the existing normalized collision footprint.
- [x] Added a directional final-collision animation: craft-only pitch/roll attitude loss, short camera hit, terminal nozzle discharge, slow environmental drift and result-overlay delay (1.12 s). Upright collision/control root is retained; player input shuts off immediately.
- [x] Documented obstacle-avoidance research and applied design rules in Docs/RESEARCH_SPACE_FLIGHT_FEEL.md: reaction-time budgets, readable pattern grammar, safe-lane feasibility, near-miss incentives, generous fairness margins and layered death feedback.
- [ ] **Gameplay acceptance still open:** trigger three controlled hits with damage cooldown respected, capture the 1.12-second final-hit beat, verify results appear afterward, and restart. Then test one size-small and one size-large near miss at matched normalized clearances.
- [ ] **Current Editor is in Edit Mode** according to the last runtime snapshot (Application.isPlaying=False). The prior boost screenshot was genuinely captured in Play Mode; the latest scripts need a fresh Play Mode launch before current live-state checks.


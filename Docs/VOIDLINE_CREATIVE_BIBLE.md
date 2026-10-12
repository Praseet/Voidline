# VOIDLINE — Creative Bible & Technical Direction

**Decision date:** 2026-10-12  
**Status:** Locked direction for production; the seeded single-route spine, encounter pacing and tunnel retirement are implemented. Multiple routes are explicitly rejected; no branch cues, parallel route offsets or branch reward logic should remain active. The darker AllSky Free Night MoonBurst skybox and adaptive procedural soundtrack are integrated in source but require fresh Unity compile/runtime verification. Bespoke ship art and creature encounters remain pending.  
**Engine:** Unity 6.6 / URP, Windows x64 first  
**Project:** `D:\dump\Voidline`

**Execution note:** this bible owns creative intent and design rationale. Any phase sketches retained below are reference material, not the active schedule; always use `Docs/VOIDLINE_IMPLEMENTATION_ROADMAP.md` for current ordering, status and exit gates.

## 1. The definitive direction

**VOIDLINE is a high-speed sci-fi survival flight game about threading a fragile courier craft through an ancient transit network that is breaking apart—and something vast is waking inside it.**

It is a *cinematic arcade survival flyer built around line-reading and set-piece pacing*, not a flight simulator, conventional shooter, generic endless runner, or open-world space game. Preserve a controllable 2.5D flight envelope: the ship travels forward through the world while the player steers left/right and up/down, boosts, and barrel-rolls. The route itself becomes more expressive over time, so skill is about reading geometry, choosing a line, managing boost, and deliberately flirting with danger.

The experience needs three rhythms:
1. **Flow:** readable flight, steering, collecting, and learning the route's visual language.
2. **Pressure:** authored hazard formations and route geometry that force a choice.
3. **Wonder/dread:** giant transit structures, exposed starfields, and a creature whose size is understood through the world—not a health bar.

The game should feel precise first and physically convincing second. Use real transforms, collision envelopes and momentum-inspired response, but never let uncontrolled rigidbody chaos steal the player's ability to correct a mistake.

### Four pillars (use them as a filter for every feature)

- **Speed is a decision.** Boost spends a recoverable resource and increases approach speed. A skilled player uses it to chain clean passages and reach optional rewards; holding it constantly is not the dominant solution.
- **Every hazard has a readable answer.** The player may need to react fast, but not to impossible overlap, off-screen attacks, or a pattern with no viable path. Difficulty comes from combining known rules under pressure.
- **The route tells a story.** Architecture, lighting, debris, sound and one enormous landmark communicate the sector's identity. A gate is a real milestone, not a prop.
- **The unknown is systemic, not random noise.** Route grammar creates authored situations; a pacing director varies timing and mixes patterns. A later creature reacts to the player but does not cheat or attack continuously.

## 2. Core loop

**Read the flight line → steer/roll/boost → resolve a near miss, collision or pickup → cross a landmark → release pressure briefly → meet the next sector.**

A new player should understand steering, boost and roll in the first 10 seconds, see the first real decision inside 15 seconds, and experience the first landmark in roughly the opening half-minute. That is a tuning target, not a promise about the current build.

A sector crossing should punctuate a run roughly every 25–40 seconds after the opening. Use the existing gate/arch system to change the *shape and mood* of the next section. Avoid long empty stretches and avoid an uninterrupted, monotonically increasing wall of hazards.

### Controls (preserve existing bindings unless testing proves a change is better)

- Move: WASD / arrow keys / gamepad left stick.
- Boost: hold Shift / gamepad left-stick press (existing action mapping).
- Barrel roll left/right: Q/E / gamepad shoulder buttons.
- Pause: Esc.
- Restart after a run: R/Enter or the existing Restart UI action.
- Mouse remains for UI, not aiming. Avoid adding combat/aiming until the flight loop is great.

The ship stays within a controlled local flight envelope. The root owns the collision shape; only the visual child banks and rolls. Steering should start quickly and settle smoothly: neither instant digital teleport nor slippery zero-grip drift. Roll is a timed defensive commitment with a clear start, invulnerability/dodge window, recovery and cooldown. Tune the window with observed collisions, not by feel alone.

## 3. Route generation: authored grammar, constrained variation

**Do not build an infinite route from unstructured randomness.** Use deterministic seeds and a library of hand-authored modules. Designers own the quality of each module; the generator owns the order, variant, danger profile and pacing. This is the proven trade-off between authored quality and run variation described in the Rogue Singularity procedural-generation postmortem/article [1].

### Architecture: two cooperating directors

1. **Track/Route Director (long horizon):** composes tunnel modules 3–5 modules ahead. It owns a seed, route distance, lane-frame transform, segment tags, section identity and re-use/spacing rules. It never creates an unavoidable hard collision.
2. **Encounter/Pacing Director (short horizon):** chooses a validated hazard formation, pickup placement and a brief recovery beat for the currently active route module. It accounts for elapsed time, sectors, recent damage, hull, boost use and recent success without punishing a good player with pure density.

These remain separate from `VoidlineGameManager`, which coordinates run state, score, hull, pause/restart and HUD. The environment owns segment placement and landmark fades; individual encounter prefabs own their local animation and collision.

### Initial segment kit to author

Use the existing tunnel segments, basalt/steel materials, Ancient Transit Gate, Hollow Asteroid Arch, MoonRock and crystal asteroid assets as the first art foundation. Extend the kit in Blender, not by filling every gap with primitive shapes.

| Module family | Gameplay purpose | Visual signature |
|---|---|---|
| Spine / breathing run | Recovery, let the player read the ship | Long structural ribs, sparse cyan guidance |
| Offset chicane | Teach left/right route commitment | Asymmetric trusses and staggered light bars |
| Vertical shear | Exercise up/down steering | Hanging supports, floor apertures and upward rails |
| Broken conduit | Force a clear gap or roll decision | Missing panels, loose cables, exposed service frames |
| Basalt cathedral | Slow-looking scale beat and silhouette contrast | Dark mineral ribs, sparse amber warning lights |
| Debris ribbon | Near-miss and flow opportunity | A small number of layered, readable rock clusters |
| Open-void breach | Change visual scale and soundscape | Tunnel silhouette fades away; gate/arch frames the nebula |
| Salvage/scrape corridor | Optional high-risk reward line | Clearly marked rails, repair cell, stable safe path |
| Transit gate | Sector change and release beat | Iconic frame with strong silhouette, not a particle wall |
| Offset hazard line | Meaningful flight-line choice within the same route | One continuous space with a readable safe gap and an optional risky near-miss |

Start with 6 genuinely distinct high-quality modules, not 20 shallow ones. Each module definition should include: length, entry/exit connector, allowed curvature/vertical offset, lane width, valid obstacle sockets, tags, minimum sector, difficulty band, cooldown/repeat rules, lighting palette, audio mood and seed/version. Build deterministic seeded output so any bad run can be reproduced.

### Anti-boredom rules

- Use a rhythm such as **relief → telegraphed setup → one main challenge → optional reward → release**.
- Never place two high-complexity formations back-to-back without a clear recovery lane.
- Do not repeat the same module family, obstacle arrangement or major landmark silhouette immediately.
- Change more than color: alternate visual scale, corridor openness, route curvature, hazard timing, silence/audio intensity, and reward placement.
- Every sequence must retain a guaranteed path at least as wide as the ship collision envelope plus a tunable clearance margin.
- Telegraph a safe flight line through the current hazard formation; make the alternative line visually readable but never create a parallel route or route-choice system. Risk/reward lives in the flight line, boost timing, near-miss and pickup placement.
- Use a fixed seed per run (and show it on the post-run screen / diagnostics) so generation bugs can be reproduced.

Build route *choreography* and safe formations on one continuous seeded path. The centerline may curve, but ship, hazards and landmarks must all query the same route frame; do not ship visual-only bends that contradict gameplay.

## 4. Difficulty that feels hard but fair

Use a **tension director**, not a continuously accelerating difficulty slider.

- **Opening:** one hazard at a time; demonstrate the safe lane and make the controls legible.
- **Learn:** introduce singles, staggered pairs, pickups and a low-risk first near miss.
- **Combine:** combine known patterns with mild route bends; leave more than one reaction window.
- **Test:** use a signature formation or tighter optional route; pay well for clean execution.
- **Release:** a short breathing segment, visual reveal or reward after a hard passage.
- **Escalate:** increase pattern combinations and route geometry variety before shrinking reaction time or spamming more hazards.
- **Low-hull recovery:** reduce stacked complexity and make a recovery pickup plausible, but do not guarantee free health every time the player makes a mistake.
- **Mastery:** a strong player sees more interesting choices and richer risk/reward lines, not simply more unavoidable projectiles.

Difficulty should rise primarily by **combination and decision load**, secondarily by encounter density/speed. The existing gradual spawn interval can remain a final pressure layer, but it cannot be the entire difficulty curve. The pacing principle from *Alien: Isolation* is useful here: threat intensity must vary; permanent pressure exhausts the player, while a responsive director can create the impression of intelligence without cheating [2].

### Reward design

Keep one readable score economy. Existing survival score, gate/arch bonuses, repair cell score, clean passes, rolls, near misses and a short timed flow chain are enough for the prototype. Ensure a hazard cannot score as both a hit and a near miss; reward a near miss only after a genuinely safe clearance has passed the collision envelope.

Future run-only reward choices (after route generation is reliable):
- At selected gates, offer **two temporary protocol upgrades**, not a shop: e.g. longer boost reserve vs a stronger one-hit scrape guard.
- Let the risk route reward a larger flow multiplier or a repair/boost cell, never an essential key required to progress.
- Give each reward a trade-off and clear UI copy. No currency, crafting tree, online leaderboard, loot rarity spreadsheet or permanent upgrade economy in the initial game.

## 5. Dynamic camera: one controlled system

Stay with a small custom camera rig for now; do not add Cinemachine just to get a feature name. The project already has `VoidlineCameraEffects` and the route should provide a forward tangent / lane frame.

Camera behavior must be composed from independent, bounded channels:
1. **Base follow:** smooth, damped follow of the route frame and ship, preserving safe framing at the lane edge.
2. **Look-ahead:** aim a short distance along the route tangent and modestly bias toward the player's current velocity—not directly at twitchy raw input.
3. **Curvature anticipation:** begin a gentle pan/lean before a bend, with different tuning for horizontal vs vertical changes.
4. **Speed:** boost increases FOV modestly and adds a controlled engine/parallax response; FOV returns smoothly when boost is released.
5. **Impact:** brief directional camera kick away from the source, layered with sound, spark/debris and a hull cue. Do not stack unbounded random shake.
6. **Set-piece framing:** during a gate/creature reveal, permit a small bounded composition shift while retaining enough view of the ship and next hazard.
7. **Accessibility:** intensity setting, shake off, minimal roll/flash option; never use constant blur or full-screen red flashes to convey routine hits.

Unity's camera guidance identifies look-ahead, damping and screen composition as the key tuning controls and warns that noisy look-ahead can amplify jitter [3]. Apply the GDC camera/shake principle: keep kicks short, cap the total and let players reduce or disable them [4]. Motion should help read the route, never hide a hazard.

## 6. Physics: believable feedback, arcade control

Do not attempt full 6-DOF Newtonian flight. Keep the current kinematic/trigger collision architecture until there is a measured reason to change it. Borrow **mass cues and inertia**, not uncontrollable simulation:
- Ship visual banks into turns and settles with damped spring response; root stays in the route frame.
- A glancing contact creates a short directional displacement/steering disturbance, engine sputter and scratch/spark effect; the player can recover.
- A damaging asteroid hit has one consistent hull consequence, an impact impulse, low-frequency audio, short transient flash and a rapidly decaying camera kick.
- Roll has a clear dodge window; visuals and sound communicate when protection begins/ends. No collision mesh should rotate with the roll.
- Rocks can tumble from local impulse and spin while moving down-route; their gameplay colliders remain simple.
- Pooling must reset every state (speed, resolution flag, near-miss check, material/particle/audio state and owner references). Unity's pooling guidance specifically targets repeated instantiation/destruction overhead [5].
- Camera kick, hitstop and shake must remain bounded. Test whether any hit pause improves impact readability; never add it to near misses, routine pickups or menus by default.

Naughty Dog's GDC discussion of physics animation is a useful reminder: raw physics can initially look terrible; predictable art-directed motion gave the team control while preserving physicality [6]. The target is *felt force with reliable agency*, not simulation purity.

## 7. Alien threat arc — build toward it, don't rush it

Introduce the creature as a three-stage promise:

**Stage A — Distant witness (visual/audio only):** enormous movement behind the gate or across an open-void breach; scale communicated by the structures it eclipses, shadow displacement, low-frequency audio and brief silhouette. It does not damage the player yet.

**Stage B — The Clinger (first playable creature):** a small biomechanical leech hooks onto a wing or engine. It tilts handling, makes boost less efficient and adds a persistent audible/visual cue. It does not randomly drain all hull. Give the player a readable grace window and a distinct scrape-off solution.

**Stage C — Scrape-off set-piece:** the route deliberately offers an asteroid edge / sacrificial rail with a highlighted scrape surface. The player must line up a very close pass to shear the creature off. Success creates a dramatic peel-off animation, directional impact feedback, a short boost/flow reward and a release beat. A miss causes a controlled penalty and another opportunity later, not immediate unavoidable death.

**Stage D — Colossal abomination (after the route is fun alone):** a sector-scale presence changes route structure, sweeps across one branch or forces the player to shelter behind a gate rib. It is an encounter director/set-piece, not a bullet sponge. No combat/weapon system until the scrape, routing and handling are proven fun.

The creature should be telegraphed by motion, sound and route response. It must have rules the player can learn. Build the visual, audio and pacing foundation first; the creature's AI is not a substitute for a good flight loop.

## 8. Final art direction and production asset choices

**Style:** "industrial cosmic gothic": monumental ancient transit architecture, brutalist/megalithic silhouettes, mineral/ceramic surfaces, sparse signal lights, expansive ink-black space. Avoid generic neon cyberpunk, noisy emissive everywhere, AI-slop icons, piles of unrelated marketplace props, and noisy detail that reduces hazard readability.

**Palette:** near-black/navy negative space; cold gunmetal and oxidized steel structure; dark basalt/mineral masses; bone/ivory ceramic panels on the hero craft; restrained cyan/teal guidance and engine output; amber only for warnings, heat and damage. Keep most architecture non-emissive. A rare white/cyan gate aperture can own the frame.

### Chosen asset set (existing foundation + bespoke hero assets)

1. **Hero ship:** replace the current placeholder/free-pack-looking craft with one original Blender-built courier/interceptor. Long readable center spine, two asymmetric engine nacelles, chunky replaceable ceramic armor panels, bright engine cores, one damageable wing/engine silhouette. Model LOD0 target 15–25k triangles, LOD1 5–8k, LOD2 1–2k; one 2K base-color/normal/packed-mask material set as the first target. No need for bespoke skeletal animation; use procedural banking, engine gimbals and emissive/pulse response.
2. **Tunnel architecture kit:** preserve existing panels and reuse industrial steel + basalt textures; add a shared trim-sheet/atlas, 6 strong modular segment silhouettes, cross-ribs, broken beam/rail, hanging supports, service cable, and a few large structural set pieces. Prefer vertex color/property blocks and shared materials over material-instance explosion.
3. **Hero landmarks:** keep and polish `VOIDLINE_AncientTransitGate.fbx` and `VOIDLINE_HollowAsteroidArch.fbx` sourced from `Assets/VOIDLINE/Art/Blender/VOIDLINE_SpaceLandmarks.blend`. They are visual anchors and sector milestones, not clutter. Build at least two gate silhouettes (intact + breached) from the same kit later.
4. **Asteroid set:** keep `PF_Asteroid_MoonRock`, `PF_Asteroid_Crystal`, `PF_Asteroid_Meteor` and the current basalt/moon-rock materials. Add large/small silhouettes and a clearly glowing *scrape rail* variant. LOD and collision proxies are separate; visuals must never dictate the gameplay collider.
5. **VFX:** authored sparks, engine trails, rock chips, gate aperture distortion, fine dust silhouettes and rare high-energy burst. Separate low-cost gameplay-critical VFX from optional background garnish. Keep particles pooled or finite, and never obscure the opening through a hazard.
6. **Audio:** keep distinct event cues, then add a small original sound library for engine beds, structural resonances, passing debris, metal scrape, gate charge, deep creature motion and rare silence. Use a few adaptive stems keyed to pressure (calm, movement, pressure, creature); don't play a loud sting on every event.
7. **Environment:** keep the existing space panorama as a starting backdrop but replace generic diffuse fog with a controlled hierarchy: black negative space, distant star field, localized nebula only for color contrast, and giant silhouette scale. A horizon should be meaningful.

### Asset QA and budgets

- Author source meshes in Blender; export clean FBX with applied transforms, consistent forward/up axes, correct scale, tangents/normals and named LODs.
- Use 2K tiling/trim material maps for architecture; use 2K hero-ship textures first; reserve 4K only for a hero asset that genuinely benefits after profiler review.
- LOD high/mid/low meshes, simple capsule/box/sphere colliders, shared materials, static batching or instancing where appropriate. Profile before increasing texture resolution, lights, shadows or full-screen effects. Unity 6 graphics profiling guides recommend measuring CPU and GPU/render work rather than tuning blindly [7].
- Target: stable 60 FPS minimum and 90 FPS goal at 1920x1080 on the RTX 4050/i7 development class hardware in the standalone build, with a lower-stress fallback quality preset. This is an engineering target, not a current benchmark claim.

## 9. Implementation order (the order is part of the design)

**Phase 0 — Recovery and trustworthy baseline**
- Active scene is `Assets/VOIDLINE/Scenes/main.unity` (not SampleScene).
- Check UPM/Console; use a fresh Editor compile. Preserve the older Windows build, current source changes, and backups.
- Ensure fresh-run state is explicit, independent of disabled domain/scene reload.
- Add seed/diagnostic output and reproducible run conditions.

**Phase 1 — Core flight and feel**
- Make player route-relative movement explicit and deterministic; document local lane bounds.
- Improve bounded dynamic camera and directional hit/near-miss/boost feedback.
- Fix collision/near-miss sequencing, pooling reset and boost depletion/recharge feedback.
- Acceptance: repeated fresh runs, keyboard/gamepad, collision, roll, boost, repair, restart, pause; zero unexpected Console errors.

**Phase 2 — Fair procedural encounter director**
- Replace single random spawn timer as the sole difficulty system with a validated, seeded pattern library and tension/recovery beats.
- Add safe-lane and minimum reaction-time checks; sequence diagnostics contain seed, sector, pattern id and lane.
- Acceptance: replay the same seed; pass 30+ generated patterns without unavoidable overlap; show no consecutive peak encounters; verify reduced hull produces a fairer composition rather than free health.

**Phase 3 — Route grammar and curved track**
- Introduce seeded route modules with entry/exit connectors, width, curvature, obstacle sockets, palette/audio tags and repeat cooldown.
- Generate 3–5 modules ahead, recycle segments and keep objects in the same route coordinate frame. Ensure the ship, camera, hazards, pickups and landmark crossing planes all share the route frame.
- Start with gentle S-bends and vertical shear only; then implement one safe/risk fork that rejoins.
- Acceptance: 10 minutes with no seam/gap, no detached obstacles, stable camera, clear route read, fixed-seed reproduction, no unplayable route. Do not call it dynamically routed until all gameplay objects follow the curve correctly.

**Phase 4 — Reward depth**
- Tune flow chain, near misses and sector rewards using observed distributions, not intuition alone.
- Add two-choice *run-only* protocols at selected gates only after basic rewards are stable.
- Add risk/reward forks and one salvage/scrape corridor.
- Acceptance: player can explain why the riskier route pays more; no essential progression depends on it.

**Phase 5 — Art vertical slice**
- Finish one hero ship, 6 tunnel modules, one polished gate/arch, 3 asteroid families, one unique breach/open-space reveal, real particle/audio cues and a coherent lighting pass.
- Replace primitive/temporary assets only when the final asset is imported, collision-tested and profiled.
- Acceptance: the first 30 seconds look coherent in a capture; a distant, mid-distance and close view all hold up; no generic icon packs.

**Phase 6 — Creature prototype**
- Create the distant witness first, then the Clinger with an attachment point, grace timer, gameplay effect, scrape surface and success/failure feedback.
- Add only after route-relative collision and pacing are stable.
- Acceptance: at least one clear, learnable scrape-off attempt; creature can never attach from an unseen/invulnerable angle; recoverable miss path exists.

**Phase 7 — Giant set-piece and route consequences**
- A rare creature crossing forces a temporary route change, gates close/open or a branch choice.
- Build one memorable 60–90 second authored encounter using existing route grammar, not a new combat mode.

**Phase 8 — Performance, accessibility and release**
- Profile CPU/GPU, memory, asset residency, GC allocations and worst-case hazard waves in standalone.
- Add camera motion/shake intensity, flash reduction and sound level controls; verify HUD at 16:9 and ultrawide.
- Run deterministic seeds, score/hull edge cases, 10-minute stress, repeated restarts, all control paths and standalone build launch.
- A successful compile, Editor runtime check, completed BuildReport and standalone launch are separate release gates.

## 10. Out of scope until the route is fun

No multiplayer, persistent gear tree, currency/crafting economy, weapon inventory, open-world navigation, branching story campaign, NPC dialogue system, loot-rarity system, or giant boss with health bars. These do not improve the current moment-to-moment promise as directly as ship feel, routes, fair pressure and creature scraping.

## 11. Research references and practical takeaways

1. Scott Beca, Game Developer, [Procedural generation: a primer for game devs](https://www.gamedeveloper.com/design/procedural-generation-a-primer-for-game-devs) (Rogue Singularity). Uses a generated spline, designer-authored blocks, obstacle sockets, weighted variants and seeded replay; it also warns that pure randomness becomes samey and difficult to QA. This is the foundation for route grammar.
2. Tommy Thompson / AI and Games, Game Developer, [The Perfect Organism: The AI of Alien: Isolation](https://www.gamedeveloper.com/design/the-perfect-organism-the-ai-of-alien-isolation). A macro pacing director and the creature's own local behavior work together; pressure changes so it does not exhaust the player. Use as a later threat model, not a reason to add an alien now.
3. Unity Cinemachine guidance, [Composer/look-ahead/damping](https://docs.unity.cn/Packages/com.unity.cinemachine%402.6/manual/CinemachineAimComposer.html). Look-ahead can amplify noisy motion; damping and composition must be tuned. VOIDLINE stays on its current custom rig for now and uses the same principles.
4. GDC Game Development Constitution, [Use camera motion and screenshake to convey force—but dose it and let players control it](https://adventuresincausality.com/principles/GDC-L1-FEEL-0006/). Scale by event strength, cap total shake and provide an off/reduced setting.
5. Unity Learn, [Use object pooling to boost performance](https://learn.unity.com/tutorial/use-object-pooling-to-boost-performance-of-c-scripts-in-unity). Pool repeated obstacles, debris and transient effects, and reset all per-instance state on reuse.
6. GDC Vault / Naughty Dog, [Physics Animation in Uncharted 4](https://www.gdcvault.com/play/1024087/Physics-Animation-in-Uncharted-4). Raw physics did not initially produce the wanted feel; art-directed, predictable movement retained control. Aim for convincing feedback, not uncontrolled realism.
7. Unity 6 docs, [Graphics performance and profiling](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling) and [Profiling in URP](https://docs.unity.cn/Documentation/Manual/graphics-performance-and-profiling-in-urp.html). Measure frame time and CPU/GPU bottlenecks before allocating a larger rendering budget.
8. GDC, Charlie Cleveland, [The Design of Subnautica](https://www.gdcvault.com/play/1025745/The-Design-of-Subnauticann). Maintain a small set of design pillars, prototype emotion early, and let mystery/exploration add structure without over-directing the player.

### Decision rule

If a proposed feature does not improve **route reading, steering/boost/roll skill, risk/reward, sector identity, physical feedback, or the creature's threat fantasy**, defer it. Quality comes from a few interlocking systems that reinforce each other.


## 12. Implementation status ? first route/pacing slice (2026-10-12)

- Implemented: VoidlineRouteDirector generates a repeatable seeded Catmull?Rom X/Y centreline from bounded route knots. It exposes the current route seed, section, centre offset, forward tangent and track rotation.
- Implemented: CorridorScroller tracks travelled distance, recycles tunnel segments and aligns their position/orientation against the same route spine. VoidlinePlayerController now steers in route-local lane coordinates, while asteroids, repair cells, landmarks and the camera read from that frame.
- Implemented: VoidlineEncounterDirector uses the route seed to schedule singles, offset pairs, cross-weaves and later vertical weaves. It chooses a clear lane, tests hazard separation, inserts a recovery beat after a peak, and softens patterns at one hull. The GameManager consumes the plan and its pacing multiplier.
- Implemented in scene: VoidlineRouteDirector is attached to CorridorRoot in Assets/VOIDLINE/Scenes/main.unity; scene saved through Unity Editor API.
- Runtime evidence: in a fresh 20-second Editor run, the route seed was -1089701328, distance 216.87, route section 2, player frame error 0.000, hull 2, one transit gate cleared, and the current pattern was CrossWeave with a central safe lane. Console showed zero errors/warnings in that run.
- Lifecycle: Enter Play Mode now skips domain reload but reloads the scene. This keeps script iteration reasonably fast while resetting scene components per run. A 4-second follow-up began with three hull, zero score/sector/near misses, lane (0,0), and player frame error 0.000.
- Historical experiment (2026-10-12): a seeded route graph and branch metadata passed a 64-seed smoke test, but the multi-route design was subsequently rejected. Its controller/graph scripts and smoke test were removed from the active project and archived outside `Assets`; single-route centerline behavior remains the only active route system.
- Still not implemented: distinct single-route environment modules, telemetry-backed pacing tuning, hero ship replacement, Clinger/abomination encounters, creature scrape surfaces, accessibility UI, and a current Windows player build. Multi-route branches are explicitly de-scoped.
- Camera baseline verified: the active main scene's serialized Main Camera is 65 FOV and runtime reads 65.0; the earlier value near 76 came from the previously open SampleScene, not the game scene. No FOV defect is indicated by that comparison. Keep the existing bounded boost increase (8.5 degrees) and tune only after visual review.


## 13. Archived multi-route experiment — superseded 2026-10-12

**Historical only:** the proposal below was prototyped, then explicitly rejected by the user. Do not treat any fork, parallel branch, branch-reward or generated route-cue instructions in this section as active design work. The active game stays on one continuous route; see `Docs/VOIDLINE_IMPLEMENTATION_ROADMAP.md`.

The first route spine is only a movement-coordinate primitive. It does not solve the design problem by itself. The game must not be described as having dynamic routes while every run still presents one forward corridor with different obstacle placements.

### Route system v2: a graph of authored spaces

Build a continuous forward-moving world from authored spatial modules. Each module has an entry frame, exit frame(s), playable clearance envelope, hazard sockets, landmark sockets, and an encounter role. Connect modules as a directed graph. Select edges using the run seed, difficulty budget, recent pattern history, player state, and optional risk/reward preference. Preserve a reachable mainline while presenting genuine choices.

First route vocabulary:
- **Transit Spine:** fast, readable, safer; strong boost lines and fewer repair opportunities.
- **Fracture Run:** an open asteroid field with several possible flight lanes, more physical debris and optional repair caches.
- **Hull Breach:** a ruined transit structure with rotating/sliding wreckage and narrow, readable gaps.
- **Gravity Wake:** a large moving body or alien structure bends the route around itself; the camera and ship orient to the changing tangent.
- **Exposed Void:** a short open-space release with large-scale celestial scenery, then a clear approach cue back into a structure.
- **Scrape Corridor:** deliberate hard-surface near-miss geometry, reserved for the attached-creature escape sequence.
- **Gate Threshold:** a major route junction/sector transition, not merely a background arch.

### Actual route choice
- Show two forward route openings before the decision point; each opening must be visually legible at gameplay speed.
- Let the pilot commit using lateral steering and a strong roll/lean, not a menu. Do not require a new input if the current control language already communicates the choice.
- Route A should trade safety for speed; route B should trade speed for resources, visibility or a different encounter. Both must be physically traversable.
- The chosen route's geometry, hazard schedule, landmark, audio, and reward must agree. Never spawn a reward behind an impossible turn or seal every lane with a random obstacle.
- Add reconvergence after a short branch to preserve forward momentum and keep route state bounded. Branch length and rejoin location are authored, not random.
- Make the branch preview readable through lighting, silhouette, color/material language, gate markings, and motion; do not depend on tiny HUD labels.

### Continuous generation without repetition
Use a constrained grammar, not a random prefab roulette:
1. Choose the next **beat** (release, decision, traversal challenge, reward, chase, recovery, landmark).
2. Choose a compatible module from the library and connect entry/exit frames.
3. Check geometric continuity, collision clearance, visibility/read time, route reachability, hazard density, and recent-pattern repetition.
4. Place encounters only into module-authored sockets and validate them against the actual route.
5. Keep a short future horizon committed so the camera can reveal choices and the player never sees geometry pop in.
6. Pool whole modules and their attached actors. Recycle after the rejoin and camera-safe distance.
7. Record seed, module IDs, graph edges, selected branch, encounter IDs, hull, near misses, route speed and death cause. Replay a seed to reproduce unfair situations.

Variation comes from module order, branch selection, moving geometry, event timing, celestial scale, lighting state and encounter intent?not merely asteroid positions. Maintain a recent-history window to avoid repeated beat/module combinations.

### Vehicle / camera contract
Keep the ship genuinely controllable within the local route frame, with momentum and damped acceleration, readable lateral/vertical limits, and a boost that trades steering authority or heat for speed. The route frame must not auto-steer the ship through hazards. Let the ship bank/roll around its own longitudinal axis; route curvature influences camera anticipation and framing, not player input. Use bounded camera look-ahead, curvature banking, acceleration pitch, and event-sourced impact impulses. Camera effects should be damped and reducible, not random shake. A route branch should be visible before the camera commits to the new frame.

### Collision and creature encounter
Use arcade-physical collision: believable mass and impulse cues, but protect the player's ability to recover. Impacts should communicate direction and severity through ship recoil, a brief controlled speed loss, sparks/debris, low-frequency audio and a bounded camera impulse. Avoid physics forces that fling the craft into an unrecoverable death without a readable cause.

Creature escalation is a multi-beat system:
1. **Witness:** a colossal silhouette moves behind a gate or through exposed space; no attack.
2. **Pursuit:** a distant moving mass bends debris and changes route readability.
3. **Clinger:** a small/medium organism attaches to the ship, adds drag/roll bias and obscures a small part of the HUD/viewport. The pilot must keep control.
4. **Scrape-off:** the route deliberately offers two or more hard, broad scrape surfaces. A close pass removes the creature in stages; scrape direction, particles, sound and reduced drag confirm progress. The surface must have forgiving contact rails and a clear penalty envelope, not demand pixel-perfect collisions.
5. **Abomination:** only after the above loop is fun, a giant creature alters the route graph, blocks one exit, creates temporary passages, or tears through a module. Its body is an environmental event, not just a large health bar.

### First vertical slice to build
Implement one fully authored junction with two traversable branches and a reconvergent exit, plus one Fracture Run module, one Hull Breach module and a short Exposed Void transition. Place a repair cache on the riskier branch and a faster boost line on the safer branch, then measure which choice players take. Add one Clinger scrape-off prototype only after branch selection and collision response are reliable.

Acceptance: a single seed can produce at least three materially different route sequences; branch choice is made in flight and affects geometry/rewards; all exits remain reachable; no identical encounter beat repeats three times in a row; and the same seed reproduces the same graph and encounters. The earlier single-spline implementation is retained as the route-frame primitive but is not accepted as the finished route system.

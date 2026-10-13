# VOIDLINE — Space Flight Feel & Visual Research

**Research date:** 2026-10-13  
**Applies to:** Phase 3 environment, Phase 4 encounter grammar, Phase 5 hit/traversal feedback, Phase 7 camera/audio polish.

## Design target

VOIDLINE should feel like a highly advanced courier cutting through an immense, abstract space. Boost is the main power fantasy and must remain the strongest sustained sensory state. Ordinary steering should have a little inertia and follow-through, but never steal control. A hit should cost hull and interrupt flow without making a capable craft feel like an unprotected metal box hitting a wall.

## Findings and decisions

### Engine trails, boost, and apparent speed

- Unity documents that URP supports both Trail Renderer and Line Renderer, and custom trails can also be authored in VFX Graph: https://docs.unity.com/en-us/engine/7000.0/manual/visual-effects/lines-trails-billboards
- Unity's Visual Effect Graph is GPU-simulated and is intended for large, highly customisable particle populations: https://docs.unity.com/en-us/engine/6000.6/manual/visual-effects/vfxgraph
- Free asset scan (2026-10-13): [Trails VFX - URP](https://marketplace.unity.com/packages/vfx/trails-vfx-urp-242574) is free and compatible with Unity 6 URP, but is a general trail collection rather than a ship-engine system. [Unity Technologies Spaceship Demo](https://github.com/Unity-Technologies/SpaceshipDemo) is a strong reference for effect layering, but is HDRP. The older [Rocket Trail VFX Pack](https://marketplace.unity.com/packages/vfx/particles/rocket-trail-vfx-pack-192346) is HDRP-only and therefore a poor direct fit. We should not import a mismatched renderer setup or a generic effect only to repaint it.
- Decision: a reusable hand-authored mesh plume is the distinctive part of this engine design, with only a few particles reserved for boost ion breakup. The Blender source scene builds a thin hot spine, translucent envelope, three narrow counter-swept plasma membranes, three separated partial induction arcs, and two hairline filaments. It deliberately avoids fog, a dense ring cage, or wide opaque ribbon cards. Unity's low-cost particle layers stay available for time-varying core intensity and boost-only sparks. Keep the nozzle footprint narrow so thrust feels coherent with the craft instead of a diffuse fan.
- Speed should be layered rather than represented by blur alone: the ignition transient, sustained engine plume, motion-linked peripheral streaks, FOV expansion, modest object blur and highlight bloom each play a different role. Keep the central ship and safe lane crisp.

### Camera feel and impacts

- Unity's Cinemachine Impulse documentation models an event as a directional signal with a time response, rather than requiring a large undifferentiated camera shake: https://docs.unity.cn/Packages/com.unity.cinemachine%406.6/manual/CinemachineImpulse.html
- Decision: apply that principle directly in the existing lightweight camera script instead of adding Cinemachine. Boost keeps its strong focus-squeeze/release, forward dolly and rumble. Damage uses a short, directional shield-field shear, tiny camera parallax and a low-amplitude spectral flash. Hull loss and chain break remain the gameplay consequence; the camera should not imply a heavy physical crash.
- Ordinary craft steering is smoothed independently from roll. A 360° barrel roll is an explicit angular channel and must not be passed through the same shortest-path quaternion filter as the bank/pitch target. A short, cancelable settling motion supplies follow-through without delaying the next input.

### Sky, retro abstraction and post-processing

- URP's built-in Volume overrides include Bloom, Color Adjustments, Film Grain, Chromatic Aberration, Lens Distortion and Motion Blur. Unity notes that post-processing has a frame-time cost, so the effects need to be selective: https://docs.unity.com/en-us/engine/6000.6/manual/post-processing-and-full-screen-effects/urp/effect-list
- Candidate reviewed: Unity Asset Store's Procedural Space Skybox uses VFX Graph and includes tools to generate nebula textures; version 3.0 was noted in June 2025. It was not blindly imported because its runtime graph/renderer dependency and licensing/price should be verified against this project's URP build first: https://assetstore.unity.com/packages/vfx/shaders/procedural-space-skybox-295596
- Candidate reviewed: StarCanopy can bake coloured nebula/galaxy cubemaps to HDR formats, but its public repository describes the visual interface as future work. It is useful as art-direction inspiration, not a production-ready drop-in for this pass: https://github.com/chalkwalk/star-canopy
- Candidate reviewed: Unity-Stars adds a fullscreen procedural star pass, but requires a URP Full Screen Pass Renderer Feature and a very long camera far plane. That would touch project-wide render settings and is unnecessary for the current targeted art pass: https://github.com/B0XEY/Unity-Stars
- Decision: retain the existing licensed SpaceSkies Free Purple panorama as the local source art and keep the procedural fallback self-contained. Brighten the runtime tint/exposure and ambient light, then add a modest blue/cyan bias, saturated violet palette, gentle baseline bloom and very light grain. During boost, increase contrast/bloom and peripheral flow. This is a controlled stylisation layer, not a destructive replacement of the existing artwork. Licensed source files remain local and excluded from Git.

### Encounter rhythm

- Fixed periodic patterns are easy to learn and can make hazards feel like a metronome. Preserve seeded reproducibility and protected-lane metadata, but stretch the encounter cycle, add a readable wide-flank pair, and make clearance checks reflect hazard radius.
- Do not increase difficulty by only increasing count or spawn frequency. Keep short recovery beats after formations, and ensure the central/outer escape lane is wide enough for the real ship footprint.

## Implemented in this pass

- Starting and restarted runs begin in open space; the old tunnel geometry is hidden from the first gameplay frame.
- SpaceSkies receives a restrained exposure lift and a blue/cyan bias; fallback nebula bands are brighter and more saturated.
- Runtime speed grading adds a small baseline colour/contrast lift, restrained bloom and very light film grain, then strengthens boost contrast/bloom and edge streaks.
- Boost exhaust retains its narrow nozzle/core but now transitions the three plasma membranes from restrained wisps in cruise to a wider, travelling sheet silhouette under boost; peak shader glow is reduced by only about 5%.
- Survivable damage uses a short cyan directional field-deflection cue. Fatal contact now has a separate asymmetric camera kick, craft-only attitude loss, a brief ion discharge and a 1.12-second delayed results reveal; boost ignition remains a distinct event.
- Barrel roll is separated from smoothed attitude, preserves 360° travel, and adds a small settling tail.
- Encounter selection uses a longer cycle and an open-centre flank-pair pattern, with radius-sensitive spacing thresholds.

## Evidence still required

A successful compile is not art acceptance. Compare startup, cruise, sustained boost, left/right rolls while steering, a shield hit from either side, a close pass, and sector transitions in the Game view. Confirm that boost remains more intense than damage; the ship stays readable; no tunnel flash occurs on restart; the new flank pair leaves a usable line; and the film-grain/bloom treatment improves the sky without clipping detail or making the scene look noisy. Capture a baseline and a new frame before altering these values again.


## 2026-10-13 — Obstacle avoidance as a readable skill loop

### Research synthesis

The primary moment-to-moment loop is **read → choose → commit → near-miss or impact → consequence → recovery**. Obstacle volume alone is not the difficulty dial. Each formation should pose one legible spatial question and leave enough reaction time to answer it. The key references informing this pass:

- Steve Swink's *Game Feel* framework breaks control into input, response, context, polish, metaphor and rules. Its central useful implication for VOIDLINE: the flying craft needs to respond instantly, while hazard spacing/shape gives steering a meaningful context. https://www.gamedeveloper.com/design/game-feel-the-secret-ingredient
- Lee Perry's *The Single Most Useful Advice… Feedback* argues that an action needs a crisp, layered response—movement, audio, animation and camera—rather than effects being postponed as cosmetic polish. https://www.gamedeveloper.com/design/the-single-most-useful-advice-i-can-give-for-making-any-game-better-feedback
- Game Developer's *Enemy Attacks and Telegraphing* explains that avoidance mechanics must communicate the incoming question before asking the player to react. This applies to a high-speed obstacle just as it does to an enemy wind-up: arrival time and silhouette must be readable, not simply present in the scene. https://www.gamedeveloper.com/design/enemy-attacks-and-telegraphing
- The *Helix* controls postmortem describes making collision more generous by shrinking the lethal hull, encouraging near-misses, and using a small separating force weaker than player input. It also avoids spawning threats in blind spots. This is a directly useful precedent for VOIDLINE's courier/asteroid relationship. https://www.gamedeveloper.com/design/game-design-deep-dive-the-unique-touchscreen-action-game-controls-of-i-helix-i-
- Nicolae Berbece's GDC Europe death-animation breakdown, summarized by Game Developer, shows how animation, sound, particles, displacement and composition work together to turn a bare state change into a satisfying readable end beat. https://www.gamedeveloper.com/design/video-understanding-why-your-game-feels-bad-and-how-to-fix-it-

### Current rules and changes

1. **Use a pattern grammar, not independent asteroid spam.** A single sets the rhythm; OffsetPair/CrossWeave/VerticalWeave/FlankPair create recognizable movement questions; a recovery single follows a multi-hazard peak or low hull. Keep seeded patterns reproducible.
2. **Budget by seconds, not just metres.** Pair-member depth now scales to speed (roughly 0.75–1.0 seconds between member arrivals at the sampled normal/boost values), and next-formation scheduling accounts for the pair's full depth, bounded Z jitter and a recovery beat. Never add a pair's second member before the first one is readable.
3. **Protect a feasible escape lane.** Each Plan carries SafeLane metadata and validates candidate hazard positions against a ship-footprint margin. We retain multiple hazard lanes so the protected path is a real choice, not a forced single-file line. Keep large obstacles out of the central silhouette and avoid surprise spawns in the player's immediate blind spot.
4. **Make close avoidance profitable, not mandatory.** Clean proximity earns near-miss score and refreshes the adaptive music/flow chain; active barrel-roll dodge has its own reward. A normal clear pass must not count as a roll scrape for the Clinger. Near-miss clearance now scales with the actual pooled asteroid size, so a large rock needs a wider skim than a small rock.
5. **A collision is an interruption, not random punishment.** Survivable hits use a small bounded deflection and short directional camera/audio response; the player continues to steer. The final hull hit has a distinct cinematic response described below, rather than repeating the small survivable-hit cue or instantly covering the collision with the result panel.

### Fatal collision choreography now implemented

- **0–0.12 s — contact:** fast camera recoil/focus pulse, asymmetric roll kick and a bright but brief engine discharge. Fine ion fragments eject from both nozzles; not a large opaque smoke cloud.
- **0.12–1.12 s — loss of attitude:** ship model only (not its upright collision/control root) kicks to one side, pitches down, rolls approximately 72° and drifts slightly off its original pose with a damped rattle. Input stops immediately; this animation does not lock out an action the player could still use.
- **During the beat:** other hazards are frozen, while the open-space route drifts at 18% cruise speed. That small amount of environmental parallax sells momentum without creating new collision danger.
- **At 1.12 s:** background motion stops and the result/restart panel becomes available. The ruined ship holds its final pose so the score screen does not fight a looping tumble.
- **Reset contract:** restart reconfigures the controller and clears the terminal animation state. Final-impact routine, boost discharge and results delay still require a fresh live acceptance pass after this source compile.

### Propulsion sheets: cruise versus boost

- Cruise keeps the three sheet meshes as narrow, dim wisps: transverse width 0.90×, reduced alpha and small residual deformation, instead of three broad brightly glowing cards.
- Boost smoothly blooms the same meshes toward 2.68× transverse width, increases travelling wave amplitude and width breathing, and preserves the existing plume/core silhouette. It is continuous interpolation, not a sudden mesh swap.
- Peak sheet glow moves from 2.35 to 2.23 (about a 5% reduction) and overall peak colour gain from 2.05 to 2.00. The requested brightness reduction is intentionally small; the silhouette gains width rather than relying on exposure.
- Runtime sheet recovery reacquires the original FBX mesh and instantiates a fresh dynamic copy, avoiding the hot-reload bug where the previous frame's deformed geometry could become the next rest pose. Repeated reconfiguration also retires the previous runtime copy to avoid a mesh leak.

### Acceptance pass — do not mark complete until observed

- [ ] Cruise: sheet edges are restrained and coherent, with no broad stationary triangles or opaque cards at idle.
- [ ] Boost: membranes visibly broaden and ripple on both sides of the ship, but the hot core and surrounding space remain readable; highlight clipping is only slightly reduced.
- [ ] Obstacle rhythm: test an isolated single, every two-hazard pattern, a boost transition mid-pair, low-hull recovery, a close non-roll pass and a genuine roll-dodge. There is a viable escape lane and no pair/next-formation overlap.
- [ ] Contact: a surviving hit costs exactly one hull, provides readable deflection, and returns control quickly. A fatal hit shows the 1.12-second wreck beat before the result panel.
- [ ] Reset: new run starts level and controllable, with plume mesh copies/caches rebuilt once, no stale crash pose, no extra URP volume, no active old hazards and no duplicated score/event.

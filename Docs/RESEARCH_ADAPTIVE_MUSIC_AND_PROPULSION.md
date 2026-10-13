# VOIDLINE — Adaptive Music & Propulsion VFX Pass

**Date:** 2026-10-13  
**Project:** Unity 6000.6.5f1 / URP  
**Scope:** Music transitions for short boost bursts, engine-plume silhouette at actual gameplay scale.

## Findings

### Supplied music is three full arrangements, not stems

The local Resources copies were inspected as PCM WAV files:

| Arrangement | Measured duration | Role |
|---|---:|---|
| Space Threat — Slow Ambient Version, Looped | 131.992 s | Cruise bed |
| Space Threat — Electronic Dramatic Version, Looped | 81.240 s | Critical-hull pressure |
| Space Threat — Energetic Powerful Version, Looped | 66.012 s | Boost / flow state |

The public asset listing describes the same three loop variants as approximately 2:24, 1:30 and 1:10 respectively. Source: https://www.gamedevmarket.net/asset/space-threat-free-action-music

A lightweight RMS-envelope periodicity check on the local ambient and energetic files supports an approximate 80 BPM pulse; the dramatic arrangement has a weaker estimate. At 80 BPM in 4/4, one bar is 3 seconds. The ambient and energetic durations are near 44 and 22 bars respectively. **80 BPM is therefore a project-side estimate, not official tempo metadata**, and stays configurable in the VoidlineAdaptiveMusic source.

Unity documents AudioSource.PlayScheduled against AudioSettings.dspTime as the preferred approach when sample-timed playback/segment transitions need to be independent of frame rate: https://docs.unity.com/en-us/engine/6000.7/script-reference/unityengine/audiosource/playscheduled

Adaptive-music guidance cautions against relying on arbitrary crossfades alone: musical boundaries and layered/horizontal resequencing preserve continuity better than repeatedly fading unrelated sections in and out. Source: https://www.gamedeveloper.com/audio/design-with-music-in-mind-a-guide-to-adaptive-audio-for-game-designers

## Decisions implemented

- Keep boost fuel and music intensity separate. No gameplay boost duration or recharge was increased to solve the soundtrack complaint.
- Start an incoming energetic or dramatic arrangement on the next inferred beat using DSP-scheduled playback, with the silent ambient loop serving as the reference clock. Offset the incoming clip to a corresponding position instead of starting it at an arbitrary time-zero cue.
- Keep a queued arrangement silent until its DSP start time, then apply the existing smooth crossfade. A queued boost/tension track is cancelled if game over occurs before its start boundary.
- On a boost/flow trigger, keep the energetic state for at least four bars after the last trigger (12 seconds at the current 80 BPM estimate) even if the actual boost fuel has emptied. Skill-flow events can refresh the hold without changing the boost meter.
- Release carried intensity over approximately two bars (6 seconds at the current tempo) rather than dropping the energetic layer as soon as the fuel or input ends.
- Keep musicTempoBpm and boostPhraseBars serialized and editable so they can be recalibrated if the provided music changes.

## Propulsion silhouette

The V8 render still looked too much like thin luminous ribbons at the gameplay camera distance. The latest Blender build (V5 generator, V9 render) increased the radius of the blue outer envelope and secondary shear layer while retaining the narrower inner hot spine and the three distinct field ribbons. This is a shape/readability change, not a post-processing/bloom increase.

The FBX replacement is backed up at:
D:\dump\VOIDLINE_VFX_backup\VOIDLINE_Propulsion_Module_pre_v9.fbx

Source scene:
D:\dump\Voidline\ArtSource\VOIDLINE_PropulsionLab.blend

Runtime asset:
Assets/VOIDLINE/Resources/VOIDLINE_Propulsion_Module.fbx

## Acceptance checks still required

- [ ] Confirm the new wider envelope reads as plasma volume at both cruise and boost in the live Game View, without merging into one opaque mass or hiding the craft.
- [ ] Confirm boost music enters on beat, remains energetic after boost fuel ends, and fades without a noticeable rhythmic stumble. Test repeated boost/flow pulses and critical-hull tension.
- [ ] Confirm pause and game-over fades still behave correctly, including a game-over event while a music cue is queued.
- [ ] Check performance and Unity console for errors after fresh compilation and repeated run resets.

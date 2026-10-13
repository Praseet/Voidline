# VOIDLINE Asset Audit — 2026-10-13

## Decision summary

### SpaceSkies Free

- **Promote:** `Assets/SpaceSkies Free/Skybox_1/Pink_2K_Resolution.mat`. Use it as VOIDLINE's primary runtime sky, copied as `Assets/VOIDLINE/Resources/M_VOIDLINE_SpaceSky_Pink.mat` so the game can load it through `Resources.Load`. The pink-red nebula clouds sit around a darker central flight lane in the face previews; that gives the ship and incoming hazards more readable contrast than a bright central object.
- **Keep available for later experiments:** `Skybox_2/Green_2K_Resoution.mat`. Its galaxy core is visually strong, but the luminous horizontal disc would compete with the route and threats in the main forward view. It could work in a slower scenic sector or a dedicated transition.
- **Do not treat as a new art upgrade:** `Skybox_3/Purple_2K_Resolution.mat`. Its six face texture GUIDs match the previously installed `SpaceSkiesPurple2K` set, so it largely reproduces the sky already in use.
- **Use 2K for the default build.** The game has a moving foreground and a static skybox. 2K faces provide a good quality/memory compromise; the 4K face set is unnecessary for the default configuration and can remain available for high-resolution experiments. 1K is a lower-memory option if profiling later shows a need.
- Keep the sample scene and demo scripts separate from the main game. In particular, do not attach the package's free-fly `LookCamera` or dropdown-driven `SkyboxChanger` to gameplay objects.

The main scene must not depend on a package demo scene. The runtime sky loader keeps the local previous sky material and a procedurally generated sky as fallbacks so a checkout without the optional third-party package still has a sky.

## Deep In Space soundtrack

- Source: Unity Asset Store, **Deep In Space** by Breitbarth, asset ID 88071: https://assetstore.unity.com/packages/audio/music/electronic/deep-in-space-88071
- Imported clip is moved by Unity's AssetDatabase into `Assets/VOIDLINE/Resources/Deep_In_Space.wav` so the runtime music system can load it without scene-serialized references. It is about 134.4 seconds at 44.1 kHz; streaming/Vorbis import is configured to avoid a full PCM copy in memory.
- Use it as a non-looping lead theme. When the authored track finishes, fade back to VOIDLINE's existing looped ambient stem. Keep the existing dramatic and boost stems for adaptive risk/boost layers; do not loop the long theme blindly or layer two full-volume melodies.
- Stream and Vorbis-compress the long music clip at import time to avoid retaining a full-length PCM copy in memory. Keep it 2D even though the source importer initially marked it as 3D audio.

## Import and source-control notes

These assets have been imported into the local Unity project and should stay in the project under their Unity asset paths. The new source package and WAV are local imported assets; the project can use procedural sky fallback and existing audio fallback if an asset is not present. Preserve each asset's original .meta file and licensing/source information. The Unity Asset Store source package should be re-acquired from its publisher rather than redistributing source textures independently.

## Validation checklist

- [ ] Pink 2K skybox is the material loaded by the live game, with all six cubemap faces non-null.
- [ ] The ship remains silhouetted against the sky and the central flight corridor stays dark/readable.
- [ ] Sector tint/exposure transitions remain smooth and reset correctly on a new run.
- [ ] Deep In Space fades in and plays as 2D music, then hands off to the looped ambient track at completion.
- [ ] Tension/boost/critical layers still respond, pause ducks volume, and game-over fades music.
- [ ] Editor Console and `git diff --check` contain no new code errors.
- [ ] No demo scene is added to Build Settings.

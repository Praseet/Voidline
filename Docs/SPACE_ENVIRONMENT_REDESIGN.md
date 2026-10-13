# VOIDLINE — Celestial World Redesign

**Status:** environment implementation prepared on branch `environment/celestial-world-redesign`; Unity visual acceptance pending.
**Art direction:** bright, expansive, colourful deep space — not the nearly-black purple panorama with rocks as the only depth cue.

## Asset choice

Use **[Procedural Planet Generation - Lite: Baked Planets and Simple Procedural Shaders](https://marketplace.unity.com/packages/vfx/shaders/procedural-planet-generation-lite-baked-planets-and-simple-proce-296362)** as the primary authored celestial package. Its listed contents include planet and moon materials, gas giants, asteroid rings, stars/coronas, atmospheric glow, and a static 4K space skybox; its January 2026 release adds 12 sample planets. It is listed as free and compatible with URP in Unity 6.

The package's [Lite Samples Pack documentation](https://parallelcascades.com/procedural-planet-generation/latest/lite-samples-pack/) documents gas-giant bands, ring systems, star surfaces, atmosphere glow and its asset-generation tools. Keep the original asset files inside the locally imported Asset Store package: this repository stores references to them, not copies of licensed source files.

An optional static-only alternative is [Space Skyboxes 4K Free](https://marketplace.unity.com/packages/2d/textures-materials/sky/space-skyboxes-4k-free-354458). Its current listing is compatible with URP and is 683 MB, so do not import the entire pack in addition to the primary package unless its specific skybox gives a clear art-direction improvement.

## New composition

The environment now has two visual depths:

1. **The celestial skybox:** the imported 4K star/nebula/galaxy panorama. Keep its colour and exposure vibrant enough to convey wonder, with cyan/blue gas clouds, violet/magenta filaments and bright stellar clusters.
2. **Slow-moving 3D celestial bodies:** a large warm gas giant with rings toward one edge, a contrasting oceanic/terrestrial planet toward the other, a much smaller moon in the upper/outer field, and a small luminous star. These are placed at roughly 965–1,690 world units so they read as distant worlds rather than encounter obstacles. All colliders are disabled and all renderers have shadows disabled.

The runtime composition is deliberately asymmetric and uses a few large landmarks rather than a uniform field of tiny rocks. That gives the eye scale and direction while preserving the visual simplicity needed for speed.

## Install and populate the asset references

1. In Unity's Package Manager, open **My Assets**, find Procedural Planet Generation - Lite, download it, and import the needed skybox and celestial shader/material samples. Do not import all 683 MB of the optional static-sky package as a first step.
2. In the editor, run **VOIDLINE → Environment → Build Celestial Asset Set**.
3. Open `Assets/VOIDLINE/Resources/VOIDLINE_CelestialAssetSet.asset`. Review its arrays. The builder auto-fills categories when it finds matching names, including baked colour textures as well as materials/prefabs. Asset-pack naming can vary; if a slot is empty, drag the matching imported skybox/material/colour texture/prefab into that array. Keep the references pointed at the original imported assets. The runtime prefers the package's authored material, then its baked colour texture, and only then uses the procedural fallback texture.
4. Enter Play Mode. `VoidlineSpaceSequence` loads `VOIDLINE_CelestialAssetSet`, installs the selected celestial skybox instead of `M_VOIDLINE_DarkSpace`, and creates the far-field planet composition. If the optional package is absent, a brighter colourful fallback keeps the run playable; this is not the intended final art.

## Future enemy readability

- Keep enemy gameplay depths close to the player and ahead of the celestial bodies. Planets and stars remain distant dressing, never colliders, targets, pickups or hazards.
- Reserve a clean central sightline and enough contrast around ship/enemy silhouettes. Large planets occupy the outer composition, not the middle of the steering envelope.
- Keep background movement slower and smoother than foreground hazards; don't add random bright flashes or busy texture motion behind an enemy tell.
- Sector variation should come from shifting which planet/moon arrangement is visible and changing the authored sky, not from adding more clutter.
- Tune enemy materials against the brightest gas-giant and nebula colours so their silhouettes remain identifiable when boosting.

## Validation required before this is considered finished

The remote development-machine session was unavailable during this environment rewrite, so this branch has not yet been compiled or inspected in Unity. After importing the package and building the asset set, verify:
- Startup and restart show open celestial space immediately; the old opening corridor does not render.
- The selected 4K skybox material is a valid Skybox shader/material and renders correctly under URP.
- The gas giant/ring, planet, moon and star appear inside the camera far plane with no clipping and no central-lane occlusion.
- No celestial objects contain active colliders or shadow casters.
- Planet count stays bounded at four, materials and generated meshes are released on scene teardown, and frames remain stable.
- Future enemy silhouettes remain readable in front of the brightest parts of the sky and during boost.

Do not mark the redesign complete based on successful compilation alone; adjust the composition after the first real Game-view capture.

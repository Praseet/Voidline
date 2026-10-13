# VOIDLINE — Celestial World Redesign

**Status:** environment implementation prepared on branch `environment/celestial-world-redesign`; Unity visual acceptance pending.
**Art direction:** bright, expansive, colourful deep space — not the nearly-black purple panorama with rocks as the only depth cue.

## The sky asset is being replaced completely

The primary sky is no longer `M_VOIDLINE_DarkSpace` and no longer depends on the old dark panorama or its procedural nebula fallback. The new primary art is three distinct, real 10,000×5,000 equirectangular images from **[Space Spheremaps](https://www.spacespheremaps.com/)**, imported into the project's Art folder:

- [Multi Nebulae 1](https://www.spacespheremaps.com/wp-content/uploads/multi_nebulae_1.png): richly multicoloured nebula clouds.
- [Galactic Plane 1](https://www.spacespheremaps.com/wp-content/uploads/galactic_plane_1.png): a brighter Milky Way-style star/galaxy band.
- [Blue Nebulae 1](https://www.spacespheremaps.com/wp-content/uploads/blue_nebulae_1.png): a cyan/blue-biased nebula for a cooler sector.

The official [Multi-nebulae page](https://www.spacespheremaps.com/multi-nebulae-spheremaps/), [Galactic Plane page](https://www.spacespheremaps.com/galactic-plane-spheremaps/) and [Blue Nebulae page](https://www.spacespheremaps.com/blue-nebulae-spheremaps/) list these PNGs at about 11 MB each. The author's [licensing terms](https://www.spacespheremaps.com/about/) explicitly permit use and modification in commercial games; no attribution is required, but credit is appreciated. Source textures are downloaded into `Assets/VOIDLINE/Art/External/SpaceSpheremaps`; this is real authored background art, not a generated colour grade.

A new `Skybox/VOIDLINE Celestial Blend` shader crossfades between those panoramas when the player passes a sector landmark. That changes the underlying sky image rather than only tinting one texture. The editor importer also creates a serialized Resources material so the shader is explicitly referenced by player builds. Panorama import settings use high-quality texture compression, mipmaps, texture streaming, and an 8K max import size to balance detail with GPU memory.

## Celestial bodies

The skybox provides the large-scale stars, galaxies, nebulae, and star clusters. The world-dressing layer adds four distant 3D bodies: a warm gas giant with rings, a contrasting terrestrial/oceanic planet, a small moon, and a luminous star. The outer framing is asymmetric so the central flight/enemy lane stays readable. These bodies have colliders and shadow casting disabled and remain inside the current 1,800-unit far clip.

For more detailed planetary surface materials, the optional free [Procedural Planet Generation - Lite](https://marketplace.unity.com/packages/vfx/shaders/procedural-planet-generation-lite-baked-planets-and-simple-proce-296362) pack can be imported. The builder references its local planet/gas-giant/moon materials, baked colour textures and prefabs; it does not copy its licensed source files. The celestial sky does **not** require that package, because the panorama downloader supplies the primary sky art directly.

## Use the new sky in Unity

1. Pull the `environment/celestial-world-redesign` branch (or use its pull request).
2. In Unity, run **VOIDLINE → Environment → Download Multicolour Space Panoramas (33 MB)** once. This fetches the three licensed PNGs, imports them with the correct equirectangular texture settings, generates the Resources blend material, and writes the image references into `Assets/VOIDLINE/Resources/VOIDLINE_CelestialAssetSet.asset`.
3. Enter Play Mode. The runtime selects an authored panorama instead of the old dark sky, slowly drifts it, and crossfades to another panorama on actual landmark crossing. It builds the distant planetary layer and still supports a procedural fallback only if an external image download fails.
4. For higher-detail planet surfaces, import Procedural Planet Generation - Lite and run **VOIDLINE → Environment → Build Celestial Asset Set**. Inspect its populated arrays and replace any incorrect auto-matched material/texture by reference to the proper imported source asset.

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

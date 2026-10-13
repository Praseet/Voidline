# Space Spheremaps — asset provenance

VOIDLINE bundles three optimized 4096×2048 JPEG panorama assets derived from the official free Space Spheremaps PNG downloads. The original files are 10000×5000 PNGs. The bundled versions were resized with high-quality Lanczos resampling and encoded as JPEG at quality 86 to keep the three background assets small while retaining sufficient detail for a panoramic 1080p game view.

| Bundled asset | Original source |
|---|---|
| `Assets/VOIDLINE/Resources/SpacePanoramas/01_MultiNebulae_01.jpg` | https://www.spacespheremaps.com/wp-content/uploads/multi_nebulae_1.png |
| `Assets/VOIDLINE/Resources/SpacePanoramas/02_GalacticPlane_01.jpg` | https://www.spacespheremaps.com/wp-content/uploads/galactic_plane_1.png |
| `Assets/VOIDLINE/Resources/SpacePanoramas/03_BlueNebulae_01.jpg` | https://www.spacespheremaps.com/wp-content/uploads/blue_nebulae_1.png |

## Creator and license

Creator/source: TonyS / Space Spheremaps — https://www.spacespheremaps.com/

The creator describes the panoramas as equirectangular sphere maps for 3D and game engines, and the individual asset pages list the three source PNGs at 10000×5000. The creator's licensing page states that the background assets are available under Creative Commons BY 4.0 and may be shared and adapted for commercial use; the creator additionally waives the usual attribution requirement, while appreciating a credit.

- About and license terms: https://www.spacespheremaps.com/about/
- Multi-nebulae source page: https://www.spacespheremaps.com/multi-nebulae-spheremaps/
- Galactic Plane source page: https://www.spacespheremaps.com/galactic-plane-spheremaps/
- Blue Nebulae source page: https://www.spacespheremaps.com/blue-nebulae-spheremaps/
- CC BY 4.0: https://creativecommons.org/licenses/by/4.0/

Suggested credit: **Space Spheremaps (TonyS), spacespheremaps.com**.

## Import and use

The optimized panorama files are committed directly under `Assets/VOIDLINE/Resources/SpacePanoramas`; normal Unity asset import is sufficient. They are loaded by `Resources.LoadAll<Texture2D>("SpacePanoramas")` and used by the serialized `M_VOIDLINE_CelestialBlend` material. No runtime network access or external package import is required to get the replacement sky. The editor download menu exists only as a way to re-download/replace source maps during art iteration.

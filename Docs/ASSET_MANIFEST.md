# VOIDLINE — Asset Manifest

## Existing third-party content
| Asset family | Usage | Source / license |
|---|---|---|
| Kenney Space Kit 2.0 | Imported player ship and meteor/crystal/fuel-cell meshes | Included Assets/Third Party/kenney_space-kit/License.txt states Creative Commons Zero (CC0). |
| Kenney Modular Space Kit | Present in the project; use only where it fits the established low-poly corridor style | Check its own bundled license before using additional files. |

## Project-owned runtime assets
| Asset | Purpose |
|---|---|
| PF_Asteroid_Meteor.prefab | First asteroid hazard variant |
| PF_Asteroid_Crystal.prefab | Second asteroid hazard variant |
| PF_Pickup_Repair.prefab | Repair cell based on the Kenney fuel model with an emissive ring and point light |
| M_Asteroid_Rock.mat | URP-compatible rock material |
| M_Asteroid_Crystal.mat | URP-compatible crystal material; all imported sub-material slots normalized to avoid magenta shader fallbacks |
| M_Pickup_Repair.mat | Teal/green emissive repair-cell material |
| M_Tunnel_Accent.mat | Cyan emissive corridor rails |
| VoidlineRunSettings.asset | Editable run tuning |

## Music

| Asset | Purpose |
|---|---|
| Space Threat (Slow Ambient Version, Looped).wav | Cruise bed — `Music_Ambient` layer |
| Space Threat (Electronic Dramatic Version, Looped).wav | Tension and critical-hull layers |
| Space Threat (Energetic Powerful Version, Looped).wav | Boost layer |

- Source: **Space Threat (Free Action Music)**, Unity Asset Store productId 205935, package v1.0.
- Only the three "Looped" stems are used; the non-looped masters are not loaded. Copies live in
  `Assets/Resources` (so `Resources.Load` resolves them) with their original `.meta` GUIDs intact;
  the originals remain under `Assets/Space Threat (Action Cinematic Music)`.
- Driven by `VoidlineAdaptiveMusic`; layer levels are `[SerializeField]` and untuned.
- Licence terms must be checked before shipping or redistributing.


## Visual direction
- Low-poly industrial sci-fi, dark blue-grey structure, cyan/teal light accents.
- Keep the ship silhouette and incoming hazards readable against the corridor.
- Use emission as a restrained navigation cue, not as full-screen bloom or decoration.
- Keep UI typography small but legible at the target desktop resolution; validate in a standalone window as well as the editor.
- Prefer the supplied Kenney assets over placeholder primitives when a suitable model exists.

## Import / validation notes
- Project-owned materials use the Universal Render Pipeline Lit shader where available.
- Imported mesh renderers should not retain unsupported legacy Standard materials when the active render pipeline is URP; normalize every material slot where needed.
- Prefab visuals have their own colliders removed; the root prefab owns the simple trigger collider and kinematic Rigidbody.
- Temporary screenshots and QA captures are in Temp/ and are not production assets.
# VOIDLINE authored asset additions — 2026-10-12

## Space backdrop

- Source: local Unity Asset Store package SpaceSkies Free by PULSAR BYTES.
- Selected Skybox_3/Purple_2K_Resolution.mat and only its six 2K cubemap faces.
- Runtime material: Assets/VOIDLINE/Resources/M_VOIDLINE_DarkSpace.mat (unique key; exposure 0.19, restrained blue-violet tint (0.64, 0.71, 0.88)).
- Faces: Assets/VOIDLINE/Art/SpaceSkiesPurple2K/SpaceSky_{Back,Down,Front,Left,Right,Up}_2K.png.
- Only the required six 2K faces and material were extracted; the full cached package was not imported.

## Player craft

- Authored in Blender from a low-poly lofted hull, swept wing armor, cockpit, nozzle and emissive details.
- FBX runtime source: Assets/VOIDLINE/Resources/VOIDLINE_Courier.fbx.
- Editable source: Assets/VOIDLINE/Art/Blender/VOIDLINE_Courier.blend.
- PBR maps: 2K base color; 1K tangent normal; 1K packed mask and ORM. Only base color uses sRGB.
- Four authored exhaust plume meshes plus two engine socket empties; runtime boost scaling is handled by VoidlineFlightEffects.
- FBX importer disables animation, cameras, lights, colliders and CPU-readable mesh data.

## Integrity / limitations

- Check upstream package license terms before redistributing assets.
- Runtime material and silhouette now verified in Play Mode: `Resources/VOIDLINE_Courier.fbx` instantiates
  as `Player/Model/VOIDLINE_Courier` at scale 0.42 (≈1.52 x 1.62 units against the 1.55 x 1.80 trigger
  collider), centred on the collision root, ~16% of Game-view height from the 7.76-unit chase camera.
- The runtime assigns materials per authored material slot, so the FBX's multi-material renderers
  (`Engine_Nozzles`, `Titanium_Details`, `Armor_Panels`) keep their ceramic/titanium/armor split instead
  of collapsing to one flat material. `T_VOIDLINE_Courier_ORM.png` now feeds URP's occlusion channel;
  `T_VOIDLINE_Courier_Mask.png` feeds the metallic/smoothness channel.
- The superseded Kenney `craft_speederD` placeholder and the stray empty `EngineLeft`/`EngineRight`
  anchors were removed from both `main.unity` and `main_open_space.unity` through the Unity Editor API
  and both scenes were saved. `VoidlineGameManager.RemoveLegacyShipVisuals` also strips them at runtime,
  so a stale scene cannot reintroduce the placeholder.
- The placeholder previously carried a scene-level material override pointing at `M_Asteroid_Crystal`,
  so the player ship had been rendering with an asteroid material. That override is gone.
- Saving `main.unity` intermittently fails with "Access is denied" on the Temp-file move. This is
  environmental, not a project defect: in-place writes succeed and delete-then-create succeeds, but
  atomic rename-over of that specific file is denied. Clicking "Try Again" on the dialog completes the
  save. Expect to retry once when saving `main.unity`.
- Do not treat visual or runtime acceptance gates as passed until Unity recompiles, the scene loads and
  the Game view is inspected.


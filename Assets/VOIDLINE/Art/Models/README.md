# VOIDLINE Moon Rock LOD Asset

Source: Poly Haven — Moon Rock 03, https://polyhaven.com/a/moon_rock_03
License: CC0 (public domain dedication). Source mesh and original 4K maps remain unmodified in Assets/Third Party/moon_rock_03_4k.blend/.

Game-ready conversion:
- VOIDLINE_MoonRock_LOD.fbx: four separate LOD meshes; 5,598 / 2,798 / 1,398 / 698 triangles, respectively. Scaled for Unity gameplay. Shared material assigned in Unity.
- T_MoonRock_BaseColor_2K.jpg: 2K sRGB base color.
- T_MoonRock_NormalGL_1K.png: downsampled OpenGL tangent-space normal map.
- T_MoonRock_MetallicSmoothness_1K.png: packed low metallic in red and inverted roughness in alpha for URP Lit.
- PF_Asteroid_MoonRock.prefab: runtime pooled hazard with a spherical trigger, kinematic Rigidbody, VoidlineAsteroid, shared URP material, and four-level LODGroup.

Performance intent:
- Preserve a single shared material across LODs.
- Keep the collision primitive independent of the detailed render mesh.
- Let Unity import settings compress textures and generate mipmaps.
- LODs are screen-relative; verify thresholds and visual quality in the real Game View.
- The source blend file and 4K maps are retained for future edits. Do not replace them with generated copies.
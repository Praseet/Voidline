# External asset setup

The public repository intentionally contains only source assets that can be redistributed. Some locally installed Unity Asset Store packs are retained on the development machine but excluded from GitHub.

## Space sky

The local development project can use SpaceSkies Free from the Unity Asset Store. Those source textures and the derived M_VOIDLINE_DarkSpace.mat are not vendored in this public repository. VoidlineSpaceSequence uses the local material when installed; without it, the game generates a small procedural starfield/nebula panorama at runtime. The procedural background is a portability fallback, not the final environment-art direction.

## Adaptive soundtrack

The three looped Space Threat stems are also local-only Asset Store source assets. They remain in Assets/Resources on the development machine but are excluded from Git. To enable the adaptive soundtrack in another checkout, obtain the licensed package and place these looped WAVs in Assets/Resources:

- Space Threat  (Slow Ambient Version, Looped).wav
- Space Threat (Electronic Dramatic Version, Looped).wav
- Space Threat (Energetic Powerful Version, Looped).wav

The filenames are resource keys; preserve the double space in the ambient stem. If the clips are absent, the game logs a warning and remains playable without music.

## Redistributable model assets

Assets/Third Party/kenney_space-kit is included because current asteroid/repair-pickup prefabs reference its DAE models. Its included license identifies the content as CC0. Other downloaded packs that are not dependencies of the current build remain local and are ignored to avoid bloating or improperly redistributing raw source files.

## Editor-only material

Temporary screenshots and recovery scenes under Assets/Temp and Assets/_Recovery are local artifacts, not game content, and are excluded from Git.

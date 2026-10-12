from pathlib import Path
import re, shutil, json
root=Path(r"D:\dump\Voidline")
files=[
"Assets/VOIDLINE/Scripts/Environment/VoidlineRouteBranchController.cs",
"Assets/VOIDLINE/Scripts/Environment/VoidlineRouteBranchController.cs.meta",
"Assets/VOIDLINE/Resources/M_VOIDLINE_SpaceSkybox.mat",
"Assets/VOIDLINE/Resources/M_VOIDLINE_SpaceSkybox.mat.meta",
"Assets/VOIDLINE/Editor/VOIDLINECourierAssetBuilder.cs",
"Assets/VOIDLINE/Editor/VOIDLINECourierAssetBuilder.cs.meta",
"AgentScripts/ConfigureVoidline.cs",
"AgentScripts/FinalizeVoidlineStatic.py",
"AgentScripts/FinalizeVoidlineAssets.py",
"AgentScripts/list_spaceskies_paths.py",
"AgentScripts/extract_spaceskies_purple_2k.py",
"AgentScripts/generate_voidline_courier_textures.py",
"AgentScripts/generate_voidline_courier_blender.py"]
for rel in files:
    p=root/rel
    if p.exists():
        if p.is_file(): p.unlink()
        meta=p.with_suffix(p.suffix+".meta")
        if meta.exists(): meta.unlink()
# Clear Blender's auto-generated backup; retain only the current editable source.
for p in (root/"Assets/VOIDLINE/Art/Blender").glob("*.blend1"):
    p.unlink()
# The FBX contains baked normals; use a tangent-space normal import.
normal_meta=root/"Assets/VOIDLINE/Resources/T_VOIDLINE_Courier_Normal.png.meta"
if normal_meta.exists():
    t=normal_meta.read_text(encoding="utf-8-sig")
    t=re.sub(r"(?m)^    sRGBTexture: [01]$","    sRGBTexture: 0",t,count=1)
    t=re.sub(r"(?m)^    textureType: \d+$","    textureType: 1",t,count=1)
    normal_meta.write_text(t,encoding="utf-8",newline="")
# Scene/runtime uses unique dark sky. Existing old key is no longer required.
scene=(root/"Assets/VOIDLINE/Scenes/main.unity").read_text(encoding="utf-8-sig")
manager=(root/"Assets/VOIDLINE/Scripts/Core/VoidlineGameManager.cs").read_text(encoding="utf-8-sig")
fx=(root/"Assets/VOIDLINE/Scripts/Player/VoidlineFlightEffects.cs").read_text(encoding="utf-8-sig")
seq=(root/"Assets/VOIDLINE/Scripts/Environment/VoidlineSpaceSequence.cs").read_text(encoding="utf-8-sig")
report={
"dark_sky_bound": "0ea8f08fb0604d1a965ed59dac44d4ec" in scene,
"legacy_route_component_removed": "1039906633" not in scene,
"old_route_graph_settings_removed": "plannedJunctionCount" not in scene and "branchLateralOffset" not in scene,
"authored_ship_loaded_by_game_manager": 'Resources.Load<GameObject>("VOIDLINE_Courier")' in manager,
"plumes_updated_with_boost": "UpdateAuthoredPlumes();" in fx,
"intro_tunnel_permanently_retires": "tunnelRetired = true;" in seq and "GetDesiredTunnelVisibility" not in seq,
"space_sky_face_count": sum((root/"Assets/VOIDLINE/Art/SpaceSkiesPurple2K"/("SpaceSky_"+face+"_2K.png")).exists() for face in ("Back","Down","Front","Left","Right","Up")),
"fbx_bytes": (root/"Assets/VOIDLINE/Resources/VOIDLINE_Courier.fbx").stat().st_size,
"blender_source_bytes": (root/"Assets/VOIDLINE/Art/Blender/VOIDLINE_Courier.blend").stat().st_size,
"legacy_space_sky_removed": not (root/"Assets/VOIDLINE/Resources/M_VOIDLINE_SpaceSkybox.mat").exists(),
"authored_asset_manifest": (root/"Docs/AssetProvenance/VOIDLINE_AuthoredAssets_2026-10-12.md").exists()
}
print(json.dumps(report,indent=2))
if not all([report["dark_sky_bound"],report["legacy_route_component_removed"],report["old_route_graph_settings_removed"],report["authored_ship_loaded_by_game_manager"],report["plumes_updated_with_boost"],report["intro_tunnel_permanently_retires"],report["space_sky_face_count"]==6,report["legacy_space_sky_removed"],report["authored_asset_manifest"]]):
    raise SystemExit(2)

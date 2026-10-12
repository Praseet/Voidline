import bpy, math, random, os
from mathutils import Vector
from mathutils.noise import noise_vector

OUT_DIR = r"D:\dump\Voidline\Assets\VOIDLINE\Resources"
ASSET_DIR = os.path.join(OUT_DIR, "RouteCues")
os.makedirs(ASSET_DIR, exist_ok=True)
BLEND_PATH = os.path.join(ASSET_DIR, "VOIDLINE_EnvironmentKit.blend")
FBX_PATH = os.path.join(ASSET_DIR, "VOIDLINE_EnvironmentKit.fbx")

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.materials, bpy.data.curves):
    for item in list(datablocks):
        if item.users == 0:
            datablocks.remove(item)

def make_material(name, color, metallic=0.0, roughness=0.65, emission=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat

rock_mat = make_material("M_ObsidianAsteroid", (0.075, 0.105, 0.15), 0.12, 0.88)
rock_edge_mat = make_material("M_ColdMineralFacet", (0.19, 0.24, 0.30), 0.24, 0.72)
cyan_mat = make_material("M_CyanRouteCrystal", (0.055, 0.55, 0.76), 0.2, 0.24, (0.015, 0.42, 0.95), 2.8)
amber_mat = make_material("M_AmberFractureCrystal", (0.88, 0.23, 0.045), 0.15, 0.28, (1.0, 0.105, 0.018), 2.2)
relic_mat = make_material("M_AncientTransitRelic", (0.045, 0.10, 0.13), 0.52, 0.42)
relic_light = make_material("M_RelicResidualSignal", (0.10, 0.42, 0.52), 0.35, 0.24, (0.02, 0.28, 0.45), 1.1)

def set_material(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)

def finish_mesh(obj, name):
    obj.name = name
    obj.data.name = name + "_Mesh"
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    # UVs are deliberately simple and robust for the game-ready low-poly kit.
    bpy.ops.object.mode_set(mode='OBJECT') if obj.mode != 'OBJECT' else None
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    try:
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(island_margin=0.02)
        bpy.ops.object.mode_set(mode='OBJECT')
    except Exception:
        if obj.mode != 'OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)
    return obj

def asteroid(name, seed, scale):
    random.seed(seed)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=1, location=(0,0,0))
    obj = bpy.context.object
    for v in obj.data.vertices:
        p = v.co.copy()
        # Multi-frequency silhouette breakup: large-scale fracture plus fine facet variation.
        n = noise_vector(p * (1.7 + seed * 0.001))
        factor = 1.0 + 0.19*n.x + 0.12*n.y + random.uniform(-0.09, 0.09)
        v.co = Vector((p.x*factor*scale[0], p.y*factor*scale[1], p.z*factor*scale[2]))
    finish_mesh(obj, name)
    set_material(obj, rock_mat)
    obj.data.materials.append(rock_edge_mat)
    # Facet material variation stays in geometry rather than adding shader complexity.
    for poly in obj.data.polygons:
        poly.material_index = 1 if random.random() < 0.16 else 0
        poly.use_smooth = False
    return obj

def crystal(name, material, height=2.2, radius=0.55, sides=6, seed=1):
    random.seed(seed)
    verts=[]
    rings=[(-height*0.5, radius*0.58), (-height*0.20, radius), (height*0.17, radius*0.70)]
    for ri,(z,r) in enumerate(rings):
        for i in range(sides):
            a=2*math.pi*i/sides + (ri%2)*math.pi/sides*0.35
            rr=r*random.uniform(0.83,1.14)
            verts.append((math.cos(a)*rr, math.sin(a)*rr, z))
    verts.append((0.0,0.0,height*0.62))
    top=len(verts)-1
    faces=[]
    for ri in range(len(rings)-1):
        for i in range(sides):
            a=ri*sides+i; b=ri*sides+(i+1)%sides
            c=(ri+1)*sides+(i+1)%sides; d=(ri+1)*sides+i
            faces.extend([(a,b,d),(b,c,d)])
    last=(len(rings)-1)*sides
    for i in range(sides):
        faces.append((last+i,last+(i+1)%sides,top))
    mesh=bpy.data.meshes.new(name+"_Mesh")
    mesh.from_pydata(verts,[],faces); mesh.update()
    obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj)
    finish_mesh(obj,name); set_material(obj,material)
    for p in obj.data.polygons: p.use_smooth=False
    return obj

def relic_fragment():
    # Broken alien transit relic: asymmetric basalt fins around an empty centre.
    pieces=[]
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0,0,0))
    core=bpy.context.object; core.name="Relic_Core"
    core.dimensions=(0.7,0.7,3.2)
    core.rotation_euler=(math.radians(8),math.radians(-14),math.radians(9))
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bevel=core.modifiers.new("Worn edges","BEVEL"); bevel.width=0.12; bevel.segments=1
    core.modifiers.new("Weighted normals","WEIGHTED_NORMAL")
    bpy.context.view_layer.objects.active=core
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    bpy.ops.object.modifier_apply(modifier="Weighted normals")
    set_material(core,relic_mat); pieces.append(core)
    for i,(loc,dim,rot) in enumerate([
        ((-0.65,0.05,0.65),(0.18,0.28,2.0),(0.12,0.32,-0.25)),
        ((0.60,-0.12,-0.25),(0.22,0.3,1.45),(-0.28,-0.18,0.35)),
        ((0.05,0.58,0.18),(0.20,0.18,1.8),(0.18,0.55,0.1))]):
        bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
        fin=bpy.context.object; fin.name="Relic_Fin_%02d"%i
        fin.dimensions=dim; fin.rotation_euler=rot
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        b=fin.modifiers.new("Fractured edge","BEVEL"); b.width=0.07; b.segments=1
        bpy.context.view_layer.objects.active=fin; bpy.ops.object.modifier_apply(modifier=b.name)
        set_material(fin,relic_mat); pieces.append(fin)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=0.18,location=(0.42,0.4,0.9))
    signal=bpy.context.object; signal.name="Relic_Signal"
    set_material(signal,relic_light); pieces.append(signal)
    # Join fragments to keep the unique landmark to a single draw call.
    bpy.ops.object.select_all(action='DESELECT')
    for obj in pieces: obj.select_set(True)
    bpy.context.view_layer.objects.active=pieces[0]
    bpy.ops.object.join()
    joined=bpy.context.object
    finish_mesh(joined,"AncientTransitRelic_Fragment")
    return joined

root=bpy.data.objects.new("VOIDLINE_EnvironmentKit",None)
bpy.context.collection.objects.link(root)
assets=[]
assets.append(asteroid("Asteroid_Rugged_A",11,(1.25,0.86,1.0)))
assets.append(asteroid("Asteroid_Rugged_B",29,(0.92,1.2,0.82)))
assets.append(asteroid("Asteroid_Rugged_C",47,(1.08,0.9,1.28)))
assets.append(crystal("Crystal_Cyan_Shard",cyan_mat,2.1,0.52,6,71))
assets.append(crystal("Crystal_Amber_Shard",amber_mat,1.8,0.48,6,83))
assets.append(relic_fragment())
# Put every pivot at its own origin and parent to the neutral kit root.
for obj in assets:
    obj.parent=root
    obj.location=(0,0,0)
    obj.rotation_euler=(0,0,0)
    obj.select_set(False)
root.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
bpy.ops.object.select_all(action='DESELECT')
for obj in assets: obj.select_set(True)
bpy.context.view_layer.objects.active=assets[0]
bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=True, apply_scale_options='FBX_SCALE_ALL',
    object_types={'MESH'}, apply_unit_scale=True, bake_space_transform=False,
    add_leaf_bones=False, mesh_smooth_type='OFF', use_mesh_modifiers=True,
    path_mode='AUTO', embed_textures=False, axis_forward='-Z', axis_up='Y')
print("VOIDLINE_ASSET_EXPORT", FBX_PATH)
for obj in assets:
    print("ASSET", obj.name, "verts", len(obj.data.vertices), "tris", sum(len(p.vertices)-2 for p in obj.data.polygons))

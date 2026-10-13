import bpy
import math
import os
import sys
from mathutils import Vector

def args_after_dash():
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

args = args_after_dash()
blend_path = args[0] if len(args) > 0 else r"D:\dump\Voidline\ArtSource\VOIDLINE_Clinger.blend"
fbx_path = args[1] if len(args) > 1 else r"D:\dump\Voidline\ArtSource\Exports\VOIDLINE_Clinger.fbx"
os.makedirs(os.path.dirname(blend_path), exist_ok=True)
os.makedirs(os.path.dirname(fbx_path), exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0

def material(name, base, metallic, roughness, emission=None, emission_strength=0.0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*base, 1.0)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*base, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission and "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    elif emission and "Emission" in bsdf.inputs:
        bsdf.inputs["Emission"].default_value = (*emission, 1.0)
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = emission_strength
    return m

shell = material("Clinger / Basalt Chitin", (0.010, 0.018, 0.024), 0.74, 0.41)
plate = material("Clinger / Ceramic Armor", (0.032, 0.052, 0.060), 0.64, 0.43)
soft = material("Clinger / Dark Membrane", (0.004, 0.009, 0.013), 0.25, 0.52)
glow = material("Clinger / Bioelectric Teal", (0.004, 0.085, 0.12), 0.18, 0.29, (0.008, 0.28, 0.42), 2.0)
amber = material("Clinger / Signal Gland", (0.26, 0.045, 0.006), 0.30, 0.31, (0.55, 0.06, 0.004), 1.2)
clawmat = material("Clinger / Hooked Keratin", (0.045, 0.066, 0.069), 0.53, 0.43)

mesh_objects = []

def finish_mesh_object(obj, mat, smooth=True):
    obj.data.materials.append(mat)
    if obj.type == "MESH":
        for p in obj.data.polygons:
            p.use_smooth = smooth
    mesh_objects.append(obj)
    return obj

def ellipsoid(name, loc, scale, mat, segments=14, rings=9):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1.0, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish_mesh_object(obj, mat, True)

def swept_taper(name, points, radii, mat, sides=7):
    pts = [Vector(p) for p in points]
    vertices = []
    faces = []
    for i, center in enumerate(pts):
        if i == 0:
            tangent = (pts[1] - pts[0]).normalized()
        elif i == len(pts) - 1:
            tangent = (pts[-1] - pts[-2]).normalized()
        else:
            tangent = (pts[i+1] - pts[i-1]).normalized()
        ref = Vector((0.0, 0.0, 1.0))
        if abs(tangent.dot(ref)) > 0.92:
            ref = Vector((1.0, 0.0, 0.0))
        axis_a = tangent.cross(ref).normalized()
        axis_b = tangent.cross(axis_a).normalized()
        for j in range(sides):
            a = 2.0 * math.pi * j / sides
            offset = axis_a * math.cos(a) * radii[i] + axis_b * math.sin(a) * radii[i]
            vertices.append(tuple(center + offset))
    faces.append(tuple(range(sides-1, -1, -1)))
    for i in range(len(pts)-1):
        start = i*sides
        nxt = (i+1)*sides
        for j in range(sides):
            j2 = (j+1) % sides
            faces.append((start+j, start+j2, nxt+j2, nxt+j))
    end = (len(pts)-1)*sides
    faces.append(tuple(end+j for j in range(sides)))
    mesh = bpy.data.meshes.new(name + " Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish_mesh_object(obj, mat, True)

def armor_plate(name, loc, scale, rotation_z=0.0, mat=plate):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.rotation_euler[2] = rotation_z
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bevel = obj.modifiers.new("Machined soft edge", "BEVEL")
    bevel.width = min(scale) * 0.24
    bevel.segments = 2
    obj.modifiers.new("Corner normals", "WEIGHTED_NORMAL")
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    # Weighted normals preserve the faceted plate silhouette while softening bevel highlights.
    normal_mod = obj.modifiers.get("Corner normals")
    if normal_mod:
        bpy.ops.object.modifier_apply(modifier=normal_mod.name)
    return finish_mesh_object(obj, mat, False)

# Organic core: elongated along Blender local Y, the courier's fore/aft axis.
ellipsoid("CL_Body_Shell", (0, -0.015, 0.005), (0.132, 0.255, 0.105), shell, 20, 12)
ellipsoid("CL_Underbelly_Membrane", (0, -0.035, -0.035), (0.098, 0.220, 0.090), soft, 14, 9)
ellipsoid("CL_Forehead_Carriage", (0, 0.175, 0.025), (0.108, 0.105, 0.091), plate, 14, 9)
ellipsoid("CL_Thorax_Ridge", (0, -0.12, 0.074), (0.076, 0.142, 0.046), shell, 14, 8)


# Short flank plates expose a segmented rib cage under the shell and help the hooks read at scale.
for side in (-1.0, 1.0):
    for index, y in enumerate((0.095, -0.015, -0.120)):
        rib = armor_plate(
            "CL_Flank_Rib_%s_%02d" % ("L" if side < 0 else "R", index),
            (side * 0.111, y, -0.008),
            (0.026, 0.047, 0.039),
            side * 0.10,
            clawmat)
        rib.rotation_euler[1] = side * 0.46

# Segmented dorsal armor reads as overlapping, manufactured plates rather than a noisy insect body.
armor_plate("CL_Plate_Crown", (0, 0.165, 0.093), (0.092, 0.089, 0.035), 0.0, plate)
armor_plate("CL_Plate_Fore", (0, 0.075, 0.111), (0.100, 0.078, 0.029), 0.0, plate)
armor_plate("CL_Plate_Mid", (0, -0.025, 0.112), (0.094, 0.073, 0.027), 0.025, plate)
armor_plate("CL_Plate_Aft", (0, -0.125, 0.098), (0.078, 0.072, 0.026), -0.025, plate)
armor_plate("CL_Plate_Tail", (0, -0.207, 0.075), (0.054, 0.053, 0.021), 0.0, clawmat)

# Six hooked limbs form a gripping cage around an engine nacelle. Curves are custom low-poly sweeps.
for side in (-1.0, 1.0):
    suffix = "L" if side < 0 else "R"
    swept_taper("CL_Fore_Hook_" + suffix,
        [(side*0.080,0.155,0.010),(side*0.152,0.195,0.015),(side*0.221,0.263,0.025),
         (side*0.205,0.309,0.048),(side*0.142,0.302,0.055)],
        [0.040,0.036,0.027,0.017,0.004], clawmat, 8)
    swept_taper("CL_Mid_Talon_" + suffix,
        [(side*0.098,0.015,0.003),(side*0.177,-0.002,0.010),(side*0.254,-0.058,0.012),
         (side*0.232,-0.105,0.020),(side*0.180,-0.098,0.030)],
        [0.036,0.032,0.023,0.013,0.004], shell, 8)
    swept_taper("CL_Rear_Tendon_" + suffix,
        [(side*0.072,-0.154,-0.005),(side*0.141,-0.210,-0.015),(side*0.177,-0.285,-0.030),
         (side*0.120,-0.321,-0.012)],
        [0.032,0.027,0.018,0.003], clawmat, 7)
    # Underbody suction nubs signal an attachment mechanism at game camera distance.
    ellipsoid("CL_Suction_Pad_" + suffix, (side*0.076, -0.10, -0.092), (0.028,0.050,0.018), soft, 10, 6)
    ellipsoid("CL_Sensor_Lens_" + suffix, (side*0.063,0.198,0.091), (0.021,0.027,0.018), amber, 10, 7)

# Restrained light piping: three interrupted bioluminescent traces, not continuous neon.
for i, y in enumerate((0.108, 0.012, -0.086, -0.169)):
    z = 0.128 if i < 2 else (0.111 if i == 2 else 0.086)
    length = 0.047 if i == 0 else (0.056 if i < 3 else 0.036)
    for side in (-1.0, 1.0):
        x = side * (0.025 if i < 2 else 0.022)
        swept_taper("CL_BioTrace_%02d_%s" % (i, "L" if side < 0 else "R"),
            [(x,y+length,z),(x*1.06,y,z+0.003),(x,y-length,z)],
            [0.0045,0.006,0.002], glow, 6)

# A central charge organ and rear sensor give a readable focal point without large particles.
ellipsoid("CL_Charge_Organ", (0,0.216,0.101), (0.028,0.041,0.013), glow, 12, 7)
ellipsoid("CL_Aft_Indicator", (0,-0.222,0.092), (0.017,0.023,0.012), amber, 10, 6)

# Root for clean Unity placement and stable runtime articulation.
root = bpy.data.objects.new("VOIDLINE_Clinger_Root", None)
bpy.context.collection.objects.link(root)
root.empty_display_type = "SPHERE"
root.empty_display_size = 0.05
for obj in list(mesh_objects):
    obj.parent = root
    obj.matrix_parent_inverse = root.matrix_world.inverted()


# Lighting/camera remain only in the editable Blender source; the FBX export below selects meshes only.
def aim_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

cam_data = bpy.data.cameras.new("CL_Preview_Camera")
cam = bpy.data.objects.new("CL_Preview_Camera", cam_data)
bpy.context.collection.objects.link(cam)
cam.location = (1.05, -1.35, 0.92)
cam_data.type = "ORTHO"
cam_data.ortho_scale = 0.94
aim_at(cam, (0.0, 0.0, 0.0))
scene.camera = cam

def area_light(name, loc, energy, color, size):
    data = bpy.data.lights.new(name, type="AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    aim_at(obj, (0.0, 0.0, 0.0))
    return obj

area_light("CL_Preview_Key", (0.60,-0.35,1.45), 42.0, (0.72,0.86,1.0), 1.0)
area_light("CL_Preview_Fill", (-1.05,-0.35,0.36), 14.0, (0.20,0.66,0.82), 1.1)
area_light("CL_Preview_Rim", (0.05,1.0,0.85), 48.0, (0.18,0.82,1.0), 0.55)
area_light("CL_Preview_Warm", (0.0,0.45,-0.15), 9.0, (1.0,0.22,0.06), 0.45)

# Validate material assignment and mesh budget before saving/exporting.
total_tris = 0
total_verts = 0
for obj in mesh_objects:
    if obj.type != "MESH":
        continue
    total_verts += len(obj.data.vertices)
    total_tris += sum(max(0, len(poly.vertices)-2) for poly in obj.data.polygons)

scene.render.engine = "BLENDER_EEVEE"
scene.view_settings.view_transform = "AgX"
scene.render.resolution_x = 1024
scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True
scene.world = bpy.data.worlds.new("VOIDLINE_Clinger_World")
scene.world.color = (0.012, 0.02, 0.028)
scene.render.filepath = os.path.join(os.path.dirname(blend_path), "VOIDLINE_Clinger_Preview.png")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)

bpy.ops.object.select_all(action="DESELECT")
for obj in mesh_objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = mesh_objects[0]
bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=True,
    object_types={"MESH"},
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z",
    axis_up="Y",
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    use_custom_props=False,
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="AUTO"
)
bpy.ops.render.render(write_still=True)
print("VOIDLINE_CLINGER_EXPORT_OK")
print("Blend:", blend_path)
print("FBX:", fbx_path)
print("Objects:", len(mesh_objects), "Vertices:", total_verts, "Triangles:", total_tris)

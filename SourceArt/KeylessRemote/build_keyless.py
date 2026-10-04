"""Build an editable, unbranded keyless fob, removable emergency key, and pouch.

Blender 5.2+:
    blender -b --python SourceArt/KeylessRemote/build_keyless.py

All measurements are in metres. The saved .blend uses the assembled state.
"""

from pathlib import Path
import math
import os
import sys

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
UNITY_INVENTORY = ROOT.parents[1] / "Assets/_Project/Art/Models/Props/Inventory"
PREVIEWS = Path("/tmp/saksi-terakhir-generated/previews/KeylessRemote")
PREVIEWS.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for collection in list(bpy.data.collections):
    if not collection.objects and collection.name != "Collection":
        bpy.data.collections.remove(collection)

scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.unit_settings.length_unit = "METERS"
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.render.resolution_x = 1440
scene.render.resolution_y = 1080
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.view_settings.view_transform = "AgX"
scene.view_settings.exposure = -0.6


def collection(name):
    coll = bpy.data.collections.new(name)
    scene.collection.children.link(coll)
    return coll


remote_col = collection("01 REMOTE KEYLESS - TANPA LOGO")
key_col = collection("02 KUNCI DARURAT - LEPAS")
pouch_col = collection("03 DOMPET KULIT - LEPAS")
studio_col = collection("STUDIO - TIDAK DIEKSPOR")


def material(name, rgba, metallic=0.0, roughness=0.55, grain=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*rgba, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgba, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if grain:
        tex = mat.node_tree.nodes.new("ShaderNodeTexNoise")
        tex.inputs["Scale"].default_value = 245.0
        tex.inputs["Detail"].default_value = 2.0
        bump = mat.node_tree.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = 0.18
        bump.inputs["Distance"].default_value = grain
        mat.node_tree.links.new(tex.outputs["Fac"], bump.inputs["Height"])
        mat.node_tree.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


plastic = material("Plastik charcoal tekstur halus", (0.012, 0.013, 0.014), 0.0, 0.74, 0.00014)
plastic_edge = material("Sisi bodi abu gelap", (0.008, 0.009, 0.010), 0.0, 0.80, 0.00008)
recess = material("Cekungan hitam", (0.003, 0.004, 0.005), 0.0, 0.61)
metal = material("Baja satin", (0.55, 0.57, 0.57), 0.83, 0.31)
key_blade_metal = material("Bilah baja agak gelap", (0.24, 0.25, 0.25), 0.78, 0.40)
metal_dark = material("Alur baja gelap", (0.22, 0.23, 0.24), 0.75, 0.43)
button_metal = material("Tombol aluminium terang", (0.72, 0.73, 0.72), 0.76, 0.27)
engraving = material("Simbol tombol", (0.20, 0.22, 0.23), 0.35, 0.48)
led_red = material("LED merah redup", (0.46, 0.014, 0.013), 0.10, 0.25)
led_bsdf = led_red.node_tree.nodes.get("Principled BSDF")
led_bsdf.inputs["Emission Color"].default_value = (0.64, 0.006, 0.006, 1.0)
led_bsdf.inputs["Emission Strength"].default_value = 0.7
leather = material("Kulit hitam matte", (0.008, 0.008, 0.009), 0.0, 0.81, 0.00017)
leather_edge = material("Tepi kulit gelap", (0.003, 0.003, 0.004), 0.0, 0.84, 0.00012)
thread = material("Benang jahitan abu gelap", (0.040, 0.040, 0.038), 0.0, 0.83)


def move_to_collection(obj, coll):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    coll.objects.link(obj)


def empty(name, coll, parent=None):
    obj = bpy.data.objects.new(name, None)
    coll.objects.link(obj)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.012
    if parent:
        obj.parent = parent
    return obj


def bevel_box(name, dims, loc, radius, mat, coll, parent=None, segments=7):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    move_to_collection(obj, coll)
    obj.dimensions = dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if radius:
        mod = obj.modifiers.new("Sudut membulat", "BEVEL")
        mod.width = radius
        mod.segments = segments
        mod.affect = "EDGES"
        mod.loop_slide = True
        normal = obj.modifiers.new("Normal permukaan", "WEIGHTED_NORMAL")
        normal.keep_sharp = True
    obj.data.materials.append(mat)
    if parent:
        obj.parent = parent
    return obj


def apply_modifiers(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    for mod in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.select_set(False)


def subtract_shape(target, cutter):
    apply_modifiers(target)
    apply_modifiers(cutter)
    mod = target.modifiers.new("Rongga fisik", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.solver = "EXACT"
    mod.object = cutter
    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.modifier_apply(modifier=mod.name)
    target.select_set(False)
    bpy.data.objects.remove(cutter, do_unlink=True)


def line(name, points, depth, mat, coll, parent=None, cyclic=False):
    curve = bpy.data.curves.new(name + " curve", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 16
    curve.bevel_depth = depth
    curve.bevel_resolution = 2
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for point, coord in zip(spline.points, points):
        point.co = (*coord, 1.0)
    spline.use_cyclic_u = cyclic
    obj = bpy.data.objects.new(name, curve)
    coll.objects.link(obj)
    obj.data.materials.append(mat)
    if parent:
        obj.parent = parent
    return obj


def torus(name, loc, major, minor, mat, coll, parent=None, rot=None):
    bpy.ops.mesh.primitive_torus_add(
        major_segments=64,
        minor_segments=10,
        location=loc,
        major_radius=major,
        minor_radius=minor,
    )
    obj = bpy.context.object
    obj.name = name
    move_to_collection(obj, coll)
    if rot:
        obj.rotation_euler = rot
    obj.data.materials.append(mat)
    if parent:
        obj.parent = parent
    return obj


remote_root = empty("REMOTE_KEYLESS_ROOT", remote_col)
remote_root["part"] = "Remot; logo belakang sengaja dihilangkan"
remote_root["front"] = "Tiga tombol vertikal dan LED kecil di ujung atas"

# Body: black, palm-sized fob (about 43 x 78 x 14 mm). The internal channel is
# genuinely empty and opens through the lower end where the pull-tab protrudes.
body = bevel_box(
    "Bodi remote berongga",
    (0.043, 0.078, 0.014),
    (0.0, 0.0, 0.0),
    0.0066,
    plastic,
    remote_col,
    remote_root,
    segments=11,
)
channel = bevel_box(
    "PEMOTONG - alur kunci darurat",
    (0.0105, 0.074, 0.0035),
    (0.0, -0.0030, -0.0018),
    0.0004,
    recess,
    remote_col,
)
subtract_shape(body, channel)

# Thin shell detail at the seam, placed only at the sides so the internal key
# channel stays accessible.
for side in (-1, 1):
    bevel_box(
        f"Sambungan bodi samping {'kiri' if side < 0 else 'kanan'}",
        (0.00042, 0.065, 0.00055),
        (side * 0.0211, 0, -0.0018),
        0.00020,
        plastic_edge,
        remote_col,
        remote_root,
    )

# Recessed central button well; precisely three separate press plates.
bevel_box(
    "Panel cekung tiga tombol",
    (0.0245, 0.0450, 0.0009),
    (0.0, 0.0, 0.0072),
    0.0026,
    recess,
    remote_col,
    remote_root,
)
buttons = [
    ("01 Kunci terkunci", 0.0140),
    ("02 Kunci terbuka", 0.0000),
    ("03 Tombol polos", -0.0140),
]
for label, y in buttons:
    bevel_box(
        "Tombol " + label,
        (0.0202, 0.0120, 0.00145),
        (0.0, y, 0.0082),
        0.00090,
        button_metal,
        remote_col,
        remote_root,
    )


def lock_icon(center_y, opened=False):
    z = 0.00906
    bevel_box(
        "Ikon gembok badan - " + ("terbuka" if opened else "terkunci"),
        (0.0041, 0.0028, 0.00010),
        (0.0, center_y - 0.0011, z),
        0.00035,
        engraving,
        remote_col,
        remote_root,
    )
    if opened:
        pts = [(-0.0016, center_y, z), (-0.0016, center_y + 0.0021, z),
               (-0.0008, center_y + 0.0030, z), (0.0007, center_y + 0.0030, z),
               (0.0017, center_y + 0.0021, z)]
    else:
        pts = [(-0.0016, center_y, z), (-0.0016, center_y + 0.0021, z),
               (-0.0008, center_y + 0.0030, z), (0.0008, center_y + 0.0030, z),
               (0.0016, center_y + 0.0021, z), (0.0016, center_y, z)]
    line("Ikon gembok lengkung", pts, 0.00016, engraving, remote_col, remote_root)


lock_icon(0.0140, opened=False)
lock_icon(0.0000, opened=True)

# Red indicator above the button stack (at the top of the front face).
bevel_box("Cekungan LED", (0.0049, 0.0023, 0.00065), (0, 0.0305, 0.00715),
          0.0007, recess, remote_col, remote_root)
bevel_box("LED kecil merah", (0.0023, 0.00115, 0.00042), (0, 0.0305, 0.00764),
          0.00025, led_red, remote_col, remote_root)

# The reverse is deliberately plain: no maker's symbol or writing. A little
# recessed sliding release sits close to the end where the key pulls out.
bevel_box("Cekungan sakelar pelepas", (0.0082, 0.0078, 0.00055),
          (0, -0.0240, -0.0071), 0.0010, recess, remote_col, remote_root)
bevel_box("Sakelar kecil pelepas kunci", (0.0021, 0.0052, 0.00072),
          (0.0005, -0.0240, -0.00755), 0.00030, metal_dark, remote_col, remote_root)
for dx in (-0.0045, 0.0045):
    bevel_box("Bibir lubang kunci", (0.0010, 0.0022, 0.0030),
              (dx, -0.0391, -0.0018), 0.00025, plastic_edge, remote_col, remote_root)


key_root = empty("KUNCI_DARURAT_ROOT__GESER_Y_NEGATIF_UNTUK_MELEPAS", key_col)
key_root["initial_state"] = "Bilah berada di rongga remote; kepala logam menonjol di bawah"
key_root["pull_direction"] = "Local -Y, kira-kira 0.10 m sampai seluruh bilah keluar"

# Flat notched blade. Its long direction is +Y; moving the key root along -Y
# slides it out of the fob without moving the remote.
outline = [
    (-0.0040, -0.0385), (-0.0040, -0.0280),
    (-0.0032, -0.0268), (-0.0032, -0.0240),
    (-0.0040, -0.0220), (-0.0040, -0.0165),
    (-0.0031, -0.0145), (-0.0031, -0.0115),
    (-0.0040, -0.0090), (-0.0040, 0.0290),
    (-0.0033, 0.0335), (0.0033, 0.0335),
    (0.0040, 0.0290), (0.0040, 0.0160),
    (0.0031, 0.0140), (0.0031, 0.0110),
    (0.0040, 0.0090), (0.0040, -0.0030),
    (0.0030, -0.0050), (0.0030, -0.0080),
    (0.0040, -0.0100), (0.0040, -0.0385),
]
z_middle = -0.0018
half_depth = 0.0010
count = len(outline)
verts = [(x, y, z_middle - half_depth) for x, y in outline]
verts += [(x, y, z_middle + half_depth) for x, y in outline]
faces = [tuple(reversed(range(count))), tuple(range(count, 2 * count))]
faces += [(i, (i + 1) % count, (i + 1) % count + count, i + count)
          for i in range(count)]
mesh = bpy.data.meshes.new("Bilah dengan gerigi samping")
mesh.from_pydata(verts, [], faces)
mesh.update()
blade = bpy.data.objects.new("Bilah kunci fisik - baja berprofil", mesh)
key_col.objects.link(blade)
blade.data.materials.append(key_blade_metal)
blade.parent = key_root
bevel = blade.modifiers.new("Tepi logam", "BEVEL")
bevel.width = 0.00020
bevel.segments = 2

for dx in (-0.00165, 0.00165):
    bevel_box("Alur memanjang bilah baja", (0.00042, 0.052, 0.00012),
              (dx, 0.002, -0.00073), 0.00004,
              metal_dark, key_col, key_root, 2)

# A wide metal pull tab is the only part visible when the blade is inserted.
head = bevel_box("Gagang tarik kunci darurat", (0.0166, 0.0155, 0.0019),
                 (0, -0.0458, z_middle), 0.0022, metal, key_col, key_root)
hole = bevel_box("PEMOTONG - lubang gantungan", (0.0103, 0.0042, 0.0050),
                 (0, -0.0483, z_middle), 0.0019, metal, key_col)
subtract_shape(head, hole)
bevel_box("Leher antara bilah dan gagang", (0.0092, 0.0063, 0.0021),
          (0, -0.0379, z_middle), 0.0009, metal, key_col, key_root)


# The leather wallet/pouch is an independent root, parented to the key root
# only so it follows the key when pulled out. Clear that parent to detach it.
pouch_root = empty("DOMPET_KULIT_ROOT__BISA_DILEPAS", pouch_col, key_root)
pouch_root["detach"] = "Clear Parent > Keep Transform; cincin ikut aksesori"
pouch_root["model_note"] = "Gantungan/dompet kulit, bukan bagian elektronik remote"

bevel_box("Dompet kulit - lapisan belakang", (0.098, 0.067, 0.0032),
          (0.054, -0.096, -0.0129), 0.0060, leather_edge, pouch_col, pouch_root)
bevel_box("Dompet kulit - lapisan depan", (0.096, 0.065, 0.0028),
          (0.054, -0.0952, -0.0098), 0.0058, leather, pouch_col, pouch_root)
bevel_box("Pinggiran kulit terlipat", (0.098, 0.0045, 0.0018),
          (0.054, -0.0650, -0.0092), 0.0019, leather_edge, pouch_col, pouch_root)
line("Garis bukaan dompet", [(0.012, -0.0700, -0.00818),
                            (0.096, -0.0700, -0.00818)],
     0.00016, leather_edge, pouch_col, pouch_root)

tab = bevel_box("Lidah kulit untuk cincin", (0.019, 0.018, 0.0028),
                (0.018, -0.061, -0.0068), 0.0030, leather, pouch_col, pouch_root)
tab_hole = bevel_box("PEMOTONG - lubang lidah kulit", (0.0048, 0.0060, 0.0060),
                     (0.018, -0.0575, -0.0068), 0.0020, leather, pouch_col)
subtract_shape(tab, tab_hole)
torus("Cincin baja penghubung", (0.009, -0.0530, -0.0040),
      0.0102, 0.0011, metal, pouch_col, pouch_root, rot=(0.23, -0.16, 0))

# Small dashed leather stitching, created as one curve object.
stitch_curve = bpy.data.curves.new("Jahitan tepi halus", "CURVE")
stitch_curve.dimensions = "3D"
stitch_curve.bevel_depth = 0.00019
stitch_curve.bevel_resolution = 1
cx, cy, stitch_z = 0.054, -0.0952, -0.00824
xl, xr = cx - 0.0420, cx + 0.0420
yb, yt = cy - 0.0270, cy + 0.0270
stitches = []
for x in [xl + i * 0.0022 for i in range(38)]:
    stitches.extend([[(x, yb, stitch_z), (x + 0.00095, yb, stitch_z)],
                     [(x, yt, stitch_z), (x + 0.00095, yt, stitch_z)]])
for y in [yb + i * 0.0022 for i in range(25)]:
    stitches.extend([[(xl, y, stitch_z), (xl, y + 0.00095, stitch_z)],
                     [(xr, y, stitch_z), (xr, y + 0.00095, stitch_z)]])
for pair in stitches:
    spline = stitch_curve.splines.new("POLY")
    spline.points.add(1)
    for p, coord in zip(spline.points, pair):
        p.co = (*coord, 1.0)
stitch_obj = bpy.data.objects.new("Jahitan keliling dompet", stitch_curve)
pouch_col.objects.link(stitch_obj)
stitch_obj.data.materials.append(thread)
stitch_obj.parent = pouch_root


# Neutral studio lighting. Cameras and lights remain outside the export.
world = bpy.data.worlds.new("Studio abu netral")
world.use_nodes = True
world.node_tree.nodes.get("Background").inputs["Color"].default_value = (
    0.16, 0.17, 0.18, 1.0
)
world.node_tree.nodes.get("Background").inputs["Strength"].default_value = 0.35
scene.world = world


def area_light(name, loc, energy, size, color):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    data.color = color
    obj = bpy.data.objects.new(name, data)
    studio_col.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = (Vector((0, -0.038, 0)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


area_light("Softbox depan besar", (0.09, 0.07, 0.15), 1.5, 0.30, (1.0, 0.96, 0.91))
area_light("Softbox sisi dingin", (-0.10, -0.04, 0.08), 1.1, 0.22, (0.79, 0.85, 1.0))
area_light("Softbox belakang", (0.02, 0.09, -0.13), 1.1, 0.25, (1.0, 1.0, 1.0))


def camera(name, loc, target, ortho):
    data = bpy.data.cameras.new(name)
    data.type = "ORTHO"
    data.ortho_scale = ortho
    obj = bpy.data.objects.new(name, data)
    studio_col.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


front_cam = camera("Kamera depan - tiga tombol", (0.082, -0.085, 0.205),
                   (0, -0.003, 0), 0.137)
back_cam = camera("Kamera belakang - tanpa logo", (-0.082, -0.068, -0.205),
                  (0, -0.003, 0), 0.137)
assembly_cam = camera("Kamera remot plus dompet", (0.120, -0.137, 0.25),
                      (0.035, -0.043, -0.006), 0.31)
exploded_cam = camera("Kamera bagian terpisah", (0.125, -0.183, 0.31),
                      (0.050, -0.070, -0.006), 0.40)


def render(name, cam):
    scene.camera = cam
    scene.render.filepath = str(PREVIEWS / name)
    bpy.ops.render.render(write_still=True)
    print("Rendered preview:", scene.render.filepath)


# Both first views show the fob without its attached wallet, making small
# controls and the blank reverse easier to judge.
for obj in pouch_col.objects:
    obj.hide_render = True
render("01_remote_depan.png", front_cam)
render("02_remote_belakang.png", back_cam)
for obj in pouch_col.objects:
    obj.hide_render = False
render("03_terpasang_dengan_dompet.png", assembly_cam)

# Present how the independent objects separate. Reset transforms afterward:
# the editable .blend and export always start with the key stored inside.
key_root.location.y = -0.105
pouch_root.location.x = 0.100
pouch_root.location.y = 0.105
bpy.context.view_layer.update()
render("04_tiga_bagian_terpisah.png", exploded_cam)
key_root.location.y = 0
pouch_root.location.x = 0
pouch_root.location.y = 0
bpy.context.view_layer.update()

scene.camera = assembly_cam
scene.render.filepath = str(PREVIEWS / "03_terpasang_dengan_dompet.png")
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == "VIEW_3D":
            area.spaces.active.shading.type = "MATERIAL"
            area.spaces.active.region_3d.view_location = (0.035, -0.045, 0)
            area.spaces.active.region_3d.view_distance = 0.23

bpy.ops.object.select_all(action="DESELECT")
remote_root.select_set(True)
bpy.context.view_layer.objects.active = remote_root
blend_path = ROOT / "Remote_Keyless_Tanpa_Logo.blend"
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
print("Saved editable model:", blend_path)

# SourceArt is outside Unity Assets so the user can review before importing.
for coll in (remote_col, key_col, pouch_col):
    for obj in coll.objects:
        obj.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=str(UNITY_INVENTORY / "Remote_Keyless_Separated_Parts.fbx"),
    use_selection=True,
    object_types={"MESH", "EMPTY"},
    apply_unit_scale=True,
    axis_forward="-Z",
    axis_up="Y",
)
print("Saved Unity-ready FBX:", UNITY_INVENTORY / "Remote_Keyless_Separated_Parts.fbx")

# Some headless audio backends retain a background main loop after all
# artifacts have been closed; Blender has already finished the work here.
if bpy.app.background:
    sys.stdout.flush()
    sys.stderr.flush()
    os._exit(0)

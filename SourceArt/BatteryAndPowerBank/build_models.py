"""Create editable Blender inventory props for the Chapter 1 shop.

Run with Blender 5.2+: blender -b --python SourceArt/BatteryAndPowerBank/build_models.py
Measurements use metres. Each prop has its own .blend and .fbx file.
"""

from pathlib import Path
import math
import os
import subprocess
import sys

import bpy
from mathutils import Quaternion, Vector


ROOT = Path(__file__).resolve().parent
UNITY_INVENTORY = ROOT.parents[1] / "Assets/_Project/Art/Models/Props/Inventory"
PREVIEWS = Path("/tmp/saksi-terakhir-generated/previews/BatteryAndPowerBank")
PREVIEWS.mkdir(parents=True, exist_ok=True)


def new_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1100
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = -0.45
    world = bpy.data.worlds.new("Studio netral")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.22, 0.23, 0.25, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.30
    scene.world = world
    return scene


def collection(name):
    coll = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(coll)
    return coll


def put(obj, coll):
    for old in tuple(obj.users_collection):
        old.objects.unlink(obj)
    coll.objects.link(obj)
    return obj


def mat(name, color, metallic=0, roughness=0.5):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return m


def empty(name, coll, parent=None):
    obj = bpy.data.objects.new(name, None)
    coll.objects.link(obj)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.008
    if parent:
        obj.parent = parent
    return obj


def bevel(obj, width, segments=4, angle=None):
    if width:
        mod = obj.modifiers.new("Sudut halus", "BEVEL")
        mod.width = width
        mod.segments = segments
        mod.affect = "EDGES"
        if angle is not None:
            mod.limit_method = "ANGLE"
            mod.angle_limit = angle
    normal = obj.modifiers.new("Normal halus", "WEIGHTED_NORMAL")
    normal.keep_sharp = True
    return obj


def box(name, dims, loc, material, coll, parent=None, radius=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = put(bpy.context.object, coll)
    obj.name = name
    obj.dimensions = dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    if radius:
        bevel(obj, radius)
    if parent:
        obj.parent = parent
    return obj


def cylinder(name, radius, height, z, material, coll, parent=None, vertices=64):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius,
                                        depth=height, location=(0, 0, z))
    obj = put(bpy.context.object, coll)
    obj.name = name
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = len(polygon.vertices) == 4
    if parent:
        obj.parent = parent
    return obj


def printed_sleeve(name, radius, height, texture_path, coll, parent, segments=128):
    """A UV-mapped blue cylinder; all printing lives inside its flat image."""
    material = mat("PVC biru dengan tinta tercetak", (.03, .35, .76), .02, .61)
    image = bpy.data.images.load(str(texture_path), check_existing=True)
    image.pack()
    nodes = material.node_tree.nodes
    image_node = nodes.new("ShaderNodeTexImage")
    image_node.name = "Cetakan menyatu dengan selubung"
    image_node.image = image
    material.node_tree.links.new(image_node.outputs["Color"],
                                 nodes["Principled BSDF"].inputs["Base Color"])
    points = []
    for z in (-height/2, height/2):
        for i in range(segments + 1):
            a = 2 * math.pi * i / segments
            points.append((radius * math.cos(a), radius * math.sin(a), z))
    faces = [(i, i+1, segments+1+i+1, segments+1+i)
             for i in range(segments)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(points, [], faces)
    mesh.update()
    uv = mesh.uv_layers.new(name="Label menyatu dengan PVC")
    for i, polygon in enumerate(mesh.polygons):
        polygon.use_smooth = True
        for loop_idx, coordinate in zip(polygon.loop_indices, (
            (i/segments, 0), ((i+1)/segments, 0),
            ((i+1)/segments, 1), (i/segments, 1),
        )):
            uv.data[loop_idx].uv = coordinate
    obj = bpy.data.objects.new(name, mesh)
    coll.objects.link(obj)
    obj.data.materials.append(material)
    obj.parent = parent
    return obj


def text_mesh(name, body, loc, size, material, coll, parent=None, rotation=(0, 0, 0), align="LEFT"):
    curve = bpy.data.curves.new(name, "FONT")
    curve.body = body
    curve.size = size
    curve.extrude = 0.000025
    curve.align_x = align
    obj = bpy.data.objects.new(name, curve)
    coll.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = rotation
    obj.data.materials.append(material)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target="MESH")
    result = bpy.context.object
    for polygon in result.data.polygons:
        polygon.use_smooth = True
    result.select_set(False)
    if parent:
        result.parent = parent
    return result


def apply_modifiers(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    for mod in tuple(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.select_set(False)


def cut(target, cutter):
    apply_modifiers(target)
    apply_modifiers(cutter)
    modifier = target.modifiers.new("Rongga port", "BOOLEAN")
    modifier.operation = "DIFFERENCE"
    modifier.solver = "EXACT"
    modifier.object = cutter
    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    target.select_set(False)
    bpy.data.objects.remove(cutter, do_unlink=True)


def round_prism(name, width, length, depth, corner, loc, material, coll, parent=None):
    """Rounded XY outline with shallow bevel on the front/back edges."""
    hw, hl = width / 2, length / 2
    centers = [(hw-corner, -hl+corner, -90, 0),
               (hw-corner, hl-corner, 0, 90),
               (-hw+corner, hl-corner, 90, 180),
               (-hw+corner, -hl+corner, 180, 270)]
    outline = []
    steps = 12
    for cx, cy, start, end in centers:
        for k in range(steps + 1):
            a = math.radians(start + (end-start) * k / steps)
            outline.append((cx + corner * math.cos(a), cy + corner * math.sin(a)))
    n = len(outline)
    verts = [(x, y, -depth/2) for x, y in outline]
    verts += [(x, y, depth/2) for x, y in outline]
    faces = [tuple(reversed(range(n))), tuple(range(n, 2*n))]
    faces += [(i, (i+1) % n, (i+1) % n+n, i+n) for i in range(n)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    coll.objects.link(obj)
    obj.location = loc
    obj.data.materials.append(material)
    bevel(obj, 0.0011, segments=3, angle=0.55)
    if parent:
        obj.parent = parent
    return obj


def cable_curve(name, points, radius, material, coll, parent=None):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 24
    curve.bevel_depth = radius
    curve.bevel_resolution = 4
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points)-1)
    for p, xyz in zip(spline.bezier_points, points):
        p.co = xyz
        p.handle_left_type = "AUTO"
        p.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    coll.objects.link(obj)
    obj.data.materials.append(material)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target="MESH")
    result = bpy.context.object
    for polygon in result.data.polygons:
        polygon.use_smooth = True
    result.select_set(False)
    if parent:
        result.parent = parent
    return result


def camera(name, pos, look_at, scale, studio):
    data = bpy.data.cameras.new(name)
    data.type = "ORTHO"
    data.ortho_scale = scale
    obj = bpy.data.objects.new(name, data)
    studio.objects.link(obj)
    obj.location = pos
    obj.rotation_euler = (Vector(look_at) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


def light(name, pos, look_at, power, size, studio, color=(1, 1, 1)):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = power
    data.shape = "DISK"
    data.size = size
    data.color = color
    obj = bpy.data.objects.new(name, data)
    studio.objects.link(obj)
    obj.location = pos
    obj.rotation_euler = (Vector(look_at) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


def studio_setup(target, scale, cam_pos):
    studio = collection("STUDIO - jangan diekspor")
    light("Softbox kiri", (-0.13, 0.07, 0.20), target, 1.8, 0.25, studio, (1.0, .95, .88))
    light("Softbox kanan", (0.18, -0.09, 0.16), target, 1.3, 0.24, studio, (.84, .91, 1.0))
    light("Pantulan tepi", (0.02, 0.14, -0.10), target, 0.8, 0.20, studio)
    return camera("Kamera pratinjau", cam_pos, target, scale, studio)


def render(scene, cam, filename):
    scene.camera = cam
    scene.render.filepath = str(PREVIEWS / filename)
    bpy.ops.render.render(write_still=True)
    print("Rendered", scene.render.filepath, flush=True)


def save_and_export(scene, blend_name, fbx_name, export_collections, focus=(0, 0, 0), view_distance=.24):
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                area.spaces.active.shading.type = "MATERIAL"
                area.spaces.active.region_3d.view_location = focus
                area.spaces.active.region_3d.view_distance = view_distance
    bpy.ops.object.select_all(action="DESELECT")
    root = export_collections[0].objects.get(next((o.name for o in export_collections[0].objects if o.type == "EMPTY"), ""))
    if root:
        root.select_set(True)
        bpy.context.view_layer.objects.active = root
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / blend_name))
    bpy.ops.object.select_all(action="DESELECT")
    for coll in export_collections:
        for obj in coll.objects:
            obj.select_set(True)
    fbx_path = UNITY_INVENTORY / fbx_name
    bpy.ops.export_scene.fbx(filepath=str(fbx_path), use_selection=True,
                             object_types={"MESH", "EMPTY"}, apply_unit_scale=True,
                             axis_forward="-Z", axis_up="Y", path_mode="COPY",
                             embed_textures=True)
    print("Saved", ROOT / blend_name, "and", fbx_path, flush=True)


def build_battery():
    subprocess.run(["python3", str(ROOT / "make_label_texture.py")], check=True)
    scene = new_scene()
    asset = collection("BATERAI_LI_ION_18650 - satu sel")
    root = empty("BATERAI_LI_ION_18650_ROOT", asset)
    root["ukuran"] = "Sekitar diameter 18.6 mm x panjang 65 mm"
    root["label"] = "ICR 18650 2200mAh 3.7V; tinta menyatu dengan UV selubung"
    steel = mat("Kontak logam perak", (.53, .58, .61), .78, .29)
    graphite = mat("Cincin isolator abu gelap", (.045, .060, .073), .18, .60)

    # A single unbranded 18650 cell. The wrap reaches almost both ends;
    # the positive terminal is low, silver, and far smaller than before.
    cylinder("Inti silinder logam", .00903, .0632, 0, steel, asset, root, 128)
    printed_sleeve("Selubung biru - cetak UV rata", .00931, .0630,
                   ROOT / "LiIon_18650_Label.png", asset, root)
    cylinder("Piringan atas perak", .00830, .00056, .03165, steel, asset, root, 128)
    cylinder("Ring isolator atas", .00630, .00034, .03210, graphite, asset, root, 128)
    cylinder("Tombol positif perak rendah", .00345, .00082, .03254, steel, asset, root, 128)
    cylinder("Piringan negatif rata", .00720, .00042, -.03170, steel, asset, root, 128)
    for obj in asset.objects:
        if obj.type == "MESH" and obj.name.startswith(("Piringan", "Tombol")):
            bevel(obj, .00015, 3, .55)

    cam = studio_setup((0, 0, 0), .092, (.072, -.120, .063))
    render(scene, cam, "01_baterai_LiIon_18650.png")
    save_and_export(scene, "Baterai_LiIon_18650.blend",
                    "Baterai_LiIon_18650.fbx", [asset], view_distance=.12)


def port(body, asset, x, width, height, name, silver, dark, gold, root):
    y_outer = .0665
    cutter = box("PEMOTONG " + name, (width, .011, height),
                 (x, .0640, 0), dark, asset)
    cut(body, cutter)
    # Metal rim built as four individual, editable pieces around the actual cavity.
    thin = .00045
    for suffix, z in (("atas", height/2 + thin/2), ("bawah", -height/2 - thin/2)):
        box(f"Bingkai {name} {suffix}", (width + 2*thin, .00075, thin),
            (x, y_outer + .00015, z), silver, asset, root, .00010)
    for suffix, dx in (("kiri", -width/2-thin/2), ("kanan", width/2+thin/2)):
        box(f"Bingkai {name} {suffix}", (thin, .00075, height),
            (x+dx, y_outer + .00015, 0), silver, asset, root, .00010)
    box("Dasar gelap " + name, (width-.0006, .0005, height-.0005),
        (x, .0589, 0), dark, asset, root)
    if "USB-A" in name:
        box("Lidah dalam " + name, (width-.0028, .0050, .00085),
            (x, .0612, -.00067), dark, asset, root, .0002)
        for n in range(4):
            px = x + (n-1.5)*.00165
            box(f"Pin {n+1} {name}", (.0006, .0027, .00012),
                (px, .0616, -.00017), gold, asset, root)


def build_powerbank():
    scene = new_scene()
    bank = collection("POWER_BANK - bodi dan port")
    cable = collection("KABEL_USB_A_KE_C - bisa dilepas")
    bank_root = empty("POWER_BANK_ROOT", bank)
    bank_root["ukuran"] = "68 x 133 x 13 mm, desain tipis dengan sudut membulat"
    bank_root["port"] = "2 USB-A output dan 1 USB-C input pada satu sisi"
    cable_root = empty("KABEL_ROOT__GESER_UNTUK_MELEPAS", cable)
    cable_root["default"] = "USB-A tertancap pada Output 1; USB-C di ujung kabel"

    pearl = mat("Polimer putih hangat matte", (.67, .69, .68), .02, .49)
    face = mat("Permukaan putih lembut", (.80, .81, .78), .01, .56)
    side = mat("Garis sambungan abu", (.26, .29, .29), .12, .52)
    ink = mat("Teks abu gelap", (.22, .25, .25), 0, .62)
    cavity = mat("Rongga port hitam", (.014, .018, .020), .05, .65)
    metal = mat("Logam konektor satin", (.47, .51, .52), .81, .31)
    contacts = mat("Kontak kuningan USB", (.68, .47, .16), .77, .28)
    rubber = mat("Karet kabel hitam", (.018, .022, .024), 0, .79)
    led = mat("LED biru pucat", (.13, .58, .68), .04, .25)
    led.node_tree.nodes["Principled BSDF"].inputs["Emission Color"].default_value = (.11, .52, .69, 1)
    led.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = .55

    body = round_prism("Bodi utama - rongga port asli", .068, .133, .013,
                       .012, (0, 0, 0), pearl, bank, bank_root)
    # A front panel and a thin seam reproduce the smooth layered case.
    round_prism("Panel depan", .065, .130, .00065, .011,
                (0, 0, .00655), face, bank, bank_root)
    round_prism("Panel belakang", .065, .130, .00075, .011,
                (0, 0, -.00655), side, bank, bank_root)
    # Cut the port openings through the top side and keep the cavities visible.
    port(body, bank, -.0190, .0121, .0043, "USB-A OUT 1", metal, cavity, contacts, bank_root)
    port(body, bank, .0190, .0121, .0043, "USB-A OUT 2", metal, cavity, contacts, bank_root)
    port(body, bank, 0, .0077, .0028, "USB-C IN", metal, cavity, contacts, bank_root)

    for i in range(4):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=1,
                                            location=(-.0066 + i*.0044, .044, .00703))
        obj = put(bpy.context.object, bank)
        obj.name = f"Indikator daya {i+1}"
        obj.scale = (.00080, .00080, .00024)
        obj.data.materials.append(led if i < 3 else side)
        obj.parent = bank_root
    box("Tombol daya sisi kanan", (.0008, .011, .0024),
        (.0341, .041, 0), side, bank, bank_root, .00033)
    text_mesh("Kapasitas tanpa merek", "10000 mAh", (0, -.046, .00709), .0037,
              ink, bank, bank_root, align="CENTER")

    # The USB-A plug is already inserted into the left output. Both plugs and
    # the cable share a separate root, so the full cable can be removed.
    box("USB-A metal tertancap OUT 1", (.0110, .0145, .00355),
        (-.019, .0670, 0), metal, cable, cable_root, .00055)
    box("Rumah USB-A karet", (.0140, .0135, .0060),
        (-.019, .0792, 0), rubber, cable, cable_root, .0018)
    box("Strain relief USB-A", (.0098, .0050, .0048),
        (-.019, .0886, 0), rubber, cable, cable_root, .0018)
    cable_curve("Kabel lentur USB-A ke USB-C", [
        (-.019, .090, -.0003), (-.021, .114, -.001),
        (.018, .132, -.001), (.081, .123, -.001),
        (.108, .083, -.001), (.106, .020, -.001),
        (.086, -.045, -.001), (.083, -.060, -.001),
    ], .00155, rubber, cable, cable_root)
    box("Strain relief USB-C", (.0065, .0055, .0042),
        (.083, -.0615, -.001), rubber, cable, cable_root, .0012)
    box("Rumah konektor USB-C", (.0102, .0155, .0054),
        (.083, -.0702, -.001), rubber, cable, cable_root, .0020)
    box("Ujung USB-C logam", (.0074, .0078, .00265),
        (.083, -.0813, -.001), metal, cable, cable_root, .0010)
    box("Celah pada ujung USB-C", (.0045, .00018, .00065),
        (.083, -.08525, -.001), cavity, cable, cable_root, .00015)

    cam = studio_setup((.028, .014, 0), .285, (.162, .165, .225))
    cam.rotation_euler = (cam.rotation_euler.to_quaternion() @
                          Quaternion((0, 0, 1), math.pi)).to_euler()
    render(scene, cam, "02_powerbank_dengan_kabel.png")
    port_cam = camera("Kamera detail port", (.025, .142, .105),
                      (0, .060, 0), .120, bpy.data.collections["STUDIO - jangan diekspor"])
    port_cam.rotation_euler = (port_cam.rotation_euler.to_quaternion() @
                               Quaternion((0, 0, 1), math.pi)).to_euler()
    # An unobstructed port view is easier to inspect than a cropped cable.
    for obj in cable.objects:
        obj.hide_render = True
    render(scene, port_cam, "03_detail_tiga_port.png")
    for obj in cable.objects:
        obj.hide_render = False
    scene.camera = cam
    scene.render.filepath = str(PREVIEWS / "02_powerbank_dengan_kabel.png")
    save_and_export(scene, "Power_Bank_USB_A_to_C.blend", "Power_Bank_USB_A_to_C.fbx",
                    [bank, cable], focus=(.025, .008, 0), view_distance=.30)


build_battery()
if "--battery-only" not in sys.argv:
    build_powerbank()
print("Blender assets are ready in", ROOT, flush=True)
# Blender's headless audio backend can keep a background loop alive after save.
if bpy.app.background:
    sys.stdout.flush()
    sys.stderr.flush()
    os._exit(0)

"""Build two editable ten-metre rope props for Chapter 1 inventory.

Run: blender -b --python SourceArt/Ropes/build_rope_models.py
The main rope in each file has a measured centreline length of 10 m. Bundles
are presentation shapes; in-game unrolling/physics can be added later.
"""

from pathlib import Path
import math
import os
import subprocess
import sys

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
UNITY_INVENTORY = ROOT.parents[1] / "Assets/_Project/Art/Models/Props/Inventory"
PREVIEWS = Path("/tmp/saksi-terakhir-generated/previews/Ropes")
PREVIEWS.mkdir(parents=True, exist_ok=True)
subprocess.run(["python3", str(ROOT / "make_rope_textures.py")], check=True)


def scene_reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1200
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = -0.40
    world = bpy.data.worlds.new("Latar studio")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.20, .21, .22, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .30
    scene.world = world
    return scene


def collection(name):
    coll = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(coll)
    return coll


def empty(name, coll, parent=None, at=None):
    obj = bpy.data.objects.new(name, None)
    coll.objects.link(obj)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = .015
    if parent:
        obj.parent = parent
    if at is not None:
        obj.location = at
    return obj


def color_material(name, color, roughness=.8):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1)
    material.use_nodes = True
    shader = material.node_tree.nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Roughness"].default_value = roughness
    return material


def image_material(name, image_path, roughness, bump_distance=0):
    material = color_material(name, (.4, .4, .4), roughness)
    image = bpy.data.images.load(str(image_path), check_existing=True)
    image.pack()
    nodes = material.node_tree.nodes
    texture = nodes.new("ShaderNodeTexImage")
    texture.name = "Warna pilinan"
    texture.image = image
    texture.extension = "REPEAT"
    links = material.node_tree.links
    shader = nodes["Principled BSDF"]
    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    if bump_distance:
        bump = nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = .24
        bump.inputs["Distance"].default_value = bump_distance
        links.new(texture.outputs["Color"], bump.inputs["Height"])
        links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    return material


def racetrack(distance, radius, straight_half):
    """Return one upright U-loop, parameterized by length in the XZ plane."""
    up = 2 * straight_half
    arc = math.pi * radius
    if distance <= up:
        return radius, -straight_half + distance
    if distance <= up + arc:
        angle = (distance - up) / radius
        return radius * math.cos(angle), straight_half + radius * math.sin(angle)
    if distance <= 2*up + arc:
        return -radius, straight_half - (distance-up-arc)
    angle = math.pi + (distance - (2*up+arc)) / radius
    return radius * math.cos(angle), -straight_half + radius * math.sin(angle)


def path_length(points):
    return sum((b-a).length for a, b in zip(points, points[1:]))


def bundle_path(turns, radius, straight_half, columns, x_step, y_step,
                flare_min, loose_tail=.135, loop_var=.05, waviness=.003):
    centers = []
    rows = math.ceil(turns / columns)
    for row in range(rows):
        col_order = range(columns) if row % 2 == 0 else reversed(range(columns))
        for col in col_order:
            centers.append(((col-(columns-1)/2)*x_step,
                            (row-(rows-1)/2)*y_step))
    centers = centers[:turns]
    perimeter = 4*straight_half + 2*math.pi*radius
    samples_per_loop = math.ceil(perimeter / .006)
    main = []
    for i in range(turns*samples_per_loop + 1):
        turn_position = i / samples_per_loop
        turn = min(int(turn_position), turns-1)
        fraction = turn_position-turn
        nxt = min(turn+1, turns-1)
        ease = fraction*fraction*(3-2*fraction)
        variation = ((1-ease)*math.sin(2.17*turn+.2) +
                     ease*math.sin(2.17*nxt+.2))
        current_radius = radius*(1 + loop_var*variation)
        current_straight = straight_half*(1 + loop_var*.7*variation)
        current_perimeter = 4*current_straight + 2*math.pi*current_radius
        x, z = racetrack(fraction*current_perimeter,
                         current_radius, current_straight)
        cx = centers[turn][0]*(1-ease) + centers[nxt][0]*ease
        cy = centers[turn][1]*(1-ease) + centers[nxt][1]*ease
        # The middle is lightly cinched; the loops flare toward both U ends.
        flare = flare_min + (1-flare_min) * (abs(z)/(current_straight+current_radius))**.8
        x = ((x+cx)*flare + waviness*math.sin(2*math.pi*fraction)*
             math.sin(1.31*turn+.7))
        y = cy + waviness*.45*math.sin(4*math.pi*turn_position)
        main.append(Vector((x, y, z)))

    # A short drooping cut end at the bottom and another emerging beside the
    # bundle keep the shape from reading as a rigid closed spring.
    start, end = main[0], main[-1]
    points = []
    for i in range(21):
        t = i/20
        points.append(start + Vector((.024*(1-t), -.009*(1-t),
                                      -loose_tail*(1-t) + .007*math.sin(math.pi*t))))
    points.extend(main[1:])
    for i in range(1, 23):
        t = i/22
        points.append(end + Vector((.025*t, -.011*t,
                                    loose_tail*t - .009*math.sin(math.pi*t))))

    # Scale only the centreline. The physical rope diameter remains unchanged.
    scale = 10.0 / path_length(points)
    points = [p*scale for p in points]
    measured = path_length(points)
    assert abs(measured-10.0) < .00001, measured
    return points, measured


def tube(name, points, radius, pitch, material, end_material, coll, root,
         texture_repeat=.05, sides=12):
    cumulative = [0.0]
    for a, b in zip(points, points[1:]):
        cumulative.append(cumulative[-1] + (b-a).length)
    ring_count = len(points)
    vertices = []
    reference = Vector((0, 1, 0))
    for i, center in enumerate(points):
        tangent = (points[min(i+1, ring_count-1)] -
                   points[max(i-1, 0)]).normalized()
        normal = tangent.cross(reference).normalized()
        binormal = tangent.cross(normal).normalized()
        for side in range(sides+1):
            angle = 2*math.pi*side/sides
            # Shallow three-strand relief; most fiber detail lives in the UV.
            relief = 1 + .055*math.cos(3*angle - 2*math.pi*cumulative[i]/pitch)
            offset = (normal*math.cos(angle) + binormal*math.sin(angle))*radius*relief
            vertices.append(tuple(center+offset))
    stride = sides+1
    faces = []
    for i in range(ring_count-1):
        for side in range(sides):
            a = i*stride+side
            b = (i+1)*stride+side
            faces.append((a, a+1, b+1, b))
    side_faces = len(faces)
    faces.append(tuple(reversed(range(sides))))
    faces.append(tuple((ring_count-1)*stride+j for j in range(sides)))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    mesh.materials.append(material)
    mesh.materials.append(end_material)
    uv = mesh.uv_layers.new(name="Pilinan mengikuti panjang tali")
    for i, polygon in enumerate(mesh.polygons):
        if i >= side_faces:
            polygon.material_index = 1
            continue
        polygon.use_smooth = True
        segment, side = divmod(i, sides)
        coords = ((side/sides, cumulative[segment]/texture_repeat),
                  ((side+1)/sides, cumulative[segment]/texture_repeat),
                  ((side+1)/sides, cumulative[segment+1]/texture_repeat),
                  (side/sides, cumulative[segment+1]/texture_repeat))
        for loop_idx, coord in zip(polygon.loop_indices, coords):
            uv.data[loop_idx].uv = coord
    obj = bpy.data.objects.new(name, mesh)
    coll.objects.link(obj)
    obj.parent = root
    obj["panjang_jalur_meter"] = round(cumulative[-1], 6)
    return obj


def guide_curve(points, coll):
    curve = bpy.data.curves.new("Jalur pusat 10 meter", "CURVE")
    curve.dimensions = "3D"
    spline = curve.splines.new("POLY")
    spline.points.add(len(points)-1)
    for point, coord in zip(spline.points, points):
        point.co = (*coord, 1)
    obj = bpy.data.objects.new("PANDUAN_JALUR_10M__SEMBUNYI", curve)
    coll.objects.link(obj)
    obj.hide_render = True
    obj.hide_viewport = True
    obj["catatan"] = "Referensi bentuk; jalur 10 m untuk pemodelan ulang"
    return obj


def binder_path(rx, ry, turns=7, height=.060):
    points = []
    steps = 96
    for i in range(turns*steps + 1):
        phase = i / steps
        angle = 2*math.pi*phase
        z = -height/2 + height*phase/turns
        points.append(Vector((rx*math.cos(angle), ry*math.sin(angle), z)))
    return points


def paper_band(half_width, half_depth, height, texture, coll, root):
    material = image_material("Kertas label nilon 8 mm 10 m", texture, .91)
    # A softly rounded rectangle hugs the folded strands better than an oval.
    corner = .015
    outline = [(0.0, half_depth), (half_width-corner, half_depth)]
    corners = (
        (half_width-corner, half_depth-corner, 90, 0),
        (half_width-corner, -half_depth+corner, 0, -90),
        (-half_width+corner, -half_depth+corner, -90, -180),
        (-half_width+corner, half_depth-corner, 180, 90),
    )
    for index, (cx, cy, start, end) in enumerate(corners):
        if index == 1:
            outline.append((half_width, -half_depth+corner))
        elif index == 2:
            outline.extend([(0.0, -half_depth), (-half_width+corner, -half_depth)])
        elif index == 3:
            outline.append((-half_width, half_depth-corner))
        for step in range(1, 17):
            angle = math.radians(start+(end-start)*step/16)
            outline.append((cx+corner*math.cos(angle),
                            cy+corner*math.sin(angle)))
    outline.append((0.0, half_depth))
    distances = [0.0]
    for a, b in zip(outline, outline[1:]):
        distances.append(distances[-1] + math.dist(a, b))
    perimeter = distances[-1]
    count = len(outline)
    vertices = []
    for z in (-height/2, height/2):
        vertices.extend((x, y, z) for x, y in outline)
    faces = [(i, count+i, count+i+1, i+1) for i in range(count-1)]
    mesh = bpy.data.meshes.new("Pita kertas pembungkus")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    uv = mesh.uv_layers.new(name="Teks 8 mm 10 m pada pita")
    for i, polygon in enumerate(mesh.polygons):
        polygon.use_smooth = True
        u0 = 1 - distances[i]/perimeter
        u1 = 1 - distances[i+1]/perimeter
        coords = ((u0, 0), (u0, 1), (u1, 1), (u1, 0))
        for loop_idx, coord in zip(polygon.loop_indices, coords):
            uv.data[loop_idx].uv = coord
    obj = bpy.data.objects.new("Pita kertas 8 mm - 10 m", mesh)
    coll.objects.link(obj)
    obj.data.materials.append(material)
    obj.parent = root
    return obj


def light(name, position, target, power, size, studio, tint=(1, 1, 1)):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = power
    data.size = size
    data.color = tint
    obj = bpy.data.objects.new(name, data)
    studio.objects.link(obj)
    obj.location = position
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


def camera(name, position, target, scale, studio):
    data = bpy.data.cameras.new(name)
    data.type = "ORTHO"
    data.ortho_scale = scale
    obj = bpy.data.objects.new(name, data)
    studio.objects.link(obj)
    obj.location = position
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


def studio(target, scale):
    coll = collection("STUDIO - tidak diekspor")
    light("Softbox kiri hangat", (-.27, -.18, .38), target, 2.4, .42,
          coll, (1, .94, .87))
    light("Softbox kanan dingin", (.30, -.25, .22), target, 1.4, .36,
          coll, (.84, .91, 1))
    light("Lampu tepi", (.02, .28, .22), target, 1.2, .30, coll)
    return camera("Kamera pratinjau", (.18, -.42, .19), target, scale, coll)


def save_asset(scene, camera_obj, name, coll, guide, root):
    scene.camera = camera_obj
    scene.render.filepath = str(PREVIEWS / (name + ".png"))
    bpy.ops.render.render(write_still=True)
    print("Rendered", scene.render.filepath, flush=True)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                area.spaces.active.shading.type = "MATERIAL"
                area.spaces.active.region_3d.view_location = (0, 0, 0)
                area.spaces.active.region_3d.view_distance = .7
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / (name + ".blend")))
    bpy.ops.object.select_all(action="DESELECT")
    for obj in coll.objects:
        obj.select_set(True)
    fbx_path=UNITY_INVENTORY/(name+".fbx")
    bpy.ops.export_scene.fbx(filepath=str(fbx_path),
                             use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_unit_scale=True, axis_forward="-Z", axis_up="Y",
                             path_mode="COPY", embed_textures=True)
    print("Saved", ROOT/(name+".blend"), fbx_path, "with 10 m main rope", flush=True)


def build_jute():
    scene = scene_reset()
    asset = collection("TALI TAMBANG - item")
    guide = collection("PANDUAN JALUR - sumber")
    root = empty("TALI_TAMBANG_10M_ROOT", asset)
    root["jenis"] = "Tali tambang serat alami"
    root["panjang_utama_meter"] = 10.0
    root["diameter_mm"] = 12
    root["bentuk"] = "Ikatan memanjang lentur dengan lilitan tengah"
    path, actual = bundle_path(13, .058, .0955, 4, .014, .011,
                               flare_min=.86, loose_tail=.080,
                               loop_var=.048, waviness=.003)
    material = image_material("Serat tambang pilin krem",
                              ROOT / "Textures/Tali_Tambang_Serat.png", .89, .00045)
    cut_material = color_material("Potongan serat tambang", (.40, .31, .19), .93)
    rope = tube("TALI_UTAMA_10M", path, .006, .038, material, cut_material,
                asset, root, texture_repeat=.05)
    rope["panjang_terukur_meter"] = actual
    guide_curve(path, guide)
    empty("UJUNG_A", asset, root, path[0])
    empty("UJUNG_B", asset, root, path[-1])
    binding = binder_path(.083, .030, 7, .063)
    tube("LILITAN_PENGIKAT_TENGAH", binding, .0035, .031,
         material, cut_material, asset, root, texture_repeat=.05, sides=10)
    cam = studio((0, 0, 0), .46)
    save_asset(scene, cam, "Tali_Tambang_10m", asset, guide, root)


def build_nylon():
    scene = scene_reset()
    asset = collection("TALI NILON - item")
    guide = collection("PANDUAN JALUR - sumber")
    root = empty("TALI_NILON_10M_ROOT", asset)
    root["jenis"] = "Tali nilon pilin hijau gelap dengan serat merah"
    root["panjang_utama_meter"] = 10.0
    root["diameter_mm"] = 8
    root["bentuk"] = "Ikatan oval longgar dengan pita kertas 8 mm / 10 m"
    path, actual = bundle_path(9, .095, .120, 3, .023, .023,
                               flare_min=.91, loose_tail=.115,
                               loop_var=.08, waviness=.005)
    material = image_material("Nilon hijau dengan flek merah",
                              ROOT / "Textures/Tali_Nilon_Hijau_Merah.png", .53, .00026)
    cut_material = color_material("Potongan nilon gelap", (.02, .17, .15), .67)
    rope = tube("TALI_UTAMA_10M", path, .004, .027, material, cut_material,
                asset, root, texture_repeat=.045)
    rope["panjang_terukur_meter"] = actual
    guide_curve(path, guide)
    empty("UJUNG_A", asset, root, path[0])
    empty("UJUNG_B", asset, root, path[-1])
    paper_band(.125, .031, .061, ROOT / "Textures/Pita_Kertas_Nilon_10m.png",
               asset, root)
    cam = studio((0, 0, 0), .60)
    save_asset(scene, cam, "Tali_Nilon_10m", asset, guide, root)


build_jute()
build_nylon()
print("Both ten-metre rope props are ready in", ROOT, flush=True)
if bpy.app.background:
    sys.stdout.flush()
    sys.stderr.flush()
    os._exit(0)

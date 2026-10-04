"""Build an editable wrapped-rice prop, FBX and two studio previews.

Run from the repository root:
    blender -b --python SourceArt/NasiBungkus/build_nasi_bungkus.py

The package is a closed, slightly puffy kraft-paper envelope. Its fold flaps,
banana-leaf ribbon and sticker remain separate, named meshes for later edits.
"""

from pathlib import Path
import math
import subprocess

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
UNITY_INVENTORY = ROOT.parents[1] / "Assets/_Project/Art/Models/Props/Inventory"
TEXTURES = ROOT / "Textures"
PREVIEWS = Path("/tmp/saksi-terakhir-generated/previews/NasiBungkus")
PREVIEWS.mkdir(parents=True, exist_ok=True)
subprocess.run(["python3", str(ROOT / "make_textures.py")], check=True)


def collection(name):
    result = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(result)
    return result


def material(name, color, roughness=0.8, texture=None, bump=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Roughness"].default_value = roughness
    if texture:
        image = bpy.data.images.load(str(TEXTURES / texture), check_existing=True)
        image.pack()
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = image
        mat.node_tree.links.new(tex.outputs["Color"], shader.inputs["Base Color"])
        if bump:
            node = mat.node_tree.nodes.new("ShaderNodeBump")
            node.inputs["Strength"].default_value = 0.17
            node.inputs["Distance"].default_value = bump
            mat.node_tree.links.new(tex.outputs["Color"], node.inputs["Height"])
            mat.node_tree.links.new(node.outputs["Normal"], shader.inputs["Normal"])
    return mat


def mesh_object(name, vertices, faces, mat, coll, root, uv_function=None,
                solidify=0, smooth=False):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    mesh.materials.append(mat)
    if uv_function:
        layer = mesh.uv_layers.new(name="UV tekstur")
        for polygon in mesh.polygons:
            for loop_index in polygon.loop_indices:
                vertex = mesh.vertices[mesh.loops[loop_index].vertex_index]
                layer.data[loop_index].uv = uv_function(vertex.co)
    for polygon in mesh.polygons:
        polygon.use_smooth = smooth
    obj = bpy.data.objects.new(name, mesh)
    coll.objects.link(obj)
    obj.parent = root
    if solidify:
        mod = obj.modifiers.new("Ketebalan lembar", "SOLIDIFY")
        mod.thickness = solidify
        mod.offset = -1
        mod.use_even_offset = True
    return obj


def paper_uv(co):
    return ((co.x + .112) / .224, co.z / .212)


def leaf_uv(co):
    return ((co.x + .067) / .046, co.z / .205)


def body_mesh(coll, root, paper):
    # Each ring changes width and depth slightly: a soft, hand-folded packet.
    rings = [
        (.002, .074, .027),
        (.018, .088, .038),
        (.067, .095, .041),
        (.121, .098, .041),
        (.166, .090, .038),
        (.187, .079, .029),
    ]
    vertices = []
    for i, (z, half_width, half_depth) in enumerate(rings):
        chamfer = .009 if i else .013
        wobble = .0012 * math.sin(i * 1.67)
        vertices.extend([
            (-half_width + chamfer, -half_depth, z + wobble),
            (half_width - chamfer, -half_depth, z - .0006 * i),
            (half_width, -half_depth + chamfer, z + .0004),
            (half_width, half_depth - chamfer, z - .0002),
            (half_width - chamfer, half_depth, z),
            (-half_width + chamfer, half_depth, z + .0008),
            (-half_width, half_depth - chamfer, z + .0002),
            (-half_width, -half_depth + chamfer, z),
        ])
    faces = []
    for ring in range(len(rings) - 1):
        for edge in range(8):
            a = 8 * ring + edge
            b = 8 * ring + (edge + 1) % 8
            faces.append((a, b, b + 8, a + 8))
    faces.append(tuple(reversed(range(8))))
    faces.append(tuple(8 * (len(rings) - 1) + i for i in range(8)))
    return mesh_object("01 Isi tertutup - bentuk kertas menggembung", vertices,
                       faces, paper, coll, root, paper_uv, smooth=True)


def front_y(z):
    # Match the broad front plane of the tapered body.
    profile = [(0, .027), (.018, .038), (.067, .041),
               (.121, .041), (.166, .038), (.187, .029)]
    for (z0, d0), (z1, d1) in zip(profile, profile[1:]):
        if z <= z1:
            t = max(0, (z - z0) / (z1 - z0))
            return -(d0 * (1-t) + d1 * t)
    return -.029


def front_sheet(name, outline, offset, mat, coll, root, uv=paper_uv,
                thickness=.00065):
    signed_area = sum(outline[i][0] * outline[(i+1) % len(outline)][1]
                      - outline[(i+1) % len(outline)][0] * outline[i][1]
                      for i in range(len(outline)))
    if signed_area < 0:
        outline = list(reversed(outline))
    vertices = [(x, front_y(z) - offset, z) for x, z in outline]
    return mesh_object(name, vertices, [tuple(range(len(vertices)))], mat,
                       coll, root, uv, solidify=thickness)


def folded_paper(coll, root, paper, light_paper, shadow_paper, crease):
    # The pointed sheets overlap like the reference's envelope closure.
    front_sheet("02 Lipatan bawah", [
        (-.084, .018), (-.052, .046), (-.003, .078),
        (.071, .041), (.084, .017), (.065, .009), (-.071, .009),
    ], .0012, shadow_paper, coll, root)
    front_sheet("03 Flap kiri turun diagonal", [
        (-.086, .166), (-.027, .168), (.052, .139),
        (.015, .089), (-.010, .061), (-.067, .115),
    ], .0020, light_paper, coll, root)
    front_sheet("04 Flap kanan silang", [
        (.022, .169), (.083, .174), (.091, .123),
        (.076, .081), (.015, .057), (-.011, .066),
        (.048, .125),
    ], .0027, paper, coll, root)
    front_sheet("05 Bibir kertas terlipat", [
        (-.090, .175), (-.061, .183), (.076, .187),
        (.084, .179), (.066, .160), (-.070, .163),
    ], .0030, light_paper, coll, root)

    # Fold lines are narrow, low-contrast creases rather than thick seams.
    for label, points in [
        ("Garis lipatan kiri", [(-.082, .157), (-.033, .113), (-.010, .064)]),
        ("Garis lipatan kanan", [(.080, .169), (.052, .118), (.018, .063)]),
    ]:
        curve = bpy.data.curves.new(label, "CURVE")
        curve.dimensions = "3D"
        curve.bevel_depth = .00035
        curve.bevel_resolution = 1
        spline = curve.splines.new("POLY")
        spline.points.add(len(points) - 1)
        for point, (x, z) in zip(spline.points, points):
            point.co = (x, front_y(z)-.0033, z, 1)
        obj = bpy.data.objects.new(label, curve)
        coll.objects.link(obj)
        obj.parent = root
        obj.data.materials.append(crease)

    # A narrow rolled top edge makes the opening visibly sealed.
    top = [(-.078, -.026, .188), (-.045, -.031, .192),
           (.045, -.031, .192), (.078, -.026, .188)]
    mesh_object("06 Tepi atas tertutup", top,
                [(0, 1, 2, 3)], shadow_paper, coll, root,
                paper_uv, solidify=.002)


def ribbon(coll, root, leaf):
    # Front strip has a V-notch at the bottom and subtle paper/leaf waviness.
    left = [-.069, -.070, -.071, -.069, -.068, -.068]
    right = [-.029, -.030, -.030, -.028, -.027, -.027]
    heights = [.186, .158, .122, .086, .050]
    vertices = []
    for i, z in enumerate(heights):
        y = front_y(z) - .0041 - .0005 * math.sin(i * 1.8)
        if i == 0:
            y -= .009  # Bring the leaf over the protruding top paper fold.
        vertices.extend([(left[i], y, z), (right[i], y - .0001, z)])
    faces = [(i*2+2, i*2+3, i*2+1, i*2)
             for i in range(len(heights)-1)]
    # Two tails terminate at z=.012; the center notch rises to z=.038.
    vertices.extend([(-.068, front_y(.012)-.0041, .012),
                     (-.048, front_y(.038)-.0041, .038),
                     (-.027, front_y(.012)-.0041, .012)])
    faces.extend([(11, 9, 8), (10, 11, 8), (11, 12, 9)])
    mesh_object("07 Pita daun depan - ujung V", vertices, faces,
                leaf, coll, root, leaf_uv, solidify=.0008)

    # The strip continues across the sealed top and behind the parcel.
    top_verts = [(-.069, -.043, .188), (-.029, -.043, .188),
                 (-.029, -.010, .193), (-.069, -.010, .193),
                 (-.029, .029, .189), (-.069, .029, .189)]
    mesh_object("08 Pita daun melintasi atas", top_verts,
                [(0, 1, 2, 3), (3, 2, 4, 5)], leaf, coll, root,
                lambda co: ((co.x+.069)/.04, (co.y+.034)/.063),
                solidify=.0007)
    back = [(-.069, .032, .185), (-.029, .032, .185),
            (-.028, .040, .110), (-.067, .040, .110),
            (-.067, .039, .044), (-.028, .039, .044)]
    mesh_object("09 Pita daun belakang", back,
                [(0, 1, 2, 3), (3, 2, 5, 4)], leaf,
                coll, root, lambda co: ((co.x+.069)/.04, co.z/.205),
                solidify=.0007)


def sticker(coll, root, sticker_mat, edge_mat):
    center_x, center_z = -.049, .152
    radius = .0175
    y = front_y(center_z) - .0068
    count = 48
    verts = [(center_x, y, center_z)]
    verts += [(center_x + radius * math.cos(2*math.pi*i/count),
               y, center_z + radius * math.sin(2*math.pi*i/count))
              for i in range(count)]
    faces = [(0, i+1, (i+1)%count+1) for i in range(count)]
    uv = lambda co: ((co.x-center_x)/(2*radius)+.5,
                     (co.z-center_z)/(2*radius)+.5)
    mesh_object("10 Stiker bulat Nasi Uduk", verts, faces, sticker_mat,
                coll, root, uv, solidify=.0005)
    # The thin raised perimeter reads as adhesive paper, not an extra badge.
    curve = bpy.data.curves.new("Tepi stiker", "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = .00032
    curve.bevel_resolution = 2
    spline = curve.splines.new("POLY")
    spline.points.add(count - 1)
    spline.use_cyclic_u = True
    for i, point in enumerate(spline.points):
        angle = 2*math.pi*i/count
        point.co = (center_x + radius*math.cos(angle), y-.0002,
                    center_z + radius*math.sin(angle), 1)
    obj = bpy.data.objects.new("11 Tepi stiker kertas", curve)
    coll.objects.link(obj)
    obj.parent = root
    obj.data.materials.append(edge_mat)


def broaden_packet(item):
    """Give the meal parcel a wider, shorter silhouette; keep its seal round."""
    seal_x, seal_z = -.049, .152
    for obj in item.objects:
        if obj.type not in {"MESH", "CURVE"}:
            continue
        is_sticker = "Stiker" in obj.name or "stiker" in obj.name
        if obj.type == "MESH":
            coords = [vert.co for vert in obj.data.vertices]
        else:
            coords = [point.co for spline in obj.data.splines
                      for point in spline.points]
        for co in coords:
            if is_sticker:
                co.x += seal_x * .18
                co.z += seal_z * -.08
            else:
                co.x *= 1.18
                co.z *= .92


def make_light(coll, name, location, target, power, size, color):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = power
    data.size = size
    data.color = color
    obj = bpy.data.objects.new(name, data)
    coll.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat("-Z", "Y").to_euler()


def camera(coll, name, location, target, ortho):
    data = bpy.data.cameras.new(name)
    data.type = "ORTHO"
    data.ortho_scale = ortho
    obj = bpy.data.objects.new(name, data)
    coll.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


def setup():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 36
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = -.55
    world = bpy.data.worlds.new("Latar studio")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.22,.21,.19,1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .45
    scene.world = world
    return scene


def main():
    scene = setup()
    item = collection("NASI BUNGKUS - model item")
    studio = collection("STUDIO - pratinjau saja")
    root = bpy.data.objects.new("NASI_UDUK_BUNGKUS_ROOT", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = .015
    item.objects.link(root)
    root["nama_item"] = "Nasi Uduk — 1 bungkus"
    root["satuan"] = "meter"
    root["isi_terlihat"] = False

    paper = material("Kertas kraft - tekstur serat", (.58,.39,.22),
                     .92, "Kertas_Kraft.png", bump=.00035)
    light_paper = material("Sisi terang lipatan kraft", (.63,.43,.25),
                           .90, "Kertas_Kraft.png", bump=.0002)
    shadow_paper = material("Sisi dalam lipatan kraft", (.49,.33,.19),
                            .91, "Kertas_Kraft.png", bump=.00025)
    crease = material("Kerut lipatan halus", (.29,.20,.12))
    leaf = material("Pita daun pisang hijau", (.08,.27,.12),
                    .53, "Pita_Daun_Pisang.png", bump=.00014)
    sticker_mat = material("Stiker krem Nasi Uduk", (.81,.70,.48),
                           .72, "Stiker_Nasi_Uduk.png")
    edge_mat = material("Pinggir kertas stiker", (.65,.47,.24))

    body_mesh(item, root, paper)
    folded_paper(item, root, paper, light_paper, shadow_paper, crease)
    ribbon(item, root, leaf)
    sticker(item, root, sticker_mat, edge_mat)
    broaden_packet(item)

    ground_mat = material("Lantai foto netral", (.34,.32,.29), .83)
    bpy.ops.mesh.primitive_plane_add(size=2, location=(0,0,-.002))
    ground = bpy.context.object
    ground.name = "Latar lantai - tidak diekspor"
    for parent in list(ground.users_collection):
        parent.objects.unlink(ground)
    studio.objects.link(ground)
    ground.data.materials.append(ground_mat)
    target = (0, 0, .096)
    make_light(studio, "Softbox kiri", (-.34,-.36,.47), target,
               13, .36, (1,.89,.78))
    make_light(studio, "Softbox kanan", (.31,-.17,.28), target,
               8, .32, (.86,.93,1))
    make_light(studio, "Cahaya tepi", (.08,.26,.32), target,
               10, .23, (1,.98,.88))
    front = camera(studio, "Kamera tampak depan", (0,-.55,.16), target, .275)
    angle = camera(studio, "Kamera sudut tiga perempat",
                   (.29,-.48,.26), target, .31)
    for cam, filename in [(front,"01_Nasi_Bungkus_Depan.png"),
                          (angle,"02_Nasi_Bungkus_Sudut.png")]:
        scene.camera = cam
        scene.render.filepath = str(PREVIEWS / filename)
        bpy.ops.render.render(write_still=True)
        print("Rendered", scene.render.filepath, flush=True)

    scene.camera = angle
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.context.preferences.filepaths.save_version = 0
    blend_path = ROOT / "Nasi_Uduk_Bungkus.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    # FBX stores meshes; convert only in-memory after the editable .blend is saved.
    for obj in list(item.objects):
        if obj.type != "CURVE":
            continue
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.convert(target="MESH")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in item.objects:
        obj.select_set(True)
    fbx_path = UNITY_INVENTORY / "Nasi_Uduk_Bungkus.fbx"
    bpy.ops.export_scene.fbx(filepath=str(fbx_path), use_selection=True,
                             object_types={"MESH", "EMPTY"},
                             apply_unit_scale=True, axis_forward="-Z",
                             axis_up="Y", path_mode="COPY",
                             embed_textures=True)
    print("Saved", blend_path, fbx_path, flush=True)


if __name__ == "__main__":
    main()

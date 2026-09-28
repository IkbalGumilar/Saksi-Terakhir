"""Build the editable yellow city scooter source asset and review renders.

Run from the project root with:
    blender --background --factory-startup --python SourceArt/build_motor_matic.py

Blender coordinates: +Y forward, +X rider's right, +Z up; one unit is one metre.
The Preview_Studio collection is only for review and should not be exported.
"""

import math
import os
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
PREVIEWS = ROOT / "Previews"
PREVIEWS.mkdir(exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for collection in list(bpy.data.collections):
    if collection.name == "Collection":
        bpy.data.collections.remove(collection)

scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0


def collection(name):
    result = bpy.data.collections.new(name)
    scene.collection.children.link(result)
    return result


model_collection = collection("Motor_Matic_Kuning")
preview_collection = collection("Preview_Studio__do_not_export")


def in_collection(obj, target=model_collection):
    for old in tuple(obj.users_collection):
        old.objects.unlink(obj)
    target.objects.link(obj)
    return obj


def material(name, color, metallic=0.0, roughness=0.45, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*color, 1)
        bsdf.inputs["Emission Strength"].default_value = emission
    return mat


yellow = material("Body | warm yellow", (0.97, 0.49, 0.006), 0.22, 0.31)
yellow_light = material("Body | upper highlights", (1.0, 0.66, 0.025), 0.2, 0.32)
yellow_dark = material("Body | shadow accents", (0.55, 0.31, 0.012), 0.1, 0.4)
black = material("Trim | satin black", (0.035, 0.043, 0.052), 0.05, 0.54)
rubber = material("Rubber | tires and grips", (0.016, 0.018, 0.019), 0.0, 0.78)
seat_mat = material("Seat | black vinyl", (0.012, 0.014, 0.016), 0.0, 0.74)
gunmetal = material("Hardware | gunmetal", (0.14, 0.17, 0.19), 0.62, 0.33)
steel = material("Hardware | brushed silver", (0.51, 0.57, 0.60), 0.68, 0.30)
brake_mat = material("Brake disc | muted steel", (0.40, 0.44, 0.45), 0.76, 0.32)
red = material("Lamp | red", (0.72, 0.018, 0.012), 0.04, 0.2, 0.20)
amber = material("Lamp | amber", (1.0, 0.33, 0.018), 0.05, 0.21, 0.17)
lens = material("Lamp | clear lens", (0.72, 0.87, 0.98), 0.22, 0.15, 0.17)
dark_glass = material("Instrument | dark glass", (0.022, 0.042, 0.061), 0.12, 0.14)
plate_mat = material("Plate | charcoal", (0.018, 0.023, 0.025), 0.04, 0.42)
white = material("Plate | lettering", (0.92, 0.94, 0.93), 0.0, 0.44)
ground_mat = material("Preview | warm grey floor", (0.47, 0.48, 0.49), 0.0, 0.88)


def parent_keep_world(obj, parent):
    # Operators can leave a just-set rotation unapplied until the view layer
    # updates. Capture the evaluated matrix so rods retain their real angle.
    bpy.context.view_layer.update()
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = world
    return obj


def empty(name, location=(0, 0, 0), parent=None):
    obj = bpy.data.objects.new(name, None)
    model_collection.objects.link(obj)
    obj.location = location
    obj.empty_display_type = "ARROWS"
    obj.empty_display_size = 0.13
    if parent:
        parent_keep_world(obj, parent)
    return obj


root = empty("Motor_Matic_Kuning | root | front +Y", (0, 0, 0))
steering = empty("Steering_Assembly | rotate around Z", (0, 0.61, 0.99), root)
front_wheel = empty("Front_Wheel | spin around local X", (0, 0.68, 0.305), steering)
rear_wheel = empty("Rear_Wheel | spin around local X", (0, -0.68, 0.305), root)


def add_material(obj, mat):
    obj.data.materials.append(mat)
    return obj


def add_bevel(obj, width, segments=3):
    if width <= 0:
        return
    modifier = obj.modifiers.new("Soft manufactured edges", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    modifier.angle_limit = 0.40
    weighted = obj.modifiers.new("Weighted panel normals", "WEIGHTED_NORMAL")
    weighted.keep_sharp = True


def cube(name, location, dimensions, mat, bevel=0.0, parent=None, target=model_collection):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    in_collection(obj, target)
    add_material(obj, mat)
    add_bevel(obj, bevel)
    if parent:
        parent_keep_world(obj, parent)
    return obj


def mesh_obj(name, vertices, faces, mat, bevel=0, parent=None):
    mesh = bpy.data.meshes.new(name + " mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    model_collection.objects.link(obj)
    add_material(obj, mat)
    add_bevel(obj, bevel)
    if parent:
        parent_keep_world(obj, parent)
    return obj


def extrude_side(name, points, x_min, x_max, mat, bevel=0.0, parent=root):
    """A solid silhouette drawn in the longitudinal Y / vertical Z plane."""
    area = sum(points[i][0] * points[(i + 1) % len(points)][1]
               - points[(i + 1) % len(points)][0] * points[i][1]
               for i in range(len(points)))
    if area < 0:
        points = list(reversed(points))
    n = len(points)
    verts = [(x_min, y, z) for y, z in points] + [(x_max, y, z) for y, z in points]
    faces = [tuple(range(n - 1, -1, -1)), tuple(range(n, 2 * n))]
    faces += [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)]
    return mesh_obj(name, verts, faces, mat, bevel, parent)


def extrude_front(name, points, y_back, y_front, mat, bevel=0.0, parent=root):
    """A solid silhouette drawn in the lateral X / vertical Z plane."""
    area = sum(points[i][0] * points[(i + 1) % len(points)][1]
               - points[(i + 1) % len(points)][0] * points[i][1]
               for i in range(len(points)))
    if area < 0:
        points = list(reversed(points))
    n = len(points)
    verts = [(x, y_back, z) for x, z in points] + [(x, y_front, z) for x, z in points]
    faces = [tuple(range(n)), tuple(range(2 * n - 1, n - 1, -1))]
    faces += [(i, i + n, (i + 1) % n + n, (i + 1) % n) for i in range(n)]
    return mesh_obj(name, verts, faces, mat, bevel, parent)


def loft_y(name, sections, mat, parent=root):
    """Sections are (Y, bottom Z, top Z, lower width, middle width, upper width)."""
    vertices = []
    for y, bottom, top, low, middle, high in sections:
        mid = (bottom + top) * 0.52
        vertices.extend([(0, y, top), (high, y, top - .018),
                         (middle, y, mid), (low, y, bottom + .018),
                         (0, y, bottom), (-low, y, bottom + .018),
                         (-middle, y, mid), (-high, y, top - .018)])
    count = 8
    faces = [tuple(range(count - 1, -1, -1)),
             tuple(range((len(sections) - 1) * count, len(sections) * count))]
    for section in range(len(sections) - 1):
        a, b = section * count, (section + 1) * count
        for i in range(count):
            j = (i + 1) % count
            faces.append((a + i, a + j, b + j, b + i))
    return mesh_obj(name, vertices, faces, mat, .025, parent)


def loft_z(name, sections, mat, parent=root):
    """Sections are (Z, rear Y, front Y, rear half width, front half width)."""
    vertices = []
    for z, rear, front, rear_w, front_w in sections:
        vertices.extend([(0, front, z), (front_w, front - .018, z),
                         (rear_w, rear + .018, z), (0, rear, z),
                         (-rear_w, rear + .018, z), (-front_w, front - .018, z)])
    count = 6
    faces = [tuple(range(count)),
             tuple(range(len(sections) * count - 1, (len(sections) - 1) * count - 1, -1))]
    for section in range(len(sections) - 1):
        a, b = section * count, (section + 1) * count
        for i in range(count):
            j = (i + 1) % count
            faces.append((a + j, a + i, b + i, b + j))
    return mesh_obj(name, vertices, faces, mat, .022, parent)


def cylinder(name, location, radius, depth, mat, axis="Z", vertices=32, parent=root):
    rotation = (0, math.pi / 2, 0) if axis == "X" else (0, 0, 0)
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius,
                                        depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    in_collection(obj)
    add_material(obj, mat)
    add_bevel(obj, min(.007, depth / 3), 2)
    for face in obj.data.polygons:
        face.use_smooth = True
    if parent:
        parent_keep_world(obj, parent)
    return obj


def rod(name, start, end, radius, mat, parent=root, vertices=12):
    start, end = Vector(start), Vector(end)
    mid = (start + end) / 2
    direction = end - start
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius,
                                        depth=direction.length, location=mid)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    in_collection(obj)
    add_material(obj, mat)
    add_bevel(obj, min(.004, radius / 3), 2)
    for face in obj.data.polygons:
        face.use_smooth = True
    if parent:
        parent_keep_world(obj, parent)
    return obj


def tube(name, points, radius, mat, parent=root, resolution=2):
    curve = bpy.data.curves.new(name + " path", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 16
    curve.bevel_depth = radius
    curve.bevel_resolution = resolution
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for point, coord in zip(spline.points, points):
        point.co = (*coord, 1)
    obj = bpy.data.objects.new(name, curve)
    model_collection.objects.link(obj)
    add_material(obj, mat)
    if parent:
        parent_keep_world(obj, parent)
    return obj


# Body: a continuous dark underframe and the separate yellow pressings.
loft_y("Rear shell | dark core", [
    (-1.00, .58, .68, .055, .09, .055),
    (-.84, .43, .75, .13, .18, .17),
    (-.55, .40, .76, .17, .21, .19),
    (-.26, .43, .74, .17, .22, .19),
    (.04, .51, .68, .12, .17, .13),
], black)
for side, label in ((-1, "Left"), (1, "Right")):
    x0, x1 = ((-.243, -.203) if side < 0 else (.203, .243))
    extrude_side("Yellow rear fairing | " + label,
                 [(-.96, .64), (-.80, .74), (-.55, .745), (-.24, .715),
                  (.015, .665), (-.12, .57), (-.42, .555), (-.68, .545),
                  (-.86, .56)], x0, x1, yellow, .015)
    x0, x1 = ((-.253, -.244) if side < 0 else (.244, .253))
    extrude_side("Sculpted black side inset | " + label,
                 [(-.70, .66), (-.34, .685), (-.05, .64),
                  (-.28, .603), (-.57, .607)], x0, x1, black, .007)
    extrude_side("Lower fairing highlight | " + label,
                 [(-.89, .605), (-.67, .586), (-.43, .575),
                  (-.63, .556), (-.83, .563)],
                 x0 + .001 * side, x1 + .001 * side, yellow_light, .004)

loft_z("Front shield | yellow body", [
    (.41, .49, .64, .12, .13),
    (.57, .50, .78, .17, .19),
    (.73, .51, .895, .20, .225),
    (.88, .53, .92, .22, .225),
    (1.01, .55, .81, .17, .17),
    (1.07, .59, .76, .12, .13),
], yellow)
extrude_front("Inner leg shield | black", [(-.18, .53), (-.20, .82),
              (-.14, 1.005), (.14, 1.005), (.20, .82), (.18, .53)],
              .48, .515, black, .012)
extrude_front("Front centre visor | black", [(-.17, 1.045),
              (-.09, 1.09), (.09, 1.09), (.17, 1.045),
              (.12, .99), (-.12, .99)], .792, .817, black, .012)

cube("Flat step-through floorboard", (0, .17, .326), (.39, .78, .082), black, .045)
cube("Low structural tunnel", (0, -.12, .367), (.21, .34, .13), black, .04)
for x in (-.145, -.09, -.035, .035, .09, .145):
    cube("Rubber grip rib", (x, .15, .373), (.015, .53, .007), rubber, .004)
cube("Seat lower support", (0, -.39, .694), (.365, .97, .07), black, .025)
loft_y("One-piece padded seat", [
    (-.87, .71, .775, .13, .18, .16),
    (-.72, .71, .815, .16, .195, .17),
    (-.44, .705, .808, .16, .195, .17),
    (-.13, .70, .778, .13, .17, .145),
    (.12, .705, .745, .075, .105, .08),
], seat_mat)
for x in (-.13, .13):
    tube("Seat piping", [(x, -.82, .752), (x, -.55, .775),
         (x, -.22, .763), (x, .06, .735)], .0035, gunmetal)

# Rear rack / passenger grab rail, lamps, mudguard and a fictional plate.
tube("Passenger grab rail", [(-.19, -.73, .767), (-.24, -.86, .77),
     (-.20, -.995, .744), (0, -1.026, .733), (.20, -.995, .744),
     (.24, -.86, .77), (.19, -.73, .767)], .019, black)
extrude_front("Tail-lamp housing", [(-.19, .695), (-.14, .748),
              (.14, .748), (.19, .695), (.13, .657), (-.13, .657)],
              -1.012, -.983, black, .012)
extrude_front("Red rear lamp", [(-.115, .690), (-.09, .730),
              (.09, .730), (.115, .690), (.07, .67), (-.07, .67)],
              -1.022, -1.015, red, .006)
for side in (-1, 1):
    x0, x1 = sorted((side * .12, side * .19))
    extrude_front("Rear indicator " + str(side),
                  [(x0, .68), (x0, .719), (x1, .703), (x1, .67)],
                  -1.025, -1.017, amber, .003)
extrude_side("Rear mudguard", [(-1.013, .655), (-.965, .608),
             (-1.025, .35), (-1.10, .27), (-1.097, .35),
             (-1.06, .62)], -.10, .10, black, .012)
extrude_front("Plate mounting bracket", [(-.135, .43), (-.135, .545),
              (.135, .545), (.135, .43)], -1.103, -1.081, gunmetal, .006)
extrude_front("Fictional rear plate", [(-.128, .447), (-.128, .529),
              (.128, .529), (.128, .447)], -1.116, -1.105, plate_mat, .005)
bpy.ops.object.text_add(location=(-.102, -1.119, .473), rotation=(math.pi / 2, 0, 0))
plate_text = bpy.context.object
plate_text.name = "Fictional plate lettering | ST 001"
plate_text.data.body = "ST 001"
plate_text.data.size = .047
plate_text.data.extrude = .0005
in_collection(plate_text)
add_material(plate_text, white)
parent_keep_world(plate_text, root)

# Front steering fairing, transparent pieces and lamps.
cube("Handlebar cowl | upper", (0, .64, 1.07), (.39, .18, .135), black, .055, steering)
extrude_front("Handlebar cowl | yellow cap", [(-.18, 1.085),
              (-.15, 1.145), (.15, 1.145), (.18, 1.085),
              (.10, 1.055), (-.10, 1.055)], .718, .755,
              yellow, .014, steering)
cube("Dashboard recess", (0, .565, 1.095), (.17, .015, .074), dark_glass, .014, steering)
for x in (-.27, .27):
    rod("Handlebar steel tube", (0, .62, 1.092), (x, .62, 1.092), .012, gunmetal, steering)
    rod("Rubber handle grip", (x * .75, .62, 1.092), (x * 1.22, .62, 1.092),
        .019, rubber, steering, 16)
    rod("Brake lever", (x * .74, .565, 1.078), (x * 1.21, .57, 1.057),
        .005, steel, steering)
    tube("Mirror stalk", [(x * .84, .64, 1.115),
         (x * 1.20, .66, 1.235), (x * 1.31, .69, 1.267)],
         .006, gunmetal, steering)
    mirror = cube("Mirror housing", (x * 1.39, .71, 1.273),
                  (.148, .052, .076), black, .022, steering)
    mirror.rotation_euler[2] = -x * .10
    cube("Mirror glass", (x * 1.39, .680, 1.273),
         (.121, .005, .053), steel, .015, steering)

extrude_front("Main headlight dark bezel", [(-.178, .865),
              (-.13, .796), (0, .772), (.13, .796), (.178, .865),
              (.10, .898), (0, .868), (-.10, .898)],
              .913, .943, black, .012, steering)
for side in (-1, 1):
    profile = [(side * .121, .897), (side * .207, .922),
               (side * .189, .953), (side * .137, .940)]
    extrude_front("Front turn indicator " + str(side), profile,
                  .889, .901, amber, .004, steering)
extrude_front("One-piece V-shaped headlight lens",
              [(-.154, .848), (-.108, .795), (0, .773),
               (.108, .795), (.154, .848), (.11, .860),
               (0, .816), (-.11, .860)],
              .944, .953, lens, .004, steering)
extrude_front("Centre nose ridge", [(-.045, .906), (0, .947),
              (.045, .906), (0, .869)], .914, .934, yellow_light, .007, steering)
extrude_front("Lower front intake", [(-.11, .725), (-.08, .704),
              (.08, .704), (.11, .725), (.06, .746), (-.06, .746)],
              .898, .907, black, .004, steering)

# Front fork: two telescopic legs and a sculpted yellow wheel arch.
for x in (-.115, .115):
    rod("Front suspension upper tube", (x, .625, .76), (x, .677, .385),
        .017, steel, steering)
    rod("Front suspension lower slider", (x, .665, .455), (x, .681, .307),
        .024, gunmetal, steering)
rod("Front axle", (-.155, .68, .305), (.155, .68, .305),
    .017, steel, steering)


def curved_fender(name, wheel_y, wheel_z, radius, half_width, mat, parent):
    vertices = []
    segments = 19
    for i in range(segments):
        angle = math.radians(-70 + 140 * i / (segments - 1))
        for r in (radius, radius - .018):
            for x in (-half_width, half_width):
                vertices.append((x, wheel_y + r * math.sin(angle),
                                 wheel_z + r * math.cos(angle)))
    faces = []
    for i in range(segments - 1):
        a, b = 4 * i, 4 * (i + 1)
        faces.extend([(a, b, b + 1, a + 1),
                      (a + 2, a + 3, b + 3, b + 2),
                      (a, a + 2, b + 2, b),
                      (a + 1, b + 1, b + 3, a + 3)])
    faces.extend([(0, 1, 3, 2),
                  tuple(range(4 * (segments - 1), 4 * segments))])
    return mesh_obj(name, vertices, faces, mat, .009, parent)


curved_fender("Yellow front mudguard", .68, .305, .359, .133, yellow, steering)


def wheel(name, y, pivot, front=False):
    center_z = .305
    for side in (-1, 1):
        # The two sidewall stripes hint at a real tire profile without a dense tread mesh.
        bpy.ops.mesh.primitive_torus_add(major_segments=64, minor_segments=12,
                                        major_radius=.247, minor_radius=.050,
                                        location=(0, y, center_z),
                                        rotation=(0, math.pi / 2, 0))
        tire = bpy.context.object
        tire.name = name + " | black tire"
        in_collection(tire)
        add_material(tire, rubber)
        parent_keep_world(tire, pivot)
        break
    for x in (-.048, .048):
        bpy.ops.mesh.primitive_torus_add(major_segments=64, minor_segments=8,
                                        major_radius=.205, minor_radius=.021,
                                        location=(x, y, center_z),
                                        rotation=(0, math.pi / 2, 0))
        rim = bpy.context.object
        rim.name = name + " | dark alloy rim"
        in_collection(rim)
        add_material(rim, gunmetal)
        parent_keep_world(rim, pivot)
    cylinder(name + " | hub", (0, y, center_z), .064, .12,
             steel if front else gunmetal, axis="X", parent=pivot)
    for index in range(5):
        angle = 2 * math.pi * index / 5 + .18
        radial = Vector((math.sin(angle), math.cos(angle)))
        across = Vector((math.cos(angle), -math.sin(angle)))
        def at(radius, width):
            return (y + radial.x * radius + across.x * width,
                    center_z + radial.y * radius + across.y * width)
        points = [at(.057, -.025), at(.198, -.028),
                  at(.198, .028), at(.057, .025)]
        extrude_side(name + " | alloy spoke %02d" % (index + 1),
                     points, -.053, .053, gunmetal, .007, pivot)
    if front:
        bpy.ops.mesh.primitive_torus_add(major_segments=64, minor_segments=8,
                                        major_radius=.132, minor_radius=.018,
                                        location=(.064, y, center_z),
                                        rotation=(0, math.pi / 2, 0))
        brake_ring = bpy.context.object
        brake_ring.name = "Front brake disc | open centre ring"
        in_collection(brake_ring)
        add_material(brake_ring, brake_mat)
        parent_keep_world(brake_ring, pivot)
        for index in range(8):
            angle = 2 * math.pi * index / 8
            cylinder("Brake disc perforation mark", (.070,
                     y + .132 * math.sin(angle), center_z + .132 * math.cos(angle)),
                     .005, .002, gunmetal, axis="X", vertices=12, parent=pivot)
        cube("Front brake caliper", (.081, y + .107, .405),
             (.046, .071, .051), gunmetal, .011, steering)
    else:
        cylinder("Rear brake hub", (.065, y, center_z), .090, .012,
                 gunmetal, axis="X", parent=pivot)


wheel("Front wheel", .68, front_wheel, True)
wheel("Rear wheel", -.68, rear_wheel)

# Visible machinery: left transmission/fan cover, right exhaust and rear shock.
extrude_side("CVT transmission cover | left", [(-.84, .38),
             (-.70, .455), (-.35, .48), (-.24, .425),
             (-.43, .315), (-.74, .30)], -.21, -.13, gunmetal, .026)
cylinder("Cooling fan grille | left", (-.215, -.32, .43),
         .105, .02, black, axis="X")
cylinder("Cooling fan hub | left", (-.229, -.32, .43),
         .035, .012, steel, axis="X")
for index in range(9):
    angle = 2 * math.pi * index / 9
    rod("Fan grille slash", (-.233, -.32 + .04 * math.sin(angle),
        .43 + .04 * math.cos(angle)),
        (-.233, -.32 + .087 * math.sin(angle + .22),
         .43 + .087 * math.cos(angle + .22)), .0045, gunmetal)
rod("Rear swing arm | left", (-.14, -.32, .36),
    (-.14, -.68, .305), .037, gunmetal)
rod("Rear swing arm | right", (.13, -.34, .36),
    (.13, -.68, .305), .032, gunmetal)

rod("Rear shock | upper tube", (.20, -.47, .62),
    (.20, -.65, .36), .018, steel)
rod("Rear shock | black casing", (.20, -.53, .55),
    (.20, -.64, .38), .028, black)
shock_a = Vector((.20, -.47, .62))
shock_b = Vector((.20, -.65, .36))
shock_vec = shock_b - shock_a
shock_axis = shock_vec.normalized()
shock_side = Vector((1, 0, 0))
shock_other = shock_axis.cross(shock_side).normalized()
coil_points = []
for index in range(65):
    t = index / 64
    angle = 2 * math.pi * 7 * t
    point = shock_a + shock_vec * t + .037 * (
        math.cos(angle) * shock_side + math.sin(angle) * shock_other)
    coil_points.append(tuple(point))
tube("Rear shock | visible coil", coil_points, .006, steel)

tube("Exhaust header pipe | right", [(.16, -.26, .37),
     (.24, -.34, .31), (.25, -.49, .30)], .018, gunmetal)
exhaust = extrude_side("Exhaust muffler | right",
                       [(-.88, .37), (-.78, .40), (-.40, .39),
                        (-.29, .35), (-.37, .29), (-.80, .30)],
                       .235, .327, black, .032)
extrude_side("Exhaust heat shield | brushed steel",
             [(-.78, .391), (-.45, .387), (-.36, .36),
              (-.47, .337), (-.76, .345)], .328, .337, steel, .009)
cylinder("Exhaust outlet", (.28, -.875, .35), .033, .025,
         rubber, axis="X")

# Passenger feet, a side stand, and the foldable centre stand remain static.
for x in (-.19, .19):
    cube("Passenger foot peg", (x, -.34, .42),
         (.115, .070, .033), gunmetal, .013)
rod("Side stand", (-.145, -.15, .28), (-.29, -.25, .025), .014, gunmetal)
for x in (-.14, .14):
    rod("Centre stand leg", (x, -.46, .285), (x, -.52, .038),
        .014, gunmetal)
rod("Centre stand foot", (-.16, -.52, .035), (.16, -.52, .035),
    .013, gunmetal)

# A subtle central hook and steering lock circle complete the rider-facing area.
cube("Inner apron | small glovebox", (-.095, .478, .70),
     (.13, .011, .092), gunmetal, .012)
cylinder("Ignition key/lock surround", (.145, .483, .76),
         .024, .011, gunmetal, axis="X")
tube("Bag hook", [(0, .476, .77), (0, .45, .76),
     (0, .45, .733)], .009, gunmetal)

# Review-only studio. Its collection name makes it easy to exclude on export.
floor = cube("Review floor", (0, 0, -.045), (200, 200, .08),
             ground_mat, 0, target=preview_collection)


def area_light(name, location, energy, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    preview_collection.objects.link(obj)
    obj.location = location
    direction = Vector((0, 0, .57)) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


area_light("Large softbox front-right", (2.6, 2.0, 3.5), 500, 4.0)
area_light("Softbox back-left", (-2.0, -1.8, 3.1), 340, 3.0)
area_light("Overhead edge light", (0.0, -.3, 3.5), 250, 2.0)

world = bpy.data.worlds.new("Neutral studio")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (.55, .58, .61, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = .75
scene.world = world


def camera(name, location, target=(0, 0, .59), ortho=2.55):
    data = bpy.data.cameras.new(name)
    data.type = "ORTHO"
    data.ortho_scale = ortho
    obj = bpy.data.objects.new(name, data)
    preview_collection.objects.link(obj)
    obj.location = location
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    return obj


cameras = [
    (camera("Camera | front-right", (2.9, 3.0, 1.90)), "motor_matic_front_right.png"),
    (camera("Camera | right profile", (3.7, .03, 1.36), ortho=2.43),
     "motor_matic_right_profile.png"),
    (camera("Camera | rear-left", (-2.7, -3.0, 1.85)),
     "motor_matic_rear_left.png"),
]
scene.camera = cameras[0][0]
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
fast_preview = os.environ.get("MOTOR_FAST_PREVIEW") == "1"
scene.cycles.samples = 8 if fast_preview else 24
scene.cycles.use_denoising = True
scene.render.resolution_x = 1280
scene.render.resolution_y = 840
scene.render.resolution_percentage = 55 if fast_preview else 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.view_settings.view_transform = "Standard"
scene.view_settings.exposure = -0.25
scene.render.filepath = str(PREVIEWS / cameras[0][1])

# Present the model in Material Preview if a reviewer opens the .blend directly.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == "VIEW_3D":
            area.spaces.active.shading.type = "MATERIAL"
            area.spaces.active.region_3d.view_distance = 3.2
            area.spaces.active.region_3d.view_location = (0, 0, .55)

bpy.ops.object.select_all(action="DESELECT")
root.select_set(True)
bpy.context.view_layer.objects.active = root

blend_path = ROOT / "Motor_Matic_Kuning.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
print("Saved editable model:", blend_path)
print("Model objects:", len(model_collection.objects))

for cam, filename in cameras:
    scene.camera = cam
    scene.render.filepath = str(PREVIEWS / filename)
    bpy.ops.render.render(write_still=True)
    print("Rendered preview:", scene.render.filepath)

scene.camera = cameras[0][0]
scene.render.filepath = str(PREVIEWS / cameras[0][1])
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
if bpy.app.background:
    # This headless host can leave PulseAudio's main loop alive after a
    # completed render. All files are already closed and flushed by Blender.
    sys.stdout.flush()
    sys.stderr.flush()
    os._exit(0)

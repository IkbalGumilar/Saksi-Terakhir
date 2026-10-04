"""Export the supplied flashlight and render a non-destructive preview.

Run with:
    blender -b SourceArt/Flashlight/Flashlight.blend --python SourceArt/Flashlight/export_flashlight.py

The original .blend is not saved or modified by this script.
"""

from pathlib import Path

import bpy
from mathutils import Vector


asset_dir = Path(__file__).resolve().parent
unity_inventory = asset_dir.parents[1] / "Assets/_Project/Art/Models/Props/Inventory"
fbx_path = unity_inventory / "Flashlight.fbx"
preview_path = Path("/tmp/saksi-terakhir-generated/previews/Flashlight/Flashlight_Three_Quarter.png")
preview_path.parent.mkdir(parents=True, exist_ok=True)

meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
assert meshes, "No flashlight meshes found in source .blend"

bpy.ops.object.select_all(action="DESELECT")
for obj in meshes:
    obj.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]

bpy.ops.export_scene.fbx(
    filepath=str(fbx_path),
    use_selection=True,
    object_types={"MESH"},
    axis_forward="-Z",
    axis_up="Y",
    global_scale=1.0,
    apply_unit_scale=True,
    use_space_transform=True,
    bake_space_transform=False,
    use_mesh_modifiers=True,
    add_leaf_bones=False,
    path_mode="COPY",
    embed_textures=True,
)
print(f"EXPORTED_FBX {fbx_path} MESHES {len(meshes)}")


def point_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def area_light(name, location, power, size, target=(0, 0, 0.045)):
    data = bpy.data.lights.new(name=name, type="AREA")
    data.energy = power
    data.shape = "DISK"
    data.size = size
    light = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(light)
    light.location = location
    point_at(light, target)
    return light


scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 48
scene.render.resolution_x = 900
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.render.filepath = str(preview_path)

world = bpy.data.worlds.new("Flashlight Preview World")
world.use_nodes = True
background = world.node_tree.nodes.get("Background")
background.inputs["Color"].default_value = (0.22, 0.27, 0.32, 1.0)
background.inputs["Strength"].default_value = 0.65
scene.world = world

camera_data = bpy.data.cameras.new("Preview Camera")
camera = bpy.data.objects.new("Preview Camera", camera_data)
scene.collection.objects.link(camera)
camera.location = (0.29, -0.38, 0.20)
point_at(camera, (0, 0, 0.047))
camera_data.type = "ORTHO"
camera_data.ortho_scale = 0.27
scene.camera = camera

area_light("Key Softbox", (0.18, -0.20, 0.25), 5.0, 0.28)
area_light("Fill Softbox", (-0.20, -0.12, 0.18), 2.3, 0.23)
area_light("Edge Softbox", (0.02, 0.20, 0.28), 4.0, 0.20)

scene.view_settings.view_transform = "AgX"
scene.view_settings.look = "AgX - Medium High Contrast"
bpy.ops.render.render(write_still=True)
print(f"RENDERED_PREVIEW {preview_path}")

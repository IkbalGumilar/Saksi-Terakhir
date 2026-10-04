"""Build a compact, closed P3K vinyl case with editable Blender source.

Run: blender -b --python SourceArt/P3K/build_p3k.py
The lid print is in its UV texture, so the cross and lettering stay flush.
"""

from pathlib import Path
import math
import subprocess

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
UNITY_INVENTORY = ROOT.parents[1] / "Assets/_Project/Art/Models/Props/Inventory"
PREVIEWS = Path("/tmp/saksi-terakhir-generated/previews/P3K")
PREVIEWS.mkdir(parents=True, exist_ok=True)
subprocess.run(["python3", str(ROOT / "make_p3k_textures.py")], check=True)


def coll(name):
    result = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(result)
    return result


def material(name, color, roughness=.8, image=None, bump=0, metallic=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    shader = nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    if image:
        texture = nodes.new("ShaderNodeTexImage")
        texture.image = bpy.data.images.load(str(ROOT / "Textures" / image),
                                             check_existing=True)
        texture.image.pack()
        mat.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
        if bump:
            bump_node = nodes.new("ShaderNodeBump")
            bump_node.inputs["Strength"].default_value = .12
            bump_node.inputs["Distance"].default_value = bump
            mat.node_tree.links.new(texture.outputs["Color"], bump_node.inputs["Height"])
            mat.node_tree.links.new(bump_node.outputs["Normal"], shader.inputs["Normal"])
    return mat


def create_mesh(name, vertices, faces, mat, parent_coll, root,
                uv_from_vertex=None, smooth=False):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    mesh.materials.append(mat)
    if uv_from_vertex:
        uv = mesh.uv_layers.new(name="UV")
        for polygon in mesh.polygons:
            for loop_index in polygon.loop_indices:
                vi = mesh.loops[loop_index].vertex_index
                uv.data[loop_index].uv = uv_from_vertex(vi, mesh.vertices[vi].co)
    for face in mesh.polygons:
        face.use_smooth = smooth
    obj = bpy.data.objects.new(name, mesh)
    parent_coll.objects.link(obj)
    obj.parent = root
    return obj


def outline_ring(z, half_x, half_y, corner):
    return [(-half_x+corner,-half_y,z),
            (half_x-corner,-half_y,z),
            (half_x,-half_y+corner,z),
            (half_x,half_y-corner,z),
            (half_x-corner,half_y,z),
            (-half_x+corner,half_y,z),
            (-half_x,half_y-corner,z),
            (-half_x,-half_y+corner,z)]


def shell(name, rings, mat, parent_coll, root):
    vertices = []
    for z, hx, hy, corner in rings:
        vertices.extend(outline_ring(z,hx,hy,corner))
    faces = []
    for i in range(len(rings)-1):
        for j in range(8):
            a = i*8+j
            b = i*8+(j+1)%8
            faces.append((a,b,b+8,a+8))
    faces.append(tuple(reversed(range(8))))
    faces.append(tuple((len(rings)-1)*8+j for j in range(8)))
    return create_mesh(name,vertices,faces,mat,parent_coll,root,
                       lambda _,co: ((co.x+.14)/.28,(co.z+.005)/.065),
                       smooth=True)


def lid_print(parent_coll,root,mat):
    # 13×9 vertex grid gives the vinyl a subtle loose fabric contour.
    nx, ny = 12, 8
    verts=[]
    for j in range(ny+1):
        v=j/ny
        y=-.080+v*.160
        for i in range(nx+1):
            u=i/nx
            x=-.125+u*.250
            edge=max(abs(x)/.125, abs(y)/.080)
            ruffle=.00048*math.sin(7.2*x/.125+4.1*y/.080)
            ruffle+=.00024*math.sin(17*x/.125-9*y/.080)
            z=.0552+ruffle*(.5+.5*edge)
            verts.append((x,y,z))
    faces=[]
    stride=nx+1
    for j in range(ny):
        for i in range(nx):
            a=j*stride+i
            faces.append((a,a+1,a+1+stride,a+stride))
    def uv(index,_):
        i=index%stride
        j=index//stride
        return i/nx,j/ny
    obj=create_mesh("03 Cetak tutup P.P.P.K. menyatu",verts,faces,mat,
                    parent_coll,root,uv,smooth=True)
    mod=obj.modifiers.new("Ketebalan lembar vinil", "SOLIDIFY")
    mod.thickness=.0008
    mod.offset=-1
    return obj


def rounded_rectangle(hx,hy,radius,z):
    points=[]
    for cx,cy,start in [(hx-radius,hy-radius,0),
                        (-hx+radius,hy-radius,90),
                        (-hx+radius,-hy+radius,180),
                        (hx-radius,-hy+radius,270)]:
        for step in range(7):
            angle=math.radians(start+step*90/6)
            points.append((cx+radius*math.cos(angle),
                           cy+radius*math.sin(angle),z))
    return points


def piping(name,points,radius,mat,parent_coll,root,closed=True):
    curve=bpy.data.curves.new(name,"CURVE")
    curve.dimensions="3D"
    curve.bevel_depth=radius
    curve.bevel_resolution=2
    spline=curve.splines.new("POLY")
    spline.points.add(len(points)-1)
    spline.use_cyclic_u=closed
    for control,pos in zip(spline.points,points):
        control.co=(*pos,1)
    obj=bpy.data.objects.new(name,curve)
    parent_coll.objects.link(obj)
    obj.parent=root
    obj.data.materials.append(mat)
    return obj


def snap(x,parent_coll,root,metal,dark):
    bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=.0043,depth=.0017,
                                        end_fill_type="NGON",
                                        location=(x,-.086,.022),
                                        rotation=(math.pi/2,0,0))
    obj=bpy.context.object
    obj.name=f"05 Kancing logam {'kiri' if x<0 else 'kanan'}"
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    parent_coll.objects.link(obj)
    obj.parent=root
    obj.data.materials.append(metal)
    bevel=obj.modifiers.new("Tepi kancing","BEVEL")
    bevel.width=.0005
    bevel.segments=2
    obj.modifiers.new("Normal halus","WEIGHTED_NORMAL")
    bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=.0015,depth=.0003,
                                        end_fill_type="NGON",
                                        location=(x,-.0872,.022),
                                        rotation=(math.pi/2,0,0))
    indent=bpy.context.object
    indent.name=f"06 Inti kancing {'kiri' if x<0 else 'kanan'}"
    for owner in list(indent.users_collection):
        owner.objects.unlink(indent)
    parent_coll.objects.link(indent)
    indent.parent=root
    indent.data.materials.append(dark)


def side_fold(parent_coll,root,mat):
    # Loose triangular tucks at the left-hand corners, visible in three-quarter view.
    for name,vertices in [
        ("07 Lipatan samping kiri depan",[
            (-.129,-.080,.041),(-.129,-.066,.036),
            (-.132,-.084,.020),(-.130,-.080,.006)]),
        ("08 Lipatan samping kiri belakang",[
            (-.128,.078,.040),(-.130,.084,.025),
            (-.131,.080,.009),(-.129,.061,.030)]),
    ]:
        create_mesh(name,vertices,[(0,1,2),(1,3,2)],mat,
                    parent_coll,root,smooth=False)


def light(parent_coll,name,location,target,power,size,color):
    data=bpy.data.lights.new(name,"AREA")
    data.energy=power
    data.size=size
    data.color=color
    obj=bpy.data.objects.new(name,data)
    parent_coll.objects.link(obj)
    obj.location=location
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat("-Z","Y").to_euler()


def camera(parent_coll,name,location,target,scale):
    data=bpy.data.cameras.new(name)
    data.type="ORTHO"
    data.ortho_scale=scale
    obj=bpy.data.objects.new(name,data)
    parent_coll.objects.link(obj)
    obj.location=location
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat("-Z","Y").to_euler()
    return obj


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    scene.unit_settings.system="METRIC"
    scene.unit_settings.scale_length=1
    scene.render.engine="CYCLES"
    scene.cycles.device="CPU"
    scene.cycles.samples=28
    scene.cycles.use_denoising=True
    scene.render.resolution_x=1000
    scene.render.resolution_y=1000
    scene.render.image_settings.file_format="PNG"
    scene.view_settings.view_transform="Standard"
    scene.view_settings.exposure=-.60
    world=bpy.data.worlds.new("Latar studio")
    world.use_nodes=True
    world.node_tree.nodes["Background"].inputs["Color"].default_value=(.22,.23,.24,1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value=.30
    scene.world=world

    item=coll("KOTAK P3K - model item")
    studio=coll("STUDIO - tidak diekspor")
    root=bpy.data.objects.new("KOTAK_P3K_ROOT",None)
    root.empty_display_type="PLAIN_AXES"
    root.empty_display_size=.012
    item.objects.link(root)
    root["nama_item"]="Kotak P3K"
    root["satuan"]="meter"
    root["kondisi"]="tertutup"

    vinyl=material("Vinil putih agak kusut",(.82,.82,.82),.71,
                   "P3K_Vinyl_1024.png",bump=.00013)
    lid=material("Cetak merah P.P.P.K. pada vinil",(.91,.90,.89),.68,
                 "P3K_Lid_1024.png",bump=.00009)
    piping_mat=material("Jahitan tepi putih",(.74,.75,.75),.82)
    seam=material("Celah sambungan tutup",(.42,.43,.44),.90)
    metal=material("Logam kancing nikel",(.62,.64,.65),.28,metallic=.75)
    dark=material("Cekungan kancing",(.25,.26,.27),.70,metallic=.25)

    shell("01 Bawah kotak vinil",[
        (.001,.119,.073,.010),(.007,.128,.083,.009),
        (.033,.129,.084,.008),(.041,.126,.081,.010),
    ],vinyl,item,root)
    shell("02 Tutup kotak berlapis",[
        (.039,.130,.085,.009),(.047,.130,.085,.009),
        (.054,.126,.081,.010),
    ],vinyl,item,root)
    lid_print(item,root,lid)
    piping("04 Celah keliling tutup",
           rounded_rectangle(.130,.085,.009,.0405),.00075,seam,item,root)
    piping("04 Lis jahitan tutup",
           rounded_rectangle(.126,.081,.010,.0540),.00085,piping_mat,item,root)
    snap(-.103,item,root,metal,dark)
    snap(.103,item,root,metal,dark)
    side_fold(item,root,vinyl)

    ground_mat=material("Lantai studio",(.33,.35,.37),.86)
    bpy.ops.mesh.primitive_plane_add(size=2,location=(0,0,-.002))
    ground=bpy.context.object
    ground.name="Lantai pratinjau"
    for owner in list(ground.users_collection):
        owner.objects.unlink(ground)
    studio.objects.link(ground)
    ground.data.materials.append(ground_mat)
    target=(0,0,.028)
    light(studio,"Softbox utama",(-.34,-.32,.40),target,2.4,.48,(1,.96,.94))
    light(studio,"Softbox kanan",(.35,-.04,.32),target,1.4,.42,(.91,.94,1))
    light(studio,"Cahaya belakang",(.02,.30,.38),target,1.2,.30,(1,1,1))
    cams=[
        (camera(studio,"Kamera tiga perempat",(.35,-.37,.27),target,.42),
         "01_P3K_Sudut.png"),
        (camera(studio,"Kamera atas",(.005,-.04,.42),target,.36),
         "02_P3K_Atas.png"),
    ]
    for cam,name in cams:
        scene.camera=cam
        scene.render.filepath=str(PREVIEWS/name)
        bpy.ops.render.render(write_still=True)
        print("Rendered",scene.render.filepath,flush=True)
    scene.camera=cams[0][0]
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.context.preferences.filepaths.save_version=0
    blend=ROOT/"Kotak_P3K.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))

    # Keep editable piping in the .blend, bake it to mesh in the FBX export.
    for obj in list(item.objects):
        if obj.type != "CURVE":
            continue
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active=obj
        bpy.ops.object.convert(target="MESH")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in item.objects:
        obj.select_set(True)
    fbx=UNITY_INVENTORY/"Kotak_P3K.fbx"
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,
                             object_types={"MESH","EMPTY"},
                             apply_unit_scale=True,axis_forward="-Z",
                             axis_up="Y",path_mode="COPY",
                             embed_textures=True)
    print("Saved",blend,fbx,flush=True)


if __name__=="__main__":
    main()

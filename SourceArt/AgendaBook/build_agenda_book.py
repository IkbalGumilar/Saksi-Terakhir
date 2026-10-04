"""Editable A5 leather agenda: 80 physical leaves (160 ruled writing faces).

Run: blender -b --python SourceArt/AgendaBook/build_agenda_book.py
Each .blend and FBX keeps all leaves as separate objects for procedural motion.
Closed and open poses are supplied.
"""

from pathlib import Path
import math
import subprocess
import sys

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
UNITY_INVENTORY = ROOT.parents[1] / "Assets/_Project/Art/Models/Props/Inventory"
PREVIEWS = Path("/tmp/saksi-terakhir-generated/previews/AgendaBook")
PREVIEWS.mkdir(parents=True, exist_ok=True)
TEXTURES = ROOT / "Textures"
COUNT = 80
PW, PH = .148, .210
SHEET = .000075
PITCH = .000095
PAPER_BASE = .0025
SPINE_X = -.071
COVER_W, COVER_H, COVER_T = .156, .218, .0018
TOP_Z = PAPER_BASE + COUNT * PITCH + .0005
subprocess.run(["python3", str(ROOT / "make_book_textures.py")], check=True)


def collection(name):
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    return c


def mat(name, color, rough=.7, metal=0, texture=None):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*color, 1)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    if texture:
        n = m.node_tree.nodes.new("ShaderNodeTexImage")
        n.image = bpy.data.images.load(str(TEXTURES / texture), check_existing=True)
        n.image.pack()
        m.node_tree.links.new(n.outputs["Color"], p.inputs["Base Color"])
    return m


def leather_material():
    m = mat("Kulit biru - butiran halus", (.06, .25, .46), .75,
            texture="Kulit_Biru_BaseColor.png")
    p = m.node_tree.nodes["Principled BSDF"]
    for filename, socket in [("Kulit_Biru_Roughness.png", "Roughness"),
                              ("Kulit_Biru_Height.png", "Normal")]:
        n = m.node_tree.nodes.new("ShaderNodeTexImage")
        n.image = bpy.data.images.load(str(TEXTURES / filename), check_existing=True)
        n.image.colorspace_settings.name = "Non-Color"
        n.image.pack()
        if socket == "Normal":
            b = m.node_tree.nodes.new("ShaderNodeBump")
            b.inputs["Strength"].default_value = .20
            b.inputs["Distance"].default_value = .000045
            m.node_tree.links.new(n.outputs["Color"], b.inputs["Height"])
            m.node_tree.links.new(b.outputs["Normal"], p.inputs["Normal"])
        else:
            m.node_tree.links.new(n.outputs["Color"], p.inputs[socket])
    return m


def mesh(name, verts, faces, materials, c, parent, uvs=None, origin=(0,0,0)):
    data = bpy.data.meshes.new(name)
    data.from_pydata([tuple(Vector(v)-Vector(origin)) for v in verts], [], faces)
    data.update()
    for m in materials:
        data.materials.append(m)
    layer = data.uv_layers.new(name="UV")
    for poly in data.polygons:
        for li in poly.loop_indices:
            vi = data.loops[li].vertex_index
            if uvs is not None:
                layer.data[li].uv = uvs[vi]
            else:
                co = Vector(verts[vi])
                layer.data[li].uv = (co.x/.16+.5, co.y/.22+.5)
    obj = bpy.data.objects.new(name, data)
    c.objects.link(obj)
    obj.location = origin
    obj.parent = parent
    return obj


def rounded_outline(cx,cy,width,height,radius,steps=6):
    out=[]
    for x,y,a in [(cx+width/2-radius,cy+height/2-radius,0),
                   (cx-width/2+radius,cy+height/2-radius,90),
                   (cx-width/2+radius,cy-height/2+radius,180),
                   (cx+width/2-radius,cy-height/2+radius,270)]:
        for j in range(steps+1):
            angle=math.radians(a+90*j/steps)
            out.append((x+radius*math.cos(angle),y+radius*math.sin(angle)))
    return out


def cover(name,cx,z,c,parent,leather,lining,hinge):
    outline=rounded_outline(cx,0,COVER_W,COVER_H,.0065)
    n=len(outline)
    verts=[(x,y,z) for x,y in outline]+[(x,y,z+COVER_T) for x,y in outline]
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    obj=mesh(name,verts,faces,[leather,lining],c,parent,origin=hinge)
    bevel=obj.modifiers.new("Tepi sampul membulat halus","BEVEL")
    bevel.width=.0004
    bevel.segments=2
    obj.modifiers.new("Normal sampul","WEIGHTED_NORMAL")
    return obj


def curve(name, points, radius, material, c, root, closed=False):
    data=bpy.data.curves.new(name,"CURVE")
    data.dimensions="3D"
    data.bevel_depth=radius
    data.bevel_resolution=1
    s=data.splines.new("POLY")
    s.points.add(len(points)-1)
    s.use_cyclic_u=closed
    for p,v in zip(s.points,points):
        p.co=(*v,1)
    obj=bpy.data.objects.new(name,data)
    c.objects.link(obj)
    obj.parent=root
    obj.data.materials.append(material)
    return obj


def stitches(cx,z,c,root,material,name):
    path=rounded_outline(cx,0,COVER_W-.005,COVER_H-.005,.006,steps=10)
    # One curve object contains all short stitch splines.
    data=bpy.data.curves.new(name,"CURVE")
    data.dimensions="3D"
    data.bevel_depth=.000085
    data.bevel_resolution=0
    for a,b in zip(path,path[1:]+path[:1]):
        a,b=Vector(a),Vector(b)
        length=(b-a).length
        for j in range(max(1,int(length/.0021))):
            count=max(1,int(length/.0021))
            p=a+(b-a)*(j+.12)/count
            q=a+(b-a)*(j+.60)/count
            s=data.splines.new("POLY")
            s.points.add(1)
            s.points[0].co=(*p,z,1)
            s.points[1].co=(*q,z,1)
    obj=bpy.data.objects.new(name,data)
    c.objects.link(obj)
    obj.parent=root
    obj.data.materials.append(material)


def paper_sheet(number, opened, c, root, paper, edge):
    nx=8
    rows=[-.105,-.104,-.1025,-.101]
    rows += [-.09+.018*j for j in range(11)]
    rows += [.101,.1025,.104,.105]
    ny=len(rows)-1
    left=opened and number<COUNT//2
    side=-1 if left else 1
    stack_index=number if left else COUNT-1-number
    spine=side*.001 if opened else SPINE_X
    base=PAPER_BASE+stack_index*PITCH
    verts=[]
    uv=[]
    corner=.004
    jitter=.000085*math.sin(number*1.83)
    for surface in (0,1):
        for j,y in enumerate(rows):
            inset=0
            if abs(y)>PH/2-corner:
                d=abs(y)-(PH/2-corner)
                inset=corner-math.sqrt(max(0,corner*corner-d*d))
            for i in range(nx+1):
                u=i/nx
                distance=(PW-inset+jitter)*u
                z=base+surface*SHEET
                if opened:
                    # Gentle curl near the sewn gutter, almost flat at the edge.
                    z += .0054*math.sin(math.pi*u)*math.exp(-2.5*u)
                    z += .00035*math.sin(math.pi*u)*math.cos(y/PH*2*math.pi)
                    z += .00020*u*u*math.sin(y*32+number*.12)
                verts.append((spine+side*distance,y,z))
                uv.append(((1-u if left else u),(y+PH/2)/PH))
    stride=nx+1
    face_count=stride*(ny+1)
    faces=[]
    for j in range(ny):
        for i in range(nx):
            a=j*stride+i
            ring=(a,a+1,a+1+stride,a+stride)
            top=tuple(v+face_count for v in ring)
            if left:
                faces.extend([ring,tuple(reversed(top))])
            else:
                faces.extend([tuple(reversed(ring)),top])
    face_surfaces=len(faces)
    boundary=list(range(nx+1))
    boundary += [j*stride+nx for j in range(1,ny+1)]
    boundary += [ny*stride+i for i in range(nx-1,-1,-1)]
    boundary += [j*stride for j in range(ny-1,0,-1)]
    for a,b in zip(boundary,boundary[1:]+boundary[:1]):
        f=(a,b,b+face_count,a+face_count)
        faces.append(tuple(reversed(f)) if left else f)
    obj=mesh(f"Lembar_{number+1:03d}",verts,faces,[paper,edge],c,root,uv,
              origin=(spine,0,base))
    obj["nomor_lembar"]=number+1
    obj["halaman_depan"]=number*2+1
    obj["halaman_belakang"]=number*2+2
    obj["halaman_menghadap_atas"]=number*2+(2 if left else 1)
    obj["tebal_mm"]=SHEET*1000
    for p in obj.data.polygons:
        p.use_smooth=p.index<face_surfaces and opened
        if p.index>=face_surfaces:
            p.material_index=1
        elif p.index % 2 == 0:
            # A leaf turns around its spine: the reverse face needs its own U.
            for li in p.loop_indices:
                obj.data.uv_layers.active.data[li].uv.x = 1 - obj.data.uv_layers.active.data[li].uv.x
    return obj


def strip(name,path,width,material,c,root,thickness=.0008):
    verts=[]
    for x,y,z in path:
        verts += [(x,y-width/2,z),(x,y+width/2,z)]
    faces=[(i*2,i*2+1,i*2+3,i*2+2) for i in range(len(path)-1)]
    obj=mesh(name,verts,faces,[material],c,root)
    for p in obj.data.polygons:p.use_smooth=True
    mod=obj.modifiers.new("Tebal kulit pengikat","SOLIDIFY")
    mod.thickness=thickness
    mod.offset=-1
    return obj


def metal_bar(name,a,b,z,width,depth,material,c,root):
    a,b=Vector(a),Vector(b)
    d=b-a
    length=d.length
    normal=Vector((-d.y,d.x)).normalized()*width/2
    pts=[a-normal,b-normal,b+normal,a+normal]
    verts=[(*p,z) for p in pts]+[(*p,z+depth) for p in pts]
    faces=[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
    obj=mesh(name,verts,faces,[material],c,root)
    bevel=obj.modifiers.new("Sudut kuningan","BEVEL")
    bevel.width=.00012
    bevel.segments=2
    obj.modifiers.new("Normal logam","WEIGHTED_NORMAL")
    return obj


def clasp(cx,cy,z,c,root,gold):
    w,h=.020,.018
    corners=[(cx-w/2,cy-h/2),(cx+w/2,cy-h/2),
             (cx+w/2,cy+h/2),(cx-w/2,cy+h/2)]
    for i in range(4):
        metal_bar(f"Bingkai_Emas_{i+1}",corners[i],corners[(i+1)%4],
                  z,.0010,.00085,gold,c,root)
    # Open rectangular meanders, inspired by the reference's geometric clasp.
    paths=[
        [(-.008,-.006),(-.004,-.006),(-.004,-.002),(-.007,-.002),(-.007,.001),(-.002,.001)],
        [(-.008,.006),(-.004,.006),(-.004,.003),(.001,.003),(.001,.007)],
        [(.008,.006),(.004,.006),(.004,.002),(.007,.002),(.007,-.001),(.002,-.001)],
        [(.008,-.006),(.004,-.006),(.004,-.003),(-.001,-.003),(-.001,-.007)],
        [(-.001,.001),(.002,.001),(.002,.003)],
        [(.001,-.001),(-.002,-.001),(-.002,-.003)],
    ]
    n=0
    for path in paths:
        for a,b in zip(path,path[1:]):
            n+=1
            metal_bar(f"Motif_Emas_{n:02d}",(cx+a[0],cy+a[1]),
                      (cx+b[0],cy+b[1]),z,.00065,.00085,gold,c,root)


def accessories(opened,c,root,leather,gold,ribbon_mat):
    if opened:
        # Released strap folds outward, leaving both writing faces unobstructed.
        path=[(.146,-.009,.0030),(.154,-.009,.0038),(.159,-.009,.0058),
              (.166,-.009,.0052),(.185,-.009,.0040),(.197,-.009,.0030)]
        strip("Pengikat_kulit_terlepas",path,.017,leather,c,root)
        clasp(.185,-.009,.0050,c,root,gold)
        loop_x=.155
        loop_z=.004
        bx=0
    else:
        path=[(.073,-.009,.0010),(.081,-.009,.0010)]
        for i in range(15):
            a=-math.pi/2+math.pi*i/14
            path.append((.081+.0065*math.cos(a),-.009,.0075+.0065*math.sin(a)))
        path += [(.071,-.009,.0142),(.048,-.009,.0142),(.018,-.009,.0142)]
        strip("Pengikat_kulit_melingkar",path,.017,leather,c,root)
        # Rounded tongue at the end of the closure.
        out=rounded_outline(.019,-.009,.014,.017,.004)
        obj=mesh("Ujung_pengikat_membulat",[(x,y,.01425) for x,y in out],
                  [tuple(range(len(out)))],[leather],c,root)
        mod=obj.modifiers.new("Tebal ujung pengikat","SOLIDIFY")
        mod.thickness=.0008
        clasp(.038,-.009,.01465,c,root,gold)
        loop_x=.081
        loop_z=.006
        bx=-.064
    loop=[]
    for i in range(17):
        a=-math.pi/2+math.pi*i/16
        loop.append((loop_x+.0040*math.cos(a),.048,loop_z+.0036*math.sin(a)))
    strip("Lingkar_tempat_pena",loop,.012,leather,c,root,.00065)
    # Narrow brown ribbon bookmark. The short exposed tail has a diagonal cut.
    verts=[(bx-.0018,-.083,.0023),(bx+.0018,-.083,.0023),
           (bx+.0034,-.122,.0007),(bx-.0002,-.123,.0007)]
    obj=mesh("Pita_pembatas_cokelat",verts,[(0,1,2,3)],[ribbon_mat],c,root)
    m=obj.modifiers.new("Tebal pita pembatas","SOLIDIFY")
    m.thickness=.00025


def spine(opened,c,root,leather):
    if opened:
        points=[(-.007,0,.0012),(-.003,0,.0008),(.003,0,.0008),(.007,0,.0012)]
        strip("Punggung_kulit_terbuka",points,COVER_H,leather,c,root)
    else:
        zlow=.0003
        zhigh=TOP_Z+COVER_T-.0002
        zmid=(zlow+zhigh)/2
        points=[]
        for i in range(21):
            a=-math.pi/2+math.pi*i/20
            points.append((-.075-.0036*math.cos(a),0,zmid+(zhigh-zlow)/2*math.sin(a)))
        strip("Punggung_kulit_melengkung",points,COVER_H,leather,c,root)


def paper_binding(c,root):
    material=mat("Kertas pengikat tengah",(.85,.80,.65),.93,
                 texture="Kertas_Krem_Polos.png")
    # A shallow cream valley joins the two stacks without a dark open split.
    top=PAPER_BASE+39*PITCH+SHEET
    profile=[(-.003,top-.00020),(-.001,top),(0,top-.00045),
             (.001,top),(.003,top-.00020)]
    verts=[(x,y,z) for x,z in profile for y in (-PH/2,PH/2)]
    faces=[(2*i,2*i+2,2*i+3,2*i+1) for i in range(len(profile)-1)]
    obj=mesh("Jilidan_kertas_tengah",verts,faces,[material],c,root)
    for poly in obj.data.polygons:poly.use_smooth=True
    mod=obj.modifiers.new("Tebal jilidan","SOLIDIFY")
    mod.thickness=.00020
    mod.offset=-1


def light(c,name,location,target,power,size,color):
    data=bpy.data.lights.new(name,"AREA")
    data.energy=power
    data.size=size
    data.color=color
    obj=bpy.data.objects.new(name,data)
    c.objects.link(obj)
    obj.location=location
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat("-Z","Y").to_euler()


def camera(c,name,position,target,scale):
    data=bpy.data.cameras.new(name)
    data.type="ORTHO"
    data.ortho_scale=scale
    obj=bpy.data.objects.new(name,data)
    c.objects.link(obj)
    obj.location=position
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat("-Z","Y").to_euler()
    return obj


def make_scene(opened):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    scene.unit_settings.system="METRIC"
    scene.unit_settings.scale_length=1
    scene.render.engine="CYCLES"
    scene.cycles.device="CPU"
    scene.cycles.samples=32
    scene.cycles.use_denoising=True
    scene.render.resolution_x=1200
    scene.render.resolution_y=1000
    scene.render.image_settings.file_format="PNG"
    scene.view_settings.view_transform="Standard"
    scene.view_settings.exposure=-1.5 if opened else -.6
    world=bpy.data.worlds.new("Studio netral")
    world.use_nodes=True
    world.node_tree.nodes["Background"].inputs["Color"].default_value=(.22,.23,.25,1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value=.32
    scene.world=world
    asset=collection("BUKU AGENDA A5 - 80 lembar")
    pages=collection("KERTAS - 80 lembar terpisah")
    studio=collection("STUDIO - tidak diekspor")
    root=bpy.data.objects.new("BUKU_AGENDA_A5_ROOT",None)
    asset.objects.link(root)
    root.empty_display_size=.010
    root["jumlah_lembar"]=80
    root["jumlah_halaman"]=160
    root["ukuran_kertas_mm"]="148 x 210 (A5)"
    root["ketebalan_blok_kertas_mm"]=round(((COUNT-1)*PITCH+SHEET)*1000,3)
    root["pose"]="Terbuka" if opened else "Tertutup"
    leather=leather_material()
    lining=mat("Lapisan dalam kulit biru",(.032,.12,.235),.91)
    paper=mat("Kertas krem bergaris dua sisi",(.85,.80,.65),.93,
              texture="Kertas_Agenda_Bergaris.png")
    edge=mat("Tepi serat kertas krem",(.72,.69,.58),.96)
    gold=mat("Kuningan pengunci",(.63,.43,.15),.27,metal=.85)
    thread=mat("Benang biru muda",(.14,.35,.52),.90)
    ribbon_mat=mat("Pita pembatas cokelat",(.17,.083,.033),.88)
    if opened:
        cover("Sampul_depan_terbuka",-.075,.0002,asset,root,leather,lining,(-.001,0,.001))
        cover("Sampul_belakang",.075,.0002,asset,root,leather,lining,(.001,0,.001))
        stitches(-.075,.00216,asset,root,thread,"Jahitan_sampul_kiri")
        stitches(.075,.00216,asset,root,thread,"Jahitan_sampul_kanan")
    else:
        back=cover("Sampul_belakang",.003,.0002,asset,root,leather,lining,(-.075,0,.001))
        back.data.polygons[1].material_index=1
        front=cover("Sampul_depan",.003,TOP_Z,asset,root,leather,lining,(-.075,0,TOP_Z))
        front.data.polygons[0].material_index=1
        stitches(.003,TOP_Z+COVER_T+.00012,asset,root,thread,"Jahitan_sampul_depan")
    for index in range(COUNT):
        paper_sheet(index,opened,pages,root,paper,edge)
    spine(opened,asset,root,leather)
    if opened:paper_binding(asset,root)
    accessories(opened,asset,root,leather,gold,ribbon_mat)
    ground_mat=mat("Lantai studio",(.29,.30,.32),.88)
    bpy.ops.mesh.primitive_plane_add(size=2,location=(0,0,-.0009))
    ground=bpy.context.object
    ground.name="Lantai pratinjau"
    for c in list(ground.users_collection):c.objects.unlink(ground)
    studio.objects.link(ground)
    ground.data.materials.append(ground_mat)
    target=(.015,0,.007) if opened else (0,0,.006)
    light(studio,"Softbox utama",(-.27,-.25,.42),target,3.0,.38,(1,.96,.89))
    light(studio,"Softbox isi",(.28,-.10,.33),target,1.4,.34,(.89,.94,1))
    light(studio,"Lampu tepi",(.02,.27,.36),target,2.3,.28,(1,1,1))
    if opened:
        cam=camera(studio,"Kamera halaman terbuka",(.03,-.22,.47),target,.415)
    else:
        cam=camera(studio,"Kamera sampul",(.245,-.27,.36),target,.31)
    scene.camera=cam
    return scene,asset,pages,studio,root


def save_export(scene,asset,pages,root,opened):
    stem="Buku_Agenda_A5_Terbuka" if opened else "Buku_Agenda_A5_80Lembar"
    image_name="02_Buku_Terbuka.png" if opened else "01_Buku_Tertutup.png"
    if "--skip-render" not in sys.argv:
        scene.render.filepath=str(PREVIEWS/image_name)
        bpy.ops.render.render(write_still=True)
        print("RENDERED",image_name,flush=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    bpy.context.view_layer.objects.active=root
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=="VIEW_3D":
                area.spaces.active.shading.type="MATERIAL"
                area.spaces.active.region_3d.view_location=(0,0,.008)
                area.spaces.active.region_3d.view_distance=.42
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/(stem+".blend")))
    for obj in list(asset.objects):
        if obj.type=="CURVE":
            bpy.ops.object.select_all(action="DESELECT")
            obj.select_set(True)
            bpy.context.view_layer.objects.active=obj
            bpy.ops.object.convert(target="MESH")
    # The decorative metal bars are one mesh in the exported static prop.
    metal=[o for o in asset.objects if o.name.startswith(("Motif_Emas", "Bingkai_Emas"))]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in metal:obj.select_set(True)
    bpy.context.view_layer.objects.active=metal[0]
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.join()
    bpy.context.object.name="Pengunci_Kuningan_Bermotif"
    bpy.ops.object.select_all(action="DESELECT")
    for obj in list(asset.objects)+list(pages.objects):obj.select_set(True)
    fbx_path=UNITY_INVENTORY/(stem+".fbx")
    bpy.ops.export_scene.fbx(filepath=str(fbx_path),use_selection=True,
        object_types={"MESH","EMPTY"},apply_unit_scale=True,
        axis_forward="-Z",axis_up="Y",path_mode="COPY",embed_textures=True)
    print("SAVED",ROOT/(stem+".blend"),fbx_path,"80 sheets",flush=True)


for opened in (False,True):
    scene,asset,pages,studio,root=make_scene(opened)
    save_export(scene,asset,pages,root,opened)

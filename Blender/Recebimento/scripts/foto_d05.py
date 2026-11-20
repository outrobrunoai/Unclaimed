"""D05 intake photo (docs/textos/etapa2-documentos.md): Leo's left forearm with the splint and the 083 wristband, and
belongings bag P26 with its patched seam beside him on the stretcher, photographed with the on-camera flash at intake.
Everything is the game's own geometry, so the photo matches what the player finds later:
  - Leo as he lies in drawer C-02 (Blender/CamaraFria/Ocupantes.blend, opened, never saved): body, sheet, crepe
    bandage, padded aluminium strip, the two hook-and-loop straps and the wristband (Pulseira_083.png);
  - bag P26 and its stitched tear from Blender/Administracao/Administracao.blend, with the baked Unity textures.
Nothing graphic: the sheet covers the body and the face; only the forearm and the hand show.
Run: blender -b --factory-startup --python Blender/Recebimento/scripts/foto_d05.py      (PREVIEW=1: quick, half size)
Out: Blender/Recebimento/fotos/d05_tala.png (then python3 Blender/Recebimento/scripts/documentos_recebimento.py)"""
import math
import os

import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
OUT = ROOT + "/Blender/Recebimento/fotos/d05_tala.png"
TEX = ROOT + "/Assets/Modelos/Administracao/Texturas/"
PREVIEW = os.environ.get("PREVIEW") == "1"
bpy.ops.wm.open_mainfile(filepath=ROOT + "/Blender/CamaraFria/Ocupantes.blend")
sc = bpy.context.scene

LEO = ("Corpo_Leo", "Lencol_Leo", "Tala", "TalaPlaca", "TalaFaixas", "Pulseira083")
for o in sc.objects:
    o.hide_render = o.name not in LEO


def mat(name, rgb, rough, tex=None, normal=None, ao=None, uv=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Roughness"].default_value = rough
    b.inputs["Base Color"].default_value = (*rgb, 1)
    if tex:
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(tex)
        col = t.outputs["Color"]
        if ao:
            a = nt.nodes.new("ShaderNodeTexImage")
            a.image = bpy.data.images.load(ao)
            a.image.colorspace_settings.name = "Non-Color"
            mx = nt.nodes.new("ShaderNodeMix")
            mx.data_type = "RGBA"
            mx.blend_type = "MULTIPLY"
            mx.inputs["Factor"].default_value = 1.0
            nt.links.new(col, mx.inputs[6])
            nt.links.new(a.outputs["Color"], mx.inputs[7])
            col = mx.outputs[2]
        nt.links.new(col, b.inputs["Base Color"])
    if normal:
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(normal)
        t.image.colorspace_settings.name = "Non-Color"
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(t.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    return m


def solo(ob, m):
    ob.data.materials.clear()
    ob.data.materials.append(m)
    for p in ob.data.polygons:
        p.material_index = 0


# ---------------------------------------------------------------- Leo (C-02 pose), wristband print
pulseira = bpy.data.objects["Pulseira083"]
solo(pulseira, mat("Pulseira 083", (1, 1, 1), 0.35, tex=ROOT + "/Assets/Modelos/CamaraFria/Texturas/Etiquetas/Pulseira_083.png"))
sol = pulseira.modifiers.new("Espessura", "SOLIDIFY")
sol.thickness = 0.0012
sol.offset = 1
bpy.data.objects["Corpo_Leo"].data.materials[0].node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.50, 0.40, 0.34, 1)

# ---------------------------------------------------------------- stretcher: mattress with a fitted sheet, steel frame
TOP = 0.03
bpy.ops.mesh.primitive_cube_add(location=(0.06, 1.05, TOP - 0.04), scale=(0.40, 1.08, 0.04))
bpy.context.active_object.data.materials.append(mat("Lencol da maca", (0.74, 0.76, 0.77), 0.95))
bpy.ops.object.modifier_add(type="BEVEL")
bpy.context.active_object.modifiers[-1].width = 0.02
bpy.context.active_object.modifiers[-1].segments = 3
bpy.ops.mesh.primitive_cube_add(location=(0.06, 1.05, TOP - 0.10), scale=(0.41, 1.09, 0.02))
bpy.context.active_object.data.materials.append(mat("Estrutura da maca", (0.55, 0.56, 0.57), 0.35))
bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, -0.85))
bpy.context.active_object.data.materials.append(mat("Piso", (0.07, 0.07, 0.07), 0.9))

# ---------------------------------------------------------------- intake sheet over the body, the splinted forearm on top
# (the drawer's simulated sheet folds badly where it meets the arm; at intake the sheet is fresh anyway). A grid is
# projected down onto a proxy (the body with the crossed forearms flattened into the belly, plus the mattress),
# smoothed and kept outside it; of the body itself only the forearms and hands are rendered, so no skin shows through.
bpy.data.objects["Lencol_Leo"].hide_render = True
body = bpy.data.objects["Corpo_Leo"]
proxy = body.copy(); proxy.data = body.data.copy(); proxy.name = "Proxy do lencol"
sc.collection.objects.link(proxy)
import bmesh
bm = bmesh.new(); bm.from_mesh(proxy.data)
mw = body.matrix_world
inv = mw.inverted()
for v in bm.verts:                                   # flatten the crossed forearms and hands into the belly
    w = mw @ v.co
    if -0.27 < w.x < 0.25 and 0.60 < w.y < 1.02 and w.z > 0.205:
        v.co = inv @ Vector((w.x, w.y, 0.205))
bmesh.ops.create_cube(bm, size=1.0, matrix=mw.inverted() @ __import__("mathutils").Matrix.LocRotScale(
    Vector((0.06, 1.05, TOP - 0.04)), None, Vector((1.10, 2.60, 0.08))))
bm.to_mesh(proxy.data); bm.free()
proxy.hide_render = True
# Only the arms and hands are rendered from the body (posed Humano_Leo, cut by its skin groups); everything else
# is the sheet over the proxy.
hum = bpy.data.objects["Humano_Leo"]
for m in hum.modifiers:
    if m.type == "MASK":
        m.show_render = m.show_viewport = False
dg = bpy.context.evaluated_depsgraph_get()
ev = hum.evaluated_get(dg)
arm_me = bpy.data.meshes.new_from_object(ev, depsgraph=dg)
names = {g.index for g in hum.vertex_groups
         if g.name.startswith(("upperarm_", "lowerarm_", "hand_", "index_", "middle_", "pinky_", "ring_", "thumb_"))}
w = [sum(e.weight for e in v.groups if e.group in names) for v in hum.data.vertices]
arms = bpy.data.objects.new("Antebracos", arm_me)
arms.matrix_world = hum.matrix_world
sc.collection.objects.link(arms)
bm = bmesh.new(); bm.from_mesh(arm_me)
bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[v for v in bm.verts if w[v.index] < 0.5], context="VERTS")
bm.to_mesh(arm_me); bm.free()
solo(arms, body.data.materials[0])
for p_ in arm_me.polygons:
    p_.use_smooth = True
body.hide_render = True
hum.hide_render = True
arms.hide_render = False
bpy.ops.mesh.primitive_grid_add(x_subdivisions=110, y_subdivisions=280, size=1, location=(0.06, 1.02, 0.7))
sheet = bpy.context.active_object
sheet.scale = (0.80, 2.14, 1)
bpy.ops.object.transform_apply(scale=True)
sw = sheet.modifiers.new("Projetar", "SHRINKWRAP"); sw.wrap_method = "PROJECT"; sw.use_negative_direction = True
sw.use_positive_direction = False; sw.use_project_z = True; sw.target = proxy; sw.offset = 0.03
sm = sheet.modifiers.new("Assentar", "SMOOTH"); sm.factor = 1.0; sm.iterations = 90
sw2 = sheet.modifiers.new("Por fora", "SHRINKWRAP"); sw2.wrap_method = "NEAREST_SURFACEPOINT"; sw2.wrap_mode = "OUTSIDE"
sw2.target = proxy; sw2.offset = 0.02
sm2 = sheet.modifiers.new("Assentar 2", "SMOOTH"); sm2.factor = 0.5; sm2.iterations = 3
for nome, escala, forca in (("Dobras", 0.16, 0.014), ("Rugas", 0.05, 0.005)):
    tx = bpy.data.textures.new(nome, "CLOUDS"); tx.noise_scale = escala
    dp = sheet.modifiers.new(nome, "DISPLACE"); dp.texture = tx; dp.strength = forca; dp.mid_level = 0.5
sheet.modifiers.new("Espessura", "SOLIDIFY").thickness = 0.002
bpy.ops.object.shade_smooth()
sheet.data.materials.append(mat("Lencol de entrada", (0.80, 0.81, 0.80), 0.9))

# ---------------------------------------------------------------- bag P26 beside the hip, its front (tag, tear) to the camera
lib = ROOT + "/Blender/Administracao/Administracao.blend"
with bpy.data.libraries.load(lib, link=False) as (src, dst):
    dst.objects = [n for n in src.objects if n in ("BolsaPertences", "RemendoBolsa")]
bag_parts = []
for ob in dst.objects:
    sc.collection.objects.link(ob)
    ob.location = (0, 0, 0)
    ob.hide_render = False
    k = ob.name
    solo(ob, mat("Foto " + k, (1, 1, 1), 0.85, tex=TEX + k + "_BaseMap.png", normal=TEX + k + "_Normal.png", ao=TEX + k + "_Occlusion.png"))
    bag_parts.append(ob)
# Printed tag "P26" over the card (the Unity print quad's place: centre (0.11, -0.125, 0.175), 78 x 48 mm).
from PIL import Image, ImageDraw, ImageFont
tag_png = ROOT + "/Blender/Recebimento/fotos/_tag_p26.png"
im = Image.new("RGB", (390, 240), (238, 235, 222))
d = ImageDraw.Draw(im)
d.rectangle((6, 6, 383, 233), outline=(70, 70, 70), width=3)
d.text((24, 14), "PERTENCES", font=ImageFont.truetype("/usr/share/fonts/noto/NotoSans-Bold.ttf", 34), fill=(60, 60, 60))
d.text((24, 64), "P26", font=ImageFont.truetype("/usr/share/fonts/noto/NotoSans-Bold.ttf", 128), fill=(22, 22, 22))
im.save(tag_png)
me = bpy.data.meshes.new("Etiqueta P26")
x0, x1, z0, z1, y = 0.071, 0.149, 0.151, 0.199, -0.1250
me.from_pydata([(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], [], [(0, 1, 2, 3)])
uv = me.uv_layers.new()
for li, c in enumerate(((0, 0), (1, 0), (1, 1), (0, 1))):
    uv.data[li].uv = c
tag = bpy.data.objects.new("Etiqueta P26", me)
sc.collection.objects.link(tag)
me.materials.append(mat("Etiqueta P26", (1, 1, 1), 0.8, tex=tag_png))
bag_parts.append(tag)
# Bag frame -> stretcher: standing on the mattress beside Leo's hip, its front (-Y) turned to the camera.
for ob in bag_parts:
    ob.rotation_euler = (0, 0, math.radians(160))
    ob.location = (0.37, 0.70, TOP)

# ---------------------------------------------------------------- camera + flash (on-camera, hard, slightly above the lens)
cam_target = Vector((0.17, 0.76, 0.22))
cam_pos = Vector((0.50, 1.05, 1.02))
if os.environ.get("FOTO_CAM"):                      # review: "px,py,pz;tx,ty,tz[;lens]"
    c = os.environ["FOTO_CAM"].split(";")
    cam_pos, cam_target = Vector(map(float, c[0].split(","))), Vector(map(float, c[1].split(",")))
bpy.ops.object.camera_add(location=cam_pos)
cam = bpy.context.active_object
cam.rotation_euler = (cam_target - cam_pos).to_track_quat("-Z", "Y").to_euler()
cam.data.lens = 35
sc.camera = cam
bpy.ops.object.light_add(type="SPOT", location=cam_pos + Vector((0, 0, 0.07)))
flash = bpy.context.active_object
flash.rotation_euler = cam.rotation_euler
flash.data.energy = 14
flash.data.spot_size = math.radians(100)
flash.data.spot_blend = 0.6
flash.data.shadow_soft_size = 0.015
bpy.ops.object.light_add(type="AREA", location=(0.1, 0.9, 2.6))   # dim, cold room light from the ceiling
room = bpy.context.active_object
room.data.energy = 22
room.data.color = (0.8, 0.88, 1.0)
room.data.size = 1.2
sc.world.color = (0.008, 0.008, 0.01)

r = sc.render
r.engine = "CYCLES"
sc.cycles.samples = 32 if PREVIEW else 160
sc.cycles.use_denoising = True
r.resolution_x, r.resolution_y = (700, 430) if PREVIEW else (1400, 860)
r.film_transparent = False
sc.view_settings.view_transform = "Standard"
sc.view_settings.look = "None"
r.image_settings.file_format = "PNG"
os.makedirs(os.path.dirname(OUT), exist_ok=True)
r.filepath = OUT.replace(".png", "_previa.png") if PREVIEW else OUT
bpy.ops.render.render(write_still=True)
print("saved", r.filepath)

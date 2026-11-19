"""D05 intake photo (docs/textos/etapa2-documentos.md): Leo's left forearm with a splint and belongings bag P26 with its
patched seam, photographed with a flash at intake. Reuses the covered body of Blender/Autopsia/Corpo.blend (opened,
never saved) and the bag mesh Assets/Modelos/Administracao/BolsaPertences.fbx. Nothing graphic: face and body stay
under the sheet; only the forearm and hand show.
Run: blender -b --factory-startup --python Blender/Recebimento/scripts/foto_d05.py
Out: Blender/Recebimento/fotos/d05_tala.png (then python3 Blender/Recebimento/scripts/documentos_recebimento.py)"""
import math
import os
import random

import bpy
import bmesh
from mathutils import Vector, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
OUT = ROOT + "/Blender/Recebimento/fotos/d05_tala.png"
bpy.ops.wm.open_mainfile(filepath=ROOT + "/Blender/Autopsia/Corpo.blend")
os.makedirs(os.path.dirname(OUT), exist_ok=True)
sc = bpy.context.scene
for n in ("MesaAutopsia", "Piso estudio", "Softbox", "Softbox.001", "Softbox.002", "EtiquetaMovimentacao", "Alfinete"):
    if n in bpy.data.objects:
        bpy.data.objects[n].hide_render = True


def mat(name, rgb, rough, metal=0.0, tex=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if tex:
        t = m.node_tree.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(tex)
        m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
    return m


def obj(name, me, m):
    ob = bpy.data.objects.new(name, me)
    sc.collection.objects.link(ob)
    me.materials.append(m)
    return ob


# ---------------------------------------------------------------- splint: padded aluminium strip under the forearm,
# curved around it, held by three elastic straps. Forearm runs along X from the wrist (x -0.33) towards the elbow.
ARM_Y, ARM_Z = 0.318, 0.918
x0, x1 = -0.62, -0.31
foam = mat("Tala espuma", (0.85, 0.42, 0.10), 0.7)          # orange padded splint, as used by rescue teams
strap = mat("Tala faixa", (0.08, 0.11, 0.20), 0.85)
bm = bmesh.new()
nx, na = 24, 14
for i in range(nx + 1):
    x = x0 + (x1 - x0) * i / nx
    r = 0.044 + 0.006 * (1 - i / nx)                         # forearm thickens towards the elbow
    for j in range(na + 1):
        a = math.radians(-150 + 300 * j / na)                 # U-shaped trough under the arm, open on top
        bm.verts.new((x, ARM_Y + math.sin(a) * r, ARM_Z - math.cos(a) * r * 0.75))
bm.verts.ensure_lookup_table()
for i in range(nx):
    for j in range(na):
        k = i * (na + 1) + j
        bm.faces.new((bm.verts[k], bm.verts[k + 1], bm.verts[k + na + 2], bm.verts[k + na + 1]))
me = bpy.data.meshes.new("Tala")
bm.to_mesh(me)
bm.free()
tala = obj("Tala", me, foam)
sol = tala.modifiers.new("e", "SOLIDIFY")
sol.thickness = 0.006
for xs in (-0.58, -0.47, -0.36):
    bpy.ops.mesh.primitive_cylinder_add(radius=0.053, depth=0.026, vertices=32, location=(xs, ARM_Y, ARM_Z),
                                        rotation=(0, math.radians(90), 0))
    s_ = bpy.context.active_object
    s_.scale = (0.80, 1, 1)
    s_.data.materials.append(strap)
    bpy.ops.object.shade_smooth()
# Exposed forearm skin over the splint (the body mesh's arm is under the sheet edge here).
skin = bpy.data.materials.get("Pele simples")
bpy.ops.mesh.primitive_cylinder_add(radius=0.036, depth=x1 - x0 + 0.04, location=((x0 + x1) / 2 - 0.01, ARM_Y, ARM_Z + 0.004),
                                    rotation=(0, math.radians(90), 0))
arm = bpy.context.active_object
arm.scale = (0.78, 1, 1)
arm.data.materials.append(skin)
bpy.ops.object.shade_smooth()

# ---------------------------------------------------------------- belongings bag P26 on the stretcher sheet, beside the arm
bpy.ops.import_scene.fbx(filepath=ROOT + "/Assets/Modelos/Administracao/BolsaPertences.fbx")
bag = [o for o in bpy.context.selected_objects if o.type == "MESH"][0]
tex = ROOT + "/Assets/Modelos/Administracao/Texturas/BolsaPertences_BaseMap.png"
bag.data.materials.clear()
bag.data.materials.append(mat("Bolsa", (0.5, 0.5, 0.5), 0.6, tex=tex))
# Lay the bag flat: try the axis rotations and keep the one with the thinnest vertical extent.
best = None
for rx in (0, 90):
    for ry in (0, 90):
        bag.rotation_euler = (math.radians(rx), math.radians(ry), math.radians(8))
        bpy.context.view_layer.update()
        bb = [bag.matrix_world @ Vector(c) for c in bag.bound_box]
        hz = max(v.z for v in bb) - min(v.z for v in bb)
        if best is None or hz < best[0]:
            best = (hz, rx, ry)
bag.rotation_euler = (math.radians(best[1]), math.radians(best[2]), math.radians(8))
bpy.context.view_layer.update()
bb = [bag.matrix_world @ Vector(c) for c in bag.bound_box]
size = Vector([max(v[i] for v in bb) - min(v[i] for v in bb) for i in range(3)])
centre = sum(bb, Vector()) / 8
bag.location += Vector((-0.40, 0.55, 0.905)) - Vector((centre.x, centre.y, min(v.z for v in bb)))
bpy.context.view_layer.update()
bb = [bag.matrix_world @ Vector(c) for c in bag.bound_box]
top = max(v.z for v in bb)
lo, hi = Vector([min(v[i] for v in bb) for i in range(3)]), Vector([max(v[i] for v in bb) for i in range(3)])
print("bag size", size, "top", top)
# The patched seam: a long run of thick, uneven cross stitches in a different (white) thread along one side.
thread = mat("Linha do remendo", (0.86, 0.84, 0.78), 0.8)
rnd = random.Random(26)
inv = bag.matrix_world.inverted()
sx = lo.x + (hi.x - lo.x) * 0.30
n = 12
for i in range(n):
    y = lo.y + (hi.y - lo.y) * (0.22 + 0.50 * i / (n - 1))
    for sgn in (-1, 1):
        origin, target = Vector((sx + rnd.uniform(-.002, .002), y, top + 0.2)), Vector((0, 0, -1))
        hit, loc, nor, _ = bag.ray_cast(inv @ origin, (inv.to_3x3() @ target).normalized())
        if not hit:
            continue
        w = bag.matrix_world @ loc
        bpy.ops.mesh.primitive_cylinder_add(radius=0.0018, depth=0.024, location=w + Vector((0, 0, 0.0015)),
                                            rotation=(0, math.radians(90), math.radians(sgn * 38 + rnd.uniform(-6, 6))))
        bpy.context.active_object.data.materials.append(thread)
# Paper tag with the bag's number.
from PIL import Image, ImageDraw, ImageFont
tag_png = ROOT + "/Blender/Recebimento/fotos/_tag_p26.png"
im = Image.new("RGB", (300, 160), (236, 232, 214))
d = ImageDraw.Draw(im)
d.rectangle((4, 4, 295, 155), outline=(60, 60, 60), width=3)
d.text((26, 26), "P26", font=ImageFont.truetype("/usr/share/fonts/noto/NotoSans-Bold.ttf", 96), fill=(25, 25, 25))
im.save(tag_png)
bpy.ops.mesh.primitive_plane_add(size=1, location=((lo.x + hi.x) / 2 + 0.03, lo.y + 0.06, top + 0.002), rotation=(0, 0, math.radians(-6)))
tag = bpy.context.active_object
tag.scale = (0.06, 0.032, 1)
tag.data.materials.append(mat("Etiqueta P26", (1, 1, 1), 0.8, tex=tag_png))

# ---------------------------------------------------------------- camera + flash (on-camera, hard, slightly from above the lens)
cam_target = Vector((-0.44, 0.47, 0.93))
cam_pos = Vector((-0.50, 0.20, 1.72))
bpy.ops.object.camera_add(location=cam_pos)
cam = bpy.context.active_object
cam.rotation_euler = (cam_target - cam_pos).to_track_quat("-Z", "Y").to_euler()
cam.data.lens = 30
sc.camera = cam
bpy.ops.object.light_add(type="SPOT", location=cam_pos + Vector((0, 0, 0.06)))
flash = bpy.context.active_object
flash.rotation_euler = cam.rotation_euler
flash.data.energy = 9
flash.data.spot_size = math.radians(95)
flash.data.spot_blend = 0.6
flash.data.shadow_soft_size = 0.02
# Dim, cold room light from the ceiling.
bpy.ops.object.light_add(type="AREA", location=(-0.4, 0.3, 3.0))
room = bpy.context.active_object
room.data.energy = 25
room.data.color = (0.8, 0.88, 1.0)
room.data.size = 1.2
sc.world.color = (0.01, 0.01, 0.012)
bpy.ops.mesh.primitive_plane_add(size=6, location=(0, 0, 0.0))     # dark floor far below the stretcher
bpy.context.active_object.data.materials.append(mat("Piso", (0.08, 0.08, 0.08), 0.9))
# Stretcher top under the sheet (the sheet only covers the body).
bpy.ops.mesh.primitive_cube_add(location=(-0.2, 0.25, 0.88), scale=(1.1, 0.5, 0.02))
bpy.context.active_object.data.materials.append(mat("Lencol da maca", (0.78, 0.80, 0.80), 0.95))

r = sc.render
r.engine = "CYCLES"
PREVIEW = os.environ.get("PREVIEW") == "1"
sc.cycles.samples = 24 if PREVIEW else 128
sc.cycles.use_denoising = True
r.resolution_x, r.resolution_y = (700, 430) if PREVIEW else (1400, 860)
r.film_transparent = False
sc.view_settings.view_transform = "Standard"
sc.view_settings.look = "None"
r.image_settings.file_format = "PNG"
os.makedirs(os.path.dirname(OUT), exist_ok=True)
r.filepath = OUT.replace('.png', '_previa.png') if PREVIEW else OUT
bpy.ops.render.render(write_still=True)
print("saved", OUT)

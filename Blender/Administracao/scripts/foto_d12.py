"""D12 photo (docs/textos/etapa2-documentos.md): Célia's flash photo inside Limiar's warehouse, 12/11. Steel shelving
with cardboard boxes; in front, the box labelled "LOTE 784" with Vera's blue keychain (a tooth-shaped charm with a
broken corner) lying on its lid. Original scene built here, nothing imported.
Run: blender -b --factory-startup --python Blender/Administracao/scripts/foto_d12.py      (PREVIEW=1 for a fast low-res pass)
Out: Blender/Administracao/fotos/d12_galpao.png (then python3 Blender/Administracao/scripts/documentos_administracao.py)"""
import math
import os
import random

import bpy
import bmesh
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
OUT = ROOT + "/Blender/Administracao/fotos/d12_galpao.png"
PREVIEW = os.environ.get("PREVIEW") == "1"
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
rnd = random.Random(784)


def mat(name, rgb, rough, metal=0.0, tex=None, noise=0.0, emit=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if tex:
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(tex)
        nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    elif noise:
        # Mottled surface (cardboard, concrete): noise between two tones of the base colour.
        nz = nt.nodes.new("ShaderNodeTexNoise")
        nz.inputs["Scale"].default_value = 18
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].color = (*[c * (1 - noise) for c in rgb], 1)
        ramp.color_ramp.elements[1].color = (*[min(1, c * (1 + noise)) for c in rgb], 1)
        nt.links.new(nz.outputs["Fac"], ramp.inputs["Fac"])
        nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])
    else:
        b.inputs["Base Color"].default_value = (*rgb, 1)
    if emit:
        b.inputs["Emission Color"].default_value = (*emit[0], 1)
        b.inputs["Emission Strength"].default_value = emit[1]
    return m


def box(name, m, size, loc, rot=0.0, bevel=0.004):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=(0, 0, math.radians(rot)))
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = (size[0] / 2, size[1] / 2, size[2] / 2)
    bpy.ops.object.transform_apply(scale=True)
    if bevel:
        md = ob.modifiers.new("b", "BEVEL")
        md.width = bevel
        md.segments = 2
    ob.data.materials.append(m)
    return ob


M = {
    "concreto": mat("Concreto", (0.30, 0.29, 0.27), 0.9, noise=0.25),
    "parede": mat("Bloco pintado", (0.42, 0.42, 0.39), 0.9, noise=0.12),
    "aco": mat("Aco pintado", (0.20, 0.30, 0.42), 0.5, 0.6),
    "chapa": mat("Chapa galvanizada", (0.55, 0.56, 0.56), 0.45, 0.8),
    "papelao": mat("Papelao", (0.47, 0.33, 0.19), 0.85, noise=0.18),
    "papelao2": mat("Papelao velho", (0.38, 0.28, 0.17), 0.88, noise=0.2),
    "fita": mat("Fita adesiva", (0.62, 0.50, 0.30), 0.35),
    "azul": mat("Plastico azul", (0.04, 0.22, 0.75), 0.35),
    "argola": mat("Argola", (0.7, 0.7, 0.72), 0.25, 1.0),
}

# ---------------------------------------------------------------- room: floor, back wall, a side wall
box("Piso", M["concreto"], (12, 12, 0.1), (0, 0, -0.05), bevel=0)
box("Parede fundo", M["parede"], (12, 0.2, 5), (0, 2.6, 2.5), bevel=0)
box("Parede lateral", M["parede"], (0.2, 12, 5), (-2.4, 0, 2.5), bevel=0)


def shelving(x0, y0, w=1.8, d=0.6, h=2.2, levels=(0.12, 0.85, 1.55, 2.15)):
    for dx in (0, w):
        for dy in (0, d):
            box("Montante", M["aco"], (0.05, 0.05, h), (x0 + dx, y0 + dy, h / 2), bevel=0.003)
    for z in levels:
        box("Prateleira", M["chapa"], (w + 0.05, d + 0.05, 0.03), (x0 + w / 2, y0 + d / 2, z), bevel=0.003)
    for z in levels[:-1]:
        # Cardboard boxes of mixed sizes along each shelf.
        x = x0 + 0.05
        while True:
            bw, bd, bh = rnd.uniform(.32, .55), rnd.uniform(.35, .5), rnd.uniform(.25, .48)
            if x + bw > x0 + w - 0.03:
                break
            box("Caixa", M[rnd.choice(("papelao", "papelao2"))], (bw, bd, min(bh, 0.62)),
                (x + bw / 2, y0 + d / 2 + rnd.uniform(-.04, .04), z + 0.015 + min(bh, .62) / 2), rnd.uniform(-4, 4))
            x += bw + rnd.uniform(.02, .06)


shelving(-2.1, 1.8)
shelving(0.0, 1.8)
shelving(2.1, 1.8)

# ---------------------------------------------------------------- the 784 box on the floor in front, lid taped
B = Vector((0.15, 0.55, 0.0))
bw, bd, bh = 0.52, 0.40, 0.36
box("Caixa 784", M["papelao"], (bw, bd, bh), B + Vector((0, 0, bh / 2)), -6)
box("Fita", M["fita"], (0.06, bd + 0.004, 0.003), B + Vector((0, 0, bh + 0.001)), -6, bevel=0)
box("Caixa vizinha", M["papelao2"], (0.48, 0.42, 0.30), B + Vector((-0.62, 0.15, 0.15)), 8)
box("Caixa empilhada", M["papelao"], (0.40, 0.36, 0.28), B + Vector((-0.60, 0.17, 0.44)), 3)
box("Caixa ao lado", M["papelao2"], (0.45, 0.40, 0.42), B + Vector((0.62, 0.12, 0.21)), -12)

# Paper label on the front face: printed, taped on, slightly crooked.
from PIL import Image, ImageDraw, ImageFont
lbl = ROOT + "/Blender/Administracao/fotos/_etiqueta_784.png"
im = Image.new("RGB", (640, 340), (238, 234, 222))
d = ImageDraw.Draw(im)
d.rectangle((6, 6, 633, 333), outline=(40, 40, 40), width=5)
d.text((40, 40), "LIMIAR SERVIÇOS", font=ImageFont.truetype("/usr/share/fonts/noto/NotoSans-Bold.ttf", 44), fill=(50, 50, 50))
d.text((40, 120), "LOTE 784", font=ImageFont.truetype("/usr/share/fonts/noto/NotoSans-Bold.ttf", 130), fill=(20, 20, 20))
im.save(lbl)
bpy.ops.mesh.primitive_plane_add(size=1)
pl = bpy.context.active_object
pl.scale = (0.22, 0.117, 1)
pl.rotation_euler = (math.radians(90), math.radians(2), math.radians(-6))
pl.location = B + Vector((-0.02 * math.sin(math.radians(6)), -bd / 2 - 0.003, bh * 0.58))
pl.data.materials.append(mat("Etiqueta 784", (1, 1, 1), 0.8, tex=lbl))

# ---------------------------------------------------------------- Vera's keychain on the lid: steel ring and a blue
# tooth-shaped charm (crown + two roots) with one corner of the crown broken off.
top = B + Vector((0.07, -0.12, bh + 0.004))
bpy.ops.mesh.primitive_torus_add(major_radius=0.018, minor_radius=0.0019, location=top + Vector((-0.045, 0.022, 0.002)))
bpy.context.active_object.data.materials.append(M["argola"])
bm = bmesh.new()
outline = [(-.018, .012), (-.010, .016), (0, .013), (.010, .016), (.018, .012), (.019, .0), (.014, -.008), (.010, -.024),
           (.005, -.026), (.002, -.012), (-.002, -.012), (-.005, -.026), (-.010, -.024), (-.014, -.008), (-.019, .0)]
# Broken corner: the crown's top right loses a notch.
outline[3], outline[4] = (.008, .010), (.013, .005)
verts = [bm.verts.new((x, y, 0)) for x, y in outline]
bm.faces.new(verts)
me = bpy.data.meshes.new("Dente")
bm.to_mesh(me)
bm.free()
tooth = bpy.data.objects.new("Chaveiro dente", me)
sc.collection.objects.link(tooth)
tooth.location = top + Vector((0.0, 0.0, 0.0))
tooth.rotation_euler = (0, 0, math.radians(-25))
tooth.scale = (1.5, 1.5, 1.5)
so = tooth.modifiers.new("s", "SOLIDIFY")
so.thickness = 0.006
so.offset = 1
md = tooth.modifiers.new("b", "BEVEL")
md.width = 0.0015
md.segments = 3
me.materials.append(M["azul"])

# ---------------------------------------------------------------- camera with on-camera flash; dim sodium lamp far back
cam_pos, target = Vector((0.0, -0.50, 0.98)), Vector((0.12, 0.75, 0.42))
bpy.ops.object.camera_add(location=cam_pos)
cam = bpy.context.active_object
cam.rotation_euler = (target - cam_pos).to_track_quat("-Z", "Y").to_euler()
cam.data.lens = 22
sc.camera = cam
bpy.ops.object.light_add(type="SPOT", location=cam_pos + Vector((0, 0, 0.08)))
fl = bpy.context.active_object
fl.rotation_euler = cam.rotation_euler
fl.data.energy = 38
fl.data.spot_size = math.radians(100)
fl.data.spot_blend = 0.7
fl.data.shadow_soft_size = 0.03
bpy.ops.object.light_add(type="POINT", location=(1.8, 1.2, 3.6))
lamp = bpy.context.active_object
lamp.data.energy = 45
lamp.data.color = (1.0, 0.62, 0.28)
sc.world = bpy.data.worlds.new("Escuro")
sc.world.color = (0.004, 0.004, 0.005)

r = sc.render
r.engine = "CYCLES"
sc.cycles.samples = 24 if PREVIEW else 160
sc.cycles.use_denoising = True
r.resolution_x, r.resolution_y = (700, 395) if PREVIEW else (1400, 790)
sc.view_settings.view_transform = "Standard"
r.image_settings.file_format = "PNG"
r.filepath = OUT.replace(".png", "_previa.png") if PREVIEW else OUT
bpy.ops.render.render(write_still=True)
print("saved", r.filepath)

"""Trava de seguranca da camara fria: emergency release on the inside wall, by the door to the autopsy.

Stainless wall plate with four screws, a painted mechanism housing, a red pull lever with a black rubber grip
hanging from a horizontal pivot, and a red sign above ("SAIDA DE EMERGENCIA / PUXE A ALAVANCA").

Frame (Blender): wall at y = 0, front towards -Y, z up from the bottom of the plate, x across. Unity local =
(x, z, y) after export: front faces local -Z.
Two meshes:
  TravaSegurancaBase     plate, screws, housing, pivot boss, sign (slots: inox, escuro, pintura, placa)
  TravaSegurancaAlavanca the lever, origin on the pivot axis (slots: vermelho, borracha); it swings about X.
The sign texture is written by Blender/Sistemas/Trava/placa.py (Pillow).

Run: blender -b --factory-startup --python Blender/Sistemas/Trava/modelar_trava.py
"""
import os, types, math
import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
OUT = ROOT + "/Assets/Modelos/CamaraFria"


def load(rel):
    p = ROOT + "/" + rel
    ns = {"__name__": "trava_module", "__file__": p}
    exec(compile(open(p).read(), p, "exec"), ns)
    return types.SimpleNamespace(**ns)


R = load("Blender/Recepcao/scripts/recepcao_modelo.py")
box, cyl, tube = R.box, R.cyl, R.tube

# Pivot of the lever (Blender frame); the lever mesh is built around it and exported with it at the origin.
PIVO = Vector((0.0, -0.034, 0.235))
SLOTS = {"base": ("inox", "escuro", "pintura", "placa"), "alavanca": ("vermelho", "borracha")}


def materiais():
    for nome in ("inox", "escuro", "pintura", "placa", "vermelho", "borracha"):
        if bpy.data.materials.get("Trava - " + nome) is None:
            bpy.data.materials.new("Trava - " + nome)


def juntar(nome, partes, slots):
    for ob in partes:
        ob.data.materials.clear()
        ob.data.materials.append(bpy.data.materials["Trava - " + ob["mat"]])
    bpy.ops.object.select_all(action="DESELECT")
    for ob in partes:
        if ob.name not in bpy.context.scene.collection.objects:
            bpy.context.scene.collection.objects.link(ob)
        ob.select_set(True)
    bpy.context.view_layer.objects.active = partes[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = ob.data.name = nome
    # Fixed slot order so Unity can assign materials by index.
    me = ob.data
    atuais = [m.name.replace("Trava - ", "") for m in me.materials]
    mapa = {i: slots.index(k) for i, k in enumerate(atuais)}
    indices = [mapa[p.material_index] for p in me.polygons]
    me.materials.clear()   # also resets every face to slot 0, so the indices are reapplied below
    for k in slots:
        me.materials.append(bpy.data.materials["Trava - " + k])
    for p, i in zip(me.polygons, indices):
        p.material_index = i
    return ob


def placa(nome, x0, x1, z0, z1, y):
    """Flat sign quad facing -Y with 0..1 UVs (the printed texture)."""
    me = bpy.data.meshes.new(nome)
    me.from_pydata([(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], [], [(0, 1, 2, 3)])
    uv = me.uv_layers.new()
    for li, (u, v) in zip(range(4), ((0, 0), (1, 0), (1, 1), (0, 1))):
        uv.data[li].uv = (u, v)
    me.polygons[0].normal_flip() if me.polygons[0].normal.y > 0 else None
    ob = bpy.data.objects.new(nome, me)
    ob["mat"] = "placa"
    bpy.context.scene.collection.objects.link(ob)
    return ob


def base():
    R._current["asset"] = "TravaSegurancaBase"
    p = []
    # Wall plate 120 x 300 x 3 mm with the corners rounded.
    p.append(box("Placa de fixacao", "inox", -0.06, -0.003, 0.0, 0.06, 0.0, 0.30, r=0.0015))
    for x in (-0.045, 0.045):
        for z in (0.018, 0.282):
            p.append(cyl("Parafuso", "escuro", (x, -0.004, z), 0.0045, 0.003, axis=(0, 1, 0), seg=12, r=0.001))
    # Mechanism housing (painted grey) and the boss the lever turns on.
    p.append(box("Caixa do mecanismo", "pintura", -0.034, -0.040, 0.195, 0.034, -0.003, 0.275, r=0.006, seg=3))
    p.append(cyl("Eixo", "inox", (0.0, PIVO.y, PIVO.z), 0.007, 0.090, axis=(1, 0, 0), seg=16, r=0.0015))
    for x in (-0.042, 0.042):
        p.append(cyl("Mancal", "pintura", (x, PIVO.y + 0.002, PIVO.z), 0.013, 0.012, axis=(1, 0, 0), seg=20, r=0.002))
    # Stop under the lever (shows which way it goes).
    p.append(box("Batente", "escuro", -0.02, -0.018, 0.035, 0.02, -0.003, 0.05, r=0.003))
    # Sign above the plate: red backing sheet and the printed face.
    p.append(box("Fundo da placa", "pintura", -0.105, -0.003, 0.33, 0.105, 0.0, 0.44, r=0.002))
    p.append(placa("Placa impressa", -0.1, 0.1, 0.335, 0.435, -0.0032))
    return juntar("TravaSegurancaBase", p, SLOTS["base"])


def alavanca():
    R._current["asset"] = "TravaSegurancaAlavanca"
    o = PIVO
    p = []
    # Hub round the pivot, then a flat bar dropping in front of the plate and a cranked grip at the bottom.
    p.append(cyl("Cubo", "vermelho", (0, 0, 0), 0.016, 0.066, axis=(1, 0, 0), seg=24, r=0.003))
    p.append(tube("Braco", "vermelho", [(0, 0, 0), (0, -0.022, -0.03), (0, -0.026, -0.12), (0, -0.040, -0.16)],
                  0.0085, fillet=0.02, res=3))
    p.append(tube("Travessa", "vermelho", [(-0.05, -0.040, -0.168), (0.05, -0.040, -0.168)], 0.0085, res=3))
    p.append(cyl("Empunhadura", "borracha", (0, -0.040, -0.168), 0.014, 0.085, axis=(1, 0, 0), seg=24, r=0.004))
    for x in (-0.054, 0.054):
        p.append(cyl("Ponteira", "vermelho", (x, -0.040, -0.168), 0.011, 0.008, axis=(1, 0, 0), seg=20, r=0.003))
    ob = juntar("TravaSegurancaAlavanca", p, SLOTS["alavanca"])
    return ob


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    materiais()
    os.makedirs(OUT, exist_ok=True)
    b = base()
    R.export_fbx(b, OUT + "/TravaSegurancaBase.fbx", keep_slots=True)
    a = alavanca()
    R.export_fbx(a, OUT + "/TravaSegurancaAlavanca.fbx", keep_slots=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__), "Trava.blend"))
    print("TRAVA OK", [v.co[:] for v in b.data.vertices][:1], "pivo", tuple(PIVO))


main()

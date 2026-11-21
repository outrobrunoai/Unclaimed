"""Daniel Vilar for the chase: the same MPFB 2 (MakeHuman) adult male as the autopsy-table body (Daniel posed as
the corpse), standing, with MPFB's game_engine rig (full finger bones) for Unity's Humanoid retargeting.
Assets: MPFB core + MakeHuman system assets (CC0) + skins02 pack (CC0); clips: CMU Graphics Lab mocap (free).

Run: blender -b --python Blender/Daniel/scripts/daniel_modelo.py -- etapa [etapa ...]
  modelo  : build the human, eyes/brows/lashes/hair, every outfit candidate; saves Blender/Daniel/Daniel.blend
  visual  : review renders of each outfit (front, three-quarter, side) in Blender/Daniel/revisao/
"""
import os, sys, math
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.dirname(HERE)
BLEND = os.path.join(SRC, "Daniel.blend")
OUT = os.path.join(SRC, "revisao")
STEPS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["modelo", "visual"]
DATA = os.path.expanduser("~/.config/blender/5.2/extensions/.user/blender_org/mpfb/data")
SKIN = DATA + "/skins/mindfront_aksel_skin/mindfront_aksel_skin.mhmat"

# Same macros as Blender/Autopsia/scripts/corpo_base.py (the table body is Daniel).
MACRO = {"gender": 1.0, "age": 0.55, "muscle": 0.5, "weight": 0.5, "proportions": 0.5, "height": 0.52,
         "cupsize": 0.5, "firmness": 0.5, "race": {"asian": 0.2, "caucasian": 0.45, "african": 0.35}}

PARTS = [("eyes/high-poly/high-poly.mhclo", "Eyes"), ("eyebrows/eyebrow001/eyebrow001.mhclo", "Eyebrows"),
         ("eyelashes/eyelashes01/eyelashes01.mhclo", "Eyelashes"), ("hair/short02/short02.mhclo", "Hair")]

# Outfit candidates: name -> (clothes, shoes, tint of the clothes or None)
OUTFITS = {
    "A_uniforme_limiar": ("male_worksuit01", "shoes03", (0.11, 0.13, 0.12)),   # transport overalls, dull grey-green
    "B_camiseta_jeans": ("male_casualsuit06", "shoes02", None),                  # plain tee and jeans, worn sneakers
    "C_jaqueta_escura": ("male_casualsuit05", "shoes03", None),                  # dark jacket over jeans, boots
}


def upd(): bpy.context.view_layer.update()


def tint(obj, rgb):
    """Darkens/desaturates a MAKESKIN material: base colour * grey of the texture * rgb."""
    for slot in obj.material_slots:
        m = slot.material
        if m is None or not m.use_nodes: continue
        nt = m.node_tree
        bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is None or not bsdf.inputs["Base Color"].is_linked: continue
        link = bsdf.inputs["Base Color"].links[0]
        hs = nt.nodes.new("ShaderNodeHueSaturation"); hs.inputs["Saturation"].default_value = 0.15
        mul = nt.nodes.new("ShaderNodeMix"); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"
        sa = next(i for i in mul.inputs if i.name == "A" and i.type == "RGBA")
        sb = next(i for i in mul.inputs if i.name == "B" and i.type == "RGBA")
        out = next(o for o in mul.outputs if o.type == "RGBA")
        mul.inputs["Factor"].default_value = 1.0; sb.default_value = (*rgb, 1.0)
        nt.links.new(link.from_socket, hs.inputs["Color"]); nt.links.new(hs.outputs["Color"], sa)
        nt.links.new(out, bsdf.inputs["Base Color"])


def modelo():
    from bl_ext.blender_org.mpfb.services.humanservice import HumanService
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    h = HumanService.create_human(macro_detail_dict=MACRO, scale=0.1); h.name = "Daniel"
    rig = HumanService.add_builtin_rig(h, "game_engine"); rig.name = "Daniel.rig"
    if os.path.exists(SKIN): HumanService.set_character_skin(SKIN, h, skin_type="ENHANCED_SSS", material_instances=False)
    for rel, kind in PARTS:
        HumanService.add_mhclo_asset(os.path.join(DATA, rel), h, asset_type=kind, subdiv_levels=0)
    for name, (clothes, shoes, rgb) in OUTFITS.items():
        added = []
        for asset in (clothes, shoes):
            before = set(bpy.data.objects)
            HumanService.add_mhclo_asset(os.path.join(DATA, "clothes", asset, asset + ".mhclo"), h,
                                         asset_type="Clothes", subdiv_levels=0)
            new = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
            for o in new:
                o.name = f"{name}.{asset}"; added.append(o)
                if rgb is not None and asset == clothes: tint(o, rgb)
        coll = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(coll)
        for o in added:
            for c in list(o.users_collection): c.objects.unlink(o)
            coll.objects.link(o)
    # Clothes hide the body under them (MPFB delete groups); keep the body whole for every outfit and let each
    # outfit's own mask decide at render time.
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print("saved", BLEND, [o.name for o in bpy.data.objects])


def studio(scene):
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try: scene.render.engine = engine; break
        except TypeError: pass
    scene.render.resolution_x, scene.render.resolution_y = 720, 1080
    scene.render.film_transparent = False
    w = bpy.data.worlds.new("Estudio"); scene.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.035, 0.037, 0.04, 1)
    bpy.ops.mesh.primitive_plane_add(size=20); chao = bpy.context.object; chao.name = "Chao"
    m = bpy.data.materials.new("Chao"); m.use_nodes = True
    next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.09, 0.09, 0.085, 1)
    chao.data.materials.append(m)
    def luz(nome, tipo, energia, pos, cor=(1, 1, 1), tam=1.0):
        d = bpy.data.lights.new(nome, tipo); d.energy = energia; d.color = cor
        if tipo == "AREA": d.size = tam
        o = bpy.data.objects.new(nome, d); scene.collection.objects.link(o); o.location = pos
        o.rotation_euler = (Vector((0, 0, 1.1)) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
    luz("Chave", "AREA", 450, (1.6, -2.2, 2.6), (1.0, 0.93, 0.82), 1.2)
    luz("Recorte", "AREA", 260, (-1.4, 1.8, 2.2), (0.75, 0.85, 1.0), 1.0)
    luz("Preenchimento", "AREA", 60, (-2.2, -1.5, 1.2), (0.9, 0.95, 1.0), 2.0)


def mostrar(name):
    """Only this outfit renders, and only its own masks hide the body under it."""
    for c in bpy.data.collections:
        if c.name in OUTFITS: c.hide_render = c.name != name
    usados = {"Delete." + a for a in OUTFITS[name][:2]}
    for m in bpy.data.objects["Daniel"].modifiers:
        if m.type == "MASK" and m.name.startswith("Delete."): m.show_render = m.show_viewport = m.name in usados


def visual():
    bpy.ops.wm.open_mainfile(filepath=BLEND)
    scene = bpy.context.scene; studio(scene); os.makedirs(OUT, exist_ok=True)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); scene.collection.objects.link(cam); scene.camera = cam
    cam.data.lens = 50
    views = {"frente": (0, -4.2, 1.0), "tres_quartos": (2.6, -3.3, 1.15), "lado": (4.2, 0, 1.0)}
    for name in OUTFITS:
        mostrar(name)
        for view, pos in views.items():
            cam.location = pos
            cam.rotation_euler = (Vector((0, 0, 0.92)) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(OUT, f"{name}_{view}.png")
            bpy.ops.render.render(write_still=True)
    # close-up of the face (shared by every outfit)
    mostrar("A_uniforme_limiar")
    cam.data.lens = 85; cam.location = (0.55, -1.25, 1.66)
    cam.rotation_euler = (Vector((0, 0, 1.6)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.resolution_x = scene.render.resolution_y = 900
    scene.render.filepath = os.path.join(OUT, "rosto.png"); bpy.ops.render.render(write_still=True)


for step in STEPS:
    {"modelo": modelo, "visual": visual}[step]()

# Regenerates the archive / office furniture into Blender/Administracao/Administracao.blend:
# parts -> procedural materials -> one game mesh per asset -> UV atlas -> bake (Cycles, CPU) -> Unity textures -> FBX.
# Headless:  blender -b --factory-startup --python Blender/Administracao/scripts/gerar_tudo.py [-- Asset1 Asset2 ...]
# In Unity, Tools > Unclaimed > Build Administracao Prefabs then rebuilds materials and prefabs.
import os, sys, types
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
BLEND = os.path.join(ROOT, "Blender", "Administracao", "Administracao.blend")
TABLE_SCRIPTS = os.path.join(ROOT, "Blender", "MesaAutopsia", "scripts")
UNITY = os.path.join(ROOT, "Assets", "Modelos", "Administracao")
BAKE_AND_EXPORT = os.environ.get("SO_MALHAS") != "1"
ONLY = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else None

ATLAS = {"MesaServidor": 2048, "GavetaMesa1": 1024, "GavetaMesa2": 1024, "GavetaMesa3": 1024, "EquipamentosMesa": 2048, "ArquivoAco": 1024,
         "GavetaArquivo": 1024, "EstanteAco": 2048, "EstanteCoordenacao": 2048, "ArmarioPertences": 2048, "PortaPertences": 512, "Bancada": 2048,
         "Impressora": 1024, "PastasManuais": 512, "BolsaPertences": 1024, "RemendoBolsa": 512, "Gravador": 1024, "EnvelopeGuarda": 1024, "EnvelopeAba": 512, "CaixaArquivo": 512, "PastaContrato": 512, "MesaApoio": 1024}
AO_SAMPLES = 64
# Baked without the floor plane: the flap is seen from both sides once it opens.
NO_GROUND = ("EnvelopeAba",)
OCCLUDERS = {"EquipamentosMesa": ("MesaServidor",), "RemendoBolsa": ("BolsaPertences",)}


def load(folder, module):
    path = os.path.join(folder, module + ".py")
    ns = {"__name__": module, "__file__": path}
    exec(compile(open(path, encoding="utf-8").read(), path, "exec"), ns)
    return types.SimpleNamespace(**ns)


def main():
    if os.path.exists(BLEND) and bpy.data.filepath != BLEND:
        bpy.ops.wm.open_mainfile(filepath=BLEND)
    os.makedirs(os.path.join(UNITY, "Texturas"), exist_ok=True)
    model = load(HERE, "admin_modelo")
    mats = load(HERE, "admin_materiais").make_all(load(TABLE_SCRIPTS, "mesa_materiais").G, model)
    built = {}
    for i, asset in enumerate(model.ASSETS):
        if ONLY and asset not in ONLY:
            continue
        model.build(asset)
        turned = asset in getattr(model, "INSPECTED", ())
        ob = model.assemble(asset, mats[asset], 2.5 * i, keep_floor=turned)
        built[asset] = model.layout_uvs(ob, ATLAS[asset], hidden_scale=1.0 if turned else 0.3)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    if not BAKE_AND_EXPORT:
        return built
    baker = load(TABLE_SCRIPTS, "mesa_bake")
    for asset in built:
        baker._image.__globals__["SIZE"] = ATLAS[asset]
        ob = bpy.data.objects[asset]
        keep = (asset,) + OCCLUDERS.get(asset, ())
        moved = {n: bpy.data.objects[n].location.copy() for n in keep if n in bpy.data.objects}
        hidden = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.name not in keep and not o.hide_render]
        for o in hidden:
            o.hide_render = True
        for n in moved:
            bpy.data.objects[n].location = (0, 0, 0)
        try:
            imgs, times = baker.bake(ob, [s.material for s in ob.material_slots], ao_samples=AO_SAMPLES,
                                     ground=asset not in NO_GROUND)
        finally:
            for n, loc in moved.items():
                bpy.data.objects[n].location = loc
            for o in hidden:
                o.hide_render = False
        baker.write_unity_maps(imgs, os.path.join(UNITY, "Texturas"), prefix=asset)
        model.export_fbx(ob, os.path.join(UNITY, asset + ".fbx"))
        print("ASSET", asset, built[asset], "px/m", times, flush=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    return built


main()

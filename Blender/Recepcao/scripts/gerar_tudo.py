# Regenerates the reception furniture in the open Recepcao.blend:
# parts -> procedural materials -> one game mesh per asset -> UV atlas -> bake (Cycles, CPU) -> Unity
# textures -> FBX. Reuses the node helper and the baker of the autopsy table (Blender/MesaAutopsia/scripts).
# Run it from Blender's Text Editor, or: blender Recepcao.blend --python scripts/gerar_tudo.py
# BAKE_AND_EXPORT = False rebuilds the meshes only; ONLY limits the run to some assets.
# In Unity, Tools > Unclaimed > Build Recepcao Prefabs then rebuilds materials and prefabs.
import os
import types
import bpy

BAKE_AND_EXPORT = True
ONLY = None   # e.g. ("CadeiraEspera",)

BLEND_DIR = os.path.dirname(bpy.data.filepath)
SCRIPTS = os.path.join(BLEND_DIR, "scripts")
TABLE_SCRIPTS = os.path.join(BLEND_DIR, "..", "MesaAutopsia", "scripts")
UNITY = os.path.join(BLEND_DIR, "..", "..", "Assets", "Modelos", "Recepcao")

# Atlas size per asset and AO samples: the counter is big and seen up close; the desk kit has small parts
# (keycaps, phone keys) seen from the clerk's seat.
ATLAS = {"BalcaoRecepcao": 2048, "CadeiraEspera": 1024, "CadeiraAtendente": 1024, "ArmarioApoio": 1024,
         "LongarinaEspera": 1024, "EquipamentosBalcao": 2048, "LivroRegistro": 512, "Bebedouro": 1024, "Lixeira": 512,
         "LuminariaSobrepor": 1024, "LuminariaExterna": 512, "PortaVidroFixos": 512, "PortaVidroFolha": 1024}
AO_SAMPLES = 64
# Other assets that should occlude an asset during its bake (the desk kit sits on the counter's worktop).
OCCLUDERS = {"EquipamentosBalcao": ("BalcaoRecepcao",)}
SPREAD = {"BalcaoRecepcao": 0.0, "CadeiraEspera": 2.2, "CadeiraAtendente": 3.2, "ArmarioApoio": 4.4,
          "LongarinaEspera": 6.2, "EquipamentosBalcao": 0.0, "LivroRegistro": 7.6, "Bebedouro": 8.3, "Lixeira": 8.9,
          "LuminariaSobrepor": 10.5, "LuminariaSobrepor_Luz": 10.5, "LuminariaExterna": 12.0, "LuminariaExterna_Luz": 12.0,
          "PortaVidroFixos": 14.0, "PortaVidroFolha": 14.0, "PortaVidroVidro": 14.0}


def load(folder, module):
    path = os.path.join(folder, module + ".py")
    ns = {"__name__": module}
    exec(compile(open(path, encoding="utf-8").read(), path, "exec"), ns)
    return types.SimpleNamespace(**ns)


def build_meshes():
    """Builds every asset, returns {asset: (object, px per metre)}."""
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    model = load(SCRIPTS, "recepcao_modelo")
    mats = load(SCRIPTS, "recepcao_materiais").make_all(load(TABLE_SCRIPTS, "mesa_materiais").G, model)
    out = {}
    for asset in model.ASSETS:
        if ONLY and asset not in ONLY:
            continue
        model.build(asset)
        ob = model.assemble(asset, mats[asset], SPREAD[asset])
        out[asset] = (ob, None if asset in model.UNBAKED else model.layout_uvs(ob, ATLAS[asset]))
    return out


def bake_and_export(asset):
    model = load(SCRIPTS, "recepcao_modelo")
    if asset in model.UNBAKED:   # diffusers, tubes, glass: own UVs and Unity materials, nothing to bake
        return {}, [], model.export_fbx(bpy.data.objects[asset], os.path.join(UNITY, asset + ".fbx"), keep_slots=True)
    baker = load(TABLE_SCRIPTS, "mesa_bake")
    baker._image.__globals__["SIZE"] = ATLAS[asset]   # the table's baker reads its atlas size from SIZE
    ob = bpy.data.objects[asset]
    # Every asset is baked at the origin (where the bake's ground plane is), alone except for its occluders,
    # so the assets spread out for review never shadow one another.
    keep = (asset,) + OCCLUDERS.get(asset, ())
    moved = {n: bpy.data.objects[n].location.copy() for n in keep if n in bpy.data.objects}
    hidden = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.name not in keep and not o.hide_render]
    for o in hidden:
        o.hide_render = True
    for n in moved:
        bpy.data.objects[n].location = (0, 0, 0)
    try:
        imgs, times = baker.bake(ob, [s.material for s in ob.material_slots], ao_samples=AO_SAMPLES)
    finally:
        for n, loc in moved.items():
            bpy.data.objects[n].location = loc
        for o in hidden:
            o.hide_render = False
    files = baker.write_unity_maps(imgs, os.path.join(UNITY, "Texturas"), prefix=asset)
    fbx = model.export_fbx(ob, os.path.join(UNITY, asset + ".fbx"))
    return times, files, fbx


def main():
    built = build_meshes()
    if BAKE_AND_EXPORT:
        for asset in built:
            print(asset, bake_and_export(asset)[0])
    return built


if __name__ == "__main__":
    main()

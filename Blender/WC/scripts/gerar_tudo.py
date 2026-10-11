# Regenerates the bathroom (WC) fittings in the open WC.blend:
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
UNITY = os.path.join(BLEND_DIR, "..", "..", "Assets", "Modelos", "WC")

# Atlas size per asset (the toilet and basin are seen close up) and AO samples.
ATLAS = {"VasoSanitario": 1024, "Lavatorio": 1024, "Saboneteira": 512, "ToalheiroPapel": 512, "PapeleiraRolao": 512,
         "BarraApoio": 512, "PortaWCFixos": 1024, "PortaWCFolha": 1024}
AO_SAMPLES = 64
OCCLUDERS = {}
SPREAD = {"VasoSanitario": 0.0, "Lavatorio": 1.2, "Espelho": 2.2, "Saboneteira": 3.0, "ToalheiroPapel": 3.5,
          "PapeleiraRolao": 4.1, "BarraApoio": 4.8, "PortaWCFixos": 6.5, "PortaWCFolha": 6.5}


def load(folder, module):
    path = os.path.join(folder, module + ".py")
    ns = {"__name__": module, "__file__": path}
    exec(compile(open(path, encoding="utf-8").read(), path, "exec"), ns)
    return types.SimpleNamespace(**ns)


def build_meshes():
    """Builds every asset, returns {asset: (object, px per metre)}."""
    ob0 = getattr(bpy.context, "object", None)
    if ob0 and ob0.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    model = load(SCRIPTS, "wc_modelo")
    mats = load(SCRIPTS, "wc_materiais").make_all(load(TABLE_SCRIPTS, "mesa_materiais").G, model)
    out = {}
    for asset in model.ASSETS:
        if ONLY and asset not in ONLY:
            continue
        model.build(asset)
        ob = model.assemble(asset, mats[asset], SPREAD[asset])
        out[asset] = (ob, None if asset in model.UNBAKED else model.layout_uvs(ob, ATLAS[asset]))
    return out


def bake_and_export(asset):
    model = load(SCRIPTS, "wc_modelo")
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

# Regenerates the autopsy table in the open MesaAutopsia.blend:
# parts -> UV seams/unwrap -> procedural materials -> one game mesh -> atlas layout
# -> bake (Cycles, CPU) -> Unity textures -> FBX.
# Run it from Blender's Text Editor, or: blender MesaAutopsia.blend --python scripts/gerar_tudo.py
# Set BAKE_AND_EXPORT = False to rebuild the mesh only. The tiled detail map
# (AcoEscovado_DetailNormal.png) comes from detalhe_escovado.py, run with a system Python (numpy + Pillow).
import os
import bpy

BAKE_AND_EXPORT = True

BLEND_DIR = os.path.dirname(bpy.data.filepath)
SCRIPTS = os.path.join(BLEND_DIR, "scripts")
UNITY = os.path.join(BLEND_DIR, "..", "..", "Assets", "Modelos", "MesaAutopsia")


def load(module):
    path = os.path.join(SCRIPTS, module + ".py")
    ns = {"__name__": module}
    exec(compile(open(path, encoding="utf-8").read(), path, "exec"), ns)
    return ns


def main():
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    model, materials, baker = load("mesa_modelo"), load("mesa_materiais"), load("mesa_bake")
    steel, rubber = model["build_all"]()
    model["unwrap_parts"](steel + rubber)
    mats = materials["make_all"]()
    ob = model["assemble"](mats)
    model["layout_uvs"](ob)
    if BAKE_AND_EXPORT:
        imgs, times = baker["bake"](ob, [s.material for s in ob.material_slots])
        print("bake (s):", times)
        print(baker["write_unity_maps"](imgs, os.path.join(UNITY, "Texturas")))
        print(model["export_fbx"](ob, os.path.join(UNITY, "MesaAutopsia.fbx")))
    return ob


main()

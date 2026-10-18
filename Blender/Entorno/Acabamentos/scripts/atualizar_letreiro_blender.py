"""Atualização seletiva no Blender aberto; preserva todos os outros objetos e exports."""
import bpy, json, importlib.util
from pathlib import Path

ROOT=Path(__file__).resolve().parents[4]
SOURCE=ROOT/'Blender/Entorno/Acabamentos'
spec=importlib.util.spec_from_file_location('unclaimed_modelos',SOURCE/'scripts/modelos.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
expected=str(SOURCE/'Acabamentos_Exterior.blend')
if bpy.data.filepath!=expected:raise RuntimeError('Abra a fonte Acabamentos_Exterior.blend antes da atualização seletiva.')
scene=bpy.data.scenes.get('Acabamentos Exterior')
old=scene.objects.get('Placa_Fachada')
if old is None:raise RuntimeError('Letreiro fonte ausente.')
location=old.location.copy();rotation=old.rotation_euler.copy()
unrelated={o.name:(o.as_pointer(),tuple(o.location),o.data.as_pointer()) for o in scene.objects if o!=old}
bpy.context.window.scene=scene;m.SCENE=scene
old_collections=list(old.users_collection)
bpy.data.objects.remove(old,do_unlink=True)
for col in old_collections:
    if col.name=='Placa_Fachada' and len(col.objects)==0:bpy.data.collections.remove(col)
# Reload the only changed image; remove stale texture nodes from its source material.
for im in bpy.data.images:
    if im.filepath and Path(bpy.path.abspath(im.filepath)).name=='Placa_Fachada.png':im.reload()
mat=bpy.data.materials.get('Print_Placa_Fachada')
if mat and mat.use_nodes:
    for node in list(mat.node_tree.nodes):
        if node.type=='TEX_IMAGE':mat.node_tree.nodes.remove(node)
m.main_sign();o=m.finish('Placa_Fachada');o.location=location;o.rotation_euler=rotation
assert unrelated=={o.name:(o.as_pointer(),tuple(o.location),o.data.as_pointer()) for o in scene.objects if o.name!='Placa_Fachada'}
manifest=m.EXPORT/'assets.json';data=json.loads(manifest.read_text())
data['assets']['Placa_Fachada']=m.META['Placa_Fachada']
data['materials']['Chapa_Letreiro']=m.COLORS['Chapa_Letreiro']
manifest.write_text(json.dumps(data,indent=2))
for file in ['modelos.py','texturas.py','letreiro.py','atualizar_letreiro_blender.py']:
    block=bpy.data.texts.get('Acabamentos_'+file) or bpy.data.texts.new('Acabamentos_'+file)
    block.clear();block.write((SOURCE/'scripts'/file).read_text())
# Retain the previous source as a project-local backup; do not generate another .blend1.
old_versions=bpy.context.preferences.filepaths.save_version
try:
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=expected)
finally:bpy.context.preferences.filepaths.save_version=old_versions
print(json.dumps({'updated':'Placa_Fachada','unchanged_objects':len(unrelated),'mesh':m.META['Placa_Fachada']}))

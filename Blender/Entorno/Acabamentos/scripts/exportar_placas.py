"""Rebuild and export only the named signs, keeping every other exterior finish untouched.
    blender -b Blender/Entorno/Acabamentos/Acabamentos_Exterior.blend --python Blender/Entorno/Acabamentos/scripts/exportar_placas.py -- Placa_Atendimento [...]
Updates those entries (and the material table) in Assets/Modelos/Entorno/Acabamentos/assets.json; saves the .blend."""
import sys, json, runpy
from pathlib import Path
import bpy

HERE = Path(__file__).resolve().parent
SIZES = {'Placa_Fachada': (2.4, .52, .025), 'Placa_Atendimento': (.44, .32, .009), 'Placa_Numero': (.24, .12, .005),
         'Placa_NaoEstacione': (.5, .25, .004), 'Placa_Guarita': (.5, .19, .006)}

m = runpy.run_path(str(HERE / 'modelos.py'), run_name='modelos')['main'].__globals__
names = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
scene = bpy.data.scenes.get('Acabamentos Exterior') or bpy.data.scenes.new('Acabamentos Exterior')
bpy.context.window.scene = scene
m['SCENE'] = scene
for name in names:
    old = bpy.data.collections.get(name)
    if old:
        for o in list(old.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(old)
    # Materials are rebuilt from COLORS so edited values reach the export.
    for mat in list(bpy.data.materials):
        if mat.users == 0:
            bpy.data.materials.remove(mat)
    if name == 'Interfone_Externo':
        m['intercom']()
    else:
        m['sign'](name, *SIZES[name])
    m['finish'](name)
manifest_path = m['EXPORT'] / 'assets.json'
manifest = json.loads(manifest_path.read_text())
manifest['assets'].update(m['META'])
manifest['materials'] = m['COLORS']
manifest_path.write_text(json.dumps(manifest, indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(m['SOURCE'] / 'Acabamentos_Exterior.blend'))
print('Exported:', ', '.join(names))

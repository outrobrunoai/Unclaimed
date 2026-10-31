"""Part-by-part inspection: blender -b --factory-startup --python depurar.py -- Asset   (prints each part's bounding box)"""
import sys, runpy, bpy
from mathutils import Vector
name = sys.argv[sys.argv.index('--') + 1]
g = runpy.run_path('/mnt/dados/Projetos/Unity/Unclaimed/Blender/Descanso/scripts/gerar.py', run_name='dbg')
g['R']._current['asset'] = name
g['BUILDERS'][name]()
for o in g['R']._coll(name).objects:
    v = [Vector(c) for c in o.bound_box]
    mn = Vector(map(min, *v)); mx = Vector(map(max, *v))
    print(f'{o.name:34s} x[{mn.x:+.3f},{mx.x:+.3f}] y[{mn.y:+.3f},{mx.y:+.3f}] z[{mn.z:+.3f},{mx.z:+.3f}] tris={len(o.data.polygons)}')
if len(sys.argv) > sys.argv.index('--') + 2:
    o = bpy.data.objects[sys.argv[sys.argv.index('--') + 2]]
    vs = sorted(o.data.vertices, key=lambda v: -v.co.z)[:6]
    for v in vs: print('TOP', tuple(round(c, 4) for c in v.co))

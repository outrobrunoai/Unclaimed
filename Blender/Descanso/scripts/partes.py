"""Render only some parts of an asset (before they are joined): blender -b --factory-startup --python partes.py -- Asset 'regex' out.png [views]"""
import sys, runpy, re, bpy
from mathutils import Vector
a = sys.argv[sys.argv.index('--') + 1:]
name, rx, out = a[0], re.compile(a[1]), a[2]
views = a[3] if len(a) > 3 else 'fq'
g = runpy.run_path('/mnt/dados/Projetos/Unity/Unclaimed/Blender/Descanso/scripts/gerar.py', run_name='dbg')
g['R']._current['asset'] = name
g['BUILDERS'][name]()
keep = []
for o in list(g['R']._coll(name).objects):
    if rx.search(o.name):
        o.name = 'K_' + o.name
        keep.append(o)
    else:
        o.hide_render = True
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.render.resolution_x = sc.render.resolution_y = 600
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'SINGLE'; sc.display.shading.single_color = (0.78, 0.76, 0.72)
sc.display.shading.show_cavity = True
sc.world.color = (0.3, 0.31, 0.33)
mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
for o in keep:
    for c in o.bound_box:
        mn = Vector(map(min, mn, c)); mx = Vector(map(max, mx, c))
ctr, ext = (mn + mx) / 2, max((mx - mn).length, 0.1)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = ext * 1.05
dirs = {'f': (0, -1, 0), 'b': (0, 1, 0), 'l': (-1, 0, 0), 'r': (1, 0, 0), 't': (0, -0.02, 1), 'q': (-0.6, -0.8, 0.35), 'Q': (0.7, -0.6, 0.45), 'u': (0, -0.02, -1)}
from PIL import Image
tiles = []
for v in views:
    d = Vector(dirs[v]).normalized()
    cam.location = ctr + d * ext * 2
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = f'/tmp/_pt_{v}.png'
    bpy.ops.render.render(write_still=True)
    tiles.append(Image.open(f'/tmp/_pt_{v}.png').convert('RGB'))
sh = Image.new('RGB', (600 * len(tiles), 600))
for i, t in enumerate(tiles): sh.paste(t, (i * 600, 0))
sh.save(out)

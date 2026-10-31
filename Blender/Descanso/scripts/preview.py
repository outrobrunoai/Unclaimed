"""Workbench previews of assets in Descanso.blend: blender -b Descanso.blend --python preview.py -- out.png Asset[,Asset2] [views]
views: any of f (front), b (back), l (left), r (right), t (top), q (three-quarter), u (under), default 'fqlr'"""
import bpy, sys, math
from mathutils import Vector
a = sys.argv[sys.argv.index('--') + 1:]
out, names = a[0], a[1].split(',')
views = a[2] if len(a) > 2 else 'fqlr'
size = int(a[3]) if len(a) > 3 else 520
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.render.resolution_x = sc.render.resolution_y = size
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'SINGLE'
sc.display.shading.single_color = (0.78, 0.76, 0.72)
sc.display.shading.show_cavity = True
sc.display.shading.show_shadows = True
sc.display.shading.cavity_type = 'BOTH'
sc.world.color = (0.30, 0.31, 0.33)
for o in bpy.data.objects:
    o.hide_render = o.name not in names
objs = [bpy.data.objects[n] for n in names]
mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
for o in objs:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
ctr, ext = (mn + mx) / 2, max((mx - mn).length, 0.1)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = ext * 1.05
dirs = {'f': (0, -1, 0), 'b': (0, 1, 0), 'l': (-1, 0, 0), 'r': (1, 0, 0), 't': (0, -0.02, 1), 'q': (-0.6, -0.8, 0.35), 'u': (0, -0.02, -1), 'Q': (0.7, -0.6, 0.45)}
import os
from PIL import Image
tiles = []
for v in views:
    d = Vector(dirs[v]).normalized()
    cam.location = ctr + d * ext * 2
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = f'/tmp/_pv_{v}.png'
    bpy.ops.render.render(write_still=True)
    tiles.append(Image.open(f'/tmp/_pv_{v}.png').convert('RGB'))
sh = Image.new('RGB', (size * len(tiles), size))
for i, t in enumerate(tiles): sh.paste(t, (i * size, 0))
sh.save(out)

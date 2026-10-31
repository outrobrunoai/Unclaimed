"""Plain grey studio render of a lab coat drape, for judging the silhouette without textures:
blender -b --factory-startup --python jaleco_cinza.py -- drape.json out_prefix [views] [samples]
views: f (front), l (side, from the wearer's left), q (three-quarter), b (back); writes out_prefix_<view>.png.
The drape is given a 2.4 mm shell; the hanger is the same profile as pecas_roupas.cabide. Same frame as jaleco_molde.py."""
import bpy, bmesh, json, math, sys
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
SRC, OUT = a[0], a[1]
VIEWS = a[2] if len(a) > 2 else 'flq'
SAMPLES = int(a[3]) if len(a) > 3 else 64

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
sc = bpy.context.scene


def material(name, rgb, rough):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = (*rgb, 1)
    b.inputs['Roughness'].default_value = rough
    return m


def link(name, bm, mat):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    sc.collection.objects.link(ob)
    ob.data.materials.append(mat)
    return ob


d = json.load(open(SRC))
bm = bmesh.new()
vs = [bm.verts.new(c) for c in d['v']]
for f in d['f']:
    try:
        bm.faces.new([vs[i] for i in f])
    except ValueError:
        pass
grey = material('cinza', (0.55, 0.55, 0.55), 0.8)
coat = link('jaleco', bm, grey)
for p in coat.data.polygons:
    p.use_smooth = True
sol = coat.modifiers.new('esp', 'SOLIDIFY')
sol.thickness = 0.0024
sol.offset = 0.0

# patch pockets, laid on the settled front (sleeves excluded) by rays from the front; the edges come down onto the shell's
# outer face (the drape is the shell's mid-surface, 1.2 mm under it)
from mathutils.bvhtree import BVHTree
sleeves = set(d.get('manga', []))
body = [f for i, f in enumerate(d['f']) if i not in sleeves]
tree = BVHTree.FromPolygons([Vector(c) for c in d['v']], body)


def smooth(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def pocket(name, x0, x1, z0, z1, belly, n=16, fe=0.14):
    bm = bmesh.new()
    grid = []
    for j in range(n + 1):
        row = []
        for i in range(n + 1):
            u, v = i / n, j / n
            x, z = x0 + (x1 - x0) * u, z0 + (z1 - z0) * v
            loc, nrm, _, _ = tree.ray_cast(Vector((x, -0.5, z)), Vector((0, 1, 0)), 1.0)
            if loc is None:
                return
            if nrm.y > 0:
                nrm = -nrm
            k = (smooth(0, fe, u) * smooth(0, fe, 1 - u) * smooth(0, fe, v)) ** 0.6     # the mouth (v = 1) stays open
            lift = 0.0016 + (0.0010 + belly * math.sin(math.pi * u) * math.sin(math.pi * min(v * 1.15, 1))) * k
            row.append(bm.verts.new(loc + nrm * lift))
        grid.append(row)
    for j in range(n):
        for i in range(n):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    ob = link(name, bm, grey)
    for p in ob.data.polygons:
        p.use_smooth = True
    m = ob.modifiers.new('esp', 'SOLIDIFY')
    m.thickness, m.offset = 0.0008, -1


if d.get('bolsos', True):
    for name, x0, x1 in (('bolso d', -0.195, -0.060), ('bolso e', 0.060, 0.195)):
        pocket(name, x0, x1, -0.720, -0.548, 0.0016)
    pocket('bolso do peito', 0.098, 0.192, -0.330, -0.238, 0.0010)

# hanger
hm = material('cabide', (0.25, 0.25, 0.27), 0.5)
for s in (-1, 1):
    bm = bmesh.new()
    pts = [(0.0, -0.0825), (s * 0.206, -0.1375), (s * 0.206, -0.1560), (0.0, -0.1010)]
    f0 = [bm.verts.new((x, -0.0042, z)) for x, z in pts]
    f1 = [bm.verts.new((x, 0.0042, z)) for x, z in pts]
    bm.faces.new(f0)
    bm.faces.new(f1[::-1])
    for i in range(4):
        bm.faces.new((f0[i], f0[i - 1], f1[i - 1], f1[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    link(f'braco{s}', bm, hm)
bm = bmesh.new()
bmesh.ops.create_cube(bm, size=1.0)
bmesh.ops.scale(bm, vec=(0.412, 0.0072, 0.015), verts=bm.verts)
bmesh.ops.translate(bm, vec=(0, 0, -0.150), verts=bm.verts)
link('barra', bm, hm)
bpy.ops.curve.primitive_bezier_circle_add(radius=0.0085, location=(-0.0085, 0, -0.0037), rotation=(math.pi / 2, 0, 0))
hook = bpy.context.active_object
hook.data.bevel_depth = 0.0016
hook.data.materials.append(hm)
bm = bmesh.new()
bmesh.ops.create_cube(bm, size=1.0)
bmesh.ops.scale(bm, vec=(0.003, 0.003, 0.088), verts=bm.verts)
bmesh.ops.translate(bm, vec=(0, 0, -0.050), verts=bm.verts)
link('haste', bm, hm)

# studio: neutral grey backdrop, soft key + fill, no colour
wm = material('fundo', (0.42, 0.42, 0.42), 0.9)
bm = bmesh.new()
bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=3.0)
bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=__import__('mathutils').Matrix.Rotation(math.radians(90), 3, 'X'), verts=bm.verts)
bmesh.ops.translate(bm, vec=(0, 0.074, -0.5), verts=bm.verts)
link('parede', bm, wm)
for name, loc, energy, size in (('key', (-1.2, -2.0, 0.9), 40, 1.6), ('fill', (1.6, -1.4, 0.2), 16, 2.2), ('top', (0.0, -0.6, 1.8), 10, 1.5)):
    L = bpy.data.lights.new(name, 'AREA')
    L.energy, L.size = energy, size
    o = bpy.data.objects.new(name, L)
    sc.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector((0, 0, -0.5)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
sc.world = bpy.data.worlds.new('w')
sc.world.use_nodes = True
bg = next(n for n in sc.world.node_tree.nodes if n.type == 'BACKGROUND')
bg.inputs['Color'].default_value = (0.42, 0.42, 0.42, 1)
bg.inputs['Strength'].default_value = 0.35

try:
    sc.render.engine = 'CYCLES'
except TypeError:
    pass
sc.cycles.samples = SAMPLES
sc.cycles.use_denoising = True
sc.view_settings.view_transform = 'Standard'
sc.render.resolution_x, sc.render.resolution_y = 700, 900
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.lens = 50
import os
target = Vector(tuple(float(c) for c in os.environ.get('CT', '0 0 -0.53').split()))
dirs = {'f': (0, -1, 0.06), 'l': (1, 0, 0.06), 'q': (0.62, -0.78, 0.10), 'b': (0, 1, 0.06), 'u': (0.15, -0.35, -1.0)}
for v in VIEWS:
    if v == 'b':
        bpy.data.objects['parede'].hide_render = True
    dv = Vector(dirs[v]).normalized()
    cam.location = target + dv * float(os.environ.get('CD', 2.0))
    cam.rotation_euler = (-dv).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = f'{OUT}_{v}.png'
    bpy.ops.render.render(write_still=True)

"""Review render of the storage kit straight from Deposito.blend (procedural materials, print through 'Impressao'):
blender -b Blender/Deposito/Deposito.blend --python previa.py -- out_prefix scene.json [samples]
scene.json: {"objects": [[asset, x, y, z, yaw_deg], ...], "cameras": [[name, [x, y, z], [tx, ty, tz], lens], ...]}.
A neutral room: floor, two walls, one ceiling tube light (4300 K-ish), dim fill. Writes out_prefix_<name>.png."""
import bpy, json, math, sys
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
OUT, SPEC = a[0], json.load(open(a[1]))
SAMPLES = int(a[2]) if len(a) > 2 else 48
sc = bpy.context.scene
src = {o.name: o for o in bpy.data.objects if o.type == 'MESH' and not o.name.endswith('_fonte')}
for o in list(sc.objects):
    o.hide_render = True
col = bpy.data.collections.new('previa')
sc.collection.children.link(col)
for i, (asset, x, y, z, yaw) in enumerate(SPEC['objects']):
    s = src[asset]
    o = s.copy()
    o.hide_render = False
    o.location = (x, y, z)
    o.rotation_euler = (0, 0, math.radians(yaw))
    col.objects.link(o)


def mat(name, rgb, rough):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = (*rgb, 1)
    b.inputs['Roughness'].default_value = rough
    return m


def plane(name, co, m):
    me = bpy.data.meshes.new(name)
    me.from_pydata(co, [], [(0, 1, 2, 3)])
    o = bpy.data.objects.new(name, me)
    o.data.materials.append(m)
    col.objects.link(o)


plane('piso', [(-4, -4, 0), (4, -4, 0), (4, 4, 0), (-4, 4, 0)], mat('piso', (0.40, 0.39, 0.37), 0.6))
wall = mat('parede', (0.62, 0.62, 0.60), 0.8)
yw = SPEC.get('wall_y', 0.30)
plane('parede', [(-4, yw, 0), (4, yw, 0), (4, yw, 3.2), (-4, yw, 3.2)], wall)
xw = SPEC.get('wall_x', -2.0)
plane('parede2', [(xw, -4, 0), (xw, 4, 0), (xw, 4, 3.2), (xw, -4, 3.2)], wall)
L = bpy.data.lights.new('tubo', 'AREA')
L.shape, L.size, L.size_y = 'RECTANGLE', 1.2, 0.15
L.energy, L.color = 130, (1.0, 0.93, 0.84)
o = bpy.data.objects.new('tubo', L)
o.location = SPEC.get('light', (0.0, -1.2, 3.1))
col.objects.link(o)
sc.world = bpy.data.worlds.new('w')
sc.world.use_nodes = True
bg = next(n for n in sc.world.node_tree.nodes if n.type == 'BACKGROUND')
bg.inputs['Color'].default_value = (0.30, 0.31, 0.33, 1)
bg.inputs['Strength'].default_value = 0.25
try:
    sc.render.engine = 'CYCLES'
except TypeError:
    pass
sc.cycles.samples = SAMPLES
sc.cycles.use_denoising = True
sc.view_settings.view_transform = 'Standard'
sc.render.resolution_x, sc.render.resolution_y = SPEC.get('res', (1280, 800))
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
col.objects.link(cam)
sc.camera = cam
for name, pos, tgt, lens in SPEC['cameras']:
    cam.location = pos
    cam.data.lens = lens
    cam.rotation_euler = (Vector(tgt) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = f'{OUT}_{name}.png'
    bpy.ops.render.render(write_still=True)

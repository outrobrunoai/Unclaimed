"""Review render of the technical-room kit straight from Tecnica.blend (procedural materials), with the door devices
and atlas labels placed as Layout.json will place them:
blender -b Blender/Tecnica/Tecnica.blend --python Blender/Tecnica/scripts/previa.py -- out_prefix scene.json [samples]
scene.json: {"objects": [[asset, x, y, z, yaw_deg], ...], "cameras": [[name, [x, y, z], [tx, ty, tz], lens], ...],
"lit": [device names lit]}. Wall at y = 0 behind the panels, floor, one tube light. Writes out_prefix_<name>.png."""
import bpy, json, math, os, sys
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
OUT, SPEC = a[0], json.load(open(a[1]))
SAMPLES = int(a[2]) if len(a) > 2 else 48
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../..'))
SEL = {'Seletor - refrigeracao': 45, 'Seletor - luz_geral': 45, 'Seletor - exame': -45}   # clockwise from the front
LIT = {'Sinaleiro - refrigeracao': (0.1, 1.0, 0.2), 'Sinaleiro - luz_geral': (0.1, 1.0, 0.2), 'Sinaleiro - ligado': (0.1, 1.0, 0.2)}
OFF = {'Sinaleiro - exame': (0.015, 0.06, 0.02), 'Sinaleiro - degelo': (0.10, 0.06, 0.01), 'Sinaleiro - alarme': (0.10, 0.015, 0.01)}
sc = bpy.context.scene
src = {o.name: o for o in bpy.data.objects if o.type == 'MESH' and not o.name.endswith('_fonte')}
for o in list(sc.objects):
    o.hide_render = True
col = bpy.data.collections.new('previa')
sc.collection.children.link(col)


def put(asset, x, y, z, yaw=0.0, roll=0.0, mat=None):
    o = src[asset].copy()
    o.hide_render = False
    o.location = (x, y, z)
    o.rotation_euler = (0, math.radians(roll), math.radians(yaw))
    if mat:
        o.data = o.data.copy()
        o.data.materials.clear()
        o.data.materials.append(mat)
    col.objects.link(o)
    return o


def emit(name, rgb, strength):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = (*rgb, 1)
    b.inputs['Roughness'].default_value = 0.2
    b.inputs['Emission Color'].default_value = (*rgb, 1)
    b.inputs['Emission Strength'].default_value = strength
    return m


def textured(name, path, emission=0.0, rect=None, atlas=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    t = nt.nodes.new('ShaderNodeTexImage')
    t.image = bpy.data.images.load(path, check_existing=True)
    if rect:
        x, y, w, h = rect
        W, H = atlas
        mp = nt.nodes.new('ShaderNodeMapping')
        mp.inputs['Scale'].default_value = (w / W, h / H, 1)
        mp.inputs['Location'].default_value = (x / W, 1 - (y + h) / H, 0)
        uv = nt.nodes.new('ShaderNodeTexCoord')
        nt.links.new(uv.outputs['UV'], mp.inputs['Vector'])
        nt.links.new(mp.outputs['Vector'], t.inputs['Vector'])
    nt.links.new(t.outputs['Color'], b.inputs['Base Color'])
    b.inputs['Roughness'].default_value = 0.35
    if emission:
        nt.links.new(t.outputs['Color'], b.inputs['Emission Color'])
        b.inputs['Emission Strength'].default_value = emission
    return m


def quad(name, cx, y, cz, w, h, m, yaw, ox, oy, pitch=0.0):
    me = bpy.data.meshes.new(name)
    if pitch > 45:   # lying face up, its top towards the wall (+Y)
        co = [(cx - w / 2, y - h / 2, cz), (cx + w / 2, y - h / 2, cz), (cx + w / 2, y + h / 2, cz), (cx - w / 2, y + h / 2, cz)]
    else:
        co = [(cx - w / 2, y, cz - h / 2), (cx + w / 2, y, cz - h / 2), (cx + w / 2, y, cz + h / 2), (cx - w / 2, y, cz + h / 2)]
    me.from_pydata(co, [], [(0, 1, 2, 3)])
    me.uv_layers.new().data.foreach_set('uv', [0, 0, 1, 0, 1, 1, 0, 1])
    me.materials.append(m)
    o = bpy.data.objects.new(name, me)
    o.location = (ox, oy, 0)
    o.rotation_euler = (0, 0, math.radians(yaw))
    col.objects.link(o)


ET = json.load(open(ROOT + '/Assets/UnclaimedPrototype/Tecnica/Etiquetas.json'))
RECTS = {e['id']: (e['rect'], e['tamanho']) for e in ET['etiquetas']}
ATLAS_PNG = ROOT + '/Assets/Modelos/Tecnica/Texturas/Etiquetas_Tecnica.png'
VISOR = {'Visor - contingencia': 'Visor_Contingencia.png', 'Visor - camara fria': 'Visor_CamaraFria.png'}
for i, (asset, x, y, z, yaw) in enumerate(SPEC['objects']):
    put(asset, x, y, z, yaw)
    mj = ROOT + f'/Blender/Tecnica/revisao/montagem/{asset}.json'
    if not os.path.exists(mj):
        continue
    info = json.load(open(mj))
    for kind, name, dx, dz, dy in info.get('devices', []):
        put(kind, x + dx, y + dy, z + dz, yaw, roll=SEL.get(name, 0.0))
        if kind == 'Sinaleiro':
            rgb = LIT.get(name) or OFF.get(name)
            put('SinaleiroLente', x + dx, y + dy, z + dz, yaw, mat=emit(name, rgb, 1.5 if name in LIT else 0.0))
    for lid, dx, dz, dy, *pitch in info.get('labels', []):
        rect, (w, h) = RECTS[lid]
        pitch = pitch[0] if pitch else 0.0
        quad('et ' + lid, dx, dy - (0.0 if pitch > 45 else 0.0004), dz + (0.0004 if pitch > 45 else 0.0), w, h,
             textured('et ' + lid, ATLAS_PNG, rect=rect, atlas=ET['atlas']), yaw, x, y, pitch)
    for name, dx, dz, dy, (w, h) in info.get('displays', []):
        quad(name, dx, dy - 0.0002, dz, w, h, textured(name, ROOT + '/Assets/Modelos/Tecnica/Texturas/' + VISOR[name], emission=3.0), yaw, x, y)


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


plane('piso', [(-5, -4, 0), (5, -4, 0), (5, 1, 0), (-5, 1, 0)], mat('piso', (0.40, 0.37, 0.33), 0.6))
plane('parede', [(-5, 0.0, 0), (5, 0.0, 0), (5, 0.0, 3.2), (-5, 0.0, 3.2)], mat('parede', (0.62, 0.62, 0.60), 0.8))
L = bpy.data.lights.new('tubo', 'AREA')
L.shape, L.size, L.size_y = 'RECTANGLE', 1.2, 0.15
L.energy, L.color = SPEC.get('energy', 110), (1.0, 0.93, 0.84)
o = bpy.data.objects.new('tubo', L)
o.location = SPEC.get('light', (0.0, -1.4, 3.1))
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

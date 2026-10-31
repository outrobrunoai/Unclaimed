"""Corridor sign bodies (metres, Blender axes; Unity uses x, z, y): one mesh per sign in specs.json.

Each sign is ONE mesh with three slots, so no quad ever lies on top of another:
  0 frente    the printed face (UV 0..1 over the face, texture from sinais_vetoriais.py)
  1 borda     edge, bevel and back (plastic core)
  2 parafuso  fixing screws (orientation and safety signs; room plates are bonded)
Prefab origin = centre of the back (the wall plane, y = 0); the sign stands out towards -Y (Unity -Z).
Exit signs are light boxes: ABS frame around a recessed print.

Run: blender -b --factory-startup --python Blender/Circulacao/scripts/placas.py [-- Name ...]
or exec it in a live Blender (Blender MCP) and call build_all()."""
import bpy, bmesh, math, os, json, sys, types
from mathutils import Vector, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(globals().get('__file__', '.')), '../../..')) if '__file__' in globals() else '/mnt/dados/Projetos/Unity/Unclaimed'
SRC = ROOT + '/Blender/Circulacao'
OUT = ROOT + '/Assets/Modelos/Circulacao/Placas'
TEX = ROOT + '/Assets/UnclaimedPrototype/Circulacao/Sinais'
THICK = {'room': 0.003, 'orient': 0.004, 'safety': 0.0035, 'exit': 0.026}
EDGE = {'room': (0.78, 0.76, 0.70), 'orient': (0.20, 0.27, 0.23), 'safety': (0.55, 0.55, 0.52), 'exit': (0.82, 0.81, 0.76)}
SCREWS = ('orient', 'safety')


def rounded_rect(w, h, r, seg=5):
    pts = []
    for cx, cz, a0 in ((w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)):
        for i in range(seg + 1):
            a = math.radians(a0 + 90 * i / seg)
            pts.append((cx + r * math.cos(a), cz + r * math.sin(a)))
    return pts


def mat(name, rgb, rough=0.5, metal=0.0, image=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    bsdf.inputs['Roughness'].default_value = rough
    bsdf.inputs['Metallic'].default_value = metal
    if image and os.path.exists(image):
        tex = next((n for n in m.node_tree.nodes if n.type == 'TEX_IMAGE'), None) or m.node_tree.nodes.new('ShaderNodeTexImage')
        tex.image = bpy.data.images.load(image, check_existing=True)
        m.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    return m


def screw(bm, x, z, y0, sign):
    """Pan-head screw: low cylinder with a rounded cap, standing out of the face at y0 (towards -Y)."""
    head_r, head_h = 0.0032, 0.0016
    ring = []
    for i in range(14):
        a = 2 * math.pi * i / 14
        ring.append((math.cos(a) * head_r, math.sin(a) * head_r))
    base = [bm.verts.new((x + px, y0, z + pz)) for px, pz in ring]
    top = [bm.verts.new((x + px * 0.82, y0 - head_h, z + pz * 0.82)) for px, pz in ring]
    cap = bm.verts.new((x, y0 - head_h - 0.0004, z))
    faces = []
    for i in range(14):
        j = (i + 1) % 14
        faces.append(bm.faces.new((base[i], base[j], top[j], top[i])))
        faces.append(bm.faces.new((top[i], top[j], cap)))
    for f in faces:
        f.material_index = 2
    return faces


def plate_mesh(name, spec):
    kind, w, h = spec['kind'], spec['w'] / 1000.0, spec['h'] / 1000.0
    t = THICK[kind]
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new('UVMap')
    if kind == 'exit':
        return light_box(name, spec, bm, uv)
    outline = rounded_rect(w, h, 0.005 if kind != 'room' else 0.004)
    front = [bm.verts.new((x, -t, z)) for x, z in outline]
    back = [bm.verts.new((x, 0.0, z)) for x, z in outline]
    bm.faces.new(back)
    ff = bm.faces.new(list(reversed(front)))
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[i], front[j], back[j], back[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for f in bm.faces:
        f.material_index = 1
    # small bevel on the front edge only
    edges = [e for e in bm.edges if all(abs(v.co.y + t) < 1e-9 for v in e.verts)]
    res = bmesh.ops.bevel(bm, geom=edges, offset=0.0006, offset_type='OFFSET', profile_type='SUPERELLIPSE', segments=2, profile=0.5, affect='EDGES')
    for f in bm.faces:
        f.material_index = 0 if f.normal.y < -0.9999 and all(abs(v.co.y + t) < 1e-9 for v in f.verts) else 1
    for f in bm.faces:
        for lp in f.loops:
            lp[uv].uv = ((lp.vert.co.x + w / 2) / w, (lp.vert.co.z + h / 2) / h)
    if kind in SCREWS:
        inset = 0.016
        for sx in (-1, 1):
            for sz in (-1, 1):
                screw(bm, sx * (w / 2 - inset), sz * (h / 2 - inset), -t, sz)
    return finish(name, bm)


def light_box(name, spec, bm, uv):
    w, h, d = spec['w'] / 1000.0, spec['h'] / 1000.0, THICK['exit']
    fw = 0.009   # frame width around the print
    W, H = w + 2 * fw, h + 2 * fw
    outline = rounded_rect(W, H, 0.007)
    front = [bm.verts.new((x, -d, z)) for x, z in outline]
    back = [bm.verts.new((x, 0.0, z)) for x, z in outline]
    bm.faces.new(back)
    ff = bm.faces.new(list(reversed(front)))
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[i], front[j], back[j], back[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for f in bm.faces:
        f.material_index = 1
    # recessed print: inset the front face by the frame width, then push the inner face 3 mm back
    ret = bmesh.ops.inset_region(bm, faces=[ff], thickness=fw, depth=0.0, use_even_offset=True)
    inner = ff   # inset_region keeps the original face as the inner one; the ring is new
    for v in inner.verts:
        v.co.y += 0.003
    for f in bm.faces:
        f.material_index = 1
    inner.material_index = 0
    for lp in inner.loops:
        lp[uv].uv = ((lp.vert.co.x + w / 2) / w, (lp.vert.co.z + h / 2) / h)
    for sx in (-1, 1):
        screw(bm, sx * (W / 2 - 0.012), 0.0, -d, 1)
    return finish(name, bm)


def finish(name, bm):
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    return me


def build_all(only=None, export=True):
    specs = json.load(open(SRC + '/fontes_sinais/specs.json'))
    os.makedirs(OUT, exist_ok=True)
    stats = {}
    for i, (name, spec) in enumerate(specs.items()):
        if only and name not in only:
            continue
        old = bpy.data.objects.get('Placa_' + name)
        if old:
            bpy.data.objects.remove(old, do_unlink=True)
        me = plate_mesh('Placa_' + name, spec)
        ob = bpy.data.objects.new('Placa_' + name, me)
        bpy.context.scene.collection.objects.link(ob)
        kind = spec['kind']
        # (never me.materials.clear(): it resets every polygon's material_index to 0)
        me.materials.append(mat('frente ' + name, (1, 1, 1), 0.45, 0.0, TEX + '/' + name + '.png'))
        me.materials.append(mat('borda ' + kind, EDGE[kind], 0.55))
        me.materials.append(mat('parafuso', (0.72, 0.72, 0.70), 0.35, 1.0))
        for p in me.polygons:
            p.use_smooth = False
        ob.location = (i * 0.0, 0, 0)
        stats[name] = {'triangles': len(me.polygons), 'size': [round(v, 4) for v in ob.dimensions]}
        if export:
            export_fbx(ob, OUT + '/' + name + '.fbx')
    return stats


def export_fbx(ob, path):
    """Same convention as recepcao_modelo.export_fbx: metres, Y up / -Z forward, turned 180 deg about Z (the
    conversion maps Blender +X to Unity -X), slots kept so the prefab can assign print / edge / screw materials."""
    cp = ob.copy()
    cp.data = ob.data.copy()
    cp.name = cp.data.name = ob.name.replace('Placa_', '')
    cp.location = (0, 0, 0)
    bpy.context.scene.collection.objects.link(cp)
    cp.data.transform(Matrix.Rotation(math.pi, 4, 'Z'))
    bpy.ops.object.select_all(action='DESELECT')
    cp.select_set(True)
    bpy.context.view_layer.objects.active = cp
    try:
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
                                 apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                                 bake_space_transform=True, mesh_smooth_type='OFF', use_mesh_modifiers=True,
                                 use_triangles=True, use_tspace=False, add_leaf_bones=False, bake_anim=False,
                                 path_mode='STRIP', embed_textures=False, colors_type='NONE')
    finally:
        me = cp.data
        bpy.data.objects.remove(cp)
        bpy.data.meshes.remove(me)


if __name__ == '__main__':
    only = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else None
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    print(json.dumps(build_all(only), indent=1))
    bpy.ops.wm.save_as_mainfile(filepath=SRC + '/Placas.blend')

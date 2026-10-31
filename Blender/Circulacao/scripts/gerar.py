"""Corridor equipment (metres, Blender axes; Unity uses x, z, y): fire extinguisher (4 kg ABC, wall hung) with its
printed label and gauge dial as separate unbaked pieces, the break-glass manual call point with its printed front, and
the autonomous emergency light block. Wall plane at y = 0, the
fitting stands in front of it towards -Y (Unity prefabs face -Z).

Run: blender -b --factory-startup --threads 8 --python Blender/Circulacao/scripts/gerar.py [-- Asset ...]"""
import bpy, bmesh, math, os, json, types, sys
from mathutils import Vector, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../..'))
SRC = ROOT + '/Blender/Circulacao'
OUT = ROOT + '/Assets/Modelos/Circulacao'
ONLY = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else None
if ONLY and os.path.exists(SRC + '/Circulacao.blend'):
    bpy.ops.wm.open_mainfile(filepath=SRC + '/Circulacao.blend')


def load(rel):
    p = ROOT + '/' + rel
    ns = {'__name__': 'circ_module', '__file__': p}
    exec(compile(open(p, encoding='utf-8').read(), p, 'exec'), ns)
    return types.SimpleNamespace(**ns)


R = load('Blender/Recepcao/scripts/recepcao_modelo.py')
GN = load('Blender/MesaAutopsia/scripts/mesa_materiais.py').G
B = load('Blender/MesaAutopsia/scripts/mesa_bake.py')
M = load('Blender/Recepcao/scripts/recepcao_materiais.py')
M.metal.__globals__['G'] = GN
box, cyl, tube, extrude = R.box, R.cyl, R.tube, R.extrude


def enamel(name, rgb, rough=0.38, kick=0.18):
    return M.laminado(name, rgb, kick=kick, rough=rough)


def mats_for(asset):
    inox = M.metal('Inox escovado - corredor', (0.74, 0.74, 0.73), 0.30)
    aco = M.metal('Aco pintado preto - corredor', (0.07, 0.07, 0.075), 0.42)
    cromo = M.metal('Latao cromado - extintor', (0.70, 0.70, 0.68), 0.26)
    borr = M.borracha('Borracha - corredor')
    if asset == 'Extintor':
        return {'vermelho': enamel('Esmalte vermelho - extintor', (0.50, 0.035, 0.03), 0.30, 0.2), 'inox': inox, 'aco': aco,
                'cromo': cromo, 'borracha': borr, 'plastico': enamel('Plastico preto - suporte', (0.035, 0.035, 0.037), 0.5, 0.1),
                'cinza': enamel('Pintura cinza - suporte', (0.34, 0.35, 0.34), 0.45, 0.2)}
    if asset in ('ExtintorEtiqueta', 'ExtintorMostrador', 'AcionadorFrente'):
        return {'impresso': M.tela('impresso')}
    if asset == 'Acionador':
        return {'vermelho': enamel('Plastico vermelho - acionador', (0.40, 0.035, 0.03), 0.40, 0.12), 'aco': aco, 'inox': inox,
                'preto': enamel('Plastico preto - acionador', (0.03, 0.03, 0.032), 0.5, 0.1)}
    if asset == 'BlocoEmergencia':
        return {'abs': enamel('ABS marfim - bloco autonomo', (0.80, 0.79, 0.74), 0.46, 0.1), 'lente': M.tela('Lente do farol'),
                'inox': inox, 'aco': aco, 'borracha': borr,
                'led': enamel('LED indicador verde', (0.08, 0.40, 0.12), 0.2, 0.0), 'cinza': enamel('Cinza - bloco autonomo', (0.22, 0.23, 0.23), 0.5, 0.1)}
    raise KeyError(asset)


# ---------------------------------------------------------------- Extintor
# Portable dry-powder (ABC) 4 kg extinguisher on a wall bracket, from the references in
# docs/revisao-placas-equipamentos/referencias.md: drawn-steel shell with a black plastic base cup, red cast head with
# gauge, fixed grip and trigger lever, safety pin and ring, black hose clipped down the side with a metal nozzle, flat
# printed label, one black plastic strap bracket with its wall plate (no metal bands across the label).
# Axis at x = 0, y = YC (the shell stands 20 mm off the wall), base at z = BASE.

EX = dict(r=0.072, base=0.92, yc=-0.092)


def extintor():
    r, z0, yc = EX['r'], EX['base'], EX['yc']
    X = Matrix.Translation((0.0, yc, z0))
    # Shell: rolled bottom chime, straight wall, shoulder dome, neck collar.
    R.lathe('Corpo', 'vermelho', [(0.0, 0.006), (0.060, 0.006), (0.068, 0.010), (r - 0.0005, 0.022), (r, 0.040), (r, 0.236),
                                   (r - 0.0015, 0.252), (0.066, 0.272), (0.054, 0.292), (0.040, 0.306), (0.0295, 0.314),
                                   (0.028, 0.330), (0.0, 0.330)], seg=48, r=0.0012, matrix=X)
    R.lathe('Base de plastico', 'plastico', [(r + 0.0018, 0.0), (r + 0.0018, 0.032), (r - 0.002, 0.034), (r - 0.002, 0.012),
                                             (0.062, 0.004), (0.062, 0.0)], seg=48, r=0.001, matrix=X)
    # Strap bracket: black plastic ring under the label, two lugs to the steel wall plate, front latch.
    zb = 0.082
    R.lathe('Cinta do suporte', 'plastico', [(r + 0.0006, zb), (r + 0.0050, zb), (r + 0.0050, zb + 0.034), (r + 0.0006, zb + 0.034)],
            seg=48, r=0.0006, matrix=X)
    box('Placa de parede', 'cinza', -0.036, -0.003, z0 + zb - 0.016, 0.036, 0.0, z0 + zb + 0.050, r=0.001, seg=1)
    for sx in (-1, 1):
        box(f'Braco do suporte {sx:+d}', 'plastico', sx * 0.024 - 0.0045, yc + r * 0.55, z0 + zb + 0.004, sx * 0.024 + 0.0045, -0.003, z0 + zb + 0.030, r=0.0008, seg=1)
    box('Trava da cinta', 'plastico', -0.012, yc - r - 0.012, z0 + zb + 0.006, 0.012, yc - r + 0.001, z0 + zb + 0.028, r=0.002, seg=2)
    for dz in (-0.006, 0.040):
        cyl(f'Parafuso {dz}', 'inox', (0.0, -0.0045, z0 + zb + dz + 0.002), 0.0045, 0.003, axis=(0, 1, 0), seg=12, r=0.0006)
    # Head: collar, cast valve body, gauge, grips, pin.
    zt = 0.330
    cyl('Colar da valvula', 'inox', (0.0, yc, z0 + zt + 0.006), 0.033, 0.012, seg=32, r=0.0015)
    box('Corpo da valvula', 'vermelho', -0.034, yc - 0.040, z0 + zt + 0.012, 0.034, yc + 0.034, z0 + zt + 0.066, r=0.012, seg=4)
    box('Saida da mangueira', 'cromo', -0.052, yc - 0.012, z0 + zt + 0.036, -0.030, yc + 0.012, z0 + zt + 0.056, r=0.003, seg=2)
    cyl('Manometro - caixa', 'inox', (0.0, yc - 0.042, z0 + zt + 0.036), 0.0215, 0.016, axis=(0, 1, 0), seg=40, r=0.0025)
    cyl('Manometro - aro', 'cromo', (0.0, yc - 0.0508, z0 + zt + 0.036), 0.0222, 0.0035, axis=(0, 1, 0), seg=40, r=0.0007)
    # Fixed carry grip (arched strap towards +x) and trigger lever below it, both flat stamped steel 12 mm wide.
    grip = [(0.006, z0 + zt + 0.066), (0.030, z0 + zt + 0.078), (0.070, z0 + zt + 0.074), (0.092, z0 + zt + 0.062),
            (0.090, z0 + zt + 0.058), (0.068, z0 + zt + 0.066), (0.030, z0 + zt + 0.069), (0.004, z0 + zt + 0.060)]
    R.extrude('Punho fixo', 'inox', grip, 'XZ', yc - 0.006, yc + 0.006, r=0.0007, seg=1)
    lever = [(0.010, z0 + zt + 0.046), (0.040, z0 + zt + 0.040), (0.074, z0 + zt + 0.028), (0.096, z0 + zt + 0.014),
             (0.092, z0 + zt + 0.010), (0.070, z0 + zt + 0.022), (0.038, z0 + zt + 0.033), (0.008, z0 + zt + 0.038)]
    R.extrude('Gatilho', 'inox', lever, 'XZ', yc - 0.006, yc + 0.006, r=0.0007, seg=1)
    cyl('Eixo do gatilho', 'inox', (0.012, yc, z0 + zt + 0.043), 0.0035, 0.020, axis=(0, 1, 0), seg=12, r=0.0004)
    # Safety pin through the head with its ring (in front of the valve).
    cyl('Pino', 'cromo', (0.034, yc - 0.012, z0 + zt + 0.055), 0.0022, 0.040, axis=(0, 1, 0), seg=10, r=0.0003)
    ring = [(0.034 + 0.0125 * math.cos(a), yc - 0.036, z0 + zt + 0.055 + 0.0125 * math.sin(a)) for a in [i * math.tau / 14 for i in range(15)]]
    tube('Argola do pino', 'cromo', ring, 0.0020, fillet=0.0, res=2)
    # Hose: from the head's left outlet over the shoulder and down the left side to a clip; metal nozzle at the end.
    hx = -(r + 0.014)
    hose = [(-0.050, yc, z0 + zt + 0.046), (-0.074, yc, z0 + zt + 0.050), (-0.098, yc, z0 + zt + 0.030), (hx - 0.012, yc, z0 + 0.300),
            (hx - 0.012, yc, z0 + 0.230), (hx - 0.010, yc, z0 + 0.170), (hx - 0.004, yc, z0 + 0.120)]
    tube('Mangueira', 'aco', hose, 0.0085, fillet=0.040, res=3)
    cyl('Engate da mangueira', 'cromo', (-0.050, yc, z0 + zt + 0.046), 0.0105, 0.016, axis=(1, 0, 0), seg=18, r=0.0012)
    cyl('Bico', 'inox', (hx - 0.004, yc, z0 + 0.100), 0.0105, 0.044, axis=(0, 0, -1), seg=18, r=0.0015)
    box('Presilha da mangueira', 'plastico', hx - 0.016, yc - 0.020, z0 + 0.136, hx + 0.012, yc + 0.020, z0 + 0.166, r=0.003, seg=2)


def extintor_etiqueta():
    """Printed label on the shell, 134 x 130 mm, 107 degrees of the cylinder, seen from -Y; u grows with +x."""
    r, z0, yc = EX['r'], EX['base'], EX['yc']
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new('UVMap')
    n = 16
    half = (0.134 / r) / 2     # arc length 134 mm
    za, zb = z0 + 0.105, z0 + 0.235
    rows = []
    for zz, v in ((za, 0.0), (zb, 1.0)):
        ring = []
        for i in range(n + 1):
            a = -half + 2 * half * i / n
            ring.append((bm.verts.new(((r + 0.0006) * math.sin(a), yc - (r + 0.0006) * math.cos(a), zz)), i / n, v))
        rows.append(ring)
    for i in range(n):
        vs = [rows[0][i], rows[0][i + 1], rows[1][i + 1], rows[1][i]]
        f = bm.faces.new([v[0] for v in vs])
        for lp, v in zip(f.loops, vs):
            lp[uv].uv = (v[1], v[2])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for f in bm.faces:       # the shell faces -Y at the label: normals must point away from the axis
        c = f.calc_center_median()
        if (Vector((c.x, c.y - yc, 0)) @ Vector((f.normal.x, f.normal.y, 0))) < 0:
            f.normal_flip()
    me = bpy.data.meshes.new('Etiqueta')
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new('Etiqueta', me)
    R._coll(R._current['asset']).objects.link(ob)
    ob['mat'] = 'impresso'


def extintor_mostrador():
    """Gauge dial disc (printed), flush in the gauge case; UV = the disc's bounding square."""
    r, z0, yc = EX['r'], EX['base'], EX['yc']
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new('UVMap')
    cx, cy, cz, rad, n = 0.0, yc - 0.0529, z0 + 0.330 + 0.036, 0.0178, 32
    ctr = bm.verts.new((cx, cy, cz))
    ring = [bm.verts.new((cx + rad * math.cos(i * math.tau / n), cy, cz + rad * math.sin(i * math.tau / n))) for i in range(n)]
    for i in range(n):
        f = bm.faces.new((ctr, ring[(i + 1) % n], ring[i]))
        for lp in f.loops:
            lp[uv].uv = (0.5 + (lp.vert.co.x - cx) / (2 * rad), 0.5 + (lp.vert.co.z - cz) / (2 * rad))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for f in bm.faces:
        if f.normal.y > 0:
            f.normal_flip()
    me = bpy.data.meshes.new('Mostrador')
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new('Mostrador', me)
    R._coll(R._current['asset']).objects.link(ob)
    ob['mat'] = 'impresso'


# ---------------------------------------------------------------- Acionador manual (quebre o vidro)
# After the Kidde AM-013 / Intelbras AME 521 housings: square red surface box 98 x 98, stepped cover with a recessed
# printed front (pictogram, instruction window, break mark), raised window bezel, reset-key slot, cable entry blind.
# Centre of the back at the origin; stands out towards -Y.

def acionador():
    w, base_d, cov_d = 0.098, 0.026, 0.040
    box('Caixa', 'vermelho', -w / 2, -base_d, -w / 2, w / 2, 0.0, w / 2, r=0.006, seg=3)
    # Stepped cover (3 mm smaller all round), pocket 84 mm square and 4 mm deep: the printed front sits just proud of
    # the pocket floor (towards the viewer, which is -Y), so no two faces are coplanar.
    c = 0.046
    cover = box('Tampa', 'vermelho', -c, -cov_d, -c, c, -base_d + 0.001, c, r=0.0045, seg=3)
    cutter = box('__bolso', 'vermelho', -0.042, -cov_d - 0.002, -0.042, 0.042, -0.0345, 0.042, r=0)
    R.cut(cover, cutter)
    # Raised window bezel (1.6 mm) around the 66 x 36 mm window, on the printed front.
    fy0, fy1 = -0.0352, -0.0368
    wx, wz0, wz1, t = 0.033, -0.025, 0.011, 0.0016
    box('Moldura da janela - topo', 'vermelho', -wx - t, fy1, wz1, wx + t, fy0, wz1 + t, r=0, seg=1)
    box('Moldura da janela - base', 'vermelho', -wx - t, fy1, wz0 - t, wx + t, fy0, wz0, r=0, seg=1)
    box('Moldura da janela - esquerda', 'vermelho', -wx - t, fy1, wz0, -wx, fy0, wz1, r=0, seg=1)
    box('Moldura da janela - direita', 'vermelho', wx, fy1, wz0, wx + t, fy0, wz1, r=0, seg=1)
    # Reset-key slot in the lower cover rim.
    box('Fenda da chave', 'preto', -0.007, -cov_d - 0.0004, -0.0445, 0.007, -cov_d + 0.0008, -0.0425, r=0.0002, seg=1)


def acionador_frente():
    """The printed front (84 x 84 mm), 6 mm below the cover bezel, seen from -Y."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new('UVMap')
    h, y = 0.042, -0.0352
    vs = [bm.verts.new((-h, y, -h)), bm.verts.new((h, y, -h)), bm.verts.new((h, y, h)), bm.verts.new((-h, y, h))]
    f = bm.faces.new(vs)
    for lp in f.loops:
        lp[uv].uv = ((lp.vert.co.x + h) / (2 * h), (lp.vert.co.z + h) / (2 * h))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if f.normal.y > 0:
        f.normal_flip()
    me = bpy.data.meshes.new('Frente')
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new('Frente', me)
    R._coll(R._current['asset']).objects.link(ob)
    ob['mat'] = 'impresso'


# ---------------------------------------------------------------- Bloco autonomo de emergencia

def bloco_emergencia():
    w, h, d = 0.36, 0.13, 0.085
    z = 0.0
    box('Corpo', 'abs', -w / 2, -d, z, w / 2, 0.0, z + h, r=0.014, seg=3)
    box('Tampa frontal', 'abs', -w / 2 + 0.010, -d - 0.006, z + 0.010, w / 2 - 0.010, -d + 0.002, z + h - 0.010, r=0.006, seg=2)
    for i in range(7):   # moulded ventilation slots on the top edge
        x = -0.12 + i * 0.04
        box(f'Fenda {i}', 'cinza', x - 0.011, -d - 0.0062, z + h - 0.030, x + 0.011, -d - 0.0052, z + h - 0.024, r=0.0003, seg=1)
    # Two adjustable lamp heads on short swivel arms.
    for s in (-1, 1):
        x = s * 0.098
        box(f'Base do farol {s:+d}', 'cinza', x - 0.020, -d - 0.006, z + h / 2 - 0.020, x + 0.020, -d - 0.016, z + h / 2 + 0.020, r=0.004, seg=2)
        cyl(f'Braco do farol {s:+d}', 'inox', (x, -d - 0.030, z + h / 2), 0.005, 0.030, axis=(0, 1, 0), seg=14, r=0.001)
        cyl(f'Cabeca do farol {s:+d}', 'abs', (x, -d - 0.055, z + h / 2 + 0.004), 0.034, 0.050, axis=(0, 1, 0), seg=32, r=0.008)
        cyl(f'Aro do farol {s:+d}', 'cinza', (x, -d - 0.082, z + h / 2 + 0.004), 0.031, 0.006, axis=(0, 1, 0), seg=32, r=0.0015)
        cyl(f'Lente {s:+d}', 'lente', (x, -d - 0.0855, z + h / 2 + 0.004), 0.0275, 0.003, axis=(0, 1, 0), seg=32, r=0.001)
    # Centre: green charge LED and the test button.
    cyl('LED de carga', 'led', (0.0, -d - 0.0075, z + h * 0.66), 0.0055, 0.003, axis=(0, 1, 0), seg=16, r=0.0006)
    cyl('Botao de teste', 'cinza', (0.0, -d - 0.0095, z + h * 0.30), 0.0085, 0.006, axis=(0, 1, 0), seg=20, r=0.002)
    for s in (-1, 1):
        box(f'Fixacao {s:+d}', 'inox', s * (w / 2 - 0.022) - 0.011, 0.0, z + 0.04, s * (w / 2 - 0.022) + 0.011, 0.004, z + h - 0.04, r=0.001, seg=1)


BUILDERS = {'Extintor': extintor, 'ExtintorEtiqueta': extintor_etiqueta, 'ExtintorMostrador': extintor_mostrador, 'Acionador': acionador,
            'AcionadorFrente': acionador_frente, 'BlocoEmergencia': bloco_emergencia}
UNBAKED = ('ExtintorEtiqueta', 'ExtintorMostrador', 'AcionadorFrente')
SIZES = {'Extintor': 1024, 'Acionador': 512, 'BlocoEmergencia': 1024}


def main():
    os.makedirs(OUT + '/Texturas', exist_ok=True)
    os.makedirs(SRC + '/revisao', exist_ok=True)
    for o in list(bpy.data.objects):
        if not ONLY or any(o in list(R._coll(n).objects) or o.name == n for n in ONLY):
            bpy.data.objects.remove(o, do_unlink=True)
    stats = {}
    for i, (name, fn) in enumerate(BUILDERS.items()):
        if ONLY and name not in ONLY:
            continue
        R._current['asset'] = name
        fn()
        ob = R.assemble(name, mats_for(name))
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bmesh.ops.dissolve_degenerate(bm, dist=0.000001, edges=list(bm.edges))
        bmesh.ops.triangulate(bm, faces=list(bm.faces))
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(ob.data)
        bm.free()
        density = 0 if name in UNBAKED else R.layout_uvs(ob, SIZES[name], margin_px=8)
        stats[name] = {'triangles': len(ob.data.polygons), 'texelsPerMetre': density, 'size': [round(v, 4) for v in ob.dimensions]}
        R.export_fbx(ob, OUT + '/' + name + '.fbx', keep_slots=name in UNBAKED)
        ob.location.x = i * 1.2
    bpy.ops.wm.save_as_mainfile(filepath=SRC + '/Circulacao.blend')
    prev = json.load(open(SRC + '/revisao/malhas.json')) if ONLY and os.path.exists(SRC + '/revisao/malhas.json') else {}
    prev.update(stats)
    with open(SRC + '/revisao/malhas.json', 'w') as f:
        json.dump(prev, f, indent=2)
    for name in stats:
        if name in UNBAKED:
            continue
        ob = bpy.data.objects[name]
        pos = ob.location.copy()
        ob.location = (0, 0, 0)
        others = [o for o in bpy.context.scene.objects if o != ob and o.type == 'MESH']
        for o in others:
            o.hide_render = True
        B._image.__globals__['SIZE'] = SIZES[name]
        imgs, times = B.bake(ob, [s.material for s in ob.material_slots], ao_samples=32)
        B.write_unity_maps(imgs, OUT + '/Texturas', prefix=name)
        ob.location = pos
        for o in others:
            o.hide_render = False
        print('BAKED', name, stats[name], times, flush=True)
    bpy.ops.wm.save_as_mainfile(filepath=SRC + '/Circulacao.blend')


if __name__ == '__main__':
    main()

"""Technical room ("Sala tecnica"): main distribution board, the provisional-supply contingency panel of P02, the
cold-room control board, their door devices, the cable tray with its conduits and Mauro's maintenance bench.
Metres, Blender axes (Unity uses x, z, y); wall pieces have their origin on the wall plane at floor level, front towards
-Y (Unity prefabs face -Z). References and decisions: docs/revisao-tecnica/referencias.md.
Texts are atlas labels (etiquetas_tecnica.py) placed by Layout.json; every asset here bakes plain materials only.
Each built asset also writes Blender/Tecnica/revisao/montagem/<Asset>.json (devices, labels, displays in asset
coordinates) for layout_tecnica.py.

Run: blender -b --factory-startup --threads 8 --python Blender/Tecnica/scripts/gerar.py [-- Asset ...]"""
import bpy, bmesh, math, os, json, types, sys
from mathutils import Vector, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../..'))
SRC = ROOT + '/Blender/Tecnica'
OUT = ROOT + '/Assets/Modelos/Tecnica'
ONLY = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else None


def load(rel, **extra):
    p = ROOT + '/' + rel
    ns = {'__name__': 'tec_module', '__file__': p, **extra}
    exec(compile(open(p, encoding='utf-8').read(), p, 'exec'), ns)
    return types.SimpleNamespace(**ns)


R = load('Blender/Recepcao/scripts/recepcao_modelo.py')
GN = load('Blender/MesaAutopsia/scripts/mesa_materiais.py').G
B = load('Blender/MesaAutopsia/scripts/mesa_bake.py')
M = load('Blender/Recepcao/scripts/recepcao_materiais.py')
M.metal.__globals__['G'] = GN
F = load('Blender/Descanso/scripts/formas.py', R=R)
Q = load('Blender/Tecnica/scripts/pecas_quadros.py', R=R, F=F)
IN = load('Blender/Tecnica/scripts/pecas_infra.py', R=R, F=F, Q=Q)
BA = load('Blender/Tecnica/scripts/pecas_bancada.py', R=R, F=F)


# ---------------------------------------------------------------- materials

def chapa(name, rgb, D, latch=None, wear=1.0):
    """Electrostatic-painted sheet steel (RAL 7035 family): fine orange peel, rubbed folds, a hand-polished halo with
    grime around the latch, dust settled on the top faces and a few chips to dark steel on the door's latch edge."""
    m, g = M._new(name)
    p = g.obj()
    peel = g.noise(p, 900.0, 1.0, 0.5)
    cloud = g.noise(p, 3.0, 3.0, 0.55)
    base = g.color(g.math('ADD', M.mul(g, cloud, 0.7), M.mul(g, peel, 0.3)), tuple(c * 0.96 for c in rgb), tuple(c * 1.03 for c in rgb))
    rub = M.mul(g, M.edges(g, 0.004), 0.5 * wear)
    base = M.lerp(g, base, tuple(min(1.0, c * 1.12) for c in rgb), rub)
    geo = g.n('ShaderNodeNewGeometry')
    sep = g.n('ShaderNodeSeparateXYZ')
    g.link(geo.outputs['Normal'], sep.inputs[0])
    up = g.mrange(sep.outputs[2], 0.6, 0.95, smooth=True)
    dust = g.math('MULTIPLY', up, g.mrange(g.noise(p, 9.0, 3.0, 0.6), 0.25, 0.7))
    base = M.lerp(g, base, (0.36, 0.34, 0.31), M.mul(g, dust, 0.55 * wear))
    hand = chips = None
    if latch is not None:
        hand = g.math('MULTIPLY', M.spot(g, latch, 0.07), g.mrange(g.noise(p, 30.0, 3.0, 0.6), 0.3, 0.75))
        base = M.lerp(g, base, tuple(c * 0.62 for c in rgb), M.mul(g, hand, 0.45 * wear))
        chips = g.math('MULTIPLY', g.mrange(g.noise(p, 90.0, 2.0, 0.5), 0.76, 0.79), M.spot(g, latch, 0.05))
        chips = g.math('MULTIPLY', chips, M.edges(g, 0.003))
        base = M.lerp(g, base, (0.10, 0.10, 0.10), chips)
    r_ = g.math('ADD', 0.42, M.mul(g, dust, 0.35))
    if hand is not None:
        r_ = g.math('SUBTRACT', r_, M.mul(g, hand, 0.12))
    g.finish(base, r_, M.mul(g, chips, 0.6) if chips is not None else 0.0, M.mul(g, peel, 0.15), bump_strength=0.10, bump_dist=0.0003)
    return m


def plano(name, rgb, rough):
    m, g = M._new(name)
    g.finish(M.vec(g, rgb), rough, 0.0, None)
    return m


RAL7035 = (0.56, 0.575, 0.555)


def mats_for(asset):
    zinco = M.metal('Aco zincado - tecnica', (0.64, 0.64, 0.62), 0.34)
    comuns = {'zinco': zinco, 'zamac': M.metal('Zamac - fecho', (0.56, 0.56, 0.55), 0.30),
              'escuro': plano('Fresta escura', (0.015, 0.015, 0.016), 0.8), 'borracha': M.borracha('Borracha - tecnica'),
              'cromo': M.metal('Cromo - tecnica', (0.75, 0.75, 0.74), 0.18),
              'plastico_preto': M.plastico('Plastico preto - comando', (0.025, 0.025, 0.027), 0.42),
              'plastico_cinza': M.plastico('Plastico cinza - controlador', (0.20, 0.21, 0.21), 0.45),
              'borracha_cinza': M.plastico('Teclas de borracha - controlador', (0.10, 0.10, 0.105), 0.7, scuffed=False),
              'vidro_visor': plano('Janela do visor', (0.008, 0.009, 0.010), 0.06),
              'acrilico': plano('Acrilico - placa', (0.02, 0.02, 0.022), 0.15), 'laminado': plano('Cartao plastificado', (0.80, 0.80, 0.77), 0.2),
              'aluminio': M.metal('Rebite de aluminio', (0.70, 0.70, 0.69), 0.35), 'pvc': plano('PVC cristal - fundo', (0.70, 0.71, 0.70), 0.15),
              'pvc_claro': plano('PVC cristal - bolso', (0.42, 0.43, 0.43), 0.08), 'fita': plano('Fita adesiva transparente', (0.55, 0.53, 0.47), 0.2),
              'nylon': M.plastico('Nylon cinza - prensa-cabos', (0.30, 0.31, 0.31), 0.5), 'indice': plano('Indice branco', (0.80, 0.80, 0.78), 0.5),
              'latao': M.metal('Latao niquelado - chave', (0.66, 0.64, 0.58), 0.28), 'lente': plano('Lente', (0.5, 0.5, 0.5), 0.2)}
    if asset in Q.QUADROS:
        q = Q.QUADROS[asset]
        latch = (q['W'] / 2 - 0.035, -q['D'], q['z0'] + {'PainelContingencia': 0.36, 'QuadroCamaraFria': 0.20}.get(asset, q['H'] / 2))
        rgb = RAL7035 if asset != 'QuadroGeral' else tuple(c * 0.95 for c in RAL7035)
        return {**comuns, 'chapa': chapa('Chapa pintada - ' + asset, rgb, q['D'], latch, 1.3 if asset == 'QuadroGeral' else 1.0)}
    if asset == 'Infraestrutura':
        return {**comuns, 'calha': calha('Eletrocalha perfurada galvanizada'), 'eletroduto': M.metal('Eletroduto galvanizado', (0.55, 0.56, 0.55), 0.38),
                'cabo_pp': M.plastico('Cabo PP cinza', (0.20, 0.205, 0.21), 0.5, scuffed=False),
                'cabo_preto': M.plastico('Cabo preto', (0.02, 0.02, 0.022), 0.45, scuffed=False),
                'cabo_azul': M.plastico('Cabo azul', (0.03, 0.08, 0.30), 0.45, scuffed=False),
                'nylon_branco': M.plastico('Presilha de nylon', (0.72, 0.72, 0.68), 0.45, scuffed=False)}
    if asset == 'Bancada':
        return {**comuns, 'tubo': M.laminado('Esmalte cinza - bancada', (0.16, 0.18, 0.20), kick=0.18, rough=0.40),
                'chapa_bancada': M.laminado('Chapa cinza - bancada', (0.16, 0.18, 0.20), kick=0.0, rough=0.42),
                'mdf': mdf('MDF do tampo - bancada'), 'perfurado': perfurado('Painel perfurado - bancada', (0.40, 0.41, 0.40), Q_BA['pitch']),
                'punho_amarelo': M.plastico('Cabo amarelo - chave de fenda', (0.70, 0.46, 0.03), 0.38),
                'punho_vermelho': M.plastico('Punho vermelho - alicate', (0.45, 0.04, 0.03), 0.5),
                'punho_azul': M.plastico('Punho azul - alicate', (0.04, 0.12, 0.40), 0.5),
                'aco_ferramenta': M.metal('Aco forjado - ferramenta', (0.30, 0.30, 0.31), 0.42),
                'capa_multimetro': M.plastico('Capa de borracha amarela - multimetro', (0.72, 0.52, 0.04), 0.62),
                'plastico_escuro': M.plastico('Plastico escuro - multimetro', (0.035, 0.036, 0.038), 0.45),
                'borne_preto': M.plastico('Borne preto', (0.02, 0.02, 0.02), 0.4, scuffed=False),
                'borne_vermelho': M.plastico('Borne vermelho', (0.40, 0.02, 0.02), 0.4, scuffed=False),
                'cabo_preto': M.plastico('Cabo de prova preto', (0.02, 0.02, 0.022), 0.45, scuffed=False),
                'cabo_vermelho': M.plastico('Cabo de prova vermelho', (0.40, 0.02, 0.02), 0.45, scuffed=False),
                'lata': M.metal('Folha de flandres - lata de cafe', (0.62, 0.60, 0.56), 0.32),
                'parafusos': parafusos('Parafusos zincados'), 'buchas': M.plastico('Buchas de nylon', (0.55, 0.50, 0.30), 0.5, scuffed=False),
                'fita_isolante': plano('Fita isolante', (0.012, 0.012, 0.013), 0.25)}
    return comuns


def calha(name):
    """Galvanized perforated tray: spangle, dark slots 24 x 6 mm in rows along the tray."""
    m, g = M._new(name)
    p = g.obj()
    x, y, z = g.xyz()
    ax = g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('DIVIDE', x, 0.04)), 0.5))
    ac = g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('DIVIDE', g.math('ADD', y, z), 0.025)), 0.5))
    slot = g.math('MULTIPLY', g.mrange(ax, 0.31, 0.29), g.mrange(ac, 0.13, 0.11))
    spangle = g.voronoi(p, 60.0)
    base = g.color(spangle, (0.52, 0.53, 0.52), (0.66, 0.67, 0.66))
    base = M.lerp(g, base, (0.02, 0.02, 0.02), slot)
    g.finish(base, g.math('ADD', 0.33, M.mul(g, slot, 0.5)), g.math('SUBTRACT', 1.0, slot), M.mul(g, slot, -1.0), bump_strength=0.4, bump_dist=0.0008)
    return m


def perfurado(name, rgb, pitch):
    """Painted perforated steel tool board: round 6 mm holes on a square grid (dark), light rub round the hooks."""
    m, g = M._new(name)
    p = g.obj()
    x, y, z = g.xyz()
    ax = g.math('SUBTRACT', g.math('FRACT', g.math('DIVIDE', x, pitch)), 0.5)
    az = g.math('SUBTRACT', g.math('FRACT', g.math('DIVIDE', z, pitch)), 0.5)
    r2 = g.math('ADD', g.math('MULTIPLY', ax, ax), g.math('MULTIPLY', az, az))
    hole = g.mrange(r2, 0.016, 0.012, smooth=True)
    base = g.color(g.noise(p, 4.0, 3.0, 0.5), tuple(c * 0.95 for c in rgb), tuple(c * 1.04 for c in rgb))
    base = M.lerp(g, base, (0.03, 0.03, 0.03), hole)
    g.finish(base, g.math('ADD', 0.45, M.mul(g, hole, 0.4)), 0.0, M.mul(g, hole, -1.0), bump_strength=0.3, bump_dist=0.0006)
    return m


def mdf(name):
    """Varnished MDF top: fine fibre noise, front edge rubbed lighter, a few dark marks where tools are set down."""
    m, g = M._new(name)
    p = g.obj()
    x, y, z = g.xyz()
    base = g.color(g.noise(g.coords(scale=(40.0, 40.0, 40.0)), 1.0, 4.0, 0.6), (0.24, 0.17, 0.10), (0.31, 0.22, 0.13))
    front = g.mrange(y, -0.58, -0.62, smooth=True)
    base = M.lerp(g, base, (0.40, 0.31, 0.20), M.mul(g, g.math('MULTIPLY', front, M.edges(g, 0.006)), 0.8))
    marks = g.math('MULTIPLY', g.mrange(g.noise(p, 12.0, 3.0, 0.6), 0.66, 0.72), g.mrange(y, -0.15, -0.30))
    base = M.lerp(g, base, (0.10, 0.08, 0.06), M.mul(g, marks, 0.6))
    g.finish(base, g.math('ADD', 0.48, M.mul(g, marks, 0.2)), 0.0, g.noise(p, 400.0, 1.0, 0.5), bump_strength=0.05, bump_dist=0.0002)
    return m


def parafusos(name):
    m, g = M._new(name)
    p = g.obj()
    base = g.color(g.noise(p, 300.0, 2.0, 0.6), (0.18, 0.18, 0.17), (0.62, 0.62, 0.58))
    g.finish(base, 0.35, 1.0, g.noise(p, 600.0, 1.0, 0.5), bump_strength=0.6, bump_dist=0.0008)
    return m


Q_BA = BA.QUADRO

BUILDERS = {'PainelContingencia': Q.painel_contingencia, 'QuadroGeral': Q.quadro_geral, 'QuadroCamaraFria': Q.quadro_camara_fria,
            'Seletor': Q.seletor, 'SeletorChave': Q.seletor_chave, 'Sinaleiro': Q.sinaleiro, 'SinaleiroLente': Q.sinaleiro_lente,
            'Infraestrutura': IN.infraestrutura, 'Bancada': BA.bancada}
UNBAKED = {'SinaleiroLente'}
WALL = {'PainelContingencia', 'QuadroGeral', 'QuadroCamaraFria', 'Infraestrutura', 'Bancada'}
SIZES = {'PainelContingencia': 2048, 'QuadroGeral': 2048, 'QuadroCamaraFria': 1024, 'Seletor': 256, 'SeletorChave': 256,
         'Sinaleiro': 256, 'SinaleiroLente': 64, 'Infraestrutura': 4096, 'Bancada': 4096}


def wall_plane():
    """Temporary wall behind wall-mounted assets so the AO bake darkens their backs and tabs (not exported)."""
    me = bpy.data.meshes.new('__bake_wall')
    me.from_pydata([(-6, 0.0004, -1), (6, 0.0004, -1), (6, 0.0004, 4), (-6, 0.0004, 4)], [], [(0, 1, 2, 3)])
    ob = bpy.data.objects.new('__bake_wall', me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def main():
    os.makedirs(OUT + '/Texturas', exist_ok=True)
    os.makedirs(SRC + '/revisao/montagem', exist_ok=True)
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    stats = {}
    for i, (name, fn) in enumerate(BUILDERS.items()):
        if ONLY and name not in ONLY:
            continue
        print('BUILD', name, flush=True)
        R._current['asset'] = name
        info = fn()
        if info:
            with open(f'{SRC}/revisao/montagem/{name}.json', 'w') as f:
                json.dump(info, f, indent=1)
        ob = R.assemble(name, mats_for(name))
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bmesh.ops.dissolve_degenerate(bm, dist=0.000001, edges=list(bm.edges))
        bmesh.ops.triangulate(bm, faces=list(bm.faces))
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_area() < 2e-9], context='FACES')
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(ob.data)
        bm.free()
        for l in list(ob.data.uv_layers):   # primitives arrive with their own 'UVMap'
            ob.data.uv_layers.remove(l)
        lay = ob.data.uv_layers.new(name='UVMap')
        ob.data.uv_layers.active = lay
        lay.active_render = True
        density = R.layout_uvs(ob, SIZES[name], margin_px=4 if SIZES[name] <= 256 else 8)
        stats[name] = {'triangles': len(ob.data.polygons), 'texelsPerMetre': density, 'size': [round(v, 4) for v in ob.dimensions]}
        if not os.environ.get('NO_BAKE') and name not in UNBAKED:
            others = [o for o in bpy.context.scene.objects if o != ob and o.type == 'MESH']
            for o in others:
                o.hide_render = True
            wall = wall_plane() if name in WALL else None
            B._image.__globals__['SIZE'] = SIZES[name]
            imgs, times = B.bake(ob, [s.material for s in ob.material_slots], ground=name not in WALL, ao_samples=32)
            B.write_unity_maps(imgs, OUT + '/Texturas', prefix=name)
            print('BAKED', name, stats[name], times, flush=True)
            if wall:
                bpy.data.objects.remove(wall)
            for o in others:
                o.hide_render = False
        R.export_fbx(ob, OUT + '/' + name + '.fbx')
        ob.location.x = i * 1.0
    bpy.ops.wm.save_as_mainfile(filepath=SRC + '/Tecnica.blend')
    prev = json.load(open(SRC + '/revisao/malhas.json')) if ONLY and os.path.exists(SRC + '/revisao/malhas.json') else {}
    prev.update(stats)
    with open(SRC + '/revisao/malhas.json', 'w') as f:
        json.dump(prev, f, indent=2)


if __name__ == '__main__':
    main()

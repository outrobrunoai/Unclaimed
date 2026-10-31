"""Storage room ("Deposito"): steel shelving, shipping boxes, retail packaging of the consumables, plastic boxes and bins,
pallet, platform cart, formalin jugs on a spill tray, disposable sheet rolls and Mauro's adapted-handle screwdriver.
Metres, Blender axes (Unity uses x, z, y); origin on the floor (bottom) at the centre of the footprint, front towards -Y
(Unity prefabs face -Z). References and decisions: docs/revisao-deposito/referencias.md.
Printed assets are painted by impressos_deposito.py onto a net read through the 'Impressao' UV layer and baked with the
rest into the asset's atlas; only the atlas UV is exported.

Run: blender -b --factory-startup --threads 8 --python Blender/Deposito/scripts/gerar.py [-- Asset ...]"""
import bpy, bmesh, math, os, json, types, sys
from mathutils import Vector, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../..'))
SRC = ROOT + '/Blender/Deposito'
OUT = ROOT + '/Assets/Modelos/Deposito'
IMPRESSOS = SRC + '/impressos'
ONLY = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else None


def load(rel, **extra):
    p = ROOT + '/' + rel
    ns = {'__name__': 'dep_module', '__file__': p, **extra}
    exec(compile(open(p, encoding='utf-8').read(), p, 'exec'), ns)
    return types.SimpleNamespace(**ns)


R = load('Blender/Recepcao/scripts/recepcao_modelo.py')
GN = load('Blender/MesaAutopsia/scripts/mesa_materiais.py').G
B = load('Blender/MesaAutopsia/scripts/mesa_bake.py')
M = load('Blender/Recepcao/scripts/recepcao_materiais.py')
M.metal.__globals__['G'] = GN
I = load('Blender/Deposito/scripts/impressao.py')
P = load('Blender/Deposito/scripts/impressos_deposito.py')
PE = load('Blender/Deposito/scripts/pecas_estante.py', R=R)
PX = load('Blender/Deposito/scripts/pecas_caixas.py', R=R, I=I, P=P, IMPRESSOS=IMPRESSOS)
F = load('Blender/Descanso/scripts/formas.py', R=R)
PD = load('Blender/Deposito/scripts/pecas_diversos.py', R=R, F=F, I=I, P=P, PE=PE, IMPRESSOS=IMPRESSOS)


# ---------------------------------------------------------------- materials

def chapa(name, rgb, D, grip_z=(0.84, 1.21, 1.58)):
    """Epoxy-painted sheet steel: fine orange peel, lighter rubbed folds, hand grime on the front folds at reach height,
    scuffs and a few chips to dark steel low on the uprights (boxes and the cart knock them)."""
    m, g = M._new(name)
    p = g.obj()
    peel = g.noise(p, 900.0, 1.0, 0.5)
    cloud = g.noise(p, 4.0, 3.0, 0.55)
    base = g.color(g.math('ADD', M.mul(g, cloud, 0.7), M.mul(g, peel, 0.3)), tuple(c * 0.95 for c in rgb), tuple(c * 1.04 for c in rgb))
    x, y, z = g.xyz()
    # thin sheet: AO edge detection would mark every face, so wear is placed by position instead
    front = g.mrange(y, -D / 2 + 0.02, -D / 2 - 0.002, smooth=True)
    grime = None
    for gz in grip_z:
        band = g.mrange(g.math('ABSOLUTE', g.math('SUBTRACT', z, gz - 0.015)), 0.03, 0.0, smooth=True)
        grime = band if grime is None else g.math('ADD', grime, band, clamp=True)
    grime = g.math('MULTIPLY', g.math('MULTIPLY', grime, front), g.mrange(g.noise(p, 14.0, 3.0, 0.6), 0.35, 0.75))
    lw = M.low(g, 0.30)
    sc = g.math('MULTIPLY', M.scuffs(g, 0.60), lw)
    # chips to dark steel only where boxes and the cart knock: the low 0.3 m and the front folds
    knock = g.math('ADD', lw, M.mul(g, front, 0.5), clamp=True)
    chips = g.math('MULTIPLY', g.mrange(g.noise(p, 70.0, 2.0, 0.5), 0.74, 0.77), knock)
    dirt = g.math('ADD', M.mul(g, grime, 0.45), M.mul(g, sc, 0.35), clamp=True)
    base = M.lerp(g, base, tuple(c * 0.55 for c in rgb), dirt)
    base = M.lerp(g, base, (0.11, 0.105, 0.10), chips)
    r_ = g.math('ADD', 0.46, M.mul(g, dirt, 0.25))
    g.finish(base, r_, M.mul(g, chips, 0.6), g.math('SUBTRACT', M.mul(g, peel, 0.15), M.mul(g, chips, 0.3)), bump_strength=0.10, bump_dist=0.0003)
    return m


def impresso(name, asset, rough_board, gloss_tape=0.30):
    """Print of a packaging net, read through the 'Impressao' UV; tape glossy and a hair raised, ink slightly smoother."""
    m, g = M._new(name)
    uvn = g.n('ShaderNodeUVMap', uv_map=I.LAYER)
    tex = g.n('ShaderNodeTexImage', interpolation='Cubic')
    tex.image = bpy.data.images.load(IMPRESSOS + '/' + asset + '.png', check_existing=False)
    msk = g.n('ShaderNodeTexImage', interpolation='Linear')
    msk.image = bpy.data.images.load(IMPRESSOS + '/' + asset + '_mask.png', check_existing=False)
    msk.image.colorspace_settings.name = 'Non-Color'
    g.link(uvn.outputs[0], tex.inputs['Vector'])
    g.link(uvn.outputs[0], msk.inputs['Vector'])
    sep = g.n('ShaderNodeSeparateColor')
    g.link(msk.outputs['Color'], sep.inputs[0])
    tape, paper, ink = sep.outputs[0], sep.outputs[1], sep.outputs[2]
    p = g.obj()
    r_ = g.math('SUBTRACT', rough_board, M.mul(g, tape, rough_board - gloss_tape))
    r_ = g.math('SUBTRACT', r_, M.mul(g, paper, 0.15))
    r_ = g.math('SUBTRACT', r_, M.mul(g, ink, 0.10))
    h = g.math('ADD', M.mul(g, tape, 0.5), M.mul(g, paper, 0.8))
    h = g.math('ADD', h, M.mul(g, g.noise(p, 700.0, 1.0, 0.5), 0.15))
    g.finish(tex.outputs['Color'], r_, 0.0, h, bump_strength=0.12, bump_dist=0.0002)
    return m


def mats_for(asset):
    zinco = M.metal('Aco zincado - deposito', (0.64, 0.64, 0.62), 0.34)
    if asset.startswith('Estante'):
        D = {'EstanteRasa': 0.40, 'EstanteEstreita': 0.30, 'EstanteFunda': 0.60}[asset]
        return {'aco': chapa('Chapa pintada cinza - ' + asset, (0.34, 0.35, 0.34), D), 'parafuso': zinco,
                'sapata': M.plastico('Sapata preta - estante', (0.03, 0.03, 0.032), 0.6)}
    if asset in PX.CAIXAS or asset == 'CaixaAberta':
        return {'papelao': impresso('Papelao - ' + asset, asset, 0.90)}
    if asset in PX.PRODUTOS:
        return {'cartao': impresso('Cartao - ' + asset, asset, 0.55, 0.3)}
    borr = M.borracha('Borracha - deposito')
    if asset == 'CaixaOrganizadora':
        return {'plastico': M.plastico('Polipropileno leitoso - caixa', (0.70, 0.70, 0.67), 0.36),
                'trava': M.plastico('Trava azul - caixa', (0.08, 0.22, 0.52), 0.40)}
    if asset == 'Bin':
        return {'bin': M.plastico('Polipropileno azul - bin', (0.045, 0.16, 0.44), 0.42)}
    if asset == 'Estrado':
        return {'pead': M.laminado('PEAD preto - estrado', (0.05, 0.05, 0.055), kick=0.10, rough=0.62)}
    if asset == 'Carrinho':
        return {'azul': M.laminado('Esmalte azul - carrinho', (0.05, 0.14, 0.36), kick=0.12, rough=0.42,
                                   grime_spots=(((0.0, 0.48, 0.93), 0.20),)),
                'borracha': borr, 'zinco': zinco, 'cubo': M.plastico('Cubo cinza - roda', (0.30, 0.31, 0.31), 0.5), 'manta': manta('Manta de borracha - carrinho')}
    if asset == 'GalaoFormol':
        return {'pead': impresso('PEAD natural - galao', 'GalaoFormol', 0.45, 0.35), 'tampa': M.plastico('Tampa azul - galao', (0.08, 0.20, 0.50), 0.4)}
    if asset == 'Bandeja':
        return {'pead': M.laminado('PEAD preto - bandeja', (0.04, 0.04, 0.045), kick=0.05, rough=0.55)}
    if asset == 'RoloLencol':
        return {'papel': impresso('Papel - lencol', 'RoloLencol', 0.85)}
    if asset == 'Ferramenta':
        return {'cabo': M.plastico('Cabo amarelo - chave', (0.72, 0.50, 0.04), 0.38), 'borracha': borr, 'cromo': M.metal('Cromo - chave', (0.76, 0.76, 0.75), 0.16),
                'etiqueta': impresso('Cartao - etiqueta de reposicao', 'Ferramenta', 0.85), 'barbante': M.papel('Barbante', (0.66, 0.60, 0.48))}
    raise KeyError(asset)


def manta(name):
    """Ribbed black rubber mat: ribs along the cart, worn smoother in the middle where boxes slide."""
    m, g = M._new(name)
    x, y, z = g.xyz()
    rib = g.mrange(g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('DIVIDE', x, 0.008)), 0.5)), 0.15, 0.30, smooth=True)
    use = g.radial(0.0, 0.0, 0.10, 0.40)
    base = M.lerp(g, M.vec(g, (0.035, 0.035, 0.037)), (0.06, 0.06, 0.062), M.mul(g, use, 0.6))
    g.finish(base, g.math('SUBTRACT', 0.78, M.mul(g, use, 0.2)), 0.0, rib, bump_strength=0.5, bump_dist=0.0008)
    return m


BUILDERS = {'EstanteRasa': PE.estante_rasa, 'EstanteEstreita': PE.estante_estreita, 'EstanteFunda': PE.estante_funda,
            'CaixaAberta': PX.aberta, 'CaixaOrganizadora': PD.caixa_organizadora, 'Bin': PD.bin_plastico, 'Estrado': PD.estrado,
            'Carrinho': PD.carrinho, 'GalaoFormol': PD.galao, 'Bandeja': PD.bandeja, 'RoloLencol': PD.rolo_lencol, 'Ferramenta': PD.ferramenta}
for _a in PX.CAIXAS:
    BUILDERS[_a] = (lambda a: (lambda: PX.shipping(a)))(_a)
for _a in PX.PRODUTOS:
    BUILDERS[_a] = (lambda a: (lambda: PX.retail(a)))(_a)

SIZES = {'EstanteRasa': 2048, 'EstanteEstreita': 2048, 'EstanteFunda': 2048, 'CaixaAberta': 2048}
SIZES.update({a: (2048 if PX.SIZES[s[0]][0] > 0.35 else 1024) for a, s in PX.CAIXAS.items()})
SIZES.update({a: 512 for a in PX.PRODUTOS})
SIZES.update({'EstanteRasa': 4096, 'EstanteEstreita': 4096, 'EstanteFunda': 4096, 'CaixaOrganizadora': 1024, 'Bin': 512, 'Estrado': 2048,
              'Carrinho': 2048, 'GalaoFormol': 1024, 'Bandeja': 1024, 'RoloLencol': 1024, 'Ferramenta': 512})


def main():
    os.makedirs(OUT + '/Texturas', exist_ok=True)
    os.makedirs(SRC + '/revisao', exist_ok=True)
    os.makedirs(IMPRESSOS, exist_ok=True)
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    stats = {}
    for i, (name, fn) in enumerate(BUILDERS.items()):
        if ONLY and name not in ONLY:
            continue
        print('BUILD', name, flush=True)
        R._current['asset'] = name
        fn()
        ob = R.assemble(name, mats_for(name))
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bmesh.ops.dissolve_degenerate(bm, dist=0.000001, edges=list(bm.edges))
        bmesh.ops.triangulate(bm, faces=list(bm.faces))
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_area() < 2e-9], context='FACES')
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(ob.data)
        bm.free()
        printed = I.LAYER in ob.data.uv_layers
        for l in [l for l in ob.data.uv_layers if l.name != I.LAYER]:   # primitives arrive with their own 'UVMap'
            ob.data.uv_layers.remove(l)
        lay = ob.data.uv_layers.new(name='UVMap')
        ob.data.uv_layers.active = lay
        lay.active_render = True
        density = R.layout_uvs(ob, SIZES[name], margin_px=8)
        stats[name] = {'triangles': len(ob.data.polygons), 'texelsPerMetre': density, 'size': [round(v, 4) for v in ob.dimensions]}
        others = [o for o in bpy.context.scene.objects if o != ob and o.type == 'MESH']
        for o in others:
            o.hide_render = True
        B._image.__globals__['SIZE'] = SIZES[name]
        if not os.environ.get('NO_BAKE'):
            imgs, times = B.bake(ob, [s.material for s in ob.material_slots], ao_samples=32)
            B.write_unity_maps(imgs, OUT + '/Texturas', prefix=name)
            print('BAKED', name, stats[name], times, flush=True)
        for o in others:
            o.hide_render = False
        if printed:   # export only the atlas UV, keep the print layer in the .blend for previews
            import numpy as np
            keep = np.empty(len(ob.data.loops) * 2, dtype=np.float32)
            ob.data.uv_layers[I.LAYER].data.foreach_get('uv', keep)
            ob.data.uv_layers.remove(ob.data.uv_layers[I.LAYER])
        R.export_fbx(ob, OUT + '/' + name + '.fbx')
        if printed:
            ob.data.uv_layers.new(name=I.LAYER).data.foreach_set('uv', keep)
            ob.data.uv_layers.active = ob.data.uv_layers['UVMap']
        ob.location.x = i * 1.2
    bpy.ops.wm.save_as_mainfile(filepath=SRC + '/Deposito.blend')
    prev = json.load(open(SRC + '/revisao/malhas.json')) if ONLY and os.path.exists(SRC + '/revisao/malhas.json') else {}
    prev.update(stats)
    with open(SRC + '/revisao/malhas.json', 'w') as f:
        json.dump(prev, f, indent=2)


if __name__ == '__main__':
    main()

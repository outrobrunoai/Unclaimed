"""Staff rest / changing room ("Descanso e vestiario"): steel lockers (carcass + separate doors), changing bench,
wall hook rail, staff belongings, rest table, coffee counter, small fridge, clock, notice board.
Metres, Blender axes (Unity uses x, z, y); each asset has its origin on the floor (wall plane for wall-hung ones) and its front
towards -Y (Unity prefabs face -Z). References and measurements: docs/revisao-descanso/referencias.md.

Run: blender -b --factory-startup --threads 8 --python Blender/Descanso/scripts/gerar.py [-- Asset ...]"""
import bpy, bmesh, math, os, json, types, sys
from mathutils import Vector, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../..'))
SRC = ROOT + '/Blender/Descanso'
OUT = ROOT + '/Assets/Modelos/Descanso'
ONLY = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else None
if ONLY and os.path.exists(SRC + '/Descanso.blend'):
    bpy.ops.wm.open_mainfile(filepath=SRC + '/Descanso.blend')


def load(rel, **extra):
    p = ROOT + '/' + rel
    ns = {'__name__': 'desc_module', '__file__': p, **extra}
    exec(compile(open(p, encoding='utf-8').read(), p, 'exec'), ns)
    return types.SimpleNamespace(**ns)


R = load('Blender/Recepcao/scripts/recepcao_modelo.py')
GN = load('Blender/MesaAutopsia/scripts/mesa_materiais.py').G
B = load('Blender/MesaAutopsia/scripts/mesa_bake.py')
M = load('Blender/Recepcao/scripts/recepcao_materiais.py')
M.metal.__globals__['G'] = GN
box, cyl, tube, extrude, hull, lathe = R.box, R.cyl, R.tube, R.extrude, R.hull, R.lathe
F = load('Blender/Descanso/scripts/formas.py', R=R)
PR = load('Blender/Descanso/scripts/pecas_roupas.py', R=R, F=F)
PM = load('Blender/Descanso/scripts/pecas_mochila.py', R=R, F=F)
PC = load('Blender/Descanso/scripts/pecas_calcados.py', R=R, F=F)
PB = load('Blender/Descanso/scripts/pecas_banco.py', R=R, F=F)
PK = load('Blender/Descanso/scripts/pecas_copa.py', R=R, F=F)
PJ = load('Blender/Descanso/scripts/pecas_jaleco.py', R=R, F=F, PR=PR)

# ---------------------------------------------------------------- dimensions
# Single-door steel wardrobe lockers ("roupeiro de aco") in modules of two, after the Brazilian catalogue units
# (Nilko NK 1332, 8-door lockers with padlock latch) and NR-24 item 24.4.3 (individual lockable lockers, washable seats).
LOCKER = dict(w=0.66, d=0.46, h=1.92, t=0.012, plinth=0.10, door_w=0.326, door_t=0.018, door_z0=0.115, door_z1=1.905,
              hinge_y=-0.23 - 0.012)
BENCH = dict(l=1.60, d=0.34, h=0.45)
TABLE = dict(w=0.90, d=0.90, h=0.74)
COUNTER = dict(l=1.80, d=0.58, h=0.90)
FRIDGE = dict(w=0.50, d=0.52, h=0.86)


def enamel(name, rgb, rough=0.42, kick=0.18, spots=()):
    return M.laminado(name, rgb, kick=kick, rough=rough, grime_spots=spots)


def madeira(name, rgb):
    """Varnished hardwood: fibre streaks along X (the length of the slats), a tint per slat across Y, worn varnish on the top face."""
    m, gg = M._new(name)
    p = gg.obj()
    streak = gg.noise(gg.coords(scale=(0.35, 70.0, 70.0)), 1.0, 3.0, 0.6)
    fine = gg.noise(gg.coords(scale=(0.8, 240.0, 240.0)), 1.0, 2.0, 0.5)
    slat = gg.noise(gg.coords(scale=(0.05, 12.0, 0.05)), 1.0, 1.0, 0.5)
    t = gg.math('ADD', gg.math('ADD', M.mul(gg, streak, 0.45), M.mul(gg, fine, 0.2)), M.mul(gg, slat, 0.35))
    base = gg.color(t, tuple(c * 0.70 for c in rgb), tuple(min(1.0, c * 1.12) for c in rgb))
    e = M.edges(gg, 0.005)
    dirt = M.mul(gg, gg.mrange(gg.noise(p, 9.0, 4.0, 0.6), 0.45, 0.8), 0.25)
    base = M.lerp(gg, base, tuple(c * 0.55 for c in rgb), dirt)
    base = M.lerp(gg, base, tuple(min(1.0, c * 1.10 + 0.03) for c in rgb), M.mul(gg, e, 0.4))
    r_ = gg.math('ADD', 0.38, M.mul(gg, dirt, 0.6))
    gg.finish(base, r_, 0.0, M.mul(gg, streak, 0.25), bump_strength=0.18, bump_dist=0.0004)
    return m


def fabric(name, rgb, rough, hem, pvc=False):
    """Woven cotton (matt, fine fibre noise) or coated PVC (smoother, glossier); the hem collects a little floor dirt."""
    m, gg = M._new(name)
    p = gg.obj()
    fine = gg.noise(p, 520.0 if not pvc else 160.0, 1.0, 0.5)
    cloud = gg.noise(p, 5.0, 3.0, 0.55)
    t = gg.math('ADD', M.mul(gg, cloud, 0.45), M.mul(gg, fine, 0.55))
    base = gg.color(t, tuple(c * 0.93 for c in rgb), tuple(min(1.0, c * 1.04) for c in rgb))
    dirt = gg.math('MULTIPLY', gg.mrange(gg.xyz()[2], hem, hem - 0.4), gg.mrange(gg.noise(p, 9.0, 4.0, 0.6), 0.35, 0.8))
    base = M.lerp(gg, base, tuple(c * 0.62 for c in rgb), M.mul(gg, dirt, 0.55))
    e = M.edges(gg, 0.004)
    base = M.lerp(gg, base, tuple(min(1.0, c * 1.10 + 0.02) for c in rgb), M.mul(gg, e, 0.25))
    r_ = gg.math('ADD', rough, M.mul(gg, dirt, 0.1))
    gg.finish(base, r_, 0.0, fine, bump_strength=0.25 if not pvc else 0.05, bump_dist=0.0004)
    return m


def mats_for(asset):
    inox = M.metal('Inox escovado - descanso', (0.74, 0.74, 0.73), 0.30)
    galv = M.metal('Aco galvanizado - descanso', (0.60, 0.61, 0.60), 0.38)
    preto = enamel('Esmalte preto - descanso', (0.045, 0.045, 0.05), 0.45, 0.2)
    borr = M.borracha('Borracha - descanso')
    if asset == 'Roupeiro':
        return {'corpo': enamel('Esmalte cinza claro - roupeiro', (0.62, 0.63, 0.60), 0.46, 0.22), 'aco': galv, 'inox': inox, 'preto': preto,
                'rodape': enamel('Esmalte escuro - rodape do roupeiro', (0.12, 0.12, 0.125), 0.5, 0.25), 'borracha': borr}
    if asset == 'PortaRoupeiro':
        return {'porta': enamel('Esmalte cinza azulado - porta', (0.30, 0.37, 0.40), 0.40, 0.2, spots=(((0.30, -0.03, 1.02), 0.10),)),
                'aco': galv, 'inox': inox, 'preto': preto, 'etiqueta': enamel('Moldura de etiqueta', (0.70, 0.70, 0.68), 0.35, 0.1)}
    if asset == 'Cadeado':
        return {'laiao': M.metal('Laiao - cadeado', (0.62, 0.50, 0.22), 0.30), 'aco': M.metal('Aco - cadeado', (0.72, 0.72, 0.72), 0.25), 'preto': preto}
    if asset == 'Banco':
        wood = madeira('Madeira envernizada - banco', (0.46, 0.29, 0.15))
        return {'madeira': wood, 'tubo': enamel('Esmalte grafite - banco', (0.11, 0.115, 0.12), 0.45, 0.18), 'aco': galv, 'borracha': borr}
    if asset == 'Cabideiro':
        return {'madeira': enamel('Madeira envernizada - cabideiro', (0.43, 0.27, 0.14), 0.42, 0.1), 'inox': inox, 'aco': galv}
    if asset in ('Jaleco', 'JalecoV2'):
        # v2: a clean coat in use, the hem dirt (from 0.4 m above the hem up) left out
        cloth = fabric('Algodao branco - jaleco v2', (0.80, 0.80, 0.77), 0.88, hem=-1.5) if asset == 'JalecoV2' else \
            fabric('Algodao branco - jaleco', (0.80, 0.80, 0.77), 0.88, hem=-0.62)
        return {'tecido': cloth, 'aco': galv, 'plastico': M.plastico('Cabide branco', (0.82, 0.82, 0.80), 0.4),
                'botao': M.plastico('Botao do jaleco', (0.86, 0.86, 0.84), 0.35), 'costura': M.papel('Linha branca', (0.84, 0.84, 0.80)),
                'tampa': M.plastico('Tampa azul da caneta', (0.05, 0.12, 0.42), 0.35)}
    if asset == 'CapaDeChuva':
        return {'capa': fabric('PVC amarelo - capa de chuva', (0.78, 0.58, 0.04), 0.34, hem=-0.75, pvc=True), 'aco': galv,
                'plastico_preto': M.plastico('Cabide preto', (0.04, 0.04, 0.045), 0.45), 'botao': M.plastico('Botao de pressao', (0.12, 0.12, 0.12), 0.3),
                'costura': M.papel('Linha amarela', (0.70, 0.56, 0.12))}
    if asset == 'Mochila':
        return {'tecido': fabric('Nylon azul - mochila', (0.085, 0.105, 0.17), 0.80, hem=0.10), 'tecido2': fabric('Nylon cinza - mochila', (0.22, 0.24, 0.28), 0.78, hem=0.10),
                'tira': M.nylon('Tira da mochila'), 'zip': M.metal('Ziper - mochila', (0.52, 0.52, 0.52), 0.35), 'fundo': M.plastico('Base - mochila', (0.03, 0.03, 0.035), 0.6)}
    if asset == 'Tamancos':
        return {'branco': M.plastico('Borracha branca - tamanco', (0.80, 0.80, 0.77), 0.55), 'borracha': borr}
    if asset == 'Botas':
        return {'branco': M.plastico('PVC branco - bota', (0.78, 0.78, 0.75), 0.40), 'borracha': enamel('Sola de bota', (0.06, 0.06, 0.06), 0.8, 0.3)}
    if asset == 'Mesa':
        return {'tampo': enamel('Laminado bege - mesa', (0.60, 0.57, 0.48), 0.40, 0.3, spots=(((0.0, -0.30, 0.74), 0.30), ((0.0, 0.30, 0.74), 0.30))),
                'tubo': enamel('Esmalte grafite - mesa', (0.11, 0.115, 0.12), 0.45, 0.2), 'borracha': borr, 'aco': galv}
    if asset == 'BalcaoCopa':
        return {'laminado': enamel('Laminado marfim - balcao', (0.66, 0.64, 0.57), 0.42, 0.28, spots=(((0.0, -0.20, 0.90), 0.50),)),
                'tampo': enamel('Laminado cinza - tampo da copa', (0.38, 0.38, 0.37), 0.35, 0.1, spots=(((-0.55, -0.15, 0.92), 0.20),)),
                'rodape': enamel('Rodape preto - balcao', (0.07, 0.07, 0.075), 0.5, 0.3), 'inox': inox, 'aco': galv,
                'plastico': M.plastico('Tomada - branco', (0.82, 0.82, 0.80), 0.4)}
    if asset == 'Frigobar':
        return {'branco': enamel('Esmalte branco - frigobar', (0.80, 0.80, 0.78), 0.32, 0.2, spots=(((0.0, -0.26, 0.60), 0.18),)),
                'plastico': M.plastico('Plastico cinza - frigobar', (0.45, 0.46, 0.46), 0.5), 'borracha': borr, 'inox': inox}
    if asset == 'Marmita':
        return {'plastico': M.plastico('Plastico marmita', (0.70, 0.70, 0.68), 0.35), 'tampa': M.plastico('Tampa azul marmita', (0.12, 0.25, 0.55), 0.4)}
    if asset == 'ItensCopa':
        return {'termica': enamel('Garrafa termica - vermelha', (0.45, 0.06, 0.05), 0.35, 0.1), 'inox': inox, 'preto': M.plastico('Plastico preto - copa', (0.04, 0.04, 0.045), 0.5),
                'azul': M.plastico('Pote azul', (0.10, 0.28, 0.62), 0.35), 'branco': M.plastico('Pote branco', (0.80, 0.80, 0.77), 0.35),
                'vidro': M.plastico('Vidro do pote', (0.55, 0.45, 0.30), 0.15, scuffed=False), 'copo': M.plastico('Copo descartavel', (0.85, 0.85, 0.82), 0.5),
                'cafe': M.plastico('Cafe', (0.05, 0.025, 0.015), 0.25, scuffed=False), 'acucar': M.plastico('Acucar', (0.80, 0.78, 0.72), 0.8, scuffed=False),
                'etiqueta': M.papel('Etiqueta do pote', (0.72, 0.62, 0.40)), 'caneca': enamel('Esmalte caneca', (0.78, 0.78, 0.74), 0.25, 0.1), 'caneca2': enamel('Esmalte caneca verde', (0.10, 0.35, 0.28), 0.25, 0.1)}
    if asset == 'Relogio':
        return {'moldura': M.plastico('Moldura preta - relogio', (0.04, 0.04, 0.045), 0.35), 'ponteiro': M.plastico('Ponteiro preto', (0.02, 0.02, 0.022), 0.4, scuffed=False),
                'vermelho': M.plastico('Ponteiro vermelho', (0.5, 0.04, 0.03), 0.4, scuffed=False), 'vidro': M.tela('Vidro do relogio')}
    if asset == 'RelogioMostrador':
        return {'impresso': M.tela('impresso')}
    if asset == 'QuadroAvisos':
        return {'cortica': enamel('Cortica - quadro', (0.55, 0.40, 0.24), 0.9, 0.0), 'aluminio': M.metal('Aluminio - quadro', (0.70, 0.70, 0.69), 0.35),
                'percevejo': M.plastico('Percevejo vermelho', (0.60, 0.05, 0.04), 0.3, scuffed=False),
                'percevejo2': M.plastico('Percevejo azul', (0.10, 0.20, 0.55), 0.3, scuffed=False)}
    raise KeyError(asset)


# ---------------------------------------------------------------- Roupeiro (carcass: two lockers)
# Origin: floor, centre of the footprint; front towards -Y. Each locker 0.33 m of the module; the doors (PortaRoupeiro)
# are separate parts hinged on the left edge of each locker.

def roupeiro():
    P = LOCKER
    W, D, H, t, pl = P['w'] / 2, P['d'] / 2, P['h'], P['t'], P['plinth']
    box('Lateral esquerda', 'corpo', -W, -D, pl, -W + t, D, H, r=0.002, seg=2)
    box('Lateral direita', 'corpo', W - t, -D, pl, W, D, H, r=0.002, seg=2)
    box('Divisoria', 'corpo', -t / 2, -D + 0.012, pl, t / 2, D - t, H - 0.015, r=0.001, seg=1)
    box('Tampo', 'corpo', -W, -D, H - 0.016, W, D, H, r=0.003, seg=3)
    box('Fundo', 'corpo', -W + t, D - t, pl, W - t, D, H - 0.016, r=0, seg=1)
    box('Piso', 'corpo', -W + t, -D + 0.03, pl, W - t, D - t, pl + t, r=0.001, seg=1)
    box('Rodape', 'rodape', -W + 0.010, -D + 0.022, 0.0, W - 0.010, D - 0.014, pl, r=0.002, seg=2)
    # Front returns of the opening (folded flange): sides, centre, head and sill.
    for x0, x1 in ((-W, -W + 0.020), (-0.012, 0.012), (W - 0.020, W)):
        box('Aba frontal', 'corpo', x0, -D, pl, x1, -D + 0.012, H - 0.016, r=0.0015, seg=1)
    box('Aba superior', 'corpo', -W, -D, H - 0.052, W, -D + 0.012, H - 0.016, r=0.0015, seg=1)
    box('Aba inferior', 'corpo', -W, -D, pl, W, -D + 0.012, pl + 0.022, r=0.0015, seg=1)
    for k in range(2):
        cx = -0.165 + 0.330 * k
        x0, x1 = cx - 0.148, cx + 0.148
        # Hat shelf with a front lip, hanging rail on two brackets, coat hook on the side.
        box(f'Prateleira {k}', 'corpo', x0, -D + 0.012, 1.60, x1, D - t, 1.612, r=0.001, seg=1)
        box(f'Borda da prateleira {k}', 'corpo', x0, -D + 0.012, 1.585, x1, -D + 0.020, 1.612, r=0.001, seg=1)
        cyl(f'Cabideiro {k}', 'inox', (cx, -0.02, 1.50), 0.0075, 0.296, axis=(1, 0, 0), seg=14, r=0.001)
        for sx in (x0 + 0.002, x1 - 0.002):
            box(f'Suporte do cabideiro {k} {sx:+.2f}', 'aco', sx - 0.002, -0.05, 1.485, sx + 0.002, 0.01, 1.515, r=0.0005, seg=1)
        tube(f'Gancho {k}', 'inox', [(x0 + 0.003, 0.12, 1.40), (x0 + 0.03, 0.12, 1.40), (x0 + 0.03, 0.12, 1.43)], 0.0030, fillet=0.008)
        # Hinge halves on the frame (knuckle + leaf), one per hinge height, at the door's left edge.
        hx = cx - 0.165 + 0.001
        for z in (0.30, 1.01, 1.72):
            box(f'Folha da dobradica {k} {z:.2f}', 'aco', hx - 0.001, -D - 0.002, z - 0.032, hx + 0.020, -D + 0.012, z + 0.032, r=0.0008, seg=1)
        # Padlock tab on the frame at the free edge, hole for the shackle.
        tab = box(f'Orelha do cadeado {k}', 'aco', cx + 0.165 - 0.034, -D - 0.022, 0.955, cx + 0.165 - 0.026, -D + 0.012, 1.005, r=0.0006, seg=1)
        hole = cyl(f'furo {k}', 'aco', (cx + 0.165 - 0.030, -D - 0.011, 0.980), 0.0040, 0.02, axis=(1, 0, 0), seg=12, r=0)
        R.cut(tab, hole)
    # Levelling feet under the plinth are hidden; two floor-screw plates inside, as real units are bolted to the wall/floor.
    for sx in (-1, 1):
        box(f'Chapa de fixacao {sx:+d}', 'aco', sx * (W - 0.060) - 0.025, D - t - 0.002, 0.10, sx * (W - 0.060) + 0.025, D - t, 0.14, r=0.0005, seg=1)


# ---------------------------------------------------------------- PortaRoupeiro
# Door in the frame of its hinge axis: origin on the axis (vertical), floor level, the panel runs to +X and stands in front
# (towards -Y). Louvre slots top and bottom, recessed latch handle with padlock tab, label frame.

def porta_roupeiro():
    P = LOCKER
    w, th, z0, z1 = P['door_w'], P['door_t'], P['door_z0'], P['door_z1']
    pan = box('Painel', 'porta', 0.0, -th / 2, z0, w, th / 2, z1, r=0.0025, seg=2)
    cut_parts = []
    for z_c in (1.69, 0.23):                      # louvre fields: five slots 0.19 m wide
        for i in range(5):
            zz = z_c - 0.04 + i * 0.02
            cut_parts.append(box(f'__fenda {z_c:.2f} {i}', 'porta', 0.068, -0.02, zz - 0.0035, 0.258, 0.02, zz + 0.0035, r=0, seg=1))
    cut_parts.append(box('__bolso', 'porta', w - 0.095, -th, 0.935, w - 0.030, 0.0, 1.075, r=0.006, seg=2))
    for c in cut_parts:
        R.cut(pan, c)
    # Latch in the stamped pocket: pivoting flat lever and its round boss, the tab with the padlock hole on the edge.
    cyl('Eixo do fecho', 'inox', (w - 0.062, -th / 2 + 0.002, 1.00), 0.011, 0.004, axis=(0, 1, 0), seg=20, r=0.001)
    box('Alavanca do fecho', 'inox', w - 0.078, -th / 2 - 0.006, 0.992, w - 0.044, -th / 2 + 0.0, 1.008, r=0.002, seg=2)
    ear = box('Orelha da porta', 'aco', w - 0.028, -th / 2 - 0.0035, 0.955, w + 0.012, -th / 2 + 0.0, 1.005, r=0.0006, seg=1)
    R.cut(ear, cyl('__furo', 'aco', (w - 0.004, -th / 2 - 0.0015, 0.980), 0.0040, 0.02, axis=(0, 1, 0), seg=12, r=0))
    # Label frame at eye level (the printed card is a separate quad in Unity).
    lx, lz = w / 2 + 0.004, 1.52
    for (a0, a1, b0, b1) in ((-0.040, 0.040, 0.014, 0.017), (-0.040, 0.040, -0.017, -0.014), (-0.043, -0.040, -0.017, 0.017), (0.040, 0.043, -0.017, 0.017)):
        box('Moldura da etiqueta', 'etiqueta', lx + a0, -th / 2 - 0.0016, lz + b0, lx + a1, -th / 2 + 0.0, lz + b1, r=0.0004, seg=1)
    # Hinge: the door's half of each hinge (leaf riveted to the panel + the barrel on the axis).
    for z in (0.30, 1.01, 1.72):
        cyl(f'Pino da dobradica {z:.2f}', 'aco', (0.0, 0.0, z), 0.0058, 0.066, axis=(0, 0, 1), seg=14, r=0.0008)
        box(f'Folha da porta {z:.2f}', 'aco', 0.0, -th / 2 - 0.0016, z - 0.030, 0.034, -th / 2, z + 0.030, r=0.0006, seg=1)
    # Inside: a stiffening return along the free edge and a spare coat hook.
    box('Dobra interna', 'porta', w - 0.012, th / 2, z0 + 0.01, w - 0.002, th / 2 + 0.018, z1 - 0.01, r=0.0008, seg=1)
    tube('Gancho interno', 'inox', [(0.15, th / 2, 1.52), (0.15, th / 2 + 0.040, 1.52), (0.15, th / 2 + 0.040, 1.55)], 0.0030, fillet=0.008)


# ---------------------------------------------------------------- Cadeado (padlock)

def cadeado():
    """Small laminated-steel padlock hanging by its shackle (origin = shackle centre in the tab hole, shackle along X)."""
    box('Corpo', 'laiao', -0.020, -0.007, -0.056, 0.020, 0.007, -0.010, r=0.004, seg=2)
    box('Placa', 'aco', -0.018, -0.0072, -0.052, 0.018, -0.0062, -0.014, r=0.001, seg=1)
    tube('Arco', 'aco', [(0.0, 0.0, -0.010), (0.0, 0.0, 0.006), (0.0, 0.0, 0.022), (0.0, 0.0, 0.026)], 0.0030, fillet=0.0)
    cyl('Fechadura', 'preto', (0.0, -0.0075, -0.046), 0.0035, 0.002, axis=(0, 1, 0), seg=12, r=0)


# ---------------------------------------------------------------- Banco do vestiario
# Wooden-slat seat on two tubular steel end frames, bolted through the slats' rails and to the floor with plates.

def banco():
    P = BENCH
    L, Dd, h = P['l'] / 2, P['d'] / 2, P['h']
    seat0 = h - 0.034
    for i in range(3):
        y0 = -Dd + 0.004 + i * 0.1105
        box(f'Ripa {i}', 'madeira', -L, y0, seat0, L, y0 + 0.100, h, r=0.004, seg=2)
    for sx in (-1, 1):
        x = sx * (L - 0.14)
        for sy in (-1, 1):
            box(f'Perna {sx:+d} {sy:+d}', 'tubo', x - 0.016, sy * (Dd - 0.030) - 0.016, 0.012, x + 0.016, sy * (Dd - 0.030) + 0.016, seat0, r=0.003, seg=2)
            box(f'Chapa {sx:+d} {sy:+d}', 'aco', x - 0.036, sy * (Dd - 0.030) - 0.036, 0.0, x + 0.036, sy * (Dd - 0.030) + 0.036, 0.012, r=0.002, seg=2)
            for bx in (-0.022, 0.022):
                cyl(f'Parafuso do piso {sx:+d} {sy:+d} {bx:+.2f}', 'aco', (x + bx, sy * (Dd - 0.030) + (bx * 0.0), 0.0135), 0.0062, 0.003, seg=6, r=0.0006)
        box(f'Travessa {sx:+d}', 'tubo', x - 0.016, -Dd + 0.030, seat0 - 0.040, x + 0.016, Dd - 0.030, seat0 - 0.008, r=0.003, seg=2)
        box(f'Travessa baixa {sx:+d}', 'tubo', x - 0.012, -Dd + 0.030, 0.17, x + 0.012, Dd - 0.030, 0.19, r=0.002, seg=1)
    box('Longarina', 'tubo', -L + 0.06, -0.016, seat0 - 0.040, L - 0.06, 0.016, seat0 - 0.008, r=0.003, seg=2)
    for sx in (-1, 1):
        for y in (-0.10, 0.10):
            cyl(f'Parafuso do assento {sx:+d} {y:+.2f}', 'aco', (sx * (L - 0.14), y, h + 0.0004), 0.0050, 0.0016, seg=6, r=0.0004)


# ---------------------------------------------------------------- Cabideiro de parede (hook rail)

def cabideiro():
    l = 1.20
    box('Tabua', 'madeira', -l / 2, -0.022, -0.040, l / 2, 0.0, 0.040, r=0.004, seg=2)
    for i in range(6):
        x = -0.50 + i * 0.20
        box(f'Base do gancho {i}', 'inox', x - 0.014, -0.0245, -0.020, x + 0.014, -0.022, 0.020, r=0.001, seg=1)
        tube(f'Gancho {i}', 'inox', [(x, -0.024, -0.010), (x, -0.062, -0.004), (x, -0.075, 0.020), (x, -0.066, 0.036)], 0.0032, fillet=0.012)
        cyl(f'Ponta {i}', 'inox', (x, -0.066, 0.038), 0.0058, 0.0108, axis=(0, 0, 1), seg=10, r=0.001)
    for sx in (-1, 1):
        cyl(f'Parafuso {sx:+d}', 'aco', (sx * (l / 2 - 0.03), -0.0225, 0.0), 0.0045, 0.0016, axis=(0, 1, 0), seg=8, r=0.0004)


# ---------------------------------------------------------------- Belongings

def jaleco():
    """White lab coat on a plastic hanger; origin = hook point, coat hangs to z = -0.95, front -Y."""
    tube('Gancho do cabide', 'aco', [(0.0, 0.0, 0.0), (0.0, 0.0, -0.045), (0.012, 0.0, -0.062)], 0.0016, fillet=0.006)
    hull('Ombros', 'plastico', [(-0.215, -0.008, -0.095), (0.215, -0.008, -0.095), (-0.215, 0.008, -0.095), (0.215, 0.008, -0.095), (0.0, -0.008, -0.060), (0.0, 0.008, -0.060)], r=0.003)
    body = [(-0.215, -0.07), (-0.225, -0.30), (-0.240, -0.95), (0.240, -0.95), (0.225, -0.30), (0.215, -0.07), (0.07, -0.045), (0.0, -0.075), (-0.07, -0.045)]
    extrude('Corpo', 'tecido', body, 'XZ', -0.030, 0.030, r=0.010, seg=2, min_angle=40.0)
    for s in (-1, 1):
        hull(f'Manga {s:+d}', 'tecido', [(s * 0.215, -0.030, -0.07), (s * 0.215, 0.030, -0.07), (s * 0.285, -0.030, -0.07), (s * 0.285, 0.030, -0.07), (s * 0.300, -0.026, -0.62), (s * 0.300, 0.026, -0.62),
                                           (s * 0.245, -0.026, -0.62), (s * 0.245, 0.026, -0.62)], r=0.008)
    box('Gola', 'tecido', -0.085, -0.036, -0.075, 0.085, 0.036, -0.040, r=0.008, seg=2)
    for i in range(5):
        cyl(f'Botao {i}', 'botao', (0.012, -0.0305, -0.20 - i * 0.14), 0.0075, 0.003, axis=(0, 1, 0), seg=12, r=0.0006)
    box('Bolso inferior direito', 'tecido', 0.07, -0.0385, -0.62, 0.20, -0.030, -0.46, r=0.003, seg=1)
    box('Bolso inferior esquerdo', 'tecido', -0.20, -0.0385, -0.62, -0.07, -0.030, -0.46, r=0.003, seg=1)
    box('Bolso do peito', 'tecido', -0.19, -0.0345, -0.28, -0.09, -0.030, -0.20, r=0.002, seg=1)
    box('Caneta no bolso', 'plastico', -0.145, -0.0365, -0.255, -0.139, -0.0345, -0.172, r=0.001, seg=1)


def capa_de_chuva():
    """Hooded PVC raincoat (yellow) hanging by its loop; origin = hook point, front -Y."""
    tube('Gancho do cabide', 'aco', [(0.0, 0.0, 0.0), (0.0, 0.0, -0.040), (0.010, 0.0, -0.055)], 0.0016, fillet=0.006)
    hull('Ombros', 'plastico', [(-0.20, -0.008, -0.09), (0.20, -0.008, -0.09), (-0.20, 0.008, -0.09), (0.20, 0.008, -0.09), (0.0, -0.008, -0.058), (0.0, 0.008, -0.058)], r=0.003)
    body = [(-0.21, -0.07), (-0.27, -0.30), (-0.33, -1.00), (0.33, -1.00), (0.27, -0.30), (0.21, -0.07), (0.0, -0.095)]
    extrude('Corpo', 'capa', body, 'XZ', -0.034, 0.034, r=0.012, seg=2, min_angle=35.0)
    hull('Capuz', 'capa', [(-0.12, -0.05, -0.06), (0.12, -0.05, -0.06), (-0.12, 0.07, -0.06), (0.12, 0.07, -0.06), (-0.10, -0.03, -0.24), (0.10, -0.03, -0.24), (-0.10, 0.06, -0.24), (0.10, 0.06, -0.24)], r=0.016)
    for s in (-1, 1):
        hull(f'Manga {s:+d}', 'capa', [(s * 0.21, -0.034, -0.08), (s * 0.21, 0.034, -0.08), (s * 0.30, -0.034, -0.10), (s * 0.30, 0.034, -0.10), (s * 0.35, -0.030, -0.74), (s * 0.35, 0.030, -0.74),
                                         (s * 0.29, -0.030, -0.74), (s * 0.29, 0.030, -0.74)], r=0.010)
    for i in range(4):
        cyl(f'Botao de pressao {i}', 'plastico', (0.0, -0.0345, -0.26 - i * 0.19), 0.008, 0.003, axis=(0, 1, 0), seg=12, r=0.0006)


def mochila():
    """Daypack standing on the floor / bench, front -Y (origin: centre of the base)."""
    w, d, h = 0.30, 0.17, 0.44
    pts = [(-w / 2, 0.0), (w / 2, 0.0), (w / 2 + 0.005, h * 0.60), (w / 2 - 0.035, h - 0.03), (w / 2 - 0.09, h), (-w / 2 + 0.09, h), (-w / 2 + 0.035, h - 0.03), (-w / 2 - 0.005, h * 0.60)]
    extrude('Corpo', 'tecido', pts, 'XZ', -d / 2, d / 2, r=0.030, seg=3, min_angle=25.0)
    box('Bolso da frente', 'tecido', -w / 2 + 0.025, -d / 2 - 0.030, 0.020, w / 2 - 0.025, -d / 2 + 0.010, h * 0.50, r=0.016, seg=3)
    tube('Zipper', 'inox', [(-w / 2 + 0.05, -d / 2 - 0.031, h * 0.50 - 0.006), (w / 2 - 0.05, -d / 2 - 0.031, h * 0.50 - 0.006)], 0.0020)
    box('Puxador do zipper', 'tira', 0.0, -d / 2 - 0.036, h * 0.50 - 0.040, 0.012, -d / 2 - 0.030, h * 0.50 - 0.006, r=0.002, seg=1)
    tube('Alca superior', 'tira', [(-0.035, 0.0, h - 0.005), (-0.035, 0.0, h + 0.050), (0.035, 0.0, h + 0.050), (0.035, 0.0, h - 0.005)], 0.007, fillet=0.02)
    for s in (-1, 1):
        tube(f'Alca {s:+d}', 'tira', [(s * 0.085, d / 2 - 0.005, h - 0.02), (s * 0.095, d / 2 + 0.028, h * 0.60), (s * 0.075, d / 2 + 0.022, 0.05)], 0.011, fillet=0.03)
    for s in (-1, 1):
        cyl(f'Fivela {s:+d}', 'inox', (s * 0.075, d / 2 + 0.024, 0.05), 0.008, 0.004, axis=(0, 1, 0), seg=8, r=0.0005)


def tamancos():
    """Pair of white work clogs, toes towards -Y (origin: floor, between the clogs)."""
    for s in (-1, 1):
        x = s * 0.060
        sole = [(-0.040, -0.150), (0.040, -0.150), (0.045, 0.030), (0.036, 0.130), (-0.036, 0.130), (-0.045, 0.030)]
        extrude(f'Sola {s:+d}', 'borracha', sole, 'XY', 0.0, 0.020, r=0.006, seg=2, min_angle=20.0, matrix=Matrix.Translation((x, 0.0, 0.0)))
        hull(f'Biqueira {s:+d}', 'branco', [(x - 0.037, -0.145, 0.020), (x + 0.037, -0.145, 0.020), (x - 0.040, -0.040, 0.020), (x + 0.040, -0.040, 0.020),
                                             (x - 0.034, -0.140, 0.046), (x + 0.034, -0.140, 0.046), (x - 0.036, -0.010, 0.082), (x + 0.036, -0.010, 0.082), (x - 0.040, 0.05, 0.020), (x + 0.040, 0.05, 0.020)], r=0.010)
        hull(f'Tira do calcanhar {s:+d}', 'branco', [(x - 0.036, 0.075, 0.020), (x + 0.036, 0.075, 0.020), (x - 0.034, 0.125, 0.020), (x + 0.034, 0.125, 0.020), (x - 0.034, 0.100, 0.080), (x + 0.034, 0.100, 0.080)], r=0.006)
        for i in range(5):
            cyl(f'Furo {s:+d} {i}', 'borracha', (x - 0.020 + i * 0.010, -0.110, 0.0475), 0.0028, 0.002, axis=(0, 0, 1), seg=8, r=0)


def botas():
    """Pair of white PVC boots, upright, toes towards -Y (origin: floor, between them)."""
    for s in (-1, 1):
        x = s * 0.085
        sole = [(-0.048, -0.150), (0.048, -0.150), (0.052, 0.040), (0.044, 0.130), (-0.044, 0.130), (-0.052, 0.040)]
        extrude(f'Sola {s:+d}', 'borracha', sole, 'XY', 0.0, 0.026, r=0.008, seg=2, min_angle=20.0, matrix=Matrix.Translation((x, 0.0, 0.0)))
        hull(f'Pe {s:+d}', 'branco', [(x - 0.044, -0.140, 0.026), (x + 0.044, -0.140, 0.026), (x - 0.050, 0.100, 0.026), (x + 0.050, 0.100, 0.026), (x - 0.040, -0.110, 0.075), (x + 0.040, -0.110, 0.075),
                                      (x - 0.052, 0.085, 0.140), (x + 0.052, 0.085, 0.140), (x - 0.040, -0.060, 0.120), (x + 0.040, -0.060, 0.120)], r=0.012)
        cyl(f'Cano {s:+d}', 'branco', (x, 0.045, 0.300), 0.058, 0.320, seg=24, r=0.006, radius_top=0.066)
        cyl(f'Borda do cano {s:+d}', 'branco', (x, 0.045, 0.462), 0.068, 0.012, seg=24, r=0.004)
        box(f'Salto {s:+d}', 'borracha', x - 0.044, 0.085, 0.0, x + 0.044, 0.130, 0.030, r=0.004, seg=1)


# ---------------------------------------------------------------- Mesa de descanso

def mesa():
    P = TABLE
    W, Dd, h = P['w'] / 2, P['d'] / 2, P['h']
    box('Tampo', 'tampo', -W, -Dd, h - 0.030, W, Dd, h, r=0.003, seg=2)
    box('Fita de borda', 'borracha', -W - 0.0016, -Dd - 0.0016, h - 0.030, W + 0.0016, Dd + 0.0016, h - 0.026, r=0.0005, seg=1)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (W - 0.060), sy * (Dd - 0.060)
            box(f'Perna {sx:+d} {sy:+d}', 'tubo', x - 0.020, y - 0.020, 0.010, x + 0.020, y + 0.020, h - 0.030, r=0.003, seg=2)
            cyl(f'Sapata {sx:+d} {sy:+d}', 'borracha', (x, y, 0.005), 0.026, 0.010, seg=16, r=0.002)
    for sy in (-1, 1):
        box(f'Travessa {sy:+d}', 'tubo', -W + 0.060, sy * (Dd - 0.060) - 0.012, h - 0.075, W - 0.060, sy * (Dd - 0.060) + 0.012, h - 0.034, r=0.002, seg=1)
    for sx in (-1, 1):
        box(f'Travessa lateral {sx:+d}', 'tubo', sx * (W - 0.060) - 0.012, -Dd + 0.060, h - 0.075, sx * (W - 0.060) + 0.012, Dd - 0.060, h - 0.034, r=0.002, seg=1)


# ---------------------------------------------------------------- Balcao da copa
# Base unit in cream melamine (two closed doors by composition, kick plinth), granite-pattern laminate worktop with
# a rolled front edge and an 80 mm upstand carrying two sockets. No sink: only a place for the thermos and cups.

def balcao_copa():
    P = COUNTER
    L, Dd, h = P['l'] / 2, P['d'] / 2, P['h']
    pl, tt = 0.09, 0.036
    box('Tampo', 'tampo', -L - 0.012, -Dd - 0.020, h - tt, L + 0.012, Dd, h, r=0.006, seg=3)
    box('Rodatampo', 'tampo', -L - 0.012, Dd - 0.012, h, L + 0.012, Dd, h + 0.080, r=0.003, seg=2)
    for sx in (-1, 1):
        box(f'Lateral {sx:+d}', 'laminado', sx * L - (0.018 if sx > 0 else 0), -Dd, pl, sx * L + (0.0 if sx > 0 else 0.018), Dd - 0.020, h - tt, r=0.002, seg=1)
    box('Fundo', 'laminado', -L + 0.018, Dd - 0.020, pl, L - 0.018, Dd - 0.002, h - tt, r=0, seg=1)
    box('Base', 'laminado', -L + 0.018, -Dd + 0.03, pl, L - 0.018, Dd - 0.020, pl + 0.018, r=0.001, seg=1)
    box('Travessa superior', 'laminado', -L + 0.018, -Dd + 0.02, h - tt - 0.080, L - 0.018, -Dd + 0.045, h - tt, r=0.001, seg=1)
    box('Rodape', 'rodape', -L + 0.020, -Dd + 0.050, 0.0, L - 0.020, Dd - 0.030, pl, r=0.002, seg=1)
    box('Divisoria central', 'laminado', -0.009, -Dd + 0.020, pl, 0.009, Dd - 0.020, h - tt, r=0.001, seg=1)
    box('Prateleira esquerda', 'laminado', -L + 0.018, -Dd + 0.045, 0.43, -0.009, Dd - 0.025, 0.448, r=0.001, seg=1)
    box('Prateleira direita', 'laminado', 0.009, -Dd + 0.045, 0.43, L - 0.018, Dd - 0.025, 0.448, r=0.001, seg=1)
    for k, x0 in enumerate((-L + 0.020, 0.0115)):
        box(f'Porta {k}', 'laminado', x0, -Dd - 0.0, pl + 0.004, x0 + L - 0.0325 + 0.000, -Dd + 0.018, h - tt - 0.004, r=0.0015, seg=1)
        px = x0 + (L - 0.0325) * (0.88 if k == 0 else 0.12)
        tube(f'Puxador {k}', 'inox', [(px, -Dd - 0.001, h - tt - 0.20), (px, -Dd - 0.028, h - tt - 0.20), (px, -Dd - 0.028, h - tt - 0.30), (px, -Dd - 0.001, h - tt - 0.30)], 0.0050, fillet=0.010)
    for k, x in enumerate((-0.55, -0.37)):
        box(f'Tomada {k}', 'plastico', x - 0.040, Dd - 0.0125, h + 0.026, x + 0.040, Dd - 0.0105 + 0.010, h + 0.066, r=0.002, seg=1)
        for dx in (-0.012, 0.012):
            cyl(f'Furo da tomada {k} {dx:+.3f}', 'aco', (x + dx, Dd - 0.0010, h + 0.046), 0.0042, 0.0016, axis=(0, 1, 0), seg=10, r=0)


# ---------------------------------------------------------------- Frigobar

def frigobar():
    P = FRIDGE
    W, Dd, h = P['w'] / 2, P['d'] / 2, P['h']
    box('Corpo', 'branco', -W, -Dd + 0.020, 0.040, W, Dd, h, r=0.010, seg=3)
    box('Porta', 'branco', -W + 0.002, -Dd, 0.044, W - 0.002, -Dd + 0.026, h - 0.004, r=0.008, seg=3)
    box('Vedacao', 'borracha', -W + 0.006, -Dd + 0.024, 0.052, W - 0.006, -Dd + 0.027, h - 0.012, r=0.001, seg=1)
    box('Puxador', 'plastico', W - 0.050, -Dd - 0.026, h - 0.38, W - 0.034, -Dd + 0.004, h - 0.10, r=0.005, seg=2)
    box('Rodape', 'plastico', -W + 0.015, -Dd + 0.045, 0.0, W - 0.015, Dd - 0.020, 0.040, r=0.002, seg=1)
    for sx in (-1, 1):
        cyl(f'Pe {sx:+d}', 'borracha', (sx * (W - 0.04), Dd - 0.05, 0.010), 0.014, 0.020, seg=10, r=0.002)
    box('Grade traseira', 'plastico', -W + 0.06, Dd - 0.004, 0.10, W - 0.06, Dd, h - 0.08, r=0, seg=1)
    box('Etiqueta de marca', 'inox', -0.06, -Dd - 0.0016, h - 0.080, 0.06, -Dd + 0.0, h - 0.058, r=0.001, seg=1)
    box('Luz do painel', 'plastico', -W + 0.022, -Dd - 0.0016, h - 0.120, -W + 0.040, -Dd + 0.0, h - 0.100, r=0.001, seg=1)


# ---------------------------------------------------------------- Marmita

def marmita():
    """Empty lunch container with its blue lid, resting on the fridge top."""
    box('Recipiente', 'plastico', -0.105, -0.075, 0.0, 0.105, 0.075, 0.060, r=0.012, seg=3)
    box('Tampa', 'tampa', -0.110, -0.080, 0.058, 0.110, 0.080, 0.070, r=0.010, seg=3)
    for s in (-1, 1):
        box(f'Trava {s:+d}', 'tampa', s * 0.112 - 0.008, -0.020, 0.030, s * 0.112 + 0.008, 0.020, 0.064, r=0.003, seg=1)


# ---------------------------------------------------------------- Items on the counter (origin: counter top, x along the counter)

def itens_copa():
    # Pump thermos (red), coffee jars (blue and white lids), stack of disposable cups, two mugs, a spoon.
    lathe('Garrafa termica', 'termica', [(0.000, 0.0), (0.060, 0.0), (0.066, 0.012), (0.068, 0.160), (0.062, 0.215), (0.046, 0.245), (0.0, 0.245)], seg=28, r=0.004,
          matrix=Matrix.Translation((-0.60, -0.08, 0.0)))
    lathe('Tampa da termica', 'preto', [(0.0, 0.245), (0.046, 0.245), (0.050, 0.262), (0.034, 0.282), (0.0, 0.282)], seg=24, r=0.004, matrix=Matrix.Translation((-0.60, -0.08, 0.0)))
    tube('Bico da termica', 'preto', [(-0.60, -0.08, 0.282), (-0.60, -0.08, 0.305), (-0.58, -0.08, 0.320), (-0.545, -0.08, 0.316)], 0.008, fillet=0.015)
    tube('Alca da termica', 'preto', [(-0.60, -0.08 + 0.066, 0.200), (-0.60, -0.08 + 0.105, 0.190), (-0.60, -0.08 + 0.105, 0.080), (-0.60, -0.08 + 0.066, 0.065)], 0.009, fillet=0.02)
    for key, x, lid in (('Pote azul', -0.30, 'azul'), ('Pote branco', -0.15, 'branco')):
        cyl(f'{key} vidro', 'vidro', (x, -0.06, 0.065), 0.052, 0.130, seg=24, r=0.005)
        cyl(f'{key} tampa', lid, (x, -0.06, 0.140), 0.054, 0.024, seg=24, r=0.004)
    for i in range(7):
        cyl(f'Copo {i}', 'copo', (0.12, -0.08, 0.030 + i * 0.012), 0.034 - i * 0.0, 0.060, seg=20, r=0.0015, radius_top=0.034 + 0.0)
    cyl('Copos empilhados', 'copo', (0.12, -0.08, 0.036), 0.0300, 0.072, seg=24, r=0.0016, radius_top=0.0372)
    for k, (x, mat) in enumerate(((0.36, 'caneca'), (0.50, 'caneca2'))):
        cyl(f'Caneca {k}', mat, (x, -0.10, 0.045), 0.040, 0.090, seg=24, r=0.003)
        tube(f'Alca da caneca {k}', mat, [(x + 0.038, -0.10, 0.075), (x + 0.068, -0.10, 0.072), (x + 0.068, -0.10, 0.030), (x + 0.036, -0.10, 0.020)], 0.0050, fillet=0.012)
    box('Colher', 'inox', 0.22, -0.13, 0.0, 0.34, -0.121, 0.0030, r=0.001, seg=1)


# ---------------------------------------------------------------- Relogio de parede

def relogio():
    """Analogue wall clock, 300 mm, shows 10:09 (hour hand just past 10, minute hand on 9); origin on the wall at the centre."""
    cyl('Moldura', 'moldura', (0.0, -0.016, 0.0), 0.150, 0.032, axis=(0, 1, 0), seg=48, r=0.004)
    for name, ang, ln, wd, mat in (('Ponteiro das horas', math.radians(300 + 4.5), 0.075, 0.0072, 'ponteiro'), ('Ponteiro dos minutos', math.radians(270 + 54 * 0), 0.108, 0.0048, 'ponteiro'),
                                    ('Ponteiro dos segundos', math.radians(36.0), 0.115, 0.0016, 'vermelho')):
        a = ang  # clockwise from 12: x = sin, z = cos
        if name.startswith('Ponteiro dos minutos'):
            a = math.radians(54.0)
        elif name.startswith('Ponteiro das horas'):
            a = math.radians(300 + 4.5)
        dx, dz = math.sin(a), math.cos(a)
        px, pz = -dz, dx
        pts = [(-dx * 0.018 - px * wd, -dz * 0.018 - pz * wd), (-dx * 0.018 + px * wd, -dz * 0.018 + pz * wd), (dx * ln + px * wd * 0.35, dz * ln + pz * wd * 0.35), (dx * ln - px * wd * 0.35, dz * ln - pz * wd * 0.35)]
        yy = {'Ponteiro das horas': -0.0335, 'Ponteiro dos minutos': -0.0350, 'Ponteiro dos segundos': -0.0365}[name]
        extrude(name, mat, [(p[0], p[1]) for p in pts], 'XZ', yy - 0.0012, yy, r=0.0003, seg=1)
    cyl('Eixo central', 'vermelho', (0.0, -0.0372, 0.0), 0.0068, 0.003, axis=(0, 1, 0), seg=16, r=0.0006)
    cyl('Pino de fixacao', 'moldura', (0.0, 0.002, 0.125), 0.008, 0.004, axis=(0, 1, 0), seg=8, r=0)


def relogio_mostrador():
    """Printed dial (separate unbaked piece, planar UV over the 272 mm face; texture from mostrador_relogio.py)."""
    r = 0.136
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new('UVMap')
    n = 48
    ring = [bm.verts.new((math.cos(2 * math.pi * i / n) * r, -0.0324, math.sin(2 * math.pi * i / n) * r)) for i in range(n)]
    f = bm.faces.new(list(reversed(ring)))
    for lp in f.loops:
        lp[uv].uv = (lp.vert.co.x / (2 * r) + 0.5, lp.vert.co.z / (2 * r) + 0.5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    ob = R._finish('Mostrador', bm, 'impresso')


# ---------------------------------------------------------------- Quadro de avisos

BOARD = dict(w=0.90, h=0.60)


def quadro_avisos():
    w, h = BOARD['w'], BOARD['h']
    box('Cortica', 'cortica', -w / 2 + 0.014, -0.014, -h / 2 + 0.014, w / 2 - 0.014, 0.0, h / 2 - 0.014, r=0.0015, seg=1)
    for (a0, a1, b0, b1) in ((-w / 2, w / 2, h / 2 - 0.014, h / 2), (-w / 2, w / 2, -h / 2, -h / 2 + 0.014), (-w / 2, -w / 2 + 0.014, -h / 2 + 0.014, h / 2 - 0.014), (w / 2 - 0.014, w / 2, -h / 2 + 0.014, h / 2 - 0.014)):
        box('Moldura', 'aluminio', a0, -0.022, b0, a1, 0.0, b1, r=0.002, seg=2)
    # Pins at the top centre of each paper (positions shared with the generator: PAPEIS_QUADRO).
    for k, (x, z, m) in enumerate(((-0.255, 0.255, 'percevejo'), (0.170, 0.240, 'percevejo2'))):
        cyl(f'Percevejo {k}', m, (x, -0.0170, z), 0.0055, 0.0045, axis=(0, 1, 0), seg=10, r=0.0008)


BUILDERS = {
    'Roupeiro': roupeiro, 'PortaRoupeiro': porta_roupeiro, 'Cadeado': cadeado, 'Banco': PB.banco, 'Cabideiro': cabideiro, 'Jaleco': PJ.jaleco, 'JalecoV2': PJ.jaleco_v2,
    'CapaDeChuva': PR.capa_de_chuva, 'Mochila': PM.mochila, 'Tamancos': PC.tamancos, 'Botas': PC.botas, 'Mesa': mesa, 'BalcaoCopa': balcao_copa,
    'Frigobar': frigobar, 'Marmita': marmita, 'ItensCopa': PK.itens_copa, 'Relogio': relogio, 'RelogioMostrador': relogio_mostrador, 'QuadroAvisos': quadro_avisos,
}
UNBAKED = ('RelogioMostrador',)
KEEP_NORMALS = ('JalecoV2',)          # single-sided patches (pockets) are oriented when built; recalculating guesses their side
SMOOTH_ANGLE = {'Jaleco': 80, 'JalecoV2': 80}          # draped cloth: its tight folds are soft, not creases
SIZES = {'Roupeiro': 2048, 'PortaRoupeiro': 1024, 'Cadeado': 256, 'Banco': 1024, 'Cabideiro': 512, 'Jaleco': 2048, 'JalecoV2': 2048, 'CapaDeChuva': 1024, 'Mochila': 512,
         'Tamancos': 512, 'Botas': 512, 'Mesa': 1024, 'BalcaoCopa': 2048, 'Frigobar': 1024, 'Marmita': 256, 'ItensCopa': 1024, 'Relogio': 512, 'QuadroAvisos': 512}


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
        if name in SMOOTH_ANGLE:
            ob.data.set_sharp_from_angle(angle=math.radians(SMOOTH_ANGLE[name]))
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bmesh.ops.dissolve_degenerate(bm, dist=0.000001, edges=list(bm.edges))
        bmesh.ops.triangulate(bm, faces=list(bm.faces))
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_area() < 2e-9], context='FACES')   # boolean slivers wreck the UV packing
        if name not in KEEP_NORMALS:
            bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(ob.data)
        bm.free()
        density = 0 if name in UNBAKED else R.layout_uvs(ob, SIZES[name], margin_px=8)
        stats[name] = {'triangles': len(ob.data.polygons), 'texelsPerMetre': density, 'size': [round(v, 4) for v in ob.dimensions]}
        R.export_fbx(ob, OUT + '/' + name + '.fbx', keep_slots=name in UNBAKED)
        ob.location.x = i * 1.6
    bpy.ops.wm.save_as_mainfile(filepath=SRC + '/Descanso.blend')
    prev = json.load(open(SRC + '/revisao/malhas.json')) if ONLY and os.path.exists(SRC + '/revisao/malhas.json') else {}
    prev.update(stats)
    with open(SRC + '/revisao/malhas.json', 'w') as f:
        json.dump(prev, f, indent=2)
    if os.environ.get('NO_BAKE'):
        return
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
    bpy.ops.wm.save_as_mainfile(filepath=SRC + '/Descanso.blend')


if __name__ == '__main__':
    main()

"""Door kit of the interior openings (1.5 m raw): four families that share the approved circulation door's frame
profile and hardware language. Metres, Blender axes (Unity uses x, z, y); every leaf is built in its own frame:
origin on the hinge axis at floor level, leaf body towards +X, swing face towards +Y. The prefab builder in Unity
(Assets/Editor/UnclaimedPortasAssets.cs) mirrors leaves hinged on the right.

  A  institucional opaca  PortaInstMarco + (Adm|Descanso|Deposito) active leaf + matching secondary leaf
  B  sala tecnica          PortaInstMarco + PortaTecAtiva + PortaTecSec (louvred steel)
  C  clinica dupla         PortaClinicaMarco + PortaClinicaFolha (x2, mirrored) + PortaClinicaVidro (unbaked)
  D  camara fria           PortaFriaMarco + PortaFriaFolha (x2, mirrored): insulated panel, gasket, cam latches

Run: blender -b --factory-startup --python Blender/Portas/scripts/gerar.py [-- Asset ...]"""
import bpy, bmesh, math, os, json, types, sys
from mathutils import Vector, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../..'))
SRC = ROOT + '/Blender/Portas'
OUT = ROOT + '/Assets/Modelos/Portas'
ONLY = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else None
if ONLY and os.path.exists(SRC + '/Portas.blend'):
    bpy.ops.wm.open_mainfile(filepath=SRC + '/Portas.blend')


def load(rel):
    p = ROOT + '/' + rel
    ns = {'__name__': 'portas_module', '__file__': p}
    exec(compile(open(p, encoding='utf-8').read(), p, 'exec'), ns)
    return types.SimpleNamespace(**ns)


R = load('Blender/Recepcao/scripts/recepcao_modelo.py')
GN = load('Blender/MesaAutopsia/scripts/mesa_materiais.py').G
B = load('Blender/MesaAutopsia/scripts/mesa_bake.py')
M = load('Blender/Recepcao/scripts/recepcao_materiais.py')
M.metal.__globals__['G'] = GN
box, cyl, tube, extrude = R.box, R.cyl, R.tube, R.extrude


def mkP(**o):
    """Door parameters: the circulation door's, re-derived after overrides."""
    P = dict(R.PORTA_CIRC)
    P.update(o)
    P['face'] = P['wall'] + P['flange_t']
    P['leaf_back'] = P['face'] - P['leaf_t']
    P['jx'] = P['raw'] / 2 - P['rebate_u']
    P['pivot'] = (-P['jx'] + 0.0015, P['face'] + P['knuckle_r'])
    P['leaf_z'] = (P['sill_top'] + P['floor_gap'], P['height'] - P['rebate_u'] - P['gap'])
    return P


PA = mkP()                                                         # A and B: the circulation door's frame
PC = mkP(leaf_t=0.045, flange_w=0.05, sill_top=0.005, floor_gap=0.007)   # C: thicker leaf, stainless sill
PD = mkP(leaf_t=0.090, flange_w=0.06, stop_u=0.05, rebate_u=0.034, sill_top=0.006, floor_gap=0.008, knuckle_r=0.0085)


def leaf_dims(P, width):
    """Leaf box in the leaf frame: x0, x1, y0 (back), y1 (swing face), z0, z1."""
    px, py = P['pivot']
    x0 = P['gap'] - 0.0015
    return x0, x0 + width, P['leaf_back'] - py, P['face'] - py, P['leaf_z'][0], P['leaf_z'][1]


def leaf_width(P, active=None):
    full = 2 * P['jx'] - 2 * P['gap'] - P['center_gap']
    return (full - active) if active is not None else full / 2


# ---------------------------------------------------------------- frames

def frame(P, sill_mat, gasket=False):
    """Pressed-steel frame (one swept profile, mitred head) and a sill fitted to its outline."""
    hw, H = P['raw'] / 2, P['height']
    R.sweep_frame('Marco', 'marco', R._frame_profile(P), [(-hw, 0.0), (-hw, H), (hw, H), (hw, 0.0)],
                  [(1.0, 0.0), (0.0, -1.0), (-1.0, 0.0)])
    su, ru, lb, w = hw - P['stop_u'], P['jx'], P['leaf_back'], P['wall']
    sill = [(-su, -w), (su, -w), (su, lb), (ru, lb), (ru, w), (-ru, w), (-ru, lb), (-su, lb)]
    extrude('Soleira', sill_mat, sill, 'XY', -0.012, P['sill_top'], r=0.0015, seg=2)
    if gasket:   # EPDM seal pressed into the rebate: legs on the jambs, head on the lintel
        g = 0.012
        for sx in (-1, 1):
            xa, xb = sorted((sx * ru, sx * (ru - g)))
            box(f'Gaxeta {sx:+d}', 'veda', xa, lb - 0.002, P['sill_top'] + 0.001, xb, lb + 0.010, H - P['rebate_u'] - 0.001, r=0.003, seg=2)
        box('Gaxeta superior', 'veda', -ru + g, lb - 0.002, H - P['rebate_u'] - g, ru - g, lb + 0.010, H - P['rebate_u'] - 0.001, r=0.003, seg=2)


# ---------------------------------------------------------------- shared leaf parts

def hinges(P, zs, r=None, prefix='Dobradica', mirror=1):
    r = r or P['knuckle_r']
    for z in zs:
        R._hinge(f'{prefix} {z:.2f}', 'inox', 0.0, 0.0, z, r)


def plate(name, mat, x0, x1, y, face, z0, z1, t=0.0012, off=0.0004, r=0.0005):
    yi = y + face * off
    ya, yb = sorted((yi, yi + face * t))
    return box(name, mat, x0, ya, z0, x1, yb, z1, r=r, seg=1)


def pull_bar(prefix, x, y, face, z0=0.78, z1=1.18, r=0.0095, mat='inox'):
    """Vertical tubular pull on two stand-offs."""
    for z in (z0 + 0.04, z1 - 0.04):
        cyl(f'{prefix} - apoio {z:.2f}', mat, (x, y + face * 0.020, z), 0.0075, 0.040, axis=(0, 1, 0), seg=16, r=0.0012)
    cyl(f'{prefix} - barra', mat, (x, y + face * 0.042, (z0 + z1) / 2), r, z1 - z0, seg=20, r=0.003)


def round_rose_lever(prefix, hx, y, face, z=1.0):
    """Lever on a round rose (no long backplate) and a small cylinder escutcheon below."""
    yf = y + face * 0.004
    cyl(f'{prefix} - roseta', 'inox', (hx, yf, z), 0.027, 0.008, axis=(0, 1, 0), seg=32, r=0.002)
    tube(f'{prefix} - alavanca', 'inox', [(hx, yf + face * 0.004, z), (hx, yf + face * 0.050, z),
                                          (hx - 0.115, yf + face * 0.056, z), (hx - 0.128, yf + face * 0.042, z)],
         0.0095, fillet=0.018, res=3)
    cyl(f'{prefix} - espelho da chave', 'inox_escuro', (hx, yf, z - 0.075), 0.0135, 0.006, axis=(0, 1, 0), seg=24, r=0.0015)
    box(f'{prefix} - entrada da chave', 'fenda', hx - 0.0011, min(yf + face * 0.0031, yf + face * 0.0035), z - 0.0825,
        hx + 0.0011, max(yf + face * 0.0031, yf + face * 0.0035), z - 0.0675, r=0.0002, seg=1)


def veneziana(prefix, mat, x0, x1, z0, z1, ym, pitch=0.026, back='folha'):
    """Louvre blades (slanted 38 degrees) inside a frame already cut in the leaf."""
    n = int((z1 - z0) / pitch)
    for i in range(n):
        z = z0 + 0.004 + i * pitch
        extrude(f'{prefix} - lamina {i}', mat, [(ym - 0.0145, z), (ym + 0.0145, z + 0.019), (ym + 0.0145, z + 0.0212),
                                                (ym - 0.0145, z + 0.0022)], 'YZ', x0, x1, r=0.0004, seg=1)


def opening_leaf(name, mat, x0, x1, y0, y1, z0, z1, holes, r=0.0025):
    """Flush leaf with rectangular openings: stiles/rails around holes [(hx0, hx1, hz0, hz1), ...] (one column)."""
    holes = sorted(holes, key=lambda h: h[2])
    hx0, hx1 = holes[0][0], holes[0][1]
    box(f'{name} - montante da dobradica', mat, x0, y0, z0, hx0, y1, z1, r=r, seg=2)
    box(f'{name} - montante do fecho', mat, hx1, y0, z0, x1, y1, z1, r=r, seg=2)
    zc = z0
    for hh in holes:
        if hh[2] > zc + 1e-4:
            box(f'{name} - travessa {zc:.2f}', mat, hx0, y0, zc, hx1, y1, hh[2], r=r, seg=2)
        zc = hh[3]
    if z1 > zc + 1e-4:
        box(f'{name} - travessa {zc:.2f}', mat, hx0, y0, zc, hx1, y1, z1, r=r, seg=2)


# ---------------------------------------------------------------- family A / B leaves

def leaf_A(variant, secondary=False):
    """Opaque institutional leaf. adm: flush, backplate lever + profile cylinder; descanso: flush, round-rose lever;
    deposito: with a ventilation louvre low on the leaf and backplate lever. The secondary carries a tubular pull
    (and flush bolts on its edge, invisible when shut)."""
    P = PA
    wd = leaf_width(P, P['active']) if secondary else P['active']
    x0, x1, y0, y1, z0, z1 = leaf_dims(P, wd)
    if variant == 'deposito':
        vx0, vx1, vz0, vz1 = x0 + 0.20, x1 - 0.20, 0.30, 0.52
        if secondary:
            vx0, vx1 = x0 + 0.12, x1 - 0.12
        opening_leaf('Folha', 'folha', x0, x1, y0, y1, z0, z1, [(vx0, vx1, vz0, vz1)])
        # Pressed frame of the louvre on both faces, blades between.
        for face, y in ((-1, y0), (1, y1)):
            for nm, a, b, c, d in (('base', vx0 - 0.014, vz0 - 0.014, vx1 + 0.014, vz0), ('topo', vx0 - 0.014, vz1, vx1 + 0.014, vz1 + 0.014),
                                   ('esq', vx0 - 0.014, vz0, vx0, vz1), ('dir', vx1, vz0, vx1 + 0.014, vz1)):
                plate(f'Moldura da veneziana {nm} {face:+d}', 'folha', a, c, y, face, b, d, t=0.0035, off=0.0)
        veneziana('Veneziana', 'folha_clara', vx0, vx1, vz0, vz1, (y0 + y1) / 2)
    else:
        box('Folha', 'folha', x0, y0, z0, x1, y1, z1, r=0.0025, seg=3)
    hinges(P, (0.25, 1.05, 1.90))
    for face, y in ((-1, y0), (1, y1)):
        R._kick_plate(f'Chapa de protecao {face:+d}', x0 + 0.035, x1 - 0.035, y, face, z0 + 0.006, z0 + 0.206)
    if secondary:
        for face, y in ((-1, y0), (1, y1)):
            pull_bar(f'Puxador {face:+d}', x1 - 0.075, y, face)
        # Flush bolt keepers: a small steel strike plate in the leaf edge, top and bottom (the bolts run into the frame).
        for zb in (z1 - 0.12, z0 + 0.08):
            box(f'Espelho do fecho de embutir {zb:.2f}', 'inox', x1 - 0.0004, y0 + 0.012, zb, x1 + 0.0006, y1 - 0.012, zb + 0.10, r=0.0003, seg=1)
    else:
        hx = x1 - 0.065
        for face, y in ((-1, y0), (1, y1)):
            if variant == 'descanso':
                round_rose_lever(f'Macaneta {face:+d}', hx, y, face)
            else:
                R._lever_set(f'Fechadura {face:+d}', hx, y, face)
    # The meeting edge of the active leaf carries a latch strike plate.
    if not secondary:
        box('Testa da fechadura', 'inox', x1 - 0.0006, y0 + 0.010, 0.92, x1 + 0.0004, y1 - 0.010, 1.08, r=0.0003, seg=1)


def leaf_B(secondary=False):
    """Technical-room steel leaf: louvres high and low for the equipment's ventilation, heavy kick plates, backplate lever."""
    P = PA
    wd = leaf_width(P, P['active']) if secondary else P['active']
    x0, x1, y0, y1, z0, z1 = leaf_dims(P, wd)
    ym = (y0 + y1) / 2
    lx0, lx1 = x0 + 0.17, x1 - (0.22 if not secondary else 0.12)
    if secondary:
        lx0 = x0 + 0.10
    holes = [(lx0, lx1, 0.30, 0.50), (lx0, lx1, 1.62, 1.84)]
    opening_leaf('Folha', 'folha', x0, x1, y0, y1, z0, z1, holes)
    for hx0, hx1, hz0, hz1 in holes:
        for face, y in ((-1, y0), (1, y1)):
            for nm, a, b, c, d in (('base', hx0 - 0.014, hz0 - 0.014, hx1 + 0.014, hz0), ('topo', hx0 - 0.014, hz1, hx1 + 0.014, hz1 + 0.014),
                                   ('esq', hx0 - 0.014, hz0, hx0, hz1), ('dir', hx1, hz0, hx1 + 0.014, hz1)):
                plate(f'Moldura da veneziana {nm} {hz0:.2f} {face:+d}', 'folha', a, c, y, face, b, d, t=0.0035, off=0.0)
        veneziana(f'Veneziana {hz0:.2f}', 'folha_clara', hx0, hx1, hz0, hz1, ym)
    hinges(P, (0.25, 1.05, 1.90))
    for face, y in ((-1, y0), (1, y1)):
        R._kick_plate(f'Chapa de protecao {face:+d}', x0 + 0.035, x1 - 0.035, y, face, z0 + 0.006, z0 + 0.206)
    if secondary:
        for face, y in ((-1, y0), (1, y1)):
            pull_bar(f'Puxador {face:+d}', x1 - 0.075, y, face)
        for zb in (z1 - 0.12, z0 + 0.08):
            box(f'Espelho do fecho de embutir {zb:.2f}', 'inox', x1 - 0.0004, y0 + 0.012, zb, x1 + 0.0006, y1 - 0.012, zb + 0.10, r=0.0003, seg=1)
    else:
        hx = x1 - 0.065
        for face, y in ((-1, y0), (1, y1)):
            R._lever_set(f'Fechadura {face:+d}', hx, y, face)
        box('Testa da fechadura', 'inox', x1 - 0.0006, y0 + 0.010, 0.92, x1 + 0.0004, y1 - 0.010, 1.08, r=0.0003, seg=1)


# ---------------------------------------------------------------- family C: clinical double leaf

C_WIN = (0.205, 0.515, 1.12, 1.68)   # vision port x0, x1, z0, z1 in the leaf frame


def leaf_C():
    P = PC
    x0, x1, y0, y1, z0, z1 = leaf_dims(P, leaf_width(P))
    wx0, wx1, wz0, wz1 = C_WIN
    opening_leaf('Folha', 'folha', x0, x1, y0, y1, z0, z1, [(wx0, wx1, wz0, wz1)])
    # Stainless vision-port frame on both faces; the glass is its own unbaked asset.
    for face, y in ((-1, y0), (1, y1)):
        fw = 0.022
        for nm, a, b, c, d in (('base', wx0 - fw, wz0 - fw, wx1 + fw, wz0), ('topo', wx0 - fw, wz1, wx1 + fw, wz1 + fw),
                               ('esq', wx0 - fw, wz0, wx0, wz1), ('dir', wx1, wz0, wx1 + fw, wz1)):
            plate(f'Moldura do visor {nm} {face:+d}', 'inox', a, c, y, face, b, d, t=0.004, off=0.0, r=0.001)
        # Full-width stretcher bumper at trolley height (soft PVC, rounded) and stainless kick plate.
        plate(f'Protetor de maca {face:+d}', 'borracha', x0 + 0.012, x1 - 0.012, y, face, 0.84, 0.98, t=0.011, off=0.0, r=0.004)
        plate(f'Chapa de chute {face:+d}', 'inox', x0 + 0.02, x1 - 0.02, y, face, z0 + 0.006, 0.40, t=0.0015, off=0.0004)
        for sx in (x0 + 0.04, (x0 + x1) / 2, x1 - 0.04):
            for sz in (z0 + 0.03, 0.37):
                cyl(f'Parafuso da chapa {face:+d} {sx:.2f} {sz:.2f}', 'inox', (sx, y + face * 0.0022, sz), 0.0028, 0.0008, axis=(0, 1, 0), seg=12, r=0.0003)
        # Push plate at hand height, by the meeting edge.
        plate(f'Chapa de empurrar {face:+d}', 'inox', x1 - 0.17, x1 - 0.035, y, face, 0.98, 1.10, t=0.0015, off=0.0004)
    hinges(P, (0.25, 1.05, 1.90), r=0.0085, prefix='Dobradica pesada')
    # Meeting edge: soft seal on the leaf edge and a flush rubber bumper (opening leaves close against each other).
    box('Veda da testa', 'borracha', x1 - 0.001, y0 + 0.006, z0 + 0.03, x1 + 0.0015, y1 - 0.006, z1 - 0.03, r=0.0007, seg=1)


def glass_C():
    P = PC
    x0, x1, y0, y1, z0, z1 = leaf_dims(P, leaf_width(P))
    wx0, wx1, wz0, wz1 = C_WIN
    t = 0.004
    ym = (y0 + y1) / 2
    g = box('Vidro', 'vidro', wx0 - 0.004, ym - t, wz0 - 0.004, wx1 + 0.004, ym + t, wz1 + 0.004, r=0.0008, seg=1)
    planar_uv_glass(g, wx0, wx1, wz0, wz1)


def planar_uv_glass(g, x0, x1, z0, z1):
    R.planar_uv(g, 0, 2, x0 - 0.004, x1 + 0.004, z0 - 0.004, z1 + 0.004, 0.0, 0.96, 0.0, 1.0, edge_uv=(0.985, 0.5), normal_axis=1)


# ---------------------------------------------------------------- family D: insulated cold-room leaf

def leaf_D():
    P = PD
    x0, x1, y0, y1, z0, z1 = leaf_dims(P, leaf_width(P))
    ym = (y0 + y1) / 2
    box('Painel isolante', 'painel', x0, y0, z0, x1, y1, z1, r=0.004, seg=3)
    # Folded stainless perimeter channel (visible 2 mm proud on both faces).
    for face, y in ((-1, y0), (1, y1)):
        cw = 0.030
        for nm, a, b, c, d in (('base', x0, z0, x1, z0 + cw), ('topo', x0, z1 - cw, x1, z1), ('dobradica', x0, z0, x0 + cw, z1),
                               ('fecho', x1 - cw, z0, x1, z1)):
            plate(f'Requadro {nm} {face:+d}', 'inox', a, c, y, face, b, d, t=0.0025, off=0.0, r=0.0012)
        # Full-height lower kick plate for trolleys and a stretcher-height bumper rail.
        plate(f'Chapa de impacto {face:+d}', 'inox', x0 + cw, x1 - cw, y, face, z0 + cw, 0.45, t=0.0015, off=0.0025)
        plate(f'Defensa de maca {face:+d}', 'borracha', x0 + cw, x1 - cw, y, face, 0.84, 0.97, t=0.014, off=0.0025, r=0.005)
    # Heavy hinges proud of the swing face (cam-rise cold-room hinges): knuckle plus leaf and frame wings.
    for z in (0.28, 1.05, 1.88):
        cyl(f'Dobradica {z:.2f} - eixo', 'inox', (0.0, 0.0, z), PD['knuckle_r'], 0.13, seg=24, r=0.002)
        box(f'Dobradica {z:.2f} - aba da folha', 'inox', 0.0, y1 - 0.005, z - 0.065, 0.075, y1 + 0.0035, z + 0.065, r=0.0015, seg=1)
        for sx in (0.02, 0.058):
            for dz in (-0.04, 0.04):
                cyl(f'Parafuso {z:.2f} {sx} {dz}', 'inox', (sx, y1 + 0.0042, z + dz), 0.0045, 0.0014, axis=(0, 1, 0), seg=12, r=0.0004)
    # Cam-action latch on the swing face by the meeting edge: stainless body, lever and keeper hook on the frame side.
    hx = x1 - 0.062
    box('Fecho de came - base', 'inox', hx - 0.030, y1, 0.98, hx + 0.030, y1 + 0.016, 1.22, r=0.004, seg=2)
    cyl('Fecho de came - eixo', 'inox_escuro', (hx, y1 + 0.020, 1.10), 0.014, 0.012, axis=(0, 1, 0), seg=24, r=0.002)
    tube('Fecho de came - alavanca', 'inox', [(hx, y1 + 0.026, 1.10), (hx, y1 + 0.052, 1.10), (hx - 0.16, y1 + 0.052, 1.10),
                                              (hx - 0.16, y1 + 0.036, 1.10)], 0.0105, fillet=0.02, res=3)
    box('Fecho de came - gancho', 'inox', x1 - 0.020, y1 + 0.001, 1.04, x1 + 0.0012, y1 + 0.020, 1.16, r=0.002, seg=1)
    # Inside (back face): safety release - mushroom button on a red plate, with its marking plate.
    box('Placa do destravamento', 'sinal', hx - 0.045, y0 - 0.006, 0.93, hx + 0.045, y0, 1.15, r=0.003, seg=2)
    cyl('Cogumelo de emergencia', 'sinal_escuro', (hx, y0 - 0.018, 1.04), 0.026, 0.018, axis=(0, -1, 0), seg=28, r=0.006, radius_top=0.033)
    tube('Puxador interno', 'inox', [(hx - 0.22, y0 - 0.006, 0.80), (hx - 0.22, y0 - 0.050, 0.80), (hx - 0.22, y0 - 0.050, 1.28),
                                     (hx - 0.22, y0 - 0.006, 1.28)], 0.0095, fillet=0.02, res=3)
    # Rubber blade along the bottom and soft seal on the meeting edge.
    box('Lamina de vedacao inferior', 'veda', x0 + 0.01, y0 + 0.006, z0 - 0.0065, x1 - 0.01, y1 - 0.006, z0 + 0.012, r=0.0015, seg=1)
    box('Veda da testa', 'veda', x1 - 0.001, y0 + 0.006, z0 + 0.03, x1 + 0.0035, y1 - 0.006, z1 - 0.03, r=0.002, seg=1)


# ---------------------------------------------------------------- materials

def paint(name, rgb, kick=0.22, rough=0.40, spots=()):
    return M.laminado(name, rgb, kick=kick, rough=rough, grime_spots=spots)


def lever_spots(P, width, z=1.0):
    x0, x1, y0, y1, z0, z1 = leaf_dims(P, width)
    hx = x1 - 0.065
    return (((hx, y0 - 0.01, z), 0.13), ((hx, y1 + 0.01, z), 0.13), (((hx - 0.06), 0.0, z + 0.12), 0.22))


def steel(name, rgb, rough=0.30):
    return M.metal(name, rgb, rough)


def mats_for(asset):
    inox = steel('Inox escovado - portas', (0.74, 0.74, 0.73), 0.30)
    inox_e = steel('Inox escuro - portas', (0.36, 0.36, 0.36), 0.40)
    fenda = M.tela('Fenda escura - portas')
    borr = M.borracha('Borracha - portas')
    base = {'inox': inox, 'inox_escuro': inox_e, 'fenda': fenda, 'borracha': borr}
    if asset == 'PortaInstMarco':
        return {**base, 'marco': paint('Pintura acetinada - marco institucional', (0.29, 0.30, 0.31), 0.18, 0.42),
                'granito': M.granito('Granito - soleira institucional', -10.0, -10.0)}
    inst = {'Adm': (0.60, 0.62, 0.62), 'Descanso': (0.56, 0.62, 0.58), 'Deposito': (0.64, 0.62, 0.55)}
    for k, rgb in inst.items():
        if asset in ('PortaInst' + k, 'PortaInst' + k + 'Sec'):
            sec = asset.endswith('Sec')
            wd = leaf_width(PA, PA['active']) if sec else PA['active']
            light = tuple(min(1.0, c * 1.1) for c in rgb)
            return {**base, 'folha': paint(f'Pintura acetinada - folha {k.lower()}', rgb, 0.22, 0.40, () if sec else lever_spots(PA, wd)),
                    'folha_clara': paint(f'Pintura - lamina {k.lower()}', light, 0.05, 0.42)}
    if asset in ('PortaTecAtiva', 'PortaTecSec'):
        rgb = (0.34, 0.42, 0.39)
        wd = leaf_width(PA, PA['active']) if asset.endswith('Sec') else PA['active']
        return {**base, 'folha': paint('Esmalte sintetico - folha tecnica', rgb, 0.30, 0.45, () if asset.endswith('Sec') else lever_spots(PA, wd)),
                'folha_clara': paint('Esmalte - laminas tecnicas', tuple(c * 1.15 for c in rgb), 0.05, 0.48)}
    if asset == 'PortaClinicaMarco':
        return {**base, 'marco': paint('Pintura epoxi - marco clinico', (0.60, 0.65, 0.63), 0.16, 0.38),
                'granito': steel('Inox escovado - soleira clinica', (0.70, 0.70, 0.69), 0.34)}
    if asset == 'PortaClinicaFolha':
        return {**base, 'folha': paint('Laminado - folha clinica', (0.71, 0.76, 0.73), 0.30, 0.34,
                                       ((( 0.62, 0.0, 1.04), 0.16), ((0.62, 0.0, 0.62), 0.28)))}
    if asset == 'PortaClinicaVidro':
        return {'vidro': M.tela('vidro')}
    if asset == 'PortaFriaMarco':
        return {**base, 'marco': paint('Chapa pre-pintada - marco frigorifico', (0.66, 0.69, 0.68), 0.15, 0.36),
                'granito': steel('Inox escovado - soleira frigorifica', (0.68, 0.68, 0.67), 0.36),
                'veda': M.borracha('Gaxeta EPDM')}
    if asset == 'PortaFriaFolha':
        return {**base, 'painel': paint('Chapa pre-pintada - painel frigorifico', (0.80, 0.82, 0.82), 0.30, 0.34,
                                        (((0.62, 0.0, 1.10), 0.16), ((0.40, 0.09, 1.10), 0.2))),
                'veda': M.borracha('Gaxeta EPDM'),
                'sinal': paint('Placa vermelha de emergencia', (0.55, 0.05, 0.04), 0.02, 0.45),
                'sinal_escuro': paint('Cogumelo vermelho', (0.40, 0.03, 0.03), 0.02, 0.35)}
    raise KeyError(asset)


# ---------------------------------------------------------------- assets

def _reg(fn, *a, **k):
    return lambda: fn(*a, **k)


BUILDERS = {
    'PortaInstMarco': lambda: frame(PA, 'granito'),
    'PortaInstAdm': lambda: leaf_A('adm'), 'PortaInstAdmSec': lambda: leaf_A('adm', True),
    'PortaInstDescanso': lambda: leaf_A('descanso'), 'PortaInstDescansoSec': lambda: leaf_A('descanso', True),
    'PortaInstDeposito': lambda: leaf_A('deposito'), 'PortaInstDepositoSec': lambda: leaf_A('deposito', True),
    'PortaTecAtiva': lambda: leaf_B(), 'PortaTecSec': lambda: leaf_B(True),
    'PortaClinicaMarco': lambda: frame(PC, 'granito'),
    'PortaClinicaFolha': leaf_C, 'PortaClinicaVidro': glass_C,
    'PortaFriaMarco': lambda: frame(PD, 'granito', gasket=True),
    'PortaFriaFolha': leaf_D,
}
UNBAKED = ('PortaClinicaVidro',)
SIZES = {n: (2048 if n in ('PortaInstMarco', 'PortaFriaFolha') else 1024) for n in BUILDERS}
SIZES['PortaClinicaFolha'] = 1024


def main():
    os.makedirs(OUT + '/Texturas', exist_ok=True)
    os.makedirs(SRC + '/revisao', exist_ok=True)
    for o in list(bpy.data.objects):
        if not ONLY or any(o in list(R._coll(n).objects) or o.name == n for n in ONLY):
            bpy.data.objects.remove(o, do_unlink=True)
    stats = {}
    objs = {}
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
        if name in UNBAKED:
            R.planar_uv(ob, 0, 2, C_WIN[0] - 0.004, C_WIN[1] + 0.004, C_WIN[2] - 0.004, C_WIN[3] + 0.004, 0.0, 0.96, 0.0, 1.0,
                        edge_uv=(0.985, 0.5), normal_axis=1)
            density = 0
        else:
            density = R.layout_uvs(ob, SIZES[name], margin_px=8)
        stats[name] = {'triangles': len(ob.data.polygons), 'texelsPerMetre': density, 'size': [round(v, 4) for v in ob.dimensions]}
        R.export_fbx(ob, OUT + '/' + name + '.fbx', keep_slots=name in UNBAKED)
        ob.location.x = i * 3
        objs[name] = ob
    bpy.ops.wm.save_as_mainfile(filepath=SRC + '/Portas.blend')
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
    bpy.ops.wm.save_as_mainfile(filepath=SRC + '/Portas.blend')


if __name__ == '__main__':
    main()

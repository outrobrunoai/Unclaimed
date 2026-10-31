"""Electrical infrastructure on the north wall of the technical room, built in the wall's own frame: origin on the
finished wall face at floor level and x = world x (Unity: the prefab sits at (0, 0, wall z) with no rotation), wall at
y = 0, room towards -Y, z height.
A 150 x 50 mm perforated galvanized cable tray at 2.70 m on wall brackets (mao-francesa) collects the boards: each board's
conduit rises straight from its top into a conduit outlet under the tray (rigid galvanized steel, NBR 5624, unsupported
runs of about 1 m), the cold-room sensor cable climbs the wall in nylon clips and drops into the tray over its back
side, and three conduits leave the tray's front side through lateral outlets and turn up into the slab, where the
circuits run embedded to the rooms. The main board's feeder comes from the floor. Needs R, F, Q (pecas_quadros)."""
import math
from mathutils import Vector

WALL_Z = 8.875                    # finished face of the north wall (Unity world z), for reference only
SLAB = 3.20
TRAY = dict(x0=-3.75, x1=-0.65, z=2.70, y0=-0.17, y1=-0.02, h=0.05, t=0.0015, lip=0.010)
BRACKETS = (-3.55, -2.20, -0.85)
SPLICE = -1.05
# board centres along the wall (world x), as Layout.json places them
BOARDS = {'QuadroGeral': -3.00, 'PainelContingencia': -1.95, 'QuadroCamaraFria': -1.05}
# conduits from each board's top (local x of the board, radius), from pecas_quadros
FROM_BOARDS = [('QuadroGeral', -0.16, 0.0134), ('QuadroGeral', 0.16, 0.0134), ('PainelContingencia', 0.10, 0.0134),
               ('QuadroCamaraFria', -0.08, 0.0134)]
TO_SLAB = (-3.45, -2.50, -1.40)


def eletrocalha():
    T = TRAY
    x0, x1, z, y0, y1, h, t, lip = T['x0'], T['x1'], T['z'], T['y0'], T['y1'], T['h'], T['t'], T['lip']
    R.box('Eletrocalha fundo', 'calha', x0, y0, z, x1, y1, z + t, r=0.0, seg=1)
    for nm, ya, yb, lipdir in (('frente', y0, y0 + t, 1), ('fundo', y1 - t, y1, -1)):
        R.box(f'Eletrocalha lateral {nm}', 'calha', x0, ya, z, x1, yb, z + h, r=0.0, seg=1)
        # inward return lip at the top edge
        yl0, yl1 = sorted((ya if lipdir > 0 else yb, (ya if lipdir > 0 else yb) + lipdir * lip))
        R.box(f'Eletrocalha aba {nm}', 'calha', x0, yl0, z + h - t, x1, yl1, z + h, r=0.0, seg=1)
    # end caps (tampa terminal) bolted on the outside of the U
    for s, xe in ((-1, x0), (1, x1)):
        xa, xb = sorted((xe, xe + s * t))
        R.box(f'Tampa terminal {s:+d}', 'zinco', xa, y0 - t, z - t, xb, y1 + t, z + h, r=0.0, seg=1)
        for k, yy in enumerate((y0 + 0.03, y1 - 0.03)):
            R.cyl(f'Tampa terminal {s:+d} parafuso {k}', 'zinco', (xe + s * 0.002, yy, z + 0.022), 0.0045, 0.003, axis=(1, 0, 0), seg=6, r=0.0004)
    # splice plates (tala de emenda) on both sides, four bolts each
    for nm, ys in (('frente', y0 - t), ('fundo', y1 + t)):
        ya, yb = sorted((ys, ys + (-1 if nm == 'frente' else 1) * 0.0015))
        R.box(f'Tala {nm}', 'zinco', SPLICE - 0.05, ya, z + 0.006, SPLICE + 0.05, yb, z + h - 0.006, r=0.0003, seg=1)
        for k, dx in enumerate((-0.035, -0.015, 0.015, 0.035)):
            yh = ya if nm == 'frente' else yb
            R.cyl(f'Tala {nm} parafuso {k}', 'zinco', (SPLICE + dx, yh + (-0.0015 if nm == 'frente' else 0.0015), z + h / 2), 0.0042, 0.003,
                  axis=(0, 1, 0), seg=6, r=0.0004)


def mao_francesa(k, x):
    """Bolted wall bracket of perforated galvanized strip: wall leg, arm under the tray and a diagonal brace."""
    T = TRAY
    zt = T['z']
    R.box(f'Suporte {k} perna', 'zinco', x - 0.019, -0.0025, zt - 0.26, x + 0.019, 0.0, zt + 0.005, r=0.0004, seg=1)
    R.box(f'Suporte {k} braco', 'zinco', x - 0.019, T['y0'] - 0.03, zt - 0.022, x + 0.019, -0.0025, zt, r=0.0006, seg=1)
    R.box(f'Suporte {k} braco aba', 'zinco', x - 0.019, T['y0'] - 0.03, zt - 0.022, x - 0.017, -0.0025, zt - 0.002, r=0.0, seg=1)
    F.bar(f'Suporte {k} diagonal', 'zinco', (x, -0.004, zt - 0.24), (x, T['y0'] - 0.01, zt - 0.024), 0.026, 0.003, r=0.0005, up=(1, 0, 0))
    for j, zz in enumerate((zt - 0.22, zt - 0.08)):
        R.cyl(f'Suporte {k} chumbador {j}', 'zinco', (x, -0.0045, zz), 0.0068, 0.004, axis=(0, 1, 0), seg=6, r=0.0005)
        R.cyl(f'Suporte {k} arruela {j}', 'zinco', (x, -0.0028, zz), 0.0090, 0.0010, axis=(0, 1, 0), seg=16, r=0.0002)
    R.cyl(f'Suporte {k} fixacao calha', 'zinco', (x, (T['y0'] + T['y1']) / 2, zt + T['t'] + 0.0015), 0.0042, 0.003, seg=6, r=0.0004)


def eletroduto(name, pts, r, fillet):
    R.tube(name, 'eletroduto', pts, r, fillet=fillet, res=3)


def saida_vertical(name, x, y, r):
    """Conduit outlet under the tray: flanged box spigot with its bolt heads, and the conduit's coupling."""
    z = TRAY['z']
    R.box(f'{name} flange', 'zinco', x - 0.024, y - 0.024, z - 0.003, x + 0.024, y + 0.024, z, r=0.0006, seg=1)
    R.cyl(f'{name} bocal', 'zinco', (x, y, z - 0.016), r + 0.0030, 0.026, seg=24, r=0.0008)
    for k, (dx, dy) in enumerate(((-0.017, -0.017), (0.017, 0.017))):
        R.cyl(f'{name} parafuso {k}', 'zinco', (x + dx, y + dy, z - 0.0045), 0.0035, 0.003, seg=6, r=0.0003)


def saida_lateral(name, x, r):
    """Lateral outlet on the tray's front side: flange on the side wall and a short spigot out towards the room."""
    y = TRAY['y0']
    zc = TRAY['z'] + TRAY['h'] / 2
    R.box(f'{name} flange', 'zinco', x - 0.024, y - 0.003, zc - 0.021, x + 0.024, y, zc + 0.021, r=0.0006, seg=1)
    R.cyl(f'{name} bocal', 'zinco', (x, y - 0.015, zc), r + 0.0030, 0.024, axis=(0, 1, 0), seg=24, r=0.0008)
    return y - 0.027, zc


def abracadeira_teto(name, x, y, r):
    """D-clamp holding a conduit just under the slab."""
    z = SLAB - 0.05
    R.tube(f'{name} aro', 'zinco', [(x - r - 0.0015, y + 0.0, z), (x - r - 0.0015, y - r - 0.002, z), (x + r + 0.0015, y - r - 0.002, z),
                                     (x + r + 0.0015, y + r + 0.012, z)], 0.0016, fillet=r * 0.9, res=2)
    R.box(f'{name} base', 'zinco', x + r - 0.002, y + r + 0.006, z - 0.010, x + r + 0.022, y + r + 0.010, z + 0.010, r=0.0004, seg=1)


def infraestrutura():
    eletrocalha()
    for k, x in enumerate(BRACKETS):
        mao_francesa(k, x)
    z_tray = TRAY['z']
    # conduits from the boards up into the tray
    for k, (b, lx, r) in enumerate(FROM_BOARDS):
        q = Q.QUADROS[b]
        x = BOARDS[b] + lx
        y = -q['D'] / 2
        z0 = q['z0'] + q['H'] + 0.021          # top of the coupling on the board (Q.bucha_arruela)
        saida_vertical(f'Saida {k}', x, y, r)
        eletroduto(f'Eletroduto quadro {k}', [(x, y, z0 - 0.004), (x, y, z_tray - 0.026)], r, 0.0)
        # couplings every 3 m are not needed on these 1 m runs; a union just above the board makes the run serviceable
        R.cyl(f'Eletroduto quadro {k} luva', 'zinco', (x, y, z0 + 0.05), r + 0.0022, 0.03, seg=24, r=0.0010)
    # main feeder from the floor into the bottom of the QDG (1")
    qg = Q.QUADROS['QuadroGeral']
    x, y = BOARDS['QuadroGeral'], -qg['D'] / 2
    eletroduto('Alimentador', [(x, y, -0.02), (x, y, qg['z0'] - 0.020)], 0.0167, 0.0)
    R.cyl('Alimentador luva piso', 'zinco', (x, y, 0.012), 0.0195, 0.024, seg=24, r=0.0010)
    R.cyl('Alimentador abracadeira', 'zinco', (x, y, 0.45), 0.0185, 0.012, seg=24, r=0.0008)
    R.box('Alimentador abracadeira base', 'zinco', x - 0.012, y + 0.016, 0.44, x + 0.012, -0.0015, 0.46, r=0.0005, seg=1)
    # circuits leaving the tray for the slab: lateral outlet, 90 deg bend, vertical run into the concrete
    for k, x in enumerate(TO_SLAB):
        y, zc = saida_lateral(f'Saida laje {k}', x, 0.0134)
        yv = y - 0.050
        eletroduto(f'Eletroduto laje {k}', [(x, y + 0.004, zc), (x, yv, zc), (x, yv, SLAB + 0.02)], 0.0134, 0.045)
        R.cyl(f'Eletroduto laje {k} luva', 'zinco', (x, yv, SLAB - 0.012), 0.0156, 0.024, seg=24, r=0.0010)
        abracadeira_teto(f'Abracadeira laje {k}', x, yv, 0.0134)
    # cold-room sensor cable: from the gland on the board top, clipped up the wall, over the tray's back side
    qc = Q.QUADROS['QuadroCamaraFria']
    x = BOARDS['QuadroCamaraFria'] + 0.11
    zs = qc['z0'] + qc['H'] + 0.017
    yw = -0.0045
    pts = [(x, -0.035, zs), (x, -0.030, zs + 0.05), (x, yw, zs + 0.16), (x, yw, z_tray + 0.03), (x, yw - 0.004, z_tray + 0.068),
           (x - 0.012, -0.045, z_tray + 0.06), (x - 0.04, -0.07, z_tray + 0.004)]
    R.cable('Cabo do sensor', 'cabo_pp', pts, 0.0032, per_seg=8)
    for j, zc in enumerate((zs + 0.30, zs + 0.62, zs + 0.94)):
        R.box(f'Presilha {j}', 'nylon_branco', x - 0.006, -0.0015, zc - 0.007, x + 0.006, 0.0, zc + 0.007, r=0.0005, seg=1)
        R.tube(f'Presilha {j} aro', 'nylon_branco', [(x - 0.0045, -0.0015, zc), (x - 0.0045, yw - 0.0042, zc), (x + 0.0045, yw - 0.0042, zc),
                                                     (x + 0.0045, -0.0015, zc)], 0.0011, fillet=0.003, res=2)
        R.cyl(f'Presilha {j} prego', 'zinco', (x, -0.0018, zc + 0.0045), 0.0016, 0.001, axis=(0, 1, 0), seg=8, r=0.0)
    # cables visible in the open tray: a loose bundle along its floor
    for k, (dy, dz, rr) in enumerate(((-0.055, 0.009, 0.0075), (-0.075, 0.008, 0.0065), (-0.095, 0.010, 0.0085), (-0.115, 0.009, 0.007),
                                      (-0.065, 0.022, 0.006))):
        xs = [TRAY['x0'] + 0.05 + k * 0.01, -3.0, -2.2, -1.5, TRAY['x1'] - 0.08 - k * 0.012]
        pts = [(xx, dy + 0.006 * math.sin(i * 1.7 + k), z_tray + dz + 0.003 * math.cos(i * 2.1 + k)) for i, xx in enumerate(xs)]
        R.cable(f'Cabo na calha {k}', ('cabo_pp', 'cabo_preto', 'cabo_azul', 'cabo_preto', 'cabo_pp')[k], pts, rr, per_seg=6)

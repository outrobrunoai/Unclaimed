"""Mauro's maintenance bench against the north wall of the technical room: welded steel-tube frame with a lower shelf,
30 mm MDF top worn at the front edge, one drawer, and a 970 x 640 mm perforated steel tool board (Marcon PM-P size)
on spacers above it with a few hooked tools (three screwdrivers, universal and cutting pliers). On the top: a
multimeter, insulating tape and two reused coffee tins of screws and wall plugs labelled on masking tape.
Origin: floor, centre of the bench; the wall is y = 0 and the bench faces -Y. Labels (multimeter face, tins) are
atlas labels placed by Layout.json. Needs R, F."""
import math
from mathutils import Vector, Matrix

BANCADA = dict(W=1.20, D=0.60, H=0.90, top_t=0.030, tube=0.040, back_gap=0.02, shelf_z=0.16, drawer_w=0.46)
QUADRO = dict(W=0.97, H=0.64, z0=1.12, gap=0.022, pitch=0.025)


def bancada():
    B = BANCADA
    W, D, H, tt, tb = B['W'], B['D'], B['H'], B['top_t'], B['tube']
    y0, y1 = -B['back_gap'] - D, -B['back_gap']
    out = {'labels': []}
    # frame: four legs with feet, top rails front/back/sides, lower rails carrying the shelf
    for sx in (-1, 1):
        for sy, yy in ((-1, y0 + 0.03), (1, y1 - 0.03)):
            x = sx * (W / 2 - 0.04)
            R.box(f'Perna {sx:+d}{sy:+d}', 'tubo', x - tb / 2, yy - tb / 2, 0.012, x + tb / 2, yy + tb / 2, H - tt, r=0.003, seg=2)
            R.cyl(f'Sapata {sx:+d}{sy:+d}', 'borracha', (x, yy, 0.006), 0.022, 0.012, seg=16, r=0.002)
    zr = H - tt - 0.04
    for nm, (xa, ya, xb, yb) in {'frente': (-W / 2 + 0.04, y0 + 0.03 - 0.02, W / 2 - 0.04, y0 + 0.03 + 0.02),
                                 'fundo': (-W / 2 + 0.04, y1 - 0.05, W / 2 - 0.04, y1 - 0.01)}.items():
        R.box(f'Travessa {nm}', 'tubo', xa, ya, zr, xb, yb, H - tt, r=0.003, seg=2)
        R.box(f'Travessa baixa {nm}', 'tubo', xa, ya, B['shelf_z'] - 0.04, xb, yb, B['shelf_z'], r=0.003, seg=2)
    for sx in (-1, 1):
        x = sx * (W / 2 - 0.04)
        R.box(f'Travessa lateral {sx:+d}', 'tubo', x - 0.02, y0 + 0.05, zr, x + 0.02, y1 - 0.05, H - tt, r=0.003, seg=2)
        R.box(f'Travessa lateral baixa {sx:+d}', 'tubo', x - 0.02, y0 + 0.05, B['shelf_z'] - 0.04, x + 0.02, y1 - 0.05, B['shelf_z'], r=0.003, seg=2)
    R.box('Prateleira', 'chapa_bancada', -W / 2 + 0.06, y0 + 0.05, B['shelf_z'], W / 2 - 0.06, y1 - 0.05, B['shelf_z'] + 0.0015, r=0.0005, seg=1)
    # MDF top, edge banded
    R.box('Tampo', 'mdf', -W / 2, y0, H - tt, W / 2, y1, H, r=0.003, seg=2)
    # one drawer under the top, to the right, on its own runners box
    dw = B['drawer_w']
    dx = W / 2 - 0.08 - dw / 2
    R.box('Gaveta caixa', 'chapa_bancada', dx - dw / 2 - 0.01, y0 + 0.032, zr - 0.10, dx + dw / 2 + 0.01, y1 - 0.06, zr, r=0.002, seg=1)
    R.box('Gaveta frente', 'chapa_bancada', dx - dw / 2, y0 + 0.010, zr - 0.098, dx + dw / 2, y0 + 0.030, zr - 0.004, r=0.003, seg=2)
    R.tube('Gaveta puxador', 'cromo', [(dx - 0.06, y0 + 0.010, zr - 0.05), (dx - 0.06, y0 - 0.012, zr - 0.05), (dx + 0.06, y0 - 0.012, zr - 0.05),
                                      (dx + 0.06, y0 + 0.010, zr - 0.05)], 0.0045, fillet=0.010, res=2)
    # perforated tool board on four spacers
    q = QUADRO
    yb = -q['gap']
    R.box('Painel perfurado', 'perfurado', -q['W'] / 2, yb - 0.0012, q['z0'], q['W'] / 2, yb, q['z0'] + q['H'], r=0.0006, seg=1)
    for f, (fx, fz) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
        x, z = fx * (q['W'] / 2 - 0.03), q['z0'] + q['H'] / 2 + fz * (q['H'] / 2 - 0.03)
        R.cyl(f'Espacador {f}', 'zinco', (x, yb / 2, z), 0.007, -yb, axis=(0, 1, 0), seg=12, r=0.0)
        R.cyl(f'Espacador {f} parafuso', 'zinco', (x, yb - 0.0025, z), 0.0055, 0.0024, axis=(0, 1, 0), seg=16, r=0.001)
    ferramentas(yb - 0.0012)
    objetos_do_tampo(H, y0, y1, out)
    return out


# ---------------------------------------------------------------- tools on the board

def gancho(name, x, z, yb, reach=0.055, up=0.012):
    """Single wire hook: through the board, bent out and slightly up at the tip."""
    R.tube(name, 'zinco', [(x, yb + 0.004, z + 0.008), (x, yb - 0.002, z + 0.008), (x, yb - 0.004, z), (x, yb - reach, z), (x, yb - reach - 0.004, z + up)],
           0.0021, fillet=0.004, res=2)


def chave_de_fenda(name, x, z_ring, yb, length, handle_rgb_key, tip='fenda'):
    """Screwdriver hanging by its shaft in a ring hook, handle above the ring."""
    ry = yb - 0.035
    R.tube(f'{name} argola', 'zinco', [(x - 0.006, yb + 0.004, z_ring), (x - 0.006, ry + 0.002, z_ring), (x - 0.006, ry - 0.006, z_ring),
                                       (x + 0.006, ry - 0.006, z_ring), (x + 0.006, ry + 0.002, z_ring), (x + 0.006, yb + 0.004, z_ring)],
           0.0018, fillet=0.005, res=2)
    hl = 0.105
    prof = [(0.0, 0.0), (0.0075, 0.0), (0.0115, 0.006), (0.0135, 0.030), (0.0130, 0.075), (0.0140, 0.095), (0.0110, hl - 0.002), (0.0, hl)]
    R.lathe(f'{name} cabo', handle_rgb_key, prof, seg=20, r=0.0, matrix=Matrix.Translation((x, ry, z_ring + 0.004)))
    R.cyl(f'{name} anel', 'borracha', (x, ry, z_ring + 0.004 + 0.052), 0.0140, 0.030, seg=20, r=0.0015)
    R.cyl(f'{name} haste', 'cromo', (x, ry, z_ring + 0.004 - length / 2), 0.0030, length, seg=12, r=0.0004)
    zt = z_ring + 0.004 - length
    if tip == 'fenda':
        R.hull(f'{name} ponta', 'cromo', [(x + sx * 0.0030, ry + sy * 0.0030, zt + 0.012) for sx in (-1, 1) for sy in (-1, 1)] +
               [(x + sx * 0.0034, ry + sy * 0.0006, zt) for sx in (-1, 1) for sy in (-1, 1)], r=0.0002, seg=1)
    else:
        R.cyl(f'{name} ponta', 'cromo', (x, ry, zt + 0.004), 0.0030, 0.008, seg=8, r=0.0003, radius_top=0.0030)
        R.cyl(f'{name} ponta cruz', 'cromo', (x, ry, zt - 0.002), 0.0008, 0.006, seg=8, r=0.0, radius_top=0.0030)


def alicate(name, x, z_pin, yb, cutting=False, grip='punho_vermelho'):
    """Pliers hanging jaws-up over two pins: insulated handles spread downwards from the joint."""
    hy = yb - 0.045
    for s in (-1, 1):
        gancho(f'{name} pino {s:+d}', x + s * 0.022, z_pin, yb, reach=0.050, up=0.010)
    zj = z_pin + 0.075             # joint
    # handles: steel core visible near the joint, PVC sleeves below
    for s in (-1, 1):
        a = (x + s * 0.006, hy, zj - 0.012)
        b = (x + s * 0.024, hy, z_pin + 0.006)
        c = (x + s * 0.030, hy, z_pin - 0.115)
        F.bar(f'{name} haste {s:+d}', 'aco_ferramenta', a, b, 0.012, 0.006, r=0.0015, up=(0, 1, 0))
        F.bar(f'{name} punho {s:+d}', grip, (b[0] - s * 0.001, hy, b[2] + 0.01), c, 0.017, 0.012, r=0.004, up=(0, 1, 0))
    R.cyl(f'{name} articulacao', 'aco_ferramenta', (x, hy, zj), 0.010, 0.012, axis=(0, 1, 0), seg=20, r=0.0015)
    R.cyl(f'{name} rebite', 'cromo', (x, hy - 0.0062, zj), 0.0045, 0.0012, axis=(0, 1, 0), seg=16, r=0.0004)
    if cutting:   # diagonal cutter: short pointed jaws
        R.hull(f'{name} mordente', 'aco_ferramenta', [(x - 0.010, hy + sy * 0.005, zj + 0.004) for sy in (-1, 1)] +
               [(x + 0.010, hy + sy * 0.005, zj + 0.004) for sy in (-1, 1)] + [(x - 0.002, hy + sy * 0.003, zj + 0.040) for sy in (-1, 1)] +
               [(x + 0.002, hy + sy * 0.003, zj + 0.040) for sy in (-1, 1)], r=0.0012, seg=1)
        R.box(f'{name} corte', 'escuro', x - 0.0004, hy - 0.0052, zj + 0.012, x + 0.0004, hy + 0.0052, zj + 0.040, r=0.0, seg=1)
    else:         # universal pliers: flat gripping jaw over the cutter
        R.hull(f'{name} mordente', 'aco_ferramenta', [(x + sx * 0.011, hy + sy * 0.005, zj + 0.004) for sx in (-1, 1) for sy in (-1, 1)] +
               [(x + sx * 0.0045, hy + sy * 0.004, zj + 0.058) for sx in (-1, 1) for sy in (-1, 1)], r=0.0012, seg=1)
        R.box(f'{name} fenda', 'escuro', x - 0.0004, hy - 0.0047, zj + 0.020, x + 0.0004, hy + 0.0047, zj + 0.058, r=0.0, seg=1)


def ferramentas(yb):
    q = QUADRO
    z = q['z0']
    chave_de_fenda('Chave fenda grande', -0.36, z + 0.42, yb, 0.150, 'punho_amarelo')
    chave_de_fenda('Chave fenda media', -0.30, z + 0.42, yb, 0.100, 'punho_amarelo')
    chave_de_fenda('Chave philips', -0.24, z + 0.42, yb, 0.100, 'punho_amarelo', tip='philips')
    alicate('Alicate universal', -0.05, z + 0.26, yb, grip='punho_vermelho')
    alicate('Alicate de corte', 0.09, z + 0.26, yb, cutting=True, grip='punho_azul')
    # a free hook left where a tool is missing, and one carrying the insulating-tape roll's spare
    gancho('Gancho livre', 0.24, z + 0.40, yb)
    gancho('Gancho fita', 0.36, z + 0.40, yb, reach=0.065)
    R.lathe('Fita isolante reserva', 'fita_isolante', [(0.016, -0.0095), (0.025, -0.0095), (0.025, 0.0095), (0.016, 0.0095)], seg=28, r=0.0006,
            matrix=Matrix.Translation((0.36, yb - 0.045, z + 0.40 + 0.0021 - 0.016)) @ Matrix.Rotation(math.pi / 2, 4, 'X'))


# ---------------------------------------------------------------- on the top

def objetos_do_tampo(H, y0, y1, out):
    # digital multimeter in its yellow rubber holster, lying face up, probes coiled beside it
    mx, my = -0.32, y0 + 0.20
    w, d, h = 0.085, 0.165, 0.040
    R.box('Multimetro capa', 'capa_multimetro', mx - w / 2, my - d / 2, H, mx + w / 2, my + d / 2, H + h, r=0.010, seg=3)
    R.box('Multimetro frente', 'plastico_escuro', mx - w / 2 + 0.007, my - d / 2 + 0.008, H + h - 0.004, mx + w / 2 - 0.007, my + d / 2 - 0.008,
          H + h + 0.0005, r=0.002, seg=1)
    out['labels'].append(('multimetro', mx, H + h + 0.0007, my, 90.0))
    R.cyl('Multimetro chave', 'plastico_escuro', (mx, my - 0.012, H + h + 0.004), 0.019, 0.007, seg=28, r=0.002)
    R.box('Multimetro chave aleta', 'plastico_escuro', mx - 0.004, my - 0.030, H + h + 0.006, mx + 0.004, my + 0.006, H + h + 0.012, r=0.002, seg=2)
    for k, (dx, col) in enumerate(((-0.022, 'borne_preto'), (0.0, 'borne_vermelho'), (0.022, 'borne_vermelho'))):
        R.cyl(f'Multimetro borne {k}', col, (mx + dx, my - d / 2 + 0.022, H + h + 0.002), 0.0055, 0.005, seg=16, r=0.0008)
    for k, (dx, col, pts) in enumerate(((-0.022, 'cabo_preto', [(0.0, 0.0), (0.03, -0.05), (0.10, -0.06), (0.14, 0.02), (0.10, 0.08), (0.07, 0.05)]),
                                        (0.0, 'cabo_vermelho', [(0.0, 0.0), (0.04, -0.04), (0.12, -0.045), (0.16, 0.03), (0.12, 0.10), (0.09, 0.075)]))):
        base = Vector((mx + dx, my - d / 2 + 0.022, H + h + 0.006))
        path = [base, base + Vector((0, -0.02, 0.0))] + [Vector((mx + dx + px, my - d / 2 - 0.03 + py, H + 0.004)) for px, py in pts[1:]]
        R.cable(f'Ponta de prova {k} cabo', col, path, 0.0018, per_seg=8)
        tip = path[-1]
        R.cyl(f'Ponta de prova {k}', col, (tip.x + 0.04, tip.y, H + 0.006), 0.0055, 0.08, axis=(1, 0, 0), seg=14, r=0.002)
    # two reused coffee tins with screws and wall plugs; masking-tape labels in Mauro's hand
    for k, (tx, lid, heap) in enumerate(((0.10, 'pote_parafusos', 'parafusos'), (0.21, 'pote_buchas', 'buchas'))):
        ty = y1 - 0.12
        r_, h_ = 0.050, 0.120
        R.lathe(f'Pote {k}', 'lata', [(0.0, 0.0), (r_ - 0.003, 0.0), (r_, 0.004), (r_, h_ - 0.006), (r_ + 0.0015, h_ - 0.003), (r_ + 0.0015, h_),
                                     (r_ - 0.002, h_), (r_ - 0.002, 0.004), (0.0, 0.004)], seg=36, r=0.0, matrix=Matrix.Translation((tx, ty, H)))
        # contents: a lumpy fill of small parts a few centimetres under the rim
        R.cyl(f'Pote {k} conteudo', heap, (tx, ty, H + h_ - 0.024), r_ - 0.0025, 0.004, seg=36, r=0.0)
        for j in range(14):
            a = j * 2.39996
            rr = (r_ - 0.010) * math.sqrt((j + 0.5) / 14)
            px, py = tx + rr * math.cos(a), ty + rr * math.sin(a)
            if heap == 'parafusos':
                R.cyl(f'Pote {k} parafuso {j}', 'parafusos', (px, py, H + h_ - 0.020), 0.0021, 0.022, axis=(math.cos(a * 3), math.sin(a * 3), 0.25),
                      seg=8, r=0.0)
            else:
                R.cyl(f'Pote {k} bucha {j}', 'buchas', (px, py, H + h_ - 0.019), 0.0040, 0.030, axis=(math.cos(a * 3), math.sin(a * 3), 0.3),
                      seg=8, r=0.0008)
        out['labels'].append((lid, tx, H + 0.07, ty - r_ - 0.0012, 0.0))
    # insulating tape roll lying on the top
    R.lathe('Fita isolante', 'fita_isolante', [(0.016, 0.0), (0.025, 0.0), (0.025, 0.019), (0.016, 0.019)], seg=28, r=0.0006,
            matrix=Matrix.Translation((0.36, y0 + 0.16, H)))

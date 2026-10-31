"""Dressing of the storage room ("Deposito") -> Assets/UnclaimedPrototype/Deposito/Layout.json, read by the generator
(UnclaimedMorgueGenerator.Deposito.cs). Unity metres. Shelving and floor pieces are placed in world coordinates; what
stands on a shelf is placed in the shelf's local frame (x along the shelf, y up from the floor, -z = the shelf's front).
Every prefab faces -Z and stands on its origin.

What the room stores (references and decisions: docs/revisao-deposito/referencias.md):
  west, 4 shallow units (0.40 m)  PPE (gloves, masks, gowns) and the disposables of the necropsy (blades, sutures, gauze)
  north, 3 deep units (0.60 m)    body bags, disposable sheet rolls, and the formalin on its spill tray (low, by the door)
  east, 1 narrow unit (0.30 m)    maintenance spares, with Mauro's adapted screwdriver and Helena's replacement tag
  floor                           the last delivery on a pallet, still to be shelved; the platform cart parked beside it
No cleaning products, food or personal belongings here. Heavy at the bottom, daily use between 0.8 and 1.6 m, light and
bulky at the top; one opened box, gaps where stock was taken, small offsets, no clutter in the walkway or the door's swing.
Run with any python3: python3 Blender/Deposito/scripts/layout_deposito.py"""
import json, math, os, random

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../..'))
OUT = ROOT + '/Assets/UnclaimedPrototype/Deposito/Layout.json'
rnd = random.Random(20261118)

# Free interior on the finished surfaces (wall centrelines -8 / -4.5 and 0.875 / 6).
XMIN, XMAX, ZMIN, ZMAX = -7.875, -4.625, 1.0, 5.875
GAP = 0.05                                   # air gap kept between the shelving and the walls

DEPTH = {'EstanteRasa': 0.40, 'EstanteEstreita': 0.30, 'EstanteFunda': 0.60}
LEVELS = {'EstanteRasa': (0.10, 0.47, 0.84, 1.21, 1.58, 1.98), 'EstanteEstreita': (0.10, 0.47, 0.84, 1.21, 1.58, 1.98),
          'EstanteFunda': (0.10, 0.55, 1.00, 1.45, 1.98)}
# footprints (x, z, height) of what goes on the shelves
DIM = {'CaixaSacoCadaver': (0.50, 0.35, 0.30), 'CaixaLuvas': (0.50, 0.35, 0.30), 'CaixaAventais': (0.40, 0.30, 0.24), 'CaixaAlgodao': (0.40, 0.30, 0.24),
       'CaixaCompressas': (0.32, 0.24, 0.18), 'CaixaMascaras': (0.32, 0.24, 0.18), 'CaixaAberta': (0.40, 0.30, 0.24),
       'LuvaM': (0.24, 0.125, 0.065), 'LuvaG': (0.24, 0.125, 0.065), 'LuvaNitrilica': (0.24, 0.125, 0.065), 'Mascaras': (0.19, 0.095, 0.09),
       'PFF2': (0.20, 0.115, 0.11), 'Laminas': (0.12, 0.055, 0.026), 'FioSutura': (0.13, 0.07, 0.036), 'LampadaLED': (0.62, 0.04, 0.04),
       'CaixaOrganizadora': (0.44, 0.32, 0.21), 'Bin': (0.15, 0.258, 0.13), 'RoloLencol': (0.70, 0.11, 0.11), 'GalaoFormol': (0.19, 0.125, 0.30),
       'Bandeja': (0.64, 0.44, 0.075), 'Ferramenta': (0.30, 0.13, 0.04)}
SHEET = 0.001                                # items stand on the shelf sheet

pecas, etiquetas = [], []


def piece(name, prefab, pos, yaw=0.0, parent=''):
    pecas.append({'nome': name, 'prefab': prefab, 'pai': parent, 'pos': [round(v, 4) for v in pos], 'yaw': round(yaw, 2)})


def label(id_, parent, pos, yaw=0.0, pitch=0.0):
    """Printed label quad facing -z of its parent (pitch: top leaning back, for the tapered storage boxes)."""
    etiquetas.append({'id': id_, 'pai': parent, 'pos': [round(v, 4) for v in pos], 'yaw': round(yaw, 2), 'pitch': round(pitch, 2)})


def org_label(id_, s, level, x):
    """Card on the front of a storage box pushed to the front of shelf s: the tapered wall leans back 7.8 deg."""
    zc = -s.D / 2 + 0.025 + DIM['CaixaOrganizadora'][1] / 2
    label(id_, s.name, (x, s.levels[level] + SHEET + 0.10, zc - (0.125 + 0.10 / 0.183 * 0.025) - 0.0012), 0.0, 7.8)


def jitter(a=0.008):
    return rnd.uniform(-a, a)


class Shelf:
    def __init__(self, name, prefab, x, z, yaw):
        self.name, self.prefab, self.D = name, prefab, DEPTH[prefab]
        self.levels = LEVELS[prefab]
        piece(name, prefab, (x, 0.0, z), yaw)
        self.count = 0

    def put(self, prefab, level, x, z=None, yaw=0.0, stack=1, y0=None, name=None, wobble=1.0, jit=0.004):
        """Item on shelf `level`, centred at x along the shelf; z None = pushed to the front (25 mm back from the fold)."""
        w, d, h = DIM[prefab]
        dd = w if abs(yaw) % 180 == 90 else d
        if z is None:
            z = -self.D / 2 + 0.025 + dd / 2
        y = (self.levels[level] if y0 is None else y0) + SHEET
        for k in range(stack):
            self.count += 1
            piece(name or f'{self.name} - {prefab} {self.count}', prefab, (x + jitter(jit), y + k * h, z + jitter(jit)), yaw + rnd.uniform(-wobble, wobble),
                  self.name)

    def row(self, prefab, level, x0, x1, depth_rows=1, stacks=None, yaw=0.0, skip=()):
        """Packaging side by side between x0 and x1 (centres), depth_rows deep, stacks[i] boxes high per column."""
        w, d, h = DIM[prefab]
        n = max(1, int((x1 - x0) / (w + 0.006)) + 1)
        for i in range(n):
            x = x0 + i * (w + 0.006) if n > 1 else x0
            for j in range(depth_rows):
                if (i, j) in skip:
                    continue
                z = -self.D / 2 + 0.025 + d / 2 + j * (d + 0.006)
                s = stacks[(i * depth_rows + j) % len(stacks)] if stacks else 1
                self.put(prefab, level_of(level), x, z, yaw, stack=s, wobble=0.6, jit=0.0015)
        return n

    def lip(self, id_, level, x):
        """Paper label on the front fold of a shelf (fold faces -z, 30 mm high, top at the shelf level)."""
        label(id_, self.name, (x, self.levels[level] - 0.0165, -self.D / 2 - 0.0012), 0.0)


def level_of(l):
    return l


# ---------------------------------------------------------------- shelving
west = -90.0       # yaw: front towards +X (the room); north wall units face -Z (yaw 0); the east unit faces -X (yaw 90)
xw = XMIN + GAP + 0.20
O = [Shelf(f'Estante O{i + 1}', 'EstanteRasa', xw, z, west) for i, z in enumerate((1.52, 2.45, 3.38, 4.31))]
zn = ZMAX - GAP - 0.30
N = [Shelf(f'Estante N{i + 1}', 'EstanteFunda', x, zn, 0.0) for i, x in enumerate((-7.365, -6.435, -5.505))]
E = Shelf('Estante L1', 'EstanteEstreita', XMAX - GAP - 0.15, 4.81, 90.0)

# Unit plaques on the top fold (what the unit holds; the storeroom's own way of finding things)
for s, plaque in ((O[0], 'placa_epi_luvas'), (O[1], 'placa_epi'), (O[2], 'placa_necropsia'), (O[3], 'placa_identificacao'),
                  (N[0], 'placa_sacos'), (N[1], 'placa_lencois'), (N[2], 'placa_quimicos'), (E, 'placa_manutencao')):
    label(plaque, s.name, (0.0, s.levels[-1] - 0.0165, -s.D / 2 - 0.0012), 0.0)

# ---- O1: gloves. Master boxes low, the open master box and the loose dispensers at reach height.
s = O[0]
s.put('CaixaLuvas', 0, -0.17)
s.put('CaixaMascaras', 0, 0.255)
s.put('CaixaAberta', 1, -0.17, name='Estante O1 - caixa aberta de luvas')
# three dispensers left in the opened master box (two deep, packed flat), the front pile taken down to one
zc = -s.D / 2 + 0.025 + 0.15
for k, (dz, dy) in enumerate(((-0.066, 0.0), (0.066, 0.0), (0.066, 0.065))):
    piece(f'Estante O1 - luva na caixa aberta {k + 1}', 'LuvaM', (-0.17 + jitter(0.006), s.levels[1] + SHEET + 0.004 + dy, zc + dz), rnd.uniform(-2, 2), s.name)
s.row('LuvaG', 1, 0.24, 0.24, depth_rows=2, stacks=[2, 1])
s.row('LuvaM', 2, -0.30, 0.195, depth_rows=2, stacks=[3, 2, 3, 3, 1, 2], skip={(2, 1)})
s.lip('lip_luva_m', 2, -0.30)
s.row('LuvaNitrilica', 3, -0.30, -0.054, depth_rows=2, stacks=[2, 3, 1, 2])
s.row('LuvaG', 3, 0.22, 0.22, depth_rows=2, stacks=[3, 2])
s.lip('lip_luva_nitrilica', 3, -0.30)
s.lip('lip_luva_g', 3, 0.22)
s.row('Mascaras', 4, -0.325, 0.30, depth_rows=3, stacks=[2, 2, 1, 2, 2, 2, 1, 2, 2, 1, 2, 2], skip={(1, 0), (3, 0)})
s.lip('lip_mascara', 4, -0.325)
s.put('CaixaMascaras', 5, -0.24)

# ---- O2: gowns, respirators, caps and overshoes.
s = O[1]
s.put('CaixaAventais', 0, -0.22)
s.put('CaixaAventais', 0, 0.215)
s.put('CaixaOrganizadora', 1, 0.20, name='Estante O2 - caixa organizadora oculos', wobble=0.4, jit=0.002)
org_label('org_oculos', s, 1, 0.20)
s.row('PFF2', 2, -0.32, 0.13, depth_rows=2, stacks=[2, 1, 2, 2, 1, 1], skip={(1, 0)})
s.lip('lip_pff2', 2, -0.32)
# one storage box per level: two side by side do not fit between the uprights (0.44 m each with the handles)
s.put('CaixaOrganizadora', 3, -0.20, name='Estante O2 - caixa organizadora toucas', wobble=0.4, jit=0.002)
org_label('org_toucas', s, 3, -0.20)
s.put('CaixaOrganizadora', 4, 0.20, name='Estante O2 - caixa organizadora propes', wobble=0.4, jit=0.002)
org_label('org_propes', s, 4, 0.20)
s.row('Mascaras', 4, -0.325, -0.129, depth_rows=2, stacks=[2, 1, 2])
s.put('CaixaMascaras', 5, 0.20)
s.put('CaixaAventais', 5, -0.22)

# ---- O3: necropsy disposables. Bins at reach height, gauze and cotton master boxes low.
s = O[2]
s.put('CaixaCompressas', 0, -0.26)
s.put('CaixaCompressas', 0, 0.20)
s.put('CaixaAlgodao', 1, -0.22)
s.put('CaixaCompressas', 1, 0.24)
bins = (('Laminas', 'bin_lamina'), ('Laminas', 'bin_lamina'), ('FioSutura', 'bin_fio'), (None, 'bin_vazio'), ('FioSutura', 'bin_fio'))
for i, (content, lid) in enumerate(bins):
    x = -0.34 + i * 0.17
    bz = -s.D / 2 + 0.025 + 0.129
    piece(f'Estante O3 - bin {i + 1}', 'Bin', (x, s.levels[2] + SHEET, bz), rnd.uniform(-1, 1), s.name)
    label(lid, s.name, (x, s.levels[2] + SHEET + 0.043, bz - 0.125 - 0.0025), 0.0)
    if content:
        w, d, h = DIM[content]
        n = 5 if content == 'Laminas' else 3
        for k in range(n - (i % 2)):
            # small boxes in two piles, front and back of the bin, the front pile the shorter (used first)
            back = k % 2 == 0
            piece(f'Estante O3 - bin {i + 1} - {content} {k + 1}', content,
                  (x + jitter(0.004), s.levels[2] + SHEET + 0.003 + (k // 2) * h, bz + (0.055 if back else -0.06) + jitter(0.003)), rnd.uniform(-4, 4), s.name)
s.row('FioSutura', 3, -0.36, -0.10, depth_rows=3, stacks=[2, 3, 1])
s.row('Laminas', 3, 0.10, 0.36, depth_rows=4, stacks=[3, 2, 4, 2], skip={(2, 0)})
s.lip('lip_fio', 3, -0.36)
s.lip('lip_lamina', 3, 0.10)
s.put('CaixaCompressas', 4, -0.25)
s.put('CaixaAlgodao', 5, 0.20)

# ---- O4: identification and bagging sundries, in labelled storage boxes.
s = O[3]
s.put('CaixaAventais', 0, 0.215)
for lvl, x, lid in ((1, -0.20, 'org_etiquetas'), (2, -0.20, 'org_lacres'), (4, 0.20, 'org_sacos_visceras'), (3, 0.0, 'org_pulseiras')):
    s.put('CaixaOrganizadora', lvl, x, name=f'Estante O4 - caixa organizadora {lid[4:]}', wobble=0.4, jit=0.002)
    org_label(lid, s, lvl, x)
s.put('CaixaMascaras', 4, -0.24)

# ---- N1: body bags (master boxes turned so they fit the deep shelves two by two).
s = N[0]
for lvl, xs in ((0, (-0.22, 0.20)), (1, (-0.22, 0.20)), (2, (-0.22,))):
    for x in xs:
        s.put('CaixaSacoCadaver', lvl, x, yaw=90.0)
s.put('CaixaOrganizadora', 3, 0.20)
org_label('org_etiquetas_saco', s, 3, 0.20)

# ---- N2: disposable sheet rolls, a row of four with a row of three on them; fewer higher up.
s = N[1]
r = 0.055
for lvl, bottom, top in ((0, 4, 3), (1, 4, 1), (2, 2, 0)):
    y = s.levels[lvl] + SHEET
    for k in range(bottom):
        piece(f'Estante N2 - rolo {lvl}.{k}', 'RoloLencol', (jitter(0.02), y, -s.D / 2 + 0.05 + r + k * (2 * r + 0.004)), rnd.uniform(-0.3, 0.3), s.name)
    for k in range(top):
        piece(f'Estante N2 - rolo {lvl}.{bottom + k}', 'RoloLencol', (jitter(0.02), y + r * 1.75, -s.D / 2 + 0.05 + 2 * r + k * (2 * r + 0.004)), rnd.uniform(-0.3, 0.3), s.name)
s.lip('lip_lencol', 1, -0.30)
s.put('CaixaAventais', 3, -0.20)

# ---- N3: formalin low, on its spill tray, next to the door (heavy, few, seen at once); lighter stock above.
s = N[2]
ty = s.levels[0] + SHEET
piece('Estante N3 - bandeja de contencao', 'Bandeja', (0.0, ty, -0.05), 0.0, s.name)
for k, x in enumerate((-0.215, 0.0)):
    piece(f'Estante N3 - galao de formol {k + 1}', 'GalaoFormol', (x + jitter(0.004), ty + 0.004, -0.11 + jitter(0.004)), rnd.uniform(-3, 3), s.name)
piece('Estante N3 - galao de formol 3', 'GalaoFormol', (0.22, ty + 0.004, 0.02), 84.0, s.name)
s.put('CaixaOrganizadora', 1, -0.20, wobble=0.4, jit=0.002)
s.put('CaixaCompressas', 2, 0.20)
s.put('CaixaMascaras', 3, -0.22)
org_label('org_reserva', s, 1, -0.20)

# ---- L1 (east, maintenance): spare tubes for the fittings, small-parts bins, and Mauro's screwdriver at hand height.
s = E
for lvl, n in ((1, 3), (5, 2)):
    for k in range(n):
        piece(f'Estante L1 - lampada {lvl}.{k}', 'LampadaLED', (jitter(0.01), s.levels[lvl] + SHEET + (k // 2) * 0.04, -s.D / 2 + 0.05 + (k % 2) * 0.046), rnd.uniform(-1, 1), s.name)
s.lip('lip_lampada', 1, -0.30)
for i, lid in enumerate(('bin_eletrica', 'bin_hidraulica', 'bin_fixacao')):
    x = -0.24 + i * 0.24
    piece(f'Estante L1 - bin {i + 1}', 'Bin', (x, s.levels[2] + SHEET, -s.D / 2 + 0.01 + 0.129), 0.0, s.name)
    label(lid, s.name, (x, s.levels[2] + SHEET + 0.043, -s.D / 2 + 0.01 + 0.004 - 0.0025), 0.0)
piece('Ferramenta de cabo adaptado', 'Ferramenta', (-0.12, s.levels[3] + SHEET, -0.02), 8.0, s.name)
piece('Estante L1 - bin 4', 'Bin', (0.26, s.levels[3] + SHEET, -s.D / 2 + 0.01 + 0.129), 0.0, s.name)
label('bin_reposicao', s.name, (0.26, s.levels[3] + SHEET + 0.043, -s.D / 2 + 0.01 + 0.004 - 0.0025), 0.0)

# ---------------------------------------------------------------- floor: the last delivery and the cart
px, pz = -5.84, 1.70
piece('Estrado da entrega', 'Estrado', (px, 0.0, pz), 90.0)
top = 0.14
# three layers of master boxes, 2 across (x) by 3 deep (z), long side along x: bags, then gloves, then a broken top layer
layers = (('CaixaSacoCadaver', set()), ('CaixaLuvas', set()), ('CaixaLuvas', {(1, 0), (1, 2)}))
for L_, (prefab, missing) in enumerate(layers):
    w, d, h = DIM[prefab]
    for i in range(2):
        for j in range(3):
            if (i, j) in missing:
                continue
            piece(f'Entrega - {prefab} {L_}.{i}{j}', prefab, (px - 0.2525 + i * 0.505 + jitter(0.0015), top + L_ * h, pz - 0.36 + j * 0.36 + jitter(0.002)),
                  rnd.uniform(-0.4, 0.4) + (180.0 if (i + j + L_) % 2 else 0.0))
# platform cart parked along the east wall, its handle towards the door, free for the next box
piece('Carrinho plataforma', 'Carrinho', (XMAX - 0.025 - 0.322, 0.0, 1.52), 0.0)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, 'w', encoding='utf-8') as f:
    json.dump({'pecas': pecas, 'etiquetas': etiquetas}, f, ensure_ascii=False, indent=1)
print(len(pecas), 'pecas,', len(etiquetas), 'etiquetas ->', OUT)

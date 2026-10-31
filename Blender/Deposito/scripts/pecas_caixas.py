"""Packaging of the storage room: corrugated shipping boxes (regular slotted case, outer flaps meeting on a taped seam,
glued manufacturer's joint), one opened box with its flaps folded down, and the printed retail boxes of the consumables.
Every part carries the print UV (impressao.py); the painted net comes from impressos_deposito.py.
Origin: floor (bottom of the box), centre; front towards -Y. Needs R, I (impressao) and P (impressos_deposito)."""
import math
from mathutils import Matrix, Vector

T = 0.0040          # corrugated board (single wall, flute C)
SIZES = {'G': (0.50, 0.35, 0.30), 'M': (0.40, 0.30, 0.24), 'P': (0.32, 0.24, 0.18)}
KRAFT = (176, 132, 88)
INNER = (188, 150, 106)
INK = (30, 34, 48)

# Shipping boxes: size, print title, second line, quantity, stock sticker lines.
CAIXAS = {
    'CaixaSacoCadaver': ('G', 'SACO PARA CADÁVER', 'ADULTO · CINZA · COM ZÍPER', '10 UNIDADES',
                         ('SACO P/ CADÁVER ADULTO', 'QTD: 10 UN', 'VAL.: 09/2029')),
    'CaixaLuvas': ('G', 'LUVA DE PROCEDIMENTO', 'LÁTEX · TAMANHO M', '10 CX × 100 UN', ('LUVA PROCED. LÁTEX M', 'QTD: 10 CX', 'VAL.: 03/2028')),
    'CaixaAventais': ('M', 'AVENTAL DESCARTÁVEL', 'MANGA LONGA · TNT 30 g', '50 UNIDADES', ('AVENTAL DESCART. M/L', 'QTD: 50 UN', 'VAL.: 11/2028')),
    'CaixaAlgodao': ('M', 'ALGODÃO HIDRÓFILO', 'ROLO 500 g', '12 ROLOS', ('ALGODÃO ROLO 500 g', 'QTD: 12 UN', 'VAL.: 06/2029')),
    'CaixaCompressas': ('P', 'COMPRESSA DE GAZE', '7,5 × 7,5 cm · 13 FIOS', '10 PCT × 50 UN', ('COMPRESSA GAZE 7,5', 'QTD: 10 PCT', 'VAL.: 01/2029')),
    'CaixaMascaras': ('P', 'MÁSCARA CIRÚRGICA', 'TRIPLA CAMADA', '20 CX × 50 UN', ('MÁSCARA CIRÚRG. TRIPLA', 'QTD: 20 CX', 'VAL.: 08/2028')),
}
ABERTA = ('M', 'LUVA DE PROCEDIMENTO', 'LÁTEX · TAMANHO M', '10 CX × 100 UN', ('LUVA PROCED. LÁTEX M', 'QTD: 10 CX', 'VAL.: 03/2028'))

# Retail boxes: dimensions, band colour, lines, big mark, quantity.
PRODUTOS = {
    'LuvaM': ((0.240, 0.125, 0.065), (34, 72, 150), ('LUVA DE PROCEDIMENTO', 'LÁTEX · COM PÓ · NÃO ESTÉRIL'), 'M', '100 UNIDADES', True),
    'LuvaG': ((0.240, 0.125, 0.065), (128, 34, 44), ('LUVA DE PROCEDIMENTO', 'LÁTEX · COM PÓ · NÃO ESTÉRIL'), 'G', '100 UNIDADES', True),
    'LuvaNitrilica': ((0.240, 0.125, 0.065), (92, 64, 150), ('LUVA NITRÍLICA', 'SEM PÓ · NÃO ESTÉRIL'), 'M', '100 UNIDADES', True),
    'Mascaras': ((0.190, 0.095, 0.090), (24, 118, 120), ('MÁSCARA CIRÚRGICA', 'TRIPLA CAMADA · COM ELÁSTICO'), '', '50 UNIDADES', False),
    'PFF2': ((0.200, 0.115, 0.110), (196, 108, 30), ('RESPIRADOR PFF2', 'SEM VÁLVULA'), '', '20 UNIDADES', False),
    'Laminas': ((0.120, 0.055, 0.026), (168, 40, 40), ('LÂMINA DE BISTURI Nº 22', 'AÇO CARBONO · ESTÉRIL'), '22', '100 UNIDADES', False),
    'FioSutura': ((0.130, 0.070, 0.036), (30, 110, 62), ('FIO DE SUTURA NYLON 0', 'AGULHA 3/8 · 45 cm'), '', '24 ENVELOPES', False),
    # maintenance stock: spare tubes for the ceiling fittings
    'LampadaLED': ((0.620, 0.040, 0.040), (230, 176, 24), ('LÂMPADA TUBULAR LED T8', '9 W · 60 cm · 4000 K'), '', '1 UNIDADE', False),
}
PRINT_PX = 4096


def _paint_shipping(asset, spec, rects, size, open_box, out):
    _, title, sub, qty, label = spec
    L, W, H = SIZES[spec[0]]
    c = P.Canvas(rects, size, PRINT_PX if L > 0.35 else 2048 * 2, seed=hash(asset) & 0xFFFF)
    for f in ('frente', 'fundo', 'esquerda', 'direita'):
        c.board(f, KRAFT)
    if 'topo' in rects:
        c.board('topo', KRAFT, flutes_vertical=False)
    for f in rects:
        if f.startswith('i_'):
            c.board(f, INNER)
        elif f.startswith('aba_'):
            c.board(f, KRAFT, flutes_vertical=False)
    # flexo print on the long faces, handling marks and a short repeat on the ends
    for f in ('frente', 'fundo'):
        c.text(f, [(title, L / 2, H * 0.64, H * 0.105, P.SANS_C, L * 0.86), (sub, L / 2, H * 0.47, H * 0.065, P.SANS_B, L * 0.8),
                   (qty, L / 2, H * 0.32, H * 0.075, P.SANS_B, L * 0.6)], INK)
        c.fill_rect(f, L * 0.08, H * 0.775, L * 0.92, H * 0.785, INK, 0.9)
    for f in ('esquerda', 'direita'):
        c.text(f, [(title, W / 2, H * 0.42, H * 0.07, P.SANS_C, W * 0.86), (qty, W / 2, H * 0.30, H * 0.055, P.SANS_B, W * 0.8)], INK)
        c.symbols(f, W * 0.30, H * 0.56, min(W, H) * 0.17)
    # stock sticker of the storeroom on the front, low right
    c.sticker('frente', L - 0.072, 0.058, 0.112, 0.062, [('ALMOXARIFADO', 0.80, 0.16, P.SANS_B), (label[0], 0.56, 0.17, P.SANS_B),
                                                         (label[1], 0.33, 0.15, P.SANS), (label[2], 0.13, 0.15, P.SANS)],
              tilt=c.rnd.uniform(-1.6, 1.6))
    # packing tape along the seam (X) and down the two ends; on the opened box it was slit along the seam
    if open_box:
        for f, v0, v1 in (('aba_frente', W / 2 - 0.024, W / 2), ('aba_fundo', 0.0, 0.024)):
            c.tape(f, 0.0, v0, L, v1)
    else:
        c.tape('topo', 0.0, W / 2 - 0.024, L, W / 2 + 0.024)
    for f in ('esquerda', 'direita'):
        c.tape(f, W / 2 - 0.024, H - 0.062, W / 2 + 0.024, H)
    for f in ('frente', 'fundo', 'esquerda', 'direita'):
        c.wear(f, edge=0.012, bottom=0.035, amount=1.0)
    if 'topo' in rects:
        c.wear('topo', edge=0.010, amount=0.8)
    for f in rects:
        if f.startswith('aba_'):
            c.wear(f, edge=0.010, amount=0.9)
        elif f.startswith('i_'):
            c.wear(f, edge=0.006, amount=0.5)
    c.save(out)


def _paint_retail(asset, spec, rects, size, out):
    (L, W, H), band, lines, mark, qty, dispenser = spec
    c = P.Canvas(rects, size, 2048, seed=hash(asset) & 0xFFFF)
    white = (236, 236, 230)
    for f in ('frente', 'fundo', 'esquerda', 'direita', 'topo'):
        c.board(f, white, kraft=False)
    for f in ('frente', 'fundo'):
        c.fill_rect(f, 0, H * 0.58, L, H, band)
        c.text(f, [(lines[0], L * 0.44 if mark else L / 2, H * 0.80, H * 0.17, P.SANS_C, L * (0.78 if mark else 0.92))], (246, 246, 242), erode=0.05)
        c.text(f, [(lines[1], L / 2, H * 0.44, H * 0.10, P.SANS_B, L * 0.9), (qty, L / 2, H * 0.22, H * 0.12, P.SANS_B, L * 0.7)], (40, 40, 46), erode=0.05)
        if mark:
            c.text(f, [(mark, L * 0.90, H * 0.79, H * 0.30, P.SANS_B)], (246, 246, 242), erode=0.05)
    for f in ('esquerda', 'direita'):
        c.fill_rect(f, 0, H * 0.58, W, H, band)
        c.text(f, [(mark or qty.split()[0], W / 2, H * 0.30, H * 0.30, P.SANS_B)], band, erode=0.05)
    c.fill_rect('topo', 0, W * 0.78, L, W, band)
    c.text('topo', [(lines[0], L / 2, W * 0.88, W * 0.11, P.SANS_C, L * 0.9), ('VAL.: 03/2028', L * 0.80, W * 0.10, W * 0.07, P.MONO)], (40, 40, 46), erode=0.05)
    if dispenser:   # perforated tear-off oval of the dispenser, still closed
        def oval(d, w, h, k):
            cx, cy, rx, ry = L / 2 * k, h - W * 0.42 * k, L * 0.30 * k, W * 0.20 * k
            n = 60
            for i in range(0, n, 2):
                a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
                d.line((cx + rx * math.cos(a0), cy + ry * math.sin(a0), cx + rx * math.cos(a1), cy + ry * math.sin(a1)), fill=255, width=max(2, int(0.0008 * k)))
        c.ink('topo', oval, (120, 120, 118), fade=0.8, erode=0.0)
    for f in ('frente', 'fundo', 'esquerda', 'direita', 'topo'):
        c.wear(f, edge=0.004, amount=0.6, rub=(170, 168, 160))
    c.save(out)


# ---------------------------------------------------------------- geometry

def _closed(L, W, H, rects, size):
    body = R.box('Corpo', 'papelao', -L / 2, -W / 2, 0.0, L / 2, W / 2, H - T, r=0.0025, seg=2)
    I.cube_uv(body, rects, size, L, W, H)
    for s in (-1, 1):
        y0, y1 = sorted((s * 0.0015, s * W / 2))
        flap = R.box(f'Aba {s:+d}', 'papelao', -L / 2 + 0.0005, y0, H - T, L / 2 - 0.0005, y1, H, r=0.0012, seg=1)
        for v in flap.data.vertices:   # the free edges sag a little into the seam
            if abs(v.co.y) < 0.01:
                v.co.z -= 0.0014
        I.cube_uv(flap, rects, size, L, W, H)
    joint = R.box('Aba de cola', 'papelao', L / 2 - 0.0002, W / 2 - 0.036, 0.004, L / 2 + 0.0012, W / 2 - 0.003, H - 0.006, r=0.0004, seg=1)
    I.cube_uv(joint, rects, size, L, W, H)


def _opened(L, W, H, rects, size, angles):
    parts = [R.box('Parede frente', 'papelao', -L / 2, -W / 2, 0, L / 2, -W / 2 + T, H, r=0.0012, seg=1),
             R.box('Parede fundo', 'papelao', -L / 2, W / 2 - T, 0, L / 2, W / 2, H, r=0.0012, seg=1),
             R.box('Parede esquerda', 'papelao', -L / 2, -W / 2 + T, 0, -L / 2 + T, W / 2 - T, H, r=0.0012, seg=1),
             R.box('Parede direita', 'papelao', L / 2 - T, -W / 2 + T, 0, L / 2, W / 2 - T, H, r=0.0012, seg=1),
             R.box('Base', 'papelao', -L / 2 + T, -W / 2 + T, 0, L / 2 - T, W / 2 - T, T, r=0.0, seg=1)]
    for p in parts:
        I.cube_uv(p, rects, size, L, W, H, inner=True)
    joint = R.box('Aba de cola', 'papelao', L / 2 - 0.0002, W / 2 - 0.036, 0.004, L / 2 + 0.0012, W / 2 - 0.003, H - 0.006, r=0.0004, seg=1)
    I.cube_uv(joint, rects, size, L, W, H)
    # flaps: built closed (lying flush on the walls' top, reaching inwards), projected, then turned about the hinge
    d = W / 2 - 0.002
    specs = {
        'frente': ((-L / 2 + 0.002, -W / 2, H - T, L / 2 - 0.002, -W / 2 + d, H), (0, -W / 2, H), (1, 0, 0), angles[0], (0, 1, -L / 2 + 0.002, -W / 2)),
        'fundo': ((-L / 2 + 0.002, W / 2 - d, H - T, L / 2 - 0.002, W / 2, H), (0, W / 2, H), (-1, 0, 0), angles[1], (0, 1, -L / 2 + 0.002, W / 2 - d)),
        'esquerda': ((-L / 2, -W / 2 + 0.004, H - T, -L / 2 + d, W / 2 - 0.004, H), (-L / 2, 0, H), (0, -1, 0), angles[2], (1, 0, -W / 2 + 0.004, -L / 2)),
        'direita': ((L / 2 - d, -W / 2 + 0.004, H - T, L / 2, W / 2 - 0.004, H), (L / 2, 0, H), (0, 1, 0), angles[3], (1, 0, -W / 2 + 0.004, L / 2 - d)),
    }
    for side, (bx, hinge, axis, ang, (ua, va, u0, v0)) in specs.items():
        flap = R.box('Aba ' + side, 'papelao', *bx, r=0.0010, seg=1)
        I.planar_uv(flap, rects['aba_' + side], size, u0, v0, uaxis=ua, vaxis=va)
        m = Matrix.Translation(hinge) @ Matrix.Rotation(math.radians(ang), 4, Vector(axis)) @ Matrix.Translation(-Vector(hinge))
        flap.data.transform(m)


def shipping(asset):
    spec = CAIXAS[asset]
    L, W, H = SIZES[spec[0]]
    rects, size = I.net(L, W, H)
    _paint_shipping(asset, spec, rects, size, False, IMPRESSOS + '/' + asset)
    _closed(L, W, H, rects, size)


def aberta(asset='CaixaAberta'):
    L, W, H = SIZES[ABERTA[0]]
    rects, size = I.net(L, W, H, open_box=True)
    _paint_shipping(asset, ABERTA, rects, size, True, IMPRESSOS + '/' + asset)
    # front flap folded down over the front, the back one hanging behind, the ends down and a little open
    _opened(L, W, H, rects, size, (262.0, 266.0, 255.0, 258.0))


def retail(asset):
    spec = PRODUTOS[asset]
    L, W, H = spec[0]
    rects, size = I.net(L, W, H)
    _paint_retail(asset, spec, rects, size, IMPRESSOS + '/' + asset)
    body = R.box('Cartucho', 'cartao', -L / 2, -W / 2, 0.0, L / 2, W / 2, H, r=0.0010, seg=1)
    I.cube_uv(body, rects, size, L, W, H)

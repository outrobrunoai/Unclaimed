"""Printed plates, stickers and cards of the technical room, painted into one atlas with the storeroom's painter, plus
Etiquetas.json for the Unity builder (same format as the storeroom's), and the two lit displays:
python3 Blender/Tecnica/scripts/etiquetas_tecnica.py
Texts only name what the equipment is and feeds. The contingency panel carries the legend D04 word for word and the
plan of the lighting it feeds, drawn from the zone rectangles of the generator (UnclaimedMorgueGenerator.cs); the
circuit list of the main board names areas, without ratings. Displays: total and limit of P02 (9 of 10) and the
cold-room temperature, as emission maps of seven-segment digits (the puzzle can swap them later)."""
import json, math, os, sys
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '../../Deposito/scripts')))
import impressos_deposito as P

ROOT = os.path.normpath(os.path.join(HERE, '../../..'))
TEX = ROOT + '/Assets/Modelos/Tecnica/Texturas'
PNG = TEX + '/Etiquetas_Tecnica.png'
JSON = ROOT + '/Assets/UnclaimedPrototype/Tecnica/Etiquetas.json'
PX_PER_M = 6000
ATLAS = 4096
MAURO = os.path.normpath(os.path.join(HERE, '../../Recepcao/fontes/GochiHand-Regular.ttf'))

SIZE = {'pc_placa': (0.30, 0.045), 'nr10': (0.075, 0.050), 'pc_total': (0.050, 0.009), 'pc_limite': (0.050, 0.009),
        'pc_refrigeracao': (0.11, 0.032), 'pc_luz_geral': (0.11, 0.032), 'pc_exame': (0.11, 0.032), 'pos_01': (0.048, 0.048),
        'pc_d04': (0.22, 0.14), 'pc_planta': (0.25, 0.16), 'qdg_placa': (0.36, 0.055), 'qdg_circuitos': (0.20, 0.28),
        'cf_placa': (0.24, 0.036), 'cf_controlador': (0.076, 0.034), 'cf_ligado': (0.050, 0.011), 'cf_degelo': (0.050, 0.011),
        'cf_alarme': (0.050, 0.011), 'multimetro': (0.071, 0.149), 'pote_parafusos': (0.050, 0.018), 'pote_buchas': (0.050, 0.018)}

WHITE = (232, 232, 226)
BLACK = (22, 22, 24)
INK = (28, 30, 36)
RED = (170, 34, 30)
YELLOW = (236, 190, 26)

# Zone rectangles (x0, z0, x1, z1) of UnclaimedMorgueGenerator.cs around the autopsy, clipped to the plan's frame.
FRAME = (-5.25, -3.9, 15.0, 9.0)
ZONAS = [('AUTÓPSIA', [(-2, -3.5, 7.5, 3.625)]), ('CÂMARA FRIA', [(7.5, -3.5, 15, 5.25)]),
         ('CIRCULAÇÃO', [(-5.25, -3.875, -2, 0.875), (-4.5, 0.875, -2, 3.625), (-4.5, 3.625, 3.25, 5.75)]),
         ('VESTÍBULO', [(3.25, 3.625, 7.5, 5.25)]), ('SALA TÉCNICA', [(-4.5, 5.75, 3.25, 9)]), ('RECEBIMENTO', [(3.25, 5.25, 12.75, 11.5)])]
LUMINARIA_EXAME = (2.7, -0.45)
PAINEL = (-1.95, 8.875)

CIRCUITOS = [('1', 'ILUMINAÇÃO — RECEPÇÃO E WC'), ('2', 'ILUMINAÇÃO — ARQUIVO'), ('3', 'ILUMINAÇÃO — DESCANSO E DEPÓSITO'),
             ('4', 'ILUMINAÇÃO — CIRCULAÇÃO'), ('5', 'ILUMINAÇÃO — RECEBIMENTO E PÁTIO'), ('6', 'TOMADAS — RECEPÇÃO E ARQUIVO'),
             ('7', 'TOMADAS — DESCANSO'), ('8', 'TOMADAS — AUTÓPSIA'), ('9', 'TOMADAS — SALA TÉCNICA'),
             ('10', 'ALIMENTAÇÃO PROVISÓRIA —'), ('', 'PAINEL DE CONTINGÊNCIA'), ('11', 'RESERVA')]


def pack():
    side = ATLAS / PX_PER_M
    pad = 0.004
    rects, x, y, row = {}, pad, pad, 0.0
    for lid in sorted(SIZE, key=lambda i: -SIZE[i][1]):
        w, h = SIZE[lid]
        if x + w + pad > side:
            x, y, row = pad, y + row + pad, 0.0
        rects[lid] = (x, side - (y + h) - pad, w, h)
        x += w + pad
        row = max(row, h)
    assert y + row + pad < side, 'atlas too small'
    return rects, side


def acrilico(c, lid, lines, wear=0.25):
    """Engraved two-layer acrylic: black face, white core showing through the engraving."""
    c.board(lid, BLACK, kraft=False)
    c.text(lid, lines, WHITE, erode=0.02, fade=0.97)
    c.wear(lid, edge=0.0012, amount=wear)


def triangulo_raio(d, x, y, s, k):
    """Electrical hazard triangle (black border, lightning bolt) with its top at (x, y) px, side s m."""
    q = s * k
    pts = [(x, y), (x + q / 2, y + q * 0.866), (x - q / 2, y + q * 0.866)]
    d.polygon(pts, fill=255)
    bolt = [(0.06, 0.22), (-0.10, 0.52), (0.00, 0.52), (-0.07, 0.78), (0.11, 0.44), (0.01, 0.44), (0.09, 0.22)]
    return [(x + bx * q, y + by * q) for bx, by in bolt]


def nr10(c, lid):
    W, H = SIZE[lid]
    c.board(lid, YELLOW, kraft=False)

    def fn(d, w, h, k):
        d.rectangle((0.0015 * k, 0.0015 * k, w - 0.0015 * k, h - 0.0015 * k), outline=255, width=max(2, int(0.0012 * k)))
        pts = triangulo_raio(d, 0.020 * k, 0.008 * k, 0.030, k)
        d.polygon(pts, fill=0)
    c.ink(lid, fn, INK, fade=0.95, erode=0.05)
    c.text(lid, [('PERIGO', 0.0535, H * 0.74, 0.010, P.SANS_C, 0.034), ('RISCO DE CHOQUE', 0.0535, H * 0.47, 0.0062, P.SANS_C, 0.034),
                 ('ELÉTRICO', 0.0535, H * 0.31, 0.0062, P.SANS_C, 0.034)], INK, erode=0.05, fade=0.95)
    c.wear(lid, edge=0.0015, amount=0.5)


def posicoes(c, lid):
    """0 / 1 plate behind a 22 mm operator: positions at 45 deg left and right of the top, with tick marks."""
    W, H = SIZE[lid]
    c.board(lid, BLACK, kraft=False)

    def fn(d, w, h, k):
        cx, cy = w / 2, h / 2
        for ang, t in ((-45, '0'), (45, '1')):
            a = math.radians(ang)
            r0, r1, rt = 0.0154 * k, 0.0176 * k, 0.0228 * k   # the operator's bezel covers r < 14.6 mm
            dx, dy = math.sin(a), -math.cos(a)
            d.line((cx + dx * r0, cy + dy * r0, cx + dx * r1, cy + dy * r1), fill=255, width=max(2, int(0.0010 * k)))
            d.text((cx + dx * rt, cy + dy * rt), t, font=P.font(P.SANS_B, 0.0068 * k), fill=255, anchor='mm')
    c.ink(lid, fn, WHITE, fade=0.97, erode=0.02)
    c.wear(lid, edge=0.001, amount=0.2)


def cartao(c, lid, title, rows, foot=None, title_rgb=(30, 60, 120)):
    """Laminated printed card: coloured title band, rows (left, right) with dotted leaders, optional footer line."""
    W, H = SIZE[lid]
    c.board(lid, (240, 239, 233), kraft=False)
    band = 0.018 if H < 0.2 else 0.022
    c.fill_rect(lid, 0, H - band, W, H, title_rgb)
    c.text(lid, [(title, W / 2, H - band / 2, band * 0.56, P.SANS_C, W * 0.9)], (240, 240, 236), erode=0.02, fade=0.98)
    top = H - band - 0.006
    bottom = 0.016 if foot else 0.006
    step = (top - bottom) / len(rows)
    th = min(0.0075, step * 0.52)

    def fn(d, w, h, k):
        fl = P.font(P.SANS_C, th * k)
        for i, (lft, rgt) in enumerate(rows):
            v = top - step * (i + 0.5)
            y = h - v * k
            d.text((0.008 * k, y), lft, font=fl, fill=255, anchor='lm')
            if rgt:
                d.text((w - 0.008 * k, y), rgt, font=fl, fill=255, anchor='rm')
                x0 = 0.008 * k + d.textlength(lft, font=fl) + 0.003 * k
                x1 = w - 0.008 * k - d.textlength(rgt, font=fl) - 0.003 * k
                xx = x0
                while xx < x1:
                    d.ellipse((xx, y + th * k * 0.28, xx + 0.0006 * k, y + th * k * 0.28 + 0.0006 * k), fill=255)
                    xx += 0.0022 * k
        if foot:
            d.line((0.008 * k, h - 0.013 * k, w - 0.008 * k, h - 0.013 * k), fill=255, width=max(1, int(0.0004 * k)))
            fs = P.font(P.SANS_C, 0.0058 * k)
            d.text((w / 2, h - 0.0068 * k), foot, font=fs, fill=255, anchor='mm')
    c.ink(lid, fn, INK, fade=0.95, erode=0.04)
    c.wear(lid, edge=0.002, amount=0.35)


def planta(c, lid):
    W, H = SIZE[lid]
    c.board(lid, (240, 239, 233), kraft=False)
    band = 0.016
    c.fill_rect(lid, 0, H - band, W, H, (30, 60, 120))
    c.text(lid, [('ILUMINAÇÃO ALIMENTADA PELO PAINEL', W / 2, H - band / 2, band * 0.56, P.SANS_C, W * 0.9)], (240, 240, 236), erode=0.02, fade=0.98)
    legend_h = 0.026
    fx0, fz0, fx1, fz1 = FRAME
    s = min((W - 0.012) / (fx1 - fx0), (H - band - legend_h - 0.008) / (fz1 - fz0))
    ox = (W - s * (fx1 - fx0)) / 2
    oz = legend_h + ((H - band - legend_h) - s * (fz1 - fz0)) / 2

    def P2(d, h, k, x, z):
        x = min(max(x, fx0), fx1)
        z = min(max(z, fz0), fz1)
        return (ox + (x - fx0) * s) * k, h - (oz + (z - fz0) * s) * k

    def hatch(d, w, h, k, x0, y0, x1, y1, pitch):
        d.rectangle((x0, y0, x1, y1), outline=255, width=max(1, int(0.0005 * k)))
        lw = max(1, int(0.0005 * k))
        t = x0 - (y1 - y0)
        while t < x1:
            a, b = (t, y1), (t + (y1 - y0), y0)
            # clip the 45 deg line to the box
            ax, ay = max(a[0], x0), y1 - (max(a[0], x0) - a[0])
            bx, by = min(b[0], x1), y0 + (b[0] - min(b[0], x1))
            if bx > ax:
                d.line((ax, ay, bx, by), fill=255, width=lw)
            t += pitch * k

    def lamp(d, k, x, y, r):
        d.ellipse((x - r, y - r, x + r, y + r), outline=255, width=max(2, int(0.0006 * k)))
        q = r * 0.7071
        d.line((x - q, y - q, x + q, y + q), fill=255, width=max(2, int(0.0006 * k)))
        d.line((x - q, y + q, x + q, y - q), fill=255, width=max(2, int(0.0006 * k)))

    def walls(d, w, h, k):
        lw = max(2, int(0.0007 * k))
        for _, rs in ZONAS:
            for x0, z0, x1, z1 in rs:
                a, b = P2(d, h, k, x0, z1), P2(d, h, k, x1, z0)
                d.rectangle((a[0], a[1], b[0], b[1]), outline=255, width=lw)
        fl = P.font(P.SANS_C, 0.0046 * k)
        for name, rs in ZONAS:
            x0, z0, x1, z1 = rs[-1] if name != 'CIRCULAÇÃO' else rs[2]
            z1 = min(z1, fz1)
            cx, cy = P2(d, h, k, (x0 + x1) / 2, (z0 + z1) / 2 if name != 'AUTÓPSIA' else z1 - 1.0)
            d.text((cx, cy), name, font=fl, fill=255, anchor='mm')
        # the panel's own position, on the north wall of the technical room
        px, py = P2(d, h, k, *PAINEL)
        r = 0.0018 * k
        d.rectangle((px - r, py - r * 1.2, px + r, py + r * 0.2), fill=255)
        d.text((px, py + 0.0035 * k), 'PAINEL', font=P.font(P.SANS_C, 0.0036 * k), fill=255, anchor='mm')
    c.ink(lid, walls, INK, fade=0.95, erode=0.03)

    def luz(d, w, h, k):
        x0, z0, x1, z1 = ZONAS[0][1][0]
        a, b = P2(d, h, k, x0, z1), P2(d, h, k, x1, z0)
        inset = 0.0012 * k
        hatch(d, w, h, k, a[0] + inset, a[1] + inset, b[0] - inset, b[1] - inset, 0.0028)
        hatch(d, w, h, k, 0.010 * k, h - 0.019 * k, 0.020 * k, h - 0.009 * k, 0.0028)
    c.ink(lid, luz, RED, fade=0.85, erode=0.03)

    def exame(d, w, h, k):
        x, y = P2(d, h, k, *LUMINARIA_EXAME)
        lamp(d, k, x, y, 0.0045 * k)
        lamp(d, k, W * 0.56 * k, h - 0.014 * k, 0.0045 * k)
        fl = P.font(P.SANS_C, 0.0050 * k)
        d.text((0.024 * k, h - 0.014 * k), 'LUZ GERAL (3)', font=fl, fill=255, anchor='lm')
        d.text((W * 0.56 * k + 0.0075 * k, h - 0.014 * k), 'LUMINÁRIA DE EXAME (4)', font=fl, fill=255, anchor='lm')
    c.ink(lid, exame, INK, fade=0.95, erode=0.03)

    def rotulo(d, w, h, k):
        x0, z0, x1, z1 = ZONAS[0][1][0]
        cx, cy = P2(d, h, k, (x0 + x1) / 2, z1 - 1.0)
        f = P.font(P.SANS_C, 0.0046 * k)
        tw = d.textlength('AUTÓPSIA', font=f) / 2 + 0.0015 * k
        d.rectangle((cx - tw, cy - 0.0034 * k, cx + tw, cy + 0.0034 * k), fill=255)
    c.ink(lid, rotulo, (240, 239, 233), fade=1.0, erode=0.0)

    def rotulo_txt(d, w, h, k):
        x0, z0, x1, z1 = ZONAS[0][1][0]
        cx, cy = P2(d, h, k, (x0 + x1) / 2, z1 - 1.0)
        d.text((cx, cy), 'AUTÓPSIA', font=P.font(P.SANS_C, 0.0046 * k), fill=255, anchor='mm')
    c.ink(lid, rotulo_txt, INK, fade=0.95, erode=0.03)
    c.wear(lid, edge=0.002, amount=0.35)


def controlador(c, lid):
    """Front of the digital controller around its display: degree sign, output marks and the four key symbols."""
    W, H = SIZE[lid]
    c.board(lid, (52, 54, 55), kraft=False)

    def fn(d, w, h, k):
        f = P.font(P.SANS_B, 0.0042 * k)
        d.text((0.0275 * k + w / 2, h - (H / 2 + 0.008) * k), '°C', font=f, fill=255, anchor='mm')
        fs = P.font(P.SANS_C, 0.0030 * k)
        y = h - (H / 2 - 0.0035) * k
        for kx, t in zip((-0.027, -0.009, 0.009, 0.027), ('SET', 'up', 'down', 'DEG')):
            x = (w / 2 + kx * k)
            if t in ('up', 'down'):
                q = 0.0022 * k
                pts = [(x - q, y + q * 0.6), (x + q, y + q * 0.6), (x, y - q * 0.8)] if t == 'up' else \
                      [(x - q, y - q * 0.6), (x + q, y - q * 0.6), (x, y + q * 0.8)]
                d.polygon(pts, fill=255)
            else:
                d.text((x, y), t, font=fs, fill=255, anchor='mm')
    c.ink(lid, fn, (215, 215, 210), fade=0.95, erode=0.02)


def multimetro(c, lid):
    """Face of a basic digital multimeter: LCD window (off), range markings around the dial, terminal captions."""
    W, H = SIZE[lid]
    c.board(lid, (38, 39, 41), kraft=False)
    c.fill_rect(lid, 0.009, H - 0.046, W - 0.009, H - 0.012, (98, 104, 92))
    cx, cv = W / 2, H / 2 - 0.012

    def fn(d, w, h, k):
        f = P.font(P.SANS_C, 0.0042 * k)
        for ang, t in ((-150, 'OFF'), (-105, 'V~'), (-60, 'V–'), (-15, 'Ω'), (30, '·))'), (75, 'mA'), (120, 'A')):
            a = math.radians(ang)
            r0, r1, rt = 0.0205, 0.0230, 0.0280
            x, y = cx * k, h - cv * k
            d.line((x + math.sin(a) * r0 * k, y - math.cos(a) * r0 * k, x + math.sin(a) * r1 * k, y - math.cos(a) * r1 * k), fill=255,
                   width=max(2, int(0.0006 * k)))
            d.text((x + math.sin(a) * rt * k, y - math.cos(a) * rt * k), t, font=f, fill=255, anchor='mm')
        for dx, t in ((-0.022, 'COM'), (0.0, 'VΩmA'), (0.022, '10A')):
            d.text(((cx + dx) * k, h - 0.025 * k), t, font=P.font(P.SANS_C, 0.0036 * k), fill=255, anchor='mm')
    c.ink(lid, fn, (226, 226, 220), fade=0.95, erode=0.15)
    c.wear(lid, edge=0.002, amount=0.4)


def paint(c, lid):
    W, H = SIZE[lid]
    if lid == 'pc_placa':
        acrilico(c, lid, [('PAINEL DE CONTINGÊNCIA', W / 2, H * 0.64, H * 0.36, P.SANS_C, W * 0.9),
                          ('ALIMENTAÇÃO PROVISÓRIA', W / 2, H * 0.24, H * 0.20, P.SANS_C, W * 0.9)])
    elif lid == 'qdg_placa':
        acrilico(c, lid, [('QDG — QUADRO DE DISTRIBUIÇÃO GERAL', W / 2, H * 0.64, H * 0.30, P.SANS_C, W * 0.92),
                          ('220/127 V · 60 Hz', W / 2, H * 0.25, H * 0.20, P.SANS_C, W * 0.9)], wear=0.4)
    elif lid == 'cf_placa':
        acrilico(c, lid, [('COMANDO — CÂMARA FRIA', W / 2, H * 0.52, H * 0.44, P.SANS_C, W * 0.9)])
    elif lid in ('pc_total', 'pc_limite'):
        c.board(lid, BLACK, kraft=False)
        c.text(lid, [(('TOTAL', 'LIMITE')[lid == 'pc_limite'], W / 2, H / 2, H * 0.75, P.SANS_C, W * 0.9)], WHITE, erode=0.0, fade=0.95)
    elif lid in ('pc_refrigeracao', 'pc_luz_geral', 'pc_exame'):
        t, s = {'pc_refrigeracao': ('REFRIGERAÇÃO', '6 · PRIORIDADE'), 'pc_luz_geral': ('LUZ GERAL', '3 · AUTÓPSIA'),
                'pc_exame': ('LUMINÁRIA DE EXAME', '4 · AUTÓPSIA')}[lid]
        acrilico(c, lid, [(t, W / 2, H * 0.64, H * 0.34, P.SANS_C, W * 0.9), (s, W / 2, H * 0.25, H * 0.26, P.SANS_C, W * 0.9)])
    elif lid in ('cf_ligado', 'cf_degelo', 'cf_alarme'):
        acrilico(c, lid, [(lid[3:].upper(), W / 2, H / 2, H * 0.62, P.SANS_C, W * 0.9)], wear=0.15)
    elif lid == 'nr10':
        nr10(c, lid)
    elif lid == 'pos_01':
        posicoes(c, lid)
    elif lid == 'pc_d04':
        # D04, word for word (Assets/História: "Legenda do painel")
        cartao(c, lid, 'LEGENDA DO PAINEL', [('CAPACIDADE DISPONÍVEL', '10'), ('REFRIGERAÇÃO', '6 — PRIORIDADE'), ('LUZ GERAL', '3'),
                                             ('LUMINÁRIA DE EXAME', '4')], foot='EMERGÊNCIA EM ALIMENTAÇÃO INDEPENDENTE')
    elif lid == 'pc_planta':
        planta(c, lid)
    elif lid == 'qdg_circuitos':
        cartao(c, lid, 'RELAÇÃO DE CIRCUITOS', [(f'{n:>2}  {t}' if n else f'      {t}', '') for n, t in CIRCUITOS],
               foot='ILUMINAÇÃO DE EMERGÊNCIA: BLOCOS AUTÔNOMOS', title_rgb=(60, 62, 66))
        c.hand(lid, 0.123, 0.069, 'ver painel ao lado', 0.0075, rgb=(28, 58, 128), tilt=4.0, path=MAURO)
    elif lid == 'cf_controlador':
        controlador(c, lid)
    elif lid == 'multimetro':
        multimetro(c, lid)
    elif lid in ('pote_parafusos', 'pote_buchas'):
        # masking tape, Mauro's marker
        c.board(lid, (214, 198, 158), kraft=False)
        c.wear(lid, edge=0.002, amount=0.6)
        c.hand(lid, 0.004, H * 0.5, {'pote_parafusos': 'PARAF. 4,2', 'pote_buchas': 'BUCHA 6 e 8'}[lid], 0.011, rgb=(24, 26, 34), tilt=1.5, path=MAURO)


# ---------------------------------------------------------------- seven-segment displays (emission maps)

SEG = {'0': 'abcdef', '1': 'bc', '2': 'abged', '3': 'abgcd', '4': 'fgbc', '5': 'afgcd', '6': 'afgedc', '7': 'abc', '8': 'abcdefg',
       '9': 'abcfgd', ' ': ''}


def digit(d, x, y, w, h, ch, on, ghost, dot=False):
    """Slanted seven-segment digit in the box (x, y, w, h) px; unlit segments drawn faintly (ghost)."""
    t = w * 0.17
    sl = w * 0.10
    def P_(u, v):   # u across, v down (0..1), with the forward slant
        return (x + u * w + (1 - v) * sl, y + v * h)
    segs = {'a': ((0.12, 0.0), (0.88, 0.0)), 'b': ((1.0, 0.05), (1.0, 0.47)), 'c': ((1.0, 0.53), (1.0, 0.95)),
            'd': ((0.12, 1.0), (0.88, 1.0)), 'e': ((0.0, 0.53), (0.0, 0.95)), 'f': ((0.0, 0.05), (0.0, 0.47)),
            'g': ((0.12, 0.5), (0.88, 0.5))}
    for s, (a, b) in segs.items():
        col = on if s in SEG[ch] else ghost
        pa, pb = P_(*a), P_(*b)
        d.line((pa, pb), fill=col, width=int(t))
    if dot:
        px, py = P_(1.12, 1.0)
        d.ellipse((px - t * 0.6, py - t * 0.6, px + t * 0.6, py + t * 0.6), fill=on)


def visor(path, size_m, fields, px_per_m=8000, rgb=(255, 46, 30)):
    """fields: (u0, u1, text, dot_after_index) in metres across the quad; digits fill 70 % of its height."""
    W, H = int(size_m[0] * px_per_m), int(size_m[1] * px_per_m)
    im = Image.new('RGB', (W, H), (0, 0, 0))
    d = ImageDraw.Draw(im)
    on, ghost = rgb, tuple(int(c * 0.07) for c in rgb)
    for u0, u1, text, dot in fields:
        n = len(text)
        cw = (u1 - u0) * px_per_m / n
        for i, ch in enumerate(text):
            digit(d, u0 * px_per_m + cw * (i + 0.16), H * 0.15, cw * 0.62, H * 0.70, ch, on, ghost, dot=(dot == i))
    im.save(path)
    return W, H


def main():
    rects, side = pack()
    c = P.Canvas(rects, side, ATLAS, seed=53, bg=(30, 30, 32))
    for lid in rects:
        paint(c, lid)
    os.makedirs(TEX, exist_ok=True)
    os.makedirs(os.path.dirname(JSON), exist_ok=True)
    c.img.save(PNG)
    out = []
    for lid, (x, y, w, h) in rects.items():
        x0, y0, x1, y1 = c.box(lid)
        out.append({'id': lid, 'tipo': lid.split('_')[0], 'rect': [x0, y0, x1 - x0, y1 - y0], 'tamanho': [w, h]})
    with open(JSON, 'w', encoding='utf-8') as f:
        json.dump({'atlas': [ATLAS, ATLAS], 'etiquetas': out}, f, ensure_ascii=False, indent=1)
    # P02 display: windows 60 mm wide at -70..-10 and +10..+70 mm of the 140 mm quad; two digit cells each
    visor(TEX + '/Visor_Contingencia.png', (0.140, 0.032), [(0.004, 0.056, ' 9', None), (0.084, 0.136, '10', None)])
    # controller: three cells, decimal point after the second one (" 3.0")
    visor(TEX + '/Visor_CamaraFria.png', (0.044, 0.0135), [(0.001, 0.043, ' 30', 1)], px_per_m=16000)
    print(len(out), 'etiquetas ->', PNG)


if __name__ == '__main__':
    main()

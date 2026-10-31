"""Other pieces of the storage room: polypropylene storage box with lid, small parts bin ("caixa bin" n. 5), plastic pallet,
platform cart (P10: the box at the yard gate is moved with it), formalin jugs and their spill tray, disposable sheet roll,
and Mauro's screwdriver with the adapted handle, tagged as a replacement item by Helena.
Origin: floor (underside), centre; front towards -Y. Needs R, F (formas), I (impressao), P (impressos_deposito), PE."""
import bmesh, math
from mathutils import Vector, Matrix


def rr(w, d, r, z, n=5, cx=0.0, cy=0.0):
    """Rounded rectangle loop (counter-clockwise seen from above)."""
    r = min(r, w / 2 - 1e-4, d / 2 - 1e-4)
    pts = []
    for k, (sx, sy, a0) in enumerate(((1, -1, -90), (1, 1, 0), (-1, 1, 90), (-1, -1, 180))):
        ox, oy = cx + sx * (w / 2 - r), cy + sy * (d / 2 - r)
        for i in range(n + 1):
            a = math.radians(a0 + 90 * i / n)
            pts.append(Vector((ox + r * math.cos(a), oy + r * math.sin(a), z)))
    return pts


def solid(name, mat, rings):
    return F.as_object(name, F.rings_solid(rings), mat)


# ---------------------------------------------------------------- polypropylene storage box (20 L) with lid

ORG = dict(w0=0.37, d0=0.25, w1=0.42, d1=0.30, h=0.19, t=0.0025, rim=0.008, r=0.025)


def tub(name, mat, w0, d0, w1, d1, h, t, rim, r):
    rings = [rr(w0, d0, r * 0.8, 0.0), rr(w1, d1, r, h - 0.007), rr(w1 + 2 * rim, d1 + 2 * rim, r + rim, h - 0.007),
             rr(w1 + 2 * rim, d1 + 2 * rim, r + rim, h), rr(w1 - 2 * t, d1 - 2 * t, r - t, h), rr(w0 - 2 * t, d0 - 2 * t, r * 0.8 - t, t)]
    return solid(name, mat, rings)


def caixa_organizadora():
    o = ORG
    tub('Corpo', 'plastico', o['w0'], o['d0'], o['w1'], o['d1'], o['h'], o['t'], o['rim'], o['r'])
    wl, dl, h = o['w1'] + 2 * o['rim'] + 0.006, o['d1'] + 2 * o['rim'] + 0.006, o['h']
    solid('Tampa', 'plastico', [rr(wl - 0.07, dl - 0.07, 0.02, h + 0.017), rr(wl - 0.05, dl - 0.05, 0.025, h + 0.009),
                                rr(wl, dl, o['r'] + o['rim'] + 0.003, h + 0.009), rr(wl, dl, o['r'] + o['rim'] + 0.003, h - 0.010),
                                rr(wl - 0.005, dl - 0.005, o['r'] + o['rim'], h - 0.010), rr(wl - 0.005, dl - 0.005, o['r'] + o['rim'], h + 0.002)])
    # end handles moulded under the rim, and the two lid latches on the ends
    for s in (-1, 1):
        R.box(f'Pega {s:+d}', 'plastico', s * (o['w1'] / 2 + 0.002) - 0.006 * (s < 0), -0.07, h - 0.030, s * (o['w1'] / 2 + 0.002) + 0.006 * (s > 0), 0.07,
              h - 0.024, r=0.002, seg=1)
        x0 = s * (wl / 2) - (0.004 if s > 0 else -0.004)
        R.box(f'Trava {s:+d}', 'trava', min(x0, x0 + s * 0.010), -0.035, h - 0.035, max(x0, x0 + s * 0.010), 0.035, h + 0.012, r=0.003, seg=2)


# ---------------------------------------------------------------- parts bin ("caixa bin" n. 5: 150 x 250 x 130 mm)

BIN = dict(w=0.150, d=0.250, h=0.130, front=0.075, t=0.0025)


def bin_plastico():
    w, d, h, fh, t = BIN['w'], BIN['d'], BIN['h'], BIN['front'], BIN['t']
    prof = [(d / 2, 0.0), (d / 2, h), (d / 2 - 0.08, h)]
    for i in range(1, 9):   # the scooped side edge, a quarter ellipse down to the front lip
        a = i / 8 * math.pi / 2
        prof.append((d / 2 - 0.08 - (d - 0.08) * math.sin(a), fh + (h - fh) * math.cos(a)))
    prof.append((-d / 2, 0.0))
    for s in (-1, 1):
        R.extrude(f'Lateral {s:+d}', 'bin', prof, 'YZ', s * w / 2 - (t if s > 0 else 0), s * w / 2 + (t if s < 0 else 0), r=0.0008, seg=1)
    R.box('Fundo', 'bin', -w / 2 + t, d / 2 - t, 0, w / 2 - t, d / 2, h, r=0.0008, seg=1)
    R.box('Gancho', 'bin', -w / 2 + 0.01, d / 2, h - 0.012, w / 2 - 0.01, d / 2 + 0.008, h, r=0.001, seg=1)
    R.box('Frente', 'bin', -w / 2 + t, -d / 2, 0, w / 2 - t, -d / 2 + t, fh, r=0.0008, seg=1)
    R.cyl('Borda da frente', 'bin', (0, -d / 2 + 0.002, fh), 0.0035, w - 0.004, axis=(1, 0, 0), seg=10, r=0.0006)
    R.box('Base', 'bin', -w / 2 + t, -d / 2 + t, 0, w / 2 - t, d / 2 - t, t, r=0, seg=1)
    # label holder: a shallow frame on the front for a paper card
    R.box('Porta-etiqueta', 'bin', -0.050, -d / 2 - 0.002, 0.028, 0.050, -d / 2, 0.058, r=0.0008, seg=1)


# ---------------------------------------------------------------- plastic pallet ("estrado", 1.20 x 1.00 x 0.14 m, 9 feet)

def estrado():
    Lx, Ly, Hh, top = 1.20, 1.00, 0.14, 0.014
    holes = []
    xs = [-0.55 + 0.1375 * i for i in range(9)]
    for i in range(8):
        for j in range(12):
            u0 = 0.025 + i * 0.1425
            v0 = 0.030 + j * 0.0795
            if any(abs(u0 + 0.05 - (fx + Lx / 2)) < 0.09 for fx in (-0.52, 0.0, 0.52)) and any(abs(v0 + 0.02 - (fy + Ly / 2)) < 0.09 for fy in (-0.42, 0.0, 0.42)):
                continue          # solid over the feet
            holes.append((u0 + 0.012, u0 + 0.118, v0 + 0.010, v0 + 0.050))
    bm = bmesh.new()
    PE._plate(bm, (-Lx / 2, -Ly / 2, Hh - top), (1, 0, 0), (0, 1, 0), (0, 0, 1), Lx, Ly, top, holes)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    R._finish('Deck', bm, 'pead')
    for fx in (-0.52, 0.0, 0.52):
        for fy in (-0.42, 0.0, 0.42):
            R.box(f'Pe {fx:+.2f} {fy:+.2f}', 'pead', fx - 0.075, fy - 0.075, 0.0, fx + 0.075, fy + 0.075, Hh - top + 0.002, r=0.010, seg=2)
    for fy in (-0.42, 0.0, 0.42):   # ribs under the deck between the feet
        R.box(f'Nervura {fy:+.2f}', 'pead', -Lx / 2 + 0.02, fy - 0.012, Hh - top - 0.020, Lx / 2 - 0.02, fy + 0.012, Hh - top + 0.001, r=0.003, seg=1)
    # the edge frame of the deck, closed
    for s in (-1, 1):
        R.box(f'Borda x {s:+d}', 'pead', -Lx / 2, s * Ly / 2 - (0.02 if s > 0 else 0), Hh - top - 0.012, Lx / 2, s * Ly / 2 + (0.02 if s < 0 else 0), Hh, r=0.004, seg=1)
        R.box(f'Borda y {s:+d}', 'pead', s * Lx / 2 - (0.02 if s > 0 else 0), -Ly / 2, Hh - top - 0.012, s * Lx / 2 + (0.02 if s < 0 else 0), Ly / 2, Hh, r=0.004, seg=1)


# ---------------------------------------------------------------- platform cart (0.90 x 0.60 m deck, 100 mm castors, 150 kg)

CART = dict(w=0.60, l=0.90, deck=0.190, wheel_r=0.050, handle_h=0.93)


def _castor(prefix, x, y, swivel, top):
    """Wheel in a pressed steel fork under a mounting plate; the swivel ones trail their wheel behind the kingpin."""
    wr = CART['wheel_r']
    R.box(prefix + ' placa', 'zinco', x - 0.040, y - 0.035, top - 0.005, x + 0.040, y + 0.035, top, r=0.002, seg=1)
    off = 0.032 if swivel else 0.0
    wy = y + off
    if swivel:
        R.cyl(prefix + ' giro', 'zinco', (x, y, top - 0.011), 0.030, 0.012, seg=24, r=0.002)
    zc = wr
    for s in (-1, 1):
        R.extrude(f'{prefix} garfo {s:+d}', 'zinco', [(y - 0.028, top - 0.016), (y + 0.028, top - 0.016), (wy + 0.022, zc), (wy + 0.012, zc - 0.012), (wy - 0.012, zc - 0.012),
                                                    (wy - 0.022, zc)], 'YZ', x + s * 0.022 - 0.0015, x + s * 0.022 + 0.0015, r=0.0006, seg=1)
        R.cyl(f'{prefix} porca {s:+d}', 'zinco', (x + s * 0.0245, wy, zc), 0.007, 0.004, axis=(1, 0, 0), seg=6, r=0.0005)
    R.box(prefix + ' ponte', 'zinco', x - 0.024, y - 0.028, top - 0.018, x + 0.024, y + 0.028, top - 0.013, r=0.001, seg=1)
    tire = [(0.0, -0.016), (0.030, -0.016), (wr - 0.006, -0.015), (wr - 0.001, -0.010), (wr, 0.0), (wr - 0.001, 0.010), (wr - 0.006, 0.015),
            (0.030, 0.016), (0.0, 0.016)]
    R.lathe(prefix + ' roda', 'borracha', [(r, z) for r, z in tire], seg=28, r=0.0,
            matrix=Matrix.Translation((x, wy, zc)) @ Matrix.Rotation(math.pi / 2, 4, 'Y'))
    R.cyl(prefix + ' cubo', 'cubo', (x, wy, zc), 0.024, 0.036, axis=(1, 0, 0), seg=20, r=0.002)
    R.cyl(prefix + ' eixo', 'zinco', (x, wy, zc), 0.006, 0.050, axis=(1, 0, 0), seg=8, r=0.0005)


def carrinho():
    w, l, deck = CART['w'], CART['l'], CART['deck']
    t = 0.0025
    # steel deck pan, folded down 35 mm all round, with the ribbed rubber mat on top
    R.box('Plataforma', 'azul', -w / 2, -l / 2, deck - t, w / 2, l / 2, deck, r=0.0012, seg=1)
    for s in (-1, 1):
        R.box(f'Dobra x {s:+d}', 'azul', -w / 2, s * l / 2 - (t if s > 0 else 0), deck - 0.035, w / 2, s * l / 2 + (t if s < 0 else 0), deck, r=0.0008, seg=1)
        R.box(f'Dobra y {s:+d}', 'azul', s * w / 2 - (t if s > 0 else 0), -l / 2, deck - 0.035, s * w / 2 + (t if s < 0 else 0), l / 2, deck, r=0.0008, seg=1)
        R.box(f'Longarina {s:+d}', 'azul', s * 0.18 - 0.02, -l / 2 + 0.01, deck - 0.040, s * 0.18 + 0.02, l / 2 - 0.01, deck - t, r=0.002, seg=1)
    R.box('Manta', 'manta', -w / 2 + 0.012, -l / 2 + 0.012, deck, w / 2 - 0.012, l / 2 - 0.012, deck + 0.004, r=0.0015, seg=1)
    for sx in (-1, 1):
        for sy in (-1, 1):
            R.cyl(f'Batente {sx:+d}{sy:+d}', 'borracha', (sx * (w / 2 + 0.006), sy * (l / 2 + 0.006), deck - 0.016), 0.016, 0.034, seg=16, r=0.003)
            _castor(f'Rodizio {sx:+d}{sy:+d}', sx * 0.22, sy * (l / 2 - 0.09), sy > 0, deck - 0.040)
    # U handle (25 mm tube) at the back (+Y), in two sockets, rubber grip on the top bar
    hx, hy, top = w / 2 - 0.04, l / 2 - 0.022, CART['handle_h']
    R.tube('Alca', 'azul', [(-hx, hy, deck + 0.004), (-hx, hy + 0.03, top - 0.10), (-hx, hy + 0.04, top), (hx, hy + 0.04, top),
                            (hx, hy + 0.03, top - 0.10), (hx, hy, deck + 0.004)], 0.0125, fillet=0.07)
    R.cyl('Punho', 'borracha', (0, hy + 0.04, top), 0.0155, 0.34, axis=(1, 0, 0), seg=16, r=0.003)
    for s in (-1, 1):
        R.box(f'Encaixe da alca {s:+d}', 'azul', s * hx - 0.022, hy - 0.022, deck - 0.030, s * hx + 0.022, hy + 0.022, deck + 0.050, r=0.003, seg=1)
        R.cyl(f'Pino da alca {s:+d}', 'zinco', (s * (hx + 0.023), hy, deck + 0.028), 0.006, 0.006, axis=(1, 0, 0), seg=10, r=0.001)


# ---------------------------------------------------------------- formalin 5 L jug and spill tray

JUG = (0.190, 0.125, 0.255)


def galao():
    L, W, H = JUG
    rects, size = I.net(L, W, H)
    _paint_galao(rects, size, IMPRESSOS + '/GalaoFormol')
    body = solid('Corpo', 'pead', [rr(L - 0.012, W - 0.012, 0.022, 0.0), rr(L, W, 0.026, 0.006), rr(L, W, 0.026, H - 0.035),
                                   rr(L - 0.030, W - 0.025, 0.030, H - 0.008), rr(L - 0.070, W - 0.055, 0.026, H)])
    I.cube_uv(body, rects, size, L, W, H)
    nx = 0.045
    neck = R.cyl('Gargalo', 'pead', (nx, 0, H + 0.012), 0.021, 0.026, seg=24, r=0.001)
    cap = R.cyl('Tampa', 'tampa', (nx, 0, H + 0.030), 0.025, 0.024, seg=32, r=0.002)
    hd = R.tube('Alca', 'pead', [(-0.070, 0.0, H - 0.020), (-0.062, 0.0, H + 0.040), (-0.010, 0.0, H + 0.046), (0.004, 0.0, H - 0.004)], 0.010, fillet=0.03)
    for o in (neck, cap, hd):
        I.blank_uv(o, size)


def _paint_galao(rects, size, out):
    L, W, H = JUG
    c = P.Canvas(rects, size, 2048, seed=0x5A1, bg=(214, 214, 204))
    for f in rects:
        c.board(f, (214, 214, 204), kraft=False)
    lines = [('ALMOX. · PRODUTOS QUÍMICOS', 0.92, 0.07, P.SANS_B), ('FORMOL 10% TAMPONADO', 0.78, 0.13, P.SANS_B),
             ('Solução de formaldeído · 5 L', 0.64, 0.085, P.SANS), ('PERIGO', 0.50, 0.11, P.SANS_B),
             ('Pode provocar câncer. Tóxico se inalado.', 0.38, 0.065, P.SANS), ('Manter fechado e em bandeja de contenção.', 0.29, 0.065, P.SANS),
             ('VAL.: 05/2027', 0.10, 0.075, P.MONO)]
    c.sticker('frente', L / 2, H * 0.47, L * 0.86, H * 0.62, lines, tilt=0.6, paper=(238, 238, 232), soil=0.10)

    def ghs(d, w, h, k):
        for cx, kind in ((L * 0.66, 'saude'), (L * 0.84, 'exclamacao')):
            cy, s = h - H * 0.26 * k, 0.020 * k
            cxp = cx * k
            d.polygon([(cxp, cy - s), (cxp + s, cy), (cxp, cy + s), (cxp - s, cy)], outline=255, width=max(2, int(0.0022 * k)))
    c.ink('frente', ghs, (190, 20, 24), fade=1.0, erode=0.05)

    def sym(d, w, h, k):
        cy = h - H * 0.26 * k
        x = L * 0.84 * k
        d.rectangle((x - 0.0018 * k, cy - 0.011 * k, x + 0.0018 * k, cy + 0.003 * k), fill=255)
        d.ellipse((x - 0.002 * k, cy + 0.006 * k, x + 0.002 * k, cy + 0.010 * k), fill=255)
        x = L * 0.66 * k
        d.ellipse((x - 0.0035 * k, cy - 0.012 * k, x + 0.0035 * k, cy - 0.005 * k), fill=255)
        d.polygon([(x - 0.008 * k, cy + 0.010 * k), (x - 0.006 * k, cy - 0.004 * k), (x + 0.006 * k, cy - 0.004 * k), (x + 0.008 * k, cy + 0.010 * k)], fill=255)
        pts = []
        for i in range(16):
            a = math.pi * 2 * i / 16
            r = (0.0045 if i % 2 == 0 else 0.0022) * k
            pts.append((x + r * math.cos(a), cy + 0.003 * k + r * math.sin(a)))
        d.polygon(pts, fill=0)
    c.ink('frente', sym, (20, 20, 22), fade=1.0, erode=0.0)
    for f in ('frente', 'fundo', 'esquerda', 'direita'):
        c.wear(f, edge=0.006, bottom=0.03, amount=0.5)
    c.save(out)


def bandeja():
    tub('Corpo da bandeja', 'pead', 0.60, 0.40, 0.62, 0.42, 0.075, 0.004, 0.010, 0.03)


# ---------------------------------------------------------------- disposable sheet roll (70 cm x 50 m)

ROLL = dict(r=0.055, l=0.70, core=0.021)


def rolo_lencol():
    r, l = ROLL['r'], ROLL['l']
    rects, size = I.roll_net(r, l)
    _paint_rolo(rects, size, IMPRESSOS + '/RoloLencol')
    body = R.cyl('Rolo', 'papel', (0, 0, r), r, l, axis=(1, 0, 0), seg=40, r=0.004)
    I.roll_uv(body, rects, size, r, l, cz=r)


def _paint_rolo(rects, size, out):
    r, l = ROLL['r'], ROLL['l']
    c = P.Canvas(rects, size, 2048, seed=0x7011, bg=(232, 232, 226))
    for f in rects:
        c.board(f, (232, 232, 226), kraft=False)
    # paper band label round the middle; the ends show the wound layers and the cardboard core
    x0, y0, w, h = rects['manto']
    band = 0.11

    def lbl(d, ww, hh, k):
        d.rectangle((0, hh - (l / 2 + band / 2) * k, ww, hh - (l / 2 - band / 2) * k), fill=255)
    c.ink('manto', lbl, (70, 120, 170), fade=0.9, erode=0.05)
    for k_, rep in enumerate((0.08, 0.30)):
        c.text('manto', [('LENÇOL HOSPITALAR DESCARTÁVEL', w * (rep + 0.18), l / 2 + 0.022, 0.016, P.SANS_B, w * 0.42),
                         ('70 cm × 50 m · 100% celulose', w * (rep + 0.18), l / 2 - 0.022, 0.013, P.SANS, w * 0.42)], (245, 245, 240), erode=0.05)
    for f in ('ponta_e', 'ponta_d'):
        def rings(d, ww, hh, k):
            cx, cy = ww / 2, hh / 2
            for i in range(0, int((r - ROLL['core']) * k), max(2, int(0.0012 * k))):
                rad = ROLL['core'] * k + i
                d.ellipse((cx - rad, cy - rad, cx + rad, cy + rad), outline=90, width=1)
        c.ink(f, rings, (180, 180, 172), fade=0.5, erode=0.2)

        def core(d, ww, hh, k):
            cx, cy = ww / 2, hh / 2
            d.ellipse((cx - ROLL['core'] * k, cy - ROLL['core'] * k, cx + ROLL['core'] * k, cy + ROLL['core'] * k), fill=255)
        c.ink(f, core, (150, 112, 70), fade=1.0, erode=0.0)

        def hole(d, ww, hh, k):
            cx, cy, rr_ = ww / 2, hh / 2, (ROLL['core'] - 0.003) * k
            d.ellipse((cx - rr_, cy - rr_, cx + rr_, cy + rr_), fill=255)
        c.ink(f, hole, (40, 32, 24), fade=1.0, erode=0.0)
    c.wear('manto', edge=0.008, amount=0.4)
    c.save(out)


# ---------------------------------------------------------------- Mauro's screwdriver, adapted handle, replacement tag

def ferramenta():
    rects = {'etiqueta': (I.PAD, I.PAD, 0.085, 0.045)}
    size = 0.085 + 2 * I.PAD
    _paint_etiqueta(rects, size, IMPRESSOS + '/Ferramenta')
    # thick bi-material handle (38 mm, for a weak grip) along +X, lying on the shelf
    hr = 0.019
    prof = [(0.0, 0.000), (0.012, 0.000), (0.0165, 0.004), (0.019, 0.020), (0.0185, 0.045), (0.0170, 0.075), (0.0180, 0.098), (0.0160, 0.115), (0.0100, 0.128),
            (0.0060, 0.134), (0.0, 0.134)]
    parts = [R.lathe('Cabo', 'cabo', prof, seg=24, r=0.0, matrix=Matrix.Translation((-0.10, 0, hr)) @ Matrix.Rotation(math.pi / 2, 4, 'Y'))]
    for i, (x0, x1) in enumerate(((0.030, 0.050), (0.058, 0.078), (0.086, 0.104))):
        rmax = max(r for r, z in prof if x0 <= z <= x1 + 0.01) + 0.0008
        parts.append(R.cyl(f'Faixa de borracha {i}', 'borracha', (-0.10 + (x0 + x1) / 2, 0, hr), rmax, x1 - x0, axis=(1, 0, 0), seg=24, r=0.0015))
    parts.append(R.cyl('Ponteira', 'cromo', (-0.10 + 0.140, 0, hr), 0.0050, 0.014, axis=(1, 0, 0), seg=16, r=0.001))
    parts.append(R.cyl('Haste', 'cromo', (-0.10 + 0.134 + 0.075, 0, hr), 0.0030, 0.150, axis=(1, 0, 0), seg=12, r=0.0004))
    tip = -0.10 + 0.284
    parts.append(R.hull('Ponta', 'cromo', [(tip, -0.0030, hr - 0.0030), (tip, 0.0030, hr - 0.0030), (tip, -0.0030, hr + 0.0030), (tip, 0.0030, hr + 0.0030),
                                           (tip + 0.014, -0.0030, hr - 0.0006), (tip + 0.014, 0.0030, hr - 0.0006), (tip + 0.014, -0.0030, hr + 0.0006),
                                           (tip + 0.014, 0.0030, hr + 0.0006)], r=0.0003, seg=1))
    # card tag lying flat in front of the handle, cord through its eyelet tied round the ferrule
    tx0, ty0 = -0.095, -0.105
    tag = R.box('Etiqueta', 'etiqueta', tx0, ty0, 0.0, tx0 + 0.085, ty0 + 0.045, 0.0005, r=0.0, seg=1)
    I.planar_uv(tag, rects['etiqueta'], size, tx0, ty0)
    ring = R.cyl('Ilhos', 'cromo', (tx0 + 0.077, ty0 + 0.0225, 0.0006), 0.0035, 0.0006, seg=14, r=0.0)
    cord = R.tube('Barbante', 'barbante', [(tx0 + 0.077, ty0 + 0.0225, 0.0012), (tx0 + 0.095, ty0 + 0.050, 0.002), (0.020, -0.030, 0.004),
                                           (0.040, -0.008, hr - 0.004), (0.042, 0.0, hr + 0.0055), (0.044, 0.008, hr - 0.002), (0.048, 0.010, 0.002)],
                  0.0008, fillet=0.006)
    for o in parts + [ring, cord]:
        I.blank_uv(o, size)


def _paint_etiqueta(rects, size, out):
    c = P.Canvas(rects, size, 1024, seed=0x4E1)
    c.board('etiqueta', (226, 208, 164), kraft=False)
    W, H = rects['etiqueta'][2:]
    c.text('etiqueta', [('ETIQUETA DE REPOSIÇÃO', W * 0.46, H * 0.86, H * 0.13, P.SANS_B, W * 0.80)], (40, 40, 44), erode=0.1)

    def fields(d, w, h, k):
        for i, (t, v) in enumerate((('Material:', 0.64), ('Setor:', 0.42), ('Autorizado:', 0.20))):
            y = h - v * H * k
            d.text((0.004 * k, y), t, font=P.font(P.SANS, H * 0.10 * k), fill=255, anchor='lb')
            d.line((0.022 * k if i < 2 else 0.026 * k, y, 0.072 * k, y), fill=200, width=max(1, int(0.0003 * k)))
    c.ink('etiqueta', fields, (50, 50, 54), fade=0.95, erode=0.1)
    c.hand('etiqueta', 0.021, H * 0.68, 'chave de fenda – cabo adaptado', H * 0.13)
    c.hand('etiqueta', 0.016, H * 0.46, 'Manutenção', H * 0.14)
    c.hand('etiqueta', 0.027, H * 0.25, 'H. Duarte', H * 0.16, tilt=-4.0)
    c.wear('etiqueta', edge=0.003, amount=0.5)
    c.save(out)

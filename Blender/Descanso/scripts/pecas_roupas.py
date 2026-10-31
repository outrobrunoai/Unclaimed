"""Hanging garments of the rest room: a clean lab coat and a hooded PVC raincoat, each on its own hanger.
Cut as pieces (front and back panels, sleeves, collar, lapels, pockets, flap) and draped with a hand-made gravity fold field,
then thickened: static meshes, no simulation. Loaded by gerar.py (R, F injected).
Frame: origin on the wall hook's arm (hanger loop centre), garment hangs towards -Z, front is -Y, x across."""
import math
import bmesh
import bpy
from mathutils import Vector

smooth, lerp, interp, spow = F.smooth, F.lerp, F.interp, F.spow
HANGER_DX = 0.0085          # hanger body centre is this far from the origin (the loop sits beside the stem)


# ---------------------------------------------------------------- hanger

def cabide(body_mat, wire_mat='aco'):
    """Plastic coat hanger: shoulder arms with a bottom bar, neck block, wire hook that wraps the wall hook's arm
    (arm centre = origin before the final shift)."""
    for s in (-1, 1):
        R.extrude(f'Braco do cabide {s:+d}', body_mat, [(0.0, -0.0825), (s * 0.206, -0.1375), (s * 0.206, -0.1560), (0.0, -0.1010)][::s], 'XZ', -0.0042, 0.0042, r=0.0018, seg=2)
    R.box('Barra do cabide', body_mat, -0.206, -0.0036, -0.1575, 0.206, 0.0036, -0.1425, r=0.0016, seg=2)
    R.box('Pescoco do cabide', body_mat, -0.014, -0.0048, -0.1010, 0.014, 0.0048, -0.0740, r=0.0025, seg=2)
    r_loop, zc = 0.0085, -0.0037
    pts = [(0.0, 0.0, -0.088), (0.0, 0.0, zc)]
    for k in range(1, 15):                                # over the arm: from the stem round the top to the tip
        a = k * math.radians(215) / 14
        pts.append((-r_loop + r_loop * math.cos(a), 0.0, zc + r_loop * math.sin(a)))
    F.wire('Gancho do cabide', wire_mat, pts, 0.0016)
    R.cyl('Luva do gancho', body_mat, (0.0, 0.0, -0.0820), 0.0036, 0.016, seg=10, r=0.0008)


# ---------------------------------------------------------------- garment cut

def z_shoulder(x, tip):
    """Fabric top line: rests on the hanger arm (14 deg slope), drops more steeply past the hanger end."""
    ax = abs(x)
    z = -0.0825 - 0.30 * max(min(ax, 0.206) - 0.016, 0.0) + 0.0035
    if ax > 0.206:
        z -= 0.42 * (ax - 0.206)
    return z


class Cut:
    """Parametric front/back surface of a coat body. All lengths in metres, z negative downwards."""
    def __init__(self, P):
        self.P = P
        self.z0 = z_shoulder(0.0, P['tip'])
        self.hem = P['hem']

    def W(self, z):
        return interp([(-a, b) for a, b in self.P['W']], -z)       # keys are listed top to bottom (z falling)

    def D(self, z):
        return interp([(-a, b) for a, b in self.P['D']], -z)

    def fold(self, u, z, face):
        """Gravity folds: wide soft folds that fan out from the shoulders and deepen towards the hem."""
        P = self.P
        r = min(max((-z + self.z0) / (self.z0 - self.hem), 0.0), 1.0)
        amp = P['fold0'] + P['fold1'] * r ** 1.2
        psi = u * (1.0 + 0.5 * r)
        ph = P['phase'] + (2.1 if face > 0 else 0.0)
        f = 0.62 * math.sin(2 * math.pi * P['freq'] * psi + ph + 0.8 * r) + 0.38 * math.sin(2 * math.pi * 2.1 * P['freq'] * psi + 2.3 * ph + 1.7 * r)
        env = (1 - abs(u) ** 3.5) * smooth(0.0, 0.16, r)
        return amp * env * f

    def top(self, x, face):
        P = self.P
        z = z_shoulder(x, P['tip'])
        if face < 0:    # front neckline: open V (collar and lapels cover it)
            nw = P['neck_w']
            z = min(z, self.z0 - P['neck_drop'] * max(0.0, 1.0 - abs(x) / nw) ** 1.5)
        else:
            nw = P['neck_w']
            z = min(z, self.z0 - 0.012 * max(0.0, 1.0 - abs(x) / nw) ** 1.5)
        return z

    def point(self, u, v, face):
        """Point of the front (face -1) or back (+1) panel; v 0 = shoulder line, 1 = hem."""
        P = self.P
        zc = lerp(self.z0, self.hem, v)
        w = self.W(zc)
        x = u * w
        zt = self.top(x, face)
        wave = 0.5 * P['hem_wave'] * math.sin(2 * math.pi * 1.4 * u + (0.6 if face < 0 else 2.0)) + 0.4 * P['hem_wave'] * math.sin(2 * math.pi * 3.1 * u + 1.3 * face)
        z = zc + (zt - self.z0) * (1 - v) ** 2 + wave * smooth(0.0, 1.0, v) ** 3 * (1 - abs(u) ** 3)
        d = self.D(z)
        sh = (1 - abs(u) ** 2.6) ** (1 / 2.6)
        y = face * d * sh + self.fold(u, z, face)
        y = min(y, -P['gap']) if face < 0 else max(y, P['gap'])        # the two layers touch, never cross
        return Vector((x, y, z))

    def front_y(self, x, z):
        """Outer surface y of the front panel at (x, z): used to sit pockets and buttons on the cloth."""
        P = self.P
        w = self.W(z)
        u = max(-0.999, min(0.999, x / w))
        d = self.D(z)
        sh = (1 - abs(u) ** 2.6) ** (1 / 2.6)
        return -d * sh + self.fold(u, z, -1)


def body_panels(P, cut, t, mat):
    nu, nv = P.get('nu', 34), P.get('nv', 46)
    for face, name in ((-1, 'Frente'), (1, 'Costas')):
        bm = F.surface(lambda i, j: cut.point(-1 + 2 * i / nu, (j / nv) ** 1.15, face), nu, nv)
        F.shell(name, mat, bm, t, inside=(0, 0, -0.5))


def sleeves(P, cut, t, mat):
    """Sleeve tubes hanging from the shoulder tip: the axis leaves along the shoulder slope and falls to the vertical, so the
    cap flows out of the shoulder line; rings are perpendicular to the axis."""
    nr, ns = 24, 34
    L = P['sleeve_len']
    for s in (-1, 1):
        # axis by integration
        axis = []
        x, y, z = P['tip'] - 0.020, -0.004, P['sleeve_top']
        for k in range(ns + 1):
            q = k / ns
            th = math.radians(P['sleeve_tilt']) * (1 - smooth(0.0, 0.26, q))
            axis.append((x, y, z, th))
            ds = L / ns
            x += math.sin(th) * ds + 0.012 * math.sin(math.pi * q) * ds * 0.0
            z -= math.cos(th) * ds
            y -= P['sleeve_fwd'] * ds / L * (0.3 + q)
        def ring(i, j):
            q = j / ns
            cx, cy, cz, th = axis[j]
            rx = interp(P['sleeve_rx'], q)
            ry = interp(P['sleeve_ry'], q)
            a = 2 * math.pi * i / nr
            bunch = 1.0 + P['bunch'] * math.sin(2 * math.pi * (P['bunch_n'] * q + 0.15 * s)) * smooth(0.25, 0.55, q) * (1 - smooth(0.7, 0.95, q))
            wrinkle = 1.0 + P['sleeve_fold'] * math.sin(3 * a + 5.5 * q + 1.3 * s) * smooth(0.1, 0.5, q)
            gather = 1.0 - P['cuff_gather'] * smooth(0.88, 1.0, q)
            ex, ey = rx * bunch * wrinkle * gather, ry * bunch * wrinkle * gather
            tw = 0.10 * q * s
            ca, sa = math.cos(a + tw), math.sin(a + tw)
            return Vector((s * cx + s * ex * ca * math.cos(th), cy + ey * sa, cz + ex * ca * math.sin(th)))
        bm = F.surface(ring, nr, ns, wrap=True)
        F.shell(f'Manga {s:+d}', mat, bm, t, inside=(s * 0.27, -0.01, P['sleeve_top'] - L / 2))


# ---------------------------------------------------------------- details on the cloth

def patch(name, mat, cut, x0, x1, z0, z1, t, lift, belly=0.0, corner=0.014, top_gap=0.0, nu=14, nv=14):
    """Patch pocket / flap laid on the front: a rounded rectangle sampled from the folded cloth, lifted by `lift`; belly
    adds a bulge (weight of the contents) that grows towards the open top edge (top_gap)."""
    hw, hh = (x1 - x0) / 2, (z1 - z0) / 2
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2

    def P(i, j):
        s, tt = -1 + 2 * i / nu, -1 + 2 * j / nv
        px, pz = F.rrect_map(s, tt, hw, hh, corner, corner * (1 if top_gap == 0 else 0.35) if tt > 0 else corner)
        x, z = cx + px, cz + pz
        top = (pz / hh + 1) / 2                                    # 0 at the sewn bottom, 1 at the top edge
        y = cut.front_y(x, z) - lift - belly * math.sin(math.pi * min(max(top, 0), 1) ** 0.8) * (1 - abs(s) ** 3) - top_gap * smooth(0.6, 1.0, top) * (1 - abs(s) ** 4)
        return Vector((x, y, z))
    bm = F.surface(P, nu, nv)
    ob = F.shell(name, mat, bm, t, inside=(cx, 0.05, cz))
    return ob


def stitch_line(name, mat, cut, pts_xz, lift, radius=0.0007):
    pts = []
    for x, z in pts_xz:
        pts.append((x, cut.front_y(x, z) - lift, z))
    return F.wire(name, mat, pts, radius)


def rect_stitch(x0, x1, z0, z1, inset, open_top=True, n=8):
    """Path round three sides of a pocket (open top)."""
    a, b, c, d = x0 + inset, x1 - inset, z0 + inset, z1 - inset
    pts = [(a, d)]
    pts += [(a, d + (c - d) * k / n) for k in range(1, n + 1)]
    pts += [(a + (b - a) * k / n, c) for k in range(1, n + 1)]
    pts += [(b, c + (d - c) * k / n) for k in range(1, n + 1)]
    return pts


def collar_band(name, mat, cut, P, t):
    """Turn-down collar: a band round the back of the neck that stands, rolls over and falls on the shoulders."""
    nu, nv = 40, 8
    rx, ry = P['neck_w'] * 1.0, 0.0165
    rows = P['collar_rows']                                     # (outwards factor, height) from the neckline

    def Pt(i, j):
        al = math.radians(34 + (360 - 68) * i / nu)               # from the front-right gorge, round the back
        f = 0.55 + 0.45 * math.sin(math.pi * i / nu) ** 0.6       # narrower at the ends
        q = j / nv
        # polyline through the rows, sampled smoothly
        k = q * (len(rows) - 1)
        a, b = int(math.floor(k)), min(int(math.floor(k)) + 1, len(rows) - 1)
        tt = k - a
        rf = lerp(rows[a][0], rows[b][0], tt)
        h = lerp(rows[a][1], rows[b][1], tt)
        x = rx * rf * math.sin(al)
        y = -ry * rf * math.cos(al) * (1 if math.cos(al) > 0 else 0.9)
        z = z_shoulder(x, P['tip']) + 0.002 + h * f
        return Vector((x, y * 0.95 + 0.004, z))
    bm = F.surface(Pt, nu, nv)
    return F.shell(name, mat, bm, t, inside=(0, 0.0, -0.12))


def lapel(s, name, mat, cut, P, t):
    """Notch lapel turned back from the roll line; s = +1 right of the picture."""
    nu, nv = 10, 22
    p = P['lapel']                                              # (x_neck, z_neck, x_out, z_out, x_btn, z_btn)
    xn, zn, xo, zo, xb, zb = p

    def Pt(i, j):
        a, b = i / nu, j / nv
        # a: from the roll line (0) to the outer edge (1); b: from the neck (0) to the first button (1)
        x_roll = lerp(xn, xb, b)
        x_edge = lerp(xo, xb + 0.052, b) if b > 0.35 else lerp(xo, lerp(xo, xb + 0.052, 0.35), b / 0.35)
        z = lerp(zn, zb, b) + (zo - zn) * (1 - b) * a * 0.55
        x = lerp(x_roll, x_edge, a)
        y = cut.front_y(s * x, z) - 0.003 - 0.010 * smooth(0.0, 1.0, a) ** 1.4 * (1 - 0.5 * b)
        return Vector((s * x, y, z))
    bm = F.surface(Pt, nu, nv)
    return F.shell(name, mat, bm, t, inside=(s * 0.05, 0.02, zn - 0.1), flip=False)


def button(name, mat, x, z, cut, lift, r=0.0072):
    y = cut.front_y(x, z) - lift
    R.cyl(name, mat, (x, y - 0.0012, z), r, 0.0024, axis=(0, 1, 0), seg=14, r=0.0006)


# ---------------------------------------------------------------- the two garments

def jaleco():
    P = dict(
        tip=0.206, neck_w=0.060, neck_drop=0.100, hem=-0.985, gap=0.0036,
        W=[(-0.0825, 0.206), (-0.17, 0.212), (-0.24, 0.232), (-0.30, 0.258), (-0.62, 0.276), (-0.985, 0.300)],
        D=[(-0.080, 0.0105), (-0.115, 0.015), (-0.19, 0.026), (-0.36, 0.027), (-0.65, 0.019), (-0.985, 0.0135)],
        fold0=0.0040, fold1=0.0140, phase=0.4, freq=1.25, hem_wave=0.018,
        sleeve_len=0.64, sleeve_top=-0.158, sleeve_tilt=30, sleeve_fwd=0.030,
        sleeve_rx=[(0.0, 0.030), (0.09, 0.048), (0.5, 0.050), (1.0, 0.038)], sleeve_ry=[(0.0, 0.018), (0.09, 0.022), (0.5, 0.016), (1.0, 0.013)],
        bunch=0.12, bunch_n=4, sleeve_fold=0.20, cuff_gather=0.05,
        lapel=(0.040, -0.115, 0.095, -0.158, 0.012, -0.335),
        collar_rows=[(1.0, 0.000), (1.04, 0.026), (1.22, 0.040), (1.58, 0.026), (1.95, 0.004)],
    )
    cut = Cut(P)
    cabide('plastico')
    t = 0.0030
    body_panels(P, cut, t, 'tecido')
    sleeves(P, cut, t, 'tecido')
    collar_band('Gola', 'tecido', cut, P, t)
    for s in (-1, 1):
        lapel(s, f'Lapela {s:+d}', 'tecido', cut, P, 0.0026)
    # front closure: the right panel lies over the left; its free edge is a visible step
    nz = 40

    def over(i, j):
        a, b = i / 6, j / nz
        zc = lerp(-0.30, P['hem'] + 0.03, b)
        x = lerp(-0.038, 0.050, a)
        return Vector((x, cut.front_y(x, zc) - 0.0042, zc))
    F.shell('Sobreposicao', 'tecido', F.surface(over, 6, nz), 0.0030, inside=(0.0, 0.03, -0.6))
    xb = 0.020
    for i, zb in enumerate((-0.37, -0.52, -0.67, -0.82, -0.94)):
        button(f'Botao {i}', 'botao', xb, zb, cut, 0.0072)
    # pockets: two patch pockets on the hip (one carries a folded paper), a breast pocket with a pen
    pk = [('Bolso direito', 0.070, 0.222, -0.640, -0.470, 0.0035), ('Bolso esquerdo', -0.222, -0.070, -0.650, -0.480, 0.0)]
    for name, x0, x1, z0, z1, bel in pk:
        patch(name, 'tecido', cut, x0, x1, z0, z1, 0.0028, lift=0.0016, belly=bel + 0.0012, corner=0.016, top_gap=0.003)
        stitch = rect_stitch(x0, x1, z0, z1, 0.007)
        stitch_line(name + ' - costura', 'costura', cut, stitch, 0.0046 + bel * 0.0)
    patch('Bolso do peito', 'tecido', cut, -0.215, -0.118, -0.318, -0.240, 0.0026, lift=0.0016, belly=0.0015, corner=0.012, top_gap=0.002)
    stitch_line('Bolso do peito - costura', 'costura', cut, rect_stitch(-0.215, -0.118, -0.318, -0.240, 0.0055), 0.0046)
    yb = cut.front_y(-0.146, -0.236) - 0.0041
    R.box('Caneta', 'plastico', -0.151, yb - 0.0095, -0.292, -0.141, yb - 0.0035, -0.195, r=0.0012, seg=2)
    R.box('Clipe da caneta', 'aco', -0.1497, yb - 0.0107, -0.265, -0.1423, yb - 0.0092, -0.205, r=0.0004, seg=1)
    # hem stitching and cuff turn-ups
    stitch_line('Barra - costura', 'costura', cut, [(x, P['hem'] + 0.028 + 0.0) for x in (-0.26, -0.13, 0.0, 0.13, 0.26)], 0.0045)
    F.shift_asset('Jaleco', HANGER_DX)


def capa_de_chuva():
    P = dict(
        tip=0.206, neck_w=0.050, neck_drop=0.028, hem=-1.045, gap=0.0028,
        W=[(-0.0825, 0.206), (-0.17, 0.214), (-0.24, 0.246), (-0.30, 0.282), (-0.62, 0.312), (-1.045, 0.350)],
        D=[(-0.080, 0.0105), (-0.115, 0.017), (-0.19, 0.034), (-0.36, 0.034), (-0.65, 0.024), (-1.045, 0.015)],
        fold0=0.003, fold1=0.0115, phase=1.3, freq=0.85, hem_wave=0.018,
        sleeve_len=0.66, sleeve_top=-0.158, sleeve_tilt=30, sleeve_fwd=0.030,
        sleeve_rx=[(0.0, 0.034), (0.10, 0.056), (0.5, 0.054), (1.0, 0.046)], sleeve_ry=[(0.0, 0.022), (0.10, 0.028), (0.5, 0.026), (1.0, 0.022)],
        bunch=0.05, bunch_n=3, sleeve_fold=0.07, cuff_gather=0.16,
        lapel=(0.040, -0.115, 0.095, -0.158, 0.012, -0.335),
        collar_rows=[(1.0, 0.000), (1.0, 0.040), (1.03, 0.056), (1.10, 0.058)],
    )
    cut = Cut(P)
    cabide('plastico_preto')
    t = 0.0022
    body_panels(P, cut, t, 'capa')
    sleeves(P, cut, t, 'capa')
    collar_band('Gola alta', 'capa', cut, P, t)
    # storm flap over the zip with snaps
    nz = 44

    def flap(i, j):
        a, b = i / 6, j / nz
        zc = lerp(-0.125, P['hem'] + 0.02, b)
        x = lerp(-0.034, 0.034, a)
        return Vector((x, cut.front_y(x, zc) - 0.0050 - 0.002 * math.sin(math.pi * a), zc))
    F.shell('Aba do ziper', 'capa', F.surface(flap, 6, nz), 0.0022, inside=(0.0, 0.03, -0.6))
    for i, zb in enumerate((-0.30, -0.47, -0.64, -0.81, -0.98)):
        R.cyl(f'Botao de pressao {i}', 'botao', (0.0, cut.front_y(0.0, zb) - 0.0075, zb), 0.0085, 0.0030, axis=(0, 1, 0), seg=16, r=0.0008)
    for name, x0, x1, z0, z1, bel in (('Bolso direito', 0.085, 0.245, -0.700, -0.520, 0.006), ('Bolso esquerdo', -0.245, -0.085, -0.700, -0.520, 0.002)):
        patch(name, 'capa', cut, x0, x1, z0, z1, 0.0022, lift=0.0016, belly=bel, corner=0.014, top_gap=0.003)
        patch(name + ' - aba', 'capa', cut, x0 - 0.004, x1 + 0.004, z1 - 0.012, z1 + 0.050, 0.0022, lift=0.0075, belly=0.0, corner=0.010)
        stitch_line(name + ' - costura', 'costura', cut, rect_stitch(x0, x1, z0, z1, 0.007), 0.0044)
    # hood lying on the back, seen above the shoulders: open bag with a rim
    nr, nh = 28, 20
    def hood(i, j):
        a = 2 * math.pi * i / nr
        q = j / nh
        z = lerp(-0.050, -0.260, q)
        rx = 0.105 * (0.65 + 0.35 * math.sin(math.pi * min(q * 1.2 + 0.05, 1.0)) ** 0.5) * (1 - 0.45 * q ** 3)
        ry = 0.026 * (0.5 + 0.5 * smooth(0.0, 0.4, q)) * (1 - 0.5 * q ** 3)
        wob = 1 + 0.06 * math.sin(3 * a + 4 * q)
        return Vector((rx * math.sin(a) * wob, cut.D(z) + 0.003 + ry * (1 + math.cos(a)) * 0.9, z))
    bm = F.surface(hood, nr, nh, wrap=True)
    F.shell('Capuz', 'capa', bm, 0.0022, inside=(0, 0.07, -0.18))
    F.shift_asset('CapaDeChuva', HANGER_DX)

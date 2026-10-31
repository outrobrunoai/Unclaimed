"""Footwear: white PVC boots and white occupational clogs, as left/right pairs.
Built from a foot last (loft of cross sections with a flat sole and a rounded toe box), hollowed with a boolean so the opening, the
inner wall and the thickness are real; boots add a tilted shaft with a rolled rim. Toes towards -Y, soles on z = 0 (+x is the left shoe).
Loaded by gerar.py (R, F injected)."""
import math
from mathutils import Vector, Matrix

smooth, lerp, spow = F.smooth, F.lerp, F.spow


def interp(keys, x):
    """Catmull-Rom through (x, value) keys: no plateaus at the keys (smoothstep interpolation terraces the toe box)."""
    if x <= keys[0][0]:
        return keys[0][1]
    for i in range(len(keys) - 1):
        (x0, v0), (x1, v1) = keys[i], keys[i + 1]
        if x <= x1:
            t = (x - x0) / (x1 - x0)
            vm = keys[max(i - 1, 0)][1]
            vp = keys[min(i + 2, len(keys) - 1)][1]
            m0 = 0.5 * (v1 - vm) if i > 0 else 0.0
            m1 = 0.5 * (vp - v0) if i + 2 < len(keys) else 0.0
            t2, t3 = t * t, t * t * t
            return (2 * t3 - 3 * t2 + 1) * v0 + (t3 - 2 * t2 + t) * m0 + (-2 * t3 + 3 * t2) * v1 + (t3 - t2) * m1
    return keys[-1][1]


def last(keys_w, keys_h, z0, medial, inset=0.0, n_sec=44, ring=40, nbot=5.0, ntop=2.3, shift=0.0):
    """Closed solid of a shoe last. keys_w: (y, half width), keys_h: (y, height above z0). medial: side of the big toe (+1/-1).
    inset shrinks every dimension (cavity); shift moves y."""
    y0, y1 = keys_w[0][0], keys_w[-1][0]
    rings = []
    for k in range(n_sec + 1):
        y = y0 + (y1 - y0) * (0.5 - 0.5 * math.cos(math.pi * k / n_sec))      # denser at both ends
        w = max(interp(keys_w, y) - inset, 0.0006)
        h = max(interp(keys_h, y) - inset * 2, 0.0012)
        yy = y + shift
        cx = -medial * 0.006 * smooth(0.0, 1.0, (y1 - y) / (y1 - y0)) ** 2     # the toes point a little towards the big-toe side
        ring_pts = []
        for i in range(ring):
            a = 2 * math.pi * i / ring
            s, c = math.sin(a), math.cos(a)
            wm = w * (1.05 if s * medial > 0 else 0.96)                        # fuller on the big-toe side
            x = cx + wm * spow(abs(s), 2 / (ntop if c > 0 else nbot)) * (1 if s >= 0 else -1)
            z = z0 + inset + h * 0.5 + h * 0.5 * spow(abs(c), 2 / (ntop if c > 0 else nbot)) * (1 if c >= 0 else -1)
            ring_pts.append(Vector((x, yy, z)))
        rings.append(ring_pts)
    return F.rings_solid(rings, caps=True)


def tilted_prism(name, cx, cy, z0, z1, rx, ry, lean_y, n=28, grow=1.0):
    """Elliptic prism whose axis leans towards +Y by lean_y per metre: the opening of a boot or the mouth of a clog."""
    rings = []
    for k in range(7):
        z = lerp(z0, z1, k / 6)
        g = lerp(1.0, grow, k / 6)
        rings.append([Vector((cx + rx * g * math.cos(2 * math.pi * i / n), cy + lean_y * (z - z0) + ry * g * math.sin(2 * math.pi * i / n), z)) for i in range(n)])
    return F.as_object(name, F.rings_solid(rings, caps=True), 'branco')


def sole(name, mat, keys_w, y_heel, z0, z1, medial, heel_lift=0.0, r=0.006, grow=0.003):
    """Outsole: the plan outline of the last, a little larger, extruded; the heel block is taller."""
    ys = [keys_w[0][0] + (keys_w[-1][0] - keys_w[0][0]) * k / 40 for k in range(41)]
    ys = [y for y in ys if keys_w[0][0] + 0.004 < y < keys_w[-1][0] - 0.004]
    left, right = [], []
    for y in ys:
        w = interp(keys_w, y) + grow
        cx = -medial * 0.006 * smooth(0.0, 1.0, (keys_w[-1][0] - y) / (keys_w[-1][0] - keys_w[0][0])) ** 2
        left.append((cx - w * (0.96 if medial > 0 else 1.05), y))
        right.append((cx + w * (1.05 if medial > 0 else 0.96), y))
    poly = left + list(reversed(right))
    ob = R.extrude(name, mat, poly, 'XY', z0, z1, r=r, seg=2, min_angle=25.0)
    return ob


def apply_bool(a, parts, op='DIFFERENCE'):
    for b in parts:
        F.boolean(a, b, op)
    return a


# ---------------------------------------------------------------- boots

BOOT_W = [(-0.150, 0.0), (-0.142, 0.022), (-0.125, 0.036), (-0.095, 0.049), (-0.060, 0.053), (-0.020, 0.049), (0.030, 0.043), (0.090, 0.039), (0.130, 0.036), (0.147, 0.0)]
BOOT_H = [(-0.150, 0.0), (-0.142, 0.026), (-0.125, 0.046), (-0.095, 0.062), (-0.060, 0.080), (-0.020, 0.128), (0.030, 0.150), (0.090, 0.150), (0.130, 0.125), (0.147, 0.0)]


def bota(s, tag):
    """One PVC boot: x offset applied by the caller via the matrix. medial = -s (the big toe faces the other boot)."""
    med = -s
    z_s = 0.024
    outer = F.as_object(f'Pe {tag}', last(BOOT_W, BOOT_H, z_s - 0.002, med), 'branco')
    # shaft: tilted back a little, flared at the top; its lower end sinks into the foot
    def shaft(inset, z_top, name):
        rings = []
        for k in range(19):
            z = lerp(0.034, z_top, k / 18)
            q = max(z - 0.082, 0.0) / 0.278
            cy = lerp(0.062, 0.072, q) + 0.006 * math.sin(math.pi * min(q * 1.6, 1.0))
            ax = lerp(0.044, 0.058, smooth(0.0, 0.7, q)) - inset
            ay = lerp(0.060, 0.060, q) - inset
            cx = -med * 0.002 * (1 - q)
            wob = 1 + 0.012 * math.sin(14 * q) * smooth(0.0, 0.25, q) * (1 - smooth(0.25, 0.6, q))     # soft creases above the ankle
            rings.append([Vector((cx + ax * wob * spow(abs(math.cos(2 * math.pi * i / 30)), 2 / 2.4) * (1 if math.cos(2 * math.pi * i / 30) >= 0 else -1),
                                  cy + ay * wob * spow(abs(math.sin(2 * math.pi * i / 30)), 2 / 2.4) * (1 if math.sin(2 * math.pi * i / 30) >= 0 else -1), z)) for i in range(30)])
        return F.as_object(name, F.rings_solid(rings, caps=True), 'branco')
    sh = shaft(0.0, 0.360, f'Cano {tag}')
    F.boolean(outer, sh, 'UNION')
    cav_foot = F.as_object(f'_cav_pe {tag}', last(BOOT_W, BOOT_H, z_s - 0.002, med, inset=0.0042), 'branco')
    cav_shaft = shaft(0.0042, 0.395, f'_cav_cano {tag}')
    F.boolean(cav_foot, cav_shaft, 'UNION')
    F.boolean(outer, cav_foot, 'DIFFERENCE')
    # rolled rim at the top of the shaft
    cy, ax, ay = 0.076, 0.060, 0.064
    rim = [(ax * math.cos(2 * math.pi * k / 40) - med * 0.0, cy + ay * math.sin(2 * math.pi * k / 40), 0.358) for k in range(41)]
    R.tube(f'Borda do cano {tag}', 'branco', rim, 0.0036, res=2)
    sole(f'Sola {tag}', 'borracha', BOOT_W, 0.02, 0.0, z_s, med, r=0.006)


# ---------------------------------------------------------------- clogs

CLOG_W = [(-0.145, 0.0), (-0.138, 0.020), (-0.120, 0.034), (-0.090, 0.045), (-0.055, 0.048), (-0.010, 0.042), (0.040, 0.037), (0.095, 0.036), (0.128, 0.034), (0.142, 0.0)]
CLOG_H = [(-0.145, 0.0), (-0.138, 0.024), (-0.120, 0.038), (-0.090, 0.046), (-0.055, 0.056), (-0.010, 0.074), (0.040, 0.080), (0.095, 0.070), (0.128, 0.050), (0.142, 0.0)]


def tamanco(s, tag):
    med = -s
    z_s = 0.018
    outer = F.as_object(f'Cabedal {tag}', last(CLOG_W, CLOG_H, z_s - 0.002, med), 'branco')
    cav = F.as_object(f'_cav {tag}', last(CLOG_W, CLOG_H, z_s - 0.002, med, inset=0.0035), 'branco')
    F.boolean(outer, cav, 'DIFFERENCE')
    # the foot goes in from above: remove the upper behind the instep (leaves the heel cup low), leaning towards the heel
    mouth = tilted_prism(f'_boca {tag}', 0.0, 0.046, z_s + 0.026, 0.22, 0.060, 0.074, 0.10)
    F.boolean(outer, mouth, 'DIFFERENCE')
    # ventilation holes on the toe box
    for k, (x, y) in enumerate(((-0.014, -0.105), (0.014, -0.105), (-0.026, -0.082), (0.0, -0.082), (0.026, -0.082), (-0.014, -0.058), (0.014, -0.058))):
        hole = R.cyl('_furo', 'branco', (x - med * 0.004, y, 0.07), 0.0042, 0.07, seg=10, r=0)
        F.detach(hole)
        F.boolean(outer, hole, 'DIFFERENCE')
    sole(f'Sola {tag}', 'borracha', CLOG_W, 0.02, 0.0, z_s, med, r=0.007)
    # pivoting heel strap: a flat band over the heel
    pts = [(-0.041, 0.052, 0.040), (-0.044, 0.088, 0.054), (-0.030, 0.126, 0.062), (0.0, 0.138, 0.062), (0.030, 0.126, 0.062), (0.044, 0.088, 0.054), (0.041, 0.052, 0.040)]
    F.ribbon(f'Tira do calcanhar {tag}', 'branco', pts, 0.018, 0.004, up_hint=(0, 0, 1), round_=0.5)


# ---------------------------------------------------------------- pairs

def _pair(fn, dx, tag_prefix):
    """Builds the pair into the current asset collection: +x is the left shoe. The right one is built at the origin then moved."""
    before = set(R._coll(R._current['asset']).objects)
    for s, tag in ((1, 'E'), (-1, 'D')):
        mark = set(R._coll(R._current['asset']).objects)
        fn(s, tag)
        yaw = 0.05 * s
        m = Matrix.Translation((s * dx, 0.012 * (1 if s > 0 else -1) * 0.0 + (0.012 if s < 0 else 0.0), 0.0)) @ Matrix.Rotation(yaw, 4, 'Z')
        for o in set(R._coll(R._current['asset']).objects) - mark:
            o.data.transform(m)


def botas():
    _pair(bota, 0.085, 'bota')


def tamancos():
    _pair(tamanco, 0.062, 'tamanco')

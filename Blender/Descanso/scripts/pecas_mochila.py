"""Daypack of the rest room (one reusable model). Soft nylon body built as a superellipse loft (flat back, belly in front,
slumped top, weight spreading the base), front zip pocket, two side pockets, top zip, grab handle, padded shoulder
straps hanging loose, rubber base. Origin: centre of the base on the floor, front -Y. Loaded by gerar.py (R, F injected)."""
import math
from mathutils import Vector

smooth, lerp, interp = F.smooth, F.lerp, F.interp

H = 0.455
# z, half width, front depth, back depth, squareness
SEC = [(0.000, 0.138, 0.080, 0.064, 4.0), (0.012, 0.152, 0.088, 0.068, 3.4), (0.050, 0.160, 0.098, 0.070, 3.0), (0.130, 0.162, 0.108, 0.071, 2.9),
       (0.220, 0.158, 0.102, 0.070, 2.9), (0.320, 0.148, 0.090, 0.068, 2.8), (0.400, 0.136, 0.078, 0.064, 2.6), (0.440, 0.120, 0.064, 0.058, 2.4), (H, 0.092, 0.044, 0.046, 2.2)]


def sec(z):
    ks = [k[0] for k in SEC]
    return [interp([(k[0], k[i]) for k in SEC], z) for i in (1, 2, 3, 4)]


def lean(z):
    """The top slumps backward under the weight of what is below."""
    return 0.030 * (max(z, 0.0) / H) ** 2.2


def pt(theta, z, lift=0.0):
    """Point of the body surface. theta 0 = front centre, growing towards +X; lift moves it along the normal (approx)."""
    w, df, db, n = sec(z)
    c, s = math.cos(theta), math.sin(theta)
    x = w * F.spow(abs(s), 2 / n) * (1 if s >= 0 else -1)
    d = df if c >= 0 else db
    y = -d * F.spow(abs(c), 2 / n) * (1 if c >= 0 else -1) * 1.0
    # pleats near the top (fabric gathers above the stuffed part)
    k = 1.0 + 0.020 * math.sin(7 * theta + 1.1) * smooth(0.22, 0.42, z / H * 1.0) + 0.012 * math.sin(11 * theta + 0.4) * smooth(0.3, 0.45, z / H)
    x, y = x * k, y * k
    y += lean(z) + 0.5 * (df - db) * 0.0
    p = Vector((x, y, z))
    if lift:
        eps = 0.01
        a, b = pt(theta + eps, z), pt(theta - eps, z)
        up = pt(theta, z + 0.01) - pt(theta, z - 0.01)
        t = (a - b).normalized()
        nrm = t.cross(up).normalized()
        out = Vector((s, -c, 0.0)).normalized()
        if nrm.dot(out) < 0:
            nrm = -nrm
        p = p + nrm * lift
    return p


def build_body():
    zs = [0.0, 0.006, 0.014, 0.028, 0.05] + [0.05 + (0.35 - 0.05) * i / 12 for i in range(1, 13)] + [0.36, 0.385, 0.41, 0.43, 0.442, 0.452, H]
    n = 40
    rings = [[pt(2 * math.pi * i / n, z) for i in range(n)] for z in zs]
    # close the top into a low dome: the fabric sags a little in the middle
    top = rings[-1]
    cen = Vector((sum(p.x for p in top) / n, sum(p.y for p in top) / n, H))
    for f, dz in ((0.72, 0.003), (0.42, 0.0045), (0.12, 0.004)):
        rings.append([Vector((cen.x + (p.x - cen.x) * f, cen.y + (p.y - cen.y) * f, H + dz)) for p in top])
    return F.as_object('Corpo', F.rings_solid(rings, caps=True), 'tecido')


def pocket(name, mat, th0, th1, z0, z1, bulge, t=0.0024, nu=16, nv=16, top_open=0.0, corner=0.022):
    """Zip/stretch pocket laid on the body between angles th0..th1 and heights z0..z1; bulges outwards by `bulge`
    (largest in the middle, zero on the sewn bottom and sides), top edge lifted by top_open."""
    cth, hth = (th0 + th1) / 2, (th1 - th0) / 2
    cz, hz = (z0 + z1) / 2, (z1 - z0) / 2

    def P(i, j):
        s, tt = -1 + 2 * i / nu, -1 + 2 * j / nv
        px, pz = F.rrect_map(s, tt, 1.0, 1.0, corner / hth / 0.15, corner / hz)
        th, z = cth + px * hth, cz + pz * hz
        top = (pz + 1) / 2
        b = bulge * (1 - abs(px) ** 3) * math.sin(math.pi * min(max(top, 0), 1) ** 0.7) ** 1.1 + top_open * smooth(0.7, 1.0, top) * (1 - abs(px) ** 4)
        return pt(th, z, 0.0015 + b)
    bm = F.surface(P, nu, nv)
    mid = pt(cth, cz, -0.05)
    return F.shell(name, mat, bm, t, inside=mid)


def zip_path(th0, th1, z, lift, n=14, drop=0.0):
    return [tuple(pt(th0 + (th1 - th0) * k / n, z + drop * math.sin(math.pi * k / n), lift)) for k in range(n + 1)]


def mochila():
    # --- body and base
    build_body()
    # rubber base band, 4 cm, a little proud of the fabric
    n = 40
    rings = [[pt(2 * math.pi * i / n, z, 0.0022) for i in range(n)] for z in (0.0, 0.006, 0.02, 0.036, 0.046)]
    bm = F.rings_solid(rings, caps=True)
    F.as_object('Fundo', bm, 'fundo')
    # --- front zip pocket (lighter fabric panel): zip along its top, piping round the rest
    th0, th1, z0, z1 = -0.62, 0.62, 0.050, 0.262
    pocket('Bolso frontal', 'tecido2', th0, th1, z0, z1, bulge=0.036, top_open=0.012)
    zt = z1 - 0.004
    R.tube('Ziper do bolso', 'zip', zip_path(th0 + 0.07, th1 - 0.07, zt, 0.0360, drop=0.0), 0.0020, res=2)
    R.tube('Fita do ziper do bolso', 'tira', zip_path(th0 + 0.05, th1 - 0.05, zt + 0.006, 0.0300, drop=0.0), 0.0045, res=2)
    p0 = pt(0.34, zt, 0.0360)
    R.box('Cursor do bolso', 'zip', p0.x - 0.007, p0.y - 0.011, p0.z - 0.010, p0.x + 0.007, p0.y - 0.001, p0.z + 0.006, r=0.0018, seg=2)
    R.tube('Puxador do bolso', 'tira', [(p0.x, p0.y - 0.008, p0.z - 0.008), (p0.x, p0.y - 0.012, p0.z - 0.030), (p0.x + 0.002, p0.y - 0.011, p0.z - 0.046)], 0.0042, res=2)
    # piping round the pocket sides and bottom
    pipe = []
    for k in range(9):
        pipe.append(tuple(pt(th0, z1 - 0.012 - (z1 - z0 - 0.04) * k / 8, 0.0021)))
    for k in range(1, 15):
        pipe.append(tuple(pt(th0 + (th1 - th0) * k / 14, z0 + 0.003, 0.0021)))
    for k in range(9):
        pipe.append(tuple(pt(th1, z0 + 0.03 + (z1 - z0 - 0.04) * k / 8, 0.0021)))
    R.tube('Vivo do bolso', 'tira', pipe, 0.0022, res=2)
    # --- side pockets (stretch mesh, bottle shaped), the right one holding a bottle
    for s, nm in ((-1, 'esquerdo'), (1, 'direito')):
        t0, t1 = (1.30, 1.95) if s > 0 else (-1.95, -1.30)
        pocket('Bolso lateral ' + nm, 'tecido', t0, t1, 0.040, 0.215, bulge=0.030 if s > 0 else 0.016, top_open=0.010, corner=0.020)
    # --- top: zip seam round the main compartment and two pulls, grab handle
    ring = [tuple(pt(2 * math.pi * k / 40, 0.438, 0.0016)) for k in range(41)]
    R.tube('Ziper principal', 'zip', ring, 0.0020, res=2)
    for k, th in enumerate((0.15, -0.15)):
        q = pt(th, 0.438, 0.002)
        R.tube(f'Puxador principal {k}', 'tira', [(q.x, q.y - 0.002, q.z + 0.004), (q.x + 0.004 * (1 if th > 0 else -1), q.y - 0.016, q.z - 0.006), (q.x + 0.006 * (1 if th > 0 else -1), q.y - 0.020, q.z - 0.034)], 0.0040, res=2)
    cy = (pt(math.pi * 0.5, H - 0.002).y + pt(-math.pi * 0.5, H - 0.002).y) / 2
    handle = [(-0.052, cy, H - 0.012), (-0.050, cy, H + 0.010), (-0.030, cy, H + 0.040), (0.0, cy, H + 0.050), (0.030, cy, H + 0.040), (0.050, cy, H + 0.010), (0.052, cy, H - 0.012)]
    F.ribbon('Alca de mao', 'tira', handle, 0.026, 0.0045, up_hint=(0, 1, 0))
    for sx in (-1, 1):
        R.box(f'Reforco da alca {sx:+d}', 'tira', sx * 0.052 - 0.020, cy - 0.016, H - 0.012, sx * 0.052 + 0.020, cy + 0.016, H - 0.008, r=0.001, seg=1)
    # --- padded shoulder straps hanging on the back, loosely (they sag away from the fabric)
    for s in (-1, 1):
        pts = []
        for k in range(15):
            q = k / 14
            z = 0.418 - 0.372 * q
            x = s * (0.080 + 0.030 * q)
            stand = 0.008 + 0.034 * math.sin(math.pi * q) ** 1.5 + 0.010 * q
            pts.append((x, 0.071 + lean(z) + stand, z))
        F.ribbon(f'Alca {s:+d}', 'tira', pts, 0.062, 0.016, up_hint=(0, 1, 0), w_end=0.036, t_end=0.0045, round_=0.4)
        end = pts[-1]
        R.box(f'Regulador {s:+d}', 'zip', end[0] - 0.020, end[1] - 0.004, end[2] - 0.006, end[0] + 0.020, end[1] + 0.006, end[2] + 0.008, r=0.0015, seg=1)
        R.tube(f'Ponta da alca {s:+d}', 'tira', [(end[0], end[1] + 0.002, end[2] - 0.004), (end[0] + s * 0.004, end[1] + 0.014, end[2] - 0.060)], 0.0085, res=2)
    # back panel pad: two ribs
    for s in (-1, 1):
        F.ribbon(f'Almofada {s:+d}', 'tira', [(s * 0.062, 0.0715 + lean(z), z) for z in (0.40, 0.30, 0.20, 0.10, 0.05)], 0.058, 0.014, up_hint=(0, 1, 0), round_=0.5)

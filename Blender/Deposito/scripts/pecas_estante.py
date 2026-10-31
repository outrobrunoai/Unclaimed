"""Steel storage shelving ("estante de aco"), after the common Brazilian catalogue units: L-section uprights in 0.9 mm
sheet (#20) with a row of slots on each flange every 50 mm, shelves of 0.45 mm sheet (#26) folded down 30 mm on all four
sides with a return, a hat-section stiffener under each shelf, bolted at every corner, X straps on the back and the sides,
plastic feet. 0.92 m wide, 1.98 m high; depths 0.30 / 0.40 / 0.60 m.
Origin: floor, centre of the footprint; front towards -Y. Needs R (recepcao_modelo) in the namespace."""
import bmesh, math
from mathutils import Vector, Matrix

W, H = 0.92, 1.98
A = 0.032          # flange width of the uprights
TC = 0.0012        # upright sheet (drawn a little over 0.9 mm so it survives mip-mapping)
TS = 0.0010        # shelf sheet
LIP = 0.030        # shelf fold depth
RET = 0.009        # return of the fold, inwards
FOOT = 0.010       # plastic foot under each upright
SLOT = (0.0055, 0.016)      # slot width x height
PITCH = 0.050

# Shelf heights (top face) per depth; the top shelf closes the frame.
LEVELS = {
    0.30: (0.10, 0.47, 0.84, 1.21, 1.58, 1.98),
    0.40: (0.10, 0.47, 0.84, 1.21, 1.58, 1.98),
    0.60: (0.10, 0.55, 1.00, 1.45, 1.98),
}


def _plate(bm, origin, u, v, n, uw, vh, t, holes):
    """Flat sheet in the (u, v) plane, thickness t along n, with rectangular holes [(u0, u1, v0, v1)] cut through."""
    us = sorted({0.0, uw, *[h[0] for h in holes], *[h[1] for h in holes]})
    vs = sorted({0.0, vh, *[h[2] for h in holes], *[h[3] for h in holes]})
    o, u, v, n = Vector(origin), Vector(u), Vector(v), Vector(n)
    grid = [[bm.verts.new(o + u * a + v * b) for a in us] for b in vs]
    faces = []
    for j in range(len(vs) - 1):
        for i in range(len(us) - 1):
            cu, cv = (us[i] + us[i + 1]) / 2, (vs[j] + vs[j + 1]) / 2
            if any(h[0] <= cu <= h[1] and h[2] <= cv <= h[3] for h in holes):
                continue
            faces.append(bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i])))
    bmesh.ops.solidify(bm, geom=faces, thickness=t)
    return faces


def _upright(name, sx, sy, D):
    """L-section upright at corner (sx, sy) in {-1, 1}: one flange on the front/back face, one on the side face."""
    bm = bmesh.new()
    holes = []
    z = 0.075
    while z < H - 0.05:
        holes.append((A / 2 - SLOT[0] / 2 + 0.002, A / 2 + SLOT[0] / 2 + 0.002, z, z + SLOT[1]))
        z += PITCH
    x0, y0 = sx * W / 2, sy * D / 2
    # flange along X (face of constant y), running from the corner inwards
    _plate(bm, (x0, y0, FOOT), (-sx, 0, 0), (0, 0, 1), (0, sy, 0), A, H - FOOT, TC, holes)
    # flange along Y (face of constant x)
    _plate(bm, (x0, y0, FOOT), (0, -sy, 0), (0, 0, 1), (sx, 0, 0), A, H - FOOT, TC, holes)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    return R._finish(name, bm, 'aco')


def _bolt(name, p, axis):
    """Round-head bolt through the upright (head outside) with its nut inside, along axis (unit vector, outward)."""
    a = Vector(axis)
    R.cyl(name + ' cabeca', 'parafuso', tuple(Vector(p) + a * (TC + 0.0012)), 0.0052, 0.0024, axis=tuple(a), seg=10, r=0.0009)
    R.cyl(name + ' porca', 'parafuso', tuple(Vector(p) - a * (TS + 0.0022)), 0.0050, 0.0036, axis=tuple(a), seg=6, r=0.0003)


def _shelf(prefix, D, z):
    """Folded tray between the uprights' inner faces; top face at z."""
    x1, y1 = W / 2 - TC, D / 2 - TC
    zl = z - LIP
    R.box(prefix + ' chapa', 'aco', -x1, -y1, z - TS, x1, y1, z, r=0, seg=1)
    for s in (-1, 1):
        # front/back folds with the return; side folds stop short of them (mitred corner left open, as bent sheet does)
        R.box(prefix + f' aba {s:+d}y', 'aco', -x1, s * y1 - (TS if s > 0 else 0), zl, x1, s * y1 + (0 if s > 0 else TS), z - TS, r=0, seg=1)
        ya, yb = sorted((s * (y1 - TS), s * (y1 - RET)))
        R.box(prefix + f' retorno {s:+d}y', 'aco', -x1 + 0.004, ya, zl, x1 - 0.004, yb, zl + TS, r=0, seg=1)
        R.box(prefix + f' aba {s:+d}x', 'aco', s * x1 - (TS if s > 0 else 0), -y1 + 0.003, zl, s * x1 + (0 if s > 0 else TS), y1 - 0.003, z - TS,
              r=0, seg=1)
    # hat-section stiffeners under the sheet, across the width (one per 0.30 m of depth)
    n = max(1, round(D / 0.30))
    for k in range(n):
        yc = -y1 + (k + 0.5) * (2 * y1 / n)
        poly = [(yc - 0.022, z - TS), (yc - 0.012, z - TS), (yc - 0.008, z - 0.016), (yc + 0.008, z - 0.016),
                (yc + 0.012, z - TS), (yc + 0.022, z - TS), (yc + 0.022, z - TS - 0.0010), (yc + 0.0112, z - TS - 0.0010),
                (yc + 0.0072, z - 0.0170), (yc - 0.0072, z - 0.0170), (yc - 0.0112, z - TS - 0.0010), (yc - 0.022, z - TS - 0.0010)]
        R.extrude(prefix + f' reforco {k}', 'aco', poly, 'YZ', -x1 + 0.002, x1 - 0.002, r=0, seg=1)
    # two bolts per corner: through the front/back flange and through the side flange, at mid-fold height
    zb = z - LIP / 2
    for sx in (-1, 1):
        for sy in (-1, 1):
            _bolt(f'{prefix} parafuso {sx:+d}{sy:+d}a', (sx * (W / 2 - A / 2 + 0.002), sy * D / 2, zb), (0, sy, 0))
            _bolt(f'{prefix} parafuso {sx:+d}{sy:+d}b', (sx * W / 2, sy * (D / 2 - A / 2 + 0.002), zb), (sx, 0, 0))


def _strap(name, p0, p1, n, off):
    """Flat X-brace strap, 18 mm x 1.2 mm, laid on a face with outward normal n, off its plane by off."""
    p0, p1, n = Vector(p0) + Vector(n) * off, Vector(p1) + Vector(n) * off, Vector(n)
    d = (p1 - p0).normalized()
    w = n.cross(d).normalized() * 0.009
    t = n * 0.0012
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in (p0 - w, p1 - w, p1 + w, p0 + w, p0 - w + t, p1 - w + t, p1 + w + t, p0 + w + t)]
    for f in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
        bm.faces.new([vs[i] for i in f])
    R._finish(name, bm, 'aco')
    for k, p in enumerate((p0, p1)):
        R.cyl(f'{name} rebite {k}', 'parafuso', tuple(p + n * 0.0018 + d * (0.010 if k == 0 else -0.010)), 0.0048, 0.0018,
              axis=tuple(n), seg=10, r=0.0007)


def estante(D):
    levels = LEVELS[D]
    for sx in (-1, 1):
        for sy in (-1, 1):
            _upright(f'Coluna {sx:+d}{sy:+d}', sx, sy, D)
            # plastic foot: a cap under the L, a little wider than it
            fx, fy = sx * (W / 2 - A / 2 + 0.001), sy * (D / 2 - A / 2 + 0.001)
            R.box(f'Sapata {sx:+d}{sy:+d}', 'sapata', fx - A / 2 - 0.002, fy - A / 2 - 0.002, 0.0, fx + A / 2 + 0.002, fy + A / 2 + 0.002, FOOT + 0.004,
                  r=0.0015, seg=1)
    for i, z in enumerate(levels):
        _shelf(f'Prateleira {i}', D, z)
    # X straps: back face over the lower and upper halves, each side over the lower half (the upper sides stay open
    # for reaching in, as the shelves are hand-loaded)
    zb, zm, zt = levels[0] - LIP / 2, levels[len(levels) // 2] - LIP / 2, levels[-1] - LIP / 2
    xb = W / 2 - 0.012
    for (za, zb_) in ((zb, zm), (zm, zt)):
        _strap(f'Trava traseira {za:.2f} a', (-xb, D / 2, za), (xb, D / 2, zb_), (0, 1, 0), TC)
        _strap(f'Trava traseira {za:.2f} b', (xb, D / 2, za), (-xb, D / 2, zb_), (0, 1, 0), TC + 0.0013)
    yb = D / 2 - 0.012
    for sx in (-1, 1):
        _strap(f'Trava lateral {sx:+d} a', (sx * W / 2, -yb, zb), (sx * W / 2, yb, zm), (sx, 0, 0), TC)
        _strap(f'Trava lateral {sx:+d} b', (sx * W / 2, yb, zb), (sx * W / 2, -yb, zm), (sx, 0, 0), TC + 0.0013)


def estante_rasa():
    estante(0.40)


def estante_estreita():
    estante(0.30)


def estante_funda():
    estante(0.60)

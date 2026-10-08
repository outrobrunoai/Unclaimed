# Builds the stainless-steel pedestal autopsy table for Unclaimed.
# Units: meters, Z up. Origin: floor, on the pedestal axis (under the middle of the tray).
# The tray runs along X: head end at -X, drain sink at +X.
import bpy, bmesh, math
from mathutils import Vector, Matrix

COLL = "MesaAutopsia_Partes"


# ---------------------------------------------------------------- rings

def rrect_ring(cx, cy, ax, by, r, xs, ys, nc):
    """Rounded rectangle (half extents ax, by, corner radius r) as a CCW point list.
    xs / ys are the absolute positions of the vertices on the straight runs (ascending, first and
    last at the arc centres). They do not depend on the offset, so offset rings of the same outline
    stay vertex-aligned and can be bridged with quads."""
    pts = []
    sx0, sx1, sy0, sy1 = xs[0], xs[-1], ys[0], ys[-1]
    def arc(ccx, ccy, a0):
        for i in range(1, nc):
            a = math.radians(a0 + 90.0 * i / nc)
            pts.append((ccx + r * math.cos(a), ccy + r * math.sin(a)))
    pts += [(cx + ax, y) for y in ys];            arc(sx1, sy1, 0)
    pts += [(x, cy + by) for x in reversed(xs)];  arc(sx0, sy1, 90)
    pts += [(cx - ax, y) for y in reversed(ys)];  arc(sx0, sy0, 180)
    pts += [(x, cy - by) for x in xs];            arc(sx1, sy0, 270)
    return pts


def runs(c, s, n):
    return [c - s + 2 * s * i / n for i in range(n + 1)]


def bridge(bm, ring_a, ring_b):
    n = len(ring_a)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((ring_a[i], ring_a[j], ring_b[j], ring_b[i]))


def seam(bm, a, b):
    e = bm.edges.get((a, b))
    if e:
        e.seam = True


def rrect_shell(name, cx, cy, ax, by, R, profile, xs, ys, nc, top_fill="grid", bottom_fill="ngon", cut_x=()):
    """Sweeps `profile` [(d, z) ...] (d = inset from the outline) around a rounded rectangle.
    The first profile point bounds the top fill and the last one the bottom fill ("grid", "ngon"
    or None for open). z may be a number or a callable (x, y) -> z.
    UV seams: both fill boundaries, across the sweep at the middle of each corner (one cut only for
    small pieces) and across sweep and grid at the columns listed in cut_x."""
    if isinstance(xs, int): xs = runs(cx, ax - R, xs)
    if isinstance(ys, int): ys = runs(cy, by - R, ys)
    bm = bmesh.new()
    rings = []
    for d, z in profile:
        r = R - d
        assert r > 1e-4, (name, d)
        rings.append([bm.verts.new((x, y, z(x, y) if callable(z) else z))
                      for (x, y) in rrect_ring(cx, cy, ax - d, by - d, r, xs, ys, nc)])
    for a, b in zip(rings, rings[1:]):
        bridge(bm, a, b)

    n_ring = len(rings[0])
    nx, ny = len(xs) - 1, len(ys) - 1
    corner_mid = [ny + (nc - 1) // 2 + 1, ny + nc + nx + (nc - 1) // 2 + 1,
                  2 * ny + 2 * nc + nx + (nc - 1) // 2, 2 * ny + 3 * nc + 2 * nx + (nc - 1) // 2]
    small = 2 * (ax + by) < 0.45
    cuts = [0] if small else corner_mid
    top_start = ny + nc            # index of the first vertex of the top run (x descending)
    bot_start = 2 * ny + 3 * nc + nx
    col = lambda x: min(range(len(xs)), key=lambda i: abs(xs[i] - x))
    for x in cut_x:
        i = col(x)
        cuts += [top_start + (nx - i), bot_start + i]
    for c in cuts:
        for ra, rb in zip(rings, rings[1:]):
            seam(bm, ra[c % n_ring], rb[c % n_ring])
    for k in ((0,) if top_fill else ()) + ((len(rings) - 1,) if bottom_fill else ()):
        ring = rings[k]
        for i in range(n_ring):
            seam(bm, ring[i], ring[(i + 1) % n_ring])

    def fill(ring, zdef, kind):
        if kind is None:
            return
        if kind == "ngon":
            bm.faces.new(ring)
            return
        # grid: inner grid on the straight-run positions, strips out to the ring, fans at the corners
        zf = zdef if callable(zdef) else (lambda x, y, zc=zdef: zc)
        grid = [[bm.verts.new((x, y, zf(x, y))) for y in ys] for x in xs]
        for i in range(nx):
            for j in range(ny):
                bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
        it = iter(ring)
        take = lambda n: [next(it) for _ in range(n)]
        right, tr, top, tl = take(ny + 1), take(nc - 1), take(nx + 1), take(nc - 1)
        left, bl, bot, br = take(ny + 1), take(nc - 1), take(nx + 1), take(nc - 1)
        for j in range(ny):
            bm.faces.new((right[j], right[j + 1], grid[nx][j + 1], grid[nx][j]))
            bm.faces.new((left[j], left[j + 1], grid[0][ny - j - 1], grid[0][ny - j]))
        for i in range(nx):
            bm.faces.new((top[i], top[i + 1], grid[nx - i - 1][ny], grid[nx - i][ny]))
            bm.faces.new((bot[i], bot[i + 1], grid[i + 1][0], grid[i][0]))
        for x in cut_x:
            i = col(x)
            for j in range(ny):
                seam(bm, grid[i][j], grid[i][j + 1])
            seam(bm, grid[i][0], bot[i]); seam(bm, grid[i][ny], top[nx - i])
        def fan(seq, c):
            for p, q in zip(seq, seq[1:]):
                bm.faces.new((p, q, c))
        fan([right[-1]] + tr + [top[0]], grid[nx][ny])
        fan([top[-1]] + tl + [left[0]], grid[0][ny])
        fan([left[-1]] + bl + [bot[0]], grid[0][0])
        fan([bot[-1]] + br + [right[0]], grid[nx][0])

    fill(rings[0], profile[0][1], top_fill)
    fill(rings[-1], profile[-1][1], bottom_fill)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish(name, bm)


def lathe(name, profile, n, center=(0, 0, 0), axis="Z", cap_top=True, cap_bottom=True, phase=0.0):
    """Revolves [(radius, z) ...] around an axis through `center`; caps are fans."""
    bm = bmesh.new()
    rings = []
    for rad, z in profile:
        ring = []
        for i in range(n):
            a = 2 * math.pi * i / n + phase
            ring.append(bm.verts.new((rad * math.cos(a), rad * math.sin(a), z)))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        bridge(bm, a, b)
        seam(bm, a[0], b[0])
    for k, capped in ((0, cap_bottom), (len(rings) - 1, cap_top)):
        if capped:
            for i in range(n):
                seam(bm, rings[k][i], rings[k][(i + 1) % n])
    if cap_bottom:
        c = bm.verts.new((0, 0, profile[0][1]))
        for i in range(n):
            bm.faces.new((rings[0][(i + 1) % n], rings[0][i], c))
    if cap_top:
        c = bm.verts.new((0, 0, profile[-1][1]))
        for i in range(n):
            bm.faces.new((rings[-1][i], rings[-1][(i + 1) % n], c))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    rot = {"Z": Matrix.Identity(4), "X": Matrix.Rotation(math.radians(90), 4, "Y"),
           "-X": Matrix.Rotation(math.radians(-90), 4, "Y"), "Y": Matrix.Rotation(math.radians(-90), 4, "X")}[axis]
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ rot, verts=bm.verts)
    return finish(name, bm)


def rounded_profile(z0, z1, bev_b, bev_t, seg=3, d_side=0.0):
    """Profile for a rounded box: bottom fill ring -> side -> top fill ring (top first is not required)."""
    prof = []
    for i in range(seg + 1):
        p = math.radians(90.0 * i / seg)
        prof.append((d_side + bev_b - bev_b * math.sin(p), z0 + bev_b - bev_b * math.cos(p)))
    for i in range(seg + 1):
        p = math.radians(90.0 * i / seg)
        prof.append((d_side + bev_t - bev_t * math.cos(p), z1 - bev_t + bev_t * math.sin(p)))
    return prof


def rbox(name, cx, cy, z0, z1, ax, by, R, bev_t=0.004, bev_b=0.004, seg=3, nc=6, top="ngon", bottom="ngon"):
    prof = list(reversed(rounded_profile(z0, z1, bev_b, bev_t, seg)))   # top first
    return rrect_shell(name, cx, cy, ax, by, R, prof, 1, 1, nc, top_fill=top, bottom_fill=bottom)


def finish(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.data.collections[COLL].objects.link(ob)
    return ob


def tube(name, pts, radius, n=12, cap=True, res=10):
    """Pipe through `pts` along a Catmull-Rom curve, swept with parallel-transport frames.
    Seam along one side; optional flat caps."""
    P = [Vector(p) for p in pts]
    if len(P) == 2:
        path = P
    else:
        ext = [2 * P[0] - P[1]] + P + [2 * P[-1] - P[-2]]
        path = [P[0]]
        for i in range(1, len(ext) - 2):
            p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
            for k in range(1, res + 1):
                t = k / res
                path.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                                   + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t))
    tangents = []
    for i in range(len(path)):
        a_, b_ = path[max(i - 1, 0)], path[min(i + 1, len(path) - 1)]
        tangents.append((b_ - a_).normalized())
    t0 = tangents[0]
    ref = Vector((0, 0, 1)) if abs(t0.z) < 0.9 else Vector((1, 0, 0))
    nrm = t0.cross(ref).normalized()
    bm = bmesh.new()
    rings = []
    for i, (p, t) in enumerate(zip(path, tangents)):
        if i:
            nrm = (nrm - t * nrm.dot(t)).normalized()     # parallel transport
        bin_ = t.cross(nrm)
        rings.append([bm.verts.new(p + radius * (math.cos(2 * math.pi * k / n) * nrm + math.sin(2 * math.pi * k / n) * bin_))
                      for k in range(n)])
    for a_, b_ in zip(rings, rings[1:]):
        bridge(bm, a_, b_)
        seam(bm, a_[0], b_[0])
    if cap:
        for ring in (rings[0], rings[-1]):
            bm.faces.new(ring)
            for k in range(n):
                seam(bm, ring[k], ring[(k + 1) % n])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish(name, bm)


# ---------------------------------------------------------------- dimensions

X0, X1, WIDTH, R_OUT = -1.20, 1.60, 0.80, 0.06   # outline of the top: head end .. foot end
CX, HALF_X, HALF_Y = (X0 + X1) / 2, (X1 - X0) / 2, WIDTH / 2
RIM_TOP, BEAD_R = 0.94, 0.0115
BEAD_Z = RIM_TOP - BEAD_R
DRAIN_X = 1.045                    # tray outlet, on a column of the floor grid
DIV_X = 1.195                      # low divider between tray and basin (doubles as an overflow)
SINK_X0, SINK_X1 = 1.236, X1 - 0.04
SINK_CX = (SINK_X0 + SINK_X1) / 2
PANEL_FACE = X1 - 0.028            # basin-side face of the end panel

# Floor grid columns: even 9.5 cm spacing along the tray, dense across the divider and the basin walls.
XS = [-1.14 + 0.095 * i for i in range(24)] + [1.095, 1.135, 1.155, 1.168, 1.18, DIV_X, 1.21, 1.222,
      1.229, SINK_X0 - 0.002, 1.243, 1.255, 1.29, 1.34, 1.40, 1.45, 1.50, X1 - R_OUT]
YS = runs(0.0, HALF_Y - R_OUT, 8)

# divider / basin head wall profile (x -> z), the tray floor and basin floor on either side
DIVIDER = [(1.135, None), (1.155, 0.9105), (1.168, 0.9195), (1.18, 0.9240), (DIV_X, 0.9255), (1.21, 0.9240),
           (1.222, 0.9170), (1.229, 0.8600), (SINK_X0 - 0.002, 0.7700), (1.243, 0.7440), (1.255, None)]


def bead(dc, zc, rb, steps=6):
    """Rolled rim: half circle from the inner face (theta 0) over the top to the outer face."""
    return [(dc + rb * math.cos(math.radians(180.0 * i / steps)), zc + rb * math.sin(math.radians(180.0 * i / steps)))
            for i in range(steps + 1)]


def tray_floor(x, y):
    # Shallow V towards the centre line and a fall towards the outlet near the foot end.
    hw = HALF_Y - 0.04
    z = 0.8905 + 0.009 * min(abs(y) / hw, 1.0)
    if x < DRAIN_X:
        z += 0.007 * (DRAIN_X - x) / (DRAIN_X - X0)
    else:
        z += 0.010 * ((x - DRAIN_X) / (1.135 - DRAIN_X)) ** 1.5
    return z


def sink_floor(x, y):
    r = math.hypot((x - 1.40) / 0.15, y / 0.30)
    return 0.729 + 0.006 * min(r, 1.0)


def top_height(x, y):
    if x <= 1.135 + 1e-6:
        return tray_floor(x, y)
    if x >= 1.255 - 1e-6:
        return sink_floor(x, y)
    for (xa, za), (xb, zb) in zip(DIVIDER, DIVIDER[1:]):
        if xa - 1e-6 <= x <= xb + 1e-6:
            za = tray_floor(xa, y) if za is None else za
            zb = sink_floor(xb, y) if zb is None else zb
            t = (x - xa) / (xb - xa)
            return za + (zb - za) * t
    raise ValueError(x)


def build_top():
    """One welded piece: rolled rim all round, tray floor, low divider, basin, skirt."""
    floor = lambda off: (lambda x, y: min(top_height(x, y) + off, BEAD_Z - 0.0015))
    prof = [(0.040, floor(0.0)), (0.0335, floor(0.0015)), (0.0298, floor(0.0060)), (0.0282, floor(0.0130))]
    prof += bead(0.0155, BEAD_Z, BEAD_R)                      # d 0.027 -> 0.004
    prof += [(0.004, 0.808), (0.0065, 0.8015), (0.012, 0.800), (0.0175, 0.8015), (0.020, 0.808),
             (0.020, 0.862), (0.026, 0.872)]
    return rrect_shell("Tampo", CX, 0.0, HALF_X, HALF_Y, R_OUT, prof, XS, YS, 6, top_fill="grid", bottom_fill=None,
                       cut_x=(-0.475, 0.38, 1.135))


def build_under():
    parts = []
    # Underside of the tray (between the skirts) and the basin housing hanging below the skirt.
    under = rrect_shell("Fundo da bandeja", (X0 + 0.026 + 1.21) / 2, 0.0, (1.21 - X0 - 0.026) / 2, HALF_Y - 0.026,
                        0.034, [(0.0, 0.8735)], 1, 1, 6, top_fill=None, bottom_fill="ngon")
    if under.data.polygons[0].normal.z > 0:
        under.data.flip_normals()
    parts.append(under)
    # Housing wall 0.5 mm inside the skirt face so the two never overlap; it shows below the rolled hem.
    d = 0.0045
    prof = [(d, 0.85)] + [(d + 0.012 - 0.012 * math.cos(math.radians(a)), 0.700 + 0.012 - 0.012 * math.sin(math.radians(a)))
                          for a in (0, 30, 60, 90)]
    parts.append(rrect_shell("Caixa da cuba", (1.20 + X1) / 2, 0.0, (X1 - 1.20) / 2, HALF_Y, R_OUT, prof,
                             1, 1, 6, top_fill=None, bottom_fill="ngon"))
    return parts


def build_panel():
    # End panel standing on the foot-end rim: faucet, two wrist-blade valves and a vacuum breaker.
    xf = PANEL_FACE
    parts = [rbox("Painel", (xf + X1 - 0.002) / 2, 0.0, 0.925, 1.10, (X1 - 0.002 - xf) / 2, 0.30, 0.012,
                  bev_t=0.008, bev_b=0.002, seg=3)]
    zv = 1.015
    for y in (-0.15, 0.15):
        parts.append(lathe("Valvula - espelho", [(0.030, 0.0), (0.030, 0.004), (0.026, 0.008), (0.0, 0.009)], 20,
                           center=(xf + 0.0005, y, zv), axis="-X", cap_top=False))
        parts.append(lathe("Valvula - corpo", [(0.016, 0.0), (0.016, 0.026), (0.012, 0.032), (0.0, 0.033)], 16,
                           center=(xf - 0.006, y, zv), axis="-X", cap_bottom=False))
        parts.append(rbox("Valvula - alavanca", xf - 0.034, y, zv - 0.012, zv + 0.105, 0.007, 0.016, 0.0065,
                          bev_t=0.006, bev_b=0.003, seg=2, nc=4))
    # Gooseneck spout over the basin.
    parts.append(lathe("Bica - canopla", [(0.026, 0.0), (0.026, 0.005), (0.020, 0.012), (0.014, 0.020), (0.0, 0.021)], 20,
                       center=(xf + 0.0005, 0.0, zv), axis="-X", cap_top=False))
    parts.append(tube("Bica", [(xf - 0.015, 0.0, zv), (xf - 0.05, 0.0, zv + 0.03),
                              (xf - 0.07, 0.0, zv + 0.15), (xf - 0.15, 0.0, zv + 0.20),
                              (xf - 0.215, 0.0, zv + 0.12), (xf - 0.22, 0.0, zv + 0.08)], 0.0105, n=16))
    parts.append(lathe("Arejador", [(0.0135, 0.0), (0.0135, -0.022), (0.0120, -0.025), (0.0, -0.0255)], 16,
                       center=(xf - 0.22, 0.0, zv + 0.088), axis="Z"))
    parts.append(lathe("Quebra-vacuo", [(0.016, 0.0), (0.016, 0.045), (0.021, 0.048), (0.021, 0.062),
                                        (0.017, 0.066), (0.0, 0.067)], 20,
                       center=((xf + X1) / 2, 0.20, 1.098), axis="Z", cap_bottom=False))
    return parts


def build_drains():
    parts = []
    # Tray outlet: flange standing proud of the sloped floor, domed perforated cover (pre-strainer).
    zf = top_height(DRAIN_X, 0.0)
    parts.append(lathe("Ralo da bandeja - anel", [(0.060, zf - 0.002), (0.058, zf + 0.0035), (0.054, zf + 0.0045),
                                                  (0.046, zf + 0.0045), (0.044, zf - 0.003)], 32,
                       center=(DRAIN_X, 0.0, 0.0), cap_top=False, cap_bottom=False))
    parts.append(lathe("Ralo da bandeja - tampa", [(0.044, zf - 0.002), (0.044, zf + 0.003), (0.036, zf + 0.0055),
                                                   (0.020, zf + 0.0070), (0.0, zf + 0.0076)], 32,
                       center=(DRAIN_X, 0.0, 0.0), cap_bottom=False))
    zs = top_height(1.40, 0.0)
    parts.append(lathe("Ralo da cuba - anel", [(0.046, zs - 0.002), (0.044, zs + 0.004), (0.040, zs + 0.005),
                                               (0.033, zs + 0.005), (0.031, zs - 0.003)], 32,
                       center=(1.40, 0.0, 0.0), cap_top=False, cap_bottom=False))
    parts.append(lathe("Ralo da cuba - cesto", [(0.031, zs - 0.012), (0.031, zs + 0.0015), (0.0, zs + 0.0017)], 32,
                       center=(1.40, 0.0, 0.0), cap_bottom=False))
    # Overflow slot on the foot-end wall of the basin.
    parts.append(rbox("Ladrao", X1 - 0.0295, 0.0, 0.855, 0.885, 0.0015, 0.045, 0.0014,
                      bev_t=0.0008, bev_b=0.0008, seg=1, nc=2))
    return parts


def build_pedestal():
    parts = []
    parts.append(rbox("Placa de base", 0.0, 0.0, 0.0, 0.014, 0.35, 0.25, 0.05, bev_t=0.004, bev_b=0.001, seg=3))
    parts.append(rbox("Colar", 0.0, 0.0, 0.012, 0.075, 0.20, 0.17, 0.07, bev_t=0.010, bev_b=0.002, seg=3))
    parts.append(rbox("Coluna", 0.0, 0.0, 0.07, 0.866, 0.16, 0.13, 0.05, bev_t=0.002, bev_b=0.002, seg=2))
    parts.append(rbox("Flange superior", 0.0, 0.0, 0.858, 0.8755, 0.23, 0.18, 0.04, bev_t=0.001, bev_b=0.004, seg=2))
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * 0.285, sy * 0.185
            parts.append(lathe("Arruela", [(0.019, 0.013), (0.019, 0.0165), (0.017, 0.0175), (0.0, 0.0175)], 20,
                               center=(x, y, 0.0), cap_bottom=False))
            parts.append(lathe("Porca", [(0.0135, 0.0175), (0.0135, 0.0275), (0.0115, 0.0295), (0.0, 0.0295)], 6,
                               center=(x, y, 0.0), cap_bottom=False, phase=math.radians(30 if sx * sy > 0 else 10)))
            parts.append(lathe("Chumbador", [(0.0075, 0.029), (0.0075, 0.035), (0.0055, 0.037), (0.0, 0.037)], 12,
                               center=(x, y, 0.0), cap_bottom=False))
    # Service panels on both long faces of the column, held by four screws.
    for sy in (-1, 1):
        y = sy * 0.13
        p = rbox("Tampa de inspecao", 0.0, 0.0, 0.25, 0.62, 0.0015, 0.08, 0.0014, bev_t=0.0012, bev_b=0.0012, seg=1, nc=2)
        p.rotation_euler.z = math.radians(90)
        p.location = (0.0, y + sy * 0.0012, 0.0)
        parts.append(p)
        for x in (-0.062, 0.062):
            for z in (0.27, 0.60):
                s = lathe("Parafuso", [(0.0055, 0.0), (0.0055, 0.0012), (0.0035, 0.0028), (0.0, 0.0031)], 12,
                          center=(0, 0, 0), cap_bottom=False)
                s.rotation_euler.x = math.radians(-90 * sy)
                s.location = (x, y + sy * 0.0024, z)
                parts.append(s)
    # Identification plate on the head-end face of the column.
    plate = rbox("Plaqueta", 0.0, 0.0, 0.60, 0.66, 0.0008, 0.045, 0.0007, bev_t=0.0005, bev_b=0.0005, seg=1, nc=2)
    plate.location.x = -0.1608
    parts.append(plate)
    for y in (-0.038, 0.038):
        for z in (0.607, 0.653):
            rv = lathe("Rebite", [(0.0028, 0.0), (0.0024, 0.0008), (0.0, 0.0012)], 8, center=(0, 0, 0), cap_bottom=False)
            rv.rotation_euler.y = math.radians(-90)
            rv.location = (-0.1616, y, z)
            parts.append(rv)
    return parts


def build_plumbing():
    parts = []
    # Tray outlet pipe runs under the tray and into the basin housing.
    parts.append(tube("Tubo de dreno da bandeja", [(DRAIN_X, 0.0, 0.874), (DRAIN_X, 0.0, 0.80),
                                                   (DRAIN_X + 0.06, 0.0, 0.775), (1.21, 0.0, 0.772)], 0.019, n=16, cap=False))
    # Basin waste down to the floor drain, with a union nut and a floor escutcheon.
    parts.append(tube("Tubo de esgoto", [(1.40, 0.0, 0.701), (1.40, 0.0, 0.0)], 0.024, n=16, cap=False))
    parts.append(lathe("Uniao", [(0.0245, -0.004), (0.031, 0.0), (0.031, 0.04), (0.027, 0.045), (0.0245, 0.047)], 6,
                       center=(1.40, 0.0, 0.52), cap_top=False, cap_bottom=False))
    parts.append(lathe("Canopla do piso", [(0.06, 0.0), (0.06, 0.003), (0.045, 0.012), (0.028, 0.016), (0.0245, 0.016)], 32,
                       center=(1.40, 0.0, 0.0), cap_top=False, cap_bottom=False))
    # Supply lines from the floor up into the panel (hot and cold).
    for y in (-0.15, 0.15):
        parts.append(tube("Alimentacao", [(X1 - 0.035, y, 0.0), (X1 - 0.035, y, 0.703)], 0.0095, n=12, cap=False))
        parts.append(lathe("Canopla", [(0.026, 0.0), (0.026, 0.003), (0.018, 0.010), (0.0095, 0.012)], 20,
                           center=(X1 - 0.035, y, 0.0), cap_top=False, cap_bottom=False))
    return parts


def build_headrest():
    # Moulded rubber head block with a cradle, resting on the tray at the head end.
    L, W = 0.15, 0.22
    x0 = X0 + 0.08
    zf = tray_floor(x0 + L / 2, 0.06)
    prof = [(0.010, 0.0), (0.004, 0.0015), (0.0, 0.008), (0.0, 0.055), (0.004, 0.071), (0.012, 0.078)]
    ob = rrect_shell("Apoio de cabeca", x0 + L / 2, 0.0, L / 2, W / 2, 0.03,
                     [(d, (lambda zz: (lambda x, y: zf + zz))(z)) for d, z in reversed(prof)], 4, 6, 5,
                     top_fill="grid", bottom_fill="ngon")
    # sculpt the cradle into the top grid
    me = ob.data
    for v in me.vertices:
        if v.co.z > zf + 0.077:
            u = (v.co.x - x0) / L
            vv = v.co.y / (W / 2 - 0.012)
            vv = max(-1.0, min(1.0, vv))
            v.co.z = zf + 0.078 - 0.028 * (1 - vv * vv) * (0.75 + 0.25 * math.sin(math.pi * max(0, min(1, u))))
    return ob


def build_all():
    if COLL in bpy.data.collections:
        c = bpy.data.collections[COLL]
        for o in list(c.objects):
            bpy.data.objects.remove(o)
    else:
        c = bpy.data.collections.new(COLL)
        bpy.context.scene.collection.children.link(c)
    steel = [build_top()] + build_under() + build_panel() + build_drains() + build_pedestal() + build_plumbing()
    rubber = [build_headrest()]
    return steel, rubber


# ---------------------------------------------------------------- assembly

PERFORATED = ("Ralo da bandeja - tampa", "Ralo da cuba - cesto")
SLOTTED = ("Ladrao",)


def assemble(mats, name="MesaAutopsia"):
    """Joins the parts into one game mesh (steel + rubber slots), smooth-by-angle normals, triangulated."""
    parts = list(bpy.data.collections[COLL].objects)
    for o in parts:
        o.data.materials.clear()
        key = "rubber" if o.name.startswith("Apoio de cabeca") else \
              "perf" if o.name.split(".")[0] in PERFORATED else \
              "slot" if o.name.split(".")[0] in SLOTTED else "steel"
        o.data.materials.append(mats[key])
    old = bpy.data.objects.get(name)
    if old:
        bpy.data.objects.remove(old)
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    root = next(o for o in parts if o.name == "Tampo")
    bpy.context.view_layer.objects.active = root
    bpy.ops.object.join()
    root.name = name
    root.data.name = name
    me = root.data
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    # Triangulated here so the baked tangent-space normals match the triangles Unity imports.
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.to_mesh(me); bm.free()
    me.shade_smooth()
    me.set_sharp_from_angle(angle=math.radians(48))
    return root


def unwrap_parts(parts):
    """Angle-based unwrap of every part along the seams the generators marked."""
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.unwrap(method="ANGLE_BASED", fill_holes=True, correct_aspect=True, margin=0.0)
    bpy.ops.object.mode_set(mode="OBJECT")


def uv_islands(me):
    """Face-index lists of the UV islands (faces sharing an edge with identical UVs)."""
    uv = me.uv_layers.active.data
    parent = list(range(len(me.polygons)))
    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    edge_faces = {}
    for f in me.polygons:
        for li in f.loop_indices:
            l = me.loops[li]
            e = me.edges[l.edge_index]
            edge_faces.setdefault(l.edge_index, []).append((f.index, li))
    def loop_uv_at(fi, vert):
        for li in me.polygons[fi].loop_indices:
            if me.loops[li].vertex_index == vert:
                return tuple(round(c, 6) for c in uv[li].uv)
    for ei, lst in edge_faces.items():
        if len(lst) != 2:
            continue
        (f1, _), (f2, _) = lst
        v1, v2 = me.edges[ei].vertices
        if loop_uv_at(f1, v1) == loop_uv_at(f2, v1) and loop_uv_at(f1, v2) == loop_uv_at(f2, v2):
            parent[find(f1)] = find(f2)
    groups = {}
    for f in me.polygons:
        groups.setdefault(find(f.index), []).append(f.index)
    return list(groups.values())


def layout_uvs(ob, detail_slots=("Aco inox - ralo", "Aco inox - ladrao"), detail_scale=3.0, hidden_scale=0.25,
               margin_px=8, size=2048):
    """Packs one non-overlapping atlas (UV0) for the baked maps: uniform texel density, perforated
    covers and the overflow slot denser (so the holes survive the bake), faces that only look down
    (never seen from the player's eye height) sparser."""
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.average_islands_scale()
    bpy.ops.object.mode_set(mode="OBJECT")
    me = ob.data
    uv = me.uv_layers.active.data
    detail = {i for i, m in enumerate(me.materials) if m and m.name in detail_slots}
    for faces in uv_islands(me):
        # Turn the island so U follows the table's length where it can (the tiled brushing runs along U).
        along_u = along_v = 0.0
        for f in faces:
            li = list(me.polygons[f].loop_indices)
            P = [me.vertices[me.loops[i].vertex_index].co for i in li]
            T = [uv[i].uv for i in li]
            e1, e2 = P[1] - P[0], P[2] - P[0]
            d1, d2 = T[1] - T[0], T[2] - T[0]
            det = d1.x * d2.y - d2.x * d1.y
            if abs(det) < 1e-12:
                continue
            du = (e1 * d2.y - e2 * d1.y) / det
            dv = (e2 * d1.x - e1 * d2.x) / det
            w = me.polygons[f].area
            along_u += w * abs(du.normalized().x)
            along_v += w * abs(dv.normalized().x)
        if along_v > 1.2 * along_u:
            for f in faces:
                for li in me.polygons[f].loop_indices:
                    u_, v_ = uv[li].uv
                    uv[li].uv = (v_, -u_)
        area = sum(me.polygons[f].area for f in faces)
        down = sum(me.polygons[f].area for f in faces if me.polygons[f].normal.z < -0.5)
        k = detail_scale if me.polygons[faces[0]].material_index in detail else \
            hidden_scale if down > 0.75 * area else 1.0
        if k != 1.0:
            for f in faces:
                for li in me.polygons[f].loop_indices:
                    uv[li].uv *= k
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=False, scale=True, margin_method="FRACTION",
                            margin=margin_px / size, shape_method="CONCAVE")
    bpy.ops.object.mode_set(mode="OBJECT")


def texel_density(ob, size=2048):
    """Pixels per metre over the steel and rubber surfaces (3D area vs UV area)."""
    me = ob.data
    uv = me.uv_layers.active.data
    a3 = a2 = 0.0
    for f in me.polygons:
        if len(f.vertices) != 3:
            continue
        p = [uv[li].uv for li in f.loop_indices]
        a2 += abs((p[1] - p[0]).cross(p[2] - p[0])) / 2
        a3 += f.area
    return size * math.sqrt(a2 / a3), a2


# ---------------------------------------------------------------- export

def export_fbx(ob, path, keep=("Aco inox - mesa", "Borracha - apoio")):
    """Exports a copy whose bake-only slots (perforated cover, overflow) are folded into the steel slot.
    Metres, Y up / -Z forward with the axis conversion baked into the mesh, split normals, triangles."""
    src_name = ob.name
    ob.name = src_name + "_fonte"
    cp = ob.copy()
    cp.data = ob.data.copy()
    cp.name = cp.data.name = src_name
    bpy.context.scene.collection.objects.link(cp)
    me = cp.data
    # The FBX axis conversion turns Blender +X into Unity -X; a half turn keeps the basin on Unity's +X.
    me.transform(Matrix.Rotation(math.pi, 4, "Z"))
    names = [m.name for m in me.materials]
    kept = [me.materials[names.index(k)] for k in keep]
    idx = [f.material_index for f in me.polygons]
    me.materials.clear()
    for m in kept:
        me.materials.append(m)
    for f, i in zip(me.polygons, idx):
        f.material_index = keep.index(names[i]) if names[i] in keep else 0
    bpy.ops.object.select_all(action="DESELECT")
    cp.select_set(True)
    bpy.context.view_layer.objects.active = cp
    try:
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"},
                                 apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                                 axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                                 mesh_smooth_type="OFF", use_mesh_modifiers=True, use_triangles=True,
                                 use_tspace=False, add_leaf_bones=False, bake_anim=False,
                                 path_mode="STRIP", embed_textures=False, colors_type="NONE")
    finally:
        bpy.data.objects.remove(cp)
        bpy.data.meshes.remove(me)
        ob.name = src_name
    return path

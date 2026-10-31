"""White lab coat on its hanger, finished on top of the cloth-simulated drape (../malhas/jaleco_caimento.json, made by jaleco_sim.py).
The drape is thickened into a thin shell; the pieces sewn onto it (lapels, collar, button stand, pockets) are laid on the settled
surface by ray casts, so they follow its folds. No topstitching geometry: a 0.5 mm thread is below one texel at 630 px/m.
Loaded by gerar.py (R, F, PR injected).
Frame: origin on the wall hook's arm, garment hangs towards -Z, front is -Y, +x is the wearer's left."""
import json, math, os
import bmesh, bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

smooth, lerp = F.smooth, F.lerp
DRAPE = os.path.join(os.path.dirname(__file__), '../malhas/jaleco_caimento.json')
V_BOTTOM = -0.335
T = 0.0024                      # cotton twill with its lining seams: a discreet thickness


def load_drape(path=DRAPE):
    d = json.load(open(path))
    bm = bmesh.new()
    vs = [bm.verts.new(c) for c in d['v']]
    for f in d['f']:
        bm.faces.new([vs[i] for i in f])
    bm.verts.index_update()
    bm.faces.ensure_lookup_table()
    sleeves = set(d['manga'])
    return bm, [i for i in range(len(bm.faces)) if i not in sleeves]


class Surface:
    """Ray casts onto the drape's body (sleeves excluded): front(x, z) / back(x, z) -> (point, outward normal)."""
    def __init__(self, bm, body):
        verts = [v.co.copy() for v in bm.verts]
        polys = [[v.index for v in bm.faces[i].verts] for i in body]
        self.tree = BVHTree.FromPolygons(verts, polys)

    def hit(self, x, z, side):
        o = Vector((x, -0.4 * side, z))
        loc, nrm, _, _ = self.tree.ray_cast(o, Vector((0, side, 0)), 1.0)
        if loc is None:
            return None, None
        if nrm.y * side > 0:
            nrm = -nrm
        return loc, nrm

    def on(self, x, z, lift, side=1):
        """Point lift metres above the outer surface of the front (side 1) or the back (side -1)."""
        p, n = self.hit(x, z, side)
        k = 0
        while p is None and k < 30:    # just past the hem or a side fold: take the nearest cloth above / inwards
            k += 1
            p, n = self.hit(x * (1 - 0.01 * k), z + 0.004 * k, side)
        return p + n * lift, n

    def hem(self, x, side=1):
        """Lowest point of the panel at x (the hem is not level: the drape lifts the corners)."""
        z = -1.2
        while self.hit(x, z, side)[0] is None and z < -0.5:
            z += 0.002
        return z


def cloth_shell(name, bm, t, inside):
    """Thin skin of the drape: the full thickness shows only at the cut edges (hem, cuffs, neck); away from them the inner layer
    stays close, so the tight side folds of the flattened sleeves do not turn the inner layer inside out."""
    from mathutils import kdtree
    edge = [v.co.copy() for v in bm.verts if v.is_boundary]
    kd = kdtree.KDTree(len(edge))
    for i, c in enumerate(edge):
        kd.insert(c, i)
    kd.balance()
    F.orient(bm, inside)
    bm.normal_update()
    inner = {}
    for v in list(bm.verts):
        d = kd.find(v.co)[2]
        inner[v] = bm.verts.new(v.co - v.normal * t * lerp(1.0, 0.18, smooth(0.008, 0.030, d)))
    for f in list(bm.faces):
        bm.faces.new([inner[v] for v in reversed(f.verts)])
    for e in [e for e in bm.edges if e.is_boundary and e.verts[0] in inner]:
        lp = next(l for l in e.link_faces[0].loops if l.edge == e)
        a, b = lp.vert, lp.link_loop_next.vert
        bm.faces.new((a, inner[a], inner[b], b))
    return R._finish(name, bm, 'tecido')


def patch(name, mat, S, x0, x1, z0, z1, lift, belly=0.0, top_lift=0.0, nu=12, nv=12, side=1, outline=None, feather=(0.06, 0.06, 0.06, 0.06),
          base=0.0005):
    """Piece of cloth sewn on the panel: its outline in (x, z) is mapped onto the surface; belly lifts the middle,
    top_lift opens the top edge (a pocket mouth). outline(u, v) -> (x, z) overrides the rectangle.
    No side walls: towards each edge the cloth comes down onto the panel, as a sewn or folded edge does (2 mm walls only made
    one-pixel UV islands that baked as dashed dark lines). feather: width of that fall in (u, v) units for the edges
    u=0, u=1, v=0, v=1; 0 keeps that edge raised (a pocket mouth, a roll line). base: height left at the fallen edges; the grid
    must be fine enough to follow the folds under it, or it dips into them."""
    def fall(d, w):
        return 1.0 if w <= 0 else smooth(0.0, w, d) ** 0.6

    def P(i, j):
        u, v = i / nu, j / nv
        if outline:
            x, z = outline(u, v)
        else:
            x, z = lerp(x0, x1, u), lerp(z0, z1, v)
        b = belly * math.sin(math.pi * u) * math.sin(math.pi * min(v * 1.15, 1.0)) + top_lift * smooth(0.75, 1.0, v) * math.sin(math.pi * u) ** 0.5
        k = fall(u, feather[0]) * fall(1 - u, feather[1]) * fall(v, feather[2]) * fall(1 - v, feather[3])
        p, _ = S.on(x, z, base + (lift + b - base) * k, side)
        return p
    bm = F.surface(P, nu, nv)
    c = outline(0.5, 0.5) if outline else ((x0 + x1) / 2, (z0 + z1) / 2)
    _, n = S.on(c[0], c[1], 0.0, side)
    bm.normal_update()
    if sum((f.normal for f in bm.faces), Vector()).dot(n) < 0:     # facing out along the panel's own normal
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    ob["mat"] = mat
    R._coll(R._current["asset"]).objects.link(ob)
    return ob


def v_edge(bm):
    """The cut edge of the front V on each side, as (z -> x) tables read from the drape's boundary."""
    pts = {-1: [], 1: []}
    for v in bm.verts:
        if v.is_boundary and v.co.y < 0.0 and V_BOTTOM - 0.02 < v.co.z < -0.13:
            pts[1 if v.co.x > 0 else -1].append((v.co.z, v.co.x))
    tables = {}
    for s, p in pts.items():
        p.sort()
        # bin by height, keep the innermost x (the very edge)
        bins = {}
        for z, x in p:
            k = round(z / 0.01)
            bins[k] = min(bins.get(k, 9), abs(x))
        tables[s] = sorted((k * 0.01, x) for k, x in bins.items())
    return tables


def edge_x(table, z):
    if z <= table[0][0]:
        return table[0][1]
    for (z0, x0), (z1, x1) in zip(table, table[1:]):
        if z <= z1:
            return lerp(x0, x1, (z - z0) / (z1 - z0))
    return table[-1][1]


def lapel(s, S, tab):
    """Notched lapel: the front edge above the top button, turned back over the panel (its roll line is the V edge)."""
    z_lo, z_hi = V_BOTTOM + 0.004, -0.170

    def outline(u, v):
        z = lerp(z_lo, z_hi, u)
        w = 0.080 * smooth(0.0, 0.85, u) ** 0.75
        w *= 1.0 - 0.38 * smooth(0.86, 1.0, u)          # the notch: the upper edge slants back to the collar
        x = s * (edge_x(tab[s], z) - 0.004 + v * w)
        return x, z + v * w * 0.32                      # lapels point a little upwards
    return patch(f'Lapela {s:+d}', 'tecido', S, 0, 0, 0, 0, 0.0030, belly=0.0012, nu=18, nv=8, outline=outline, feather=(0.0, 0.05, 0.0, 0.12))


def collar(bm, S, tab):
    """Shirt-type collar: stand round the neck, folded down over the shoulders; its front points meet the lapel notches."""
    top = [v.co.copy() for v in bm.verts if v.is_boundary and v.co.z > -0.16]
    cy = sum(p.y for p in top) / len(top)
    # neck line: for each angle round the neck centre, the boundary point in that direction
    def neck(a):
        d = Vector((math.cos(a), math.sin(a)))
        best = max(top, key=lambda p: Vector((p.x, p.y - cy)).normalized().dot(d))
        return best
    nu, prof = 30, [(0.0, -0.004), (0.0, 0.020), (0.004, 0.030), (0.012, 0.031), (0.020, 0.016), (0.032, -0.010), (0.040, -0.030)]
    rings = []
    a0, a1 = math.radians(-32), math.radians(212)          # angle from +x: from the left front round the back (+y) to the right front
    for i in range(nu + 1):
        a = lerp(a0, a1, i / nu)
        p = neck(a)
        out = Vector((p.x, p.y - cy, 0.0)).normalized()
        q = abs(i / nu - 0.5) * 2                            # 1 at the front points
        ring = []
        for k, (o, h) in enumerate(prof):
            fall = 1.0 + 0.6 * q * smooth(3, 6, k)           # the points are longer than the back
            ring.append(p + out * (o * fall + 0.002) + Vector((0, 0, h * (1.0 if h > 0 else fall))))
        rings.append(ring)
    bm2 = bmesh.new()
    V = [[bm2.verts.new(c) for c in r] for r in rings]
    for r0, r1 in zip(V, V[1:]):
        for k in range(len(prof) - 1):
            bm2.faces.new((r0[k], r1[k], r1[k + 1], r0[k + 1]))
    return F.shell('Gola', 'tecido', bm2, 0.0022, inside=(0.0, cy, -0.12))


def button(name, S, x, z):
    p, n = S.on(x, z, 0.0050)
    ob = R.cyl(name, 'botao', (0, 0, 0), 0.0072, 0.0024, axis=(0, 0, 1), seg=16, r=0.0007)
    m = Matrix.Translation(p) @ n.to_track_quat('Z', 'Y').to_matrix().to_4x4()
    ob.data.transform(m)


def jaleco():
    bm, body = load_drape()
    S = Surface(bm, body)
    tab = v_edge(bm)
    zs = [v.co.z for v in bm.verts]
    hem = min(zs)
    PR.cabide('plastico')
    cloth_shell('Corpo e mangas', bm.copy(), T, inside=(0.0, 0.004, -0.5))
    for s in (-1, 1):
        lapel(s, S, tab)
    collar(bm, S, tab)
    # button stand: the wearer's right front laps over the left from the top button down; its free edge is a visible step
    z_top = V_BOTTOM + 0.010
    hv = S.hem(-0.008) + 0.004
    patch('Vista', 'tecido', S, -0.016, 0.024, hv, z_top, 0.0026, nu=8, nv=40, feather=(0.10, 0.25, 0.01, 0.0))
    for i, zb in enumerate((-0.37, -0.52, -0.67, -0.82)):
        button(f'Botao {i}', S, 0.004, zb)
    # patch pockets: two on the hips, one on the left breast with a pen
    for name, x0, x1, z0, z1 in (('Bolso direito', -0.215, -0.075, -0.720, -0.548), ('Bolso esquerdo', 0.075, 0.215, -0.720, -0.548)):
        patch(name, 'tecido', S, x0, x1, z0, z1, 0.0024, belly=0.0030, top_lift=0.0030, nu=16, nv=16, feather=(0.05, 0.05, 0.04, 0.0))
    bx0, bx1, bz0, bz1 = 0.098, 0.192, -0.330, -0.238
    patch('Bolso do peito', 'tecido', S, bx0, bx1, bz0, bz1, 0.0024, belly=0.0016, top_lift=0.0020, nu=12, nv=12, feather=(0.06, 0.06, 0.06, 0.0))
    p, n = S.on(0.128, -0.250, 0.0042)
    R.cyl('Caneta', 'plastico', (p.x, p.y - 0.001, -0.272), 0.0048, 0.105, axis=(0, 0, 1), seg=12, r=0.0012)
    R.cyl('Tampa da caneta', 'tampa', (p.x, p.y - 0.001, -0.205), 0.0052, 0.034, axis=(0, 0, 1), seg=12, r=0.0012)
    R.box('Clipe da caneta', 'aco', p.x - 0.0018, p.y - 0.0072, -0.236, p.x + 0.0018, p.y - 0.0056, -0.196, r=0.0004, seg=1)
    bm.free()
    F.shift_asset('Jaleco', PR.HANGER_DX)


DRAPE_V2 = os.path.join(os.path.dirname(__file__), '../malhas/jaleco_caimento_v2.json')


def jaleco_v2():
    """Pilot v2: the drape from flat pattern pieces (jaleco_molde.py) already carries the lapels, the collar and the button
    overlap; here it gets its thickness, thin patch pockets (open mouth, edges down on the panel), the buttons and the pen."""
    bm, body = load_drape(DRAPE_V2)
    S = Surface(bm, body)
    PR.cabide('plastico')
    cloth_shell('Corpo e mangas', bm.copy(), T, inside=(0.0, 0.004, -0.5))
    for i, zb in enumerate((-0.57, -0.72, -0.87)):
        button(f'Botao {i}', S, 0.0, zb)
    button('Botao de cima', S, 0.006, -0.428)                       # left undone: on the right front, under the open lapel
    for name, x0, x1 in (('Bolso direito', -0.195, -0.060), ('Bolso esquerdo', 0.060, 0.195)):
        patch(name, 'tecido', S, x0, x1, -0.720, -0.548, 0.0011, belly=0.0010, top_lift=0.0012, nu=30, nv=36, feather=(0.12, 0.12, 0.10, 0.0),
              base=0.0008)
    bx0, bx1, bz0, bz1 = 0.098, 0.192, -0.330, -0.238
    patch('Bolso do peito', 'tecido', S, bx0, bx1, bz0, bz1, 0.0011, belly=0.0008, top_lift=0.0010, nu=22, nv=22, feather=(0.12, 0.12, 0.12, 0.0),
          base=0.0008)
    p, n = S.on(0.128, -0.250, 0.0042)
    R.cyl('Caneta', 'plastico', (p.x, p.y - 0.001, -0.272), 0.0048, 0.105, axis=(0, 0, 1), seg=12, r=0.0012)
    R.cyl('Tampa da caneta', 'tampa', (p.x, p.y - 0.001, -0.205), 0.0052, 0.034, axis=(0, 0, 1), seg=12, r=0.0012)
    R.box('Clipe da caneta', 'aco', p.x - 0.0018, p.y - 0.0072, -0.236, p.x + 0.0018, p.y - 0.0056, -0.196, r=0.0004, seg=1)
    bm.free()
    F.shift_asset('JalecoV2', PR.HANGER_DX)

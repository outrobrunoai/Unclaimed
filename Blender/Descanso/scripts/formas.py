"""Shape helpers of the rest-room kit: parametric surfaces, shells with thickness, lofts, booleans.
Loaded by gerar.py with R (the reception modelling module) injected."""
import bpy, bmesh, math
from mathutils import Vector, Matrix


def smooth(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def interp(keys, x):
    """Piecewise-smooth interpolation through (x, value) keys."""
    if x <= keys[0][0]:
        return keys[0][1]
    for (x0, v0), (x1, v1) in zip(keys, keys[1:]):
        if x <= x1:
            t = (x - x0) / (x1 - x0)
            return lerp(v0, v1, t * t * (3 - 2 * t))
    return keys[-1][1]


def spow(v, e):
    return math.copysign(abs(v) ** e, v)


# ---------------------------------------------------------------- surfaces

def surface(P, nu, nv, wrap=False):
    """Quad grid over P(i, j) -> Vector, i in 0..nu, j in 0..nv. wrap: the last column is the first (closed ring)."""
    bm = bmesh.new()
    cols = nu if wrap else nu + 1
    V = [[bm.verts.new(P(i, j)) for j in range(nv + 1)] for i in range(cols)]
    for i in range(nu if not wrap else nu):
        i2 = (i + 1) % cols
        for j in range(nv):
            bm.faces.new((V[i][j], V[i2][j], V[i2][j + 1], V[i][j + 1]))
    return bm


def orient(bm, inside):
    """Flips the whole surface when most normals point towards the point `inside`."""
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    inside = Vector(inside)
    score = sum(1 if f.normal.dot(f.calc_center_median() - inside) > 0 else -1 for f in bm.faces)
    if score < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])


def shell(name, mat, bm, t, inside=None, bevel=0.0, flip=False):
    """Thin cloth/plastic skin: the open surface thickened inwards by t metres (the given surface stays the outer one).
    inside: a point on the inner side (decides which way is out)."""
    if inside is not None:
        orient(bm, inside)
    if flip:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    bm.normal_update()
    inner = {v: bm.verts.new(v.co - v.normal * t) for v in list(bm.verts)}
    faces = list(bm.faces)
    for f in faces:
        bm.faces.new([inner[v] for v in reversed(f.verts)])
    for e in list(bm.edges):
        if len(e.link_faces) != 1:
            continue
        lp = next(l for l in e.link_faces[0].loops if l.edge == e)
        a, b = lp.vert, lp.link_loop_next.vert
        bm.faces.new((a, inner[a], inner[b], b))
    if bevel > 0:
        R._bevel(bm, bevel, 1, 35.0)
    return R._finish(name, bm, mat)


def rings_solid(rings, caps=True):
    """Closed solid from rings (lists of Vector with the same count) stacked in order; the end rings are capped."""
    bm = bmesh.new()
    V = [[bm.verts.new(p) for p in ring] for ring in rings]
    n = len(V[0])
    for a, b in zip(V, V[1:]):
        for i in range(n):
            bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
    if caps:
        bm.faces.new(V[0])
        bm.faces.new(list(reversed(V[-1])))
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-7)
    return bm


def as_object(name, bm, mat):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return R._finish(name, bm, mat)


def boolean(a, b, op='DIFFERENCE', keep=False):
    """a <- a (op) b, exact solver. b is deleted unless keep."""
    mod = a.modifiers.new('bool', 'BOOLEAN')
    mod.operation, mod.solver, mod.object = op, 'EXACT', b
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = a
    a.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bm = bmesh.new()
    bm.from_mesh(a.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=2e-6)
    bmesh.ops.dissolve_degenerate(bm, dist=2e-6, edges=bm.edges[:])
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_area() < 1e-10], context='FACES')
    bm.to_mesh(a.data)
    bm.free()
    if not keep:
        me = b.data
        bpy.data.objects.remove(b)
        bpy.data.meshes.remove(me)
    return a


def detach(ob):
    """Takes a part out of the asset collection (a boolean cutter / helper)."""
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def shift_asset(asset, dx=0.0, dy=0.0, dz=0.0):
    m = Matrix.Translation((dx, dy, dz))
    for o in R._coll(asset).objects:
        o.data.transform(m)


def bar(name, mat, p0, p1, w, h, r=0.002, up=(0, 0, 1)):
    """Straight flat bar between two points; w across `up`-perpendicular, h along it."""
    p0, p1 = Vector(p0), Vector(p1)
    d = (p1 - p0)
    L = d.length
    d.normalize()
    s = Vector(up).cross(d)
    if s.length < 1e-6:
        s = Vector((1, 0, 0))
    s.normalize()
    u = d.cross(s)
    pts = [p0 + d * sx + s * sy * w / 2 + u * sz * h / 2 for sx in (0, L) for sy in (-1, 1) for sz in (-1, 1)]
    return R.hull(name, mat, pts, r=r)


def wire(name, mat, pts, radius, fillet=0.0):
    return R.tube(name, mat, pts, radius, fillet=fillet, res=2)


def rrect_map(s, t, hw, hh, rx, ry):
    """Maps a square-grid point (s, t in [-1, 1]) to a rectangle hw x hh with the corners rounded by rx x ry
    (radial rescale along each ray: grid lines stay smooth)."""
    m = max(abs(s), abs(t))
    if m < 1e-9:
        return 0.0, 0.0
    ex, ey = s / m, t / m                       # point on the square boundary
    px, py = ex * hw, ey * hh
    cx, cy = hw - rx, hh - ry
    ax, ay = abs(px) - cx, abs(py) - cy
    if ax > 0 and ay > 0:                        # inside the corner square: pull onto the arc along the ray
        # ray from the origin through (px, py); intersect with the ellipse centred at (cx, cy)
        dx, dy = abs(px), abs(py)
        a = (dx / rx) ** 2 + (dy / ry) ** 2
        b = -2 * (dx * cx / rx ** 2 + dy * cy / ry ** 2)
        c = (cx / rx) ** 2 + (cy / ry) ** 2 - 1
        disc = max(b * b - 4 * a * c, 0)
        k = (-b + math.sqrt(disc)) / (2 * a)
        px, py = math.copysign(dx * k, px), math.copysign(dy * k, py)
    return px * m, py * m


def ribbon(name, mat, path, width, thick, up_hint=(0, 1, 0), w_end=None, t_end=None, round_=0.35):
    """Webbing / padded strap swept along a polyline: rounded-rectangle section, perpendicular to the path."""
    rings = []
    n = len(path)
    for k, p in enumerate(path):
        p = Vector(p)
        t = (Vector(path[min(k + 1, n - 1)]) - Vector(path[max(k - 1, 0)])).normalized()
        u = Vector(up_hint)
        s = t.cross(u).normalized()
        u = s.cross(t).normalized()
        q = k / (n - 1)
        w = lerp(width, w_end if w_end is not None else width, q) / 2
        th = lerp(thick, t_end if t_end is not None else thick, q) / 2
        ring = []
        for a in range(12):
            ang = 2 * math.pi * a / 12
            cx, cz = math.cos(ang), math.sin(ang)
            ring.append(p + s * (spow(abs(cx), round_) * w) * (1 if cx >= 0 else -1) + u * (spow(abs(cz), round_) * th) * (1 if cz >= 0 else -1))
        rings.append(ring)
    bm = rings_solid(rings, caps=True)
    return as_object(name, bm, mat)



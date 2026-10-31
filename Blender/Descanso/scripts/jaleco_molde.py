"""Lab coat drape from flat pattern pieces (pilot v2): blender -b --factory-startup --python jaleco_molde.py -- out.blend [frames]
Writes ../malhas/jaleco_caimento_v2.json (jaleco_sim.py's single-volume drape, v1, is kept for comparison).
Fabric is flat: every piece is cut as a flat 2D outline (back; left and right fronts with the lapel; sleeves cut on with the
body as in a laid-flat coat) and triangulated evenly. The pieces are laid flat around the hanger, front and back layers 12 mm
apart, meeting at the seams (shoulders, side seams, sleeve seams), so the rest shape of the bending springs is flat cloth and
the seams are the only folds it remembers. The lapels are part of the fronts, already turned back along the roll line.
Only the shoulder seams resting on the hanger arms are pinned; gravity drops the sleeves (they fold at the armhole), the
hanger and the wall do the rest. Frame: origin on the wall hook's arm, -Z down, front is -Y, +x is the wearer's left."""
import bpy, bmesh, json, math, os, sys
from mathutils import Vector, Matrix, kdtree
from mathutils.geometry import delaunay_2d_cdt

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = argv[0] if argv else '/tmp/jaleco_molde.blend'
FRAMES = int(argv[1]) if len(argv) > 1 else 360
DRAPE = os.environ.get('JOUT') or os.path.join(os.path.dirname(os.path.abspath(__file__)), '../malhas/jaleco_caimento_v2.json')
env = lambda k, d: float(os.environ.get(k, d))

H = env('JH', 0.013)                # triangle edge
G = 0.012                           # half gap between the front and back layers as laid on the hanger
HEM = -1.020
NW = 0.072                          # half width of the neck at the shoulder seam
ZB = -0.420                         # break point: top button, where the lapels start to roll
XS, XA, XH = 0.228, 0.285, 0.320    # shoulder point, armpit, side seam at the hem (half widths; a little flared)
SLEEVE_ANG = math.radians(env('JANG', 50))   # from vertical, as the sleeve lies in a laid-flat coat
SLEEVE_LEN, W_TOP, W_CUFF = 0.60, 0.17, 0.135
ROLL_R = 0.006                      # radius of the lapel's roll
OV = 0.018                          # button overlap: each front runs this far past the centre line
BUTTONS = (-0.570, -0.720, -0.870)  # the top button (at the break) is left undone, as in the reference: the fronts fall open
LAP = 0.005                         # the left front (on top) lies this far in front of the right one where they overlap
WALL_Y = 0.074


def smooth(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def zh(x):
    """Top line of the hanger arms (pecas_roupas.cabide)."""
    return -0.0825 - 0.267 * min(abs(x), 0.206)


def top(x):
    """Shoulder seam: on the arm, a cloth thickness above it; past the arm's end it slopes down to the shoulder point."""
    ax = abs(x)
    z = zh(ax) + 0.0065
    if ax > 0.206:
        z -= 0.45 * (ax - 0.206)
    return z


V2 = lambda x, z: Vector((x, z))
N = V2(NW, top(NW))
S = V2(XS, top(XS))
D_SL = V2(math.sin(SLEEVE_ANG), -math.cos(SLEEVE_ANG))
N_SL = V2(-math.cos(SLEEVE_ANG), -math.sin(SLEEVE_ANG))       # across the sleeve, towards the underarm
CO = S + D_SL * SLEEVE_LEN
CI = CO + N_SL * W_CUFF
_p0 = S + N_SL * W_TOP
A = _p0 + D_SL * ((XA - _p0.x) / D_SL.x)
HP = V2(XH, HEM)
B = V2(-OV, ZB)


def line(a, b):
    return [a, b]


def shoulder_line():
    return [V2(lerp(N.x, S.x, k / 40), top(lerp(N.x, S.x, k / 40))) for k in range(41)]


# lapel: turned back along the roll line B -> N; its outer edge in the flat pattern lies on the centre side of that line
ROLL_U = (N - B).normalized()
ROLL_M = V2(-ROLL_U.y, ROLL_U.x)            # perpendicular, towards the centre front (-x for the left front)
if ROLL_M.x > 0:
    ROLL_M = -ROLL_M
ROLL_LEN = (N - B).length


def roll(t, w):
    return B + ROLL_U * (t * ROLL_LEN) + ROLL_M * w


def lapel_edge():
    """B -> lapel point -> notch."""
    pts = [roll(0.80 * k / 24, 0.078 * (k / 24) ** 1.1) for k in range(25)]       # straight-ish outer edge up to the point
    pts.append(roll(0.835, 0.050))
    pts.append(roll(0.86, 0.030))
    return pts


def gorge():
    """Notch -> N: the collar is sewn along here and round the back neck."""
    return [roll(lerp(0.86, 1.0, k / 6), lerp(0.030, 0.0, k / 6)) for k in range(7)]


# ---------------------------------------------------------------- pieces as tagged outline segments (left half, x > 0)

def left_body_segments():
    """From the hem at the side round the left sleeve to the shoulder at the neck, as (points, kind)."""
    return [(line(HP, A), 'seam'), (line(A, CI), 'seam'), (line(CI, CO), 'open'), (line(CO, S), 'seam'), (shoulder_line()[::-1], 'seam')]


def mirror(seg):
    return [V2(-p.x, p.y) for p in seg]


def front_left():
    segs = [(line(V2(-OV, HEM), HP), 'open')]
    segs += left_body_segments()                                     # HP .. N
    segs.append((gorge()[::-1], 'neck'))                             # N .. notch
    segs.append((lapel_edge()[::-1], 'open'))                        # notch .. B
    segs.append((line(B, V2(-OV, HEM)), 'centre'))                   # the fronts are held only by the buttons
    return segs


def back():
    segs = [(line(V2(-XH, HEM), HP), 'open')]
    segs += left_body_segments()
    neck = [V2(lerp(NW, -NW, k / 20), N.y + 0.004 * (1 - (lerp(NW, -NW, k / 20) / NW) ** 2)) for k in range(21)]
    segs.append((neck, 'neck'))
    for pts, kind in reversed(left_body_segments()):
        segs.append((mirror(pts)[::-1], kind))
    return segs


def resample(pts, h):
    L = [0.0]
    for a, b in zip(pts, pts[1:]):
        L.append(L[-1] + (b - a).length)
    n = max(1, math.ceil(L[-1] / h))
    out, j = [], 0
    for k in range(n):
        s = L[-1] * k / n
        while L[j + 1] < s:
            j += 1
        t = (s - L[j]) / max(L[j + 1] - L[j], 1e-12)
        out.append(pts[j].lerp(pts[j + 1], t))
    return out                                                       # the end point belongs to the next segment


def outline(segs):
    pts, kinds = [], []
    for p, k in segs:
        r = resample(p, H)
        pts += r
        kinds += [k] * len(r)
    return pts, kinds


def inside(p, poly):
    c, n = False, len(poly)
    for i in range(n):
        a, b = poly[i], poly[i - 1]
        if (a.y > p.y) != (b.y > p.y) and p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x:
            c = not c
    return c


def kd_of(pts):
    kd = kdtree.KDTree(len(pts))
    for i, p in enumerate(pts):
        kd.insert((p.x, p.y, 0.0), i)
    kd.balance()
    return kd


def dense(segs, kinds_wanted):
    out = []
    for p, k in segs:
        if k in kinds_wanted:
            out += resample(p, H / 6) + [p[-1]]
    return out


def triangulate(segs):
    poly, kinds = outline(segs)
    edge_kd = kd_of(dense(segs, ('seam', 'open', 'centre', 'neck')))
    xs, zs = [p.x for p in poly], [p.y for p in poly]
    pts = list(poly)
    dz = H * math.sqrt(3) / 2
    j = 0
    z = min(zs) + dz / 2
    while z < max(zs):
        x = min(xs) + (H / 2 if j % 2 else 0.0)
        while x < max(xs):
            p = V2(x, z)
            if edge_kd.find((x, z, 0.0))[2] > 0.62 * H and inside(p, poly):
                pts.append(p)
            x += H
        z += dz
        j += 1
    vs, _, faces, orig, _, _ = delaunay_2d_cdt(pts, [], [list(range(len(poly)))], 1, 1e-7)
    # the solver returns its own (rounded) coordinates: take the input point back, so both sides of a seam coincide exactly
    out = [pts[o[0]] if o else V2(v.x, v.y) for v, o in zip(vs, orig)]
    neck = [bool(o) and o[0] < len(poly) and kinds[o[0]] == 'neck' for o in orig]
    return out, faces, neck


# ---------------------------------------------------------------- laying the pieces on the hanger

SEAMS = [seg for seg, k in left_body_segments() if k == 'seam']
SH_KD = kd_of(dense([(shoulder_line(), 'seam')], ('seam',)))
SEAM_KD = kd_of(dense([(s, 'seam') for s in SEAMS[:-1]], ('seam',)))
ARMHOLE_KD = kd_of(resample([S, A], H / 6) + [A])
OPEN_KD = {name: kd_of(dense(fn(), ('open', 'centre', 'neck'))) for name, fn in (('front', front_left), ('back', back))}


def layer(ax, z):
    """0 at a seam, 1 away from it: the two layers meet along the seams (fast at the shoulder, over the hanger arm)."""
    # the seams are sampled every H/6: a point on a seam can be ~1 mm from the nearest sample; that must still count as on it
    d_sh = max(0.0, SH_KD.find((ax, z, 0.0))[2] - 0.0012)
    d_se = max(0.0, SEAM_KD.find((ax, z, 0.0))[2] - 0.0012)
    return min(math.sqrt(smooth(0.0, 0.010, d_sh)), smooth(0.0, 0.045, d_se))


def fold_lapel(p):
    """Lapel points (centre side of the roll line, above the break) turned back over the front: (2D point, extra -y)."""
    q = p - B
    t = q.dot(ROLL_U) / ROLL_LEN
    s = q.dot(ROLL_M)
    if s <= 0.0 or t <= 0.0:
        return p, 0.0
    foot = B + ROLL_U * (t * ROLL_LEN)
    r = ROLL_R
    if s < math.pi * r:
        a = s / r
        return foot + ROLL_M * (r * math.sin(a)), r * (1 - math.cos(a))
    return foot - ROLL_M * (s - math.pi * r), 2 * r


def place(piece, p):
    ax = abs(p.x)
    if piece == 'back':
        return Vector((p.x, G * layer(ax, p.y), p.y))
    f, lift = fold_lapel(p)                                         # fronts: local x, negative on the overlap
    w = layer(abs(f.x), f.y) if lift == 0.0 else layer(abs(p.x), p.y)
    y = -G * w - lift
    if piece == 'front_left':
        return Vector((f.x, y - LAP * (1 - smooth(0.02, 0.06, p.x)), f.y))
    return Vector((-f.x, y, f.y))


def is_sleeve(p):
    """Outer side of the armhole line S -> A."""
    ax = abs(p.x)
    return (A - S).cross(V2(ax, p.y) - S) > 0.0 and ax > 0.20


# ---------------------------------------------------------------- vertex groups (from the flat pattern)

def weights(piece, p):
    ax, z = abs(p.x), p.y
    pin = 0.0
    if NW + 0.004 < ax < 0.204 and z > top(ax) - 0.013:
        pin = 1.0 - 0.6 * smooth(0.192, 0.204, ax)                # held to the arm's end, where the shoulder rolls over it
    if piece == 'back' and ax <= NW + 0.004:
        # the back neck: the collar band's stiffness keeps it at the hanger's height (left free it slid 4 cm down behind the
        # hanger, baring the arms above the neckline); a soft hold, fading out 2 cm below the edge
        d = (N.y + 0.004 * (1 - (min(ax, NW) / NW) ** 2)) - z
        pin = max(pin, 0.7 * (1 - smooth(0.0, 0.020, d)))
    d_open = OPEN_KD['back' if piece == 'back' else 'front'].find((ax, z, 0.0))[2]
    bend = 0.20 + 0.80 * (1 - smooth(0.010, 0.032, d_open))        # hems and facings: double fabric
    bend *= smooth(0.006, 0.030, ARMHOLE_KD.find((ax, z, 0.0))[2])  # the armhole seam is a hinge
    if piece != 'back' and fold_lapel(p)[1] > 0.0:
        bend = 1.0                                                  # lapels are interfaced: they hold their roll
    return pin, bend


def neck_chain(bm, neck):
    """The neckline's boundary vertices in order, from the right front's notch round the back to the left one."""
    kd = kdtree.KDTree(len(neck))
    for i, c in enumerate(neck):
        kd.insert(c, i)
    kd.balance()
    cs = {v for v in bm.verts if kd.find(v.co)[2] < 2e-4}
    nb = {v: [e.other_vert(v) for e in v.link_edges if e.is_boundary and e.other_vert(v) in cs] for v in cs}
    start = min((v for v in cs if len(nb[v]) == 1), key=lambda v: v.co.x)
    chain, prev = [start], None
    while True:
        nxt = [w for w in nb[chain[-1]] if w is not prev]
        if not nxt or nxt[0] in chain:
            break
        prev = chain[-1]
        chain.append(nxt[0])
    return chain


def clear_hanger(p, base):
    """Collar points that would cut through the hanger arms or its neck: over the arm where the column starts on top of it
    (the shoulder seam's end), otherwise to the side of the arm the column belongs to."""
    p = p.copy()
    if abs(p.x) < 0.214 and -0.162 < p.z < zh(p.x) + 0.0045:
        half = 0.0048 if abs(p.x) < 0.016 else 0.0042
        if abs(p.y) < half + 0.0045:
            if base.z > zh(base.x) - 0.002 and abs(base.y) < half + 0.0045:
                p.z = zh(p.x) + 0.0045
            else:
                p.y = (1.0 if base.y > 0.0 else -1.0) * (half + 0.0045)
    return p


def collar(bm, chain):
    """Shirt collar sewn to the settled neckline: a stand rising from the seam, rolled over, and a fall laid down over the coat
    as it hangs (each step down is kept a cloth thickness off the settled surface), longer towards the points.
    It is not simulated: dropped by the solver the band ruffled and twisted at its points."""
    from mathutils.bvhtree import BVHTree
    tree = BVHTree.FromBMesh(bm)
    L = [0.0]
    for a, b in zip(chain, chain[1:]):
        L.append(L[-1] + (b.co - a.co).length)
    up, r, K, step = Vector((0, 0, 1)), 0.004, 14, 0.0085
    cx = sum((v.co for v in chain), Vector()) / len(chain)
    cols = []
    for v, l in zip(chain, L):
        q = abs(l / L[-1] - 0.5) * 2                                   # 1 at the collar points
        # on the gorge (q > ~0.7) the collar runs down the lapel's steep top: kept short there, or its fall twists into a strip
        g = smooth(0.62, 0.95, q)
        hs, n_fall = lerp(0.030, 0.014, g), round(lerp(6, 3, g))
        o = Vector((v.co.x - cx.x, v.co.y - cx.y, 0.0)).normalized()
        col = [v.co + up * (hs * k / 4) + o * (0.12 * hs * k / 4) for k in range(1, 5)]
        tp = col[-1]
        col += [tp + up * (r * math.sin(a)) + o * (r * (1 - math.cos(a))) for a in (math.pi / 3, 2 * math.pi / 3, math.pi)]
        p = col[-1]
        for k in range(n_fall):
            p = p - up * step + o * (0.25 * step)
            loc, nrm, _, _ = tree.find_nearest(p)
            if nrm.dot(o) < 0:
                nrm = -nrm
            if (p - loc).dot(nrm) < 0.0035:
                p = loc + nrm * 0.0035
            col.append(p)
        cols.append([clear_hanger(c, v.co) for c in col])
    n = max(len(c) for c in cols)
    for i, c in enumerate(cols):                                       # every column to the same row count: a short one's
        m = len(c)                                                     # fall is resampled to more, closer rows
        if m < n:
            fall = c[6:]
            tt = [k * (len(fall) - 1) / (n - 7) for k in range(n - 6)]
            cols[i] = c[:6] + [fall[int(t)].lerp(fall[min(int(t) + 1, len(fall) - 1)], t - int(t)) for t in tt]
    for _ in range(3):                                                 # even out the steps between neighbouring columns
        cols = [cols[0]] + [[(a + 2 * b + c) / 4 if k > 0 else b for k, (a, b, c) in enumerate(zip(*t))]
                            for t in zip(cols, cols[1:], cols[2:])] + [cols[-1]]
    rows = [chain] + [[bm.verts.new(cols[i][k]) for i in range(len(chain))] for k in range(n)]
    for r0, r1 in zip(rows, rows[1:]):
        for i in range(len(chain) - 1):
            bm.faces.new((r0[i], r0[i + 1], r1[i + 1], r1[i]))


def build():
    bm = bmesh.new()
    dl = bm.verts.layers.deform.verify()
    sl = bm.faces.layers.int.new('manga')
    pl = bm.verts.layers.int.new('peca')
    neck = []
    for piece, segs in (('back', back()), ('front_left', front_left()), ('front_right', front_left())):
        pts, faces, on_neck = triangulate(segs)
        vs = []
        for p, nk in zip(pts, on_neck):
            v = bm.verts.new(place(piece, p))
            if nk:
                neck.append(v.co.copy())
            pin, bend = weights(piece, p)
            v[dl][0] = pin
            v[dl][1] = bend
            v[pl] = ('back', 'front_left', 'front_right').index(piece)
            vs.append(v)
        for f in faces:
            ff = bm.faces.new([vs[i] for i in f] if piece != 'front_right' else [vs[i] for i in reversed(f)])
            c = sum((pts[i] for i in f), V2(0, 0)) / len(f)
            ff[sl] = 1 if is_sleeve(c) else 0
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-4)
    for z in BUTTONS:                                               # buttoned: one point of each front joined
        pair = [min((v for v in bm.verts if v[pl] == k and abs(v.co.x) < OV + 0.004), key=lambda v: abs(v.co.x) + abs(v.co.z - z))
                for k in (1, 2)]
        bmesh.ops.pointmerge(bm, verts=pair, merge_co=(pair[0].co + pair[1].co) / 2)
    bm.verts.index_update()
    chain = [v.index for v in neck_chain(bm, neck)]
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new('Jaleco molde')
    bm.to_mesh(me)
    sleeves = [f.index for f in bm.faces if f[sl]]
    bm.free()
    ob = bpy.data.objects.new('Jaleco molde', me)
    bpy.context.scene.collection.objects.link(ob)
    ob.vertex_groups.new(name='pin')
    ob.vertex_groups.new(name='bainha')
    for p in me.polygons:
        p.use_smooth = True
    return ob, sleeves, chain


def add_collar(ob, chain):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.verts.ensure_lookup_table()
    collar(bm, [bm.verts[i] for i in chain])
    bm.to_mesh(ob.data)
    bm.free()
    for p in ob.data.polygons:
        p.use_smooth = True


# ---------------------------------------------------------------- colliders and solver

def mesh_object(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def colliders():
    out = []
    bm = bmesh.new()
    for s in (-1, 1):            # hanger arms with their bottom bar, as one solid each, ending in a round 12 mm end that the
        # shoulder rolls over (a square end left the coat with square, padded-looking shoulders)
        pts = [(0.0, -0.0825), (0.0, -0.1575)] + [(s * (0.200 + 0.0118 * math.cos(a)), -0.1475 + 0.0118 * math.sin(a))
                                                   for a in (math.radians(d) for d in range(-90, 91, 15))]
        vs = [bm.verts.new((x, y, z)) for (x, z) in pts for y in (-0.0042, 0.0042)]
        bmesh.ops.convex_hull(bm, input=vs)
    out.append(mesh_object('_cabide', bm))
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=1.2)
    bmesh.ops.transform(bm, matrix=Matrix.Translation((0, WALL_Y, -0.5)) @ Matrix.Rotation(math.radians(90), 4, 'X'), verts=bm.verts)
    out.append(mesh_object('_parede', bm))
    for o in out:
        o.modifiers.new('col', 'COLLISION')
        o.collision.thickness_outer = 0.003
        o.collision.cloth_friction = 8.0
    return out


def simulate(ob, frames):
    sc = bpy.context.scene
    sc.frame_start, sc.frame_end = 1, frames
    m = ob.modifiers.new('Cloth', 'CLOTH')
    st, cs = m.settings, m.collision_settings
    st.quality = int(env('JQ', 12))
    st.mass = env('JM', 0.15)
    st.air_damping = env('JAIR', 5.0)
    st.tension_stiffness = env('JT', 18.0)
    st.compression_stiffness = env('JC', 3.0)                       # cloth buckles instead of resisting compression
    st.shear_stiffness = env('JS', 4.0)
    st.bending_stiffness = env('JBEND', 0.08)
    st.bending_stiffness_max = env('JB', 5.0)
    st.vertex_group_bending = 'bainha'
    st.tension_damping = st.compression_damping = 5.0
    st.shear_damping = 5.0
    st.bending_damping = 0.6
    st.vertex_group_mass = 'pin'
    st.pin_stiffness = 2.0
    cs.use_collision = True
    cs.distance_min = 0.0035
    cs.use_self_collision = True
    cs.self_distance_min = 0.0030
    cs.self_friction = env('JSF', 2.0)
    cs.collision_quality = 4
    m.point_cache.frame_start, m.point_cache.frame_end = 1, frames
    for f in range(1, frames + 1):
        sc.frame_set(f)
        if f % 20 == 0:
            print('frame', f, flush=True)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    ob.modifiers.clear()
    old = ob.data
    ob.data = me
    bpy.data.meshes.remove(old)
    for p in ob.data.polygons:
        p.use_smooth = True


def save_drape(ob, sleeves, frames):
    data = {'v': [[round(c, 5) for c in v.co] for v in ob.data.vertices], 'f': [list(p.vertices) for p in ob.data.polygons],
            'manga': sleeves, 'frames': frames}
    with open(DRAPE, 'w') as f:
        json.dump(data, f, separators=(',', ':'))


def main():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    ob, sleeves, chain = build()
    print('verts', len(ob.data.vertices), 'faces', len(ob.data.polygons), 'A', tuple(round(c, 3) for c in A), flush=True)
    if FRAMES > 0:
        colliders()
        simulate(ob, FRAMES)
        add_collar(ob, chain)
        zs = [v.co.z for v in ob.data.vertices]
        print('RESULT minz %.3f' % min(zs), flush=True)
    save_drape(ob, sleeves, FRAMES)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)


main()

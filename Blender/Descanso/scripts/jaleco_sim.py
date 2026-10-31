"""Lab coat drape by cloth simulation (pilot): blender -b --factory-startup --python jaleco_sim.py -- out.blend [frames]
Writes ../malhas/jaleco_caimento.json, the settled mesh that pecas_jaleco.py thickens and finishes.
The garment starts as one hollow volume (torso + sleeves angled down), remeshed to even quads; the hem, the cuffs, the neck and the
front V are cut open. The shoulder line is pinned where it rests on the hanger arms; gravity, the hanger and the wall shape the rest.
The settled shape is applied to a static mesh (no simulation at run time).
Frame: origin on the wall hook's arm, garment hangs towards -Z, front is -Y, the wall is at y = +0.074."""
import bpy, bmesh, json, math, os, sys
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = argv[0] if argv else '/tmp/jaleco_sim.blend'
FRAMES = int(argv[1]) if len(argv) > 1 else 340
DRAPE = os.path.join(os.path.dirname(os.path.abspath(__file__)), '../malhas/jaleco_caimento.json')     # read by pecas_jaleco.py

HEM = -1.020
NECK_W = 0.072            # half width of the neck opening
V_BOTTOM = -0.335         # lowest point of the front V (top button just below)
WALL_Y = 0.074


def smooth(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def z_shoulder(x):
    """Top line of the hanger arms (same hanger as pecas_roupas.cabide)."""
    ax = abs(x)
    return -0.0825 - 0.267 * max(min(ax, 0.206), 0.0)


def link(ob):
    bpy.context.scene.collection.objects.link(ob)
    return ob


def mesh_object(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    return link(bpy.data.objects.new(name, me))


def ring_solid(rings):
    bm = bmesh.new()
    V = [[bm.verts.new(p) for p in r] for r in rings]
    n = len(V[0])
    for a, b in zip(V, V[1:]):
        for i in range(n):
            bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
    bm.faces.new(V[0])
    bm.faces.new(list(reversed(V[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def superellipse(cx, cy, z, rx, ry, n=48, e=2.6):
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        c, s = math.cos(a), math.sin(a)
        pts.append(Vector((cx + rx * math.copysign(abs(c) ** (2 / e), c), cy + ry * math.copysign(abs(s) ** (2 / e), s), z)))
    return pts


# ---------------------------------------------------------------- initial volume

def torso():
    """Body tube from above the shoulders to the hem: a little wider at the hem, shallow (no body inside)."""
    rings = []
    for k in range(40):
        z = lerp(0.0, HEM - 0.040, k / 39)
        w = lerp(0.214, 0.262, smooth(-0.12, -0.40, z)) + float(os.environ.get('JFLARE', 0.055)) * smooth(-0.45, HEM, z)
        d = lerp(0.034, 0.050, smooth(-0.08, -0.30, z)) - 0.010 * smooth(-0.40, HEM, z)
        rings.append(superellipse(0.0, 0.004, z, w, d, e=3.0))
    bm = ring_solid(rings)
    # shoulder slopes: cut along the hanger line (a little above it, the cloth thickness) on both sides
    for s in (-1, 1):
        x1 = 0.206
        p = Vector((0.0, 0.0, z_shoulder(0.0) + 0.006))
        q = Vector((s * x1, 0.0, z_shoulder(x1) + 0.006))
        d = (q - p).normalized()
        nrm = Vector((-d.z * s, 0.0, d.x * s)) * s
        if nrm.z < 0:
            nrm = -nrm
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=p, plane_no=nrm, clear_outer=True)
        edges = [e for e in bm.edges if e.is_boundary]
        bmesh.ops.holes_fill(bm, edges=edges, sides=0)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return mesh_object('Torso', bm)


SLEEVE_ANGLE = math.radians(45)      # from vertical
SLEEVE_LEN = 0.60


def sleeve_frame(s):
    root = Vector((s * 0.196, 0.004, -0.150))
    d = Vector((s * math.sin(SLEEVE_ANGLE), 0.0, -math.cos(SLEEVE_ANGLE))).normalized()
    return root, d


def sleeve(s):
    root, d = sleeve_frame(s)
    side = Vector((0, 1, 0)).cross(d).normalized()          # in the XZ plane, across the sleeve
    up = d.cross(side).normalized()
    rings = []
    for k in range(24):
        t = lerp(-0.07, SLEEVE_LEN + 0.04, k / 23)
        q = max(t, 0.0) / SLEEVE_LEN
        rx = lerp(0.066, 0.048, smooth(0.0, 1.0, q))          # across (in the plane of the coat)
        ry = lerp(0.036, 0.030, q)                             # front-back
        c = root + d * t
        ring = []
        for i in range(32):
            a = 2 * math.pi * i / 32
            ring.append(c + side * rx * math.cos(a) + up * 0.0 + Vector((0, 1, 0)) * ry * math.sin(a))
        rings.append(ring)
    return mesh_object(f'Manga {s:+d}', ring_solid(rings))


def build_volume(voxel):
    parts = [torso(), sleeve(-1), sleeve(1)]
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    ob = parts[0]
    ob.name = 'Jaleco sim'
    ob.data.remesh_voxel_size = voxel
    ob.data.use_remesh_fix_poles = True
    bpy.ops.object.voxel_remesh()
    return ob


# ---------------------------------------------------------------- openings

def open_garment(ob):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.normal_update()
    kill = set()
    for f in bm.faces:
        c, n = f.calc_center_median(), f.normal
        # hem
        if c.z < HEM:
            kill.add(f)
        # cuffs
        for s in (-1, 1):
            root, d = sleeve_frame(s)
            if (c - root).dot(d) > SLEEVE_LEN and c.x * s > 0.15:
                kill.add(f)
        # neck: the top surface between the hanger arms' inner ends, and a little down the back
        if abs(c.x) < NECK_W and c.z > z_shoulder(c.x) - 0.004 and n.z > 0.35:
            kill.add(f)
        # front V down to the top button
        if c.y < -0.01 and n.y < -0.3 and V_BOTTOM < c.z:
            zt = z_shoulder(NECK_W)
            q = smooth(0.0, 1.0, (zt - c.z) / (zt - V_BOTTOM))
            half = lerp(NECK_W + 0.004, 0.0, q ** 0.85)
            if abs(c.x) < half:
                kill.add(f)
    bmesh.ops.delete(bm, geom=list(kill), context='FACES')
    # loose bits and staircase edges
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bv = [v for v in bm.verts if v.is_boundary]
    inner = [v for v in bm.verts if not v.is_boundary]
    for _ in range(10):                                   # relax the voxel staircase before the drape
        bmesh.ops.smooth_vert(bm, verts=inner, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    for _ in range(8):                                    # the cut edges: along the edge only, so the hem does not curl in
        nb = {v: [e.other_vert(v) for e in v.link_edges if e.is_boundary] for v in bv}
        new = {v: (v.co + (n[0].co + n[1].co) / 2) / 2 for v, n in nb.items() if len(n) == 2}
        for v, c in new.items():
            v.co = c
    bm.to_mesh(ob.data)
    bm.free()


def flatten(ob, k):
    """An empty coat is nearly flat: the rest shape of the bending springs must be too (a round start keeps a bag at the hem)."""
    for v in ob.data.vertices:
        v.co.y = 0.004 + (v.co.y - 0.004) * k


def pin_group(ob):
    vg = ob.vertex_groups.new(name='pin')
    for v in ob.data.vertices:
        p = v.co
        if 0.050 < abs(p.x) < 0.202 and p.z > z_shoulder(p.x) - 0.010:
            w = 1.0 - 0.7 * smooth(0.15, 0.202, abs(p.x))     # softer towards the hanger end: the shoulder settles over it
            vg.add([v.index], max(w, 0.0), 'REPLACE')
    return vg


def hem_group(ob):
    """Turned hems and facings (double fabric) bend less: hem, cuffs and the front edge."""
    vg = ob.vertex_groups.new(name='bainha')
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    edge = [v.co.copy() for v in bm.verts if v.is_boundary]
    bm.free()
    from mathutils import kdtree
    kd = kdtree.KDTree(len(edge))
    for i, c in enumerate(edge):
        kd.insert(c, i)
    kd.balance()
    roots = [sleeve_frame(s)[0] for s in (-1, 1)]
    for v in ob.data.vertices:
        _, _, d = kd.find(v.co)
        w = 0.21 + 0.79 * (1 - smooth(0.012, 0.040, d))                 # 0.21 -> the body's 1.5
        # the armhole seam: limp, so the sleeves do not keep the angle they start at
        w *= min(smooth(0.06, 0.12, (v.co - r).length) for r in roots)
        vg.add([v.index], w, 'REPLACE')


# ---------------------------------------------------------------- colliders

def colliders():
    out = []
    # hanger: arms + neck block as one rough solid (the visible hanger is built separately)
    bm = bmesh.new()
    for s in (-1, 1):
        pts = [(0.0, -0.0825), (s * 0.206, -0.1375), (s * 0.206, -0.1560), (0.0, -0.1010)]
        vs = [bm.verts.new((x, y, z)) for (x, z) in pts for y in (-0.006, 0.006)]
        bmesh.ops.convex_hull(bm, input=vs)
    ob = mesh_object('_cabide', bm)
    out.append(ob)
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
    st.quality = int(os.environ.get('JQ', 14))
    st.mass = float(os.environ.get('JM', 0.15))
    st.air_damping = 7.0
    st.tension_stiffness = st.compression_stiffness = float(os.environ.get('JT', 18.0))
    st.shear_stiffness = float(os.environ.get('JS', 12.0))
    st.bending_stiffness = float(os.environ.get('JBEND', 0.3))                 # cotton twill: holds soft folds, no crumpling
    st.tension_damping = st.compression_damping = 5.0
    st.bending_damping = 0.6
    st.vertex_group_bending = 'bainha'
    st.bending_stiffness_max = float(os.environ.get('JB', 6.0))
    st.vertex_group_mass = 'pin'
    st.pin_stiffness = 2.0
    cs.use_collision = True
    cs.distance_min = 0.0035
    cs.use_self_collision = True
    cs.self_distance_min = 0.003
    cs.self_friction = 1.0
    cs.collision_quality = 4
    m.point_cache.frame_start, m.point_cache.frame_end = 1, frames
    for f in range(1, frames + 1):
        sc.frame_set(f)
        if f % 10 == 0:
            print('frame', f, flush=True)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    ob.modifiers.clear()
    old = ob.data
    ob.data = me
    bpy.data.meshes.remove(old)
    for p in ob.data.polygons:
        p.use_smooth = True
    return ob


def sleeve_faces(ob):
    """Faces of the sleeves, told apart before the drape (topology does not change during the simulation)."""
    out = []
    for p in ob.data.polygons:
        c = Vector(p.center)
        for s in (-1, 1):
            root, d = sleeve_frame(s)
            if (c - root).dot(d) > 0.03 and c.x * s > 0.20:
                out.append(p.index)
    return out


def save_drape(ob, sleeves):
    os.makedirs(os.path.dirname(DRAPE), exist_ok=True)
    data = {'v': [[round(c, 5) for c in v.co] for v in ob.data.vertices], 'f': [list(p.vertices) for p in ob.data.polygons], 'manga': sleeves,
            'frames': FRAMES}
    with open(DRAPE, 'w') as f:
        json.dump(data, f, separators=(',', ':'))


def main():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    ob = build_volume(0.013)
    open_garment(ob)
    flatten(ob, float(os.environ.get('JF', 1.0)))
    pin_group(ob)
    hem_group(ob)
    print('verts', len(ob.data.vertices), flush=True)
    colliders()
    sleeves = sleeve_faces(ob)
    simulate(ob, FRAMES)
    bm = bmesh.new()                                      # take out the solver's high-frequency crunch, keep the folds
    bm.from_mesh(ob.data)
    inner = [v for v in bm.verts if not v.is_boundary]
    for _ in range(3):
        bmesh.ops.smooth_laplacian_vert(bm, verts=inner, lambda_factor=0.4, lambda_border=0.0, use_x=True, use_y=True, use_z=True, preserve_volume=True)
    bm.to_mesh(ob.data)
    bm.free()
    save_drape(ob, sleeves)
    zs = [v.co.z for v in ob.data.vertices]
    low = [v.co.x for v in ob.data.vertices if v.co.z < min(zs) + 0.06 and abs(v.co.x) < 0.33]
    print('RESULT minz %.3f hemwidth %.3f' % (min(zs), max(low) - min(low)), flush=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)


main()

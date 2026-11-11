"""Autopsy table pilot: covered body (occupied state). Original, built from measured volumes.
Run: blender -b --factory-startup --threads 8 --python Blender/Autopsia/scripts/corpo.py [-- etapa ...]
  etapas: corpo (anatomy + hands), lencol (cloth drop over body + real table), detalhes (sticker, tag), render
Frame: the table's own (Blender/MesaAutopsia): origin on the pedestal axis at floor level, tray along X,
head end at -X. In Unity the pieces sit at the table's transform (2.7, 0, -0.45), yaw 0.
Story (Assets/Historia): head to the west, right hand visible (south, -Y) with a small irregular sticker on
its back, left forearm exposed (north, +Y) without a splint, transfer tag 083 fastened to the sheet. Nothing
graphic: the face and body stay under the sheet."""
import os, sys, math, json, random
import bpy, bmesh
from mathutils import Vector, Matrix, Quaternion
from mathutils.bvhtree import BVHTree

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
SRC = ROOT + "/Blender/Autopsia"
MESA = ROOT + "/Blender/MesaAutopsia/MesaAutopsia.blend"
STEPS = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else ["corpo", "lencol", "detalhes", "render", "exportar"]
BLEND = SRC + "/Corpo.blend"

# ---------------------------------------------------------------- table surface (copied from mesa_modelo.py)
X0, HALF_Y, DRAIN_X = -1.20, 0.40, 1.045
def tray_floor(x, y):
    hw = HALF_Y - 0.04
    z = 0.8905 + 0.009 * min(abs(y) / hw, 1.0)
    if x < DRAIN_X: z += 0.007 * (DRAIN_X - x) / (DRAIN_X - X0)
    return z

# ---------------------------------------------------------------- primitives
def _link(name, bm, coll="Piloto"):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    c = bpy.data.collections.get(coll) or bpy.data.collections.new(coll)
    if c.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(c)
    c.objects.link(ob); return ob

def _align(direction):
    d = Vector(direction).normalized()
    return Vector((1, 0, 0)).rotation_difference(d).to_matrix().to_4x4()

def ellipsoid(bm, c, a, b, h, direction=(1, 0, 0), seg=24):
    g = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=seg//2, radius=1.0)
    m = Matrix.Translation(c) @ _align(direction) @ Matrix.Diagonal((a, b, h, 1))
    bmesh.ops.transform(bm, matrix=m, verts=g["verts"])

def capsule(bm, a, b, r0, r1, flat=1.0, n=6):
    """Tapered limb segment: continuous cone with rounded caps (no beads); flat < 1 squashes it vertically."""
    a, b = Vector(a), Vector(b); d = b - a; L = d.length
    if L < 1e-6: return
    g = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=32, radius1=r0, radius2=r1, depth=L)
    rot = Vector((0, 0, 1)).rotation_difference(d).to_matrix().to_4x4()
    # Vertical squash in the limb's own frame: the local axis closest to world Z.
    side = d.cross(Vector((0, 0, 1)))
    up = side.cross(d).normalized() if side.length > 1e-6 else Vector((1, 0, 0))
    sq = Matrix.Identity(4)
    if flat != 1.0:
        u = up.to_4d(); u.w = 0
        sq = Matrix.Identity(4) - (1 - flat) * Matrix([[u[i] * u[j] for j in range(4)] for i in range(4)])
        sq[3][3] = 1
    mid = (a + b) / 2
    bmesh.ops.transform(bm, matrix=Matrix.Translation(mid) @ sq @ rot, verts=g["verts"])
    for c, r in ((a, r0), (b, r1)):
        e = bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=r)
        bmesh.ops.transform(bm, matrix=Matrix.Translation(c) @ sq, verts=e["verts"])

def remesh_smooth(ob, voxel, lam=.6, it=8, decimate=None):
    bpy.context.view_layer.objects.active = ob
    m = ob.modifiers.new("Uniao", "REMESH"); m.mode = "VOXEL"; m.voxel_size = voxel; m.adaptivity = 0
    s = ob.modifiers.new("Suavizar", "LAPLACIANSMOOTH"); s.lambda_factor = lam; s.iterations = it; s.use_volume_preserve = True
    for mod in list(ob.modifiers): bpy.ops.object.modifier_apply(modifier=mod.name)
    if decimate:
        d = ob.modifiers.new("Reduzir", "DECIMATE"); d.ratio = decimate; bpy.ops.object.modifier_apply(modifier=d.name)
    for p in ob.data.polygons: p.use_smooth = True

def settle(ob, gap=.0012, soft=.008):
    """Contact with the tray: soft maximum against the floor, so the weight flattens the underside and every
    vertex that reached below the floor ends just on it (nothing floats, nothing sinks)."""
    for v in ob.data.vertices:
        w = ob.matrix_world @ v.co
        f = tray_floor(w.x, w.y) + gap; d = w.z - f
        if d < .04:
            w.z = f + (d + math.sqrt(d * d + soft * soft)) / 2 - soft / 2 * math.exp(-abs(d) / soft)
            v.co = ob.matrix_world.inverted() @ w

# ---------------------------------------------------------------- anatomy (adult, ~1.74 m, lying supine)
ZF = 0.8935
R_WRIST = Vector((-0.262, -0.312, 0.0)); R_ELBOW = Vector((-0.520, -0.245, 0.0))
L_WRIST = Vector((-0.300, 0.312, 0.0)); L_ELBOW = Vector((-0.545, 0.268, 0.0))

def body():
    bm = bmesh.new()
    # Head on the rubber block (cradle ~0.95 m), face up, nose and chin give the sheet its profile.
    ellipsoid(bm, (-1.035, 0, 1.048), .112, .077, .098)
    ellipsoid(bm, (-0.945, 0, 1.028), .052, .056, .062)          # jaw and chin
    ellipsoid(bm, (-1.000, 0, 1.135), .020, .011, .016)          # nose
    ellipsoid(bm, (-1.045, 0, 1.118), .030, .055, .022)          # brow ridge
    capsule(bm, (-0.93, 0, 1.000), (-0.83, 0, 0.990), .056, .062, n=3)   # neck
    # Trunk: thorax, shoulders, abdomen, pelvis; the back rests on the tray.
    ellipsoid(bm, (-0.620, 0, 0.998), .205, .168, .108)
    for s in (-1, 1):
        ellipsoid(bm, (-0.775, s * .168, 0.982), .075, .072, .066)
        ellipsoid(bm, (-0.640, s * .110, 1.040), .120, .070, .062)  # pectoral mass
    ellipsoid(bm, (-0.370, 0, 0.985), .170, .150, .092)
    ellipsoid(bm, (-0.175, 0, 0.978), .120, .168, .084)
    for s in (-1, 1):
        # Leg: thigh, knee, calf, ankle; the feet fall slightly outward.
        capsule(bm, (-0.200, s * .098, 0.978), (0.200, s * .108, 0.958), .080, .060, flat=.92, n=6)
        ellipsoid(bm, (0.215, s * .108, 0.958), .055, .052, .055)
        capsule(bm, (0.230, s * .110, 0.952), (0.595, s * .120, 0.934), .052, .034, n=6)
        ellipsoid(bm, (0.340, s * .110, 0.952), .095, .050, .054)   # calf
        heel = Vector((0.615, s * .118, 0.928)); toe = Vector((0.672, s * .182, 1.060))
        capsule(bm, heel, heel + (toe - heel) * .55, .036, .033, n=4)
        ellipsoid(bm, heel + (toe - heel) * .78, .052, .042, .020, toe - heel)      # forefoot, flat
        ellipsoid(bm, (0.598, s * .118, 0.947), .030, .028, .034)                  # Achilles / ankle
        # Upper arms (both under the sheet); the forearms are built here too, the hands separately.
    for s, elbow, wrist in ((-1, R_ELBOW, R_WRIST), (1, L_ELBOW, L_WRIST)):
        sh = Vector((-0.800, s * .205, 0.972))
        e = Vector((elbow.x, elbow.y, ZF + .046)); w = Vector((wrist.x, wrist.y, ZF + .031))
        capsule(bm, sh, e, .048, .043, n=6)
        ellipsoid(bm, e, .040, .044, .044)
        # Forearm: wider than deep, muscle bulk near the elbow, flat at the wrist.
        capsule(bm, e + (w - e) * .02, w, .043, .030, flat=.82, n=8)
        ellipsoid(bm, e + (w - e) * .28, .085, .047, .040, w - e)          # forearm muscle bulk
        ellipsoid(bm, e + (w - e) * .80, .055, .036, .026, w - e)          # flat distal forearm
    ob = _link("CorpoMesa", bm)
    remesh_smooth(ob, .005, lam=.7, it=24, decimate=.6)
    settle(ob)
    return ob

def hand(name, wrist, elbow, mirror):
    """Relaxed hand palm down on the tray: knuckles raised, fingertips touching, thumb along the index.
    Local frame: x = towards the fingertips, y = thumb side, z = up (dorsum)."""
    bm = bmesh.new()
    ellipsoid(bm, (.000, .000, .022), .030, .030, .019)                 # wrist
    ellipsoid(bm, (.055, .000, .021), .062, .041, .017)                 # palm
    ellipsoid(bm, (.040, -.022, .018), .045, .020, .015)                # hypothenar edge
    ellipsoid(bm, (.032, .026, .017), .034, .022, .015, (1, .5, 0))     # thenar
    fingers = [(.030, .101, .0088, (.041, .025, .019)), (.010, .106, .0093, (.045, .028, .020)),
               (-.010, .103, .0089, (.043, .026, .019)), (-.029, .095, .0078, (.034, .020, .017))]
    for k, (y, x, r, (l1, l2, l3)) in enumerate(fingers):
        spread = math.radians((1.5 - k) * 3.2)                         # slight fan
        base = Vector((x, y, .020))
        def chain(k):
            pts = [base]; d = Vector((math.cos(spread), math.sin(spread), 0))
            for L, ang in zip((l1, l2, l3), (k, 2.2 * k, 3.0 * k)):
                a = math.radians(ang)
                pts.append(pts[-1] + Vector((d.x * math.cos(a), d.y * math.cos(a), -math.sin(a))) * L)
            return pts
        lo, hi = 0.0, 20.0                                              # flexion so the tip rests on the tray
        for _ in range(30):
            mid = (lo + hi) / 2
            if chain(mid)[-1].z > r * .92: lo = mid
            else: hi = mid
        pts = chain((lo + hi) / 2)
        for i in range(3):
            capsule(bm, pts[i], pts[i + 1], r * (1.06 - .08 * i), r * (.98 - .08 * i), flat=.88, n=3)
            ellipsoid(bm, pts[i], r * 1.08, r * 1.08, r * .98)          # knuckle
        ellipsoid(bm, pts[3], r * .85, r * .82, r * .72)
    th = [Vector(p) for p in ((.020, .030, .016), (.050, .049, .0135), (.076, .060, .0105), (.094, .064, .0090))]
    for i, r in enumerate((.0125, .0112, .0098)):
        capsule(bm, th[i], th[i + 1], r, r * .92, flat=.9, n=3)
    ellipsoid(bm, th[3], .0085, .0080, .0072)
    ob = _link(name, bm)
    remesh_smooth(ob, .0017, lam=.45, it=8, decimate=.55)
    d = (wrist - elbow); yaw = math.atan2(d.y, d.x)
    ob.data.transform(Matrix.Diagonal((1, -1 if mirror else 1, 1, 1)))
    if mirror: ob.data.flip_normals()
    w = Vector((wrist.x, wrist.y, tray_floor(wrist.x, wrist.y) + .0005))
    ob.matrix_world = Matrix.Translation(w) @ Matrix.Rotation(yaw, 4, "Z")
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True); ob.select_set(False)
    settle(ob, gap=.0008, soft=.0025)
    return ob

# ---------------------------------------------------------------- scene helpers
def load_table():
    if "MesaAutopsia" in bpy.data.objects: return bpy.data.objects["MesaAutopsia"]
    with bpy.data.libraries.load(MESA, link=False) as (src, dst): dst.objects = ["MesaAutopsia"]
    ob = dst.objects[0]; bpy.context.scene.collection.objects.link(ob); return ob

def simple_material(name, rgb, rough=.8):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = (*rgb, 1); b.inputs["Roughness"].default_value = rough
    return m

def set_mat(ob, m):
    ob.data.materials.clear(); ob.data.materials.append(m)

# ---------------------------------------------------------------- sheet
SHEET = dict(x0=-1.30, x1=0.90, width=0.76, offset=0.005, step=.014)

def sheet():
    body_parts = [bpy.data.objects[n] for n in ("CorpoMesa", "MaoDireita", "MaoEsquerda")]
    table = load_table()
    for o in body_parts + [table]:
        c = o.modifiers.get("Colisao") or o.modifiers.new("Colisao", "COLLISION")
        o.collision.thickness_outer = .0025 if o is not table else .003
        o.collision.cloth_friction = 20.0; o.collision.damping = .8
    x0, x1, W, off, st = SHEET["x0"], SHEET["x1"], SHEET["width"], SHEET["offset"], SHEET["step"]
    nx, ny = int((x1 - x0) / st), int(W / st)
    bm = bmesh.new()
    rows = [[bm.verts.new((x0 + (x1 - x0) * i / nx, off - W / 2 + W * j / ny, 1.19)) for j in range(ny + 1)] for i in range(nx + 1)]
    for i in range(nx):
        for j in range(ny):
            bm.faces.new((rows[i][j], rows[i + 1][j], rows[i + 1][j + 1], rows[i][j + 1]))
    ob = _link("LencolCorpo", bm)
    cl = ob.modifiers.new("Tecido", "CLOTH"); s = cl.settings
    s.quality = 12; s.mass = .32; s.air_damping = 3.0
    s.tension_stiffness = s.compression_stiffness = 18; s.shear_stiffness = 8; s.bending_stiffness = 1.2
    s.tension_damping = s.compression_damping = s.shear_damping = 5
    cs = cl.collision_settings; cs.collision_quality = 4; cs.distance_min = .0035
    cs.use_self_collision = True; cs.self_distance_min = .003; cs.self_friction = 8
    sc = bpy.context.scene; sc.frame_start = 1; sc.frame_end = 120
    cl.point_cache.frame_start = 1; cl.point_cache.frame_end = 120
    for f in range(1, 121): sc.frame_set(f)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=cl.name)
    sm = ob.modifiers.new("Assentar", "CORRECTIVE_SMOOTH"); sm.factor = .25; sm.iterations = 3
    so = ob.modifiers.new("Espessura", "SOLIDIFY"); so.thickness = .0016; so.offset = 1
    for mod in (sm, so): bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in ob.data.polygons: p.use_smooth = True
    for o in body_parts + [table]:
        if o.modifiers.get("Colisao"): o.modifiers.remove(o.modifiers["Colisao"])
    sc.frame_set(1)
    return ob

def check():
    """Interpenetration and contact report: sheet vs body, hands, table; hands and body vs tray."""
    sc = bpy.context.scene; deps = bpy.context.evaluated_depsgraph_get()
    def bvh(n): return BVHTree.FromObject(bpy.data.objects[n], deps)
    rep = {}
    sh = bpy.data.objects.get("LencolCorpo")
    for n in ("CorpoMesa", "MaoDireita", "MaoEsquerda", "MesaAutopsia"):
        if n not in bpy.data.objects or sh is None: continue
        t = bvh(n); inside = 0
        for v in sh.data.vertices:
            p = sh.matrix_world @ v.co; hit = t.find_nearest(p)
            if hit[0] is not None and (p - hit[0]).dot(hit[1]) < -0.0015 and hit[3] < .03: inside += 1  # deep 'inside' hits are concave false positives
        rep["lencol dentro de " + n] = inside
    for n in ("CorpoMesa", "MaoDireita", "MaoEsquerda"):
        ob = bpy.data.objects[n]; zs = [(ob.matrix_world @ v.co) for v in ob.data.vertices]
        rep["abaixo do leito " + n] = sum(1 for p in zs if p.z < tray_floor(p.x, p.y) - .0005)
        rep["tocando o leito " + n] = sum(1 for p in zs if p.z < tray_floor(p.x, p.y) + .003)
    if sh is not None:
        ys = [(sh.matrix_world @ v.co) for v in sh.data.vertices]
        def hem(sign, x):
            band = [p.y for p in ys if abs(p.x - x) < .03]
            return round(min(band), 3) if sign < 0 else round(max(band), 3)
        rep["bainha sul em x(maos/antebraco)"] = {x: hem(-1, x) for x in (-0.45, -0.30, -0.20)}
        rep["bainha norte em x"] = {x: hem(1, x) for x in (-0.50, -0.40, -0.30)}
        rep["lencol z max"] = round(max(p.z for p in ys), 3)
    print("VERIFICACAO", json.dumps(rep), flush=True)
    os.makedirs(SRC + "/revisao", exist_ok=True)
    json.dump(rep, open(SRC + "/revisao/corpo_verificacao.json", "w"), indent=2)


# ---------------------------------------------------------------- details: sticker on the right hand, tag 083
def _surface(target, x, y, z0=1.4):
    deps = bpy.context.evaluated_depsgraph_get(); t = BVHTree.FromObject(bpy.data.objects[target], deps)
    hit = t.ray_cast(Vector((x, y, z0)), Vector((0, 0, -1)))
    return hit[0], hit[1]

def conform_patch(name, target, center, size, yaw, outline, lift=.0007, n=10):
    """Thin patch laid on a surface by ray casts (follows the skin or the cloth), irregular outline."""
    cx, cy = center; a, b = size; c, s_ = math.cos(yaw), math.sin(yaw)
    bm = bmesh.new(); grid = {}
    for i in range(n + 1):
        for j in range(n + 1):
            u, v = i / n - .5, j / n - .5
            if not outline(u, v): continue
            x = cx + u * a * c - v * b * s_; y = cy + u * a * s_ + v * b * c
            p, nrm = _surface(target, x, y)
            if p is None: continue
            grid[i, j] = bm.verts.new(p + nrm * lift)
    for i in range(n):
        for j in range(n):
            q = [grid.get(k) for k in ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))]
            if all(q): bm.faces.new(q)
    ob = _link(name, bm)
    so = ob.modifiers.new("Espessura", "SOLIDIFY"); so.thickness = .0004
    bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier=so.name)
    return ob

def details():
    for n in ("AdesivoMao", "EtiquetaMovimentacao", "Alfinete"):
        if n in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
    # Small irregular sticker (~22 x 15 mm) on the back of the right hand, a bit off-centre, one corner lifted.
    rnd = random.Random(83)
    jag = [rnd.uniform(.80, 1.0) for _ in range(16)]
    def ragged(u, v):
        ang = (math.atan2(v, u) % math.tau) / math.tau * 16; k = int(ang) % 16
        r = jag[k] * (1 - (ang % 1)) + jag[(k + 1) % 16] * (ang % 1)
        return (u / .5) ** 2 + (v / .5) ** 2 <= r * r
    d = (R_WRIST - R_ELBOW).normalized(); yaw = math.atan2(d.y, d.x)
    hx = R_WRIST.x + d.x * .055 - d.y * .006; hy = R_WRIST.y + d.y * .055 + d.x * .006
    conform_patch("AdesivoMao", "MaoDireita", (hx, hy), (.022, .015), yaw + .35, ragged, lift=.0006, n=8)
    # Transfer tag 083: 90 x 55 mm card laid on the sheet over the abdomen, toward the right side,
    # held by a safety pin through its eyelet. The print ("083") is applied with the signage (step 8).
    tx, ty = -0.30, -0.075
    conform_patch("EtiquetaMovimentacao", "LencolCorpo", (tx, ty), (.090, .055), math.radians(-12),
                  lambda u, v: True, lift=.0012, n=8)
    p, nrm = _surface("LencolCorpo", tx - .052, ty + .010)
    bm = bmesh.new()
    pin = [Vector((0, 0, 0)), Vector((-.018, .003, 0)), Vector((-.030, .003, .001)), Vector((-.030, -.003, .001)),
           Vector((-.018, -.004, .0012)), Vector((.012, -.004, .0012))]
    m = Matrix.Translation(p + nrm * .0025) @ Matrix.Rotation(math.radians(-12), 4, "Z")
    for a, b in zip(pin, pin[1:]):
        capsule(bm, m @ a, m @ b, .0006, .0006, n=1)
    ob = _link("Alfinete", bm)

# ---------------------------------------------------------------- export for the Unity pilot (no bake yet)
def export():
    import types
    ns = {"__name__": "corpo_export", "__file__": ROOT + "/Blender/Recepcao/scripts/recepcao_modelo.py"}
    exec(compile(open(ns["__file__"]).read(), ns["__file__"], "exec"), ns); R = types.SimpleNamespace(**ns)
    out = ROOT + "/Assets/Modelos/Autopsia/Piloto"; os.makedirs(out, exist_ok=True)
    groups = {"CorpoOcupado": ["CorpoMesa", "MaoDireita", "MaoEsquerda"], "LencolOcupado": ["LencolCorpo"],
              "AdesivoMao": ["AdesivoMao"], "EtiquetaMovimentacao": ["EtiquetaMovimentacao", "Alfinete"]}
    stats = {}
    for name, parts in groups.items():
        objs = [bpy.data.objects[n] for n in parts if n in bpy.data.objects]
        if not objs: continue
        copies = []
        for o in objs:
            c = o.copy(); c.data = o.data.copy(); bpy.context.scene.collection.objects.link(c); copies.append(c)
        bpy.ops.object.select_all(action="DESELECT")
        for c in copies: c.select_set(True)
        bpy.context.view_layer.objects.active = copies[0]
        if len(copies) > 1: bpy.ops.object.join()
        j = bpy.context.view_layer.objects.active; j.name = name + "_exp"
        j.data.materials.clear()
        bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=.004); bpy.ops.object.mode_set(mode="OBJECT")
        stats[name] = len(j.data.polygons)
        j.name = name; R.export_fbx(j, out + "/" + name + ".fbx")
        for o in list(bpy.data.objects):
            if o.name.startswith(name) and o.name not in parts: bpy.data.objects.remove(o, do_unlink=True)
    print("EXPORT", json.dumps(stats), flush=True)

# ---------------------------------------------------------------- render (simple materials: silhouette check)
def render():
    sc = bpy.context.scene; table = load_table()
    try: sc.render.engine = "CYCLES"
    except TypeError as e: raise RuntimeError(e)
    sc.cycles.samples = 48; sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 1280, 900
    sc.world.use_nodes = True
    bg = next(n for n in sc.world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (.16, .18, .20, 1); bg.inputs["Strength"].default_value = .5
    if "Piso estudio" not in bpy.data.objects:
        bpy.ops.mesh.primitive_plane_add(size=40); bpy.context.object.name = "Piso estudio"
        set_mat(bpy.context.object, simple_material("Estudio", (.16, .17, .16), .9))
        for p, e in [((2.5, -3.5, 4.5), 650), ((-2.5, 2.0, 4.0), 380), ((0.5, 0.5, 4.0), 300)]:
            ld = bpy.data.lights.new("Softbox", "AREA"); ld.energy = e; ld.size = 3
            lo = bpy.data.objects.new("Softbox", ld); sc.collection.objects.link(lo); lo.location = p
            lo.rotation_euler = (Vector((-.3, 0, .95)) - lo.location).to_track_quat("-Z", "Y").to_euler()
    set_mat(table, simple_material("Mesa simples", (.55, .56, .56), .35))
    table.data.materials[0].node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = .9
    pele = simple_material("Pele simples", (.62, .55, .50), .55)
    for n in ("CorpoMesa", "MaoDireita", "MaoEsquerda"):
        if n in bpy.data.objects: set_mat(bpy.data.objects[n], pele)
    if "LencolCorpo" in bpy.data.objects: set_mat(bpy.data.objects["LencolCorpo"], simple_material("Lencol simples", (.70, .72, .72), .85))
    for n, rgb in (("AdesivoMao", (.85, .82, .70)), ("EtiquetaMovimentacao", (.90, .88, .80)), ("Alfinete", (.6, .6, .6))):
        if n in bpy.data.objects: set_mat(bpy.data.objects[n], simple_material(n + " simples", rgb, .5))
    cd = bpy.data.cameras.get("Revisao") or bpy.data.cameras.new("Revisao")
    cam = bpy.data.objects.get("Revisao") or bpy.data.objects.new("Revisao", cd)
    if cam.name not in sc.collection.objects: sc.collection.objects.link(cam)
    sc.camera = cam
    views = [
        ("geral-sul", (0.9, -2.4, 1.65), (-0.2, 0, 0.98), 35),
        ("cabeceira", (-2.3, -0.9, 1.62), (-0.5, 0, 0.98), 40),
        ("geral-norte", (-0.1, 2.5, 1.65), (-0.2, 0, 0.98), 35),
        ("mao-direita", (-0.05, -0.72, 1.22), (-0.20, -0.32, 0.92), 50),
        ("antebraco-esquerdo", (-0.30, 0.92, 1.25), (-0.38, 0.30, 0.93), 45),
        ("topo", (-0.15, 0, 3.6), (-0.15, 0, 0.9), 40),
    ]
    out = SRC + "/revisao/corpo"; os.makedirs(out, exist_ok=True)
    for name, pos, tgt, lens in views:
        if "--" in sys.argv and "render" not in STEPS: break
        cd.lens = lens; cam.location = pos
        cam.rotation_euler = (Vector(tgt) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = out + "/" + name + ".png"; bpy.ops.render.render(write_still=True)
        print("RENDERED", name, flush=True)

def main():
    if os.path.exists(BLEND) and "corpo" not in STEPS: bpy.ops.wm.open_mainfile(filepath=BLEND)
    else:
        for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    if "corpo" in STEPS:
        body(); hand("MaoDireita", R_WRIST, R_ELBOW, False); hand("MaoEsquerda", L_WRIST, L_ELBOW, True)
        load_table()
    if "lencol" in STEPS:
        if "LencolCorpo" in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects["LencolCorpo"], do_unlink=True)
        sheet()
    if "detalhes" in STEPS: details()
    check()
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    if "exportar" in STEPS: export(); bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    if "render" in STEPS: render(); bpy.ops.wm.save_as_mainfile(filepath=BLEND)

if __name__ == "__main__": main()

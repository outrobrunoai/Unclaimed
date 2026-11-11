"""Cold room occupants, built from the MPFB 2 (MakeHuman, CC0) base like the autopsy body (corpo_base.py).
Run: blender -b --threads 8 --python Blender/CamaraFria/scripts/ocupantes.py -- C-01 C-02 C-04 [render] [exportar]
Story (Assets/Historia, P07 / D02): C-01 = 081 Joao Ferraz (P24), C-04 = 082 Celia Prado (P25), both fully covered;
C-02 = the real 083, Leo Martins (P26): rigid splint on the left forearm, wristband 083. C-03 stays empty.
Frame: MacaCompartimento (compartment reference with the tray closed): +X across (Unity local +X, south), +Y into the
cabinet, Z up from the top of the rail; head at the door end. The tray's first stop is 1.0 m, so the forearms rest
across the abdomen (also how bodies fit a 0.48 m pan): on C-02 the splint and the wristband are inside the stretch that
comes out. Nothing graphic: faces stay covered (on C-02 too: only the left arm is out of the sheet).
Only what can be seen is exported: C-01/C-04 = sheet only (the body is just the cloth's collider); C-02 = body with
the never-seen part decimated, sheet, splint, wristband."""
import os, sys, math, json
import bpy, bmesh
from mathutils import Vector, Matrix, Quaternion
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "../../.."))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
sys.argv = sys.argv[:sys.argv.index("--")] if "--" in sys.argv else sys.argv   # corpo_base parses its own steps
CB = {"__name__": "corpo_base_lib", "__file__": ROOT + "/Blender/Autopsia/scripts/corpo_base.py"}
exec(compile(open(CB["__file__"]).read(), CB["__file__"], "exec"), CB)
upd, rotate_about, aim, head, tail, flex, palm_normal = (CB[k] for k in ("upd", "rotate_about", "aim", "head", "tail", "flex", "palm_normal"))
settle, simple_material, set_mat = CB["settle"], CB["simple_material"], CB["set_mat"]
smooth_hem, reduce_sheet, smooth_normals_source, transfer_normals, tris = (CB[k] for k in ("smooth_hem", "reduce_sheet", "smooth_normals_source", "transfer_normals", "tris"))

SRC = ROOT + "/Blender/CamaraFria"
BLEND = SRC + "/Ocupantes.blend"
OUT = SRC + "/revisao/ocupantes"
ASSETS = ROOT + "/Assets/Modelos/CamaraFria/Ocupantes"
MPFB_USER = os.path.expanduser("~/.config/blender/5.2/extensions/.user/blender_org/mpfb/data")
STAGE1 = 1.00                      # tray first stop (pivos.json cursoMaca)
OPENING = (-.40, .40, -.065, .485)   # compartment opening in this frame (x0, x1, z0, z1) at y = 0

# Different people from the autopsy base (Daniel, aksel skin). Heights are MakeHuman macro values.
PEOPLE = {
    "C-01": dict(nome="Joao", macro={"gender": 1.0, "age": .62, "muscle": .55, "weight": .62, "proportions": .5, "height": .50,
                 "cupsize": .5, "firmness": .5, "race": {"asian": .1, "caucasian": .6, "african": .3}}, skin=None),
    "C-02": dict(nome="Leo", macro={"gender": 1.0, "age": .45, "muscle": .5, "weight": .45, "proportions": .5, "height": .48,
                 "cupsize": .5, "firmness": .5, "race": {"asian": .1, "caucasian": .25, "african": .65}},
                 skin=MPFB_USER + "/skins/mindfront_skin_male_african_middleage/mindfront_skin_male_african_middleage.mhmat"),
    "C-04": dict(nome="Celia", macro={"gender": 0.0, "age": .55, "muscle": .45, "weight": .55, "proportions": .5, "height": .42,
                 "cupsize": .5, "firmness": .5, "race": {"asian": .15, "caucasian": .55, "african": .3}}, skin=None),
}

# ---------------------------------------------------------------- tray (collider + floor function)
def load_tray():
    if "Bandeja" in bpy.data.objects: return bpy.data.objects["Bandeja"]
    with bpy.data.libraries.load(SRC + "/CamaraFria.blend", link=False) as (src, dst):
        dst.objects = ["MacaCompartimento"]
    ob = dst.objects[0]; ob.name = "Bandeja"; bpy.context.scene.collection.objects.link(ob)
    ob.matrix_world = Matrix.Identity(4); upd()
    return ob

def floor_fn(objs):
    deps = bpy.context.evaluated_depsgraph_get()
    trees = [(BVHTree.FromObject(o, deps), o.matrix_world) for o in objs]
    def floor(x, y, z):
        best = None
        for t, mw in trees:
            hit = t.ray_cast(Vector((x, y, z + .02)), Vector((0, 0, -1)), 1.0)
            if hit[0] is not None and (best is None or hit[0].z > best): best = hit[0].z
        return best
    return floor

# ---------------------------------------------------------------- pose: supine, forearms across the abdomen
def two_bone(S, W, L1, L2, pole):
    d = W - S; D = min(d.length, L1 + L2 - 1e-4); u = d.normalized()
    a = (L1 * L1 - L2 * L2 + D * D) / (2 * D); h = math.sqrt(max(L1 * L1 - a * a, 0))
    p = pole - u * pole.dot(u); p.normalize()
    return S + u * a + p * h

def front_y(pts, x, z):
    band = [p.y for p in pts if abs(p.x - x) < .05 and abs(p.z - z) < .025]
    return min(band) if band else None

def pose_tray(rig, pts, back_y, leo):
    """Standing frame (front = -Y, left = +X). Upper arms close to the trunk, wrists on the front of the abdomen:
    the right forearm low (over the pelvis), the left one higher (upper abdomen) so on C-02 the splinted forearm is
    on top and nothing covers the wristband."""
    navel = head(rig, "spine_02").z if "spine_02" in rig.pose.bones else head(rig, "spine_01").z + .10
    for s, sx, wx, wz in (("r", -1, .05, navel - .01), ("l", 1, -.06, navel + .11)):
        S = head(rig, "upperarm_" + s)
        L1 = (tail(rig, "upperarm_" + s) - S).length; L2 = (tail(rig, "lowerarm_" + s) - head(rig, "lowerarm_" + s)).length
        fy = front_y(pts, wx, wz); fy = fy if fy is not None else back_y - .25
        W = Vector((wx, fy - .030 - (.010 if s == "l" else 0), wz))
        E = two_bone(S, W, L1, L2, Vector((sx * 1.0, .45, -.2)))
        aim(rig.pose.bones["upperarm_" + s], E - S)
        aim(rig.pose.bones["lowerarm_" + s], W - head(rig, "lowerarm_" + s))
        # Palm toward the body (+Y) first: roll the forearm about its own axis; then bend the hand onto the abdomen.
        for it in range(6):
            n = palm_normal(rig, s); axis = (head(rig, "hand_" + s) - head(rig, "lowerarm_" + s)).normalized()
            tgt = Vector((0, 1, 0)); tgt -= axis * tgt.dot(axis); tgt.normalize()
            n2 = n - axis * n.dot(axis); n2.normalize()
            ang = n2.angle(tgt); sgn = 1 if n2.cross(tgt).dot(axis) > 0 else -1
            if abs(ang) < .02: break
            rotate_about(rig.pose.bones["lowerarm_" + s], Quaternion(axis, sgn * ang))
        fa = (head(rig, "hand_" + s) - head(rig, "lowerarm_" + s)).normalized()
        aim(rig.pose.bones["hand_" + s], fa + Vector((0, .30, -.08)))
        palm = palm_normal(rig, s); var = CB["HAND_VAR"][s]
        for f, a in (("index", (6, 12, 6)), ("middle", (7, 14, 7)), ("ring", (9, 17, 8)), ("pinky", (11, 20, 9))):
            for k, deg in zip(("01", "02", "03"), a):
                if f"{f}_{k}_{s}" in rig.pose.bones: flex(rig, f"{f}_{k}_{s}", deg + var[f][int(k) - 1], palm)
        for k, deg in (("02", 6), ("03", 8)): flex(rig, f"thumb_{k}_{s}", deg + var["thumb"], palm)
        CB["close_fingers"](rig, s, palm)
        # Legs straight and close (the pan is narrow), feet relaxed and slightly turned out.
        hp = head(rig, "thigh_" + s); Lt = (tail(rig, "thigh_" + s) - hp).length
        kn = Vector((sx * .095, back_y - .070, 0)); kn.z = hp.z - math.sqrt(max(Lt**2 - (kn.x - hp.x)**2 - (kn.y - hp.y)**2, 0))
        aim(rig.pose.bones["thigh_" + s], kn - hp)
        k = head(rig, "calf_" + s); Lc = (tail(rig, "calf_" + s) - k).length
        an = Vector((sx * .085, back_y - .060, 0)); an.z = k.z - math.sqrt(max(Lc**2 - (an.x - k.x)**2 - (an.y - k.y)**2, 0))
        aim(rig.pose.bones["calf_" + s], an - k)
        aim(rig.pose.bones["foot_" + s], Vector((sx * .20, -.74, -.62)))

# Bones whose vertices are "arm" for contact and for the sheet's collider.
def arm_bones(s):
    return ["upperarm_" + s, "lowerarm_" + s, "hand_" + s] + [f"{f}_{k}_{s}" for f in ("thumb", "index", "middle", "ring", "pinky") for k in ("01", "02", "03")]

def group_sets(ob, names, thr=.5):
    ids = {ob.vertex_groups[n].index for n in names if n in ob.vertex_groups}
    return {v.index for v in ob.data.vertices if sum(g.weight for g in v.groups if g.group in ids) > thr}

# Lying frame: standing Z -> -Y (head at the door), standing -Y (front) -> +Z, standing +X (left) -> -X (north).
R = Matrix(((-1, 0, 0), (0, 0, -1), (0, -1, 0))).to_4x4()

def build_body(pos):
    from bl_ext.blender_org.mpfb.services.humanservice import HumanService
    p = PEOPLE[pos]; tag = p["nome"]
    for n in ("Humano_" + tag, "Humano_" + tag + ".rig", "Corpo_" + tag):
        if n in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
    h = HumanService.create_human(macro_detail_dict=p["macro"], scale=0.1); h.name = "Humano_" + tag
    rig = HumanService.add_builtin_rig(h, "game_engine"); rig.name = "Humano_" + tag + ".rig"
    if p["skin"] and os.path.exists(p["skin"]):
        HumanService.set_character_skin(p["skin"], h, skin_type="ENHANCED_SSS", material_instances=False)
    deps = bpy.context.evaluated_depsgraph_get(); ev = h.evaluated_get(deps); me = ev.to_mesh()
    pts = [h.matrix_world @ v.co for v in me.vertices]; ev.to_mesh_clear()
    back_y = max(q.y for q in pts if abs(q.x) < .17 and .85 < q.z < 1.45) - .006
    height = max(q.z for q in pts) - min(q.z for q in pts)
    bpy.context.view_layer.objects.active = rig; bpy.ops.object.mode_set(mode="POSE")
    pose_tray(rig, pts, back_y, pos == "C-02")
    bpy.ops.object.mode_set(mode="OBJECT")
    tray = load_tray(); floor = floor_fn([tray])
    rig.matrix_world = R; upd()
    deps = bpy.context.evaluated_depsgraph_get(); ev = h.evaluated_get(deps); me = ev.to_mesh()
    pts = [h.matrix_world @ v.co for v in me.vertices]; ev.to_mesh_clear()
    crown = min(q.y for q in pts)
    trunk = [q for q in pts if crown + .45 < q.y < crown + .95 and abs(q.x) < .14]
    pan_z = floor(0, .8, .3)
    rig.matrix_world = Matrix.Translation((0, .215 - crown, (pan_z - .009) - min(q.z for q in trunk))) @ R; upd()
    deps = bpy.context.evaluated_depsgraph_get(); ev = h.evaluated_get(deps)
    mesh = bpy.data.meshes.new_from_object(ev, depsgraph=deps, preserve_all_data_layers=True)
    ob = bpy.data.objects.new("Corpo_" + tag, mesh); bpy.context.scene.collection.objects.link(ob)
    ob.matrix_world = h.matrix_world.copy()
    for vg in h.vertex_groups:
        if vg.name not in ob.vertex_groups: ob.vertex_groups.new(name=vg.name)
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True); ob.select_set(False)
    arms = group_sets(ob, arm_bones("l") + arm_bones("r"))
    fore = group_sets(ob, arm_bones("l")[1:] + arm_bones("r")[1:], .35)
    # Weight on the pan (arms excluded: they rest on the abdomen), then the forearms and hands on the trunk.
    keys = {tuple(round(c, 5) for c in ob.data.vertices[i].co) for i in arms}
    n = settle(ob, floor, skip=keys)
    trunk_tree = tree_of(ob, exclude=arms)
    def on_trunk(x, y, z):
        hit = trunk_tree.ray_cast(Vector((x, y, z + .02)), Vector((0, 0, -1)), .4)
        return hit[0].z if hit[0] is not None else None
    lifted = settle_set(ob, on_trunk, fore, gap=.0015, soft=.006)
    for poly in ob.data.polygons: poly.use_smooth = True
    for o in (h, rig): o.hide_set(True); o.hide_render = True
    ws = [v.co for v in ob.data.vertices]
    rep = {"altura_m": round(height, 3), "coroa_y": round(min(q.y for q in ws), 3), "pes_y": round(max(q.y for q in ws), 3),
           "largura": [round(min(q.x for q in ws), 3), round(max(q.x for q in ws), 3)], "topo_z": round(max(q.z for q in ws), 3),
           "contato_bandeja": n, "antebracos_assentados": lifted, "abaixo_da_bandeja": below(ob, floor),
           "vertices_braco": len(arms), "vertices_antebraco_mao": len(fore)}
    for s in ("l", "r"):
        rep["punho_" + s] = [round(c, 3) for c in (rig.matrix_world @ rig.pose.bones["hand_" + s].head)]
        rep["cotovelo_" + s] = [round(c, 3) for c in (rig.matrix_world @ rig.pose.bones["lowerarm_" + s].head)]
    ob["rep"] = json.dumps(rep)
    return ob, rig, rep

def tree_of(ob, exclude=frozenset()):
    vs = [v.co.copy() for v in ob.data.vertices]
    polys = [list(p.vertices) for p in ob.data.polygons if not any(i in exclude for i in p.vertices)]
    return BVHTree.FromPolygons(vs, polys)

def lift_forearm(body, rig, floor, idx, gap=.002):
    """Forearm and hand go up as one piece until they rest on the sheet (90th percentile of the needed lift), with a
    falloff over the first 5 cm past the elbow so the upper arm joins smoothly."""
    e, w, ax, L = forearm_frame(rig, "l")
    need = []
    for i in idx:
        c = body.data.vertices[i].co; f = floor(c.x, c.y, c.z + .10)
        if f is not None: need.append(max(0.0, f + gap - c.z))
    if not need: return 0
    need.sort(); lift = need[int(.9 * (len(need) - 1))]
    for i in idx:
        c = body.data.vertices[i].co; t = (c - e).dot(ax)
        c.z += lift * max(0.0, min(1.0, (t - .015) / .035))
    return int(lift * 1000)

def settle_set(ob, floor, idx, gap=.0015, soft=.006):
    n = 0
    for i in idx:
        v = ob.data.vertices[i]; w = v.co
        f = floor(w.x, w.y, w.z + .06)
        if f is None: continue
        f += gap; d = w.z - f
        if d < .03:
            w.z = f + (d + math.sqrt(d * d + soft * soft)) / 2 - soft / 2 * math.exp(-abs(d) / soft); n += 1
    return n

def below(ob, floor):
    c = 0
    for v in ob.data.vertices:
        f = floor(v.co.x, v.co.y, v.co.z + .06)
        if f is not None and v.co.z < f - .0005: c += 1
    return c

# ---------------------------------------------------------------- cloth
def cloth(name, colliders, x0, x1, y0, y1, z, step=.012, bend=2.5, frames=120, mass=.32):
    if name in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)
    for o in colliders:
        o.modifiers.get("Colisao") or o.modifiers.new("Colisao", "COLLISION")
        o.collision.thickness_outer = .0025; o.collision.cloth_friction = 20.0; o.collision.damping = .8
    nx, ny = int((x1 - x0) / step), int((y1 - y0) / step)
    bm = bmesh.new()
    rows = [[bm.verts.new((x0 + (x1 - x0) * i / nx, y0 + (y1 - y0) * j / ny, z)) for j in range(ny + 1)] for i in range(nx + 1)]
    for i in range(nx):
        for j in range(ny): bm.faces.new((rows[i][j], rows[i + 1][j], rows[i + 1][j + 1], rows[i][j + 1]))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    uv = me.uv_layers.new(name="UVMap")
    for loop in me.loops:     # metres, like the autopsy sheet (Lencol - corpo tiles 0.25 m)
        co = me.vertices[loop.vertex_index].co; uv.data[loop.index].uv = (co.y - y0, co.x - x0)
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    cl = ob.modifiers.new("Tecido", "CLOTH"); c = cl.settings
    c.quality = 12; c.mass = mass; c.air_damping = 3.0
    c.tension_stiffness = c.compression_stiffness = 18; c.shear_stiffness = 8; c.bending_stiffness = bend
    c.tension_damping = c.compression_damping = c.shear_damping = 5
    cs = cl.collision_settings; cs.collision_quality = 4; cs.distance_min = .0035
    cs.use_self_collision = True; cs.self_distance_min = .003; cs.self_friction = 8
    sc = bpy.context.scene; sc.frame_start = 1; sc.frame_end = frames
    cl.point_cache.frame_start = 1; cl.point_cache.frame_end = frames
    for f in range(1, frames + 1): sc.frame_set(f)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=cl.name)
    sm = ob.modifiers.new("Assentar", "CORRECTIVE_SMOOTH"); sm.factor = .25; sm.iterations = 3
    bpy.ops.object.modifier_apply(modifier=sm.name)
    smooth_hem(ob)
    for p in ob.data.polygons: p.use_smooth = True
    for o in colliders:
        if o.modifiers.get("Colisao"): o.modifiers.remove(o.modifiers["Colisao"])
    sc.frame_set(1)
    return ob

def without(ob, idx, name):
    """Copy of the body without the given vertices (the arms, for a sheet the arms rest on)."""
    c = ob.copy(); c.data = ob.data.copy(); c.name = name; bpy.context.scene.collection.objects.link(c)
    bm = bmesh.new(); bm.from_mesh(c.data); bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in idx], context="VERTS"); bm.to_mesh(c.data); bm.free()
    return c

def push_out(sheet, body, idx_arm, extra=()):
    """Sheet vertices left inside an arm (the arms were not colliders) go 2.5 mm under that arm: the sheet ends up
    tucked under the upper arms instead of passing through them. Only arm surfaces are tested."""
    vs = [v.co.copy() for v in body.data.vertices]
    polys = [list(p.vertices) for p in body.data.polygons if all(i in idx_arm for i in p.vertices)]
    for o in extra:        # splint parts: the sheet goes under them too
        base = len(vs); vs += [v.co.copy() for v in o.data.vertices]
        polys += [[base + i for i in p.vertices] for p in o.data.polygons]
    t = BVHTree.FromPolygons(vs, polys); down = Vector((0, 0, -1)); n = 0
    for v in sheet.data.vertices:
        hits, z = [], v.co.z + .25
        while True:
            h = t.ray_cast(Vector((v.co.x, v.co.y, z)), down, .6)
            if h[0] is None: break
            hits.append(h[0].z); z = h[0].z - .0003
        above = [hz for hz in hits if hz > v.co.z]
        if len(above) % 2 == 1:                     # odd number of arm surfaces above = inside the arm
            below_arm = min(hz for hz in hits if hz > v.co.z - .12)
            v.co.z = below_arm - .0025; n += 1
    return n

# ---------------------------------------------------------------- C-02 details: splint and wristband 083
def forearm_frame(rig, s):
    e = rig.matrix_world @ rig.pose.bones["lowerarm_" + s].head
    w = rig.matrix_world @ rig.pose.bones["hand_" + s].head
    return e, w, (w - e).normalized(), (w - e).length

def ellipse_rings(ob, idx, origin, axis, t0, t1, step=.012):
    """Per station along the forearm: centre and semi-axes (up / side) of the skin section, from the 95th percentile
    of the points' extent (robust to stray hand or elbow vertices), smoothed between stations."""
    up = Vector((0, 0, 1)); u = (up - axis * up.dot(axis)).normalized(); v = axis.cross(u).normalized()
    pts = [ob.data.vertices[i].co for i in idx]
    n = max(3, int((t1 - t0) / step)); st = []
    for k in range(n + 1):
        t = t0 + (t1 - t0) * k / n
        sec = [p for p in pts if abs((p - origin).dot(axis) - t) < .008]
        if len(sec) < 8: st.append(None); continue
        c = sum(sec, Vector()) / len(sec); q0 = c - origin; c = origin + axis * t + (q0 - axis * q0.dot(axis))
        du = sorted(abs((p - c).dot(u)) for p in sec); dv = sorted(abs((p - c).dot(v)) for p in sec)
        st.append([t, c, du[int(.95 * (len(du) - 1))], dv[int(.95 * (len(dv) - 1))]])
    good = [x for x in st if x]
    st = [x if x else min(good, key=lambda g: abs(g[0] - (t0 + (t1 - t0) * k / n))) for k, x in enumerate(st)]
    for _ in range(2):
        st = [[st[k][0], (st[max(k - 1, 0)][1] + 2 * st[k][1] + st[min(k + 1, n)][1]) / 4,
               (st[max(k - 1, 0)][2] + 2 * st[k][2] + st[min(k + 1, n)][2]) / 4,
               (st[max(k - 1, 0)][3] + 2 * st[k][3] + st[min(k + 1, n)][3]) / 4] for k in range(n + 1)]
    return st, u, v

def tube(name, rings, u, v, off, segs=32, caps=True):
    bm = bmesh.new(); grid = []
    for t, c, a, b in rings:
        grid.append([bm.verts.new(c + u * math.cos(2 * math.pi * i / segs) * (a + off) + v * math.sin(2 * math.pi * i / segs) * (b + off))
                     for i in range(segs)])
    for k in range(len(grid) - 1):
        for i in range(segs):
            bm.faces.new((grid[k][i], grid[k][(i + 1) % segs], grid[k + 1][(i + 1) % segs], grid[k + 1][i]))
    if caps:
        bm.faces.new(grid[0][::-1]); bm.faces.new(grid[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    for p in o.data.polygons: p.use_smooth = True
    return o


def leo_details(body, rig):
    """Splint: crepe bandage over the forearm from below the elbow to above the wrist, a padded aluminium strip on
    the back of the forearm (rigid splint), two hook-and-loop straps. Wristband 083 just past the bandage."""
    for n in ("Tala", "TalaPlaca", "TalaFaixas", "Pulseira083"):
        if n in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
    fore = group_sets(body, ["lowerarm_l"] + (["lowerarm_twist_01_l"] if "lowerarm_twist_01_l" in body.vertex_groups else []), .35)
    e, w, ax, L = forearm_frame(rig, "l")
    mats = {"atadura": simple_material("Atadura crepe", (.86, .82, .74), .9), "placa": simple_material("Tala aluminio acolchoada", (.30, .42, .55), .55),
            "velcro": simple_material("Velcro", (.10, .11, .12), .8), "pulseira": simple_material("Pulseira vinil", (.93, .93, .92), .35)}
    rings, u, v = ellipse_rings(body, fore, e, ax, .07, L - .05)
    band = tube("Tala", rings, u, v, .006); set_mat(band, mats["atadura"])
    # Rigid strip on the upper side (it faces up with the forearm on the abdomen), over the bandage.
    bm = bmesh.new(); rows = []
    for t, c, a, b in rings[1:-1]:
        top = c + u * (a + .006)
        rows.append([bm.verts.new(top + v * sg * .021 + u * dz) for sg in (-1, 1) for dz in (.0006, .0056)])
    for k in range(len(rows) - 1):
        A, B = rows[k], rows[k + 1]
        for f in ((A[0], A[2], B[2], B[0]), (A[1], B[1], B[3], A[3]), (A[0], B[0], B[1], A[1]), (A[2], A[3], B[3], B[2])): bm.faces.new(f)
    bm.faces.new((rows[0][0], rows[0][1], rows[0][3], rows[0][2])); bm.faces.new((rows[-1][0], rows[-1][2], rows[-1][3], rows[-1][1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("TalaPlaca"); bm.to_mesh(me); bm.free()
    plate = bpy.data.objects.new("TalaPlaca", me); bpy.context.scene.collection.objects.link(plate); set_mat(plate, mats["placa"])
    bv = plate.modifiers.new("Bordas", "BEVEL"); bv.width = .0012; bv.segments = 2
    bpy.context.view_layer.objects.active = plate; bpy.ops.object.modifier_apply(modifier=bv.name)
    # Straps: 25 mm hook-and-loop bands around bandage and strip.
    parts = []
    for k in (2, len(rings) - 3):
        sub = [[rings[k][0] + d, rings[k][1] + ax * d, rings[k][2], rings[k][3]] for d in (-.0125, .0125)]
        st_ = tube("Faixa", sub, u, v, .0068); parts.append(st_)
        # over the strip: the strap rises over it (strip 5.6 mm + bandage)
        for vv in st_.data.vertices:
            q = vv.co - (e + ax * (vv.co - e).dot(ax))
            if q.normalized().dot(u) > .55: vv.co += u * .0058 * min(1, (q.normalized().dot(u) - .55) / .25)
    for o in parts: set_mat(o, mats["velcro"])
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]; bpy.ops.object.join(); parts[0].name = "TalaFaixas"
    # Wristband: 22 mm vinyl band around the wrist, 4 mm clear of the skin, label up (UV u = 0.5 on top).
    wr = group_sets(body, ["lowerarm_l", "hand_l"] + (["lowerarm_twist_01_l"] if "lowerarm_twist_01_l" in body.vertex_groups else []), .2)
    wrings, _, _ = ellipse_rings(body, wr, e, ax, L - .032, L - .010, step=.011)
    pb = tube("Pulseira083", wrings, u, v, .004, segs=48, caps=False)
    bm = bmesh.new(); bm.from_mesh(pb.data); uvl = bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        for lp in f.loops:
            q = lp.vert.co - (e + ax * (lp.vert.co - e).dot(ax)); ang = math.atan2(q.dot(v), q.dot(u))
            lp[uvl].uv = (((ang + math.pi) / (2 * math.pi)) % 1.0, ((lp.vert.co - e).dot(ax) - (L - .032)) / .022)
    for f in bm.faces:
        us = [lp[uvl].uv.x for lp in f.loops]
        if max(us) - min(us) > .5:
            for lp in f.loops:
                if lp[uvl].uv.x < .5: lp[uvl].uv.x += 1.0
    bm.to_mesh(pb.data); bm.free()
    so = pb.modifiers.new("Espessura", "SOLIDIFY"); so.thickness = .0012; so.offset = 1
    bpy.context.view_layer.objects.active = pb; bpy.ops.object.modifier_apply(modifier=so.name)
    set_mat(pb, mats["pulseira"])
    return band, plate, parts[0], pb

# ---------------------------------------------------------------- reduction (C-02 body)
def player_eyes():
    """Player eye positions in front of the open compartment, expressed in the tray's frame with the tray at its
    first stop (the tray moved -Y by STAGE1, so the eyes are shifted +Y by the same amount)."""
    eyes = []
    for z in (-.35, .10, .55, .70):           # crouched to standing, relative to the top rail (upper row at 1.05 m)
        for x in (-.9, -.45, 0, .45, .9):
            for y in (-.35, -.8, -1.4):
                eyes.append(Vector((x, y + STAGE1, z)))
    return eyes

def visible(ob, occluders, eyes, margin=.035):
    deps = bpy.context.evaluated_depsgraph_get(); occ = [BVHTree.FromObject(o, deps) for o in occluders]
    vs = [v.co.copy() for v in ob.data.vertices]; nrm = [v.normal.copy() for v in ob.data.vertices]
    seen = set()
    for i, p in enumerate(vs):
        o = p + nrm[i] * .002
        for e in eyes:
            d = e - o; L = d.length; d /= L
            if d.dot(nrm[i]) < -.05: continue
            # Inside the cabinet (beyond the opening plane) only through the opening.
            if o.y > STAGE1:
                t = (STAGE1 - o.y) / d.y if abs(d.y) > 1e-6 else -1
                if t <= 0: continue
                hp = o + d * t
                if not (OPENING[0] < hp.x < OPENING[1] and OPENING[2] < hp.z < OPENING[3]): continue
            if all(tr.ray_cast(o, d, L)[0] is None for tr in occ): seen.add(i); break
    kd = KDTree(max(len(seen), 1)); [kd.insert(vs[i], i) for i in seen]; kd.balance()
    keep = {i for i, p in enumerate(vs) if i in seen or (seen and kd.find(p)[2] < margin)}
    return keep, len(seen)

def reduce_body(body, occluders, keep_always):
    ob = body.copy(); ob.data = body.data.copy(); ob.name = body.name + "_Jogo"; bpy.context.scene.collection.objects.link(ob)
    keep, nseen = visible(ob, occluders + [body], player_eyes())
    keep |= keep_always
    vg = ob.vertex_groups.new(name="Reduzir"); vg.add([i for i in range(len(ob.data.vertices)) if i not in keep], 1.0, "REPLACE")
    full = tris(ob)
    dp = ob.modifiers.new("Recuar", "DISPLACE"); dp.direction = "NORMAL"; dp.strength = -.005; dp.mid_level = 0; dp.vertex_group = "Reduzir"
    bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier=dp.name)
    hid = set(range(len(ob.data.vertices))) - keep
    hid_tris = sum(len(p.vertices) - 2 for p in ob.data.polygons if all(i in hid for i in p.vertices))
    dm = ob.modifiers.new("Reduzir", "DECIMATE"); dm.decimate_type = "COLLAPSE"; dm.ratio = (full - hid_tris * .8) / full
    dm.vertex_group = "Reduzir"; dm.vertex_group_factor = 1.0; dm.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=dm.name)
    sw = ob.modifiers.new("Dentro", "SHRINKWRAP"); sw.target = body; sw.wrap_method = "NEAREST_SURFACEPOINT"
    sw.wrap_mode = "INSIDE"; sw.offset = .004; sw.vertex_group = "Reduzir"; bpy.ops.object.modifier_apply(modifier=sw.name)
    ob.vertex_groups.clear()
    transfer_normals(ob, body)
    return ob, {"vistos": nseen, "mantidos": len(keep), "tris_fonte": full, "tris_jogo": tris(ob)}

# ---------------------------------------------------------------- per compartment
def occupant(pos):
    tray = load_tray()
    body, rig, rep = build_body(pos)
    print("CORPO", pos, json.dumps(rep), flush=True)
    tag = PEOPLE[pos]["nome"]
    arms = group_sets(body, arm_bones("l") + arm_bones("r"))
    if pos != "C-02":
        # Full cover: head to beyond the feet.
        sh = cloth("Lencol_" + tag, [body, tray], -.42, .42, -.25, 2.45, rep["topo_z"] + .10, bend=2.5)
        out = {"lencol": sh}
    else:
        # One sheet from the head to the feet; its left (north, -X) hem lands on the left flank, so the left arm
        # stays out: during the drape that arm is moved under the tray (no hole at the shoulder), then the forearm
        # rests on the sheet and the sheet is tucked under the upper arm.
        left = group_sets(body, arm_bones("l"))
        core = without(body, left, "Corpo_sem_braco")
        # Symmetric drape (an asymmetric strip slides toward its overhang), then the left part is cut back to the
        # flank: the edge reads as the sheet folded back beside the trunk.
        sh = cloth("Lencol_" + tag, [core, tray], -.42, .42, -.25, 2.45, rep["topo_z"] + .10, bend=2.5)
        bpy.data.objects.remove(core, do_unlink=True)
        rep["tecido_aparado"] = trim(sh, left=-.15)
        fore = group_sets(body, arm_bones("l")[1:], .35)
        st = BVHTree.FromObject(sh, bpy.context.evaluated_depsgraph_get())
        def on_sheet(x, y, z):
            hit = st.ray_cast(Vector((x, y, z + .03)), Vector((0, 0, -1)), .2)
            return hit[0].z if hit[0] is not None else None
        rested = lift_forearm(body, rig, on_sheet, fore)
        rested += settle_set(body, on_sheet, fore, gap=.0012, soft=.004)
        out = {"lencol": sh}
        out.update(dict(zip(("tala", "placa", "faixas", "pulseira"), leo_details(body, rig))))
        pushed = push_out(sh, body, left, extra=[out["tala"], out["placa"], out["faixas"]])
        rep.update({"lencol_empurrado": pushed, "antebraco_no_lencol": rested})
    if pos != "C-02": rep["tecido_aparado"] = trim(out["lencol"])
    rep.update(check(pos, body, out, tray))
    json.dump(rep, open(OUT + f"/{pos}.json", "w"), indent=2)
    print("OCUPANTE", pos, json.dumps(rep), flush=True)
    return body, out

def trim(sheet, half=.30, left=-.30):
    """What would hang past the tray frame (and reach the telescopic rails or the door when the tray goes in) is
    cut; the new edge lies on the frame and gets the same hem relaxation as the free edges."""
    bm = bmesh.new(); bm.from_mesh(sheet.data)
    # Across: past the frame tube. Along: past the handles (the door closes 6 cm in front of the tray).
    def left_edge(y):      # C-02: folded back beside the trunk only (shoulder to hip), full cover elsewhere
        if left <= -half: return -half
        a, b, r = .30, 1.05, .12
        if y < a - r or y > b + r: return -half
        k = 1.0 if a <= y <= b else 1 - min(abs(y - a), abs(y - b)) / r
        k = k * k * (3 - 2 * k)
        return -half + (left + half) * k
    # Down: corners hanging under the frame would cut the sill / compartment floor with the tray closed.
    cut = [v for v in bm.verts if v.co.x > half or v.co.x < left_edge(v.co.y) or v.co.y < .075 or v.co.y > 2.06 or v.co.z < -.03]
    n = len(cut)
    bmesh.ops.delete(bm, geom=cut, context="VERTS"); bm.to_mesh(sheet.data); bm.free()
    smooth_hem(sheet, rounds=12)
    return n

def check(pos, body, out, tray):
    """Envelope inside the compartment (rails at |x| 0.33 below z 0, opening 0.55 high), sheet inside the body,
    head covered, C-02 story items exposed (nothing above them) and inside the stretch that comes out."""
    deps = bpy.context.evaluated_depsgraph_get(); rep = {}
    allv = [v.co for o in [body] + list(out.values()) for v in o.data.vertices]
    clothv = [v.co for v in out["lencol"].data.vertices]
    rep["envelope_x"] = [round(min(p.x for p in allv), 3), round(max(p.x for p in allv), 3)]
    rep["envelope_z_max"] = round(max(p.z for p in allv), 3)
    rep["envelope_z_min"] = round(min(p.z for p in allv), 3)
    rep["tecido_fora_trilhos"] = sum(1 for p in clothv if abs(p.x) > .312 and p.z < .03)
    rep["tecido_y"] = [round(min(p.y for p in clothv), 3), round(max(p.y for p in clothv), 3)]
    tb = BVHTree.FromObject(body, deps)
    inside = 0
    for p in clothv:
        hit = tb.find_nearest(p)
        if hit[0] is not None and (p - hit[0]).dot(hit[1]) < -.0015 and hit[3] < .03: inside += 1
    rep["tecido_dentro_do_corpo"] = inside
    trees = [BVHTree.FromObject(out["lencol"], deps)]
    up = Vector((0, 0, 1))
    def free(p): return all(t.ray_cast(p + up * .002, up, 1.0)[0] is None for t in trees)
    cy = min(v.co.y for v in body.data.vertices)
    head = [v.co for v in body.data.vertices if v.co.y < cy + .24]
    rep["cabeca_coberta"] = round(1 - sum(1 for p in head if free(p)) / max(len(head), 1), 3)
    if pos == "C-02":
        pul = out["pulseira"]; ys = [v.co.y for v in pul.data.vertices]
        top = [v.co for v in pul.data.vertices if v.co.z > max(q.z for q in [w.co for w in pul.data.vertices]) - .008]
        rep["pulseira_y"] = [round(min(ys), 3), round(max(ys), 3)]
        rep["pulseira_topo_livre"] = round(sum(1 for p in top if free(p)) / max(len(top), 1), 3)
        tl = out["tala"]; rep["tala_y"] = [round(min(v.co.y for v in tl.data.vertices), 3), round(max(v.co.y for v in tl.data.vertices), 3)]
        tt = [v.co for v in tl.data.vertices if v.co.z > max(w.co.z for w in tl.data.vertices) - .01]
        rep["tala_topo_livre"] = round(sum(1 for p in tt if free(p)) / max(len(tt), 1), 3)
        rep["fora_com_bandeja_1m"] = max(ys) < STAGE1 - .05
    return rep

# ---------------------------------------------------------------- review render (neutral studio, simple materials)
def render(pos, body, out):
    sc = bpy.context.scene
    try: sc.render.engine = "CYCLES"
    except TypeError: pass
    sc.cycles.samples = 64; sc.render.resolution_x, sc.render.resolution_y = 1400, 900
    world = bpy.data.worlds.get("Estudio") or bpy.data.worlds.new("Estudio"); world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (.5, .5, .52, 1); world.node_tree.nodes["Background"].inputs[1].default_value = .6
    sc.world = world
    if "Sol" not in bpy.data.objects:
        l = bpy.data.objects.new("Sol", bpy.data.lights.new("Sol", "AREA")); l.data.energy = 600; l.data.size = 2.5
        l.location = (0, .8, 2.6); sc.collection.objects.link(l)
    for o in bpy.data.objects:
        if o.type == "MESH": o.hide_render = True
    load_tray().hide_render = False; set_mat(load_tray(), simple_material("Inox simples", (.7, .7, .7), .35))
    body.hide_render = pos != "C-02"
    if pos == "C-02": set_mat(body, simple_material("Pele simples", (.45, .32, .25), .5))
    linen = simple_material("Lencol simples", (.80, .80, .78), .85)
    for k, o in out.items():
        o.hide_render = False
        if k == "lencol": set_mat(o, linen)
    cam = bpy.data.objects.get("Cam") or bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    if cam.name not in sc.collection.objects: sc.collection.objects.link(cam)
    sc.camera = cam; cam.data.lens = 35
    views = [("geral", (1.1, -.6, 1.2), (0, .9, .1)), ("lado", (-1.3, .9, .6), (0, .9, .1)), ("frente", (0, -1.0, .7), (0, .5, .05))]
    if pos == "C-02": views += [("tala", (-.45, .25, .55), (-.02, .60, .16)), ("pulseira", (.25, .45, .45), (-.03, .72, .16))]
    os.makedirs(OUT, exist_ok=True)
    for n, loc, tgt in views:
        cam.location = loc; cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = f"{OUT}/{pos}_{n}.png"; bpy.ops.render.render(write_still=True)

# ---------------------------------------------------------------- export
def export(pos, body, out):
    ns = {"__name__": "rexp", "__file__": ROOT + "/Blender/Recepcao/scripts/recepcao_modelo.py"}
    exec(compile(open(ns["__file__"]).read(), ns["__file__"], "exec"), ns)
    os.makedirs(ASSETS + "/Texturas", exist_ok=True)
    tag = PEOPLE[pos]["nome"]; key = "Ocupante" + pos.replace("-", "")
    jobs = []
    sj = out["lencol"].copy(); sj.data = out["lencol"].data.copy(); sj.name = key + "_Lencol"; bpy.context.scene.collection.objects.link(sj)
    reduce_sheet(sj); donor = smooth_normals_source(out["lencol"]); transfer_normals(sj, donor); bpy.data.objects.remove(donor, do_unlink=True)
    jobs.append(sj)
    stats = {}
    if pos == "C-02":
        fore = group_sets(body, arm_bones("l") + arm_bones("r"), .2)
        cj, stats = reduce_body(body, [out["lencol"], load_tray()], fore)
        cj.name = key + "_Corpo"; jobs.append(cj)
        acc = [out[k] for k in ("tala", "placa", "faixas")]
        cps = []
        for o in acc:
            c = o.copy(); c.data = o.data.copy(); bpy.context.scene.collection.objects.link(c); cps.append(c)
        bpy.ops.object.select_all(action="DESELECT")
        for c in cps: c.select_set(True)
        bpy.context.view_layer.objects.active = cps[0]; bpy.ops.object.join(); cps[0].name = key + "_Tala"; jobs.append(cps[0])
        pu = out["pulseira"].copy(); pu.data = out["pulseira"].data.copy(); pu.name = key + "_Pulseira"; bpy.context.scene.collection.objects.link(pu); jobs.append(pu)
        import shutil
        d = os.path.dirname(PEOPLE[pos]["skin"])
        for a, b in (("skin_male_african_middleage.png", key + "_Pele_BaseMap.png"), ("skin_male_african_middleage_NRM.png", key + "_Pele_Normal.png")):
            shutil.copyfile(d + "/" + a, ASSETS + "/Texturas/" + b)
    for o in jobs:
        ns["export_fbx"](o, ASSETS + "/" + o.name + ".fbx", keep_slots=o.name.endswith("_Tala"))
        stats[o.name] = tris(o); bpy.data.objects.remove(o, do_unlink=True)
    print("EXPORT", pos, json.dumps(stats), flush=True)
    json.dump(stats, open(OUT + f"/{pos}_export.json", "w"), indent=2)

def main():
    os.makedirs(OUT, exist_ok=True)
    if os.path.exists(BLEND) and not any(a.startswith("C-") for a in ARGS): bpy.ops.wm.open_mainfile(filepath=BLEND)
    else:
        for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for pos in [a for a in ARGS if a.startswith("C-")]:
        body, out = occupant(pos)
        if "render" in ARGS: render(pos, body, out)
        if "exportar" in ARGS: export(pos, body, out)
        bpy.ops.wm.save_as_mainfile(filepath=BLEND)

if __name__ == "__main__": main()

"""Autopsy table body from an MPFB 2 (MakeHuman) base: adult male, game_engine rig, posed supine on the table.
Run: blender -b --threads 8 --python Blender/Autopsia/scripts/corpo_base.py [-- etapa ...]
  etapas: base (create + pose + lay down + contact), render (neutral studio, simple material), pele
Needs the MPFB extension (extensions.blender.org, 2.0.18). Assets used: MPFB core base mesh + rig (CC0).
Same frame as corpo.py (table origin, head at -X, right side -Y). The earlier scripted pilot (corpo.py,
Corpo.blend) is kept untouched as a reference; this script writes CorpoBase.blend."""
import os, sys, math, json
import bpy
from mathutils import Vector, Matrix, Quaternion
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
P = {"__name__": "corpo_lib", "__file__": HERE + "/corpo.py"}
exec(compile(open(P["__file__"]).read(), P["__file__"], "exec"), P)
ROOT, SRC, load_table, simple_material, set_mat = P["ROOT"], P["SRC"], P["load_table"], P["simple_material"], P["set_mat"]
STEPS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["base", "render"]
BLEND = SRC + "/CorpoBase.blend"
OUT = SRC + "/revisao/corpo_base"
MPFB_USER = os.path.expanduser("~/.config/blender/5.2/extensions/.user/blender_org/mpfb/data")
SKIN = MPFB_USER + "/skins/mindfront_aksel_skin/mindfront_aksel_skin.mhmat"

MACRO = {"gender": 1.0, "age": 0.55, "muscle": 0.5, "weight": 0.5, "proportions": 0.5, "height": 0.52,
         "cupsize": 0.5, "firmness": 0.5, "race": {"asian": 0.2, "caucasian": 0.45, "african": 0.35}}

# ---------------------------------------------------------------- pose helpers (armature space, standing frame)
def upd(): bpy.context.view_layer.update()

def rotate_about(pb, q, pivot=None):
    m = pb.matrix.copy(); c = pivot if pivot is not None else m.translation.copy()
    pb.matrix = Matrix.Translation(c) @ q.to_matrix().to_4x4() @ Matrix.Translation(-c) @ m; upd()

def aim(pb, direction):
    y = pb.matrix.col[1].xyz.normalized()
    rotate_about(pb, y.rotation_difference(Vector(direction).normalized()))

def head(rig, n): return rig.pose.bones[n].head.copy()
def tail(rig, n): return rig.pose.bones[n].tail.copy()

def flex(rig, n, deg, palm):
    """Bend a finger bone toward the palm side (sign chosen from where the tip moves)."""
    pb = rig.pose.bones[n]; ax = pb.matrix.col[0].xyz.normalized(); t0 = pb.tail.copy()
    rotate_about(pb, Quaternion(ax, math.radians(deg)))
    if (pb.tail - t0).dot(palm) < 0: rotate_about(pb, Quaternion(ax, -2 * math.radians(deg)))

def palm_normal(rig, s):
    """Palm side of the hand = where the middle finger tip goes when it flexes."""
    pb = rig.pose.bones["middle_02_" + s]; ax = pb.matrix.col[0].xyz.normalized()
    t0 = pb.tail.copy(); rotate_about(pb, Quaternion(ax, .3)); t1 = pb.tail.copy()
    rotate_about(pb, Quaternion(ax, -.3))
    hand = (tail(rig, "hand_" + s) - head(rig, "hand_" + s)).normalized()
    d = t1 - t0; d -= hand * d.dot(hand); return d.normalized()

# Resting spread: the MakeHuman rest hand is evenly fanned. Each finger is brought toward the middle finger by
# its own fraction (a relaxed hand is closer and slightly uneven), different on each hand.
SPREAD = {"l": {"index": .55, "ring": .45, "pinky": .35}, "r": {"index": .45, "ring": .55, "pinky": .40}}
HAND_VAR = {"l": {"index": (0, 2, 1), "middle": (1, 0, 2), "ring": (-1, 3, 0), "pinky": (2, -2, 1), "thumb": 3},
            "r": {"index": (2, -1, 0), "middle": (0, 3, 1), "ring": (2, 0, 2), "pinky": (-1, 4, 2), "thumb": -2}}

def close_fingers(rig, s, palm):
    mid = (tail(rig, "middle_01_" + s) - head(rig, "middle_01_" + s)); mid -= palm * mid.dot(palm); mid.normalize()
    for f, k in SPREAD[s].items():
        n = f"{f}_01_{s}"
        d = tail(rig, n) - head(rig, n); d -= palm * d.dot(palm); d.normalize()
        ang = d.angle(mid); sgn = 1 if d.cross(mid).dot(palm) > 0 else -1
        rotate_about(rig.pose.bones[n], Quaternion(palm, sgn * ang * k))

def pose(rig, back_y):
    """Supine, relaxed: arms along the body with palms on the table, slight natural finger flexion,
    legs straight with the feet falling outward. back_y = the plane the body lies on (standing frame)."""
    for s, sx in (("l", 1), ("r", -1)):
        sh = head(rig, "upperarm_" + s); L1 = (tail(rig, "upperarm_" + s) - sh).length
        L2 = (tail(rig, "lowerarm_" + s) - head(rig, "lowerarm_" + s)).length
        el = Vector((sx * .235, back_y - .050, 0)); el.z = sh.z - math.sqrt(max(L1**2 - (el.x - sh.x)**2 - (el.y - sh.y)**2, 0))
        aim(rig.pose.bones["upperarm_" + s], el - sh)
        e = head(rig, "lowerarm_" + s)
        wr = Vector((sx * .262, back_y - .034, 0)); wr.z = e.z - math.sqrt(max(L2**2 - (wr.x - e.x)**2 - (wr.y - e.y)**2, 0))
        aim(rig.pose.bones["lowerarm_" + s], wr - e)
        aim(rig.pose.bones["hand_" + s], Vector((sx * .04, .16, -1)))
        # Palm onto the table (+Y): roll the whole arm about the shoulder-wrist line (shoulder rotation plus
        # pronation, which a straight arm shares), split so the forearm takes part of the twist.
        for it in range(3):
            n = palm_normal(rig, s); axis = (head(rig, "hand_" + s) - head(rig, "upperarm_" + s)).normalized()
            tgt = Vector((0, 1, 0)); tgt -= axis * tgt.dot(axis); tgt.normalize()
            n2 = n - axis * n.dot(axis); n2.normalize()
            ang = n2.angle(tgt); sgn = 1 if n2.cross(tgt).dot(axis) > 0 else -1
            if abs(ang) < .02: break
            rotate_about(rig.pose.bones["upperarm_" + s], Quaternion(axis, sgn * ang * .55), head(rig, "upperarm_" + s))
            fa = (head(rig, "hand_" + s) - head(rig, "lowerarm_" + s)).normalized()
            rotate_about(rig.pose.bones["lowerarm_" + s], Quaternion(fa, sgn * ang * .45))
        palm = palm_normal(rig, s)
        # Small natural differences between the hands (not mirrored): per-finger curl offsets.
        var = HAND_VAR[s]
        for f, a in (("index", (4, 10, 5)), ("middle", (5, 12, 6)), ("ring", (7, 15, 7)), ("pinky", (9, 18, 8))):
            for k, deg in zip(("01", "02", "03"), a):
                if f"{f}_{k}_{s}" in rig.pose.bones: flex(rig, f"{f}_{k}_{s}", deg + var[f][int(k) - 1], palm)
        for k, deg in (("02", 6), ("03", 8)): flex(rig, f"thumb_{k}_{s}", deg + var["thumb"], palm)
        close_fingers(rig, s, palm)
        # Legs: thighs/calves resting on the table, slight abduction, feet relaxed and turned outward.
        hp = head(rig, "thigh_" + s); Lt = (tail(rig, "thigh_" + s) - hp).length
        kn = Vector((sx * .112, back_y - .070, 0)); kn.z = hp.z - math.sqrt(max(Lt**2 - (kn.x - hp.x)**2 - (kn.y - hp.y)**2, 0))
        aim(rig.pose.bones["thigh_" + s], kn - hp)
        k = head(rig, "calf_" + s); Lc = (tail(rig, "calf_" + s) - k).length
        an = Vector((sx * .118, back_y - .060, 0)); an.z = k.z - math.sqrt(max(Lc**2 - (an.x - k.x)**2 - (an.y - k.y)**2, 0))
        aim(rig.pose.bones["calf_" + s], an - k)
        leg = (head(rig, "foot_" + s) - head(rig, "thigh_" + s)).normalized()
        rotate_about(rig.pose.bones["thigh_" + s], Quaternion(leg, sx * math.radians(9)), head(rig, "thigh_" + s))
        aim(rig.pose.bones["foot_" + s], Vector((sx * .26, -.74, -.60)))


# ---------------------------------------------------------------- hands: rest on the steel, never pressed into it
FINGERS = ("thumb", "index", "middle", "ring", "pinky")

def _groups(h, names):
    ids = {h.vertex_groups[n].index for n in names if n in h.vertex_groups}
    return [v.index for v in h.data.vertices if sum(g.weight for g in v.groups if g.group in ids) > .5]

def _eval_world(h):
    mask = h.modifiers.get("Hide helpers"); mask.show_viewport = False; upd()
    deps = bpy.context.evaluated_depsgraph_get(); ev = h.evaluated_get(deps); me = ev.to_mesh()
    pts = [h.matrix_world @ v.co for v in me.vertices]; ev.to_mesh_clear(); mask.show_viewport = True
    return pts

def _clearance(h, idx, floor):
    pts = _eval_world(h); best = 1.0
    for i in idx:
        p = pts[i]; f = floor(p.x, p.y, p.z + .06)
        if f is not None: best = min(best, p.z - f)
    return best

def orient_hand(rig, s, slope_deg):
    """Hand frame from landmarks (wrist, index and little-finger knuckles, palm side) turned so the palm faces
    the steel, the knuckle line is level and the hand runs along the forearm's horizontal heading, tilted
    slope_deg down toward the fingers."""
    hb = rig.pose.bones["hand_" + s]; W = rig.matrix_world.to_3x3(); Wi = W.inverted()
    w = head(rig, "hand_" + s); i = head(rig, "index_01_" + s); p = head(rig, "pinky_01_" + s)
    f = ((i + p) / 2 - w).normalized(); n = palm_normal(rig, s); n = (n - f * n.dot(f)).normalized()
    fa = (W @ (head(rig, "hand_" + s) - head(rig, "lowerarm_" + s))); fa.z = 0; fa.normalize()
    a = math.radians(slope_deg); ft = fa * math.cos(a) + Vector((0, 0, -math.sin(a)))
    nt = Vector((0, 0, -1)); nt = (nt - ft * nt.dot(ft)).normalized()
    ft, nt = (Wi @ ft).normalized(), (Wi @ nt).normalized()
    q1 = f.rotation_difference(ft); n1 = q1 @ n
    ang = n1.angle(nt); sgn = 1 if n1.cross(nt).dot(ft) > 0 else -1
    rotate_about(hb, Quaternion(ft, sgn * ang) @ q1, w)

def place_thumb(h, rig, s, floor, idx):
    """Relaxed thumb of a palm-down hand: beside the index, ~30 deg off the hand axis, lying on its side with the tip
    level with the index's first phalanx, a few mm above the steel. Aimed by target, then a light natural flexion."""
    W = rig.matrix_world; Wi = W.inverted()
    w, i, p = (W @ head(rig, n) for n in ("hand_" + s, "index_01_" + s, "pinky_01_" + s))
    f = ((i + p) / 2 - w); f.z = 0; f.normalize(); med = (i - p); med.z = 0; med.normalize()
    base = W @ head(rig, "thumb_01_" + s)
    tip = base + f * .085 + med * .050   # ~30 deg off the hand axis, tip level with the index's first phalanx
    for lift in (.009, .012, .016, .020):
        tip.z = (floor(tip.x, tip.y, tip.z + .1) or tip.z) + lift
        t = Wi @ tip
        for n in ("thumb_01_", "thumb_02_", "thumb_03_"): aim(rig.pose.bones[n + s], t - head(rig, n + s))
        palm = palm_normal(rig, s)
        for n, deg in (("thumb_02_", 8), ("thumb_03_", 10)): flex(rig, n + s, deg, palm)
        c = _clearance(h, idx, floor)
        if c >= .0005: return round(c, 4)
    return round(c, 4)

def rest_hands(h, rig, floor):
    """Relaxed hand on the steel: palm toward the steel, knuckles level; the tilt is found by bisection until the
    lowest point of the hand just touches (no penetration). If the curl props the palm up, the touching
    fingers open a little and the tilt is solved again. Gaps left by the natural curl are kept."""
    rep = {}
    def turn(pb, axis_col, deg):
        rotate_about(pb, Quaternion(pb.matrix.col[axis_col].xyz.normalized(), math.radians(deg)))
    def lift_dir(pb, axis_col, idx):
        c0 = _clearance(h, idx, floor); turn(pb, axis_col, 1.0); c1 = _clearance(h, idx, floor); turn(pb, axis_col, -1.0)
        return (1.0 if c1 > c0 else -1.0), abs(c1 - c0)
    for s in ("l", "r"):
        fb = {f: [f"{f}_0{k}_{s}" for k in (1, 2, 3) if f"{f}_0{k}_{s}" in rig.pose.bones] for f in FINGERS}
        all_idx = _groups(h, ["hand_" + s] + sum(fb.values(), []))
        palm_idx = _groups(h, ["hand_" + s]); f_idx = {f: _groups(h, b) for f, b in fb.items()}
        # Tilt only (no finger is straightened to force contact): the relaxed curl may keep the palm a few mm up.
        nothumb = _groups(h, ["hand_" + s] + [b for f in FINGERS if f != "thumb" for b in fb[f]])
        lo, hi = -20.0, 45.0
        for _ in range(14):
            mid = (lo + hi) / 2; orient_hand(rig, s, mid)
            if _clearance(h, nothumb, floor) > .0008: lo = mid
            else: hi = mid
        orient_hand(rig, s, lo); rep["inclinacao_" + s] = round(lo, 1)
        rep["polegar_" + s] = place_thumb(h, rig, s, floor, f_idx["thumb"])
        rep["mao_" + s] = round(_clearance(h, all_idx, floor), 4)
        rep["palma_" + s] = round(_clearance(h, palm_idx, floor), 4)
        for f in FINGERS: rep[f + "_" + s] = round(_clearance(h, f_idx[f], floor), 4)
    return rep, set()

def hand_points(h):
    pts = _eval_world(h); keep = set()
    for s in ("l", "r"):
        names = ["hand_" + s] + [f"{f}_0{k}_{s}" for f in FINGERS for k in (1, 2, 3)]
        keep |= {tuple(round(c, 5) for c in pts[i]) for i in _groups(h, names)}
    return keep

# ---------------------------------------------------------------- table contact
def settle_only(ob, floor, keys, gap=.0006, soft=.0025):
    """Small pad compression for the hands (fingertips and palm flatten 1-2 mm where they touch)."""
    mw = ob.matrix_world; mi = mw.inverted()
    for v in ob.data.vertices:
        w = mw @ v.co
        if tuple(round(c, 5) for c in w) not in keys: continue
        f = floor(w.x, w.y, w.z + .06)
        if f is None: continue
        f += gap; d = w.z - f
        if d < .01:
            w.z = f + (d + math.sqrt(d * d + soft * soft)) / 2 - soft / 2 * math.exp(-abs(d) / soft); v.co = mi @ w

def mesa_floor_fn(table):
    deps = bpy.context.evaluated_depsgraph_get(); t = BVHTree.FromObject(table, deps)
    mw = table.matrix_world
    def floor(x, y, z):
        hit = t.ray_cast(Vector((x, y, z + .02)), Vector((0, 0, -1)), .5)
        return (mw @ hit[0]).z if hit[0] is not None else None
    return floor

def settle(ob, floor, gap=.0012, soft=.010, skip=frozenset()):
    """Soft contact: weight flattens what touches (back, buttocks, calves, heels, arms, head on the rest);
    nothing floats above a contact patch and nothing sinks into the steel."""
    mw = ob.matrix_world; mi = mw.inverted(); n = 0
    for v in ob.data.vertices:
        w = mw @ v.co
        if tuple(round(c, 5) for c in w) in skip: continue
        f = floor(w.x, w.y, w.z + .06)
        if f is None: continue
        f += gap; d = w.z - f
        if d < .04:
            w.z = f + (d + math.sqrt(d * d + soft * soft)) / 2 - soft / 2 * math.exp(-abs(d) / soft)
            v.co = mi @ w; n += 1
    return n

def base():
    from bl_ext.blender_org.mpfb.services.humanservice import HumanService
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    h = HumanService.create_human(macro_detail_dict=MACRO, scale=0.1); h.name = "CorpoBase"
    rig = HumanService.add_builtin_rig(h, "game_engine"); rig.name = "CorpoBase.rig"
    # Skin (applied now so the baked mesh keeps the slot; review renders override it with a simple material
    # unless "pele" is requested). Skins 02 pack, CC0: mindfront_aksel_skin.
    if os.path.exists(SKIN): HumanService.set_character_skin(SKIN, h, skin_type="ENHANCED_SSS", material_instances=False)
    me = h.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
    body = [h.matrix_world @ v.co for v in me.vertices]
    back_y = max(p.y for p in body if abs(p.x) < .17 and .85 < p.z < 1.45) - .006
    height = max(p.z for p in body) - min(p.z for p in body)
    h.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
    bpy.context.view_layer.objects.active = rig; bpy.ops.object.mode_set(mode="POSE")
    pose(rig, back_y)
    bpy.ops.object.mode_set(mode="OBJECT")
    # Lay down: standing Z -> table -X (head west), standing -Y (front) -> +Z, standing +X (left) -> +Y.
    R = Matrix(((0, 0, -1), (1, 0, 0), (0, -1, 0))).to_4x4()
    table = load_table(); floor = mesa_floor_fn(table)
    rig.matrix_world = R; upd()
    deps = bpy.context.evaluated_depsgraph_get(); ev = h.evaluated_get(deps); me = ev.to_mesh()
    pts = [h.matrix_world @ v.co for v in me.vertices]; ev.to_mesh_clear()
    crown = min(p.x for p in pts)
    # Back on the tray (sink 9 mm so the soft contact flattens a patch), crown 10 cm in from the head end.
    trunk = [p for p in pts if -.80 < p.x < -.25 and abs(p.y) < .16]
    dz = (P["tray_floor"](-.5, 0) - .009) - min(p.z for p in trunk)
    rig.matrix_world = Matrix.Translation((-1.135 - crown, 0, dz)) @ R; upd()
    bpy.context.view_layer.objects.active = rig; bpy.ops.object.mode_set(mode="POSE")
    hands_rep, _ = rest_hands(h, rig, floor)
    bpy.ops.object.mode_set(mode="OBJECT")
    skip = hand_points(h)
    # Baked game mesh (rig + full source kept in the .blend): evaluated copy, then contact.
    deps = bpy.context.evaluated_depsgraph_get(); ev = h.evaluated_get(deps)
    me = bpy.data.meshes.new_from_object(ev, depsgraph=deps)
    ob = bpy.data.objects.new("CorpoMesa", me); bpy.context.scene.collection.objects.link(ob)
    ob.matrix_world = h.matrix_world.copy()
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    n = settle(ob, floor, skip=skip)
    settle_only(ob, floor, skip, soft=.0025)
    for p in ob.data.polygons: p.use_smooth = True
    h.hide_render = True; h.hide_set(True); rig.hide_set(True); rig.hide_render = True
    # Report: scale, contact, penetration into the steel, head on the headrest, hand landmarks.
    t = BVHTree.FromObject(table, bpy.context.evaluated_depsgraph_get())
    ws = [v.co for v in ob.data.vertices]
    def region(pred): return [p for p in ws if pred(p)]
    pen = 0
    for p in ws:
        f = floor(p.x, p.y, p.z + .06)
        if f is not None and p.z < f - .0005: pen += 1
    hd = region(lambda p: p.x < -.95)
    hr = lambda s: region(lambda p: (s * p.y) > .22 and -.40 < p.x < -.05)
    rep = {"altura_m": round(height, 3), "vertices": len(ws), "contato_vertices": n, "abaixo_do_aco": pen,
           "cabeca_z_min": round(min(p.z for p in hd), 4),
           "apoio_cabeca_z": round(floor(-1.05, 0, 1.2) or -1, 4),
           "mao_dir_y": [round(min(p.y for p in hr(-1)), 3), round(max(p.y for p in hr(-1)), 3)],
           "mao_esq_y": [round(min(p.y for p in hr(1)), 3), round(max(p.y for p in hr(1)), 3)],
           "pes_x_max": round(max(p.x for p in ws), 3), "coroa_x": round(min(p.x for p in ws), 3),
           "maos_folga_m": hands_rep, "maos_excluidas_do_assentamento": len(skip)}
    for s in ("l", "r"):
        rep["punho_" + s] = [round(c, 3) for c in (rig.matrix_world @ rig.pose.bones["hand_" + s].head)]
        rep["cotovelo_" + s] = [round(c, 3) for c in (rig.matrix_world @ rig.pose.bones["lowerarm_" + s].head)]
    print("BASE", json.dumps(rep), flush=True)
    os.makedirs(OUT, exist_ok=True); json.dump(rep, open(OUT + "/verificacao.json", "w"), indent=2)

# ---------------------------------------------------------------- neutral studio, simple material
def render(views=None):
    sc = bpy.context.scene; table = load_table()
    sc.render.engine = "CYCLES"; sc.cycles.samples = 64; sc.cycles.use_denoising = True
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
    next(n for n in table.data.materials[0].node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Metallic"].default_value = .9
    game = "jogo" in STEPS and "CorpoJogo" in bpy.data.objects
    for a, b in (("CorpoMesa", "CorpoJogo"), ("LencolCorpo", "LencolJogo")):
        if b in bpy.data.objects: bpy.data.objects[a].hide_render = game; bpy.data.objects[b].hide_render = not game
    ob = bpy.data.objects["CorpoJogo" if game else "CorpoMesa"]
    p08v = "p08" in STEPS and "LencolP08" in bpy.data.objects
    if p08v:
        for n in ("CorpoMesa", "CorpoJogo", "LencolCorpo", "LencolJogo", "EtiquetaMovimentacao", "Alfinete", "AdesivoMao"):
            if n in bpy.data.objects: bpy.data.objects[n].hide_render = True
        set_mat(bpy.data.objects["LencolP08"], simple_material("Lencol simples", (.72, .73, .72), .85))
        set_mat(bpy.data.objects["EtiquetaP08"], tag_material())
        set_mat(bpy.data.objects["AlfineteP08"], simple_material("Alfinete simples", (.62, .62, .62), .3))
        set_mat(bpy.data.objects["MarcaSolaP08"], tag_material("Decal_MarcaSola"))
    if "pele" not in STEPS:
        set_mat(ob, simple_material("Pele simples", (.62, .55, .50), .55)); sub = ""
    else:
        ob.data.materials.clear(); ob.data.materials.append(bpy.data.objects["CorpoBase"].data.materials[0]); sub = "pele-"
    sh = None if p08v else bpy.data.objects.get("LencolJogo" if game else "LencolCorpo")
    if sh is not None: sh.hide_render = "jogo" not in STEPS and "comparar" not in STEPS and "lencol" not in STEPS and "pele" not in STEPS and "comlencol" not in STEPS and "detalhes" not in STEPS; set_mat(sh, simple_material("Lencol simples", (.72, .73, .72), .85))
    if "AdesivoMao" in bpy.data.objects and not p08v: set_mat(bpy.data.objects["AdesivoMao"], tag_material("AdesivoMao"))
    for n, rgb in (("Alfinete", (.62, .62, .62)),):
        if n in bpy.data.objects: set_mat(bpy.data.objects[n], simple_material(n + " simples", rgb, .5))
    if "EtiquetaMovimentacao" in bpy.data.objects and not p08v: set_mat(bpy.data.objects["EtiquetaMovimentacao"], tag_material())
    cd = bpy.data.cameras.get("Revisao") or bpy.data.cameras.new("Revisao")
    cam = bpy.data.objects.get("Revisao") or bpy.data.objects.new("Revisao", cd)
    if cam.name not in sc.collection.objects: sc.collection.objects.link(cam)
    sc.camera = cam
    w = {k: Vector(v) for k, v in json.load(open(OUT + "/verificacao.json")).items() if k.startswith("punho")}
    hr, hl = w["punho_r"], w["punho_l"]
    views = views or [
        ("geral-sul", (0.9, -2.4, 1.75), (-0.25, 0, 0.98), 32),
        ("geral-norte", (-0.1, 2.5, 1.75), (-0.25, 0, 0.98), 32),
        ("cabeceira", (-2.2, -0.9, 1.55), (-0.6, 0, 1.0), 38),
        ("pes", (1.55, 0.6, 1.45), (0.1, 0, 0.96), 40),
        ("topo", (-0.25, 0, 3.7), (-0.25, 0, 0.9), 38),
        ("mao-direita-cima", (hr.x + .10, hr.y - .10, hr.z + .42), (hr.x + .09, hr.y + .005, hr.z - .01), 55),
        ("mao-direita-lado", (hr.x + .18, hr.y - .42, hr.z + .12), (hr.x + .07, hr.y, hr.z - .01), 60),
        ("mao-esquerda-cima", (hl.x + .10, hl.y + .10, hl.z + .42), (hl.x + .09, hl.y - .005, hl.z - .01), 55),
        ("mao-esquerda-lado", (hl.x + .18, hl.y + .42, hl.z + .12), (hl.x + .07, hl.y, hl.z - .01), 60),
        ("antebraco-esquerdo", (hl.x - .12, hl.y + .62, hl.z + .38), (hl.x - .12, hl.y, hl.z + .01), 45),
    ]
    if "detalhes" in STEPS:
        tx, ty, _ = LENCOL_TAG; sx, sy = json.load(open(OUT + "/verificacao.json"))["punho_r"][:2]
        views = [("adesivo", (sx + .05, sy - .22, 1.22), (sx + .05, sy, .95), 60),
                 ("etiqueta", (tx, ty - .35, 1.42), (tx, ty, 1.08), 55),
                 ("detalhes-sul", (-0.3, -1.6, 1.62), (-0.3, -0.1, 1.0), 35)]
    if p08v:
        e = json.load(open(OUT + "/p08_verificacao.json"))["etiqueta_centro"]; sx, sy, _ = P08["sole"]
        views = [("p08-geral-norte", (-0.1, 2.6, 1.75), (-0.25, 0.2, 0.7), 30), ("p08-geral-sul", (0.9, -2.4, 1.75), (-0.25, 0, 0.9), 32),
                 ("p08-pes", (1.8, 0.9, 1.3), (-0.2, 0.3, 0.75), 34),
                 ("p08-etiqueta", (e[0] + .05, e[1] + .45, e[2] + .25), tuple(e), 60),
                 ("p08-sola", (sx + .35, sy + .55, 1.1), (sx, sy, 0), 45)]
    if "comparar" in STEPS:
        views = [v for v in views if v[0] in ("geral-sul", "geral-norte", "cabeceira", "pes", "antebraco-esquerdo")]
        views.append(("agachado-sul", (-0.3, -1.25, 0.65), (-0.3, 0, 0.98), 30))
        views.append(("agachado-pes", (1.6, -0.3, 0.65), (0.4, 0, 0.98), 30))
    os.makedirs(OUT, exist_ok=True)
    if "maos" in STEPS: views = [v for v in views if v[0].startswith("mao")]
    for name, pos, tgt, lens in views:
        # Detail captures: neutral and less blown out (the studio is tuned for the wide shots).
        sc.view_settings.exposure = -0.8 if name.split("-")[0] in ("mao", "polegar", "adesivo", "etiqueta", "antebraco") else 0.0
        cd.lens = lens; cam.location = pos
        cam.rotation_euler = (Vector(tgt) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
        pre = "" if p08v else ("jogo-" if game else "fonte-" if "comparar" in STEPS else "lencol-" if "comlencol" in STEPS else "") + sub
        sc.render.filepath = OUT + "/" + pre + name + ".png"; bpy.ops.render.render(write_still=True)
        print("RENDERED", name, flush=True)

# ---------------------------------------------------------------- sheet (same cloth setup as the scripted pilot)
LENCOL_TAG = (-0.40, -0.085, -10)   # tag centre x, y on the sheet and yaw (deg)
LENCOL = dict(x0=-1.42, x1=0.94, width=0.70, offset=-0.008, step=.012, z=1.18, bend=2.5)   # accepted 2026-11-08 (lencol_verificacao.json)

def sheet():
    if "LencolCorpo" in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects["LencolCorpo"], do_unlink=True)
    body, table = bpy.data.objects["CorpoMesa"], load_table()
    for o in (body, table):
        o.modifiers.get("Colisao") or o.modifiers.new("Colisao", "COLLISION")
        o.collision.thickness_outer = .0025 if o is body else .003
        o.collision.cloth_friction = 20.0; o.collision.damping = .8
    x0, x1, W, off, st = (LENCOL[k] for k in ("x0", "x1", "width", "offset", "step"))
    nx, ny = int((x1 - x0) / st), int(W / st)
    import bmesh
    bm = bmesh.new()
    rows = [[bm.verts.new((x0 + (x1 - x0) * i / nx, off - W / 2 + W * j / ny, LENCOL["z"])) for j in range(ny + 1)] for i in range(nx + 1)]
    for i in range(nx):
        for j in range(ny): bm.faces.new((rows[i][j], rows[i + 1][j], rows[i + 1][j + 1], rows[i][j + 1]))
    me = bpy.data.meshes.new("LencolCorpo"); bm.to_mesh(me); bm.free()
    uv = me.uv_layers.new(name="UVMap")
    for loop in me.loops:
        co = me.vertices[loop.vertex_index].co; uv.data[loop.index].uv = ((co.x - x0) / 1.0, (co.y - off + W / 2) / 1.0)
    ob = bpy.data.objects.new("LencolCorpo", me); bpy.context.scene.collection.objects.link(ob)
    cl = ob.modifiers.new("Tecido", "CLOTH"); c = cl.settings
    c.quality = 12; c.mass = .32; c.air_damping = 3.0
    c.tension_stiffness = c.compression_stiffness = 18; c.shear_stiffness = 8; c.bending_stiffness = LENCOL["bend"]
    c.tension_damping = c.compression_damping = c.shear_damping = 5
    cs = cl.collision_settings; cs.collision_quality = 4; cs.distance_min = .0035
    cs.use_self_collision = True; cs.self_distance_min = .003; cs.self_friction = 8
    sc = bpy.context.scene; sc.frame_start = 1; sc.frame_end = 120
    cl.point_cache.frame_start = 1; cl.point_cache.frame_end = 120
    for f in range(1, 121): sc.frame_set(f)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=cl.name)
    sm = ob.modifiers.new("Assentar", "CORRECTIVE_SMOOTH"); sm.factor = .25; sm.iterations = 3
    bpy.ops.object.modifier_apply(modifier=sm.name)
    smooth_hem(ob)
    for p in ob.data.polygons: p.use_smooth = True
    for o in (body, table):
        if o.modifiers.get("Colisao"): o.modifiers.remove(o.modifiers["Colisao"])
    sc.frame_set(1)
    return ob

def smooth_hem(ob, rounds=6):
    """Hem: the self-collision leaves a saw-tooth along the free edge; relax only the boundary loop along its own
    direction (keeps the drape and the contact with the steel, removes the zig-zag)."""
    import bmesh
    bm = bmesh.new(); bm.from_mesh(ob.data); bm.verts.ensure_lookup_table()
    edge = [v for v in bm.verts if v.is_boundary]
    for _ in range(rounds):
        new = {}
        for v in edge:
            nb = [e.other_vert(v) for e in v.link_edges if e.is_boundary]
            if len(nb) == 2: new[v] = v.co * .5 + (nb[0].co + nb[1].co) * .25
        for v, c in new.items(): v.co.x, v.co.y = c.x, c.y; v.co.z = max(v.co.z, c.z) if v.co.z < 1.0 else c.z
    bm.to_mesh(ob.data); bm.free()

def head_hidden(sh, body):
    """Share of the head's surface seen from where the Player can stand (eye 1.6 m and crouched 1.1 m) around the
    head end and the sides of the table; the steel and the sheet block the view."""
    deps = bpy.context.evaluated_depsgraph_get()
    occ = [BVHTree.FromObject(o, deps) for o in (sh, load_table())]
    head = [v.co for v in body.data.vertices if v.co.x < -.93]
    eyes = [Vector((x, y, z)) for z in (1.1, 1.6) for x, y in ((-1.95, 0), (-1.9, -.7), (-1.9, .7), (-1.2, -.9), (-1.2, .9), (-.7, -.9), (-.7, .9))]
    seen = 0
    for p in head:
        for e in eyes:
            d = e - p; L = d.length; d.normalize(); o = p + d * .004
            if all(t.ray_cast(o, d, L)[0] is None for t in occ): seen += 1; break
    return round(seen / max(len(head), 1), 4)

def check_sheet():
    """Story areas measured directly: share of the right hand's back and of the left forearm with nothing above
    them; head covered; sheet inside body or steel; hem positions."""
    deps = bpy.context.evaluated_depsgraph_get()
    sh, body = bpy.data.objects["LencolCorpo"], bpy.data.objects["CorpoMesa"]
    tsh = BVHTree.FromObject(sh, deps); tb = BVHTree.FromObject(body, deps)
    tt = BVHTree.FromObject(load_table(), deps)
    vs = [v.co for v in body.data.vertices]
    def exposed(pred):
        sel = [p for p in vs if pred(p)]
        free = sum(1 for p in sel if tsh.ray_cast(p + Vector((0, 0, .002)), Vector((0, 0, 1)), 1.0)[0] is None)
        return round(free / max(len(sel), 1), 3), len(sel)
    rep = {"dorso_mao_direita_exposto": exposed(lambda p: p.y < -.205 and -.27 < p.x < -.10 and p.z > .925),
           "antebraco_esquerdo_exposto": exposed(lambda p: p.y > .19 and -.52 < p.x < -.29 and p.z > .93),
           "cabeca_coberta": 1 - exposed(lambda p: p.x < -.93)[0],
           "cabeca_visivel_do_player": head_hidden(sh, body)}
    inside = {"corpo": 0, "mesa": 0}
    for v in sh.data.vertices:
        for k, t in (("corpo", tb), ("mesa", tt)):
            hit = t.find_nearest(v.co)
            if hit[0] is not None and (v.co - hit[0]).dot(hit[1]) < -0.0015 and hit[3] < .03: inside[k] += 1
    rep["lencol_dentro"] = inside
    ys = [v.co for v in sh.data.vertices]
    def hem(sign, x):
        band = [p.y for p in ys if abs(p.x - x) < .03]
        return round(min(band), 3) if sign < 0 else round(max(band), 3)
    rep["bainha_sul"] = {x: hem(-1, x) for x in (-0.45, -0.30, -0.20)}
    rep["bainha_norte"] = {x: hem(1, x) for x in (-0.50, -0.40, -0.30)}
    rep["lencol_tris"] = sum(len(p.vertices) - 2 for p in sh.data.polygons)
    print("LENCOL", json.dumps(rep), flush=True)
    json.dump(rep, open(OUT + "/lencol_verificacao.json", "w"), indent=2)
    return rep

# ---------------------------------------------------------------- details: sticker (right hand), tag 083 on the sheet
TEXDIR = ROOT + "/Assets/UnclaimedPrototype/Autopsia/Texturas/"
TAG_TEX = TEXDIR + "Etiqueta083.png"

def _patch(name, target, center, size, yaw, outline=None, lift=.0007, nu=10, nv=10, thick=.0004, smooth=0):
    """Thin patch laid on a surface by vertical ray casts (follows skin or cloth). UVs span the full card
    (u along `yaw`, v across), so a printed texture maps 1:1. `smooth` relaxes height only (a card is stiffer
    than the cloth under it)."""
    import bmesh
    deps = bpy.context.evaluated_depsgraph_get(); t = BVHTree.FromObject(bpy.data.objects[target], deps)
    cx, cy = center; a, b = size; c, s_ = math.cos(yaw), math.sin(yaw)
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("UVMap"); grid = {}; uvs = {}
    for i in range(nu + 1):
        for j in range(nv + 1):
            u, v = i / nu - .5, j / nv - .5
            if outline and not outline(u, v): continue
            x = cx + u * a * c - v * b * s_; y = cy + u * a * s_ + v * b * c
            hit = t.ray_cast(Vector((x, y, 1.4)), Vector((0, 0, -1)))
            if hit[0] is None: continue
            grid[i, j] = bm.verts.new(hit[0] + hit[1] * lift); uvs[i, j] = (i / nu, j / nv)
    for _ in range(smooth):
        new = {}
        for (i, j), vv in grid.items():
            nb = [grid[k] for k in ((i - 1, j), (i + 1, j), (i, j - 1), (i, j + 1)) if k in grid]
            new[vv] = max(vv.co.z, sum(n.co.z for n in nb) / len(nb))   # never sink into what is below
        for vv, z in new.items(): vv.co.z = z
    for i in range(nu):
        for j in range(nv):
            q = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
            if all(k in grid for k in q):
                f = bm.faces.new([grid[k] for k in q])
                for lp, k in zip(f.loops, q): lp[uvl].uv = uvs[k]
    ob = P["_link"](name, bm)
    if thick > 0:
        so = ob.modifiers.new("Espessura", "SOLIDIFY"); so.thickness = thick; so.offset = 1.0
        bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier=so.name)
    for p in ob.data.polygons: p.use_smooth = True
    return ob

def details():
    """Story clues on the MPFB body. Sticker: ~23 x 16 mm card (torn edge in the texture alpha), on the back of the right hand,
    plain (nothing written: no invented content). Tag: 90 x 55 mm transfer card printed '083'
    (impressos.py), on the sheet over the right side of the abdomen, text upright for someone standing on
    the south (right-hand) side, held by a safety pin through its eyelet."""
    import bmesh
    for n in ("AdesivoMao", "EtiquetaMovimentacao", "Alfinete"):
        if n in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
    w = json.load(open(OUT + "/verificacao.json"))
    wr, el = Vector(w["punho_r"]), Vector(w["cotovelo_r"])
    d = (wr - el); d.z = 0; d.normalize(); yaw = math.atan2(d.y, d.x)
    # Torn outline comes from the alpha of AdesivoMao.png (impressos.py); the patch only carries the curvature.
    hx, hy = wr.x + d.x * .050 + d.y * .004, wr.y + d.y * .050 - d.x * .004
    st = _patch("AdesivoMao", "CorpoMesa", (hx, hy), (.023, .016), yaw + .35, None, lift=.0005, nu=8, nv=6, thick=0)
    tx, ty, tyaw = LENCOL_TAG
    tag = _patch("EtiquetaMovimentacao", "LencolCorpo", (tx, ty), (.090, .055), math.radians(tyaw),
                 lift=.0010, nu=14, nv=9, thick=.0005, smooth=3)
    # Safety pin: shaft over the eyelet (texture u = 75/900 from the left edge), head outside the card.
    c, s_ = math.cos(math.radians(tyaw)), math.sin(math.radians(tyaw))
    ex, ey = tx + (-.5 + 75 / 900) * .090 * c, ty + (-.5 + 75 / 900) * .090 * s_
    deps = bpy.context.evaluated_depsgraph_get(); tt = BVHTree.FromObject(tag, deps)
    hit = tt.ray_cast(Vector((ex, ey, 1.4)), Vector((0, 0, -1)))
    base_p = hit[0] if hit[0] is not None else Vector((ex, ey, 1.0))
    # Pin lies outside the print, to the left: the shaft dips through the eyelet only, clasp off the card.
    pin = [Vector((.004, 0, -.0006)), Vector((-.026, .0035, 0)), Vector((-.036, .0035, .0008)), Vector((-.036, -.0035, .0008)),
           Vector((-.026, -.0035, .0010)), Vector((.002, -.0035, .0010)), Vector((.005, -.001, .0004))]
    m = Matrix.Translation(base_p + Vector((0, 0, .0012))) @ Matrix.Rotation(math.radians(tyaw + 8), 4, "Z")
    P["_link"]("Alfinete", wire([m @ q for q in pin], .00045))
    rep = {"adesivo_centro": [round(hx, 3), round(hy, 3)], "adesivo_tris": sum(len(p.vertices) - 2 for p in st.data.polygons),
           "etiqueta_centro": [tx, ty], "etiqueta_tris": sum(len(p.vertices) - 2 for p in tag.data.polygons)}
    print("DETALHES", json.dumps(rep), flush=True)
    return rep

def wire(points, r, sides=6):
    """Thin tube along a polyline (safety-pin wire): `sides` around, one ring per point, ends capped flat."""
    import bmesh
    bm = bmesh.new(); rings = []
    for k, p in enumerate(points):
        t = (points[min(k + 1, len(points) - 1)] - points[max(k - 1, 0)]).normalized()
        a = t.orthogonal().normalized(); b = t.cross(a)
        rings.append([bm.verts.new(p + (a * math.cos(i / sides * math.tau) + b * math.sin(i / sides * math.tau)) * r) for i in range(sides)])
    for r0, r1 in zip(rings, rings[1:]):
        for i in range(sides): bm.faces.new((r0[i], r0[(i + 1) % sides], r1[(i + 1) % sides], r1[i]))
    bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    return bm

def tag_material(name="Etiqueta083"):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True; nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"); bsdf.inputs["Roughness"].default_value = .7
    tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE"), None) or nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(TEXDIR + name + ".png", check_existing=True)
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"]); nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    return m

# ---------------------------------------------------------------- game version (source keeps full body + rig)
def player_eyes():
    """Where the Player's camera can be around the table: crouched (0.65 m), mid (1.1 m) and standing
    (1.65 m), on two rings 0.35 m and 0.9 m off the steel edge (the table is ~2.3 x 0.8 m)."""
    eyes = []
    for z in (0.65, 1.1, 1.65):
        for off in (.35, .90):
            x0, x1, y0 = -1.25 - off, 1.05 + off, .40 + off
            for k in range(13): eyes += [Vector((x0 + (x1 - x0) * k / 12, sy * y0, z)) for sy in (-1, 1)]
            for k in range(1, 6): eyes += [Vector((x, -y0 + 2 * y0 * k / 6, z)) for x in (x0, x1)]
    return eyes

def visible_vertices(ob, occluders, eyes, margin=.035):
    """Vertices seen from any Player eye (body self-occlusion included), dilated by `margin` so the reduced
    region never reaches a visible edge, the hem gap or a silhouette."""
    import bmesh
    deps = bpy.context.evaluated_depsgraph_get()
    occ = [BVHTree.FromObject(o, deps) for o in occluders]
    vs = [v.co.copy() for v in ob.data.vertices]; nrm = [v.normal.copy() for v in ob.data.vertices]
    seen = set()
    for i, p in enumerate(vs):
        o = p + nrm[i] * .002
        for e in eyes:
            d = e - o; L = d.length; d /= L
            if d.dot(nrm[i]) < -.05: continue                    # back-facing from this eye
            if all(t.ray_cast(o, d, L)[0] is None for t in occ): seen.add(i); break
    kd = __import__("mathutils").kdtree.KDTree(len(seen)); [kd.insert(vs[i], i) for i in seen]; kd.balance()
    keep = {i for i, p in enumerate(vs) if i in seen or (seen and kd.find(p)[2] < margin)}
    return keep, len(seen)

def tris(ob): return sum(len(p.vertices) - 2 for p in ob.data.polygons)

def reduce_sheet(sj):
    """Collapse with the hem loop protected (the free edge is what the eye reads)."""
    import bmesh
    bm = bmesh.new(); bm.from_mesh(sj.data)
    hem = [v.index for v in bm.verts if v.is_boundary]; bm.free()
    vg = sj.vertex_groups.new(name="Bainha"); vg.add(hem, 1.0, "REPLACE")
    dm = sj.modifiers.new("Reduzir", "DECIMATE"); dm.decimate_type = "COLLAPSE"; dm.ratio = float(os.environ.get("J_sheet", .45))
    dm.vertex_group = "Bainha"; dm.invert_vertex_group = True; dm.vertex_group_factor = 1.0; dm.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = sj; bpy.ops.object.modifier_apply(modifier=dm.name)
    sj.vertex_groups.clear()

def smooth_normals_source(src, repeat=10):
    """Normals donor for a sheet: the solver leaves grid-aligned micro-ripples (~12 mm) that read as stripes
    under flat ceiling light; a smoothed copy keeps the folds and drops the ripples. Geometry is untouched."""
    c = src.copy(); c.data = src.data.copy(); bpy.context.scene.collection.objects.link(c)
    sm = c.modifiers.new("Liso", "SMOOTH"); sm.factor = .6; sm.iterations = repeat
    bpy.context.view_layer.objects.active = c; bpy.ops.object.modifier_apply(modifier=sm.name)
    return c

def transfer_normals(o, src):
    for p in o.data.polygons: p.use_smooth = True
    dt = o.modifiers.new("Normais", "DATA_TRANSFER"); dt.object = src
    dt.use_loop_data = True; dt.data_types_loops = {"CUSTOM_NORMAL"}; dt.loop_mapping = "POLYINTERP_NEAREST"
    bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=dt.name)

def game_version():
    """CorpoJogo: CorpoMesa with only the never-seen part (under the sheet, against the steel) decimated;
    hands, forearms and everything within the margin of a visible point keep full resolution.
    LencolJogo: sheet reduced with planar-aware collapse, hem loop preserved."""
    for n in ("CorpoJogo", "LencolJogo"):
        if n in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
    body, sh, table = bpy.data.objects["CorpoMesa"], bpy.data.objects["LencolCorpo"], load_table()
    ob = body.copy(); ob.data = body.data.copy(); ob.name = "CorpoJogo"; bpy.context.scene.collection.objects.link(ob)
    eyes = player_eyes()
    keep, nseen = visible_vertices(ob, (sh, table, body), eyes)
    # Hands and forearms always full, seen or not (story areas, close inspection).
    w = json.load(open(OUT + "/verificacao.json"))
    for i, v in enumerate(ob.data.vertices):
        if abs(v.co.y) > .19 and v.co.x > -.60: keep.add(i)
    vg = ob.vertex_groups.new(name="Reduzir")
    vg.add([i for i in range(len(ob.data.vertices)) if i not in keep], 1.0, "REPLACE")
    full = tris(ob)
    # The cloth rests 1-2.5 mm on the skin; a collapse moves surfaces by about that much, so the never-seen
    # region is first pulled 5 mm inward (its edge is >= 3.5 cm from any visible point).
    dp = ob.modifiers.new("Recuar", "DISPLACE"); dp.direction = "NORMAL"; dp.strength = -.005; dp.mid_level = 0
    dp.vertex_group = "Reduzir"
    bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier=dp.name)
    dm = ob.modifiers.new("Reduzir", "DECIMATE"); dm.decimate_type = "COLLAPSE"; hid = set(range(len(ob.data.vertices))) - keep
    hid_tris = sum(len(p.vertices) - 2 for p in ob.data.polygons if all(i in hid for i in p.vertices))
    # Target = kept triangles + r x never-seen triangles (a plain ratio bottoms out and over-collapses).
    dm.ratio = (full - hid_tris * (1 - float(os.environ.get("J_ratio", .30)))) / full
    dm.vertex_group = "Reduzir"; dm.vertex_group_factor = 1.0; dm.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier=dm.name)
    # A coarse collapse can place vertices outside the anatomy (thighs bridged above the drape between the
    # legs); keep every reduced vertex inside the source surface.
    sw = ob.modifiers.new("Dentro", "SHRINKWRAP"); sw.target = body; sw.wrap_method = "NEAREST_SURFACEPOINT"
    sw.wrap_mode = "INSIDE"; sw.offset = .004; sw.vertex_group = "Reduzir"
    bpy.ops.object.modifier_apply(modifier=sw.name)
    gi = ob.vertex_groups["Reduzir"].index
    reduced = {v.index for v in ob.data.vertices if any(g.group == gi and g.weight > .5 for g in v.groups)}
    ob.vertex_groups.clear()
    # Faces entirely in the never-seen region: exported as a separate mesh that casts no shadow (its shadow
    # falls under the sheet; this only trims the shadow pass).
    at = ob.data.attributes.get("oculto") or ob.data.attributes.new("oculto", "BOOLEAN", "FACE")
    for p in ob.data.polygons: at.data[p.index].value = all(i in reduced for i in p.vertices)
    sj = sh.copy(); sj.data = sh.data.copy(); sj.name = "LencolJogo"; bpy.context.scene.collection.objects.link(sj)
    sfull = tris(sj); reduce_sheet(sj)
    # The collapse keeps the shape (sheet: 95% within 0.6 mm) but redistributes interpolated normals, which
    # reads as a translucent sheet; the source normals are transferred as custom normals (FBX carries them).
    transfer_normals(ob, body)
    donor = smooth_normals_source(sh); transfer_normals(sj, donor); bpy.data.objects.remove(donor, do_unlink=True)
    # Check: the reduced body must not poke through the sheet nor sink into the steel anywhere.
    # Poke = a body vertex that the source sheet covers but the reduced sheet no longer does (or the reverse
    # with the reduced body), tested straight up from every vertex of both body versions.
    deps = bpy.context.evaluated_depsgraph_get(); floor = mesa_floor_fn(table)
    t_src, t_game = BVHTree.FromObject(sh, deps), BVHTree.FromObject(sj, deps)
    up = Vector((0, 0, 1))
    def covered(t, p): return t.ray_cast(p + up * .0005, up, 1.0)[0] is not None
    pts = lambda o: [v.co for v in o.data.vertices] + [p.center for p in o.data.polygons]
    poke = sum(1 for o in (body, ob) for c in pts(o) if covered(t_src, c) and not covered(t_game, c))
    # Clearance: game body (vertices + face centres) to the topmost game sheet, where the sheet covers it.
    gaps = sorted(h[0].z - c.z for c in pts(ob) if covered(t_src, c)
                  and (h := t_game.ray_cast(Vector((c.x, c.y, 1.6)), -up, 1.0))[0] is not None)
    sink = sum(1 for v in ob.data.vertices if (f := floor(v.co.x, v.co.y, v.co.z + .06)) is not None and v.co.z < f - .0005)
    # Real criterion: from the Player's eyes, with the game sheet, the steel and the game body itself as
    # occluders, no reduced vertex may be seen.
    seen_game, _ = visible_vertices(ob, (sj, table, ob), eyes, margin=0)
    rep_vis = len(seen_game & reduced)
    rep = {"reduzidos_visiveis": rep_vis, "olhos_player": len(eyes), "corpo_vertices_vistos": nseen, "corpo_vertices_mantidos": len(keep),
           "corpo_tris_fonte": full, "corpo_tris_jogo": tris(ob), "lencol_tris_fonte": sfull, "lencol_tris_jogo": tris(sj),
           "corpo_atravessa_lencol": poke, "folga_min_mm": round(gaps[0] * 1000, 2) if gaps else None, "corpo_abaixo_do_aco": sink,
           "adesivo_tris": tris(bpy.data.objects["AdesivoMao"]),
           "etiqueta_tris": tris(bpy.data.objects["EtiquetaMovimentacao"]) + tris(bpy.data.objects["Alfinete"])}
    print("JOGO", json.dumps(rep), flush=True)
    json.dump(rep, open(OUT + "/jogo_verificacao.json", "w"), indent=2)

def export():
    """Review export (full body, not reduced yet) + the pack's own skin maps, for the Unity preview."""
    import types, shutil
    ns = {"__name__": "rexp", "__file__": ROOT + "/Blender/Recepcao/scripts/recepcao_modelo.py"}
    exec(compile(open(ns["__file__"]).read(), ns["__file__"], "exec"), ns)
    out = ROOT + "/Assets/Modelos/Autopsia/Piloto"; os.makedirs(out + "/Texturas", exist_ok=True)
    ob = bpy.data.objects["CorpoMesa"]; ob.name = "CorpoBaseMesa"
    try: ns["export_fbx"](ob, out + "/CorpoBase.fbx")
    finally: ob.name = "CorpoMesa"
    d = os.path.dirname(SKIN)
    for src, dst in (("Aksel_Skin_diffuse.png", "CorpoBase_Pele_BaseMap.png"), ("Aksel_Skin_NRM.png", "CorpoBase_Pele_Normal.png")):
        shutil.copyfile(d + "/" + src, out + "/Texturas/" + dst)
    print("EXPORT", len(ob.data.polygons), flush=True)

# ---------------------------------------------------------------- P08 variant: empty table, sheet fallen north
P08 = dict(shift=0.34, lift=0.02, frames1=45, frames=160, friction=30.0, sole=(-0.15, 0.80, 8.0))   # sheet shift +Y (north); sole x, y, yaw

def p08():
    """The occupied sheet with the body gone: shifted north and dropped, it slumps on the steel and hangs over
    the north edge, keeping the folds of the shape it had. Tag 083 and its pin stay attached (Surface Deform
    bound before the drop). One wet sole mark on the floor beside the north edge, toe away from the table
    (not toward a door). No collision volumes are made for the game."""
    import bmesh
    for k in list(P08):
        if os.environ.get("P_" + k): P08[k] = float(os.environ["P_" + k])
    for n in ("LencolP08", "EtiquetaP08", "AlfineteP08", "MarcaSolaP08", "Piso P08"):
        if n in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
    src, table = bpy.data.objects["LencolCorpo"], load_table()
    ob = src.copy(); ob.data = src.data.copy(); ob.name = "LencolP08"; bpy.context.scene.collection.objects.link(ob)
    att = []
    for a, b in (("EtiquetaMovimentacao", "EtiquetaP08"), ("Alfinete", "AlfineteP08")):
        o = bpy.data.objects[a].copy(); o.data = bpy.data.objects[a].data.copy(); o.name = b; o.hide_render = False
        bpy.context.scene.collection.objects.link(o); att.append(o)
        sd = o.modifiers.new("Preso", "SURFACE_DEFORM"); sd.target = ob
        bpy.context.view_layer.objects.active = o; bpy.ops.object.surfacedeform_bind(modifier=sd.name)
    ob.hide_render = False; ob.matrix_world = Matrix()
    ob.data.transform(Matrix.Translation((0, P08["shift"], P08["lift"]))); ob.data.update()
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=4); fl = bpy.data.meshes.new("Piso P08")
    bm.to_mesh(fl); bm.free(); floor = bpy.data.objects.new("Piso P08", fl); bpy.context.scene.collection.objects.link(floor)
    for o in (table, floor):
        o.modifiers.get("Colisao") or o.modifiers.new("Colisao", "COLLISION")
        o.collision.thickness_outer = .003; o.collision.cloth_friction = P08["friction"]; o.collision.damping = .8
    sc = bpy.context.scene
    def run(frames, pin=None):
        """One cloth pass; returns the largest vertex movement over its last 10 frames."""
        cl = ob.modifiers.new("Tecido", "CLOTH"); c = cl.settings
        c.quality = 12; c.mass = .32; c.air_damping = 3.0
        c.tension_stiffness = c.compression_stiffness = 18; c.shear_stiffness = 8; c.bending_stiffness = LENCOL["bend"]
        c.tension_damping = c.compression_damping = c.shear_damping = 5
        cs = cl.collision_settings; cs.collision_quality = 4; cs.distance_min = .0035
        cs.use_self_collision = True; cs.self_distance_min = .003; cs.self_friction = 8
        if pin:
            vg = ob.vertex_groups.get("Preso") or ob.vertex_groups.new(name="Preso"); vg.add(pin, 1.0, "REPLACE")
            c.vertex_group_mass = "Preso"
        sc.frame_start = 1; sc.frame_end = frames; cl.point_cache.frame_start = 1; cl.point_cache.frame_end = frames
        snap = lambda: [v.co.copy() for v in ob.evaluated_get(bpy.context.evaluated_depsgraph_get()).data.vertices]
        for f in range(1, frames + 1):
            sc.frame_set(f)
            if f == frames - 10: before = snap()
        d = max((a - b).length for a, b in zip(snap(), before))
        bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier=cl.name); sc.frame_set(1)
        return d
    # Phase 1: drop; the part over the tray lands on the steel. Phase 2: that part is held where it lies
    # (cotton grips brushed steel; the solver's friction alone lets it creep off), the overhang settles.
    run(int(P08["frames1"]))
    tray = [v.index for v in ob.data.vertices if v.co.z < .965 and abs(v.co.y) < .33 and -1.2 < v.co.x < 1.0]
    drift = run(int(P08["frames"]), tray)
    ob.vertex_groups.clear()
    sm = ob.modifiers.new("Assentar", "CORRECTIVE_SMOOTH"); sm.factor = .25; sm.iterations = 3
    bpy.ops.object.modifier_apply(modifier=sm.name)
    for o in att:
        bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier="Preso")
    for o in (table,):
        if o.modifiers.get("Colisao"): o.modifiers.remove(o.modifiers["Colisao"])
    bpy.data.objects.remove(floor, do_unlink=True)
    sc.frame_set(1)
    for p in ob.data.polygons: p.use_smooth = True
    full = ob.copy(); full.data = ob.data.copy(); bpy.context.scene.collection.objects.link(full)
    sfull = tris(ob); reduce_sheet(ob)
    donor = smooth_normals_source(full); transfer_normals(ob, donor)
    for o in (full, donor): bpy.data.objects.remove(o, do_unlink=True)
    # Sole mark: 0.30 x 0.15 m card on the floor (texture: heel at u=0, toe at u=1), 6 mm up: the floor
    # finish is drawn 3 mm above the collider and the wear decal around the table sits at 5 mm.
    sx, sy, syaw = P08["sole"]; c_, s_ = math.cos(math.radians(90 + syaw)), math.sin(math.radians(90 + syaw))
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("UVMap"); vs = []
    for u, v in ((0, 0), (1, 0), (1, 1), (0, 1)):
        lx, ly = (u - .5) * .30, (v - .5) * .15
        vs.append((bm.verts.new((sx + lx * c_ - ly * s_, sy + lx * s_ + ly * c_, .006)), (u, v)))
    f = bm.faces.new([a for a, _ in vs])
    for lp, (_, uv) in zip(f.loops, vs): lp[uvl].uv = uv
    P["_link"]("MarcaSolaP08", bm).hide_render = False
    # Checks: nothing inside the steel, tag still on the cloth, where the cloth went.
    deps = bpy.context.evaluated_depsgraph_get(); tt = BVHTree.FromObject(table, deps); ts = BVHTree.FromObject(ob, deps)
    inside = sum(1 for v in ob.data.vertices if (h := tt.find_nearest(v.co))[0] is not None and h[3] < .03
                 and (v.co - h[0]).dot(h[1]) < -.0015)
    tagd = max(ts.find_nearest(v.co)[3] for v in att[0].data.vertices)
    zs = [v.co.z for v in ob.data.vertices]; ys = [v.co.y for v in ob.data.vertices]
    tc = sum((v.co for v in att[0].data.vertices), Vector()) / len(att[0].data.vertices)
    rep = {"presos_na_bandeja": len(tray), "deriva_ultimos_10_quadros_mm": round(drift * 1000, 1), "lencol_dentro_mesa": inside, "etiqueta_dist_max_mm": round(tagd * 1000, 2), "etiqueta_centro": [round(c, 3) for c in tc],
           "lencol_z_min": round(min(zs), 3), "lencol_y": [round(min(ys), 3), round(max(ys), 3)],
           "lencol_tris_fonte": sfull, "lencol_tris": tris(ob), "etiqueta_tris": tris(att[0]) + tris(att[1]), "sola_tris": 2}
    print("P08", json.dumps(rep), flush=True)
    json.dump(rep, open(OUT + "/p08_verificacao.json", "w"), indent=2)

def export_game():
    """Game assets for the occupied state, in the table's frame (origin = table origin, like the other
    autopsy pieces): CorpoOcupado (reduced body, skin UVs), LencolOcupado (reduced sheet), AdesivoMao,
    EtiquetaMovimentacao (card slot 0 + pin slot 1). Custom normals go with the meshes."""
    import shutil
    ns = {"__name__": "rexp", "__file__": ROOT + "/Blender/Recepcao/scripts/recepcao_modelo.py"}
    exec(compile(open(ns["__file__"]).read(), ns["__file__"], "exec"), ns)
    out = ROOT + "/Assets/Modelos/Autopsia/Corpo"; os.makedirs(out + "/Texturas", exist_ok=True)
    tag, pin = bpy.data.objects["EtiquetaMovimentacao"], bpy.data.objects["Alfinete"]
    j = tag.copy(); j.data = tag.data.copy(); bpy.context.scene.collection.objects.link(j)
    pj = pin.copy(); pj.data = pin.data.copy(); bpy.context.scene.collection.objects.link(pj)
    j.data.materials.clear(); j.data.materials.append(tag_material()); pj.data.materials.clear()
    pj.data.materials.append(simple_material("Alfinete simples", (.62, .62, .62), .3))
    bpy.ops.object.select_all(action="DESELECT"); j.select_set(True); pj.select_set(True)
    bpy.context.view_layer.objects.active = j; bpy.ops.object.join()
    stats = {}
    import bmesh
    split = []
    for name, hidden in (("CorpoJogoVisivel", False), ("CorpoJogoOculto", True)):
        src = bpy.data.objects["CorpoJogo"]; c = src.copy(); c.data = src.data.copy(); c.name = name
        bpy.context.scene.collection.objects.link(c); split.append(c)
        bm = bmesh.new(); bm.from_mesh(c.data); lay = bm.faces.layers.bool.get("oculto")
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f[lay] != hidden], context="FACES")
        bm.to_mesh(c.data); bm.free()
    jobs = [("CorpoJogoVisivel", "CorpoOcupado", False), ("CorpoJogoOculto", "CorpoOcupadoOculto", False),
            ("LencolJogo", "LencolOcupado", False),
            ("AdesivoMao", "AdesivoMao", False), (j.name, "EtiquetaMovimentacao", True)]
    if "LencolP08" in bpy.data.objects:      # P08: fallen sheet, its tag + pin, sole mark
        t8, p8 = bpy.data.objects["EtiquetaP08"], bpy.data.objects["AlfineteP08"]
        j8 = t8.copy(); j8.data = t8.data.copy(); bpy.context.scene.collection.objects.link(j8)
        q8 = p8.copy(); q8.data = p8.data.copy(); bpy.context.scene.collection.objects.link(q8)
        j8.data.materials.clear(); j8.data.materials.append(tag_material()); q8.data.materials.clear()
        q8.data.materials.append(simple_material("Alfinete simples", (.62, .62, .62), .3))
        bpy.ops.object.select_all(action="DESELECT"); j8.select_set(True); q8.select_set(True)
        bpy.context.view_layer.objects.active = j8; bpy.ops.object.join()
        jobs += [("LencolP08", "LencolP08", False), (j8.name, "EtiquetaP08", True), ("MarcaSolaP08", "MarcaSolaP08", False)]
    try:
        for src, name, slots in jobs:
            ob = bpy.data.objects[src]; old = ob.name; ob.name = name + "_x"
            try:
                tmp = ob.copy(); tmp.data = ob.data.copy(); tmp.name = name; bpy.context.scene.collection.objects.link(tmp)
                ns["export_fbx"](tmp, out + "/" + name + ".fbx", keep_slots=slots)
                stats[name] = tris(tmp); bpy.data.objects.remove(tmp)
            finally:
                ob.name = old
    finally:
        bpy.data.objects.remove(j)
        for c in split: bpy.data.objects.remove(c, do_unlink=True)
        if "LencolP08" in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects[jobs[-2][0]])
    d = os.path.dirname(SKIN)
    for a, b in (("Aksel_Skin_diffuse.png", "CorpoOcupado_Pele_BaseMap.png"), ("Aksel_Skin_NRM.png", "CorpoOcupado_Pele_Normal.png")):
        shutil.copyfile(d + "/" + a, out + "/Texturas/" + b)
    print("EXPORT_JOGO", json.dumps(stats), flush=True)

def main():
    if "base" in STEPS: base(); bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    else: bpy.ops.wm.open_mainfile(filepath=BLEND)
    if "lencol" in STEPS:
        for k in list(LENCOL):
            if os.environ.get("L_" + k): LENCOL[k] = float(os.environ["L_" + k])
        sheet(); check_sheet(); bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    if "detalhes" in STEPS: details(); bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    if "jogo" in STEPS: game_version(); bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    if "p08" in STEPS: p08(); bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    if "exportar" in STEPS: export()
    if "exportar_jogo" in STEPS: export_game()
    if "render" in STEPS or "maos" in STEPS or "detalhes" in STEPS or "comparar" in STEPS or "p08" in STEPS or "pele" in STEPS or "comlencol" in STEPS: render(); bpy.ops.wm.save_as_mainfile(filepath=BLEND)

if __name__ == "__main__": main()

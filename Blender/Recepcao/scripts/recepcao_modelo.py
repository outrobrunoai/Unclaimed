# Procedural models of the reception furniture and fittings (counter, chairs, beam seating, cabinet,
# desk equipment, register book, water cooler, pedal bin).
# Units are metres. Each asset is built in its own local frame: origin on the floor at the centre of the
# footprint the map reserves for it, front (public / seated side) towards Blender -Y, Z up.
# Parts are separate closed meshes tagged with a material key; assemble() joins one game mesh per asset.
import bpy, bmesh, math
from mathutils import Vector, Matrix

# ---------------------------------------------------------------- shared dimensions

# Counter: keeps the 2.5 x 1.2 m footprint of the old placeholder (public face at y = -0.60).
# The body is 0.72 m deep; the rest of the footprint, up to the wall, is the clerk's floor space.
BALCAO = dict(length=2.5, height=1.10, front=-0.60, ledge_depth=0.28, ledge_t=0.03,
              panel_t=0.025, work_z=0.76, work_t=0.025, back=0.12, plinth=0.09, gable_t=0.025,
              pedestal_w=0.425, shelf_w=0.425)
# Waiting chair: fits the 0.6 x 0.6 m spots reserved in the waiting area.
ESPERA = dict(seat_w=0.47, seat_z=0.452, frame_x=0.215, tube_r=0.011, back_bottom=0.55, back_h=0.28,
              back_top_y=0.245, back_top_z=0.83)
# Clerk chair: armless task chair that tucks under the 0.735 m worktop underside.
ATENDENTE = dict(base_r=0.30, seat_z=0.51, seat_w=0.48, seat_d=0.47, back_w=0.44, back_h=0.47,
                 back_t=0.065, back_bottom=0.55, back_y=0.28, back_tilt=8.0)
# Support cabinet: low two-door unit in the counter's laminate.
ARMARIO = dict(width=0.90, depth=0.45, height=0.95, plinth=0.08, door_t=0.018, top_t=0.025)
# Three-seat beam seating (longarina) with the waiting chair's seat and backrest shells.
LONGARINA = dict(seats=3, pitch=0.53, beam_z=0.375, leg_x=0.62, foot_d=0.48)
# Desk kit, modelled in the counter's own frame so it shares the counter's origin and rotation:
# worktop surface at z = 0.76, usable between the riser (y = -0.32) and the staff edge (y = 0.12).
KIT = dict(work_z=0.76, monitor_x=0.15, monitor_y=-0.22, screen_w=0.53, screen_h=0.33, screen_t=0.035,
           screen_z=1.0, screen_tilt=6.0)
LIVRO = dict(w=0.22, d=0.32, t=0.035)
BEBEDOURO = dict(w=0.33, d=0.32, body_h=0.86, top_h=0.12)
LIXEIRA = dict(r_bottom=0.13, r_top=0.15, h=0.43)

ASSETS = ("BalcaoRecepcao", "CadeiraEspera", "CadeiraAtendente", "ArmarioApoio",
          "LongarinaEspera", "EquipamentosBalcao", "LivroRegistro", "Bebedouro", "Lixeira",
          "LuminariaSobrepor", "LuminariaSobrepor_Luz", "LuminariaExterna", "LuminariaExterna_Luz",
          "PortaVidroFixos", "PortaVidroFolha", "PortaVidroVidro", "BatenteEntrada")
# Not baked: parts keep their own UVs and material slots (diffusers, emissive tubes, glass).
UNBAKED = ("LuminariaSobrepor_Luz", "LuminariaExterna_Luz", "PortaVidroVidro")


# ---------------------------------------------------------------- mesh helpers

def _coll(asset):
    name = asset + " - partes"
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    return c


_current = {"asset": None}


def _bevel(bm, r, seg=2, min_angle=30.0):
    if r <= 0:
        return
    edges = [e for e in bm.edges if len(e.link_faces) == 2 and
             e.calc_face_angle(0.0) > math.radians(min_angle)]
    if edges:
        bmesh.ops.bevel(bm, geom=edges, offset=r, offset_type="OFFSET", profile_type="SUPERELLIPSE",
                        segments=seg, profile=0.5, affect="EDGES", clamp_overlap=True)


def _finish(name, bm, mat):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    ob["mat"] = mat
    _coll(_current["asset"]).objects.link(ob)
    return ob


def box(name, mat, x0, y0, z0, x1, y1, z1, r=0.003, seg=2):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector(((x0 + x1) / 2 + v.co.x * abs(x1 - x0), (y0 + y1) / 2 + v.co.y * abs(y1 - y0),
                       (z0 + z1) / 2 + v.co.z * abs(z1 - z0)))
    _bevel(bm, min(r, 0.45 * min(abs(x1 - x0), abs(y1 - y0), abs(z1 - z0))), seg)
    return _finish(name, bm, mat)


def extrude(name, mat, poly, plane, d0, d1, r=0.003, seg=2, min_angle=30.0, matrix=None):
    """Prism of a 2D polygon. plane 'YZ': poly in (y, z), extruded along x from d0 to d1;
    'XY': poly in (x, y) along z; 'XZ': poly in (x, z) along y."""
    def P(a, b, d):
        return {"YZ": (d, a, b), "XY": (a, b, d), "XZ": (a, d, b)}[plane]
    bm = bmesh.new()
    lo = [bm.verts.new(P(a, b, d0)) for a, b in poly]
    hi = [bm.verts.new(P(a, b, d1)) for a, b in poly]
    bm.faces.new(lo)
    bm.faces.new(list(reversed(hi)))
    n = len(poly)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])   # the bevel needs outward faces
    _bevel(bm, r, seg, min_angle)
    if matrix is not None:
        bmesh.ops.transform(bm, matrix=matrix, verts=bm.verts[:])
    return _finish(name, bm, mat)


def hull(name, mat, pts, r=0.003, seg=2, matrix=None):
    bm = bmesh.new()
    for p in pts:
        bm.verts.new(p)
    bmesh.ops.convex_hull(bm, input=bm.verts[:])   # every input point is a corner of the hull
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(1), verts=bm.verts[:], edges=bm.edges[:])
    _bevel(bm, r, seg)
    if matrix is not None:
        bmesh.ops.transform(bm, matrix=matrix, verts=bm.verts[:])
    return _finish(name, bm, mat)


def cyl(name, mat, center, radius, depth, axis=(0, 0, 1), seg=16, r=0.002, radius_top=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=radius,
                          radius2=radius if radius_top is None else radius_top, depth=depth)
    _bevel(bm, min(r, radius * 0.4, depth * 0.4), 2, 50.0)
    rot = Vector((0, 0, 1)).rotation_difference(Vector(axis)).to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ rot, verts=bm.verts[:])
    return _finish(name, bm, mat)


def _fillet(pts, radius, steps=6):
    pts = [Vector(p) for p in pts]
    if radius <= 0 or len(pts) < 3:
        return pts
    out = [pts[0]]
    for i in range(1, len(pts) - 1):
        a, b, c = pts[i - 1], pts[i], pts[i + 1]
        u, w = (a - b).normalized(), (c - b).normalized()
        ang = u.angle(w)
        if ang > math.radians(175):
            out.append(b)
            continue
        t = min(radius / math.tan(ang / 2), (a - b).length * 0.45, (c - b).length * 0.45)
        p0, p1 = b + u * t, b + w * t
        for s in range(steps + 1):   # quadratic Bezier through the corner: close enough to an arc
            k = s / steps
            out.append(p0 * (1 - k) ** 2 + b * 2 * k * (1 - k) + p1 * k * k)
    out.append(pts[-1])
    return out


def tube(name, mat, pts, radius, fillet=0.0, res=3):
    """Round tube along a polyline with filleted corners and flat caps (bent steel tubing, handles)."""
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = radius
    cu.bevel_resolution = res
    cu.use_fill_caps = True
    cu.twist_mode = "MINIMUM"
    sp = cu.splines.new("POLY")
    path = _fillet(pts, fillet)
    sp.points.add(len(path) - 1)
    for p, q in zip(sp.points, path):
        p.co = (q.x, q.y, q.z, 1.0)
    tmp = bpy.data.objects.new(name + "_curva", cu)
    bpy.context.scene.collection.objects.link(tmp)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    bm = bmesh.new()
    bm.from_mesh(me)
    bpy.data.meshes.remove(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    return _finish(name, bm, mat)


def curved_panel(name, mat, width, height, thick, radius, r, seg=2, nx=12):
    """Panel bent around a vertical axis (edges come forward, towards -Y); back face centre at y = 0,
    bottom at z = 0."""
    xs = [-width / 2 + width * i / nx for i in range(nx + 1)]
    sag = lambda x: -(radius - math.sqrt(radius * radius - x * x))
    front = [(x, sag(x) - thick) for x in xs]
    back = [(x, sag(x)) for x in reversed(xs)]
    return extrude(name, mat, front + back, "XY", 0.0, height, r, seg, min_angle=20.0)


def place(ob, tilt_deg=0.0, pivot=(0, 0, 0), offset=(0, 0, 0)):
    """Tilts a part about X (positive tips its top towards +Y) around pivot, then moves it."""
    m = Matrix.Translation(Vector(offset) + Vector(pivot)) @ Matrix.Rotation(math.radians(-tilt_deg), 4, "X") \
        @ Matrix.Translation(-Vector(pivot))
    ob.data.transform(m)
    return ob


# ---------------------------------------------------------------- clerk's desk details

# Cable grommet in the worktop, at its back edge (counter frame, shared by the counter and the desk kit):
# 60 mm hole with a flanged ring, a sleeve through the top and a cap whose slot (40 mm) faces where the
# cables come from (slot_dir: degrees from +X towards +Y).
GROMMET = dict(x=-0.10, y=-0.27, hole_r=0.030, flange_r=0.036, cap_slot=0.040, slot_dir=0.0)


def cut(ob, cutter):
    """Boolean difference applied to a part; the cutter is deleted."""
    mod = ob.modifiers.new("corte", "BOOLEAN")
    mod.operation, mod.solver, mod.object = "DIFFERENCE", "EXACT", cutter
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    me = cutter.data
    bpy.data.objects.remove(cutter)
    bpy.data.meshes.remove(me)
    return ob


def lathe(name, mat, profile, seg=32, r=0.0, min_angle=30.0, matrix=None):
    """Solid of revolution about Z from a closed (radius, z) profile listed outside-in."""
    bm = bmesh.new()
    verts = [bm.verts.new((rr, 0.0, z)) for rr, z in profile]
    edges = [bm.edges.new((verts[i], verts[(i + 1) % len(verts)])) for i in range(len(verts))]   # closed
    bmesh.ops.spin(bm, geom=verts + edges, cent=(0, 0, 0), axis=(0, 0, 1), angle=2 * math.pi, steps=seg,
                   use_duplicate=False)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    _bevel(bm, r, 2, min_angle)
    if matrix is not None:
        bmesh.ops.transform(bm, matrix=matrix, verts=bm.verts[:])
    return _finish(name, bm, mat)


def grommet(wz, wt):
    """Flanged ring on the worktop, sleeve lining the hole, slotted cap (counter part)."""
    G = GROMMET
    c = Matrix.Translation((G["x"], G["y"], 0.0))
    ri, rf = G["hole_r"], G["flange_r"]
    # Flange on the worktop, inner ledge carrying the cap, sleeve down through the top.
    lathe("Aro do passa-cabos", "nylon", [(rf, wz), (rf, wz + 0.0022), (ri - 0.002, wz + 0.003), (ri - 0.002, wz + 0.001),
                                          (ri - 0.0035, wz + 0.001), (ri - 0.0035, wz - 0.001), (ri - 0.002, wz - 0.001),
                                          (ri - 0.002, wz - wt - 0.004), (ri - 0.0005, wz - wt - 0.004), (ri - 0.0005, wz)],
          seg=40, r=0.0006, matrix=c)
    cap = lathe("Tampa do passa-cabos", "nylon", [(ri - 0.0026, wz + 0.0012), (ri - 0.0026, wz + 0.0028), (0.0, wz + 0.0028),
                                                  (0.0, wz + 0.0012)], seg=40, r=0.0005, matrix=c)
    slot = box("__slot", "nylon", ri - 0.017, -G["cap_slot"] / 2, wz, ri + 0.01, G["cap_slot"] / 2, wz + 0.004, r=0)
    slot.data.transform(c @ Matrix.Rotation(math.radians(G["slot_dir"]), 4, "Z"))
    cut(cap, slot)


def smooth_path(points, per_seg=10):
    """Catmull-Rom curve through the points (no corners): cables and cords."""
    P = [Vector(p) for p in points]
    P = [P[0] * 2 - P[1]] + P + [P[-1] * 2 - P[-2]]
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for s in range(per_seg):
            t = s / per_seg
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t +
                              (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t))
    out.append(P[-2])
    return out


def cable(name, mat, points, radius, per_seg=10):
    return tube(name, mat, smooth_path(points, per_seg), radius, res=2)


def coil(name, mat, points, coil_r=0.0036, wire_r=0.0011, pitch=0.0042):
    """Coiled telephone cord: a helix around a smooth path, sampled every 1/12 of a turn."""
    path = smooth_path(points, 24)
    step = pitch / 12
    dense = [path[0]]
    for a, b in zip(path, path[1:]):
        k = max(1, int((b - a).length / step))
        dense += [a.lerp(b, i / k) for i in range(1, k + 1)]
    pts, prev_n = [], None
    for i, p in enumerate(dense):
        t = (dense[min(i + 1, len(dense) - 1)] - dense[max(i - 1, 0)]).normalized()
        n = (prev_n - t * prev_n.dot(t)).normalized() if prev_n else t.orthogonal().normalized()   # parallel transport
        prev_n = n
        theta = i * 2 * math.pi / 12
        pts.append(p + (n * math.cos(theta) + t.cross(n) * math.sin(theta)) * coil_r)
    return tube(name, mat, pts, wire_r, res=2)


def pen(prefix, m, body, cap, capped=True):
    """Ballpoint pen along +Z from its end plug (z = 0) to the tip: hexagonal barrel, white cone with a
    metal point and, if capped, a round cap with a pocket clip over the tip. 140 mm capped."""
    parts = [cyl(prefix + " - tampinha", cap, (0, 0, 0.002), 0.0031, 0.004, seg=12, r=0.0006),
             cyl(prefix + " - corpo", body, (0, 0, 0.0605), 0.0043, 0.115, seg=6, r=0.0005),
             cyl(prefix + " - cone", "ponta_plastico", (0, 0, 0.1255), 0.0034, 0.015, seg=12, r=0.0004, radius_top=0.0011),
             cyl(prefix + " - ponta", "ponta", (0, 0, 0.1345), 0.0007, 0.003, seg=8, r=0.0001, radius_top=0.0003)]
    if capped:
        parts += [cyl(prefix + " - tampa", cap, (0, 0, 0.122), 0.0049, 0.036, seg=14, r=0.0012),
                  box(prefix + " - clipe", cap, 0.0046, -0.0013, 0.110, 0.0062, 0.0013, 0.139, r=0.0005, seg=1)]
    for p in parts:
        p.data.transform(m)
    return parts


def pencil(prefix, m):
    """Yellow hexagonal pencil along +Z, sharpened at the top: 183 mm."""
    for p in (cyl(prefix + " - corpo", "lapis", (0, 0, 0.0825), 0.0042, 0.165, seg=6, r=0.0005),
              cyl(prefix + " - madeira", "madeira", (0, 0, 0.1725), 0.0040, 0.015, seg=12, r=0.0002, radius_top=0.0011),
              cyl(prefix + " - grafite", "grafite", (0, 0, 0.1815), 0.0011, 0.003, seg=8, r=0.0001, radius_top=0.0002)):
        p.data.transform(m)


def along(base, end, length=None, flip=False):
    """Matrix placing an object built along +Z at `base`, pointing to `end` (flip: its +Z end at base)."""
    b, e = Vector(base), Vector(end)
    d = (e - b).normalized()
    if flip:
        return Matrix.Translation(b + d * length) @ (-d).to_track_quat("Z", "Y").to_matrix().to_4x4()
    return Matrix.Translation(b) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4()


def pen_holder(cx, cy, wz):
    """Matt plastic cup (72 mm, 100 mm tall, open, 2 mm wall) with three pens and a pencil resting on its
    floor and leaning on the rim (positions searched so nothing crosses the wall or a neighbour)."""
    R, t, H, fl = 0.036, 0.0022, 0.100, 0.004
    lathe("Porta-canetas", "porta_canetas", [(R, 0.0), (R, H), (R - t, H), (R - t, fl), (0.0, fl), (0.0, 0.0)],
          seg=36, r=0.0009, min_angle=40.0, matrix=Matrix.Translation((cx, cy, wz)))
    o = Vector((cx, cy, wz))
    items = (((-0.0212, 0.0007, 0.0048), (-0.0253, 0.0230, 0.1429), "caneta_azul", "caneta_azul", False),
             ((0.0157, -0.0004, 0.0049), (0.0289, 0.0215, 0.1425), "caneta_corpo", "caneta_preta", True),
             ((-0.0010, -0.0176, 0.0054), (0.0390, -0.0046, 0.1390), "caneta_corpo", "caneta_vermelha", False))
    for k, (b, e, body, cap, tip_down) in enumerate(items):
        pen(f"Caneta {k + 1}", along(o + Vector(b), o + Vector(e), 0.140, flip=tip_down), body, cap)
    pencil("Lapis", along(o + Vector((-0.0017, 0.0147, 0.0047)), o + Vector((-0.0034, 0.0424, 0.1855))))


def office_mouse(cx, cy, z0):
    """Two-button office mouse after common wired models (refs in referencias.md): 115 x 62 x 37 mm,
    rounded plan wider at the palm, flat base, highest about 60 % back; buttons over the front 42 %
    split by a clean central gap, wheel in a slot between them, cable leaving the front centre. Front
    (buttons, cable) towards -Y, the monitor."""
    L, W, H, lip = 0.115, 0.062, 0.037, 0.0035
    base = z0 + lip
    height = lambda t: H * (0.45 + 0.55 * math.sin(math.pi * (0.12 + 0.62 * t)))   # profile, t: 0 front .. 1 back

    def shell(scale=1.0, res=(72, 36)):
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=res[0], v_segments=res[1], radius=1.0)
        for v in bm.verts:
            x, y, z = v.co
            t = (y + 1) / 2                                   # 0 front .. 1 back
            ang = math.atan2(y, x)
            sq = (abs(math.cos(ang)) ** 2.6 + abs(math.sin(ang)) ** 2.6) ** (-1 / 2.6)   # squarer plan, blunt ends
            rxy = math.hypot(x, y) * sq
            w = W / 2 * (0.86 + 0.14 * math.sin(math.pi * (0.15 + 0.65 * t))) * scale
            hz = height(t) * scale
            zz = math.copysign(abs(z) ** 0.85, z)
            v.co = Vector((cx + rxy * math.cos(ang) * w, cy + rxy * math.sin(ang) * L / 2 * scale,
                           base + (zz * hz if z > 0 else z * lip)))
        return bm

    _finish("Mouse", shell(0.975, (48, 24)), "mouse")
    split = cy - L * 0.08                                   # buttons: front 42 %
    wy = cy - L * 0.25                                      # wheel centre, 25 % from the front
    for side, sx in (("esquerdo", 1), ("direito", -1)):
        bm = shell()
        for co, no in (((cx, split, 0), (0, 1, 0)), ((cx + sx * 0.0007, 0, 0), (-sx, 0, 0)),
                       ((0, 0, base + H * 0.45), (0, 0, -1))):
            bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=co, plane_no=no,
                                   clear_outer=True)
        bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.0016)
        btn = _finish(f"Botao {side} do mouse", bm, "mouse")
        cut(btn, box("__fenda", "mouse", cx - 0.0048, wy - 0.0135, base, cx + 0.0048, wy + 0.0135, base + 0.06, r=0))
    cyl("Roda do mouse", "borracha", (cx, wy, base + height(0.25) - 0.0105 + 0.003), 0.0105, 0.0062, axis=(1, 0, 0),
        seg=24, r=0.0012)                                   # 3 mm above the buttons
    cyl("Saida do cabo do mouse", "mouse", (cx, cy - L / 2 + 0.002, base + 0.008), 0.0034, 0.010, axis=(0, 1, 0),
        seg=12, r=0.0008)
    return Vector((cx, cy - L / 2 - 0.003, base + 0.008))   # where the cable leaves


# ---------------------------------------------------------------- desk equipment parts

KEY_U = 0.019          # key pitch
KEY_GAP = 0.0035       # gap between neighbouring keycaps


def _keycap(name, x, y, w_units, h_units, matrix):
    """One slightly tapered keycap, 6.5 mm tall, whose far-left corner is at (x, y) key units on the plate
    (x to the user's right, y towards the user); `matrix` places the plate."""
    u, g, t = KEY_U, KEY_GAP, 0.0012
    x0, x1 = x * u + g / 2, (x + w_units) * u - g / 2
    y0, y1 = -(y + h_units) * u + g / 2, -y * u - g / 2
    pts = [(a, b, 0.0) for a in (x0, x1) for b in (y0, y1)] + \
          [(a, b, 0.0065) for a in (x0 + t, x1 - t) for b in (y0 + t, y1 - t)]
    return hull(name, "teclado", pts, r=0.0009, seg=1, matrix=matrix)


def keyboard(cx, cy, wz):
    """Full-size keyboard: case with a slight rake and the usual key groups (function row in fours, main
    block with wide modifiers and a long space bar, navigation block, inverted-T arrows, numeric pad)."""
    W, D, front_h, back_h = 0.445, 0.145, 0.018, 0.026
    hull("Teclado", "plastico", [(cx + x, cy + y, wz + h) for x in (-W / 2, W / 2)
                                 for y, h in ((-D / 2, 0.0), (-D / 2, back_h), (D / 2, 0.0), (D / 2, front_h))],
         r=0.004)
    rake = math.atan((back_h - front_h) / D)
    field_w, field_d = 22.75 * KEY_U, 6.5 * KEY_U
    # Plate frame: origin at the far-left corner of the key field as the user (at +Y, facing -Y) sees it;
    # turned 180 degrees so plate x runs to the user's right (-X) and plate -y towards the user (+Y).
    origin = Vector((cx + field_w / 2, cy - field_d / 2 + 0.001, wz + back_h - 0.0012))
    plate = Matrix.Translation(origin) @ Matrix.Rotation(-rake, 4, "X") @ Matrix.Rotation(math.pi, 4, "Z")
    n = [0]

    def key(x, y, w=1.0, h=1.0):
        n[0] += 1
        _keycap(f"Tecla {n[0]}", x, y, w, h, plate)

    key(0, 0)                                              # Esc, F1-F12, Print/Scroll/Pause
    for g0 in (2.0, 6.5, 11.0):
        for i in range(4):
            key(g0 + i, 0)
    for i in range(3):
        key(15.25 + i, 0)
    rows = [[1] * 13 + [2.0],                              # numbers, backspace
            [1.5] + [1] * 12 + [1.5],                      # tab ... backslash
            [1.75] + [1] * 11 + [2.25],                    # caps ... enter
            [2.25] + [1] * 10 + [2.75],                    # shifts
            [1.25, 1.25, 1.25, 6.25, 1.25, 1.25, 1.25, 1.25]]   # ctrl, win, alt, space, alt gr, win, menu, ctrl
    for r, widths in enumerate(rows):
        x = 0.0
        for w in widths:
            key(x, 1.5 + r, w)
            x += w
    for r in range(2):                                     # insert/home/page up, delete/end/page down
        for i in range(3):
            key(15.25 + i, 1.5 + r)
    key(16.25, 4.5)                                        # arrows
    for i in range(3):
        key(15.25 + i, 5.5)
    for r in range(4):                                     # numeric pad: tall + and Enter, wide 0
        for i in range(3):
            key(18.75 + i, 1.5 + r)
    key(21.75, 1.5)
    key(21.75, 2.5, 1, 2)
    key(21.75, 4.5, 1, 2)
    key(18.75, 5.5, 2)
    key(20.75, 5.5)


def mouse(cx, cy, z0):
    """Two-button office mouse: body shell, two button shells split by a gap, rubber scroll wheel.
    Buttons towards -Y (away from the user), wider and higher towards the palm."""
    L, Wd, H, lip = 0.112, 0.062, 0.036, 0.004
    base = z0 + lip

    def shell(scale=1.0):
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=16, radius=1.0)
        for v in bm.verts:
            x, y, z = v.co
            t = (y + 1) / 2                                # 0 front .. 1 back
            w = Wd / 2 * (0.86 + 0.14 * math.sin(math.pi * min(1.0, t * 1.15))) * scale
            hz = H * (0.62 + 0.38 * math.sin(math.pi * (0.25 + 0.6 * t))) * scale
            v.co = Vector((cx + x * w, cy + y * L / 2 * scale, base + (z * hz if z > 0 else z * lip)))
        return bm

    _finish("Mouse", shell(0.97), "plastico")   # sits under the buttons: the gaps read as grooves
    split_y = cy - L * 0.08                                # the buttons cover the front 42 %
    for side, sx in (("esquerdo", 1), ("direito", -1)):
        bm = shell()
        for co, no in (((cx, split_y, 0), (0, 1, 0)), ((cx + sx * 0.0016, 0, 0), (-sx, 0, 0)),
                       ((0, 0, base + H * 0.42), (0, 0, -1))):
            bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=co,
                                   plane_no=no, clear_outer=True)
        bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.0015)
        _finish(f"Botao {side} do mouse", bm, "plastico")
    cyl("Roda do mouse", "borracha", (cx, split_y - 0.017, base + H * 0.80), 0.009, 0.0055, axis=(1, 0, 0),
        seg=20, r=0.001)


def phone(m):
    """Desk phone in frame m (user at +Y): sloped base; display, 3 x 4 keypad of separate keys and a
    column of function keys on the user's right (-X); cradle with two hook pads and the handset lying on
    them on the user's left (+X)."""
    hull("Telefone", "plastico", [(x, y, h) for x in (-0.09, 0.09) for y, h in ((-0.10, 0.0), (-0.10, 0.072),
                                                                               (0.10, 0.0), (0.10, 0.034))],
         r=0.007, matrix=m)
    slope = math.atan((0.072 - 0.034) / 0.20)
    top = m @ Matrix.Translation((0, 0, 0.053)) @ Matrix.Rotation(-slope, 4, "X")   # on the sloped face

    def on_top(ob):
        ob.data.transform(top)

    on_top(box("Visor do telefone", "visor", -0.075, -0.052, -0.001, -0.010, -0.018, 0.0018, r=0.001))
    for r in range(4):
        for c in range(3):
            x, y = -0.052 + c * 0.018, 0.0 + r * 0.0175
            on_top(box(f"Tecla do telefone {r * 3 + c + 1}", "teclado", x - 0.0065, y - 0.005, -0.001,
                       x + 0.0065, y + 0.005, 0.0045, r=0.0012, seg=1))
    for k in range(4):
        y = 0.0 + k * 0.0175
        on_top(box(f"Tecla de funcao {k + 1}", "teclado", -0.077, y - 0.004, -0.001, -0.067, y + 0.004, 0.004,
                   r=0.001, seg=1))
    hx = 0.05
    on_top(box("Berco do telefone", "plastico", hx - 0.03, -0.097, -0.002, hx + 0.032, 0.097, 0.006, r=0.003))
    for y in (-0.072, 0.072):
        on_top(box("Apoio do fone", "plastico", hx - 0.016, y - 0.012, 0.005, hx + 0.016, y + 0.012, 0.013,
                   r=0.002))
    hz = 0.013
    for name, y in (("Auricular", -0.072), ("Bocal", 0.072)):
        hull(name, "plastico", [(hx + sx * 0.024, y + sy * 0.025, hz + h) for sx in (-1, 1) for sy in (-1, 1)
                                for h in (0.0, 0.024)], r=0.009, matrix=top)
    hull("Fone", "plastico", [(hx + sx * 0.018, y, hz + h) for sx in (-1, 1) for y in (-0.07, 0.07)
                              for h in (0.008, 0.026)], r=0.007, matrix=top)


# ---------------------------------------------------------------- assets

def build_balcao():
    P = BALCAO
    W, F, H = P["length"] / 2, P["front"], P["height"]
    g, pt, wz, wt, B = P["gable_t"], P["panel_t"], P["work_z"], P["work_t"], P["back"]
    face = F + 0.025                    # public panels sit 25 mm behind the ledge nose
    riser = F + P["ledge_depth"]        # back of the ledge, where the staff-side riser stands
    ledge_z = H - P["ledge_t"]
    inner = W - g
    box("Tampo de granito", "granito", -W, F, ledge_z, W, riser, H, r=0.006, seg=3)
    gable = [(face, 0.0), (B, 0.0), (B, wz), (riser, wz), (riser, ledge_z), (face, ledge_z)]
    extrude("Lateral esquerda", "laminado", gable, "YZ", -W, -inner, r=0.003)
    extrude("Lateral direita", "laminado", gable, "YZ", inner, W, r=0.003)
    # Three public panels split by shadow gaps, over a recessed dark plinth.
    gap, n = 0.006, 3
    span = (2 * inner - (n - 1) * gap) / n
    for i in range(n):
        x0 = -inner + i * (span + gap)
        box(f"Painel frontal {i + 1}", "laminado", x0, face, P["plinth"], x0 + span, face + pt, ledge_z, r=0.002)
    box("Fundo dos paineis", "laminado", -inner, face + pt, P["plinth"], inner, face + pt + 0.015, ledge_z, r=0)
    # Recessed dark plinth from gable to gable. Everything behind it at floor level (shelf base, divider,
    # pedestal plinth) starts at its back face, so no light laminate shows in front of it.
    plinth_back = face + 0.065
    box("Rodape frontal", "rodape", -inner, face + 0.045, 0.0, inner, plinth_back, P["plinth"] + 0.01, r=0.002)
    box("Espelho do atendente", "laminado", -inner, riser - 0.02, wz, inner, riser, ledge_z, r=0.002)
    top = box("Tampo de trabalho", "laminado", -inner, face + pt + 0.015, wz - wt, inner, B, wz, r=0.005, seg=3)
    G = GROMMET   # cable grommet at the back of the worktop: a real hole, lined and capped
    cut(top, cyl("__furo", "laminado", (G["x"], G["y"], wz - wt / 2), G["hole_r"], wt + 0.02, seg=40, r=0))
    grommet(wz, wt)
    # Staff side: drawer pedestal at +X, open shelf for binders at -X, knee space between them.
    px = inner - P["pedestal_w"]
    box("Gaveteiro", "laminado", px, face + 0.05, P["plinth"], inner, B - 0.02, wz - wt, r=0.002)
    box("Rodape do gaveteiro", "rodape", px, plinth_back, 0.0, inner, B - 0.05, P["plinth"], r=0.002)
    rows = ((P["plinth"] + 0.005, 0.30), (0.305, 0.51), (0.515, wz - wt - 0.008))
    for i, (z0, z1) in enumerate(rows):
        box(f"Frente de gaveta {i + 1}", "laminado", px + 0.004, B - 0.02, z0, inner - 0.004, B - 0.002, z1, r=0.0015)
        hx, hz, cc = (px + inner) / 2, z1 - 0.045, 0.128
        tube(f"Puxador de gaveta {i + 1}", "aluminio",
             [(hx - cc / 2, B - 0.003, hz), (hx - cc / 2, B + 0.025, hz), (hx + cc / 2, B + 0.025, hz),
              (hx + cc / 2, B - 0.003, hz)], 0.0055, fillet=0.01)
    sx = -inner + P["shelf_w"]
    box("Divisoria", "laminado", sx, plinth_back, 0.0, sx + 0.018, B - 0.01, wz - wt, r=0.002)
    box("Prateleira", "laminado", -inner, face + pt + 0.015, 0.36, sx, B - 0.03, 0.378, r=0.002)
    box("Base da estante", "laminado", -inner, plinth_back, 0.0, sx, B - 0.01, 0.07, r=0.002)


def build_espera():
    P = ESPERA
    sx, tr, sz = P["frame_x"], P["tube_r"], P["seat_z"]
    rail = sz - 0.018 - tr - 0.003
    for side, x in (("esquerda", -sx), ("direita", sx)):
        tube(f"Quadro {side}", "epoxi", [(x, -0.21, 0.013), (x, -0.20, rail), (x, 0.17, rail), (x, 0.235, 0.013)],
             tr, fillet=0.035)
        tube(f"Montante {side}", "epoxi", [(x, 0.165, rail - 0.01), (x, P["back_top_y"], P["back_top_z"])], tr)
        for y in (-0.21, 0.235):
            cyl(f"Ponteira {side} {y:+.2f}", "borracha", (x, y, 0.008), 0.0135, 0.016, seg=12, r=0.002)
    for name, y in (("Travessa frontal", -0.17), ("Travessa traseira", 0.15)):
        tube(name, "epoxi", [(-sx, y, rail), (sx, y, rail)], tr * 0.9)
    seat_shell("Assento", 0.0)
    # Backrest bolted to the front of the two posts, leaning back with them.
    tilt = math.degrees(math.atan2(P["back_top_y"] - 0.165, P["back_top_z"] - rail + 0.01))
    post_y = lambda z: 0.165 + (z - rail + 0.01) * math.tan(math.radians(tilt))
    sag = BACK_R - math.sqrt(BACK_R * BACK_R - sx * sx)
    back_shell("Encosto", 0.0, tilt, post_y(P["back_bottom"]) - tr - 0.002 + sag)


# Seat and backrest shells shared by the waiting chair and the beam seating.
BACK_R = 1.2


def seat_shell(name, cx):
    sz, w = ESPERA["seat_z"], ESPERA["seat_w"] / 2
    seat = [(0.205, sz), (-0.15, sz), (-0.205, sz - 0.008), (-0.228, sz - 0.03), (-0.214, sz - 0.036),
            (-0.19, sz - 0.022), (-0.15, sz - 0.018), (0.205, sz - 0.018)]
    return extrude(name, "polipropileno", seat, "YZ", cx - w, cx + w, r=0.006, seg=2, min_angle=20.0)


def back_shell(name, cx, tilt, back_y):
    """Curved backrest; back_y is where its back face sits (centre line) at its bottom edge."""
    back = curved_panel(name, "polipropileno", 0.44, ESPERA["back_h"], 0.016, BACK_R, r=0.006)
    return place(back, tilt, offset=(cx, back_y, ESPERA["back_bottom"]))


def build_atendente():
    P = ATENDENTE
    R = P["base_r"]
    cyl("Cubo da base", "nylon", (0, 0, 0.085), 0.042, 0.07, seg=20, r=0.006)
    for i in range(5):
        a = math.radians(90 + 72 * i)
        m = Matrix.Rotation(a, 4, "Z")
        hull(f"Pe {i + 1}", "nylon",
             [(0.02, -0.024, 0.068), (0.02, 0.024, 0.068), (0.02, -0.024, 0.118), (0.02, 0.024, 0.118),
              (R, -0.016, 0.072), (R, 0.016, 0.072), (R, -0.016, 0.098), (R, 0.016, 0.098)], r=0.005, matrix=m)
        hull(f"Garfo do rodizio {i + 1}", "nylon",
             [(R - 0.03, -0.022, 0.07), (R - 0.03, 0.022, 0.07), (R + 0.03, -0.022, 0.05), (R + 0.03, 0.022, 0.05),
              (R - 0.03, -0.022, 0.035), (R - 0.03, 0.022, 0.035), (R + 0.02, -0.022, 0.022), (R + 0.02, 0.022, 0.022)],
             r=0.006, matrix=m)
        c = m @ Vector((R + 0.012, 0, 0.026))
        cyl(f"Rodas {i + 1}", "nylon", c, 0.026, 0.05, axis=tuple(m @ Vector((0, 1, 0))), seg=18, r=0.005)
    cyl("Capa do pistao", "nylon", (0, 0, 0.21), 0.027, 0.20, seg=20, r=0.003)
    cyl("Pistao", "cromo", (0, 0, 0.345), 0.014, 0.09, seg=16, r=0.001)
    box("Mecanismo", "nylon", -0.10, -0.12, 0.385, 0.10, 0.12, 0.425, r=0.008)
    tube("Alavanca", "nylon", [(0.09, -0.05, 0.40), (0.17, -0.07, 0.40), (0.215, -0.12, 0.395)], 0.007, fillet=0.03)
    sw, sd, sz = P["seat_w"] / 2, P["seat_d"] / 2, P["seat_z"]
    box("Concha do assento", "nylon", -sw + 0.01, -sd + 0.01, 0.42, sw - 0.01, sd - 0.01, 0.445, r=0.01)
    box("Estofado do assento", "tecido", -sw, -sd, 0.44, sw, sd, sz, r=0.03, seg=4)
    by, bb = P["back_y"], P["back_bottom"]
    box("Haste horizontal", "nylon", -0.03, 0.10, 0.39, 0.03, by + 0.035, 0.415, r=0.006)
    haste = box("Haste do encosto", "nylon", -0.03, by + 0.015, 0.39, 0.03, by + 0.035, bb + 0.22, r=0.006)
    back = curved_panel("Estofado do encosto", "tecido", P["back_w"], P["back_h"], P["back_t"], 0.8, r=0.025, seg=4)
    place(back, P["back_tilt"], offset=(0, by + 0.012, bb))
    place(haste, P["back_tilt"], pivot=(0, by, 0.40))


def build_armario():
    P = ARMARIO
    W, D, H = P["width"] / 2, P["depth"] / 2, P["height"]
    tt, dt, pl, s = P["top_t"], P["door_t"], P["plinth"], 0.018
    box("Tampo", "laminado", -W, -D, H - tt, W, D, H, r=0.004, seg=3)
    for side, x0 in (("esquerda", -W), ("direita", W - s)):
        box(f"Lateral {side}", "laminado", x0, -D + dt, 0.0, x0 + s, D, H - tt, r=0.002)
    box("Fundo", "laminado", -W + s, D - 0.02, pl, W - s, D - 0.002, H - tt, r=0)
    box("Base", "laminado", -W + s, -D + dt + 0.02, pl, W - s, D - 0.02, pl + s, r=0)
    box("Rodape", "rodape", -W + s, -D + 0.04, 0.0, W - s, -D + 0.06, pl, r=0.002)
    gap = 0.003
    for side, (x0, x1), hx in (("esquerda", (-W + 0.0015, -gap / 2), -0.035), ("direita", (gap / 2, W - 0.0015), 0.035)):
        box(f"Porta {side}", "laminado", x0, -D, pl + 0.003, x1, -D + dt, H - tt - 0.003, r=0.0015)
        hz, cc = 0.62, 0.128
        tube(f"Puxador {side}", "aluminio",
             [(hx, -D + 0.001, hz - cc / 2), (hx, -D - 0.028, hz - cc / 2), (hx, -D - 0.028, hz + cc / 2),
              (hx, -D + 0.001, hz + cc / 2)], 0.0055, fillet=0.01)


def build_longarina():
    """Beam seating: steel beam on two T-legs, a flat bracket under each seat and a tilted back arm."""
    P = LONGARINA
    n, pitch, bz = P["seats"], P["pitch"], P["beam_z"]
    half = (n - 1) * pitch / 2 + 0.26
    sz = ESPERA["seat_z"] - 0.018
    box("Viga", "epoxi", -half + 0.02, -0.005, bz - 0.03, half - 0.02, 0.045, bz, r=0.004)
    for side, x in (("esquerda", -P["leg_x"]), ("direita", P["leg_x"])):
        box(f"Coluna {side}", "epoxi", x - 0.025, 0.0, 0.035, x + 0.025, 0.04, bz - 0.03, r=0.004)
        fd = P["foot_d"] / 2
        box(f"Pe {side}", "epoxi", x - 0.03, -fd + 0.03, 0.012, x + 0.03, fd - 0.01, 0.045, r=0.008, seg=3)
        for y in (-fd + 0.05, fd - 0.03):
            cyl(f"Sapata {side} {y:+.2f}", "borracha", (x, y, 0.006), 0.02, 0.012, seg=12, r=0.002)
    tilt = 10.0
    for i in range(n):
        cx = (i - (n - 1) / 2) * pitch
        box(f"Suporte do assento {i + 1}", "epoxi", cx - 0.02, -0.17, bz, cx + 0.02, 0.19, sz - 0.002, r=0.003)
        # Back arm from the rear of the bracket, leaning back like the chair's posts.
        y0, z0 = 0.19, bz + 0.01
        z1 = ESPERA["back_bottom"] + 0.2
        y1 = y0 + (z1 - z0) * math.tan(math.radians(tilt))
        tube(f"Braco do encosto {i + 1}", "epoxi", [(cx, 0.15, bz + 0.015), (cx, y0, z0 + 0.02), (cx, y1, z1)],
             0.011, fillet=0.03)
        seat_shell(f"Assento {i + 1}", cx)
        by = y0 + (ESPERA["back_bottom"] - z0) * math.tan(math.radians(tilt)) - 0.013
        back_shell(f"Encosto {i + 1}", cx, tilt, by)


def desk_cables(K, B, wz, mouse_out, m_phone):
    """Cables of the desk kit. All go down through the worktop grommet (GROMMET), side by side in the
    cap's slot in a fixed order, to a thin client and a power strip fixed under the worktop:
    keyboard (out of its back edge, round the monitor base along the back of the desk), monitor power
    and video (out of the back of the screen beside the neck, down behind it, off the base), phone line
    cord (out of the back of the phone, along the front of the monitor base, over the keyboard cable),
    mouse (out of its front, a loose S of slack). The handset's coiled cord runs to the side of the base."""
    G = GROMMET
    c = Vector((G["x"], G["y"], 0.0))
    a = math.radians(G["slot_dir"])
    u, n = Vector((math.cos(a), math.sin(a), 0)), Vector((-math.sin(a), math.cos(a), 0))
    under = wz - B["work_t"]                              # underside of the worktop
    mx, my = K["monitor_x"], K["monitor_y"]

    def slot(d, r, drop=0.02):
        """From just outside the flange, over it and through the cap's slot (offset d across it), bending
        round the ring's inner ledge at its own radius, then down the hole to `drop` under the worktop
        (different depths keep the cables that cross under the desk apart)."""
        ledge = GROMMET["hole_r"] - 0.0035                # inner edge of the ring's ledge, at wz + 0.001
        def at(radial, z, k=1.0):
            a = math.sqrt(max(radial * radial - (d * k) ** 2, 1e-8))
            return tuple(c + u * a + n * d * k + Vector((0, 0, z)))
        return [at(0.062, wz + r, 1.3), at(0.045, wz + 0.0028 + r, 1.1), at(0.032, wz + 0.0032 + r),
                at(0.0278, wz + 0.0033 + r), at(ledge - 0.7 * r, wz + 0.0012 + 0.75 * r), at(ledge - r - 0.0004, wz - 0.0005),
                at(ledge - r - 0.0008, under - 0.012), tuple(c + u * 0.006 + n * d + Vector((0, 0, under - drop)))]

    # Under the worktop, at its back: thin client on two brackets (sockets on its front), power strip.
    pc = (G["x"] + 0.06, -0.315, G["x"] + 0.25, -0.13)
    box("Mini PC", "gabinete", pc[0], pc[1], under - 0.062, pc[2], pc[3], under - 0.008, r=0.004)
    for x in (pc[0] + 0.02, pc[2] - 0.02):
        box("Suporte do mini PC", "plastico", x - 0.012, pc[1] - 0.004, under - 0.066, x + 0.012, pc[3] + 0.004, under, r=0.0015)
    strip = (G["x"] - 0.30, -0.318, G["x"] - 0.04, -0.288)   # clear of the grommet's sleeve
    box("Regua de tomadas", "regua", strip[0], strip[1], under - 0.042, strip[2], strip[3], under, r=0.004)
    plugs = [strip[0] + 0.04 + i * 0.05 for i in range(4)]
    for i, x in enumerate(plugs):
        box(f"Plugue {i + 1}", "plastico", x - 0.011, strip[3], under - 0.032, x + 0.011, strip[3] + 0.022, under - 0.012, r=0.002)
    ports = [pc[0] + 0.035 + 0.04 * i for i in range(4)]
    for i, x in enumerate(ports):
        box(f"Conector {i + 1}", "plastico", x - 0.008, pc[3], under - 0.036, x + 0.008, pc[3] + 0.014, under - 0.024, r=0.0015)

    def to_plug(i):
        return [(G["x"] - 0.04 - 0.03 * (1 - i), -0.245 + 0.03 * (1 - i), under - 0.04), (plugs[i] + 0.006, strip[3] + 0.05, under - 0.03),
                (plugs[i], strip[3] + 0.021, under - 0.022)]

    def to_port(i, k, d):
        """From the bottom of the slot east in its own lane (d), then north under the thin client to socket i,
        each cable k at its own depth so the lanes can cross."""
        return [(G["x"] + 0.03 + 0.006 * k, G["y"] + d, under - 0.06 - 0.012 * k),
                (G["x"] + 0.03 + 0.006 * k, -0.21, under - 0.075 - 0.012 * k), (ports[i], -0.07, under - 0.07 - 0.008 * k),
                (ports[i], pc[3] + 0.04, under - 0.036), (ports[i], pc[3] + 0.013, under - 0.03)]

    # Straight down from its plug, below the other cables, then forward under the thin client.
    cable("Cabo de forca do mini PC", "cabo", [(plugs[3], strip[3] + 0.021, under - 0.022), (plugs[3], strip[3] + 0.03, under - 0.06),
                                              (plugs[3] + 0.012, -0.25, under - 0.11), (0.0, -0.19, under - 0.11), (ports[3], -0.075, under - 0.08),
                                              (ports[3], pc[3] + 0.04, under - 0.036), (ports[3], pc[3] + 0.013, under - 0.03)], 0.0025)

    # Monitor: power and video out of the back of the screen beside the neck, down behind the neck onto
    # the base, off its left edge (seen from the public side) and into the grommet.
    # In the slot the video cable (to the south) sits between the keyboard and the monitor power.
    for name, ex, sx, dy, d, r, dest in (("Cabo de forca do monitor", -0.040, -0.004, 0.0, 0.0005, 0.0035, to_plug(1)),
                                         ("Cabo de video", -0.052, -0.019, -0.009, -0.0065, 0.003, to_port(0, 0, -0.0065))):
        base_top = wz + 0.012 + r + 0.0004
        cable(name, "cabo", [(mx + ex, my - 0.006, 0.905), (mx + ex * 0.9, my - 0.036, 0.88), (mx + sx - 0.004, my - 0.056 + dy * 0.3, 0.83),
                             (mx + sx, my - 0.058 + dy * 0.5, wz + 0.06), (mx + sx - 0.008, my - 0.058 + dy, wz + 0.024),
                             (mx - 0.04, my - 0.056 + dy, base_top), (0.04, my - 0.05 + dy, base_top),
                             (0.028, my - 0.046 + dy, wz + 0.006 + r), (0.012, my - 0.042 + dy, wz + r + 0.0005),
                             (-0.014, G["y"] + d * 1.3, wz + r + 0.0003)] + slot(d, r, 0.02 if dest[-1][0] < G["x"] else 0.045) + dest, r)
    # Keyboard: out of the middle of its back edge, round the monitor base on the +X side, back along the
    # riser and into the grommet as the first cable of the slot.
    r = 0.002
    cable("Cabo do teclado", "cabo", [(mx + 0.06, -0.0945, wz + 0.009), (mx + 0.065, -0.112, wz + r + 0.0006), (0.26, -0.14, wz + r),
                                      (0.295, -0.2, wz + r), (0.285, -0.29, wz + r), (0.21, -0.312, wz + r), (0.06, -0.314, wz + r),
                                      (0.03, -0.301, wz + r), (0.0, -0.289, wz + r)] + slot(-0.013, r, 0.062) + to_port(1, 1, -0.013), r)
    # Phone line cord: out of the back of the base, along the front of the monitor base (over the keyboard
    # cable where they cross), then into the grommet.
    r = 0.0018
    cable("Fio da linha telefonica", "cabo", [tuple(m_phone @ Vector((-0.06, -0.104, 0.01))), tuple(m_phone @ Vector((-0.072, -0.112, r + 0.003))),
                                              tuple(m_phone @ Vector((-0.09, -0.12, r + 0.0005))),
                                              (0.33, -0.152, wz + r), (0.262, -0.129, wz + 0.0042 + r), (0.17, -0.124, wz + r),
                                              (0.05, -0.127, wz + r), (-0.006, -0.17, wz + r), (-0.008, -0.235, wz + r),
                                              (-0.02, G["y"] + 0.009, wz + r)] + slot(0.007, r, 0.03) + to_plug(0), r)
    # Mouse: out of its front, a loose S of slack, round the grommet's far side into the slot.
    r = 0.0016
    cable("Cabo do mouse", "cabo", [tuple(mouse_out), (mouse_out.x, mouse_out.y - 0.014, wz + 0.0037 + r),
                                    (mouse_out.x + 0.012, -0.096, wz + 0.003 + r), (mouse_out.x + 0.04, -0.128, wz + r),
                                    (mouse_out.x + 0.072, -0.15, wz + r), (G["x"] - 0.012, -0.19, wz + r),
                                    (G["x"] + 0.03, -0.212, wz + r), (G["x"] + 0.066, -0.238, wz + r)]
          + slot(0.0125, r, 0.085) + to_port(2, 2, 0.0125), r)
    # Coiled handset cord: from the mouthpiece end of the handset, over the edge of the base, a loop on the
    # desk beside it, back up into the jack on the base's left side.
    slope = math.atan((0.072 - 0.034) / 0.20)
    top = m_phone @ Matrix.Translation((0, 0, 0.053)) @ Matrix.Rotation(-slope, 4, "X")
    coil("Fio espiral do fone", "cabo", [tuple(top @ Vector((0.05, 0.1, 0.02))), tuple(m_phone @ Vector((0.105, 0.118, 0.016))),
                                         tuple(m_phone @ Vector((0.15, 0.07, 0.0048))), tuple(m_phone @ Vector((0.14, -0.02, 0.0048))),
                                         tuple(m_phone @ Vector((0.108, 0.01, 0.008))), tuple(m_phone @ Vector((0.093, 0.035, 0.014)))])
    box("Tomada do fone", "plastico", 0.087, 0.029, 0.009, 0.093, 0.041, 0.019, r=0.0008).data.transform(m_phone)

def build_kit():
    """Desk equipment on the counter's worktop and shelf, in the counter's frame (staff side = +Y)."""
    K, B = KIT, BALCAO
    wz = K["work_z"]
    # Monitor: base, neck, screen tilted back, facing the clerk.
    mx, my = K["monitor_x"], K["monitor_y"]
    box("Base do monitor", "plastico", mx - 0.11, my - 0.08, wz, mx + 0.11, my + 0.07, wz + 0.012, r=0.004)
    box("Haste do monitor", "plastico", mx - 0.025, my - 0.04, wz + 0.01, mx + 0.025, my - 0.015, K["screen_z"], r=0.006)
    sw, sh, st = K["screen_w"] / 2, K["screen_h"] / 2, K["screen_t"]
    scr = box("Monitor", "plastico", mx - sw, -st / 2, -sh, mx + sw, st / 2, sh, r=0.006)
    place(scr, -K["screen_tilt"], offset=(0, my + 0.005, K["screen_z"]))
    tela = box("Tela", "tela", mx - sw + 0.012, st / 2 - 0.002, -sh + 0.025, mx + sw - 0.012, st / 2 + 0.0008, sh - 0.012, r=0)
    place(tela, -K["screen_tilt"], offset=(0, my + 0.005, K["screen_z"]))
    # The clerk sits at +Y facing -Y, so their right hand is towards -X: mouse at -X, phone at +X.
    keyboard(mx, -0.02, wz)
    box("Mousepad", "tecido", -0.33, -0.10, wz, -0.10, 0.08, wz + 0.003, r=0.0015)
    mouse_out = office_mouse(-0.215, -0.005, wz + 0.003)
    m_phone = Matrix.Translation((0.53, -0.13, wz)) @ Matrix.Rotation(math.radians(-14), 4, "Z")
    phone(m_phone)
    desk_cables(K, B, wz, mouse_out, m_phone)
    # Two-tier letter tray with papers, a stack of folders, pen holder.
    tx, ty = -1.0, -0.12
    for k, z0 in enumerate((wz, wz + 0.075)):
        box(f"Bandeja {k + 1}", "bandeja", tx - 0.13, ty - 0.17, z0, tx + 0.13, ty + 0.17, z0 + 0.006, r=0.002)
        for sx in (-1, 1):
            box(f"Lateral da bandeja {k + 1} {sx:+d}", "bandeja", tx + sx * 0.13 - 0.004, ty - 0.17, z0,
                tx + sx * 0.13 + 0.004, ty + 0.17, z0 + 0.05, r=0.002)
        box(f"Fundo da bandeja {k + 1}", "bandeja", tx - 0.13, ty - 0.17, z0, tx + 0.13, ty - 0.162, z0 + 0.05, r=0.002)
        box(f"Papeis {k + 1}", "papel", tx - 0.105, ty - 0.16, z0 + 0.006, tx + 0.105, ty + 0.14, z0 + 0.006 + 0.012 - k * 0.006, r=0.001)
    for sx in (-1, 1):
        for sy in (-1, 1):
            cyl(f"Coluna da bandeja {sx:+d}{sy:+d}", "bandeja", (tx + sx * 0.125, ty + sy * 0.15, wz + 0.04), 0.005, 0.075, seg=8, r=0.001)
    for k, (key, ang) in enumerate((("pasta_kraft", 4), ("pasta_azul", -3), ("pasta_kraft", 9))):
        m = Matrix.Translation((-0.62, -0.13, wz + 0.004 + k * 0.009)) @ Matrix.Rotation(math.radians(90 + ang), 4, "Z")
        hull(f"Pasta {k + 1}", key, [(x, y, z) for x in (-0.167, 0.167) for y in (-0.118, 0.118) for z in (-0.004, 0.004)],
             r=0.0015, matrix=m)
    pen_holder(0.72, -0.24, wz)
    # Binders and archive boxes on the open shelf at -X (spines towards the clerk, +Y).
    inner = B["length"] / 2 - B["gable_t"]
    shelf_top, under = 0.378, B["work_z"] - B["work_t"]
    # Three binders standing side by side; the fourth stands a little apart and leans towards the
    # divider, on its bottom edge, its top resting on the divider (it used to lean into its neighbour).
    divider = -inner + B["shelf_w"]
    h, bw = 0.315, 0.075
    for k, key in enumerate(("fichario_preto", "fichario_azul", "fichario_preto", "fichario_vermelho")):
        x0 = -inner + 0.008 + k * 0.078 + (0.024 if k == 3 else 0.0)
        m = Matrix.Translation((x0, 0.0, shelf_top))
        if k == 3:
            lean = math.asin((divider - 0.001 - (x0 + bw)) / h)
            m = Matrix.Translation((x0 + bw, 0.0, shelf_top)) @ Matrix.Rotation(lean, 4, "Y") @ Matrix.Translation((-bw, 0, 0))
        box(f"Fichario {k + 1}", key, 0, -0.19, 0, bw, 0.085, h, r=0.004).data.transform(m)
        box(f"Etiqueta {k + 1}", "papel", 0.012, 0.085, 0.13, 0.063, 0.0865, 0.25, r=0).data.transform(m)
    for k, key in enumerate(("polionda_azul", "polionda_cinza")):
        x0 = -inner + 0.01 + k * 0.137
        box(f"Caixa arquivo {k + 1}", key, x0, -0.27, 0.07, x0 + 0.13, 0.08, 0.07 + 0.25, r=0.003)


def build_livro():
    """Hardcover register book ("livro de protocolo": black cloth, red corners and spine) with a pen."""
    P = LIVRO
    w, d, t = P["w"] / 2, P["d"] / 2, P["t"]
    box("Miolo", "papel", -w + 0.006, -d + 0.004, 0.003, w - 0.004, d - 0.004, t - 0.003, r=0.001)
    box("Capa inferior", "capa", -w, -d, 0.0, w, d, 0.0035, r=0.001)
    box("Capa superior", "capa", -w, -d, t - 0.0035, w, d, t, r=0.001)
    box("Lombada", "capa_vermelha", -w - 0.004, -d, 0.0, -w + 0.012, d, t, r=0.004)
    for sy in (-1, 1):
        hull(f"Canto {sy:+d}", "capa_vermelha", [(w + 0.0006, sy * (d + 0.0006), z) for z in (t - 0.0035, t + 0.0006)] +
             [(w - 0.045, sy * (d + 0.0006), z) for z in (t - 0.0035, t + 0.0006)] +
             [(w + 0.0006, sy * (d - 0.045), z) for z in (t - 0.0035, t + 0.0006)], r=0)
    box("Etiqueta da capa", "papel", -0.05, -0.04, t, 0.07, 0.04, t + 0.0004, r=0)
    # Capped ballpoint (same model as the pen holder's) lying on the cover, clip up: it rests on its cap
    # (r 4.9 mm) and the barrel's lower edge (4.3 mm), clear of the label and the red corners.
    a, s0, s1 = math.radians(28), 0.003, 0.140
    slope = (0.0049 - 0.0043) / (s1 - s0)
    d = Vector((math.cos(a), math.sin(a), slope)).normalized()
    end = Vector((-0.01, -0.08, t + 0.0043 - s0 * slope)) - d * 0.07
    rot = d.to_track_quat("Z", "X").to_matrix().to_4x4()   # local +X (the clip) towards world up
    pen("Caneta", Matrix.Translation(end) @ rot, "caneta_corpo", "caneta_azul")


def build_bebedouro():
    """Floor-standing pressure water cooler: painted steel cabinet, stainless top with a basin and two taps."""
    P = BEBEDOURO
    w, d, h, th = P["w"] / 2, P["d"] / 2, P["body_h"], P["top_h"]
    box("Gabinete", "metal_pintado", -w, -d, 0.06, w, d, h, r=0.01, seg=3)
    box("Base", "plastico", -w + 0.015, -d + 0.02, 0.0, w - 0.015, d - 0.015, 0.06, r=0.005)
    box("Tampo", "inox", -w - 0.005, -d - 0.008, h, w + 0.005, d + 0.003, h + th, r=0.012, seg=3)
    box("Cuba", "inox_escuro", -w + 0.04, -d + 0.03, h + th - 0.03, w - 0.04, d - 0.06, h + th + 0.0005, r=0.01, seg=2)
    box("Grelha", "plastico", -0.07, -d + 0.05, h + th - 0.0285, 0.07, -d + 0.15, h + th - 0.022, r=0.003)
    cyl("Torneira de jato", "cromo", (0.07, -d + 0.13, h + th + 0.012), 0.012, 0.025, seg=14, r=0.003)
    tube("Torneira de copo", "cromo", [(-0.07, -d + 0.20, h + th), (-0.07, -d + 0.20, h + th + 0.06),
                                     (-0.07, -d + 0.12, h + th + 0.06), (-0.07, -d + 0.12, h + th + 0.04)], 0.008, fillet=0.02)
    for x in (-0.06, 0.06):
        cyl(f"Botao {x:+.2f}", "plastico", (x, -d - 0.013, h + th * 0.5), 0.016, 0.012, axis=(0, 1, 0), seg=16, r=0.002)
    box("Grade de ventilacao", "grade", -w + 0.04, d - 0.002, 0.12, w - 0.04, d + 0.002, 0.42, r=0)
    box("Placa", "papel", -0.06, -d - 0.0015, h - 0.11, 0.06, -d, h - 0.07, r=0)


def build_lixeira():
    """Stainless pedal bin with a black base ring, hinged lid and front pedal."""
    P = LIXEIRA
    rb, rt, h = P["r_bottom"], P["r_top"], P["h"]
    cyl("Base", "plastico", (0, 0, 0.012), rb + 0.006, 0.024, seg=32, r=0.004)
    cyl("Corpo", "inox", (0, 0, 0.024 + (h - 0.024) / 2), rb, h - 0.024, seg=32, r=0.004, radius_top=rt)
    cyl("Aro", "plastico", (0, 0, h + 0.008), rt + 0.004, 0.016, seg=32, r=0.004)
    cyl("Tampa", "inox", (0, 0, h + 0.026), rt + 0.002, 0.024, seg=32, r=0.012, radius_top=rt - 0.03)
    box("Dobradica", "plastico", -0.04, rt - 0.01, h - 0.01, 0.04, rt + 0.02, h + 0.03, r=0.006)
    box("Pedal", "plastico", -0.045, -rb - 0.075, 0.012, 0.045, -rb + 0.01, 0.032, r=0.006)


# ---------------------------------------------------------------- light fittings and the glass door
# Assets whose name ends in "_Luz" or "Vidro" are not baked: their parts keep their own UVs and one
# material slot per key, and Unity gives them their own materials (diffuser, emissive tubes, glass).

# Surface-mounted fitting for two 1.2 m tubes (T8 / LED tube), sheet-steel body: origin at the centre of
# its top face (on the ceiling), hanging down to z = -height; long side along X.
LUMINARIA = dict(length=1.27, width=0.26, height=0.072, cap_t=0.008, lip=0.016, pocket=0.046, tube_r=0.013,
                 tube_y=0.058)
# Wall lamp for the main entrance: origin on the wall face at the lamp's mounting centre, front -Y.
ARANDELA = dict(w=0.28, d=0.12, h=0.10, plate_h=0.14)
# Two-leaf 10 mm tempered glass door on top / floor pivots (floor springs) in the 1.94 x 2.17 m clear
# opening of the main entrance. Door frame: origin at the opening centre on the floor, wall plane y = 0,
# outside towards -Y. Leaf frame: origin on the pivot axis at floor level, glass along +X.
PORTA = dict(clear=1.94, height=2.17, sill=0.006, glass_t=0.010, gap_jamb=0.006, gap_mid=0.004,
             pivot_in=0.055, z0=0.016, z1=2.164, handle_len=0.60, handle_z=1.10, handle_off=0.085)
PORTA["leaf_w"] = PORTA["clear"] / 2 - PORTA["gap_jamb"] - PORTA["gap_mid"] / 2
PORTA["pivot_x"] = -PORTA["clear"] / 2 + PORTA["gap_jamb"] + PORTA["pivot_in"]   # left leaf (right = mirror)


def planar_uv(ob, u_axis, v_axis, u0, u1, v0, v1, ua=0.0, ub=1.0, va=0.0, vb=1.0, edge_uv=None, normal_axis=None):
    """UVs by projection: coordinate u_axis in [u0, u1] -> [ua, ub], v_axis in [v0, v1] -> [va, vb].
    With edge_uv, faces not facing normal_axis get that single UV (a reserved spot of the texture)."""
    me = ob.data
    uv = (me.uv_layers.get("UVMap") or me.uv_layers.new(name="UVMap")).data
    for f in me.polygons:
        side = edge_uv is not None and abs(f.normal[normal_axis]) < 0.5
        for li in f.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            if side:
                uv[li].uv = edge_uv
            else:
                uv[li].uv = (ua + (co[u_axis] - u0) / (u1 - u0) * (ub - ua), va + (co[v_axis] - v0) / (v1 - v0) * (vb - va))
    return ob


def build_luminaria():
    """Body: one extrusion of the sheet-steel section (top, sides, lips under the diffuser and the
    pocket that holds the tubes, whose ceiling is the white reflector), end caps with two screws each,
    lamp holders, diffuser clips and the two screws that fix the body to the ceiling."""
    P = LUMINARIA
    L, W, H, ct, lip, pk = P["length"], P["width"], P["height"], P["cap_t"], P["lip"], P["pocket"]
    x0, x1 = -L / 2 + ct, L / 2 - ct
    section = [(-W / 2, 0.0), (W / 2, 0.0), (W / 2, -H), (W / 2 - lip, -H), (W / 2 - lip, -H + pk),
               (-W / 2 + lip, -H + pk), (-W / 2 + lip, -H), (-W / 2, -H)]
    extrude("Corpo", "chapa", section, "YZ", x0, x1, r=0.003, seg=2, min_angle=30.0)
    cap = [(-W / 2 - 0.0015, 0.0), (W / 2 + 0.0015, 0.0), (W / 2 + 0.0015, -H - 0.0015), (-W / 2 - 0.0015, -H - 0.0015)]
    for sx, a, b in ((-1, -L / 2, x0), (1, x1, L / 2)):
        extrude(f"Tampa lateral {sx:+d}", "tampa", cap, "YZ", a, b, r=0.004, seg=3)
        for y in (-W / 4, W / 4):
            cyl(f"Parafuso da tampa {sx:+d} {y:+.2f}", "parafuso", (sx * L / 2 + sx * 0.001, y, -H / 2), 0.0035, 0.002,
                axis=(1, 0, 0), seg=12, r=0.0006)
        for y in (-P["tube_y"], P["tube_y"]):   # lamp holders at both ends of each tube
            box(f"Soquete {sx:+d} {y:+.2f}", "tampa", sx * (x1 - 0.026) - 0.013, y - 0.017, -H + pk - 0.034,
                sx * (x1 - 0.026) + 0.013, y + 0.017, -H + pk, r=0.003)
    for k in range(3):                          # diffuser clips under the lips
        x = -0.42 + k * 0.42
        for sy in (-1, 1):
            box(f"Presilha {k + 1} {sy:+d}", "parafuso", x - 0.012, sy * (W / 2 - lip) - 0.004 * (sy > 0),
                -H - 0.0012, x + 0.012, sy * (W / 2 - lip) + 0.004 * (sy < 0), -H + 0.001, r=0.0005, seg=1)
    for x in (-0.36, 0.36):                     # body fixed to the ceiling through the reflector
        cyl(f"Parafuso de fixacao {x:+.2f}", "parafuso", (x, 0.0, -H + pk - 0.001), 0.005, 0.002, seg=14, r=0.0008)


def build_luminaria_luz():
    """Light parts of the fitting: milky prismatic diffuser resting on the lips, two tubes behind it."""
    P = LUMINARIA
    L, W, H, ct, lip = P["length"], P["width"], P["height"], P["cap_t"], P["lip"]
    x0, x1, y0, y1 = -L / 2 + ct + 0.002, L / 2 - ct - 0.002, -W / 2 + lip - 0.005, W / 2 - lip + 0.005
    d = box("Difusor", "difusor", x0, y0, -H + 0.0008, x1, y1, -H + 0.0035, r=0.0004, seg=1)
    planar_uv(d, 0, 1, x0, x1, y0, y1)
    for y in (-P["tube_y"], P["tube_y"]):
        t = cyl(f"Tubo {y:+.2f}", "tubo", (0.0, y, -H + P["pocket"] - 0.034 / 2 - 0.004), P["tube_r"],
                L - 2 * ct - 0.09, axis=(1, 0, 0), seg=16, r=0.002)
        planar_uv(t, 0, 1, x0, x1, y0, y1)


def build_arandela():
    """Entrance wall lamp: wall plate, cast housing with a sloping top, rim around the glass below."""
    P = ARANDELA
    w, d, h = P["w"] / 2, P["d"], P["h"] / 2
    box("Base de parede", "aluminio_pintado", -w + 0.03, -0.008, -P["plate_h"] / 2, w - 0.03, 0.0, P["plate_h"] / 2, r=0.003)
    hull("Corpo da arandela", "aluminio_pintado", [(x, y, z) for x in (-w, w) for y, z in
                                                   ((-0.008, h), (-0.008, -h), (-d, h - 0.03), (-d, -h))], r=0.008)
    for name, a0, b0, a1, b1 in (("Aro frontal", -w + 0.012, -d + 0.008, w - 0.012, -d + 0.02),
                                 ("Aro traseiro", -w + 0.012, -0.03, w - 0.012, -0.018),
                                 ("Aro esquerdo", -w + 0.012, -d + 0.008, -w + 0.024, -0.018),
                                 ("Aro direito", w - 0.024, -d + 0.008, w - 0.012, -0.018)):
        box(name, "aluminio_pintado", a0, b0, -h - 0.008, a1, b1, -h + 0.001, r=0.0015)
    for x in (-w + 0.045, w - 0.045):
        cyl(f"Parafuso {x:+.2f}", "parafuso", (x, -0.009, 0.05), 0.004, 0.002, axis=(0, 1, 0), seg=12, r=0.0006)


def build_arandela_luz():
    P = ARANDELA
    w, d, h = P["w"] / 2, P["d"], P["h"] / 2
    x0, x1, y0, y1 = -w + 0.024, w - 0.024, -d + 0.02, -0.03
    g = box("Vidro da arandela", "difusor", x0, y0, -h - 0.004, x1, y1, -h - 0.001, r=0.0004, seg=1)
    planar_uv(g, 0, 1, x0, x1, y0, y1)
    e = box("Placa de LED", "tubo", x0 + 0.02, y0 + 0.012, -h + 0.002, x1 - 0.02, y1 - 0.012, -h + 0.006, r=0.0005, seg=1)
    planar_uv(e, 0, 1, x0, x1, y0, y1)


def build_porta_fixos():
    """Fixed parts of the door system in the existing opening (the jamb linings come from the map):
    floor spring covers, floor lock keepers, top pivot plates under the head lining, floor stops that
    hold the leaves open."""
    P = PORTA
    px, top = P["pivot_x"], P["height"]
    for sx in (-1, 1):
        x = sx * -px                            # pivot axis of this leaf (left: px, right: -px)
        span = (x - 0.045, x + 0.255) if sx < 0 else (x - 0.255, x + 0.045)
        box(f"Tampa da mola de piso {sx:+d}", "inox", span[0], -0.055, P["sill"], span[1], 0.055, P["sill"] + 0.0025, r=0.001, seg=1)
        cyl(f"Eixo da mola {sx:+d}", "inox_escuro", (x, 0.0, P["sill"] + 0.003), 0.011, 0.001, seg=16, r=0.0003)
        kx = sx * 0.032
        cyl(f"Contra-fechadura de piso {sx:+d}", "inox", (kx, 0.0, P["sill"] + 0.0012), 0.014, 0.0025, seg=18, r=0.0006)
        cyl(f"Furo da contra-fechadura {sx:+d}", "inox_escuro", (kx, 0.0, P["sill"] + 0.0026), 0.006, 0.0004, seg=12, r=0)
        a0 = x - 0.04 if sx < 0 else x - 0.08            # plate runs from the pivot towards the leaf
        box(f"Pivo superior {sx:+d}", "inox", a0, -0.03, top - 0.005, a0 + 0.12, 0.03, top, r=0.0015, seg=1)
        # Floor stop with a rubber cap where the open leaf's handle end comes to rest (outside).
        sxp = x - sx * 0.032                            # the open leaf (88 degrees) ends just beyond it
        cyl(f"Prendedor de piso {sx:+d}", "inox", (sxp, -0.935, 0.018), 0.018, 0.036, seg=20, r=0.003)
        cyl(f"Borracha do prendedor {sx:+d}", "borracha", (sxp, -0.935, 0.039), 0.016, 0.006, seg=20, r=0.002)


def build_porta_folha():
    """Hardware of one leaf (left; the right leaf is its mirror): top and bottom pivot patch fittings,
    bottom lock patch at the meeting edge, back-to-back tubular pull handles on both faces."""
    P = PORTA
    t, z0, z1, w = P["glass_t"] / 2, P["z0"], P["z1"], P["leaf_w"]
    xa, xb = -P["pivot_in"], -P["pivot_in"] + w           # glass edges in the leaf frame
    clamp = t + 0.013
    box("Ferragem superior", "inox", xa - 0.003, -clamp, z1 - 0.062, xa + 0.172, clamp, z1 + 0.004, r=0.004, seg=2)
    cyl("Pino do pivo superior", "inox_escuro", (0.0, 0.0, z1 + 0.005), 0.008, 0.004, seg=16, r=0.0005)
    box("Ferragem inferior", "inox", xa - 0.003, -clamp, z0 - 0.006, xa + 0.172, clamp, z0 + 0.058, r=0.004, seg=2)
    cyl("Pino do pivo inferior", "inox_escuro", (0.0, 0.0, z0 - 0.007), 0.008, 0.004, seg=16, r=0.0005)
    box("Fechadura inferior", "inox", xb - 0.175, -clamp, z0 - 0.006, xb + 0.003, clamp, z0 + 0.058, r=0.004, seg=2)
    cyl("Cilindro da fechadura", "inox_escuro", (xb - 0.062, 0.0, z0 + 0.026), 0.0115, 2 * clamp + 0.008,
        axis=(0, 1, 0), seg=20, r=0.0015)
    for sy in (-1, 1):
        cyl(f"Rasgo da chave {sy:+d}", "borracha", (xb - 0.062, sy * (clamp + 0.0042), z0 + 0.026), 0.0025, 0.0006,
            axis=(0, 1, 0), seg=8, r=0)
    hx, hz, hl, off = xb - P["handle_off"], P["handle_z"], P["handle_len"], 0.065
    for sy in (-1, 1):
        tube(f"Puxador {sy:+d}", "puxador", [(hx, sy * (t + off), hz - hl / 2), (hx, sy * (t + off), hz + hl / 2)], 0.0125, res=4)
    for zz in (hz - hl / 2 + 0.06, hz + hl / 2 - 0.06):   # posts through the glass, one bolt for both pulls
        tube(f"Haste do puxador {zz:.2f}", "puxador", [(hx, -(t + off), zz), (hx, t + off, zz)], 0.008, res=3)


def build_porta_vidro():
    """Glass pane of one leaf (10 mm). Both faces map onto the texture; edges use a reserved texel (the
    greenish polished edge of tempered glass)."""
    P = PORTA
    t, xa, xb = P["glass_t"] / 2, -P["pivot_in"], -P["pivot_in"] + P["leaf_w"]
    g = box("Vidro", "vidro", xa, -t, P["z0"], xb, t, P["z1"], r=0.0012, seg=1)
    planar_uv(g, 0, 2, xa, xb, P["z0"], P["z1"], 0.0, 0.96, 0.0, 1.0, edge_uv=(0.985, 0.5), normal_axis=1)


def build_batente_entrada():
    """Aluminium frame of the main entrance, replacing the map's flat boxes (door frame: origin at the
    opening centre on the floor, wall faces at y = +-0.1, outside -Y; raw opening 2.0 x 2.2 m). Keeps the
    glass door's envelope: jamb faces at x = +-0.97 (1.94 m clear), head underside at 2.17, sill top at
    0.006 (the floor springs and lock keepers sit on it).
    - Jambs run full height; the head sits between them (visible joints at the top corners).
    - Casings (16 mm, mitred) stand 8 mm back from the jamb face, so the lining shows a shadow step.
    - Countersunk screws fix the lining (3 per jamb, 2 on the head, clear of the top pivot plates).
    - Grey silicone bead where the casings meet the wall; granite sill with a rounded nose outside."""
    P = PORTA
    jx, top, dep = P["clear"] / 2, P["height"], 0.104       # jamb face, head underside, lining depth (y)
    raw_x, raw_top = 1.0, 2.2
    for sx in (-1, 1):
        x0, x1 = sorted((sx * jx, sx * raw_x))
        box(f"Marco {sx:+d}", "aluminio", x0, -dep, P["sill"], x1, dep, raw_top, r=0.002, seg=2)
        for z in (0.30, 1.10, 1.90):
            screw(f"Parafuso do marco {sx:+d} {z:.2f}", (sx * (jx - 0.0004), 0.0, z), (-sx, 0, 0))
    box("Travessa superior", "aluminio", -jx, -dep, top, jx, dep, raw_top, r=0.002, seg=2)
    for x in (-0.5, 0.5):
        screw(f"Parafuso da travessa {x:+.1f}", (x, 0.0, top - 0.0004), (0, 0, -1))
    cw, ct, step = 0.082, 0.016, 0.008                      # casing width, thickness, step back
    ix, iz, ox, oz = jx + step, top + step, jx + step + cw, top + step + cw
    for side, floor_z in ((-1, P["sill"]), (1, 0.003)):     # outside on the sill, inside on the floor finish
        y0, y1 = sorted((side * 0.1, side * (0.1 + ct)))
        for sx in (-1, 1):
            poly = [(sx * ix, floor_z), (sx * ox, floor_z), (sx * ox, oz), (sx * ix, iz)]
            extrude(f"Guarnicao {side:+d} {sx:+d}", "aluminio", poly if sx > 0 else poly[::-1], "XZ", y0, y1, r=0.003, seg=2)
        extrude(f"Guarnicao superior {side:+d}", "aluminio", [(-ix, iz), (ix, iz), (ox, oz), (-ox, oz)], "XZ", y0, y1,
                r=0.003, seg=2)
        yb = side * 0.1015
        tube(f"Silicone {side:+d}", "selante", [(ox + 0.0015, yb, floor_z), (ox + 0.0015, yb, oz + 0.0015),
                                               (-ox - 0.0015, yb, oz + 0.0015), (-ox - 0.0015, yb, floor_z)], 0.0022, fillet=0.004)
    box("Soleira", "granito", -raw_x, -0.128, -0.012, raw_x, dep, P["sill"], r=0.004, seg=3)


def screw(name, center, axis):
    """Countersunk stainless screw head flush with a face (axis = outward normal), Phillips recess."""
    c = Vector(center)
    n = Vector(axis)
    cyl(name, "inox", tuple(c), 0.0042, 0.0012, axis=tuple(n), seg=16, r=0.0003)
    t = n.orthogonal().normalized()
    b = n.cross(t)
    for k, d in enumerate((t, b)):
        o = c + n * 0.0006
        pts = [o - d * 0.0026, o + d * 0.0026]
        tube(f"{name} fenda {k}", "inox_escuro", [tuple(p) for p in pts], 0.00045)


BUILDERS = {"BalcaoRecepcao": build_balcao, "CadeiraEspera": build_espera,
            "CadeiraAtendente": build_atendente, "ArmarioApoio": build_armario,
            "LongarinaEspera": build_longarina, "EquipamentosBalcao": build_kit, "LivroRegistro": build_livro,
            "Bebedouro": build_bebedouro, "Lixeira": build_lixeira,
            "LuminariaSobrepor": build_luminaria, "LuminariaSobrepor_Luz": build_luminaria_luz,
            "LuminariaExterna": build_arandela, "LuminariaExterna_Luz": build_arandela_luz,
            "PortaVidroFixos": build_porta_fixos, "PortaVidroFolha": build_porta_folha,
            "PortaVidroVidro": build_porta_vidro, "BatenteEntrada": build_batente_entrada}


def build(asset):
    c = _coll(asset)
    for o in list(c.objects):
        me = o.data
        bpy.data.objects.remove(o)
        if me and me.users == 0:
            bpy.data.meshes.remove(me)
    _current["asset"] = asset
    BUILDERS[asset]()
    return list(c.objects)


# ---------------------------------------------------------------- game mesh

def assemble(asset, mats, x_offset=0.0):
    """Joins the parts into one mesh with one slot per material key; drops faces lying on the floor,
    triangulates (so the baked normals match what Unity imports) and smooths by angle.
    x_offset only spreads the assets apart in the .blend for review; the export ignores it."""
    parts = list(_coll(asset).objects)
    keys = sorted({o["mat"] for o in parts})
    for o in parts:
        o.data.materials.clear()
        for k in keys:
            o.data.materials.append(mats[k])
        for f in o.data.polygons:
            f.material_index = keys.index(o["mat"])
    old = bpy.data.objects.get(asset)
    if old:
        bpy.data.objects.remove(old)
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.hide_set(False)
        o.select_set(True)
    root = parts[0]
    bpy.context.view_layer.objects.active = root
    bpy.ops.object.join()
    root.name = root.data.name = asset
    bm = bmesh.new()
    bm.from_mesh(root.data)
    # Only faces lying on the floor plane (z = 0): ceiling and wall fittings hang below their origin.
    floor = [f for f in bm.faces if f.normal.z < -0.99 and abs(f.calc_center_median().z) < 0.002]
    bmesh.ops.delete(bm, geom=floor, context="FACES")
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.to_mesh(root.data)
    bm.free()
    root.data.shade_smooth()
    root.data.set_sharp_from_angle(angle=math.radians(40))
    root.location.x = x_offset
    return root


def layout_uvs(ob, size, hidden_scale=0.3, margin_px=6):
    """One non-overlapping atlas for the bake: smart projection, uniform texel density, faces that only
    look down (never seen from eye height) at reduced density."""
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.0, area_weight=0.0,
                             correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.average_islands_scale()
    bpy.ops.object.mode_set(mode="OBJECT")
    me = ob.data
    uv = me.uv_layers.active.data
    for f in me.polygons:
        if f.normal.z < -0.7:
            for li in f.loop_indices:
                uv[li].uv *= hidden_scale
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, scale=True, margin_method="FRACTION", margin=margin_px / size,
                            shape_method="CONCAVE")
    bpy.ops.object.mode_set(mode="OBJECT")
    uv = me.uv_layers.active.data   # the old reference is stale after the mode switch
    a2 = a3 = 0.0
    for f in me.polygons:
        p = [uv[li].uv.copy() for li in f.loop_indices]
        a2 += abs((p[1].x - p[0].x) * (p[2].y - p[0].y) - (p[2].x - p[0].x) * (p[1].y - p[0].y)) / 2
        a3 += f.area
    return round(size * math.sqrt(a2 / a3))   # px per metre


def export_fbx(ob, path, keep_slots=False):
    """Exports a single-slot copy (everything is baked into one atlas). Metres, Y up / -Z forward with the
    axis conversion baked in; turned 180 deg about Z because the conversion maps Blender +X to Unity -X."""
    src = ob.name
    ob.name = src + "_fonte"
    cp = ob.copy()
    cp.data = ob.data.copy()
    cp.name = cp.data.name = src
    cp.location = (0, 0, 0)
    bpy.context.scene.collection.objects.link(cp)
    me = cp.data
    me.transform(Matrix.Rotation(math.pi, 4, "Z"))
    if not keep_slots:
        me.materials.clear()
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
        ob.name = src
    return path

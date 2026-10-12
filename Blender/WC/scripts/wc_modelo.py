# Procedural models of the bathroom (WC) fittings. Reuses the reception's mesh helpers and pipeline
# (Blender/Recepcao/scripts/recepcao_modelo.py: box, cyl, hull, tube, extrude, lathe, cut, assemble,
# layout_uvs, export_fbx). Units are metres; Blender -Y is the front (Unity -Z), Z up.
# Wall-mounted pieces: origin on the wall plane (y = 0) at the piece's reference point, body towards -Y.
# Floor pieces: origin on the floor, back against the wall plane (y = 0).
# Ceramic shapes are lofts of superelliptic sections smoothed with a subdivision surface.
import math
import os
import types
import bmesh
import bpy
from mathutils import Matrix, Vector

_HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.path.join(
    os.path.dirname(bpy.data.filepath), "scripts")
_BASE_PATH = os.path.join(_HERE, "..", "..", "Recepcao", "scripts", "recepcao_modelo.py")
_ns = {"__name__": "recepcao_modelo"}
exec(compile(open(_BASE_PATH, encoding="utf-8").read(), _BASE_PATH, "exec"), _ns)
base = types.SimpleNamespace(**_ns)
box, cyl, hull, tube, extrude, lathe, cut, planar_uv = (base.box, base.cyl, base.hull, base.tube, base.extrude,
                                                       base.lathe, base.cut, base.planar_uv)
assemble, layout_uvs, export_fbx, _finish = base.assemble, base.layout_uvs, base.export_fbx, base._finish

# Toilet: close-coupled tank, rim 0.38 m, seat 0.40, 0.65 m from the wall to the front of the bowl.
VASO = dict(rim=0.38, seat_t=0.02, lid_t=0.025, tank=(0.38, 0.18, 0.37), depth=0.65)
# Wall-hung basin: 0.50 x 0.42 m, rim at 0.80 m.
LAVATORIO = dict(w=0.50, d=0.42, rim=0.80, h=0.18)
# Bathroom door in a 1.0 x 2.2 m raw opening (wall faces at y = +-0.1, reception towards -Y): painted
# frame with a stop on the reception side, 35 mm flush leaf opening into the bathroom (+Y), hinges at +X.
PORTA_WC = dict(raw=1.0, height=2.2, jamb=0.03, leaf_t=0.035, gap=0.003, floor_gap=0.008, max_open=95.0)
PORTA_WC["leaf_w"] = PORTA_WC["raw"] - 2 * PORTA_WC["jamb"] - 2 * PORTA_WC["gap"]
PORTA_WC["leaf_h"] = PORTA_WC["height"] - PORTA_WC["jamb"] - PORTA_WC["gap"] - PORTA_WC["floor_gap"]
PORTA_WC["pivot"] = (PORTA_WC["raw"] / 2 - PORTA_WC["jamb"] - 0.001, 0.017)   # hinge axis (x, y) in the frame

ASSETS = ("VasoSanitario", "VasoBotao", "Lavatorio", "LavatorioBotao", "Espelho", "Saboneteira", "ToalheiroPapel",
          "PapeleiraRolao", "BarraApoio", "PortaWCFixos", "PortaWCFolha", "Ralo", "EscovaSanitaria", "Exaustor")
# Not baked: the mirror (own Unity materials per slot) and the two buttons that move (chrome in Unity).
UNBAKED = ("Espelho", "VasoBotao", "LavatorioBotao")


def _coll(asset):
    return base._coll(asset)


def ring(cx, cy, z, rx, ry_front, ry_back, n=48, p=2.4):
    """Superelliptic section at height z: x half-width rx, reaching ry_front towards -Y, ry_back towards +Y."""
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        c, s = math.cos(a), math.sin(a)
        x = cx + rx * math.copysign(abs(c) ** (2 / p), c)
        ry = ry_back if s > 0 else ry_front
        y = cy + ry * math.copysign(abs(s) ** (2 / p), s)
        pts.append((x, y, z))
    return pts


def subsurf(ob, levels=2):
    mod = ob.modifiers.new("ss", "SUBSURF")
    mod.levels = mod.render_levels = levels
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return ob


def loft(name, mat, rings, levels=2, cap_bottom=True, cap_top=True):
    """Closed solid through the given rings (same point count), smoothed by a subdivision surface."""
    bm = bmesh.new()
    vr = [[bm.verts.new(p) for p in r] for r in rings]
    n = len(rings[0])
    for a, b in zip(vr, vr[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
    if cap_bottom:
        bm.faces.new(list(reversed(vr[0])))
    if cap_top:
        bm.faces.new(vr[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    ob = _finish(name, bm, mat)
    return subsurf(ob, levels) if levels else ob


def slab(name, mat, outer, inner, z0, z1, levels=1):
    """Flat ring (outer and inner sections, same count) between z0 and z1, or a plate if inner is None."""
    bm = bmesh.new()
    lo_o = [bm.verts.new((x, y, z0)) for x, y, _ in outer]
    hi_o = [bm.verts.new((x, y, z1)) for x, y, _ in outer]
    n = len(outer)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((lo_o[i], lo_o[j], hi_o[j], hi_o[i]))
    if inner is None:
        bm.faces.new(list(reversed(lo_o)))
        bm.faces.new(hi_o)
    else:
        lo_i = [bm.verts.new((x, y, z0)) for x, y, _ in inner]
        hi_i = [bm.verts.new((x, y, z1)) for x, y, _ in inner]
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((lo_i[j], lo_i[i], hi_i[i], hi_i[j]))
            bm.faces.new((hi_o[i], hi_o[j], hi_i[j], hi_i[i]))
            bm.faces.new((lo_o[j], lo_o[i], lo_i[i], lo_i[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    ob = _finish(name, bm, mat)
    return subsurf(ob, levels) if levels else ob


# ---------------------------------------------------------------- floor and wall fittings

def build_vaso():
    """Close-coupled toilet: lofted bowl and foot, tank with lid and dual flush button, seat and closed lid
    on chrome hinges, floor fixing caps, angle valve and braided hose to the tank."""
    V = VASO
    rim = V["rim"]
    rings = [ring(0, -0.37, 0.0, 0.115, 0.19, 0.17), ring(0, -0.37, 0.012, 0.115, 0.19, 0.17),   # crisp foot on the floor
             ring(0, -0.38, 0.12, 0.105, 0.20, 0.17),
             ring(0, -0.39, 0.24, 0.150, 0.25, 0.22), ring(0, -0.37, 0.34, 0.175, 0.28, 0.31),
             ring(0, -0.36, rim, 0.180, 0.29, 0.33)]
    loft("Bacia", "louca", rings, levels=2)
    tw, td, th = V["tank"]
    box("Caixa acoplada", "louca", -tw / 2, -td - 0.02, rim - 0.01, tw / 2, -0.02, rim + th, r=0.025, seg=3)
    box("Tampa da caixa", "louca", -tw / 2 - 0.008, -td - 0.03, rim + th, tw / 2 + 0.008, -0.012, rim + th + 0.024,
        r=0.012, seg=3)
    top = rim + th + 0.024
    # Dual flush plate: the partial button stays in the body; the full one moves (asset VasoBotao).
    box("Botao de descarga parcial", "cromo", -0.028, -0.125, top - 0.001, -0.0015, -0.075, top + 0.004, r=0.0015, seg=1)
    box("Aro dos botoes", "cromo", -0.032, -0.129, top - 0.0005, 0.032, -0.071, top + 0.0015, r=0.0008, seg=1)
    # Silicone bead where the foot meets the floor tiles.
    foot = ring(0, -0.37, 0.0025, 0.1175, 0.1925, 0.1725, n=40)
    tube("Silicone do pe", "selante", foot + foot[:1], 0.0028, res=2)
    seat_o = ring(0, -0.42, 0, 0.18, 0.23, 0.23, n=48, p=2.2)
    seat_i = ring(0, -0.44, 0, 0.11, 0.15, 0.13, n=48, p=2.2)
    slab("Assento", "assento", seat_o, seat_i, rim + 0.004, rim + 0.004 + V["seat_t"], levels=1)
    lid = ring(0, -0.42, 0, 0.183, 0.235, 0.23, n=48, p=2.2)
    z = rim + 0.004 + V["seat_t"]
    slab("Tampa", "assento", lid, None, z + 0.002, z + 0.002 + V["lid_t"], levels=1)
    for sx in (-1, 1):
        cyl(f"Dobradica do assento {sx:+d}", "cromo", (sx * 0.075, -0.19, z + 0.006), 0.009, 0.03, axis=(1, 0, 0), seg=14, r=0.002)
        cyl(f"Capa do parafuso de piso {sx:+d}", "louca", (sx * 0.118, -0.40, 0.018), 0.012, 0.016, axis=(1, 0, 0), seg=14, r=0.004)
    cyl("Registro", "cromo", (0.15, -0.025, 0.18), 0.012, 0.05, axis=(0, 1, 0), seg=16, r=0.002)
    cyl("Volante do registro", "cromo", (0.15, -0.055, 0.18), 0.018, 0.012, axis=(0, 1, 0), seg=16, r=0.003)
    tube("Engate flexivel", "inox", [(0.15, -0.04, 0.18), (0.15, -0.06, 0.22), (0.13, -0.08, 0.32), (0.12, -0.09, rim - 0.01)],
         0.005, fillet=0.03)


def build_lavatorio():
    """Wall-hung basin: rounded body with the bowl carved out (boolean), tap deck at the back; push-button
    (self-closing) tap, chrome bottle trap to the wall, angle valve and braided hose."""
    L = LAVATORIO
    w, d, rim, h = L["w"] / 2, L["d"], L["rim"], L["h"]
    body = box("Cuba", "louca", -w, -d, rim - h, w, 0.0, rim, r=0.05, seg=5)   # soft ceramic edges
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=1.0)
    bmesh.ops.scale(bm, vec=(0.20, 0.15, 0.15), verts=bm.verts[:])
    bmesh.ops.translate(bm, vec=(0.0, -0.24, rim + 0.005), verts=bm.verts[:])
    cutter = _finish("__bacia", bm, "louca")
    cut(body, cutter)
    cyl("Valvula", "cromo", (0.0, -0.24, rim - 0.145 + 0.0015), 0.021, 0.003, seg=24, r=0.0008)
    cyl("Base da torneira", "cromo", (0.0, -0.05, rim + 0.006), 0.024, 0.012, seg=24, r=0.003)
    cyl("Corpo da torneira", "cromo", (0.0, -0.05, rim + 0.058), 0.019, 0.093, seg=24, r=0.002)
    tube("Bica", "cromo", [(0.0, -0.052, rim + 0.075), (0.0, -0.10, rim + 0.075), (0.0, -0.125, rim + 0.062)], 0.008, fillet=0.02)
    # Chrome waste tail into a white corrugated PVC trap (sifao sanfonado, as in most Brazilian public
    # bathrooms) that loops down and back into the wall outlet's white rose.
    cyl("Saida da valvula", "cromo", (0.0, -0.24, rim - h - 0.035), 0.016, 0.07, seg=20, r=0.002)
    cyl("Porca da valvula", "pvc", (0.0, -0.24, rim - h - 0.075), 0.022, 0.02, seg=20, r=0.003)
    corrugated("Sifao sanfonado", "pvc", [(0.0, -0.24, rim - h - 0.085), (0.0, -0.24, rim - h - 0.20), (0.0, -0.20, rim - h - 0.26),
                                          (0.0, -0.13, rim - h - 0.24), (0.0, -0.06, rim - h - 0.19), (0.0, -0.02, rim - h - 0.19)],
               0.0165, 0.0195, pitch=0.009)
    cyl("Bucha da parede", "pvc", (0.0, -0.012, rim - h - 0.19), 0.021, 0.02, axis=(0, 1, 0), seg=20, r=0.003)
    cyl("Canopla", "pvc", (0.0, -0.003, rim - h - 0.19), 0.04, 0.006, axis=(0, 1, 0), seg=24, r=0.002)
    # Silicone in the crease where the flat back face (inside the 50 mm rounded edges) meets the tiles.
    tube("Silicone da parede", "selante", [(-w + 0.05, -0.0012, rim - 0.05), (w - 0.05, -0.0012, rim - 0.05)], 0.0022, res=2)
    cyl("Registro", "cromo", (0.10, -0.025, rim - h - 0.08), 0.012, 0.05, axis=(0, 1, 0), seg=16, r=0.002)
    cyl("Volante do registro", "cromo", (0.10, -0.055, rim - h - 0.08), 0.018, 0.012, axis=(0, 1, 0), seg=16, r=0.003)
    tube("Engate flexivel", "inox", [(0.10, -0.04, rim - h - 0.08), (0.08, -0.05, rim - h - 0.02), (0.01, -0.05, rim - h + 0.02)],
         0.005, fillet=0.03)


def build_espelho():
    """Mirror 0.50 x 0.70 m, 4 mm glass 2 mm off the wall with a 6 mm polished (bevelled) edge, four chrome
    clips. Not baked: the reflective face, the glass edge and the clips get their own Unity materials."""
    w, h = 0.25, 0.70
    placa = box("Placa", "borda", -w, -0.006, 0.0, w, -0.002, h, r=0.003, seg=1)
    face = box("Face espelhada", "espelho", -w + 0.006, -0.0062, 0.006, w - 0.006, -0.006, h - 0.006, r=0)
    planar_uv(face, 0, 2, -w, w, 0.0, h)
    planar_uv(placa, 0, 2, -w, w, 0.0, h)
    for sx in (-1, 1):
        for z in (0.0, h):
            c = box(f"Presilha {sx:+d} {z:.1f}", "cromo", sx * w - 0.012, -0.009, z - 0.010, sx * w + 0.012, 0.0, z + 0.010,
                    r=0.003, seg=1)
            planar_uv(c, 0, 2, -w, w, 0.0, h)


def build_saboneteira():
    """Wall soap dispenser: white body, smoked level window, grey push plate at the bottom."""
    box("Corpo", "plastico_branco", -0.055, -0.105, 0.0, 0.055, -0.004, 0.20, r=0.016, seg=3)
    box("Placa de parede", "plastico_branco", -0.05, -0.006, 0.01, 0.05, 0.0, 0.19, r=0.003, seg=1)
    box("Visor", "janela", -0.028, -0.107, 0.085, 0.028, -0.099, 0.165, r=0.004, seg=2)
    box("Botao", "cinza", -0.036, -0.115, 0.012, 0.036, -0.09, 0.05, r=0.006, seg=2)
    cyl("Bico", "cinza", (0.0, -0.06, -0.004), 0.006, 0.01, seg=12, r=0.002)


def build_toalheiro():
    """Interfolded paper towel dispenser: white body, level window, key lock, a towel hanging from the slot."""
    box("Corpo", "plastico_branco", -0.14, -0.13, 0.0, 0.14, -0.004, 0.33, r=0.022, seg=3)
    box("Placa de parede", "plastico_branco", -0.13, -0.006, 0.01, 0.13, 0.0, 0.32, r=0.003, seg=1)
    box("Visor", "janela", -0.045, -0.131, 0.17, 0.045, -0.12, 0.27, r=0.005, seg=2)
    cyl("Fechadura", "cinza", (0.0, -0.13, 0.31), 0.008, 0.006, axis=(0, 1, 0), seg=12, r=0.002)
    box("Fenda", "cinza", -0.11, -0.10, -0.004, 0.11, -0.03, 0.004, r=0.003, seg=1)
    box("Papel toalha", "papel", -0.095, -0.068, -0.075, 0.095, -0.066, 0.002, r=0.0008, seg=1)


def build_papeleira():
    """Jumbo toilet roll dispenser: round white body, smoked front window, roll tail at the serrated slot."""
    cyl("Corpo", "plastico_branco", (0.0, -0.06, 0.0), 0.135, 0.12, axis=(0, 1, 0), seg=48, r=0.018)
    cyl("Visor", "janela", (0.0, -0.121, 0.03), 0.075, 0.004, axis=(0, 1, 0), seg=40, r=0.002)
    box("Serrilha", "cinza", -0.05, -0.115, -0.138, 0.05, -0.05, -0.128, r=0.002, seg=1)
    box("Ponta do papel", "papel", -0.048, -0.10, -0.20, 0.048, -0.098, -0.13, r=0.0008, seg=1)


def build_barra():
    """Stainless grab bar 0.80 m, 32 mm tube 50 mm off the wall, round flanges (origin: wall, bar centre)."""
    half = 0.40
    tube("Barra", "inox", [(-half, 0.0, 0.0), (-half, -0.05, 0.0), (half, -0.05, 0.0), (half, 0.0, 0.0)], 0.016,
         fillet=0.035, res=4)
    for sx in (-1, 1):
        cyl(f"Flange {sx:+d}", "inox", (sx * half, -0.004, 0.0), 0.036, 0.008, axis=(0, 1, 0), seg=28, r=0.002)


def corrugated(name, mat, pts, r_in, r_out, pitch=0.009):
    """Corrugated hose along a smoothed path: rings alternating between two radii every half pitch."""
    path = base.smooth_path(pts, per_seg=24)
    lengths = [0.0]
    for a, b in zip(path, path[1:]):
        lengths.append(lengths[-1] + (Vector(b) - Vector(a)).length)
    bm = bmesh.new()
    seg, rings = 16, []
    total, s = lengths[-1], 0.0
    k = 0
    while s <= total + 1e-6:
        i = max(1, next((j for j, L in enumerate(lengths) if L >= s), len(lengths) - 1))
        a, b = Vector(path[i - 1]), Vector(path[i])
        f = (s - lengths[i - 1]) / max(1e-9, lengths[i] - lengths[i - 1])
        c, d = a.lerp(b, f), (b - a).normalized()
        side = d.cross(Vector((1, 0, 0)))
        if side.length < 1e-4:
            side = d.cross(Vector((0, 1, 0)))
        side.normalize()
        up = d.cross(side)
        rad = r_out if k % 2 else r_in
        rings.append([bm.verts.new(c + (side * math.cos(t) + up * math.sin(t)) * rad)
                      for t in (2 * math.pi * j / seg for j in range(seg))])
        s += pitch / 2
        k += 1
    for ra, rb in zip(rings, rings[1:]):
        for j in range(seg):
            bm.faces.new((ra[j], ra[(j + 1) % seg], rb[(j + 1) % seg], rb[j]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return _finish(name, bm, mat)


def build_vaso_botao():
    """Full-flush button of the dual plate (moves when pressed; in the toilet's frame)."""
    V = VASO
    top = V["rim"] + V["tank"][2] + 0.024
    box("Botao de descarga total", "cromo", 0.0015, -0.125, top - 0.001, 0.028, -0.075, top + 0.004, r=0.0015, seg=1)


def build_lavatorio_botao():
    """Push button of the self-closing tap (moves when pressed; in the basin's frame)."""
    rim = LAVATORIO["rim"]
    cyl("Botao da torneira", "cromo", (0.0, -0.05, rim + 0.112), 0.017, 0.015, seg=24, r=0.004)


def build_ralo():
    """Square stainless floor drain grate 150 x 150 mm on a sunken PVC body: slotted grate with a rim,
    sitting 1 mm above the floor tile (origin on the floor at the grate's centre)."""
    h, top = 0.075, 0.0045
    box("Corpo do ralo", "pvc_cinza", -0.08, -0.08, top - 0.03, 0.08, 0.08, top - 0.006, r=0.002)
    box("Moldura", "inox", -h, -h, top - 0.004, h, h, top, r=0.0015, seg=1)
    box("Grelha", "inox", -h + 0.008, -h + 0.008, top - 0.0035, h - 0.008, h - 0.008, top + 0.0005, r=0.0005, seg=1)
    for k in range(-5, 6):
        box(f"Fenda {k}", "fenda", -h + 0.018, k * 0.0115 - 0.0028, top - 0.0028, h - 0.018, k * 0.0115 + 0.0028, top + 0.0007,
            r=0, seg=1)


def build_escova():
    """Toilet brush holder on the floor: white plastic pot with a lid ring, the brush handle standing up."""
    lathe("Pote", "plastico_branco", [(0.0, 0.0), (0.048, 0.0), (0.052, 0.01), (0.052, 0.115), (0.047, 0.125), (0.040, 0.125),
                                      (0.040, 0.02), (0.0, 0.02)], seg=36, r=0.0)
    cyl("Aro da tampa", "cinza", (0.0, 0.0, 0.123), 0.046, 0.008, seg=36, r=0.003)
    cyl("Cabo da escova", "cinza", (0.004, 0.0, 0.25), 0.0075, 0.26, seg=14, r=0.003)
    cyl("Ponta do cabo", "cinza", (0.004, 0.0, 0.381), 0.0095, 0.012, seg=14, r=0.004)


def build_exaustor():
    """Ceiling exhaust grille 250 x 250 mm (origin on the ceiling plane, body hanging down): white frame,
    angled louvres, two screws."""
    s, d = 0.125, 0.012
    box("Moldura do exaustor", "plastico_branco", -s, -s, -d, s, s, 0.0, r=0.003, seg=2)
    box("Fundo escuro", "fenda", -s + 0.02, -s + 0.02, -d - 0.001, s - 0.02, s - 0.02, -d + 0.002, r=0, seg=1)
    for k in range(-5, 6):
        y = k * 0.019
        hull(f"Aleta {k}", "plastico_branco", [(sx * (s - 0.02), y + dy, -d - 0.001 + dz) for sx in (-1, 1)
                                                for dy, dz in ((-0.008, 0.0), (0.008, 0.007), (-0.007, 0.0015), (0.009, 0.0085))],
             r=0.0006, seg=1)
    for sx in (-1, 1):
        cyl(f"Parafuso {sx:+d}", "cinza", (sx * (s - 0.01), 0.0, -d - 0.0005), 0.0035, 0.002, seg=12, r=0.0008)


# ---------------------------------------------------------------- bathroom door

def build_porta_wc_fixos():
    """Painted frame in the 1.0 x 2.2 m opening: jambs, head, stop on the reception side, mitred casings
    (16 mm, thicker than the skirting) on both faces, granite threshold, rubber floor stop at 97 degrees."""
    P = PORTA_WC
    hw, jt, H = P["raw"] / 2, P["jamb"], P["height"]
    jx, top = hw - jt, H - jt
    for sx in (-1, 1):
        x0, x1 = sorted((sx * jx, sx * hw))
        box(f"Marco {sx:+d}", "laca", x0, -0.105, 0.0, x1, 0.105, H, r=0.002, seg=2)
        s0, s1 = sorted((sx * (jx - 0.015), sx * jx))
        box(f"Batente {sx:+d}", "laca", s0, -0.065, 0.0, s1, -0.025, top, r=0.0015, seg=2)
    box("Travessa", "laca", -jx, -0.105, top, jx, 0.105, H, r=0.002, seg=2)
    box("Batente superior", "laca", -jx, -0.065, top - 0.015, jx, -0.025, top, r=0.0015, seg=2)
    cw, ct, step = 0.06, 0.016, 0.005
    ix, iz, ox, oz = jx + step, top + step, jx + step + cw, top + step + cw
    for side in (-1, 1):
        y0, y1 = sorted((side * 0.1, side * (0.1 + ct)))
        for sx in (-1, 1):
            poly = [(sx * ix, 0.003), (sx * ox, 0.003), (sx * ox, oz), (sx * ix, iz)]
            extrude(f"Guarnicao {side:+d} {sx:+d}", "laca", poly if sx > 0 else poly[::-1], "XZ", y0, y1, r=0.003, seg=2)
        extrude(f"Guarnicao superior {side:+d}", "laca", [(-ix, iz), (ix, iz), (ox, oz), (-ox, oz)], "XZ", y0, y1, r=0.003, seg=2)
    box("Soleira", "granito", -hw, -0.105, -0.01, hw, 0.105, 0.006, r=0.003, seg=2)
    # Floor stop just past the leaf's maximum opening, under its outer third (the leaf turns from -X to +Y).
    px, py = P["pivot"]
    a, r = math.radians(P["max_open"] + 2.5), P["leaf_w"] - 0.06
    sx, sy = px - r * math.cos(a), py + r * math.sin(a)
    cyl("Batedor de piso", "cromo", (sx, sy, 0.017), 0.02, 0.034, seg=20, r=0.003)
    cyl("Borracha do batedor", "borracha", (sx, sy, 0.037), 0.018, 0.008, seg=20, r=0.002)


def build_porta_wc_folha():
    """Flush leaf (leaf frame: origin on the hinge axis at floor level, leaf towards -X): three butt hinges,
    lever handles with roses on both faces, key rose below, stainless kick plates on both faces."""
    P = PORTA_WC
    px, py = P["pivot"]
    x_hinge = (P["raw"] / 2 - P["jamb"] - P["gap"]) - px            # leaf hinge edge in the leaf frame
    x0, x1 = x_hinge - P["leaf_w"], x_hinge
    y0, y1 = -0.02 - py, -0.02 + P["leaf_t"] - py
    z0, z1 = P["floor_gap"], P["floor_gap"] + P["leaf_h"]
    box("Folha", "folha", x0, y0, z0, x1, y1, z1, r=0.002, seg=2)
    for z in (0.25, 1.05, 1.90):
        cyl(f"Dobradica {z:.2f}", "inox", (0.0, 0.0, z), 0.0065, 0.09, seg=16, r=0.0015)
        box(f"Aba da dobradica {z:.2f}", "inox", x1 - 0.002, -0.004, z - 0.045, x1 + 0.0005, 0.0, z + 0.045, r=0.0005, seg=1)
    for face, y in ((-1, y0), (1, y1)):
        box(f"Chapa de protecao {face:+d}", "inox", x0 + 0.03, min(y, y + face * 0.0012), z0 + 0.005,
            x1 - 0.03, max(y, y + face * 0.0012), z0 + 0.205, r=0.0005, seg=1)
        hx = x0 + 0.065
        cyl(f"Roseta {face:+d}", "inox", (hx, y + face * 0.0045, 1.0), 0.026, 0.009, axis=(0, 1, 0), seg=28, r=0.002)
        tube(f"Macaneta {face:+d}", "inox", [(hx, y + face * 0.009, 1.0), (hx, y + face * 0.055, 1.0),
                                              (hx + 0.12, y + face * 0.06, 1.0)], 0.009, fillet=0.02, res=3)
        cyl(f"Roseta da chave {face:+d}", "inox", (hx, y + face * 0.004, 0.88), 0.02, 0.008, axis=(0, 1, 0), seg=24, r=0.002)
        box(f"Entrada da chave {face:+d}", "inox_escuro", hx - 0.002, y + face * 0.0085 - 0.0005, 0.872, hx + 0.002,
            y + face * 0.0085 + 0.0005, 0.89, r=0.0003, seg=1)
    # Coat hook on the bathroom face (+Y), centred, at 1.65 m.
    hx, hy = (x0 + x1) / 2, y1
    cyl("Base do cabide", "inox", (hx, hy + 0.002, 1.65), 0.016, 0.004, axis=(0, 1, 0), seg=24, r=0.0012)
    tube("Cabide", "inox", [(hx, hy + 0.003, 1.65), (hx, hy + 0.045, 1.645), (hx, hy + 0.055, 1.67), (hx, hy + 0.05, 1.69)],
         0.0045, fillet=0.012, res=3)


BUILDERS = {"VasoSanitario": build_vaso, "Lavatorio": build_lavatorio, "Espelho": build_espelho,
            "Saboneteira": build_saboneteira, "ToalheiroPapel": build_toalheiro, "PapeleiraRolao": build_papeleira,
            "BarraApoio": build_barra, "PortaWCFixos": build_porta_wc_fixos, "PortaWCFolha": build_porta_wc_folha,
            "VasoBotao": build_vaso_botao, "LavatorioBotao": build_lavatorio_botao, "Ralo": build_ralo,
            "EscovaSanitaria": build_escova, "Exaustor": build_exaustor}


def build(asset):
    c = _coll(asset)
    for o in list(c.objects):
        me = o.data
        bpy.data.objects.remove(o)
        if me and me.users == 0:
            bpy.data.meshes.remove(me)
    base._current["asset"] = asset
    BUILDERS[asset]()
    return list(c.objects)

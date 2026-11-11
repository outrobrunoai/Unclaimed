"""Camara fria: seven-body modular mortuary cabinets (approved 2 x 2 plus a 1 x 3 tower), original metre-scale models baked with the
project's PBR pipeline (Recepcao helpers, MesaAutopsia materials and baker).

Reference set (docs/revisao-camara-fria/referencias.md):
  main      Grumed modular camara mortuaria (individual insulated doors, pivot hinges, high-pressure latches,
            bakelite pulls, polycarbonate record holder, telescopic rails on nylon rollers, removable trays).
  sizes     Tectermica "Camara frigorifica para cadaveres" sheet: 2-high stack 1590 mm + 100 mm base, 1165 mm per
            column, 2330 mm deep, individual door 550 x 800 mm, top division 750 mm (+100), 0/2 C, split system.
  details   Iromtec: 2.5" stainless legs with polyethylene levelling feet, floor sloped to a drain, interior light.

Module frame (Blender): front face on y = 0 (doors and trays come out towards -Y), depth +Y, x across the width
(-x = left column seen from the front), z up from the room floor. Unity local = (x, z, y).

Run: blender -b --factory-startup --python Blender/CamaraFria/scripts/gerar.py [-- Name ...]
SO_MALHAS=1 skips baking. Writes Blender/CamaraFria/revisao/pivos.json (pivots for the Unity prefab).
"""
import os, sys, math, json, types
import bpy, bmesh
from mathutils import Vector, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
OUT = ROOT + "/Assets/Modelos/CamaraFria"
SRC = ROOT + "/Blender/CamaraFria"
ONLY = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else None
if ONLY and os.path.exists(SRC + "/CamaraFria.blend"):
    bpy.ops.wm.open_mainfile(filepath=SRC + "/CamaraFria.blend")


def load(rel):
    p = ROOT + "/" + rel
    ns = {"__name__": "camara_module", "__file__": p}
    exec(compile(open(p).read(), p, "exec"), ns)
    return types.SimpleNamespace(**ns)


R = load("Blender/Recepcao/scripts/recepcao_modelo.py")
G = load("Blender/MesaAutopsia/scripts/mesa_materiais.py").G
B = load("Blender/MesaAutopsia/scripts/mesa_bake.py")
box, cyl, tube = R.box, R.cyl, R.tube


def material(key, color, rough, metallic=0, brushed=False, grain=1.0):
    m = bpy.data.materials.new("Camara fria - " + key)
    g = G(m)
    n = g.noise(g.obj(), 7, 3)
    # Brushed grain runs along X (the long horizontal direction of every sheet on the cabinet front).
    fine = g.noise(g.coords(scale=(3, 330, 330) if brushed else (150, 150, 150)), 1, 2)
    base = g.color(n, tuple(c * (1 - .06 * grain) for c in color), tuple(c * (1 + .035 * grain) for c in color))
    rr = g.mrange(n, .15, .85, rough - .05 * grain, rough + .06 * grain)
    g.finish(base, rr, metallic, fine, bump_strength=.22, bump_dist=.00008 if brushed else .00016)
    return m


# Same family as the receiving room (satin stainless, graphite rubber, nylon) so the service wing reads as one fit-out.
MATS = {
    "inox": material("inox acetinado", (.55, .57, .56), .36, .94, True),
    "inox_interno": material("inox interno", (.50, .52, .51), .42, .92, True),
    # Localized wear: grips and rail running faces are polished by hands and rollers, a touch brighter and smoother.
    "inox_gasto": material("inox polido pelo uso", (.62, .64, .63), .24, .96, True, .6),
    "cromado": material("cromado", (.67, .69, .69), .20, .96, True),
    "escuro": material("aco nas juntas", (.13, .145, .14), .48, .8),
    "borracha": material("borracha grafite", (.025, .03, .029), .72),
    "gaxeta": material("gaxeta magnetica", (.045, .05, .05), .62),
    "nylon": material("nylon dos roletes", (.62, .62, .58), .44),
    "baquelite": material("baquelite", (.03, .03, .032), .38),
    "baquelite_gasta": material("baquelite gasta", (.06, .058, .056), .30, 0, False, .5),
    "polietileno": material("polietileno das sapatas", (.30, .31, .30), .58),
    "plastico": material("plastico cinza claro", (.62, .63, .61), .46),
    "aletas": material("aletas de aluminio", (.42, .44, .44), .40, .85),
    "isolante": material("espuma elastomerica", (.035, .036, .038), .80),
    "cobre": material("cobre", (.62, .36, .24), .32, .9),
    "pvc": material("eletroduto cinza", (.46, .47, .46), .55),
    "pintura": material("chapa pintada bege", (.62, .60, .55), .50, 0, False, .8),
    "galvanizado": material("aco galvanizado", (.50, .52, .53), .45, .85),
    "aluminio": material("aluminio anodizado", (.66, .67, .67), .34, .9, True),
    "melamina": material("quadro branco", (.80, .81, .80), .18),
}

# ---------------------------------------------------------------- dimensions (metres)
W, D, H = 2.33, 2.33, 1.69          # 2 columns of 1165 mm, 2330 mm deep, 1590 mm stack on a 100 mm base
BASE, WALL = .10, .08               # leg height; insulated panel thickness (80 mm PU for chilled units)
COLS = (-.5825, .5825)              # column centres
DOOR_W, DOOR_H, DOOR_T = .80, .55, .060
DOOR_Z = (.235, .985)               # bottom of the lower / upper door
INSET = .03                         # door overlap over the opening, each side (gasket land)
GASKET_GAP = .012                   # compressed magnetic gasket between door and frame
PIN = (.012, -.045)                 # hinge pin: beyond the door edge, in front of the frame face
DIV = (.855, .885)                  # stainless partition between levels (interior)
RAIL_Z = (.300, 1.050)              # top of the fixed rail channel per level
TRAY_LEN, TRAY_W = 1.88, .60
TRAY_Y0 = .135                      # tray front tube when closed (behind the door and the frame throat)
STAGE1 = 1.00                       # first stop of the 2-stage telescopic rail (inspection); full = 1.95
FULL = 1.95

def opening(c, lvl):
    cx = COLS[c]; z0 = DOOR_Z[lvl]
    return cx - DOOR_W / 2 + INSET, z0 + INSET * .67, cx + DOOR_W / 2 - INSET, z0 + DOOR_H - INSET * .67

def hinge_side(c):
    return -1 if c == 0 else 1      # hinges on the outer side of each column, latches meet at the mullion


# ---------------------------------------------------------------- cabinet (static)

def leg(x, y):
    # 2.5" stainless tube, threaded polyethylene levelling foot, welded top plate.
    cyl("Perna tubular", "inox", (x, y, .055), .0318, .07, seg=24, r=.002)
    cyl("Rosca niveladora", "escuro", (x, y, .016), .009, .012, seg=12)
    cyl("Sapata niveladora", "polietileno", (x, y, .006), .03, .012, seg=24, r=.003)
    box("Chapa de fixacao da perna", "inox", x - .05, y - .05, BASE - .006, x + .05, y + .05, BASE, r=.002)


def drain_valve(x):
    # Each column floor slopes to a drain under the front edge (washdown), closed by a quarter-turn valve.
    cyl("Saida de dreno", "inox", (x, .22, BASE - .03), .016, .06, seg=16)
    cyl("Registro de dreno", "cromado", (x, .22, BASE - .07), .02, .03, seg=16)
    box("Alavanca do registro", "baquelite", x - .004, .22, BASE - .078, x + .004, .30, BASE - .066, r=.002)


def cabinet(w=W, h=H, cols=COLS, door_z=DOOR_Z, rail_z=RAIL_Z, divisions=(DIV,)):
    # Defaults retain the approved 2 x 2 geometry; the tower shares the same construction.
    W, H, COLS, DOOR_Z, RAIL_Z = w, h, cols, door_z, rail_z
    DIV = divisions[0]
    def opening(c, lvl):
        cx = COLS[c]; z0 = DOOR_Z[lvl]
        return cx - DOOR_W / 2 + INSET, z0 + INSET * .67, cx + DOOR_W / 2 - INSET, z0 + DOOR_H - INSET * .67
    # Shell: insulated panels; their inner faces are the stainless liner seen through an open door.
    # Bottom and top start behind the front panel (no coincident faces with it).
    box("Fundo isolado", "inox", -W / 2, WALL, BASE, W / 2, D, BASE + WALL, r=.004)
    box("Teto isolado", "inox", -W / 2, WALL, H - WALL, W / 2, D, H, r=.004)
    for s in (-1, 1):
        box("Lateral isolada", "inox", s * W / 2 - (WALL if s > 0 else 0), WALL, BASE + WALL,
            s * W / 2 + (0 if s > 0 else WALL), D - WALL, H - WALL, r=.004)
    box("Fundo traseiro isolado", "inox", -W / 2, D - WALL, BASE + WALL, W / 2, D, H - WALL, r=.004)
    if len(COLS) == 2:
        box("Divisoria entre colunas", "inox_interno", -.05, WALL, BASE + WALL, .05, D - WALL, H - WALL, r=.003)
    # Front panel with four openings (boolean, so the reveals are real faces).
    front = box("Painel frontal", "inox", -W / 2, 0, BASE, W / 2, WALL, H, r=.004)
    for c in range(len(COLS)):
        for lvl in range(len(DOOR_Z)):
            x0, z0, x1, z1 = opening(c, lvl)
            R.cut(front, box("corte", "inox", x0, -.05, z0, x1, WALL + .05, z1, r=0))
    # Reveal trim: a pressed stainless channel lining each throat (inside the opening, 0.5 mm behind the face).
    for c in range(len(COLS)):
        for lvl in range(len(DOOR_Z)):
            x0, z0, x1, z1 = opening(c, lvl)
            t = .012
            for a in ((x0, z0, x1, z0 + t), (x0, z1 - t, x1, z1), (x0, z0 + t, x0 + t, z1 - t), (x1 - t, z0 + t, x1, z1 - t)):
                box("Requadro da abertura", "inox", a[0], .0005, a[1], a[2], WALL - .004, a[3], r=.0015)
    # Partitions between levels: stop short of the back wall so cold air returns from the lower level.
    for c in range(len(COLS)):
        xa, xb = ((-W / 2 + WALL, -.05) if c == 0 else (.05, W / 2 - WALL)) if len(COLS) == 2 else (-W / 2 + WALL, W / 2 - WALL)
        for lo, hi in divisions:
            box("Prateleira divisoria", "inox_interno", xa, WALL, lo, xb, D - WALL - .16, hi, r=.003)
        # Sloped floor pan of each column (washdown) towards the front drain.
        bm = bmesh.new()
        y0, y1 = WALL + .005, D - WALL - .005
        v = [bm.verts.new(p) for p in ((xa + .005, y0, BASE + WALL + .002), (xb - .005, y0, BASE + WALL + .002),
                                         (xb - .005, y1, BASE + WALL + .014), (xa + .005, y1, BASE + WALL + .014))]
        bm.faces.new(v)
        R._finish("Fundo com caimento", bm, "inox_interno")
        cyl("Ralo interno", "escuro", ((xa + xb) / 2, .22, BASE + WALL + .0025), .022, .002, seg=20, r=0)
        drain_valve((xa + xb) / 2)
        # Evaporator at the back of the upper level: finned coil behind a louvred cover, two axial fans.
        ex0, ex1 = (xa + xb) / 2 - .40, (xa + xb) / 2 + .40
        ey0, ey1 = D - WALL - .19, D - WALL - .005
        ez0, ez1 = H - WALL - .27, H - WALL - .005
        box("Caixa do evaporador", "inox_interno", ex0, ey0, ez0, ex1, ey1, ez1, r=.004)
        for i in range(2):
            fx = ex0 + .20 + i * .40
            cyl("Grade do ventilador", "escuro", (fx, ey0 - .003, (ez0 + ez1) / 2), .095, .006, (0, 1, 0), seg=32, r=.001)
            cyl("Cubo do ventilador", "plastico", (fx, ey0 - .007, (ez0 + ez1) / 2), .03, .008, (0, 1, 0), seg=20, r=.002)
            for k in range(4):
                beam_y(fx, ey0 - .008, (ez0 + ez1) / 2, .088, k * math.pi / 2 + .4)
        # Bottom of the evaporator: condensate tray with a drain tube down the back corner.
        box("Bandeja de condensado", "inox_interno", ex0, ey0 - .01, ez0 - .02, ex1, ey1, ez0, r=.002)
        tube("Dreno do evaporador", "escuro", [(ex1 - .03, ey1 - .03, ez0 - .02), (ex1 - .03, ey1 - .03, BASE + WALL + .03)], .008, res=2)
        # Sealed compartment luminaires, one per level near the door (reference: a light point per door).
        for lvl in range(len(DOOR_Z)):
            zc = (divisions[lvl][0] if lvl < len(divisions) else H - WALL) - .002
            box("Luminaria do compartimento", "plastico", (xa + xb) / 2 - .16, .16, zc - .022, (xa + xb) / 2 + .16, .26, zc, r=.006)
        # Tubular stacking frame: four posts per column holding the rail channels of both levels.
        for sx in (-1, 1):
            px = COLS[c] + sx * .40
            for py in (.11, D - WALL - .23):
                for lo, hi in zip((BASE + WALL,) + tuple(d[1] for d in divisions),
                                  tuple(d[0] for d in divisions) + (H - WALL - .28,)):
                    cyl("Montante tubular", "inox_interno", (px, py, (lo + hi) / 2), .0127, hi - lo, seg=16)
            for lvl in range(len(DOOR_Z)):
                rail_fixed(COLS[c], sx, RAIL_Z[lvl])
    for x in ((-W / 2 + .07, 0, W / 2 - .07) if len(COLS) == 2 else (-W / 2 + .07, W / 2 - .07)):
        for y in (.12, D - .12):
            leg(x, y)
    # Kick plate in front of the legs and a shallow top cornice carrying the position plates.
    box("Rodape frontal", "inox", -W / 2 + .01, -.006, BASE + .01, W / 2 - .01, -.0005, DOOR_Z[0] - .015, r=.002)
    box("Friso superior", "inox", -W / 2, -.008, DOOR_Z[-1] + DOOR_H + .015, W / 2, -.0005, H - .006, r=.002)
    # Frame-side hinge leaves and latch strikes.
    for c in range(len(COLS)):
        for lvl in range(len(DOOR_Z)):
            hs = hinge_side(c)
            hx = COLS[c] + hs * (DOOR_W / 2 + PIN[0])
            for hz in (DOOR_Z[lvl] + .07, DOOR_Z[lvl] + DOOR_H - .13):
                # Lower half only (the door knuckle sits above it), clear of the door edge by 6 mm.
                box("Base da dobradica", "inox", hx - .006 if hs > 0 else hx - .022, PIN[1] - .006, hz,
                    hx + .022 if hs > 0 else hx + .006, -.0005, hz + .03, r=.002)
                cyl("Junta da dobradica", "inox", (hx, PIN[1], hz + .015), .011, .03, seg=16, r=.0015)
            # Strike on the frame next to the free edge, at the latch height.
            sx = COLS[c] - hs * (DOOR_W / 2 + .020)
            sz = DOOR_Z[lvl] + DOOR_H / 2
            box("Batente do trinco", "cromado", sx - .012, -.026, sz - .03, sx + .012, 0, sz + .03, r=.003)
            box("Ressalto do batente", "inox_gasto", sx - .010, -.030, sz - .008, sx + .010, -.024, sz + .008, r=.002)


def beam_y(x, y, z, length, ang):
    # One fan blade seen through the guard.
    v = Vector((math.cos(ang), 0, math.sin(ang)))
    a, b = Vector((x, y, z)) + v * .03, Vector((x, y, z)) + v * length
    ob = box("Pa do ventilador", "plastico", -.016, -.002, 0, .016, .002, (b - a).length, r=.001)
    ob.data.transform(Matrix.Translation(a) @ Vector((0, 0, 1)).rotation_difference(b - a).to_matrix().to_4x4()
                      @ Matrix.Rotation(.35, 4, "Z"))
    return ob


def rail_fixed(cx, sx, zt):
    """Fixed outer C-channel of one side (web outside, flanges pointing to the tray), bolted to the posts."""
    xw = cx + sx * .355
    xi = cx + sx * .325
    y0, y1 = .10, D - WALL - .10
    lo, hi = min(xw, xi), max(xw, xi)
    box("Trilho fixo - alma", "inox_interno", xw - .002 if sx > 0 else xw, y0, zt - .046, xw if sx > 0 else xw + .002, y1, zt, r=0)
    box("Trilho fixo - aba superior", "inox_interno", lo, y0, zt - .003, hi, y1, zt, r=0)
    box("Trilho fixo - aba inferior", "inox_gasto", lo, y0, zt - .046, hi, y1, zt - .043, r=0)
    box("Batente do trilho", "escuro", lo, y0 - .006, zt - .046, hi, y0, zt, r=.001)
    for py in (.11, D - WALL - .23):
        box("Suporte do trilho", "inox_interno", min(cx + sx * .40, xw), py - .02, zt - .05, max(cx + sx * .40, xw), py + .02, zt - .04, r=.001)


# ---------------------------------------------------------------- moving parts

def door(side):
    """side = +1: hinge on the right (+x) edge; -1: mirrored. Built at the right column, lower level, then the
    whole mesh is moved so the hinge pin is the origin (Unity rotates the door about it)."""
    cx, z0 = COLS[1], 0.0
    xa, xb = cx - DOOR_W / 2, cx + DOOR_W / 2
    yf, yb = -GASKET_GAP - DOOR_T, -GASKET_GAP
    # Leaf: outer skin with rounded edges, insulated core shows as a thick edge; inner pan steps back.
    box("Folha da porta", "inox", xa, yf, z0, xb, yb - .018, z0 + DOOR_H, r=.006)
    box("Contra-porta", "inox_interno", xa + .035, yb - .018, z0 + .035, xb - .035, yb - .002, z0 + DOOR_H - .035, r=.004)
    # Magnetic gasket: continuous bulb profile on the inner face, just outside the inner pan.
    g0, g1 = .014, .032
    for a in ((xa + g0, z0 + g0, xb - g0, z0 + g1), (xa + g0, z0 + DOOR_H - g1, xb - g0, z0 + DOOR_H - g0),
              (xa + g0, z0 + g1, xa + g1, z0 + DOOR_H - g1), (xb - g1, z0 + g1, xb - g0, z0 + DOOR_H - g1)):
        box("Gaxeta magnetica", "gaxeta", a[0], yb - .018, a[1], a[2], -.0006, a[3], r=.004)
    # Hinge straps and knuckles on the door (the frame carries the lower halves).
    hx = xb + PIN[0]
    for hz in (z0 + .07, z0 + DOOR_H - .13):
        box("Aba da dobradica", "inox", xb - .11, yf - .005, hz + .03, hx + .008, yf + .001, hz + .06, r=.002)
        box("Ponte da dobradica", "inox", xb - .004, PIN[1] - .01, hz + .03, hx + .008, yf + .001, hz + .06, r=.002)
        cyl("Junta da dobradica", "inox", (hx, PIN[1], hz + .045), .011, .03, seg=16, r=.0015)
        cyl("Pino da dobradica", "cromado", (hx, PIN[1], hz + .064), .0055, .008, seg=12, r=.001)
        for bx in (xb - .09, xb - .04):
            cyl("Parafuso da aba", "cromado", (bx, yf - .006, hz + .045), .0045, .003, (0, 1, 0), seg=10, r=.0008)
    # High-pressure latch at the free edge: chromed body, cam reaching the strike, bakelite pull lever.
    lx, lz = xa + .06, z0 + DOOR_H / 2
    box("Corpo do trinco", "cromado", lx - .04, yf - .022, lz - .045, lx + .03, yf, lz + .045, r=.006)
    box("Came do trinco", "cromado", xa + .004, yf - .016, lz - .012, lx - .03, yf - .004, lz + .012, r=.003)
    cyl("Eixo da alavanca", "cromado", (lx, yf - .028, lz + .02), .009, .016, (0, 1, 0), seg=16, r=.001)
    tube("Alavanca do trinco", "baquelite", [(lx, yf - .034, lz + .02), (lx + .04, yf - .05, lz + .02), (lx + .17, yf - .05, lz + .02)],
         .011, fillet=.02, res=2)
    tube("Empunhadura gasta", "baquelite_gasta", [(lx + .09, yf - .05, lz + .02), (lx + .165, yf - .05, lz + .02)], .0118, res=2)
    # Polycarbonate record holder (the card and clear window are separate Unity quads).
    hx0, hz0 = cx - .09, z0 + DOOR_H - .17
    box("Moldura do porta-etiqueta", "plastico", hx0, yf - .004, hz0, hx0 + .18, yf, hz0 + .12, r=.003)
    box("Fundo do porta-etiqueta", "plastico", hx0 + .008, yf - .0015, hz0 + .008, hx0 + .172, yf - .0005, hz0 + .112, r=0)
    for bx in (hx0 + .012, hx0 + .168):
        cyl("Rebite do porta-etiqueta", "cromado", (bx, yf - .0045, hz0 + .06), .003, .002, (0, 1, 0), seg=8, r=.0005)
    if side < 0:
        mirror_current(cx)
    # Pin becomes the origin; for the mirrored door the pin lies on the other edge.
    pin = Vector((cx + side * (DOOR_W / 2 + PIN[0]), PIN[1], z0))
    for o in R._coll(R._current["asset"]).objects:
        o.data.transform(Matrix.Translation(-pin))


def mirror_current(cx):
    for o in R._coll(R._current["asset"]).objects:
        o.data.transform(Matrix.Translation((cx, 0, 0)) @ Matrix.Scale(-1, 4, (1, 0, 0)) @ Matrix.Translation((-cx, 0, 0)))
        o.data.flip_normals()


def stage():
    """Intermediate telescopic member, both sides: a nested channel that runs at half the tray speed.
    Origin = compartment reference (column centre, frame front, top of the fixed channel)."""
    for sx in (-1, 1):
        xw, xi = sx * .349, sx * .331
        lo, hi = min(xw, xi), max(xw, xi)
        y0, y1 = .12, .12 + 1.86
        box("Trilho intermediario - alma", "inox", xw - .0015 if sx > 0 else xw, y0, -.040, xw if sx > 0 else xw + .0015, y1, -.006, r=0)
        box("Trilho intermediario - aba", "inox_gasto", lo, y0, -.040, hi, y1, -.0375, r=0)
        box("Trilho intermediario - aba", "inox", lo, y0, -.0085, hi, y1, -.006, r=0)
        for y in (y0 + .03, y1 - .03):
            cyl("Rolete do trilho", "nylon", (sx * .345, y, -.023), .0105, .006, (1, 0, 0), seg=16, r=.001)


def maca():
    """Removable tray: tubular frame, pressed concave pan, handles at both ends, side rollers that run in the
    intermediate channel. Origin = compartment reference (closed position)."""
    zf = .028                                     # frame tube centre above the channel top
    y0, y1 = TRAY_Y0, TRAY_Y0 + TRAY_LEN
    hw = TRAY_W / 2
    tube("Quadro tubular", "inox", [(-hw, y0, zf), (hw, y0, zf), (hw, y1, zf), (-hw, y1, zf), (-hw, y0, zf)], .0125, fillet=.05, res=2)
    # The pan rests on three cross tubes at the frame height (its bottom just above them).
    for y in (y0 + .30, y0 + .94, y0 + 1.58):
        tube("Travessa do quadro", "inox", [(-hw, y, zf), (hw, y, zf)], .009, res=2)
    pan(TRAY_W - .05, TRAY_LEN - .06, zf + .010, .034, (y0 + y1) / 2)
    for y, s in ((y0, -1), (y1, 1)):
        tube("Alca da maca", "inox_gasto", [(-.17, y, zf), (-.17, y + s * .075, zf + .012), (.17, y + s * .075, zf + .012), (.17, y, zf)],
             .0115, fillet=.03, res=2)
    for sx in (-1, 1):
        for y in (y0 + .10, y0 + .75, y1 - .55, y1 - .08):
            box("Suporte do rolete", "inox", sx * hw - .004 if sx > 0 else sx * hw - .018, y - .015, -.026, sx * hw + .018 if sx > 0 else sx * hw + .004,
                y + .015, zf - .008, r=.001)
            cyl("Rolete da maca", "nylon", (sx * .334, y, -.023), .0105, .008, (1, 0, 0), seg=16, r=.001)
            cyl("Eixo do rolete", "cromado", (sx * .318, y, -.023), .0035, .026, (1, 0, 0), seg=8, r=.0005)
    # Identification clip on the front handle (a tag can hang here; the record itself is on the door).
    box("Grampo de identificacao", "plastico", -.03, y0 - .085, zf + .002, .03, y0 - .07, zf + .03, r=.002)


def pan(w, d, z, depth, yc):
    rings = [(w / 2 - .03, d / 2 - .03, z), (w / 2, d / 2, z + depth), (w / 2 - .006, d / 2 - .006, z + depth + .004),
             (w / 2 - .036, d / 2 - .036, z + .006)]
    verts = []
    for a, b, h in rings:
        for x, y in [(-a, -b), (a, -b), (a, b), (-a, b)]:
            verts.append((x, y + yc, h))
    faces = [(0, 3, 2, 1), (12, 13, 14, 15)]
    for k in range(3):
        for j in range(4):
            faces.append((k * 4 + j, k * 4 + (j + 1) % 4, (k + 1) * 4 + (j + 1) % 4, (k + 1) * 4 + j))
    me = bpy.data.meshes.new("Bandeja concava"); me.from_pydata(verts, [], faces); me.update()
    ob = bpy.data.objects.new("Bandeja concava", me); R._coll(R._current["asset"]).objects.link(ob); ob["mat"] = "inox"
    mod = ob.modifiers.new("Bordas arredondadas", "BEVEL"); mod.width = .008; mod.segments = 3
    bpy.context.view_layer.objects.active = ob; ob.select_set(True); bpy.ops.object.modifier_apply(modifier=mod.name); ob.select_set(False)
    # Drain hole at the foot end of the pan.
    cyl("Furo de escoamento", "escuro", (0, yc + d / 2 - .07, z + .0005), .009, .001, seg=16, r=0)


# ---------------------------------------------------------------- equipment (static, room specific)
# Wall items: origin on the wall plane at the centre of the back face, front towards -Y (into the room).

def controlador():
    """Digital temperature controller with alarm (reference: digital panel + out-of-range alarm), wall box in grey
    ABS with a recessed LCD window (the readout is a Unity quad), four membrane keys, cable glands underneath."""
    w, h, d = .20, .15, .075
    box("Caixa do controlador", "plastico", -w / 2, -d, -h / 2, w / 2, 0, h / 2, r=.008)
    box("Moldura do visor", "baquelite", -.052, -d - .002, -.005, .052, -d + .001, .036, r=.003)
    for i in range(4):
        box("Tecla de membrana", "plastico", -.066 + i * .044, -d - .0025, -.052, -.034 + i * .044, -d + .001, -.030, r=.003)
    for x in (-.07, .07):
        cyl("Parafuso da tampa", "cromado", (x, -d - .001, .062), .004, .002, (0, 1, 0), seg=10, r=.0005)
        cyl("Prensa-cabo", "baquelite", (x * .5, -d / 2, -h / 2 - .008), .009, .016, seg=12, r=.001)
    box("Dobradica da tampa", "plastico", -w / 2 - .004, -d + .01, -.03, -w / 2, -.01, .03, r=.002)


def quadro():
    """Whiteboard 600 x 450 in an anodised frame with a marker tray (the written face is a Unity quad)."""
    w, h = .60, .45
    box("Placa do quadro", "melamina", -w / 2, -.012, -h / 2, w / 2, 0, h / 2, r=.002)
    t = .018
    for a in ((-w / 2 - t, -h / 2 - t, w / 2 + t, -h / 2), (-w / 2 - t, h / 2, w / 2 + t, h / 2 + t),
              (-w / 2 - t, -h / 2, -w / 2, h / 2), (w / 2, -h / 2, w / 2 + t, h / 2)):
        box("Moldura de aluminio", "aluminio", a[0], -.02, a[1], a[2], 0, a[3], r=.004)
    box("Calha do marcador", "aluminio", -.22, -.065, -h / 2 - t - .015, .22, -.02, -h / 2 - t, r=.003)
    cyl("Marcador", "plastico", (-.05, -.045, -h / 2 - t - .002), .009, .13, (1, 0, 0), seg=12, r=.002)
    cyl("Tampa do marcador", "baquelite", (.03, -.045, -h / 2 - t - .002), .0095, .04, (1, 0, 0), seg=12, r=.002)


def clamp(x, y, z, axis, r):
    # Two-hole pipe clamp screwed to the wall (wall plane = y on which the pipe stands off).
    cyl("Abracadeira", "galvanizado", (x, y, z), r + .004, .018, axis, seg=16, r=.001)


def instalacoes():
    """Module-local services (Blender frame of the cabinet; wall plane y = 2.33): insulated refrigerant lines from the
    wall sleeve to the cabinet top, controller conduit and junction box, floor drain in front of the cabinet."""
    yw = D - .002
    # Wall sleeve with escutcheon where the lines come in from the condensing unit outside.
    cyl("Bucha de passagem", "pvc", (0, yw - .015, 2.55), .06, .03, (0, 1, 0), seg=24, r=.003)
    cyl("Espelho da passagem", "plastico", (0, yw - .032, 2.55), .085, .006, (0, 1, 0), seg=32, r=.002)
    # Suction (larger, insulated) and liquid line (smaller, insulated): out of the sleeve, down to the top, then a
    # manifold splitting to the two evaporators through grommets in the top panel.
    for off, r in ((-.03, .021), (.03, .014)):
        y = D - .09
        tube("Linha frigorifica isolada", "isolante", [(off, yw - .03, 2.55), (off, y, 2.55), (off, y, H + .09)], r, fillet=.07, res=3)
        tube("Coletor sobre o gabinete", "isolante", [(COLS[0], y + off * 1.6, H + .09), (COLS[1], y + off * 1.6, H + .09)], r, res=3)
        for cx in COLS:
            tube("Descida ao evaporador", "isolante", [(cx, y + off * 1.6, H + .09), (cx, y + off * 1.6, H + .012)], r, res=3)
            cyl("Passa-tubo de borracha", "borracha", (cx, y + off * 1.6, H + .004), r + .01, .008, seg=20, r=.002)
        for z in (2.15, 1.85):
            clamp(off, y, z, (0, 0, 1), r)
    # Controller conduit: up from the controller, along the wall above the cabinet, down into the junction box.
    xc, zc = -2.945, 1.45
    pts = [(xc, yw - .03, zc + .075), (xc, yw - .03, 2.62), (-.75, yw - .03, 2.62), (-.75, yw - .03, H + .14),
           (-.75, D - .20, H + .14), (-.75, D - .20, H + .085)]
    tube("Eletroduto do controlador", "pvc", pts, .0105, fillet=.06, res=2)
    for x in (-2.70, -2.10, -1.50, -.95):
        clamp(x, yw - .03, 2.62, (1, 0, 0), .0105)
    for z in (1.75, 2.30):
        clamp(xc, yw - .03, z, (0, 0, 1), .0105)
    box("Caixa de passagem", "pvc", -.84, D - .27, H, -.66, D - .13, H + .085, r=.006)
    cyl("Tampa da caixa", "plastico", (-.75, D - .20, H + .087), .05, .004, seg=4, r=.002)
    # Sensor lead from the junction box into the cabinet top.
    tube("Cabo do sensor", "baquelite", [(-.70, D - .25, H + .02), (-.62, D - .30, H + .006), (-.58, D - .36, H + .006)], .004, fillet=.03, res=1)
    # Floor drain in front of the cabinet (wash-down), stainless grate flush with the finished floor.
    box("Caixilho do ralo", "inox", -.09, -.54, .0032, .09, -.36, .0048, r=.001)
    for k in range(7):
        box("Barra da grelha", "inox_gasto", -.075, -.53 + k * .024, .0045, .075, -.52 + k * .024, .0058, r=0)

    xt = -1.7775
    for off, r, z in ((-.03, .021, 2.60), (.03, .014, 2.66)):
        y = D - .09 + off * 1.6
        tube("Te isolado da torre", "isolante", [(off, D - .09, 2.55), (off, y, z),
             (xt, y, z), (xt, y, 2.452)], r, fillet=.025, res=3)
        cyl("Manga do te", "isolante", (off, D - .09, 2.55), r + .005, .06, seg=16)
        cyl("Passa-tubo da torre", "borracha", (xt, y, 2.444), r + .01, .008, seg=20, r=.002)
        for x in (-.4, -1.0, -1.6):
            clamp(x, y, z, (1, 0, 0), r)
    box("Caixilho do ralo torre", "inox", xt - .09, -.54, .0032, xt + .09, -.36, .0048, r=.001)
    for k in range(7):
        box("Barra da grelha torre", "inox_gasto", xt - .075, -.53 + k * .024, .0045, xt + .075, -.52 + k * .024, .0058, r=0)
    box("Tampa-junta frontal", "inox", -1.195, -.006, .10, -1.165, 0, 1.69, r=.001)
    for xa, xb, z in ((-2.36, -1.195, 2.44), (-1.165, 1.165, H)):
        box("Cantoneira topo horizontal", "inox", xa, D - .03, z, xb, D, z + .002, r=.0005)
        box("Cantoneira topo vertical", "inox", xa, D - .002, z, xb, D, z + .03, r=.0005)
        tube("Silicone topo", "plastico", [(xa, D - .003, z + .003), (xb, D - .003, z + .003)], .003, res=2)
    box("Cantoneira lateral parede", "inox", -2.362, D - .03, .10, -2.36, D, 2.44, r=.0005)
    box("Cantoneira lateral aba", "inox", -2.36, D - .002, .10, -2.33, D, 2.44, r=.0005)
    tube("Silicone lateral", "plastico", [(-2.357, D - .003, .10), (-2.357, D - .003, 2.44)], .003, res=2)
    for x in (-1.095, 0, 1.095, xt - .5125, xt + .5125):
        # Bracket on the outside of the leg footprint, with separate floor and upright bolts.
        bx = x + (.046 if x >= 0 else -.046)
        box("Sapata fixacao piso", "inox", bx - .014, .082, .001, bx + .014, .158, .004, r=.001)
        box("Aba vertical sapata", "inox", bx - .014, .152, .004, bx + .014, .155, .065, r=.001)
        for y in (.097, .137):
            cyl("Parafuso de ancoragem", "cromado", (bx, y, .007), .005, .006, seg=6, r=.0005)


def condensadora():
    """Remote condensing unit outside the east wall on two wall brackets (split system: the compressor and its
    fan stay outdoors). Origin on the outer wall face, centre of the cabinet back; front grille towards -Y."""
    w, h, d = .86, .56, .34
    box("Gabinete da condensadora", "pintura", -w / 2, -d - .06, -h / 2, w / 2, -.06, h / 2, r=.012)
    cyl("Grade do ventilador", "escuro", (-.13, -d - .062, 0), .21, .006, (0, 1, 0), seg=40, r=.001)
    for k in range(9):
        cyl("Anel da grade", "galvanizado", (-.13, -d - .066, 0), .03 + k * .022, .003, (0, 1, 0), seg=40, r=0)
    box("Painel de servico", "pintura", .13, -d - .063, -.20, .40, -d - .06, .20, r=.004)
    for z in (-.12, .12):
        box("Veneziana lateral", "escuro", w / 2 - .001, -d + .04, z - .06, w / 2 + .002, -.10, z + .06, r=.002)
    for x in (-.32, .32):
        box("Mao-francesa", "galvanizado", x - .02, -d - .08, -h / 2 - .05, x + .02, 0, -h / 2, r=.003)
        tube("Escora da mao-francesa", "galvanizado", [(x, -.01, -h / 2 - .40), (x, -d - .02, -h / 2 - .045)], .012, res=1)
        box("Coxim de borracha", "borracha", x - .04, -d - .05, -h / 2, x + .04, -d + .02, -h / 2 + .012, r=.003)
    # Lines to the wall sleeve (it is 0.60 m above the unit centre on the inside) and the disconnect switch box.
    for off, r in ((-.03, .021), (.03, .014)):
        tube("Linha frigorifica externa", "isolante", [(w / 2 - .05, -d / 2, off * 2), (w / 2 + .06, -d / 2, off * 2),
             (w / 2 + .06, -.04, .60 + off), (0, -.04, .60 + off), (0, .0, .60 + off)], r, fillet=.06, res=3)
    box("Chave seccionadora", "plastico", -w / 2 - .22, -.10, .02, -w / 2 - .08, 0, .20, r=.006)
    tube("Eletroduto externo", "pvc", [(-w / 2 - .15, -.04, .02), (-w / 2 - .15, -.04, -.10), (-w / 2 + .02, -.04, -.10)], .0105, fillet=.04, res=2)

# ---------------------------------------------------------------- additional modules and service equipment
TOWER_W, TOWER_H, TOWER_X = 1.165, 2.44, -1.7775
TOWER_DOOR_Z = (.235, .985, 1.735)
TOWER_RAIL_Z = (.300, 1.050, 1.800)


def torre():
    cabinet(TOWER_W, TOWER_H, (0.0,), TOWER_DOOR_Z, TOWER_RAIL_Z, ((.855, .885), (1.605, 1.635)))


def beam(name, mat, a, b, width=.035, depth=None):
    a, b = Vector(a), Vector(b)
    ob = box(name, mat, -width / 2, -(depth or width) / 2, 0,
             width / 2, (depth or width) / 2, (b - a).length, r=.0015)
    ob.data.transform(Matrix.Translation(a) @ Vector((0, 0, 1)).rotation_difference(b - a).to_matrix().to_4x4())
    return ob


def lift_geometry(h):
    # Two equal scissor stages: h is the roller crown, not the height of the table frame.
    # The .312 m low-rail docking pose extends the nominal .40 m lower working limit.
    rise = (h - .267) / 2
    length = .91
    if not .312 <= h <= 1.85 or not 0 < rise < length:
        raise ValueError("Lift height outside the mechanical envelope: " + str(h))
    span = math.sqrt(length * length - rise * rise)
    return rise, span, math.degrees(math.asin(rise / length))


def lift_caster(x, y, brake=False):
    r = .0625
    cyl("Pneu rodizio 125", "borracha", (x, y, r), r, .036, (1, 0, 0), seg=24, r=.005)
    for sx in (-1, 1):
        cyl("Cubo rodizio", "nylon", (x + sx * .020, y, r), .032, .005, (1, 0, 0), seg=16)
        beam("Garfo rodizio", "inox", (x + sx * .026, y, r), (x + sx * .026, y + .018, .142), .014, .009)
    box("Ponte rodizio", "inox", x - .031, y - .018, .135, x + .031, y + .036, .146, r=.002)
    cyl("Giro rodizio", "escuro", (x, y + .018, .156), .025, .020, seg=16)
    if brake:
        box("Pedal freio", "borracha", x - .023, y - .068, .106, x + .023, y - .017, .118, r=.003)


def carro_elevador(h=1.062):
    rise, span, angle = lift_geometry(h)
    for x in (-.31, .31):
        for y in (-.80, .80):
            lift_caster(x, y, brake=y < 0)
        box("Longarina inferior", "inox", x - .024, -.94, .175, x + .024, .94, .214, r=.004)
        # C-channel: the upper lip clears the tray brackets, while the side rollers run behind it.
        sx = -1 if x < 0 else 1
        lo, hi = sorted((sx * .343, sx * .346))
        box("Guia superior alma", "inox", lo, -.965, h - .056, hi, .965, h + .018, r=.001)
        lo, hi = sorted((sx * .315, sx * .346))
        box("Guia superior aba alta", "inox_gasto", lo, -.965, h + .010, hi, .965, h + .018, r=.001)
        lo, hi = sorted((sx * .290, sx * .346))
        box("Guia superior aba baixa", "inox", lo, -.965, h - .056, hi, .965, h - .052, r=.001)
    for y in (-.84, .84):
        beam("Travessa chassi", "inox", (-.31, y, .195), (.31, y, .195), .036)
    # Paired scissors at both sides; the flat bars occupy separate X planes at each crossing.
    for sx in (-1, 1):
        for stage_index in range(2):
            z = .205 + stage_index * rise
            for sign in (-1, 1):
                x = sx * (.235 + (.018 if sign > 0 else 0))
                a = (x, -sign * span / 2, z)
                b = (x, sign * span / 2, z + rise)
                # Beam's narrow dimension lies in X: a flat bar, not a round tube.
                beam("Barra tesoura dupla", "inox", a, b, .010, .020)
            for y, zz in ((-span / 2, z), (span / 2, z), (0, z + rise / 2),
                          (-span / 2, z + rise), (span / 2, z + rise)):
                cyl("Pino tesoura", "cromado", (sx * .247, y, zz), .012, .062, (1, 0, 0), seg=12, r=.001)
                cyl("Arruela tesoura", "escuro", (sx * .282, y, zz), .017, .003, (1, 0, 0), seg=12, r=.0005)
    for z, y in ((.205, -span / 2), (.205 + rise, span / 2), (.205 + 2 * rise, -span / 2)):
        cyl("Travessa tesoura", "inox", (0, y, z), .014, .48, (1, 0, 0), seg=12)
    for y in (-span / 2, span / 2):
        for sx in (-1, 1):
            cyl("Patim guia", "nylon", (sx * .27, y, .205), .016, .025, (1, 0, 0), seg=12)
            cyl("Patim guia superior", "nylon", (sx * .27, y, h - .062), .016, .025, (1, 0, 0), seg=12)
    a, b = Vector((0, -.68, .20)), Vector((0, span * .25, .205 + rise * 1.55))
    dv = b - a
    cyl("Corpo hidraulico", "escuro", a + dv * .26, .028, dv.length * .52, dv, seg=20, r=.002)
    cyl("Haste hidraulica", "cromado", a + dv * .74, .014, dv.length * .48, dv, seg=16, r=.001)
    box("Bomba hidraulica", "escuro", -.10, -.76, .20, .10, -.58, .267, r=.005)
    tube("Pedal bomba", "inox", [(0, -.70, .23), (0, -.88, .17), (.20, -.88, .17)], .010, fillet=.025, res=2)
    box("Apoio pedal", "borracha", .13, -.92, .165, .25, -.84, .18, r=.004)
    tube("Mangueira hidraulica", "borracha", [(-.09, -.64, .23), (-.13, -.47, .22), (-.04, -.30, .205 + rise * .5)], .006, res=2)
    for y in (-.90, .90):
        beam("Travessa mesa", "inox", (-.31, y, h - .050), (.31, y, h - .050), .024)
    for i in range(7):
        y = -.84 + i * .28
        cyl("Rolete transversal", "nylon", (0, y, h - .022), .022, .59, (1, 0, 0), seg=20, r=.002)
        cyl("Eixo rolete", "cromado", (0, y, h - .022), .006, .64, (1, 0, 0), seg=12, r=.0005)
    # Fold-down rear stop leaves a central opening for the long tray handle.
    for x in (-.25, .25):
        cyl("Eixo batente rebatível", "cromado", (x, -.965, h - .015), .009, .07, (1, 0, 0), seg=12)
        box("Batente rebatível", "inox", x - .035, -.977, h - .01, x + .035, -.961, h + .045, r=.002)
        box("Encosto borracha", "borracha", x - .025, .965, h - .035, x + .025, .99, h + .012, r=.004)
    tube("Alca empurrar", "inox", [(-.28, -.91, h - .04), (-.28, -1.01, h + .19),
         (.28, -1.01, h + .19), (.28, -.91, h - .04)], .014, fillet=.04, res=2)
    tube("Empunhadura alca", "borracha", [(-.20, -1.01, h + .19), (.20, -1.01, h + .19)], .015, res=2)


def suporte_bandejas():
    # Wall plane y=0, floor z=0. Trays retain all details of maca(), without a second approximation.
    for x in (-.88, .88):
        for y in (-.31, -.05):
            cyl("Sapata niveladora suporte", "polietileno", (x, y, .014), .022, .028, seg=16)
        tube("Pe lateral suporte", "inox", [(x, -.31, .04), (x, -.31, .12), (x, -.05, .12), (x, -.05, 1.035)], .015, res=2)
        box("Fixacao parede suporte", "inox", x - .035, -.005, .94, x + .035, 0, 1.01, r=.002)
        for z in (.955, .995):
            cyl("Parafuso parede", "cromado", (x, -.008, z), .005, .006, (0, 1, 0), seg=6, r=.0005)
    for z, y in ((.12, -.32), (.98, -.05)):
        tube("Travessa suporte", "inox", [(-.985, y, z), (.985, y, z)], .015, res=2)
    box("Calha escorrimento", "inox", -.99, -.36, .065, .99, -.015, .069, r=.001)
    for y in (-.36, -.015):
        box("Borda calha", "inox", -.99, y, .069, .99, y + .003, .09, r=.001)
    for y in (-.30, -.175, -.05):
        for x in (-.72, .72):
            box("Berco bandeja", "inox_gasto", x - .05, y - .027, .115, x + .05, y + .027, .135, r=.003)
            box("Apoio nylon berco", "nylon", x - .045, y - .023, .135, x + .045, y + .023, .15, r=.004)
            box("Retentor berco", "borracha", x - .04, y - .032, .145, x + .04, y - .025, .18, r=.002)
    # X->Z, Y->X, Z->Y is a proper rotation; tilt the upper long edge towards the wall by eight degrees.
    orient = Matrix(((0, 1, 0, 0), (0, 0, 1, 0), (1, 0, 0, 0), (0, 0, 0, 1)))
    for y in (-.30, -.175):
        before = set(R._coll(R._current["asset"]).objects)
        maca()
        xf = Matrix.Translation((0, y, .47)) @ Matrix.Rotation(math.radians(-8), 4, "X") @ orient @ Matrix.Translation((0, -(TRAY_Y0 + TRAY_LEN / 2), 0))
        for ob in set(R._coll(R._current["asset"]).objects) - before:
            ob.data.transform(xf)


def posto_conferencia():
    # z=0 is the writing surface. Unity places this origin 1.05 m above the floor.
    box("Prateleira escrita", "inox", -.30, -.30, -.006, .30, 0, 0, r=.002)
    for x in (-.30, .296):
        box("Borda dobrada lateral", "inox", x, -.30, -.025, x + .004, 0, -.006, r=.001)
    box("Borda dobrada frente", "inox", -.30, -.30, -.025, .30, -.296, -.006, r=.001)
    for x in (-.22, .22):
        box("Mao francesa parede", "inox", x - .014, -.006, -.22, x + .014, 0, -.008, r=.001)
        beam("Escora prateleira", "inox", (x, -.012, -.21), (x, -.26, -.013), .018)
        for z in (-.18, -.04):
            cyl("Parafuso prateleira", "cromado", (x, -.009, z), .005, .004, (0, 1, 0), seg=6, r=.0005)
    # Landscape orientation keeps the 330 mm clipboard on the 300 mm deep shelf.
    before = set(R._coll(R._current["asset"]).objects)
    box("Prancheta", "plastico", -.1175, -.165, .001, .1175, .165, .008, r=.005)
    box("Clipe prancheta", "cromado", -.04, .137, .009, .04, .160, .018, r=.003)
    cyl("Mola clipe", "escuro", (0, .146, .019), .006, .060, (1, 0, 0), seg=12, r=.001)
    xf = Matrix.Translation((-.085, -.15, 0)) @ Matrix.Rotation(math.radians(87), 4, "Z")
    for ob in set(R._coll(R._current["asset"]).objects) - before:
        ob.data.transform(xf)
    box("Caixa etiquetas", "plastico", .09, -.12, .001, .29, 0, .101, r=.006)
    box("Abertura etiquetas", "escuro", .107, -.122, .017, .273, -.120, .030, r=.001)
    for i in range(3):
        box("Etiqueta branca", "plastico", .12, -.148 - .011 * i, .019 - .002 * i, .26, -.121, .021 - .002 * i, r=.0005)
    cyl("Porta caneta", "plastico", (.255, -.235, .028), .022, .056, seg=20, r=.003)
    cyl("Cavidade porta caneta", "escuro", (.255, -.235, .057), .016, .001, seg=20, r=0)
    tube("Caneta", "baquelite", [(.255, -.235, .025), (.245, -.245, .15)], .005, res=2)
    pts = [(.251, -.239, .080)]
    for i in range(121):
        t = i / 120
        pts.append((.255 + .007 * math.cos(t * 20 * math.pi), -.235 - .075 * t, .042 + .007 * math.sin(t * 20 * math.pi)))
    pts.append((.262, -.282, .005))
    tube("Cordao espiral caneta", "baquelite", pts, .0015, res=1)
    box("Ancoragem cordao", "inox", .254, -.290, .001, .270, -.274, .008, r=.002)


def check_geometry():
    checks = []
    for rail in TOWER_RAIL_Z:
        h = rail + .012
        rise, span, angle = lift_geometry(h)
        R._current["asset"] = "__lift_check"
        carro_elevador(h)
        obs = list(R._coll("__lift_check").objects)
        # Front bumpers at +.990: translate so their leading face is 20 mm from y=0.
        front = max(v.co.y for ob in obs for v in ob.data.vertices)
        center_y = -.020 - front
        assert abs(2 * rise + .267 - h) < 1e-8
        assert abs(span * span + rise * rise - .91 ** 2) < 1e-8
        assert 0 < angle < 90
        assert rise * math.cos(math.radians(angle)) > .020  # neighbouring stages clear the flat-bar width
        assert all(v.co.y + center_y <= -.020 + 1e-7 for ob in obs for v in ob.data.vertices)
        checks.append({"rollerTop": round(h, 3), "stageAngleDegrees": round(angle, 3),
                       "stageRise": round(rise, 4), "span": round(span, 4),
                       "carCenterY": round(center_y, 4), "cabinetGap": .020})
        for ob in obs:
            bpy.data.objects.remove(ob, do_unlink=True)
    os.makedirs(SRC + "/revisao", exist_ok=True)
    with open(SRC + "/revisao/geometria.json", "w") as f:
        json.dump(checks, f, indent=2)
    print("GEOMETRY", json.dumps(checks), flush=True)


BUILDERS = {"ModuloCamara": cabinet, "PortaCompartimentoD": lambda: door(1), "PortaCompartimentoE": lambda: door(-1),
            "TrilhoIntermediario": stage, "MacaCompartimento": maca, "ControladorCamara": controlador,
            "QuadroOcupacao": quadro, "InstalacoesCamara": instalacoes, "UnidadeCondensadora": condensadora, "ModuloTorre": torre,
            "CarroElevador": carro_elevador, "SuporteBandejas": suporte_bandejas, "PostoConferencia": posto_conferencia}
SIZES = {"ModuloCamara": 2048, "PortaCompartimentoD": 1024, "PortaCompartimentoE": 1024, "TrilhoIntermediario": 512,
         "MacaCompartimento": 1024, "ControladorCamara": 512, "QuadroOcupacao": 512, "InstalacoesCamara": 1024,
         "UnidadeCondensadora": 1024, "ModuloTorre": 2048, "CarroElevador": 1024,
         "SuporteBandejas": 1024, "PostoConferencia": 1024}


def pivots():
    """Unity local coordinates (x, y=up, z=into the cabinet) of every moving part, for the prefab builder."""
    comp = {}
    names = {(0, 1): "C-01", (1, 1): "C-02", (0, 0): "C-03", (1, 0): "C-04"}
    for (c, lvl), n in names.items():
        hs = hinge_side(c)
        pin = (COLS[c] + hs * (DOOR_W / 2 + PIN[0]), DOOR_Z[lvl], PIN[1])
        comp[n] = {"coluna": c, "nivel": lvl, "porta": "PortaCompartimentoD" if hs > 0 else "PortaCompartimentoE",
                   "pinoPorta": [round(v, 4) for v in pin],
                   # Unity degrees about +Y (left-handed): the right-hinged door turns negative to swing out.
                   "aberturaPortaGraus": -95.0 if hs > 0 else 95.0,
                   "referencia": [COLS[c], RAIL_Z[lvl], 0.0],
                   "cursoMaca": STAGE1, "cursoIntermediario": STAGE1 / 2, "cursoTotal": FULL,
                   "abertura": [round(v, 4) for v in opening(c, lvl)]}
    tower = {}
    for lvl, n in ((2, "C-05"), (1, "C-06"), (0, "C-07")):
        z0 = TOWER_DOOR_Z[lvl]
        tower[n] = {"coluna": 0, "nivel": lvl, "porta": "PortaCompartimentoE",
                    "pinoPorta": [-.412, z0, PIN[1]], "aberturaPortaGraus": 95.0,
                    "referencia": [0.0, TOWER_RAIL_Z[lvl], 0.0], "cursoMaca": STAGE1,
                    "cursoIntermediario": STAGE1 / 2, "cursoTotal": FULL,
                    "abertura": [round(v, 4) for v in (-.37, z0 + INSET * .67, .37, z0 + DOOR_H - INSET * .67)]}
    return {"dimensoes": [W, H, D], "compartimentos": comp,
            "modulos": {"ModuloTorre": {"dimensoes": [TOWER_W, TOWER_H, D], "compartimentos": tower}},
            "nota": "Unity local = (x, z, y) of Blender; module origin at the front face centre on the floor."}


def main():
    bpy.context.preferences.filepaths.save_version = 0
    os.makedirs(OUT + "/Texturas", exist_ok=True); os.makedirs(SRC + "/revisao", exist_ok=True)
    only = ONLY or list(BUILDERS)
    for o in list(bpy.data.objects):
        if not ONLY or any(o in list(R._coll(n).objects) for n in only) or o.name in only or o.name.removesuffix("_fonte") in only:
            bpy.data.objects.remove(o, do_unlink=True)
    stats = {}
    for i, (name, fn) in enumerate(BUILDERS.items()):
        if name not in only:
            continue
        R._current["asset"] = name; fn()
        ob = R.assemble(name, MATS)
        bm = bmesh.new(); bm.from_mesh(ob.data)
        bmesh.ops.dissolve_degenerate(bm, dist=.000001, edges=list(bm.edges))
        bmesh.ops.triangulate(bm, faces=list(bm.faces))
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(ob.data); bm.free()
        density = R.layout_uvs(ob, SIZES[name], margin_px=8)
        stats[name] = {"triangles": len(ob.data.polygons), "texelsPerMetre": density, "size": [round(v, 4) for v in ob.dimensions],
                       "degenerate": sum(f.area < 1e-12 for f in ob.data.polygons)}
        if os.environ.get("SEM_EXPORT") != "1":
            R.export_fbx(ob, OUT + "/" + name + ".fbx")
        ob.location.x = i * 3
    with open(SRC + "/revisao/pivos.json", "w") as f:
        json.dump(pivots(), f, indent=2)
    bpy.ops.wm.save_as_mainfile(filepath=SRC + "/CamaraFria.blend")
    previous = json.load(open(SRC + "/revisao/malhas.json")) if ONLY and os.path.exists(SRC + "/revisao/malhas.json") else {}
    previous.update(stats)
    with open(SRC + "/revisao/malhas.json", "w") as f:
        json.dump(previous, f, indent=2)
    print("STATS", json.dumps(stats), flush=True)
    if os.environ.get("SO_MALHAS") == "1":
        return
    for name in stats:
        ob = bpy.data.objects[name]; pos = ob.location.copy(); ob.location = (0, 0, 0)
        others = [o for o in bpy.context.scene.objects if o != ob and o.type == "MESH"]
        for o in others: o.hide_render = True
        B.bake.__globals__["SIZE"] = SIZES[name]
        imgs, times = B.bake(ob, [s.material for s in ob.material_slots], ao_samples=32)
        B.write_unity_maps(imgs, OUT + "/Texturas", prefix=name)
        stats[name]["bakeSeconds"] = round(sum(times.values()), 1)
        stats[name]["bakePasses"] = times
        ob.location = pos
        for o in others: o.hide_render = False
        print("BAKED", name, stats[name], times, flush=True)
    previous.update(stats)
    with open(SRC + "/revisao/malhas.json", "w") as f:
        json.dump(previous, f, indent=2)
    bpy.ops.wm.save_as_mainfile(filepath=SRC + "/CamaraFria.blend")


if __name__ == "__main__":
    if ONLY == ["--check"]:
        check_geometry()
    else:
        main()

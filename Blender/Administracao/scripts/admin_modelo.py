# Procedural models of the archive / office ("administracao"): civil-service desk with a hanging pedestal,
# steel filing cabinet, steel shelving with the coordination's boxes and binders, the belongings locker
# (columns A/B x levels 1-3), the work bench, the laser printer, and the evidence props (belongings bags,
# recorder, guard envelope, archive box). References and the story brief: ../referencias.md.
# Same conventions as Blender/Recepcao/scripts/recepcao_modelo.py, whose helpers are reused: metres, each asset
# in its own frame with the origin on the floor at the centre of its footprint, front towards Blender -Y
# (Unity -Z after export), Z up; parts tagged with a material key, one game mesh per asset.
import math, os, types
import bpy
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))


def _load(path, module):
    ns = {"__name__": module, "__file__": path}
    exec(compile(open(path, encoding="utf-8").read(), path, "exec"), ns)
    return types.SimpleNamespace(**ns)


R = _load(os.path.join(HERE, "..", "..", "Recepcao", "scripts", "recepcao_modelo.py"), "recepcao_modelo")
box, cyl, tube, extrude, hull, lathe, cable, pen, along = R.box, R.cyl, R.tube, R.extrude, R.hull, R.lathe, R.cable, R.pen, R.along

# ---------------------------------------------------------------- dimensions

# Desk in the reception counter's language (same laminate, 25 mm gables to the floor, recessed dark plinth, a
# three-drawer pedestal with the counter's drawers and pulls, a lined cable grommet) at desk size, 1.60 x 0.70 m.
# Frame as the counter's: the user sits at +Y facing -Y, so the reception desk kit (monitor, keyboard, mouse at the
# user's right = -X, phone, cables down the grommet to a thin client under the top) is reused with its own
# coordinates. Everything is modelled in the counter's coordinates and moved MESA["dy"] towards +Y at the end, which
# centres the 0.70 m depth on the origin (back edge -0.35, user edge +0.35). The kit and the grommet are first moved
# MESA["kx"] along X (the kit's cables are absolute), so the keyboard sits over the knee space beside the pedestal.
MESA = dict(w=1.60, back=-0.43, front=0.27, work_z=0.76, work_t=0.025, gable_t=0.025, plinth=0.09, pedestal_w=0.425,
            modesty_z0=0.30, dy=0.08, kx=-0.25,
            # pedestal: drawer fronts' plane (y = fronts), carcass back, rows bottom to top as the counter's
            fronts=0.25, carcass_back=-0.392, side=0.018, rail=0.0127, depth=0.46, travel=0.40,
            rows=((0.095, 0.30), (0.305, 0.51), (0.515, 0.727)))
# Four-drawer steel filing cabinet for hanging folders.
ARQUIVO = dict(w=0.47, d=0.62, h=1.33, plinth=0.05, gap=0.004, rows=4)
# Steel shelving unit (cantoneira) with six shelves.
ESTANTE = dict(w=0.92, d=0.40, h=2.00, post=0.035, shelves=(0.10, 0.44, 0.78, 1.12, 1.46, 1.80))
# Belongings locker: two columns (A, B) by three levels (1 top .. 3 bottom), louvred doors with hasps.
PERTENCES = dict(w=0.92, d=0.50, h=1.85, legs=0.10, frame=0.03, cols=2, rows=3)
BANCADA = dict(w=2.40, d=0.60, h=0.90, top_t=0.03)

ASSETS = ("MesaServidor", "GavetaMesa1", "GavetaMesa2", "GavetaMesa3", "EquipamentosMesa", "ArquivoAco", "GavetaArquivo",
          "EstanteAco", "EstanteCoordenacao", "ArmarioPertences", "PortaPertences", "Bancada", "Impressora", "PastasManuais",
          "BolsaPertences", "RemendoBolsa", "Gravador", "EnvelopeGuarda", "EnvelopeAba", "CaixaArquivo", "PastaContrato", "MesaApoio")
# Printed labels (folder spines, locker addresses, legend, bag tags) are quads with a print atlas, made in Unity.
UNBAKED = ()
# Picked up and turned over in the inspection: their underside is kept (and given full texel density).
INSPECTED = ("BolsaPertences", "Gravador", "EnvelopeGuarda", "EnvelopeAba")

# ---------------------------------------------------------------- desk


def _asset_objects():
    return list(R._coll(R._current["asset"]).objects)


def _move(objects, dx=0.0, dy=0.0):
    m = Matrix.Translation((dx, dy, 0.0))
    for ob in objects:
        ob.data.transform(m)


def _shift_desk():
    """Moves everything built so far for the current asset by MESA["dy"] (counter coordinates -> desk frame)."""
    _move(_asset_objects(), dy=MESA["dy"])


def build_mesa():
    P = MESA
    W, g, wz, wt = P["w"] / 2, P["gable_t"], P["work_z"], P["work_t"]
    yb, yf = P["back"], P["front"]
    inner = W - g
    under = wz - wt
    # Gables to the floor, eased edges, a dark leveller strip recessed under each.
    for sx in (-1, 1):
        x0, x1 = (-W, -inner) if sx < 0 else (inner, W)
        box(f"Lateral {sx:+d}", "laminado", x0, yb + 0.01, 0.012, x1, yf - 0.01, under, r=0.003)
        box(f"Sapata da lateral {sx:+d}", "rodape", x0 + 0.004, yb + 0.03, 0.0, x1 - 0.004, yf - 0.03, 0.012, r=0.0015)
    top = box("Tampo de trabalho", "laminado", -W, yb, under, W, yf, wz, r=0.005, seg=3)
    G = R.GROMMET
    R.cut(top, cyl("__furo", "laminado", (G["x"] + P["kx"], G["y"], wz - wt / 2), G["hole_r"], wt + 0.02, seg=40, r=0))
    before = set(_asset_objects())
    R.grommet(wz, wt)
    _move([ob for ob in _asset_objects() if ob not in before], dx=P["kx"])
    # Modesty panel between the gables, set back from the back edge, and a rail under the top's back edge.
    box("Painel frontal", "laminado", -inner, yb + 0.02, P["modesty_z0"], inner, yb + 0.038, under, r=0.002)
    box("Travessa", "laminado", -inner, yb + 0.038, under - 0.08, inner, yb + 0.056, under, r=0.0015)
    # Pedestal at +X (the user's left, as on the counter): open carcass with the fixed slide members.
    px, side, fr, cb = inner - P["pedestal_w"], P["side"], P["fronts"], P["carcass_back"]
    box("Lateral do gaveteiro", "laminado", px, cb, P["plinth"], px + side, fr, under, r=0.002)
    box("Fundo do gaveteiro", "laminado", px + side, cb, P["plinth"], inner, cb + 0.018, under, r=0.001)
    box("Base do gaveteiro", "laminado", px + side, cb + 0.018, P["plinth"], inner, fr, P["plinth"] + 0.018, r=0.001)
    box("Rodape do gaveteiro", "rodape", px, cb + 0.02, 0.0, inner, fr - 0.045, P["plinth"], r=0.002)
    for i in range(3):
        zb = mesa_drawer_box(i)[2]
        for nm, (x0, x1) in (("esquerda", (px + side, px + side + P["rail"])), ("direita", (inner - P["rail"], inner))):
            box(f"Corredica fixa {i + 1} {nm}", "aco", x0, fr - 0.455, zb + 0.008, x1, fr - 0.004, zb + 0.048, r=0.0008, seg=1)
    _shift_desk()


def mesa_drawer_box(i):
    """Outer limits of drawer i's box (x0, x1, z bottom, z top), counter coordinates (0 = bottom)."""
    P = MESA
    inner = P["w"] / 2 - P["gable_t"]
    px = inner - P["pedestal_w"]
    z0, z1 = P["rows"][i]
    return px + P["side"] + P["rail"] + 0.001, inner - P["rail"] - 0.001, z0 + 0.025, z1 - 0.035


def build_gaveta_mesa(i):
    """Drawer i (0 = bottom) of the desk pedestal, closed, in the desk frame: the counter's drawer (laminate front,
    tube pull, white melamine box, moving slide members) and what it holds. In Unity it slides MESA["travel"]
    towards the user (+Y here, +Z in Unity)."""
    P = MESA
    inner = P["w"] / 2 - P["gable_t"]
    px = inner - P["pedestal_w"]
    z0, z1 = P["rows"][i]
    F, t = P["fronts"], 0.015
    box(f"Frente de gaveta {i + 1}", "laminado", px + 0.004, F, z0, inner - 0.004, F + 0.018, z1, r=0.0015)
    hx, hz, cc = (px + inner) / 2, z1 - 0.045, 0.128
    tube(f"Puxador de gaveta {i + 1}", "aluminio",
         [(hx - cc / 2, F + 0.017, hz), (hx - cc / 2, F + 0.045, hz), (hx + cc / 2, F + 0.045, hz),
          (hx + cc / 2, F + 0.017, hz)], 0.0055, fillet=0.01)
    xl, xr, zb, zt = mesa_drawer_box(i)
    yb, yf = F - P["depth"], F - 0.001
    box("Lateral esquerda", "melamina", xl, yb, zb, xl + t, yf, zt, r=0.0015)
    box("Lateral direita", "melamina", xr - t, yb, zb, xr, yf, zt, r=0.0015)
    box("Contrafrente", "melamina", xl + t, yf - t, zb, xr - t, yf, zt, r=0.001)
    box("Traseira", "melamina", xl + t, yb, zb + 0.016, xr - t, yb + t, zt, r=0.001)
    box("Fundo", "melamina", xl + 0.008, yb + 0.008, zb + 0.010, xr - 0.008, yf - 0.008, zb + 0.016, r=0)
    for nm, (x0, x1) in (("esquerda", (xl - P["rail"] + 0.0015, xl)), ("direita", (xr, xr + P["rail"] - 0.0015))):
        box(f"Corredica movel {nm}", "aco", x0, yb + 0.01, zb + 0.014, x1, yf - 0.01, zb + 0.042, r=0.0006, seg=1)
    z = zb + 0.016
    if i == 2:   # top: sticky notes, stapler, clips, pens
        box("Bloco de recados", "post_it", xl + 0.03, yb + 0.06, z, xl + 0.106, yb + 0.136, z + 0.018, r=0.001)
        box("Grampeador - base", "plastico", xl + 0.15, yb + 0.05, z, xl + 0.19, yb + 0.22, z + 0.016, r=0.004)
        box("Grampeador - braco", "aco", xl + 0.152, yb + 0.05, z + 0.016, xl + 0.188, yb + 0.21, z + 0.034, r=0.004)
        box("Caixa de clipes", "plastico", xr - 0.09, yb + 0.27, z, xr - 0.03, yb + 0.35, z + 0.025, r=0.003)
        for k, cap in enumerate(("caneta_azul", "caneta_preta", "caneta_vermelha")):
            x = xr - 0.12 + 0.02 * k
            pen(f"Caneta {k + 1}", along((x, yf - 0.20, z + 0.0049), (x + 0.012, yf - 0.06, z + 0.0049)), "caneta_corpo", cap)
    elif i == 1:   # middle: kraft envelopes and forms
        box("Formularios", "papel", xl + 0.02, yb + 0.05, z, xl + 0.23, yb + 0.347, z + 0.03, r=0.001)
        box("Envelope", "kraft", xl + 0.03, yb + 0.06, z + 0.03, xl + 0.26, yb + 0.38, z + 0.034, r=0.001)
    else:        # bottom: a ream and a grey folder
        box("Resma", "papel", xl + 0.02, yb + 0.03, z, xl + 0.23, yb + 0.327, z + 0.05, r=0.002)
        box("Pasta cinza", "pasta_cinza", xl + 0.01, yb + 0.02, z + 0.05, xl + 0.245, yb + 0.33, z + 0.062, r=0.002)
    _shift_desk()


# ---------------------------------------------------------------- desk equipment (desk frame, user at -Y)


def build_equipamentos():
    """Nina's desk kit, in the desk frame: the reception's monitor, keyboard, mouse, phone and cable routing at their
    own (counter) coordinates moved MESA["kx"], then a letter tray, pen cup, mug and a small cactus; all moved with
    _shift_desk."""
    K = R.KIT
    wz = MESA["work_z"]
    mx, my = K["monitor_x"], K["monitor_y"]
    box("Base do monitor", "plastico", mx - 0.11, my - 0.08, wz, mx + 0.11, my + 0.07, wz + 0.012, r=0.004)
    box("Haste do monitor", "plastico", mx - 0.025, my - 0.04, wz + 0.01, mx + 0.025, my - 0.015, K["screen_z"], r=0.006)
    sw, sh, st = K["screen_w"] / 2, K["screen_h"] / 2, K["screen_t"]
    scr = box("Monitor", "plastico", mx - sw, -st / 2, -sh, mx + sw, st / 2, sh, r=0.006)
    R.place(scr, -K["screen_tilt"], offset=(0, my + 0.005, K["screen_z"]))
    tela = box("Tela", "tela", mx - sw + 0.012, st / 2 - 0.002, -sh + 0.025, mx + sw - 0.012, st / 2 + 0.0008, sh - 0.012, r=0)
    R.place(tela, -K["screen_tilt"], offset=(0, my + 0.005, K["screen_z"]))
    R.keyboard(mx, -0.02, wz)
    box("Mousepad", "tecido", -0.33, -0.10, wz, -0.10, 0.08, wz + 0.003, r=0.0015)
    mouse_out = R.office_mouse(-0.215, -0.005, wz + 0.003)
    m_phone = Matrix.Translation((0.53, -0.13, wz)) @ Matrix.Rotation(math.radians(-14), 4, "Z")
    R.phone(m_phone)
    R.desk_cables(K, dict(work_t=MESA["work_t"]), wz, mouse_out, m_phone)
    _move(_asset_objects(), dx=MESA["kx"])
    # Two-tier smoke letter tray over the pedestal, papers in both tiers.
    tx, ty = 0.60, -0.20
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
    R.pen_holder(-0.72, -0.24, wz)
    cx, cy = 0.355, -0.37   # mug behind the phone, cactus in the back corner beyond the monitor
    lathe("Caneca", "caneca", [(0.041, 0.0), (0.043, 0.095), (0.039, 0.095), (0.037, 0.006), (0.0, 0.006), (0.0, 0.0)],
          seg=32, r=0.0012, matrix=Matrix.Translation((cx, cy, wz)))
    tube("Alca da caneca", "caneca", [(cx + 0.041, cy, wz + 0.075), (cx + 0.065, cy, wz + 0.07), (cx + 0.065, cy, wz + 0.03),
                                      (cx + 0.04, cy, wz + 0.025)], 0.0055, fillet=0.012)
    vx, vy = -0.56, -0.35
    lathe("Vaso do cacto", "vaso", [(0.042, 0.0), (0.05, 0.085), (0.046, 0.085), (0.039, 0.006), (0.0, 0.006), (0.0, 0.0)],
          seg=28, r=0.001, matrix=Matrix.Translation((vx, vy, wz)))
    cyl("Terra", "terra", (vx, vy, wz + 0.075), 0.046, 0.004, seg=28, r=0)
    lathe("Cacto", "cacto", [(0.0, 0.0), (0.026, 0.01), (0.03, 0.05), (0.024, 0.09), (0.012, 0.11), (0.0, 0.114)],
          seg=10, r=0.0, matrix=Matrix.Translation((vx, vy, wz + 0.075)))
    _shift_desk()


# ---------------------------------------------------------------- filing cabinet


def build_arquivo():
    P = ARQUIVO
    W, D, H, pl, g = P["w"] / 2, P["d"] / 2, P["h"], P["plinth"], P["gap"]
    s = 0.0012 * 6   # visible sheet + flange thickness, exaggerated a little to read at distance
    box("Tampo", "chapa", -W, -D + 0.005, H - 0.02, W, D, H, r=0.004, seg=3)
    for sx in (-1, 1):
        box(f"Lateral {sx:+d}", "chapa", sx * W - (sx > 0) * s, -D + 0.005, pl, sx * W + (sx < 0) * s, D, H - 0.02, r=0.002)
    box("Fundo", "chapa", -W + s, D - s, pl, W - s, D, H - 0.02, r=0)
    box("Rodape", "chapa_escura", -W + 0.01, -D + 0.02, 0.0, W - 0.01, D - 0.02, pl, r=0.003)
    rows = P["rows"]
    span = (H - 0.02 - pl) / rows
    for k in range(rows + 1):   # rails between drawer openings
        z = pl + k * span
        box(f"Travessa {k}", "chapa", -W + s, -D + 0.005, z - 0.008, W - s, -D + 0.04, z + 0.008, r=0.0015)
    cyl("Cilindro da fechadura", "aluminio", (W - 0.05, -D + 0.004, H - 0.045), 0.011, 0.012, axis=(0, 1, 0), seg=20, r=0.002)
    box("Fenda da fechadura", "fenda", W - 0.052, -D - 0.003, H - 0.052, W - 0.048, -D - 0.001, H - 0.038, r=0)


def arquivo_drawer_span():
    P = ARQUIVO
    return (P["h"] - 0.02 - P["plinth"]) / P["rows"]


def build_gaveta_arquivo():
    """One drawer, closed, in its own frame: origin at the bottom-centre of its front's back face, front towards
    -Y. The generator stacks four per cabinet (z = plinth + k * span) and slides them 0.50 m out."""
    P = ARQUIVO
    W, D = P["w"] / 2, P["d"] / 2
    span = arquivo_drawer_span()
    h = span - 0.012
    box("Frente", "chapa", -W + 0.004, -0.02, 0.006, W - 0.004, 0.0, 0.006 + h, r=0.003, seg=3)
    # Shell pull with the label holder above it.
    zc = 0.006 + h * 0.62
    hull("Puxador concha", "aluminio", [(sx * 0.07, -0.02, zc + dz) for sx in (-1, 1) for dz in (-0.012, 0.012)] +
         [(sx * 0.06, -0.04, zc + dz) for sx in (-1, 1) for dz in (-0.006, 0.01)], r=0.004)
    box("Porta-etiqueta", "aluminio", -0.045, -0.023, zc + 0.03, 0.045, -0.02, zc + 0.062, r=0.001, seg=1)
    box("Etiqueta em branco", "papel", -0.041, -0.0235, zc + 0.033, 0.041, -0.022, zc + 0.059, r=0)
    # Body and the hanging-folder rails, a few folders.
    depth = P["d"] - 0.08
    zb, zt = 0.02, h - 0.02
    box("Lateral esquerda", "chapa_interna", -W + 0.025, 0.0, zb, -W + 0.03, depth, zt, r=0)
    box("Lateral direita", "chapa_interna", W - 0.03, 0.0, zb, W - 0.025, depth, zt, r=0)
    box("Fundo", "chapa_interna", -W + 0.025, 0.0, zb, W - 0.025, depth, zb + 0.004, r=0)
    box("Traseira", "chapa_interna", -W + 0.025, depth - 0.005, zb, W - 0.025, depth, zt, r=0)
    for sx in (-1, 1):
        box(f"Trilho de pasta {sx:+d}", "aco", sx * (W - 0.045) - 0.004, 0.01, zt - 0.006, sx * (W - 0.045) + 0.004, depth - 0.01, zt, r=0.0005, seg=1)
    colours = ("pasta_verde", "pasta_verde", "kraft", "pasta_verde", "pasta_azul", "pasta_verde", "kraft", "pasta_verde")
    for k, mat in enumerate(colours):
        y = 0.04 + k * 0.058
        box(f"Pasta suspensa {k}", mat, -W + 0.05, y, zb + 0.03, W - 0.05, y + 0.004 + 0.002 * (k % 3), zt - 0.01, r=0.0008)
        box(f"Haste da pasta {k}", "aco", -W + 0.04, y - 0.001, zt - 0.012, W - 0.04, y + 0.005, zt - 0.006, r=0.0006, seg=1)
        tx = -W + 0.08 + (k % 4) * 0.08
        box(f"Visor da pasta {k}", "visor_pasta", tx, y - 0.001, zt - 0.006, tx + 0.055, y + 0.003, zt + 0.016, r=0.0008)


# ---------------------------------------------------------------- shelving with the coordination's archive


def _box_arquivo(name, x0, y0, z0, w=0.135, d=0.35, h=0.25, mat="kraft", lid=True):
    """Corrugated archive box standing on its long side, spine (with the hand hole) towards -Y."""
    box(name, mat, x0, y0, z0, x0 + w, y0 + d, z0 + h, r=0.003)
    cyl(name + " - pega", "fenda", (x0 + w / 2, y0 - 0.0005, z0 + h * 0.78), 0.012, 0.002, axis=(0, 1, 0), seg=12, r=0)
    if lid:
        box(name + " - aba", mat, x0 - 0.001, y0 - 0.002, z0 + h - 0.03, x0 + w + 0.001, y0 + 0.01, z0 + h, r=0.002)


def _az(name, x0, y0, z0, mat, w=0.075, d=0.285, h=0.32):
    """Lever-arch binder (pasta AZ) standing, spine towards -Y with its finger hole and label window."""
    box(name, mat, x0, y0, z0, x0 + w, y0 + d, z0 + h, r=0.004)
    cyl(name + " - furo", "fenda", (x0 + w / 2, y0 - 0.0005, z0 + 0.06), 0.012, 0.002, axis=(0, 1, 0), seg=14, r=0)
    box(name + " - visor", "visor_pasta", x0 + 0.01, y0 - 0.0015, z0 + 0.12, x0 + w - 0.01, y0 + 0.002, z0 + 0.28, r=0.001)


def _estante_frame():
    P = ESTANTE
    W, D, H, a = P["w"] / 2, P["d"] / 2, P["h"], P["post"]
    # Slotted angle posts and shelves with folded edges.
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (W - a / 2), sy * (D - a / 2)
            box(f"Coluna {sx:+d}{sy:+d} a", "estante", x - a / 2, y - 0.0015, 0.0, x + a / 2, y + 0.0015, H, r=0.0006, seg=1)
            box(f"Coluna {sx:+d}{sy:+d} b", "estante", x - 0.0015, y - a / 2, 0.0, x + 0.0015, y + a / 2, H, r=0.0006, seg=1)
            for k in range(int(H / 0.05)):
                z = 0.03 + k * 0.05
                box(f"Furo {sx}{sy}{k}", "fenda", x - 0.004, y - sy * 0.0004 - 0.0018, z, x + 0.004, y - sy * 0.0004 + 0.0018, z + 0.016, r=0)
            cyl(f"Sapata {sx}{sy}", "borracha", (x, y, 0.004), 0.014, 0.008, seg=10, r=0.001)
    for k, z in enumerate(P["shelves"]):
        box(f"Prateleira {k}", "estante", -W + 0.003, -D + 0.003, z - 0.001, W - 0.003, D - 0.003, z, r=0.0005, seg=1)
        for sy in (-1, 1):
            box(f"Aba {k} {sy}", "estante", -W + 0.003, sy * (D - 0.0045) - 0.0015, z - 0.03, W - 0.003, sy * (D - 0.0045) + 0.0015, z, r=0.0005, seg=1)
        for sx in (-1, 1):
            for sy in (-1, 1):
                cyl(f"Parafuso {k}{sx}{sy}", "aco", (sx * (W - 0.012), sy * (D - 0.0005), z - 0.015), 0.005, 0.004, axis=(0, 1, 0), seg=8, r=0.001)
    return W, -D + 0.03, P["shelves"]


def build_estante():
    """Old records shelving (south unit): archive boxes by year, 2019 at the top to 2022 low, reams on the floor shelf.
    Contents kept to the front 0.36 m; box labels are print quads placed in Unity."""
    W, y0, z = _estante_frame()
    years = {5: "kraft_velho", 4: "kraft_velho", 3: "kraft", 2: "kraft", 1: "kraft"}
    for shelf, mat in years.items():
        x = -W + 0.03
        for k in range(5 if shelf != 4 else 4):
            _box_arquivo(f"Caixa {shelf} {k}", x, y0, z[shelf], mat=mat if k % 3 else "kraft_velho")
            x += 0.145
        if shelf == 4:   # one box taken down and left lying on its side
            box("Caixa deitada", "kraft_velho", x + 0.01, y0, z[4], x + 0.27, y0 + 0.35, z[4] + 0.13, r=0.003)
    for k in range(3):
        box(f"Resma {k}", "papel", -W + 0.05, y0, z[0] + 0.05 * k, -W + 0.26, y0 + 0.297, z[0] + 0.05 * (k + 1) - 0.002, r=0.002)
    _box_arquivo("Caixa do chao", -W + 0.32, y0, z[0], w=0.35, d=0.36, h=0.27, mat="kraft_velho", lid=False)


# Coordination binder lying flat on shelf 0.78 (Helena's August 2024 maintenance file, taken out of the row above):
# spine towards -Y, x extent = binder height. The generator lays the coordination papers on its cover.
AGO_DEITADA = dict(x0=-0.43, x1=-0.11, d=0.285, t=0.075)


def build_estante_coordenacao():
    """Coordination shelving (north unit): 2024 maintenance binders by month (AGO missing from the row), the AGO binder
    lying flat below with its 'saidas / revisao' divider tab, 2025 binders, 2023 boxes, archive boxes, reams."""
    W, y0, z = _estante_frame()
    x = -W + 0.03
    for k in range(4):
        _box_arquivo(f"Caixa alta {k}", x, y0, z[5], mat="kraft" if k % 2 == 0 else "kraft_velho")
        x += 0.145
    box("Caixa deitada", "kraft_velho", x + 0.01, y0, z[5], x + 0.27, y0 + 0.35, z[5] + 0.13, r=0.003)
    x = -W + 0.03
    for k in range(5):
        _box_arquivo(f"Caixa 2023 {k}", x, y0, z[4], mat="polionda_azul", lid=False)
        x += 0.145
    # Shelf 1.12: JAN..DEZ 2024, slimmer binders; month 7 (AGO) is the gap. The last one leans into it a little.
    x = -W + 0.03
    for k in range(12):
        if k != 7:
            _az(f"AZ 2024 {k}", x, y0, z[3], "az_preto" if k % 4 else "az_cinza", w=0.062)
        x += 0.066
    # Shelf 0.78: AGO lying flat with the divider tab out of its front, then the 2025 binders.
    A = AGO_DEITADA
    zb = z[2]
    box("AZ agosto - capa de baixo", "az_cinza", A["x0"], y0, zb, A["x1"], y0 + A["d"], zb + 0.004, r=0.002)
    box("AZ agosto - papeis", "papel", A["x0"] + 0.02, y0 + 0.012, zb + 0.004, A["x1"] - 0.012, y0 + A["d"] - 0.004, zb + A["t"] - 0.006, r=0.002)
    box("AZ agosto - capa de cima", "az_cinza", A["x0"], y0, zb + A["t"] - 0.004, A["x1"], y0 + A["d"], zb + A["t"], r=0.002)
    box("AZ agosto - lombada", "az_cinza", A["x0"], y0 - 0.002, zb, A["x1"], y0 + 0.012, zb + A["t"], r=0.004)
    box("AZ agosto - visor", "visor_pasta", A["x0"] + 0.06, y0 - 0.0035, zb + 0.015, A["x0"] + 0.20, y0 - 0.0015, zb + A["t"] - 0.015, r=0.001)
    box("Aba saidas revisao", "aba", A["x1"] - 0.10, y0 - 0.032, zb + 0.034, A["x1"] - 0.04, y0 + 0.02, zb + 0.036, r=0.0005)
    x = A["x1"] + 0.035
    for k in range(6):
        _az(f"AZ 2025 {k}", x, y0, z[2], "az_azul", w=0.07)
        x += 0.074
    x = -W + 0.03
    for k in range(5):
        _box_arquivo(f"Caixa 2022 {k}", x, y0, z[1], mat="kraft_velho" if k % 2 else "kraft")
        x += 0.145
    for k in range(3):
        box(f"Resma {k}", "papel", -W + 0.05, y0, z[0] + 0.05 * k, -W + 0.26, y0 + 0.297, z[0] + 0.05 * (k + 1) - 0.002, r=0.002)
    _box_arquivo("Caixa do chao", -W + 0.32, y0, z[0], w=0.35, d=0.36, h=0.27, mat="kraft_velho", lid=False)


# ---------------------------------------------------------------- belongings locker


def pertences_cells():
    """(column 'A'/'B', level 1..3) -> (x0, x1, z0, z1) of each compartment opening, in the locker frame."""
    P = PERTENCES
    W, H, L, f = P["w"] / 2, P["h"], P["legs"], P["frame"]
    cw = (P["w"] - 3 * f) / 2
    ch = (H - L - 4 * f) / 3
    out = {}
    for c, col in enumerate("AB"):
        x0 = -W + f + c * (cw + f)
        for lvl in (1, 2, 3):
            z1 = H - f - (lvl - 1) * (ch + f)
            out[(col, lvl)] = (x0, x0 + cw, z1 - ch, z1)
    return out


def build_pertences():
    P = PERTENCES
    W, D, H, L, f = P["w"] / 2, P["d"] / 2, P["h"], P["legs"], P["frame"]
    s = 0.008
    box("Teto", "chapa_cinza", -W, -D, H - f, W, D, H, r=0.003, seg=3)
    box("Base", "chapa_cinza", -W, -D, L, W, D, L + f, r=0.002)
    for sx in (-1, 1):
        box(f"Lateral {sx:+d}", "chapa_cinza", sx * W - (sx > 0) * s, -D, L, sx * W + (sx < 0) * s, D, H, r=0.002)
    box("Fundo", "chapa_cinza", -W + s, D - s, L, W - s, D, H - f, r=0)
    cells = pertences_cells()
    cw = cells[("A", 1)][1] - cells[("A", 1)][0]
    box("Divisoria", "chapa_cinza", -f / 2, -D, L + f, f / 2, D - s, H - f, r=0.002)
    for lvl in (1, 2):
        z = cells[("A", lvl)][2]
        box(f"Prateleira {lvl}", "chapa_cinza", -W + s, -D, z - f, W - s, D - s, z, r=0.002)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(f"Pe {sx}{sy}", "chapa_escura", sx * (W - 0.03) - 0.02, sy * (D - 0.03) - 0.02, 0.0, sx * (W - 0.03) + 0.02,
                sy * (D - 0.03) + 0.02, L, r=0.003)
    # Hasp staples on the frame beside each door's free edge (doors hinge on their outer side).
    for (col, lvl), (x0, x1, z0, z1) in cells.items():
        free = x1 if col == "A" else x0
        sx = 1 if col == "A" else -1
        zc = (z0 + z1) / 2
        box(f"Porta-cadeado {col}{lvl}", "aco", free + sx * 0.004 - 0.006, -D - 0.022, zc - 0.012, free + sx * 0.004 + 0.006, -D, zc + 0.012, r=0.001, seg=1)
        # Address plate on the rail below the opening (its print is a Unity label quad).
        box(f"Placa {col}{lvl}", "aluminio", (x0 + x1) / 2 - 0.04, -D - 0.002, z0 - f + 0.004, (x0 + x1) / 2 + 0.04, -D, z0 - 0.004, r=0.0008, seg=1)


def build_porta_pertences():
    """One louvred door (column A side: hinge on its left, -X), closed, in its own frame: origin at the hinge axis,
    bottom of the door; the door spans +X. Column B doors use the same mesh mirrored by the generator."""
    cells = pertences_cells()
    x0, x1, z0, z1 = cells[("A", 1)]
    w, h = x1 - x0 - 0.004, z1 - z0 - 0.004
    t = 0.018
    box("Folha", "chapa_cinza", 0.0, -t, 0.0, w, 0.0, h, r=0.003, seg=3)
    for k in range(6):   # louvres near the top
        z = h - 0.05 - k * 0.016
        box(f"Veneziana {k}", "fenda", 0.08, -t - 0.001, z, w - 0.08, -t + 0.002, z + 0.006, r=0.0006, seg=1)
    # Hasp loop (aldrava) meeting the staple on the frame, card holder.
    box("Aldrava", "aco", w - 0.05, -t - 0.012, h / 2 - 0.016, w + 0.004, -t - 0.006, h / 2 + 0.016, r=0.002, seg=1)
    box("Porta-cartao", "aluminio", w / 2 - 0.05, -t - 0.003, h - 0.17, w / 2 + 0.05, -t, h - 0.11, r=0.001, seg=1)
    for z in (0.06, h - 0.06):
        cyl(f"Dobradica {z:.2f}", "aco", (-0.002, -t / 2, z), 0.005, 0.06, seg=10, r=0.001)


# ---------------------------------------------------------------- work bench and printer


def build_bancada():
    P = BANCADA
    W, D, H, t = P["w"] / 2, P["d"] / 2, P["h"], P["top_t"]
    box("Tampo", "tampo", -W, -D, H - t, W, D, H, r=0.003, seg=3)
    for sx in (-1, 0, 1):
        x = sx * (W - 0.05)
        for y in (-D + 0.05, D - 0.05):
            box(f"Perna {sx}{y:+.2f}", "estrutura", x - 0.02, y - 0.02, 0.0, x + 0.02, y + 0.02, H - t, r=0.003)
        box(f"Travessa {sx}", "estrutura", x - 0.015, -D + 0.05, 0.12, x + 0.015, D - 0.05, 0.15, r=0.002)
    box("Prateleira inferior", "painel", -W + 0.03, -D + 0.07, 0.15, W - 0.03, D - 0.03, 0.168, r=0.002)
    # Power strip screwed under the back edge; a USB charger in it with its cable tied to the leg (the recorder's).
    sx0 = W - 0.75
    box("Regua de tomadas", "regua", sx0, D - 0.12, H - t - 0.045, sx0 + 0.35, D - 0.06, H - t - 0.005, r=0.006)
    for k in range(5):
        cyl(f"Tomada {k}", "fenda", (sx0 + 0.04 + k * 0.065, D - 0.12 - 0.0005, H - t - 0.025), 0.011, 0.001, axis=(0, 1, 0), seg=14, r=0)
    box("Carregador USB", "carregador", sx0 + 0.1, D - 0.155, H - t - 0.04, sx0 + 0.14, D - 0.12, H - t - 0.01, r=0.004)
    cable("Cabo USB", "cabo", [(sx0 + 0.12, D - 0.16, H - t - 0.03), (sx0 + 0.12, D - 0.2, H - t - 0.06), (sx0 + 0.05, D - 0.25, H - 0.1),
                               (sx0 - 0.05, D - 0.30, H + 0.004), (sx0 - 0.12, D - 0.35, H + 0.004)], 0.0018)
    box("Ponta USB", "plastico", sx0 - 0.135, D - 0.36, H + 0.0, sx0 - 0.115, D - 0.343, H + 0.007, r=0.0015)
    tube("Abracadeira do cabo", "plastico", [(sx0 + 0.04, D - 0.27, H - t - 0.002), (sx0 + 0.06, D - 0.27, H - t - 0.002)], 0.004)
    # Seal tape dispenser, a box of numbered seals, a rubber stamp, a stack of guard folders, a document tray.
    lathe("Fita de lacre", "fita", [(0.045, 0.0), (0.045, 0.048), (0.025, 0.048), (0.025, 0.0)], seg=28, r=0.001,
          matrix=Matrix.Translation((-W + 0.25, -0.05, H)) @ Matrix.Rotation(math.pi / 2, 4, "Y"))
    box("Caixa de lacres", "polionda_cinza", -W + 0.36, -0.12, H, -W + 0.52, 0.0, H + 0.06, r=0.003)
    box("Pastas de guarda", "kraft", -W + 0.62, -0.20, H, -W + 0.94, 0.03, H + 0.025, r=0.002)
    box("Pasta de guarda de cima", "pasta_azul", -W + 0.615, -0.205, H + 0.025, -W + 0.945, 0.035, H + 0.03, r=0.002)
    box("Base do carimbo", "plastico", -W + 1.05, -0.10, H, -W + 1.11, -0.07, H + 0.014, r=0.002)
    cyl("Cabo do carimbo", "madeira", (-W + 1.08, -0.085, H + 0.04), 0.012, 0.05, seg=16, r=0.005)
    box("Almofada de carimbo", "plastico", -W + 1.13, -0.14, H, -W + 1.22, -0.07, H + 0.016, r=0.002)
    # Under the bench: archive boxes waiting to go up.
    _box_arquivo("Caixa sob a bancada 1", -W + 0.10, -D + 0.12, 0.168, w=0.35, d=0.36, h=0.27, mat="kraft", lid=False)
    _box_arquivo("Caixa sob a bancada 2", -W + 0.50, -D + 0.12, 0.168, w=0.35, d=0.36, h=0.27, mat="kraft_velho", lid=False)


# Printer table: laminate top on a welded steel frame, a lower shelf with reams and a toner box (the reception's
# support cabinet carries the padlock puzzle, so it is not reused here).
MESA_APOIO = dict(w=0.80, d=0.50, h=0.74, top_t=0.025)


def build_mesa_apoio():
    P = MESA_APOIO
    W, D, H, t = P["w"] / 2, P["d"] / 2, P["h"], P["top_t"]
    box("Tampo", "tampo", -W, -D, H - t, W, D, H, r=0.003, seg=3)
    box("Fita de borda", "borda", -W - 0.0015, -D - 0.0015, H - t, W + 0.0015, D + 0.0015, H - t + 0.002, r=0.001, seg=1)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (W - 0.03), sy * (D - 0.03)
            box(f"Perna {sx}{sy}", "estrutura", x - 0.015, y - 0.015, 0.012, x + 0.015, y + 0.015, H - t, r=0.002)
            cyl(f"Sapata {sx}{sy}", "borracha", (x, y, 0.006), 0.012, 0.012, seg=10, r=0.001)
        box(f"Quadro lateral {sx}", "estrutura", sx * (W - 0.03) - 0.01, -D + 0.045, H - t - 0.05, sx * (W - 0.03) + 0.01, D - 0.045, H - t, r=0.002)
    for sy in (-1, 1):
        box(f"Quadro {sy}", "estrutura", -W + 0.045, sy * (D - 0.03) - 0.01, H - t - 0.05, W - 0.045, sy * (D - 0.03) + 0.01, H - t, r=0.002)
    box("Prateleira", "painel", -W + 0.015, -D + 0.015, 0.16, W - 0.015, D - 0.015, 0.178, r=0.002)
    for k in range(2):
        box(f"Resma {k}", "papel", -W + 0.06, -0.15, 0.178 + 0.05 * k, -W + 0.27, 0.147, 0.178 + 0.05 * (k + 1) - 0.002, r=0.002)
    box("Caixa de toner", "kraft", -0.02, -0.12, 0.178, 0.36, 0.06, 0.32, r=0.003)


def build_impressora():
    """Monochrome laser multifunction (about 0.42 x 0.40 x 0.33 m): body, flatbed lid with ADF, control panel,
    output bay with a few printed sheets, paper cassette."""
    W, D = 0.21, 0.20
    box("Corpo", "impressora", -W, -D, 0.0, W, D, 0.25, r=0.012)
    box("Gaveta de papel", "impressora_escura", -W + 0.01, -D - 0.006, 0.01, W - 0.01, -D + 0.02, 0.085, r=0.005)
    box("Pega da gaveta", "fenda", -0.05, -D - 0.0065, 0.07, 0.05, -D - 0.004, 0.078, r=0.001)
    box("Saida de papel", "fenda", -W + 0.03, -D + 0.002, 0.12, W - 0.03, D - 0.06, 0.20, r=0.004)
    box("Folhas impressas", "papel", -0.105, -D + 0.03, 0.125, 0.105, -D + 0.327, 0.13, r=0.0008)
    box("Tampa do scanner", "impressora", -W, -D + 0.02, 0.25, W, D, 0.30, r=0.01)
    box("Alimentador", "impressora_escura", -W + 0.04, -D + 0.08, 0.30, W - 0.04, D - 0.02, 0.33, r=0.008)
    box("Bandeja do alimentador", "impressora_escura", -W + 0.06, -D + 0.10, 0.33, W - 0.06, D - 0.04, 0.335, r=0.002)
    hull("Painel", "impressora_escura", [(x, y, z) for x in (-W + 0.02, W - 0.02) for y, z in ((-D - 0.015, 0.215), (-D - 0.015, 0.235),
                                                                                             (-D + 0.03, 0.245), (-D + 0.03, 0.25))], r=0.003)
    box("Visor", "visor", -0.12, -D - 0.008, 0.232, -0.04, -D + 0.015, 0.243, r=0.001)
    for k in range(6):
        cyl(f"Botao {k}", "teclado", (0.0 + k * 0.025, -D - 0.002, 0.24), 0.006, 0.004, axis=(0, -0.4, 1), seg=12, r=0.001)
    cyl("Led verde", "led", (0.17, -D - 0.004, 0.238), 0.003, 0.003, axis=(0, -0.4, 1), seg=10, r=0.0005)


def build_pastas_manuais():
    """Magazine file of printer manuals and Nina's second folder (a grey one with a '041' tab, labelled in
    Unity), to stand on the support cabinet beside the printer."""
    box("Porta-revistas - fundo", "polionda_cinza", -0.05, -0.13, 0.0, 0.05, 0.13, 0.004, r=0.001)
    for sx in (-1, 1):
        extrude(f"Porta-revistas - lado {sx}", "polionda_cinza", [(-0.13, 0.0), (0.13, 0.0), (0.13, 0.30), (-0.13, 0.20)], "YZ",
                sx * 0.05 - 0.002, sx * 0.05 + 0.002, r=0.0008, seg=1)
    box("Porta-revistas - costas", "polionda_cinza", -0.05, 0.126, 0.0, 0.05, 0.13, 0.30, r=0.001)
    mats = ("manual_branco", "manual_branco", "pasta_cinza", "manual_azul", "manual_branco")
    x = -0.046
    for k, m in enumerate(mats):
        th = 0.012 if m != "pasta_cinza" else 0.016
        box(f"Manual {k}", m, x, -0.12, 0.004, x + th, 0.12, 0.27 - 0.01 * (k % 2), r=0.001)
        x += th + 0.002
    box("Aba 041", "aba", x - 0.018 - 0.016 * 2 - 0.004, -0.06, 0.27, x - 0.018 - 0.016 - 0.004 + 0.002, -0.02, 0.29, r=0.0005)


# ---------------------------------------------------------------- evidence props


# Belongings bag: white TNT (spunbond polypropylene) sack with sewn side seams, filled, standing on the cell floor and
# gathered at the top by a cotton drawstring (knot, two loose ends, a cord lock), its paper tag hanging on a string.
# Origin on the floor at the centre of the footprint, front (-Y) towards the locker door. The tag card sits where the
# Unity print quad of Etiquetas.json expects it (centre (0.11, -0.125, 0.175) here, facing -Y).
BOLSA = dict(a=0.150, b=0.112, neck=0.300, top=0.362, sn=0.13)


def _smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def bolsa_ponto(th, z):
    """Outer surface at angle th (0 = +X, -pi/2 = front) and height z (0 .. neck)."""
    a, b, zn, sn = BOLSA["a"], BOLSA["b"], BOLSA["neck"], BOLSA["sn"]
    if z < 0.03:
        s = 0.84 + 0.16 * math.sin(math.pi / 2 * z / 0.03)
    elif z < 0.17:
        s = 1.0 + 0.035 * math.sin(math.pi * (z - 0.03) / 0.14)
    else:
        s = sn + (1 - sn) * math.cos(math.pi / 2 * min((z - 0.17) / (zn - 0.17), 1.0)) ** 1.25
    n = 2.0 + 2.2 * (s - sn) / (1 - sn)
    c, si = math.cos(th), math.sin(th)
    x = a * s * math.copysign(abs(c) ** (2 / n), c)
    y = b * s * math.copysign(abs(si) ** (2 / n), si)
    d = Vector((x, y, 0))
    d = d.normalized() if d.length > 1e-6 else Vector((1, 0, 0))
    g = _smooth((z - 0.19) / 0.11) * (1 - 0.55 * math.exp(-((z - zn) / 0.01) ** 2))   # pleats, pinched by the cord
    off = 0.011 * g * s ** 0.3 * math.sin(13 * th + 0.8 * math.sin(3 * th))
    off += 0.0018 * (1 - g) * math.sin(9 * th + 2.0 + z * 30) * _smooth(z / 0.05)        # drape wrinkles
    w = _smooth((z - 0.03) / 0.04) * (1 - _smooth((z - 0.22) / 0.06))
    off += 0.006 * w * (math.sin(3 * th + 1.3) * math.sin(z * 25 + 0.5) + 0.6 * math.sin(5 * th + 0.4 + z * 14))
    off += 0.0022 * w * math.sin(6 * th + z * 38 + 0.7) * math.sin(2 * th - z * 11)          # handling creases
    if si < 0:                                                                           # contents push the front out
        off += 0.0065 * si * si * math.exp(-((z - 0.16) / 0.07) ** 2)
    p = Vector((x, y, z)) + d * off
    p.x += 0.012 * (z / zn) ** 2                                                         # slumps a little to one side
    return p


def _lean(z):
    return Vector((0.012 * (min(z, BOLSA["neck"]) / BOLSA["neck"]) ** 2, 0, z))


def bolsa_franzido(th, u):
    """The frill above the drawstring: u = 0 at the cord, 1 at the ragged top edge."""
    p0 = bolsa_ponto(th, BOLSA["neck"])
    d = p0 - _lean(p0.z); d.z = 0; d.normalize()
    top = BOLSA["top"] + 0.008 * math.sin(7 * th + 2.0) + 0.004 * math.sin(11 * th)
    r = 0.030 * u ** 0.8 + 0.008 * u * math.sin(13 * th + 0.8 * math.sin(3 * th) + 0.5)
    return p0 + d * r + Vector((0, 0, u * (top - p0.z)))


def bolsa_normal(th, z):
    e = 1e-4
    t = bolsa_ponto(th + e, z) - bolsa_ponto(th - e, z)
    v = bolsa_ponto(th, z + e) - bolsa_ponto(th, z - e)
    nrm = t.cross(v).normalized()
    out = bolsa_ponto(th, z) - _lean(z); out.z = 0
    return nrm if nrm.dot(out) >= 0 else -nrm


def bolsa_frente(x, z):
    """Angle on the front (-Y) half whose surface point is at x for height z."""
    best = None
    for k in range(361):
        th = -math.pi + math.pi * k / 360
        dx = abs(bolsa_ponto(th, z).x - x)
        if best is None or dx < best[0]:
            best = (dx, th)
    lo, hi = best[1] - math.pi / 360, best[1] + math.pi / 360
    for _ in range(30):
        m = (lo + hi) / 2
        # x decreases from th = -pi/2 (front) toward -pi; increases toward 0
        if bolsa_ponto(m, z).x < x:
            lo = m
        else:
            hi = m
    return (lo + hi) / 2


def sobre(th, z, h):
    return bolsa_ponto(th, z) + bolsa_normal(th, z) * h


def build_bolsa():
    import bmesh
    N = 64
    zs = [0.0, 0.004, 0.010, 0.018, 0.026, 0.034] + [0.04 + (BOLSA["neck"] - 0.04) * k / 26 for k in range(27)]
    rings = [[bolsa_ponto(2 * math.pi * k / N, z) for k in range(N)] for z in zs]
    rings += [[bolsa_franzido(2 * math.pi * k / N, u) for k in range(N)] for u in (0.15, 0.3, 0.45, 0.6, 0.75, 0.88, 1.0)]
    top = rings[-1]
    c_top = sum(top, Vector()) / N
    rings.append([p + (c_top - p).normalized() * 0.0025 + Vector((0, 0, -0.003)) for p in top])   # inner lip
    rings.append([c_top + (p - c_top) * 0.3 + Vector((0, 0, -0.03)) for p in top])                # pinched inside
    bm = bmesh.new()
    vr = [[bm.verts.new(p) for p in r] for r in rings]
    for A, B in zip(vr, vr[1:]):
        for k in range(N):
            bm.faces.new((A[k], A[(k + 1) % N], B[(k + 1) % N], B[k]))
    c0 = bm.verts.new((0, 0, 0))
    for k in range(N):
        bm.faces.new((vr[0][(k + 1) % N], vr[0][k], c0))
    c1 = bm.verts.new(sum(rings[-1], Vector()) / N)
    for k in range(N):
        bm.faces.new((vr[-1][k], vr[-1][(k + 1) % N], c1))
    R._finish("Saco", bm, "tnt")
    # Sewn side seams (a folded, stitched hem down each side).
    for th in (0.0, math.pi):
        cable("Costura lateral", "costura_tnt", [sobre(th, z, 0.0004) for z in (0.012, 0.05, 0.09, 0.13, 0.17, 0.21, 0.25, 0.28)],
              0.0019, per_seg=6)
    # Drawstring round the neck, a knot at the front right, two loose ends, a cord lock on the longer one.
    zc = BOLSA["neck"] - 0.002
    rad = []
    for k in range(48):
        th = 2 * math.pi * k / 48
        q = bolsa_ponto(th, zc) - _lean(zc); q.z = 0
        rad.append(q.length)
    rc = max(rad) + 0.0022
    ring = [_lean(zc) + Vector((math.cos(2 * math.pi * k / 40) * rc, math.sin(2 * math.pi * k / 40) * rc * 0.86, 0)) for k in range(41)]
    tube("Cordao", "cordao", ring + [ring[1]], 0.0022, res=2)
    thk = -1.15
    kn = _lean(zc) + Vector((math.cos(thk) * (rc + 0.002), math.sin(thk) * (rc + 0.002) * 0.86, 0))
    for k, (dx, dy, dz) in enumerate(((0, 0, 0), (0.003, -0.002, -0.002), (-0.002, -0.001, -0.003))):
        cyl(f"No {k}", "cordao", kn + Vector((dx, dy, dz)), 0.0040, 0.0065, axis=(0.3 + k * 0.4, 0.5, 0.8), seg=14, r=0.0018)
    ends = []
    for dth, zs_end in ((0.10, (0.285, 0.27, 0.25, 0.225, 0.20)), (-0.18, (0.287, 0.275, 0.262, 0.25))):
        pts = [kn + Vector((0, -0.002, -0.004))] + [sobre(thk + dth * (1 + i * 0.25), z, 0.0028) for i, z in enumerate(zs_end)]
        cable("Ponta do cordao", "cordao", pts, 0.0021, per_seg=6)
        ends.append(pts)
    a, b = ends[0][-2], ends[0][-1]
    cyl("Trava do cordao", "plastico", a.lerp(b, 0.4), 0.0056, 0.013, axis=tuple(b - a), seg=16, r=0.0015)
    cyl("Trava - mola", "plastico", a.lerp(b, 0.4) + (b - a).normalized() * 0.0085, 0.0035, 0.005, axis=tuple(b - a), seg=12, r=0.001)
    # Paper tag on a string: card 78 x 56 mm (the print quad covers its lower 48 mm), brass eyelet at the top.
    y0 = -0.1245
    box("Etiqueta da bolsa", "etiqueta", 0.071, y0, 0.151, 0.149, y0 + 0.0012, 0.207, r=0.0005)
    cyl("Ilhos", "ilhos", (0.11, y0 - 0.0002, 0.2015), 0.0034, 0.0007, axis=(0, 1, 0), seg=20, r=0.0002)
    cyl("Furo do ilhos", "fenda", (0.11, y0 - 0.0005, 0.2015), 0.0016, 0.0006, axis=(0, 1, 0), seg=14, r=0)
    eye = Vector((0.11, y0 - 0.0008, 0.2015))
    path = [kn + Vector((0, -0.003, -0.002))]
    for t in (0.2, 0.4, 0.6, 0.8):
        x, z = kn.x + (0.11 - kn.x) * t, kn.z + (0.215 - kn.z) * t
        path.append(sobre(bolsa_frente(x, z), z, 0.0025))
    path += [Vector((0.11, y0 - 0.004, 0.214)), eye]
    cable("Barbante", "barbante", path, 0.0009, per_seg=6)
    # How far the bag front comes forward under the card (must stay behind it).
    worst = min(bolsa_ponto(bolsa_frente(x, z), z).y for x in (0.075, 0.11, 0.145) for z in (0.155, 0.175, 0.2))
    print("BOLSA frente sob a etiqueta y =", round(worst, 4), "cartao y =", y0)


def build_remendo():
    """Bag P26 only (a child of that bag, same frame): a tear down the front, closed with crude hand stitches in navy
    thread over an older seam, split open again at the bottom (Nina took the recorder out because of it)."""
    import bmesh
    import random
    rnd = random.Random(26)

    def rx(z):
        return -0.078 + 0.004 * math.sin(z * 90) + 0.0018 * math.sin(z * 230 + 1)

    def at(x, z, h):
        th = bolsa_frente(x, z)
        return sobre(th, z, h), bolsa_normal(th, z)

    # The tear: a dark slit, wide where it is open (z 0.045..0.09), a hairline where it is stitched.
    bm = bmesh.new()
    rows = []
    zs = [0.045 + (0.215 - 0.045) * k / 40 for k in range(41)]
    for z in zs:
        if z < 0.09:
            w = 0.0045 * math.sin(math.pi * (z - 0.045) / 0.06) + 0.0006
        else:
            w = 0.0007
        x = rx(z)
        p, nrm = at(x, z, 0.00035)
        side = Vector((1, 0, 0)) - nrm * nrm.x
        side.normalize()
        rows.append((bm.verts.new(p - side * w / 2), bm.verts.new(p + side * w / 2)))
    for (a0, a1), (b0, b1) in zip(rows, rows[1:]):
        bm.faces.new((a0, a1, b1, b0))
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.0003)
    R._finish("Rasgo", bm, "fenda")
    # Frayed lips of the open part.
    for sg in (-1, 1):
        pts = []
        for z in (0.046, 0.055, 0.065, 0.075, 0.085, 0.092):
            w = 0.0045 * math.sin(math.pi * max(0.0, min(1.0, (z - 0.045) / 0.06))) + 0.0006
            pts.append(at(rx(z) + sg * (w / 2 + 0.0008), z, 0.0009)[0])
        cable("Borda do rasgo", "tnt_borda", pts, 0.0009, per_seg=4)
    # Cross stitches, uneven, from z 0.092 to 0.212, then a knot and a loose tail at the top.
    n = 12
    for i in range(n):
        z = 0.094 + (0.208 - 0.094) * i / (n - 1) + rnd.uniform(-0.002, 0.002)
        x = rx(z)
        for sg in (1, -1) if i % 3 != 1 else (1,):
            hw = rnd.uniform(0.0065, 0.0095)
            dz = sg * rnd.uniform(0.0035, 0.0055)
            p0 = at(x - hw, z - dz, -0.0004)[0]
            pa = at(x - hw * 0.6, z - dz * 0.6, 0.0006)[0]
            pm = at(x, z + rnd.uniform(-0.0008, 0.0008), 0.0010)[0]
            pb = at(x + hw * 0.6, z + dz * 0.6, 0.0006)[0]
            p1 = at(x + hw, z + dz, -0.0004)[0]
            cable("Ponto", "linha_remendo", [p0, pa, pm, pb, p1], 0.00062, per_seg=4)
            for q in (p0, p1):
                cyl("Furo do ponto", "fenda", at(q.x, q.z, 0.0003)[0], 0.0008, 0.0004, axis=tuple(at(q.x, q.z, 0)[1]), seg=8, r=0)
    k_ = at(rx(0.214) + 0.006, 0.216, 0.0016)[0]
    cyl("No da linha", "linha_remendo", k_, 0.0019, 0.0022, axis=tuple(at(k_.x, k_.z, 0)[1]), seg=12, r=0.0008)
    tail = [k_, at(rx(0.214) + 0.012, 0.212, 0.0015)[0], at(rx(0.2) + 0.016, 0.196, 0.0012)[0], at(rx(0.19) + 0.014, 0.181, 0.001)[0]]
    cable("Ponta da linha", "linha_remendo", tail, 0.00062, per_seg=5)


def _rrect(w, l, rc, seg=6):
    pts = []
    for cx, cy, a0 in ((w / 2 - rc, l / 2 - rc, 0), (-w / 2 + rc, l / 2 - rc, 90), (-w / 2 + rc, -l / 2 + rc, 180), (w / 2 - rc, -l / 2 + rc, 270)):
        for k in range(seg + 1):
            a = math.radians(a0 + 90 * k / seg)
            pts.append((cx + rc * math.cos(a), cy + rc * math.sin(a)))
    return pts


def build_gravador():
    """Pocket digital voice recorder (102 x 36 x 14.5 mm, after common Sony/Olympus models), lying on its back, top
    end towards +Y: graphite body, gloss black face with the LCD in a satin frame, stereo mic grilles and the record
    LED at the top, menu/back keys, a navigation ring with a centre play key, stop / REC / erase keys, a speaker
    grille; volume rocker and HOLD switch on the sides, USB slider at the bottom end, mic and headphone jacks on top."""
    L, W, T = 0.102, 0.036, 0.0145
    extrude("Corpo", "gravador", _rrect(W, L, 0.008), "XY", 0.0, T - 0.0012, r=0.003, seg=3)
    extrude("Painel", "gravador_face", _rrect(W - 0.0024, L - 0.0024, 0.0068), "XY", T - 0.003, T, r=0.0011, seg=2)
    box("Moldura da tela", "prata", -0.0145, 0.0065, T - 0.0004, 0.0145, 0.0325, T + 0.0001, r=0.0009)
    box("Tela", "tela", -0.0133, 0.0077, T - 0.0002, 0.0133, 0.0313, T + 0.0003, r=0.0005)
    for sx in (-1, 1):
        cx, cy = sx * 0.0105, 0.0405
        cyl("Grade do microfone", "prata", (cx, cy, T), 0.0058, 0.0008, seg=28, r=0.0003)
        for i in range(-3, 4):
            for j in range(-3, 4):
                hx, hy = cx + (i + 0.5 * (j % 2)) * 0.0015, cy + j * 0.0013
                if math.hypot(hx - cx, hy - cy) < 0.0045:
                    cyl("Furo da grade", "fenda", (hx, hy, T + 0.0004), 0.00045, 0.0003, seg=8, r=0)
    cyl("LED", "led", (0.0, 0.0405, T), 0.0011, 0.0009, seg=12, r=0.0002)
    for sx in (-1, 1):
        box("Tecla menu", "teclado", sx * 0.0095 - 0.004, 0.0005, T - 0.0003, sx * 0.0095 + 0.004, 0.0040, T + 0.0011, r=0.0013)
    cy = -0.0165
    cyl("Anel de navegacao", "teclado", (0, cy, T), 0.0108, 0.0016, seg=40, r=0.0005)
    for dx, dy in ((0, 0.0078), (0, -0.0078), (0.0078, 0), (-0.0078, 0)):
        cyl("Marca do anel", "prata", (dx, cy + dy, T + 0.0008), 0.0007, 0.0003, seg=10, r=0)
    cyl("Vao do anel", "fenda", (0, cy, T + 0.0003), 0.0053, 0.0012, seg=32, r=0)
    cyl("Tecla play", "prata", (0, cy, T + 0.0006), 0.0046, 0.0016, seg=32, r=0.0005)
    cyl("Tecla gravar", "botao_vermelho", (0, -0.0365, T + 0.0003), 0.0052, 0.0018, seg=28, r=0.0006)
    for sx in (-1, 1):
        box("Tecla parar / apagar", "teclado", sx * 0.012 - 0.0035, -0.0395, T - 0.0003, sx * 0.012 + 0.0035, -0.0335, T + 0.0011, r=0.0013)
    for k in range(5):
        y = -0.0462 + k * 0.0011
        box("Fenda do alto-falante", "fenda", -0.0065, y - 0.0003, T - 0.0001, 0.0065, y + 0.0003, T + 0.0002, r=0)
    box("Volume", "teclado", W / 2 - 0.0006, 0.006, 0.0045, W / 2 + 0.0008, 0.022, 0.0098, r=0.0007)
    box("Divisao do volume", "fenda", W / 2 + 0.0005, 0.0138, 0.005, W / 2 + 0.0009, 0.0142, 0.0093, r=0)
    box("Trilho do HOLD", "fenda", -W / 2 - 0.0002, -0.012, 0.0055, -W / 2 + 0.0004, -0.002, 0.0088, r=0)
    box("Chave HOLD", "teclado", -W / 2 - 0.0009, -0.0105, 0.006, -W / 2 + 0.0002, -0.0065, 0.0083, r=0.0004)
    box("Conector USB", "prata", -0.0055, -L / 2 - 0.0009, 0.0042, 0.0055, -L / 2 + 0.001, 0.0098, r=0.0006)
    for k in range(3):
        z = 0.0058 + k * 0.0012
        box("Friso do USB", "fenda", -0.0035, -L / 2 - 0.0011, z, 0.0035, -L / 2 - 0.0007, z + 0.0004, r=0)
    # Back (seen when it is turned over): battery door outline, speaker holes, the model sticker.
    for x0, y0, x1, y1 in ((-0.0145, -0.0425, 0.0145, -0.0419), (-0.0145, -0.0056, 0.0145, -0.005),
                           (-0.0145, -0.0425, -0.0139, -0.005), (0.0139, -0.0425, 0.0145, -0.005)):
        box("Tampa da pilha", "fenda", x0, y0, -0.0002, x1, y1, 0.00005, r=0)
    box("Trava da tampa", "teclado", -0.004, -0.0415, -0.0005, 0.004, -0.0375, 0.0, r=0.0003)
    for i in range(6):
        for j in range(4):
            cyl("Furo do alto-falante", "fenda", (-0.00625 + i * 0.0025, 0.014 + j * 0.0025, -0.0001), 0.00055, 0.0003, seg=8, r=0)
    box("Etiqueta do modelo", "etiqueta", -0.011, 0.031, -0.00025, 0.011, 0.044, 0.0, r=0.0002)
    for sx in (-1, 1):
        cyl("Anel do conector", "prata", (sx * 0.0085, L / 2, 0.0072), 0.0026, 0.0012, axis=(0, 1, 0), seg=20, r=0.0003)
        cyl("Furo do conector", "fenda", (sx * 0.0085, L / 2 + 0.0004, 0.0072), 0.0017, 0.0012, axis=(0, 1, 0), seg=16, r=0)


# Guard envelope (C4, 229 x 324 mm) lying face up, Leo's recorder sealed inside: the papers make a low pillow, the
# recorder a raised bump; the flap (EnvelopeAba, its own mesh, hinged on the top edge) is folded over the face and held
# by the red tamper tape, which tears in two when the flap is opened. The custody label is a Unity print quad
# (centre (0, -0.07), z 0.0064 here). The recorder lies inside with its centre at GRAVADOR_DENTRO, top end towards the
# mouth (+Y); the paper stays raised over it up to the mouth, so it can slide out.
ENV = dict(w2=0.1145, l2=0.162, h=0.0058, aba=0.044)
GRAVADOR_DENTRO = (0.0, 0.055, 0.0015)       # recorder origin (centre of its underside); Unity local (0, 0.0015, 0.055)
ZH_ABA = 0.0012                              # hinge height of the flap (the top edge away from the recorder)


def env_z(x, y):
    W2, L2, H = ENV["w2"], ENV["l2"], ENV["h"]
    e = _smooth(min(W2 - abs(x), L2 - abs(y)) / 0.020)
    z = 0.0012 + (H - 0.0012) * e + 0.00015 * math.sin(x * 60 + 1) * math.sin(y * 45) * e
    # The recorder inside: the paper is not shrink-wrapped round it, it tents over it, taut on top and falling off in a
    # long slope to the edges (shorter towards the bottom, where the papers below hold it down), with faint tension
    # lines from the recorder's corners to the envelope's corners.
    gx, gy = GRAVADOR_DENTRO[0], GRAVADOR_DENTRO[1]
    fx, fy = 0.012, 0.049                                    # tent footprint (recorder 18 x 51 mm half-size, rounded)
    dx, dy = max(0.0, abs(x - gx) - fx), max(0.0, abs(y - gy) - fy)
    d = math.sqrt(dx * dx + dy * dy + 0.006 ** 2) - 0.006
    R = 0.080 if dy == 0 else (0.070 if y > gy else 0.035)
    if dy > 0 and dx > 0:                                    # blend the radii round the corners
        k = dx / (dx + dy)
        R = 0.080 * k + (0.070 if y > gy else 0.035) * (1 - k)
    b = (1 - min(1.0, d / R)) ** 1.7 * e
    for sx in (-1, 1):
        for sy in (-1, 1):
            ax, ay, bx_, by_ = gx + sx * fx, gy + sy * fy, sx * W2, sy * L2
            t = max(0.0, min(1.0, ((x - ax) * (bx_ - ax) + (y - ay) * (by_ - ay)) / ((bx_ - ax) ** 2 + (by_ - ay) ** 2)))
            dd = math.hypot(x - ax - t * (bx_ - ax), y - ay - t * (by_ - ay))
            b += 0.04 * math.exp(-(dd / 0.004) ** 2) * b * (1 - t)
    z += (0.0175 - z) * min(1.0, b)
    z += 0.006 * max(0.0, 1 - math.hypot(x + W2, y + L2) / 0.035) ** 2       # dog-eared corner
    for (ax, ay, bx, by), k in (((-0.11, -0.02, 0.06, -0.16), 0.0003), ((0.03, 0.11, 0.115, -0.03), 0.00025)):
        t = max(0.0, min(1.0, ((x - ax) * (bx - ax) + (y - ay) * (by - ay)) / ((bx - ax) ** 2 + (by - ay) ** 2)))
        dd = math.hypot(x - ax - t * (bx - ax), y - ay - t * (by - ay))
        z += k * e * math.exp(-(dd / 0.003) ** 2) * (1 - b)                  # handling creases
    return z


def _folha(bm, x0, x1, ys, nx, zfun, down=False, half=None):
    """One sheet of a grid over (x, y) at height zfun, every face wound to face up (or down)."""
    rows = []
    for y in ys:
        hx = half(y) if half else None
        xa, xb = (-hx, hx) if hx else (x0, x1)
        rows.append([bm.verts.new((xa + (xb - xa) * i / nx, y, 0)) for i in range(nx + 1)])
    for row in rows:
        for v in row:
            v.co.z = zfun(v.co.x, v.co.y)
    for A, B in zip(rows, rows[1:]):
        for i in range(nx):
            q = (A[i], A[i + 1], B[i + 1], B[i])          # x then y: counter-clockwise from above, faces +Z
            bm.faces.new(tuple(reversed(q)) if down else q)


def _sem_recalculo(name, bm, mat):
    """Like R._finish, but keeps the winding as built (recalc_face_normals flips thin sheets unpredictably)."""
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    ob["mat"] = mat
    R._coll(R._current["asset"]).objects.link(ob)
    return ob


def _env_grid(name, mat, x0, x1, y0, y1, nx, ny, zfun, thick=None):
    """A thin overlay lying on the envelope (tape, slit, gum): one upward sheet."""
    import bmesh
    bm = bmesh.new()
    _folha(bm, x0, x1, [y0 + (y1 - y0) * j / ny for j in range(ny + 1)], nx, zfun)
    return _sem_recalculo(name, bm, mat)


def build_envelope():
    import bmesh
    W2, L2 = ENV["w2"], ENV["l2"]
    nx, ny = 80, 64
    bm = bmesh.new()
    G = [[bm.verts.new((-W2 + 2 * W2 * i / nx, -L2 + 2 * L2 * j / ny, 0)) for j in range(ny + 1)] for i in range(nx + 1)]
    for row in G:
        for v in row:
            v.co.z = env_z(v.co.x, v.co.y)
    for i in range(nx):
        for j in range(ny):
            bm.faces.new((G[i][j], G[i + 1][j], G[i + 1][j + 1], G[i][j + 1]))
    loop = [G[i][0] for i in range(nx + 1)] + [G[nx][j] for j in range(1, ny + 1)] + \
           [G[i][ny] for i in range(nx - 1, -1, -1)] + [G[0][j] for j in range(ny - 1, 0, -1)]
    low = [bm.verts.new((v.co.x, v.co.y, 0.0)) for v in loop]
    for k in range(len(loop)):
        k2 = (k + 1) % len(loop)
        bm.faces.new((loop[k], low[k], low[k2], loop[k2]))
    bm.faces.new(low)
    R._finish("Envelope", bm, "kraft")
    # Under the flap: the mouth (a dark slit) and the gummed band; shown once the flap opens.
    yf = L2 - ENV["aba"]
    _env_grid("Boca", "fenda", -0.104, 0.104, L2 - 0.0062, L2 - 0.0032, 100, 1, lambda x, y: env_z(x, y) + 0.0002, 0.0001)
    _env_grid("Faixa de cola", "kraft_velho", -0.108, 0.108, yf + 0.004, yf + 0.016, 100, 2, lambda x, y: env_z(x, y) + 0.00015, 0.0001)
    # The tape's lower half (stays on the body when it tears).
    _env_grid("Lacre - metade de baixo", "fita", -0.096, 0.096, yf - 0.010, yf, 100, 2, lambda x, y: env_z(x, y) + 0.0009, 0.0002)
    # Perforation of the tear-off stub.
    yp = -0.128
    for k in range(50):
        x = -0.104 + k * 0.0042
        z = env_z(x, yp)
        box("Picote", "fenda", x - 0.0012, yp - 0.0002, z - 0.0001, x + 0.0012, yp + 0.0002, z + 0.00015, r=0)
    # Back: bottom fold and the long centre seam, slightly proud of the face it lies on.
    box("Dobra do fundo", "kraft_velho", -W2 + 0.002, -L2 + 0.002, -0.0004, W2 - 0.002, -L2 + 0.055, 0.0, r=0.0002)
    box("Costura central", "kraft_velho", -0.012, -L2 + 0.055, -0.0003, 0.012, L2 - 0.002, 0.0, r=0.0002)


def build_envelope_aba():
    """The flap, in its own frame: origin on the hinge (the envelope's top edge, x 0, y l2, z at the edge), folded
    towards -Y over the face. Opening turns it about X. Upper half of the tamper tape on it."""
    import bmesh
    W2, L2, A = ENV["w2"], ENV["l2"], ENV["aba"]
    zh = ZH_ABA
    nx, ny = 100, 10

    def zf(x, y):
        return env_z(x, y) + 0.0005 - zh

    ys = [-A + A * j / ny for j in range(ny + 1)]                     # flap frame: free edge at -A, hinge at 0
    half = lambda y: 0.100 + 0.012 * min(1.0, (y + A) / A * 2.2)     # corners cut towards the free edge
    zl = lambda x, y: zf(x, y + L2)
    bm = bmesh.new()
    _folha(bm, 0, 0, ys, nx, zl, half=half)                                        # outside (up when closed)
    _folha(bm, 0, 0, ys, nx, lambda x, y: zl(x, y) - 0.0005, down=True, half=half)  # inside
    _sem_recalculo("Aba", bm, "kraft")
    yf = L2 - A
    _env_grid("Lacre - metade de cima", "fita", -0.096, 0.096, yf - L2, yf + 0.020 - L2, 100, 3,
              lambda x, y: env_z(x, y + L2) + 0.0009 - zh, 0.0002)


def build_pasta_contrato():
    """Red polypropylene folder with an elastic band, lying flat (origin on its bottom centre, opening edge towards -Y):
    the Limiar Servicos contract file, left on top of a filing cabinet. Cover label is a Unity print quad."""
    w, d, t = 0.25, 0.335, 0.022
    box("Capa de baixo", "az_vermelho", -w / 2, -d / 2, 0.0, w / 2, d / 2, 0.002, r=0.001)
    box("Papeis", "papel", -w / 2 + 0.012, -d / 2 + 0.01, 0.002, w / 2 - 0.016, d / 2 - 0.01, t - 0.003, r=0.001)
    box("Capa de cima", "az_vermelho", -w / 2, -d / 2, t - 0.002, w / 2, d / 2, t, r=0.001)
    box("Dorso", "az_vermelho", w / 2 - 0.003, -d / 2, 0.0, w / 2, d / 2, t, r=0.0015)
    for sy in (-1, 1):   # elastic band round the two short corners
        box(f"Elastico {sy}", "borracha", -w / 2 - 0.0015, sy * (d / 2 - 0.035) - 0.004, -0.0015, w / 2 + 0.0015,
            sy * (d / 2 - 0.035) + 0.004, t + 0.0015, r=0.0015, seg=1)
    box("Etiqueta da pasta", "etiqueta", -0.06, 0.02, t, 0.06, 0.09, t + 0.0006, r=0.0002)


def build_caixa():
    _box_arquivo("Caixa arquivo", -0.0675, -0.175, 0.0, mat="kraft")


BUILDERS = {"MesaServidor": build_mesa, "GavetaMesa1": lambda: build_gaveta_mesa(0), "GavetaMesa2": lambda: build_gaveta_mesa(1),
            "GavetaMesa3": lambda: build_gaveta_mesa(2),
            "EquipamentosMesa": build_equipamentos, "ArquivoAco": build_arquivo, "GavetaArquivo": build_gaveta_arquivo,
            "EstanteAco": build_estante, "EstanteCoordenacao": build_estante_coordenacao, "ArmarioPertences": build_pertences, "PortaPertences": build_porta_pertences,
            "Bancada": build_bancada, "Impressora": build_impressora, "PastasManuais": build_pastas_manuais,
            "BolsaPertences": build_bolsa, "RemendoBolsa": build_remendo, "Gravador": build_gravador, "EnvelopeGuarda": build_envelope, "EnvelopeAba": build_envelope_aba, "CaixaArquivo": build_caixa,
            "PastaContrato": build_pasta_contrato, "MesaApoio": build_mesa_apoio}


def build(asset):
    c = R._coll(asset)
    for o in list(c.objects):
        me = o.data
        bpy.data.objects.remove(o)
        if me and me.users == 0:
            bpy.data.meshes.remove(me)
    R._current["asset"] = asset
    BUILDERS[asset]()
    return list(c.objects)


assemble, layout_uvs, export_fbx = R.assemble, R.layout_uvs, R.export_fbx

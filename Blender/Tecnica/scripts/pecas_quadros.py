"""Wall-mounted steel enclosures of the technical room and the devices on their doors.
Enclosure language (reference: 400 x 400 x 120 surface panel, docs/revisao-tecnica/referencias.md): 1.2 mm sheet body
with welded back tabs, a flush door with 20 mm folded returns, two external lift-off hinges on the left, a quarter-turn
latch with a triangular insert on the right, a rubber gasket showing in the seam and stamped louvres where the panel
holds equipment that warms up. Closed doors, no interiors.
Frame: origin on the wall plane at floor level under the enclosure's centre; the back lies on y = 0, the door looks
towards -Y, z is height above the floor (Unity: x, z, y; prefabs face -Z). Door devices (selectors, pilot lights) are
separate assets placed by Layout.json so the puzzle can turn and light them later; the door only carries their
mounting rings. Printed plates and cards are atlas labels (etiquetas_tecnica.py) laid on the modelled plates.
Needs R (recepcao_modelo) and F (formas)."""
import math
from mathutils import Vector

T_DOOR = 0.020          # depth of the door's folded returns
GAP = 0.0025            # door to body seam
HOLE = 0.0112           # 22.3 mm mounting hole of the 22 mm devices

QUADROS = {
    'QuadroGeral': dict(W=0.60, H=0.90, D=0.18, z0=0.90, louvres=False),
    'PainelContingencia': dict(W=0.60, H=0.70, D=0.20, z0=1.00, louvres=True),
    'QuadroCamaraFria': dict(W=0.40, H=0.50, D=0.16, z0=1.15, louvres=False),
}


def door_y(q):
    return -q['D']


def gabinete(p, q, latch_z=None, hinge_rel=(0.16, 0.84)):
    """Body, door, hinges, latch, gasket, wall tabs and (optionally) side louvres. Returns the latch centre."""
    W, H, D, z0 = q['W'], q['H'], q['D'], q['z0']
    yb = -D + T_DOOR                       # front of the body (the door's returns close over it)
    R.box(f'{p} corpo', 'chapa', -W / 2, yb - 0.001, z0, W / 2, 0.0, z0 + H, r=0.0025, seg=2)
    # door: front sheet with folded returns, read as one solid with rounded folds
    R.box(f'{p} porta', 'chapa', -W / 2 + GAP, -D, z0 + GAP, W / 2 - GAP, yb + 0.003, z0 + H - GAP, r=0.004, seg=3)
    # gasket line in the seam, just behind the door's return edge
    for nm, (x0, z0_, x1, z1) in {'sup': (-W / 2 + 0.004, z0 + H - 0.006, W / 2 - 0.004, z0 + H - 0.0015),
                                  'inf': (-W / 2 + 0.004, z0 + 0.0015, W / 2 - 0.004, z0 + 0.006),
                                  'esq': (-W / 2 + 0.0015, z0 + 0.004, -W / 2 + 0.006, z0 + H - 0.004),
                                  'dir': (W / 2 - 0.006, z0 + 0.004, W / 2 - 0.0015, z0 + H - 0.004)}.items():
        R.box(f'{p} gaxeta {nm}', 'borracha', x0, yb - 0.0035, z0_, x1, yb + 0.001, z1, r=0.0008, seg=1)
    # external lift-off hinges on the left: knuckle in the seam, one leaf on the body side, one on the door return
    for k, f in enumerate(hinge_rel):
        zc = z0 + H * f
        xk, yk = -W / 2 - 0.0035, yb - 0.0015
        R.cyl(f'{p} dobradica {k} junta', 'zinco', (xk, yk, zc), 0.0050, 0.060, seg=16, r=0.0008)
        R.cyl(f'{p} dobradica {k} pino', 'zinco', (xk, yk, zc + 0.032), 0.0026, 0.005, seg=12, r=0.0006)
        R.box(f'{p} dobradica {k} aba corpo', 'zinco', -W / 2 - 0.0016, yk, zc - 0.028, -W / 2, yb + 0.026, zc + 0.028, r=0.0005, seg=1)
        R.box(f'{p} dobradica {k} aba porta', 'zinco', -W / 2 + GAP - 0.0016, -D + 0.004, zc - 0.028, -W / 2 + GAP, yk, zc + 0.028, r=0.0005, seg=1)
        for j, dz in enumerate((-0.017, 0.017)):
            R.cyl(f'{p} dobradica {k} rebite c{j}', 'zinco', (-W / 2 - 0.0018, yb + 0.016, zc + dz), 0.0024, 0.0012, axis=(1, 0, 0), seg=10, r=0.0004)
            R.cyl(f'{p} dobradica {k} rebite p{j}', 'zinco', (-W / 2 + GAP - 0.0018, -D + 0.011, zc + dz), 0.0024, 0.0012, axis=(1, 0, 0), seg=10, r=0.0004)
    # quarter-turn latch: zinc die-cast bezel and a triangular insert turned by the panel key
    lz = z0 + H / 2 if latch_z is None else latch_z
    lx = W / 2 - 0.035
    R.cyl(f'{p} fecho bisel', 'zamac', (lx, -D - 0.002, lz), 0.0125, 0.004, axis=(0, 1, 0), seg=24, r=0.0012)
    R.cyl(f'{p} fecho corpo', 'zamac', (lx, -D - 0.0055, lz), 0.0095, 0.004, axis=(0, 1, 0), seg=24, r=0.001)
    s = 0.0055
    tri = [(lx + s * math.cos(math.radians(90 + 120 * i)), lz + s * math.sin(math.radians(90 + 120 * i))) for i in range(3)]
    R.extrude(f'{p} fecho triangulo', 'zamac', tri, 'XZ', -D - 0.0075, -D - 0.0110, r=0.0008, seg=1)
    # welded wall tabs, two above and two below, each held by a zinc screw with washer
    for sx in (-1, 1):
        for sz, zt in ((1, z0 + H), (-1, z0)):
            x = sx * (W / 2 - 0.055)
            za, zb = (zt, zt + 0.032) if sz > 0 else (zt - 0.032, zt)
            R.box(f'{p} orelha {sx:+d}{sz:+d}', 'chapa', x - 0.018, -0.0018, za, x + 0.018, 0.0, zb, r=0.0006, seg=1)
            zc = zt + sz * 0.018
            R.cyl(f'{p} orelha {sx:+d}{sz:+d} arruela', 'zinco', (x, -0.0024, zc), 0.0075, 0.0012, axis=(0, 1, 0), seg=16, r=0.0003)
            R.cyl(f'{p} orelha {sx:+d}{sz:+d} parafuso', 'zinco', (x, -0.0042, zc), 0.0052, 0.0024, axis=(0, 1, 0), seg=16, r=0.0010)
            for kk, d in enumerate(((1, 0), (0, 1))):
                R.box(f'{p} orelha {sx:+d}{sz:+d} fenda {kk}', 'escuro', x - 0.0032 * d[0] - 0.0004 * d[1], -0.0056, zc - 0.0032 * d[1] - 0.0004 * d[0],
                      x + 0.0032 * d[0] + 0.0004 * d[1], -0.0050, zc + 0.0032 * d[1] + 0.0004 * d[0], r=0.0, seg=1)
    if q['louvres']:
        # stamped louvres on both sides, low and high (air in at the bottom, out at the top)
        for sx in (-1, 1):
            for band, zz in (('inf', z0 + 0.09), ('sup', z0 + H - 0.16)):
                for i in range(5):
                    zc = zz + i * 0.014
                    x0, x1 = (W / 2, W / 2 + 0.0035) if sx > 0 else (-W / 2 - 0.0035, -W / 2)
                    R.hull(f'{p} veneziana {sx:+d} {band} {i}', 'chapa',
                           [(x0 if sx > 0 else x1, y, z) for y in (yb + 0.04, -D * 0.25) for z in (zc, zc + 0.008)] +
                           [(x1 if sx > 0 else x0, y, zc + 0.008) for y in (yb + 0.045, -D * 0.25 - 0.005)], r=0.0006, seg=1)
                    R.box(f'{p} veneziana {sx:+d} {band} {i} fresta', 'escuro', min(x0, x1) + (0.0004 if sx > 0 else 0.0026), yb + 0.046,
                          zc + 0.0005, max(x0, x1) - (0.0026 if sx > 0 else 0.0004), -D * 0.25 - 0.006, zc + 0.0016, r=0.0, seg=1)
    return lx, lz


def bucha_arruela(p, x, y, z, up=True, r=0.0134):
    """Conduit landing on an enclosure face: hexagonal locknut (arruela) and the threaded nipple of the conduit."""
    s = 1 if up else -1
    R.cyl(f'{p} arruela', 'zinco', (x, y, z + s * 0.0015), r + 0.0065, 0.003, seg=6, r=0.0005)
    R.cyl(f'{p} luva', 'zinco', (x, y, z + s * 0.012), r + 0.0022, 0.018, seg=24, r=0.0010)


def prensa_cabos(p, x, y, z, cable_r=0.0032, up=True):
    """PG9 nylon cable gland: hex body on the face, dome nut with the rubber insert around the cable."""
    s = 1 if up else -1
    R.cyl(f'{p} sextavado', 'nylon', (x, y, z + s * 0.003), 0.0105, 0.006, seg=6, r=0.0006)
    R.cyl(f'{p} cupula', 'nylon', (x, y, z + s * 0.0105), 0.0090, 0.009, seg=24, r=0.0015,
          radius_top=0.0062 if up else 0.0090)
    R.cyl(f'{p} vedacao', 'borracha', (x, y, z + s * 0.0155), cable_r + 0.0012, 0.002, seg=16, r=0.0003)


def anel_montagem(p, x, y, z):
    """Visible metal ring of a 22 mm device behind its operator: the device is a separate asset."""
    R.cyl(f'{p} anel', 'cromo', (x, y - 0.0006, z), 0.0150, 0.0012, axis=(0, 1, 0), seg=28, r=0.0003)


def plaqueta(p, x, z, w, h, y, mat='acrilico', t=0.0018):
    """Engraved acrylic plate / laminated card held by two rivets (the print is an atlas label on its face)."""
    R.box(f'{p}', mat, x - w / 2 - 0.001, y - t, z - h / 2 - 0.001, x + w / 2 + 0.001, y, z + h / 2 + 0.001, r=0.0006, seg=1)
    return y - t


# ---------------------------------------------------------------- contingency panel (P02)

# Door layout in panel coordinates (x from the centre, z above the floor): three circuit columns (pilot light, legend,
# operator over its 0 / 1 plate), the display of total and limit, the legend D04 and the plan of the lighting it feeds.
PC = dict(cols=(-0.17, 0.0, 0.17), light_z=0.43, legend_z=0.37, sel_z=0.285, display=(0.0, 0.535), plate_z=0.625,
          card=(-0.145, 0.125, 0.22, 0.14), plan=(0.135, 0.125, 0.25, 0.16))


def painel_contingencia():
    q = QUADROS['PainelContingencia']
    z0, D = q['z0'], q['D']
    gabinete('PC', q, latch_z=z0 + 0.36)
    yf = -D
    out = {'devices': [], 'labels': []}
    # name plate and the danger sticker
    yp = plaqueta('PC placa', 0.0, z0 + PC['plate_z'], 0.30, 0.045, yf)
    out['labels'].append(('pc_placa', 0.0, z0 + PC['plate_z'], yp))
    out['labels'].append(('nr10', -0.235, z0 + PC['plate_z'], yf))
    # display: black moulded bezel with two windows (TOTAL, LIMITE) and their captions under them
    dx, dz = PC['display']
    R.box('PC visor moldura', 'plastico_preto', dx - 0.085, yf - 0.009, z0 + dz - 0.036, dx + 0.085, yf, z0 + dz + 0.036, r=0.003, seg=2)
    for k, wx in enumerate((-0.040, 0.040)):
        R.box(f'PC visor janela {k}', 'vidro_visor', dx + wx - 0.030, yf - 0.0095, z0 + dz - 0.006, dx + wx + 0.030, yf - 0.0075,
              z0 + dz + 0.026, r=0.0008, seg=1)
        out['labels'].append((('pc_total', 'pc_limite')[k], dx + wx, z0 + dz - 0.020, yf - 0.009))
    out['displays'] = [('Visor - contingencia', dx, z0 + dz + 0.010, yf - 0.0096, (0.140, 0.032))]
    # circuit columns
    for c, cx in zip(('refrigeracao', 'luz_geral', 'exame'), PC['cols']):
        anel_montagem(f'PC sinaleiro {c}', cx, yf, z0 + PC['light_z'])
        out['devices'].append(('Sinaleiro', f'Sinaleiro - {c}', cx, z0 + PC['light_z'], yf))
        yp = plaqueta(f'PC legenda {c}', cx, z0 + PC['legend_z'], 0.11, 0.032, yf)
        out['labels'].append((f'pc_{c}', cx, z0 + PC['legend_z'], yp))
        yp = plaqueta(f'PC posicoes {c}', cx, z0 + PC['sel_z'], 0.048, 0.048, yf)
        out['labels'].append(('pos_01', cx, z0 + PC['sel_z'], yp))
        anel_montagem(f'PC seletor {c}', cx, yp, z0 + PC['sel_z'])
        out['devices'].append(('SeletorChave' if c == 'refrigeracao' else 'Seletor', f'Seletor - {c}', cx, z0 + PC['sel_z'], yp))
    # legend card D04 and the plan of the affected lighting, laminated and riveted
    for nm, (cx, cz, w, h) in (('pc_d04', PC['card']), ('pc_planta', PC['plan'])):
        yp = plaqueta(f'PC {nm}', cx, z0 + cz, w, h, yf, mat='laminado', t=0.0008)
        for sx in (-1, 1):
            for sz in (-1, 1):
                R.cyl(f'PC {nm} rebite {sx:+d}{sz:+d}', 'aluminio', (cx + sx * (w / 2 - 0.006), yp - 0.0005, z0 + cz + sz * (h / 2 - 0.006)),
                      0.0024, 0.0012, axis=(0, 1, 0), seg=12, r=0.0005)
        out['labels'].append((nm, cx, z0 + cz, yp))
    # conduit from the top into the tray
    bucha_arruela('PC conduite', 0.10, -D / 2, z0 + q['H'])
    return out


# ---------------------------------------------------------------- main distribution board (QDG)

def quadro_geral():
    q = QUADROS['QuadroGeral']
    z0, D, H = q['z0'], q['D'], q['H']
    gabinete('QDG', q)
    yf = -D
    out = {'devices': [], 'labels': [], 'displays': []}
    yp = plaqueta('QDG placa', 0.0, z0 + H - 0.075, 0.36, 0.055, yf)
    out['labels'].append(('qdg_placa', 0.0, z0 + H - 0.075, yp))
    out['labels'].append(('nr10', 0.0, z0 + H - 0.165, yf))
    # circuit list printed on card, taped to the door at its four corners
    px, pz, w, h = -0.02, z0 + 0.36, 0.20, 0.28
    R.box('QDG relacao', 'laminado', px - w / 2, yf - 0.0006, pz - h / 2, px + w / 2, yf, pz + h / 2, r=0.0002, seg=1)
    out['labels'].append(('qdg_circuitos', px, pz, yf - 0.0007))
    for sx in (-1, 1):
        for sz in (-1, 1):
            cx, cz = px + sx * (w / 2 - 0.004), pz + sz * (h / 2 - 0.004)
            R.box(f'QDG fita {sx:+d}{sz:+d}', 'fita', cx - 0.014, yf - 0.0014, cz - 0.009, cx + 0.014, yf - 0.0010, cz + 0.009, r=0.0002, seg=1)
    # feeder from below (floor), two circuit conduits to the tray
    bucha_arruela('QDG alimentador', 0.0, -D / 2, z0, up=False, r=0.0167)
    for k, x in enumerate((-0.16, 0.16)):
        bucha_arruela(f'QDG saida {k}', x, -D / 2, z0 + H)
    return out


# ---------------------------------------------------------------- cold-room control board

# Digital controller of the TC-900E family (71 x 29 mm cut-out): bezel, display window and four keys under it.
CTRL = dict(z=0.40, w=0.076, h=0.034, depth=0.010)
CF_LIGHTS = (('ligado', -0.075), ('degelo', 0.0), ('alarme', 0.075))


def quadro_camara_fria():
    q = QUADROS['QuadroCamaraFria']
    z0, D, H = q['z0'], q['D'], q['H']
    gabinete('CF', q, latch_z=z0 + 0.20)
    yf = -D
    out = {'devices': [], 'labels': [], 'displays': []}
    yp = plaqueta('CF placa', 0.0, z0 + H - 0.045, 0.24, 0.036, yf)
    out['labels'].append(('cf_placa', 0.0, z0 + H - 0.045, yp))
    # controller: grey bezel proud of the door, recessed display window, rubber key pad below the window
    cz = z0 + CTRL['z']
    w, h, d = CTRL['w'], CTRL['h'], CTRL['depth']
    R.box('CF controlador moldura', 'plastico_cinza', -w / 2, yf - d, cz - h / 2, w / 2, yf, cz + h / 2, r=0.0025, seg=2)
    R.box('CF controlador janela', 'vidro_visor', -0.030, yf - d - 0.0004, cz - 0.001, 0.016, yf - d + 0.001, cz + 0.013, r=0.0005, seg=1)
    out['labels'].append(('cf_controlador', 0.0, cz, yf - d - 0.0003))
    out['displays'].append(('Visor - camara fria', -0.007, cz + 0.006, yf - d - 0.0014, (0.044, 0.0135)))
    for k, kx in enumerate((-0.027, -0.009, 0.009, 0.027)):
        R.box(f'CF controlador tecla {k}', 'borracha_cinza', kx - 0.0065, yf - d - 0.0018, cz - 0.0135, kx + 0.0065, yf - d + 0.001, cz - 0.0055,
              r=0.0012, seg=2)
    # three pilot lights with captions
    for nm, lx in CF_LIGHTS:
        anel_montagem(f'CF sinaleiro {nm}', lx, yf, z0 + 0.25)
        out['devices'].append(('Sinaleiro', f'Sinaleiro - {nm}', lx, z0 + 0.25, yf))
        out['labels'].append((f'cf_{nm}', lx, z0 + 0.205, yf))
    out['labels'].append(('nr10', 0.0, z0 + 0.095, yf))
    # supply conduit up to the tray; the room-temperature sensor cable leaves by a gland and is clipped up the wall
    bucha_arruela('CF conduite', -0.08, -D / 2, z0 + H)
    prensa_cabos('CF prensa sensor', 0.11, -0.035, z0 + H)
    return out


# ---------------------------------------------------------------- door devices (separate assets, origin on the mounting face)

def seletor():
    """22 mm two-position selector with a short lever: black bezel and knob, white index on the lever.
    The lever is modelled pointing up; position 0 turns it 45 deg to the left, position 1 45 deg to the right."""
    R.cyl('Seletor bisel', 'plastico_preto', (0, -0.0022, 0), 0.0146, 0.0044, axis=(0, 1, 0), seg=32, r=0.0012)
    R.cyl('Seletor corpo', 'plastico_preto', (0, -0.0075, 0), 0.0122, 0.0070, axis=(0, 1, 0), seg=32, r=0.0012)
    R.box('Seletor alavanca', 'plastico_preto', -0.0042, -0.0215, -0.0110, 0.0042, -0.0095, 0.0130, r=0.0022, seg=3)
    R.box('Seletor indice', 'indice', -0.0009, -0.0220, 0.0040, 0.0009, -0.0213, 0.0118, r=0.0002, seg=1)


def seletor_chave():
    """22 mm key-operated selector held in position: chrome bezel, cylinder face and the key left in it, bow up."""
    R.cyl('Chave bisel', 'cromo', (0, -0.0022, 0), 0.0146, 0.0044, axis=(0, 1, 0), seg=32, r=0.0012)
    R.cyl('Chave cilindro', 'cromo', (0, -0.0062, 0), 0.0105, 0.0040, axis=(0, 1, 0), seg=32, r=0.0010)
    R.box('Chave fenda', 'escuro', -0.0010, -0.0084, -0.0050, 0.0010, -0.0080, 0.0050, r=0.0, seg=1)
    # the key is flat in the plane of its slot (YZ) and stands 22 mm out of the cylinder, bow outermost
    prof = [(-0.0080, -0.0040), (-0.0130, -0.0040), (-0.0150, -0.0105), (-0.0275, -0.0105), (-0.0305, -0.0060), (-0.0305, 0.0060),
            (-0.0275, 0.0105), (-0.0150, 0.0105), (-0.0130, 0.0040), (-0.0080, 0.0040)]
    key = R.extrude('Chave cabeca', 'latao', prof, 'YZ', -0.0012, 0.0012, r=0.0008, seg=2)
    hole = R.cyl('Chave furo', 'latao', (0, -0.0262, 0.0), 0.0024, 0.006, axis=(1, 0, 0), seg=16, r=0.0)
    F.boolean(key, hole)


def sinaleiro():
    """22 mm LED pilot light: chrome-ringed black bezel; the lens is a separate unbaked asset tinted in Unity."""
    R.cyl('Sinaleiro bisel', 'plastico_preto', (0, -0.0020, 0), 0.0146, 0.0040, axis=(0, 1, 0), seg=32, r=0.0012)
    R.cyl('Sinaleiro aro', 'cromo', (0, -0.0042, 0), 0.0128, 0.0012, axis=(0, 1, 0), seg=32, r=0.0004)


def sinaleiro_lente():
    R.cyl('Lente', 'lente', (0, -0.0060, 0), 0.0112, 0.0040, axis=(0, -1, 0), seg=32, r=0.0016, radius_top=0.0100)

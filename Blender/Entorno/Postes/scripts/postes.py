"""Street poles, luminaires and telecom fittings for the morgue's street (Brazilian urban distribution network).
Authored in metres, Unity-local frame: origin at the pole foot on the sidewalk, +Y up, +X along the line, +Z toward the road.
Run headless:  blender -b --python Blender/Entorno/Postes/scripts/postes.py
Exports FBX to Assets/Modelos/Entorno/Postes plus assets.json (materials and wire anchors read by the map generator).
Reuses the helpers of Blender/Entorno/Acabamentos/scripts/modelos.py.
"""
import bpy, math, json, sys
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parents[1] / 'Acabamentos' / 'scripts'))
import modelos as M

ROOT = Path(__file__).resolve().parents[4]
SOURCE = ROOT / 'Blender/Entorno/Postes'
M.EXPORT = ROOT / 'Assets/Modelos/Entorno/Postes'
COLORS = {
    'Concreto_Poste': ((1, 1, 1), 0, .10), 'Concreto_Cruzeta': ((.90, .90, .88), 0, .10),
    'Argamassa': ((.50, .49, .46), 0, .06), 'Furo': ((.015, .015, .015), 0, 0),
    'Aco_Galvanizado': ((.46, .48, .48), .7, .34), 'Aco_Oxidado': ((.30, .22, .16), .35, .2),
    'Porcelana_Marrom': ((.26, .12, .055), 0, .78), 'Porcelana_Branca': ((.78, .77, .72), 0, .72),
    'Polimero_Cinza': ((.40, .42, .42), 0, .38), 'Aluminio_Fundido': ((.52, .53, .53), .55, .32),
    'Lente_Luminaria': ((.06, .06, .06), 0, .85), 'Tanque_Trafo': ((.44, .47, .47), .25, .32),
    'Cabo_Isolado': ((.03, .03, .03), 0, .30), 'Aluminio_Nu': ((.42, .43, .44), .8, .45),
    'Cobre_Nu': ((.40, .21, .11), .8, .40), 'PVC_Cinza': ((.42, .43, .42), 0, .30),
    'Plastico_Preto': ((.035, .035, .035), 0, .32), 'Plastico_Claro': ((.68, .69, .66), 0, .30),
    'Placa_ID': ((.72, .58, .10), .3, .35),
}
M.COLORS.update(COLORS)
ANCHORS = {}
H = 9.12              # visible pole length: foot on the sidewalk (y=-0.12 in the world), top at y=9.0

def V(*p): return Vector(p)

# ------------------------------------------------------------------ geometry helpers

def quads(name, faces, mat, outward=None, uvfn=None, smooth=False):
    """faces: list of 4-point (or n-point) polygons. Each polygon gets its own vertices so seams stay sharp.
    outward(i, pts) -> Vector or None: flips any polygon whose normal disagrees with it."""
    verts, polys, uvs = [], [], []
    for i, f in enumerate(faces):
        f = [Vector(p) for p in f]
        n = (f[1] - f[0]).cross(f[-1] - f[0])
        want = outward(i, f) if outward else None
        if want is not None and n.dot(want) < 0: f = list(reversed(f))
        base = len(verts)
        for p in f:
            verts.append(tuple(p)); uvs.append(uvfn(p, f) if uvfn else (p.x + p.z, p.y))
        polys.append(tuple(range(base, base + len(f))))
    return M.mesh(name, verts, polys, mat, uvs, smooth)

def basis(axis):
    a = Vector(axis).normalized()
    u = a.cross(V(1, 0, 0) if abs(a.x) < .9 else V(0, 1, 0)).normalized()
    return a, u, a.cross(u)

def lathe(name, base, axis, profile, mat, sides=20, cap_top=True, cap_bottom=True):
    """Surface of revolution: profile is [(radius, height)] bottom to top along `axis` from `base`."""
    a, u, w = basis(axis); b = Vector(base); verts = []; uvs = []; faces = []
    for j, (r, h) in enumerate(profile):
        for k in range(sides + 1):
            t = 2 * math.pi * k / sides
            verts.append(tuple(b + a * h + (u * math.cos(t) + w * math.sin(t)) * r)); uvs.append((k / sides, h))
    s = sides + 1
    for j in range(len(profile) - 1):
        for k in range(sides):
            q = [j * s + k, j * s + k + 1, (j + 1) * s + k + 1, (j + 1) * s + k]
            p = [Vector(verts[i]) for i in q]; n = (p[1] - p[0]).cross(p[3] - p[0])
            mid = sum(p, Vector()) / 4; ax = b + a * (mid - b).dot(a)
            radial = mid - ax
            if radial.length < 1e-6: radial = a * (1 if j else -1)
            # Surfaces whose radius shrinks upward face up/out; the axial comparison keeps the skirts' undersides right.
            if n.length > 1e-12 and n.dot(radial) < 0 and abs(n.normalized().dot(a)) < .98: q.reverse()
            elif n.length > 1e-12 and abs(n.normalized().dot(a)) >= .98:
                dr = profile[j + 1][0] - profile[j][0]
                if (n.dot(a) > 0) != (dr < 0): q.reverse()
            faces.append(tuple(q))
    o = M.mesh(name, verts, faces, mat, uvs, True)
    for end, flag, sign in [(0, cap_bottom, -1), (len(profile) - 1, cap_top, 1)]:
        r, h = profile[end]
        if flag and r > 1e-4:
            ring = [b + a * h + (u * math.cos(2 * math.pi * k / sides) + w * math.sin(2 * math.pi * k / sides)) * r for k in range(sides)]
            quads(name + '_tampa', [ring], mat, lambda i, f, s=sign: a * s)
    return o

def disc(name, center, normal, r, mat, sides=10):
    a, u, w = basis(normal); c = Vector(center)
    pts = [c + (u * math.cos(2 * math.pi * k / sides) + w * math.sin(2 * math.pi * k / sides)) * r for k in range(sides)]
    return quads(name, [pts], mat, lambda i, f: a)

def block(name, lo, hi, mat, bevel=0):
    lo, hi = Vector(lo), Vector(hi)
    return M.box(name, tuple((lo + hi) / 2), tuple(hi - lo), mat, bevel)

def bar(name, a, b, w, d, mat):
    """Flat bar/beam from a to b; `d` is the thickness along the direction most perpendicular to world up."""
    return M.beam(name, a, b, w, d, mat)

def bolt(c, axis, length, r=.008):
    c = Vector(c); a = Vector(axis).normalized()
    M.tube('Parafuso', [tuple(c - a * length / 2), tuple(c + a * length / 2)], r, 'Aco_Galvanizado', 8)
    for s in (-1, 1):
        lathe('Porca', c + a * (s * length / 2 - (.012 if s > 0 else 0)), a, [(r * 2.1, 0), (r * 2.1, .012)], 'Aco_Galvanizado', 6)

# ------------------------------------------------------------------ the DT concrete pole

def hx(y): return .085 + (.065 - .085) * y / H      # narrow half-width (along the line)
def hz(y): return .165 + (.080 - .165) * y / H      # wide half-width (toward the road)

def section(y):
    x, z = hx(y), hz(y); c, f = .008, .034; d = min(.022, x * .32); s = d * .6
    return [(x - c, -z), (x, -z + c), (x, -z + f), (x - d, -z + f + s), (x - d, z - f - s), (x, z - f), (x, z - c), (x - c, z),
            (-x + c, z), (-x, z - c), (-x, z - f), (-x + d, z - f - s), (-x + d, -z + f + s), (-x, -z + f), (-x, -z + c), (-x + c, -z)]

def pole_body(y0=-.3, top=H):
    lo, hi = section(y0), section(top)
    # Signed area in (x,z): outward of edge (dx,dz) is (dz,-dx) when the loop is counter-clockwise.
    area = sum(lo[k][0] * lo[(k + 1) % len(lo)][1] - lo[(k + 1) % len(lo)][0] * lo[k][1] for k in range(len(lo)))
    sgn = 1 if area > 0 else -1; faces = []; outs = []
    for k in range(len(lo)):
        a0, a1, b0, b1 = lo[k], lo[(k + 1) % len(lo)], hi[k], hi[(k + 1) % len(lo)]
        faces.append([(a0[0], y0, a0[1]), (a1[0], y0, a1[1]), (b1[0], top, b1[1]), (b0[0], top, b0[1])])
        dx, dz = a1[0] - a0[0], a1[1] - a0[1]; outs.append(V(dz, 0, -dx) * sgn)
    def uv(p, f):
        # Metres along the face's horizontal edge, v = height: the concrete texture tiles evenly on every face.
        t = V(f[1].x - f[0].x, 0, f[1].z - f[0].z)
        t = t.normalized() if t.length > 1e-6 else V(1, 0, 0)
        return (p.x * t.x + p.z * t.z, p.y)
    quads('Poste_corpo', faces, 'Concreto_Poste', lambda i, f: outs[i], uv)
    cap = [(x, top, z) for x, z in hi]
    quads('Poste_topo', [cap], 'Concreto_Poste', lambda i, f: V(0, 1, 0))
    # Through-holes in the web (bolt holes), and the dark ring of the casting marks near the top.
    for y in [2.6, 3.4, 4.2, 4.95, 5.25, 5.55, 6.3, 6.6, 6.9, 7.2, 7.5, 7.8, 8.1, 8.35, 8.6, 8.85]:
        for s in (-1, 1):
            d = min(.022, hx(y) * .32)
            disc('Furo', (s * (hx(y) - d - .0008), y, 0), (s, 0, 0), .011, 'Furo', 10)
    # Mortar patch where the pole enters the sidewalk.
    quads('Base_argamassa', [[(-.24, .012, -.30), (.24, .012, -.30), (.24, .012, .30), (-.24, .012, .30)]], 'Argamassa',
          lambda i, f: V(0, 1, 0), lambda p, f: (p.x, p.z))
    for x0, z0, x1, z1 in [(-.24, -.30, .24, -.30), (.24, -.30, .24, .30), (.24, .30, -.24, .30), (-.24, .30, -.24, -.30)]:
        quads('Base_borda', [[(x0, -.02, z0), (x1, -.02, z1), (x1, .012, z1), (x0, .012, z0)]], 'Argamassa',
              lambda i, f: V((f[0].x + f[1].x) / 2, 0, (f[0].z + f[1].z) / 2), lambda p, f: (p.x + p.z, p.y))
    # Identification plate (company number) on the road face.
    y = 3.1; z = hz(y)
    block('Placa_identificacao', (-.06, y - .045, z), (.06, y + .045, z + .004), 'Placa_ID', .002)
    for dy in (-.032, .032): disc('Rebite', (0, y + dy, z + .0045), (0, 0, 1), .004, 'Aco_Galvanizado', 6)

def band(y, h=.04, t=.004, mat='Aco_Galvanizado', pad=.003):
    """Galvanised strap clamped around the pole at height y."""
    x, z = hx(y) + pad, hz(y) + pad
    for lo, hi in [((-x - t, y - h / 2, -z - t), (x + t, y + h / 2, -z)), ((-x - t, y - h / 2, z), (x + t, y + h / 2, z + t)),
                   ((-x - t, y - h / 2, -z), (-x, y + h / 2, z)), ((x, y - h / 2, -z), (x + t, y + h / 2, z))]:
        block('Cinta', lo, hi, mat)
    block('Cinta_aperto', (x + t, y - h / 2, -.015), (x + t + .03, y + h / 2, .015), mat)
    bolt((x + t + .015, y, 0), (0, 0, 1), .05, .005)

# ------------------------------------------------------------------ hardware

def pin_insulator(base):
    b = Vector(base)
    M.tube('Pino', [tuple(b), tuple(b + V(0, .06, 0))], .011, 'Aco_Galvanizado', 10)
    prof = [(.018, .04), (.055, .055), (.074, .07), (.056, .078), (.050, .10), (.066, .11), (.050, .118),
            (.040, .14), (.044, .155), (.030, .165), (.030, .178), (.040, .186), (.034, .2)]
    lathe('Isolador_pino', b, (0, 1, 0), prof, 'Polimero_Cinza', 20)
    return b + V(0, .178, 0)

def primary_crossarm(y=8.85, name='primaria'):
    x0 = hx(y) + .003; xc = x0 + .045
    block('Cruzeta', (x0, y - .045, -1.0), (x0 + .09, y + .045, 1.0), 'Concreto_Cruzeta', .006)
    bolt((xc - .02, y, 0), (1, 0, 0), .32, .009)
    # Two flat braces (maos-francesas) from the arm down to the pole face.
    for s in (-1, 1):
        bar('Mao_francesa', (xc, y - .046, s * .56), (x0 + .002, y - .52, s * .02), .038, .006, 'Aco_Galvanizado')
        bolt((xc, y - .03, s * .56), (1, 0, 0), .13, .006)
    bolt((x0 + .01, y - .52, 0), (1, 0, 0), .32, .008)
    tops = [pin_insulator((xc, y + .045, z)) for z in (-.9, .3, .9)]
    ANCHORS.setdefault(CURRENT, {})[name] = [list(map(lambda v: round(v, 4), t)) for t in tops]

def spool_rack(ys, key='secundaria'):
    """Vertical secondary rack (armacao secundaria) on the road face with porcelain spools."""
    zf = lambda y: hz(y) + .002
    y0, y1 = min(ys) - .1, max(ys) + .1
    bar('Armacao', (0, y0, zf(y0) + .004), (0, y1, zf(y1) + .004), .05, .008, 'Aco_Galvanizado')
    for y in (y0 + .03, y1 - .03): bolt((0, y, 0), (0, 0, 1), 2 * hz(y) + .06, .008)
    pts = []
    for y in ys:
        z0 = zf(y) + .008; zc = z0 + .095
        for s in (-1, 1):
            block('Estribo', (-.022, y + s * .046 - .003, z0), (.022, y + s * .046 + .003, zc + .03), 'Aco_Galvanizado')
        M.tube('Pino_roldana', [(0, y - .05, zc), (0, y + .05, zc)], .006, 'Aco_Galvanizado', 8)
        lathe('Roldana', (0, y - .043, zc), (0, 1, 0),
              [(.012, 0), (.036, 0), (.040, .008), (.031, .025), (.029, .043), (.031, .061), (.040, .078), (.036, .086), (.012, .086)],
              'Porcelana_Branca', 18)
        pts.append([0, round(y, 4), round(zc + .029, 4)])
    ANCHORS.setdefault(CURRENT, {})[key] = pts

def telecom_clamps(ys=(5.45, 5.15)):
    pts = []
    for y in ys:
        band(y)
        z = hz(y) + .007
        block('Suporte_BAP', (-.018, y - .03, z), (.018, y + .03, z + .05), 'Aco_Galvanizado')
        M.tube('Olhal', [(-.02, y, z + .065), (.02, y, z + .065)], .012, 'Aco_Galvanizado', 10)
        pts.append([0, y, round(z + .07, 4)])
    ANCHORS.setdefault(CURRENT, {})['telecom'] = pts

def ground_wire(face=-1, y_top=7.05):
    """Copper earth lead down the pole face, in grey PVC conduit near the ground."""
    z = lambda y: face * (hz(y) + .012)
    pts = [(0, y, z(y)) for y in (y_top, 2.5)]
    M.tube('Aterramento', pts, .004, 'Cobre_Nu', 8, False)
    M.tube('Eletroduto_terra', [(0, 2.55, z(2.55)), (0, -.02, z(0))], .013, 'PVC_Cinza', 12)
    for y in (6.0, 4.5, 3.2, 1.8, .7): block('Grampo', (-.012, y - .01, z(y) - .016), (.012, y + .01, z(y) + .016), 'Aco_Galvanizado')

def common_fittings(lamp=True):
    telecom_clamps()
    ANCHORS[CURRENT]['cto'] = [[0, 4.8, round(hz(4.8), 4)]]
    ANCHORS[CURRENT]['luminaria'] = [[0, 7.35, round(hz(7.35) + .004, 4)]]

# ------------------------------------------------------------------ transformer

def cutout(base):
    """Fuse cutout (chave fusivel): porcelain stand-off with the brown fuse tube leaning out."""
    b = Vector(base)
    block('Base_chave', b + V(-.015, 0, -.015), b + V(.015, .05, .015), 'Aco_Galvanizado')
    lathe('Isolador_chave', b + V(0, .05, 0), (0, 1, 0),
          [(.022, 0), (.045, .02), (.03, .04), (.045, .07), (.03, .09), (.045, .12), (.03, .14), (.045, .17), (.03, .19), (.025, .26)],
          'Porcelana_Marrom', 16)
    top, bot = b + V(.07, .32, 0), b + V(.10, .02, 0)
    block('Contato_sup', top + V(-.03, -.01, -.02), top + V(.03, .02, .02), 'Aco_Galvanizado')
    block('Contato_inf', bot + V(-.03, -.02, -.02), bot + V(.03, .01, .02), 'Aco_Galvanizado')
    M.tube('Cartucho', [tuple(bot + V(-.006, .02, 0)), tuple(top + V(.004, -.01, 0))], .014, 'Porcelana_Marrom', 12)
    return top + V(0, .02, 0), bot

def transformer(y=7.05):
    """45 kVA pole-mount transformer on the lot face (-Z), cutouts on a second arm, arresters on the tank bracket."""
    zf = -(hz(y + .45) + .004); zc = zf - .34
    for yy in (y + .2, y + .7):
        band(yy, .05, .005)
        block('Suporte_trafo', (-.03, yy - .03, zf - .10), (.03, yy + .03, zf), 'Aco_Galvanizado')
    block('Chapa_suporte', (-.14, y + .12, zf - .12), (.14, y + .78, zf - .10), 'Tanque_Trafo')
    lathe('Tanque', (0, y, zc), (0, 1, 0), [(.0, 0), (.27, 0), (.29, .02), (.29, .80), (.30, .81), (.30, .85), (.0, .85)], 'Tanque_Trafo', 32)
    # Corrugated cooling fins around the back half of the tank.
    for k in range(18):
        a = math.pi * (.15 + .7 * k / 17) + math.pi
        p = V(math.cos(a) * .29, 0, math.sin(a) * .29)
        bar('Aleta', (p.x, y + .08, zc + p.z), (p.x, y + .74, zc + p.z), .006, .06, 'Tanque_Trafo')
    lathe('Tampa', (0, y + .85, zc), (0, 1, 0), [(.0, 0), (.31, 0), (.31, .025), (.29, .035), (.0, .045)], 'Tanque_Trafo', 32)
    for k in range(10):
        a = 2 * math.pi * k / 10; disc('Parafuso_tampa', (math.cos(a) * .3, y + .857, zc + math.sin(a) * .3), (0, 1, 0), .008, 'Aco_Galvanizado', 6)
    # High-voltage bushings on the lid, low-voltage bushings on the road side of the tank.
    hv = []
    for dx in (-.15, 0, .15):
        p = V(dx, y + .89, zc - .05)
        lathe('Bucha_AT', p, (0, 1, 0), [(.04, 0), (.05, .03), (.03, .05), (.05, .07), (.03, .09), (.045, .11), (.028, .13), (.04, .15), (.02, .18)],
              'Porcelana_Marrom', 16)
        M.tube('Terminal_AT', [tuple(p + V(0, .18, 0)), tuple(p + V(0, .23, 0))], .007, 'Cobre_Nu', 8)
        hv.append(p + V(0, .23, 0))
    lv = []
    for k, dx in enumerate((-.18, -.06, .06, .18)):
        p = V(dx, y + .62, zc + .27)
        lathe('Bucha_BT', p, (0, 0, 1), [(.026, 0), (.03, .02), (.022, .04), (.026, .055), (.012, .08)], 'Porcelana_Marrom', 12)
        lv.append(p + V(0, 0, .08))
    block('Placa_trafo', (-.07, y + .3, zc + .288), (.07, y + .42, zc + .292), 'Aluminio_Nu')
    for dy in (.03, .82):
        for s in (-1, 1): block('Olhal_icamento', (s * .22 - .015, y + dy, zc - .31), (s * .22 + .015, y + dy + .05, zc - .29), 'Aco_Galvanizado')
    # Cutout arm under the primary arm; jumpers primary -> cutout -> bushing.
    ya = 8.22; x0 = hx(ya) + .003; xc = x0 + .045
    block('Cruzeta_chaves', (x0, ya - .045, -.85), (x0 + .09, ya + .045, .55), 'Concreto_Cruzeta', .006)
    bolt((xc - .02, ya, 0), (1, 0, 0), .32, .009)
    prim = ANCHORS[CURRENT]['primaria']
    for i, z in enumerate((-.7, -.2, .3)):
        top, bot = cutout(V(xc, ya + .045, z))
        a = Vector(prim[i]); mid = (a + top) / 2 + V(.05, -.05, 0)
        M.tube('Jumper_AT', M.rounded_path([tuple(a + V(0, -.01, 0)), tuple(mid), tuple(top)], .08, 6), .004, 'Aluminio_Nu', 6, False)
        h = hv[i]; m1 = V(bot.x + .05, bot.y - .25, bot.z * .5 + h.z * .5); m2 = V(h.x * .5 + bot.x * .5, h.y + .25, h.z)
        M.tube('Jumper_trafo', M.rounded_path([tuple(bot), tuple(m1), tuple(m2), tuple(h)], .12, 6), .004, 'Cabo_Isolado', 6, False)
        # Surge arrester next to each cutout.
        pa = V(xc, ya + .045, z + .18)
        lathe('Para_raios', pa, (0, 1, 0), [(.022, 0), (.022, .02), (.04, .03), (.022, .05), (.04, .06), (.022, .08), (.04, .09), (.022, .11),
                                             (.04, .12), (.022, .14), (.018, .17)], 'Polimero_Cinza', 14)
        M.tube('Jumper_PR', M.rounded_path([tuple(pa + V(0, .17, 0)), tuple(pa + V(0, .25, -.09)), tuple(top + V(0, .01, 0))], .05, 5), .003,
               'Aluminio_Nu', 6, False)
    # Low-voltage leads from the tank's bushings round the pole to the secondary rack spools.
    sec = ANCHORS[CURRENT]['secundaria']
    for k, p in enumerate(lv):
        s = Vector(sec[k]); side = -1 if p.x < 0 else 1
        path = [tuple(p), tuple(p + V(0, -.05, .08)), (side * (hx(s.y) + .06), s.y + .05 + .02 * k, zf + .02),
                (side * (hx(s.y) + .06), s.y + .02, s.z - .07), tuple(s + V(side * .04, 0, 0))]
        M.tube('Cabo_BT', M.rounded_path(path, .07, 6), .009, 'Cabo_Isolado', 8, False)
    ground_wire(-1, y + .1)

# ------------------------------------------------------------------ luminaires (origin = mount on the pole's road face)

def lamp_arm():
    y = 7.35; z0 = -(hz(y) + .004)
    # Mounting saddle and two straps, sized for the pole at this height (pole axis is at z0 in this frame).
    block('Sela', (-.05, -.30, -.002), (.05, .06, .012), 'Aco_Galvanizado', .004)
    for dy in (-.24, .0):
        x, z = hx(y + dy) + .004, hz(y + dy) + .004
        for lo, hi in [((-x - .004, dy - .02, z0 - z - .004), (x + .004, dy + .02, z0 - z)), ((-x - .004, dy - .02, z0 - z), (-x, dy + .02, 0)),
                       ((x, dy - .02, z0 - z), (x + .004, dy + .02, 0))]:
            block('Cinta_braco', lo, hi, 'Aco_Galvanizado')
    path = M.rounded_path([(0, 0, .0), (0, .0, .25), (0, .42, 1.25), (0, .56, 1.62), (0, .58, 1.78)], .35, 12)
    M.tube('Braco', path, .024, 'Aco_Galvanizado', 14)
    M.tube('Tirante', M.rounded_path([(0, -.24, .01), (0, -.18, .3), (0, .2, .85)], .15, 6), .014, 'Aco_Galvanizado', 10)
    return V(0, .58, 1.78)

def sodium():
    tip = lamp_arm()
    # Closed cobra head: lofted rounded housing, refractor bowl below, photocell on top.
    secs = []
    for i in range(13):
        t = i / 12; z = tip.z - .05 + t * .66
        w = .085 + .085 * math.sin(math.pi * min(1, t * 1.25) * .5) - .03 * max(0, t - .8) / .2
        h = .06 + .035 * math.sin(math.pi * min(1, t * 1.4) * .5) - .02 * max(0, t - .85) / .15
        secs.append((z, w, h))
    rings = []
    for z, w, h in secs:
        ring = []
        for k in range(24):
            a = 2 * math.pi * k / 24; c, s = math.cos(a), math.sin(a)
            yy = (abs(s) ** .7) * (1 if s > 0 else -.35) * h
            ring.append((math.copysign(abs(c) ** .6, c) * w, tip.y + .02 + yy, z))
        rings.append(ring)
    faces = [[rings[i][k], rings[i][(k + 1) % 24], rings[i + 1][(k + 1) % 24], rings[i + 1][k]] for i in range(12) for k in range(24)]
    cz = tip.z + .28
    quads('Carcaca', faces, 'Aluminio_Fundido', lambda i, f: (sum(f, Vector()) / 4) - V(0, tip.y + .02, cz), None, True)
    for end, sign in [(0, -1), (12, 1)]: quads('Carcaca_fecho', [rings[end]], 'Aluminio_Fundido', lambda i, f, s=sign: V(0, 0, s))
    # Refractor bowl (lens slot: the generator swaps in the lit/unlit material).
    bowl = []
    c = V(0, tip.y - .005, tip.z + .34)
    for j in range(7):
        for k in range(21):
            a = 2 * math.pi * k / 20; t = j / 6
            r = math.sin(t * math.pi / 2); bowl.append((c.x + math.cos(a) * .12 * r, c.y - .085 * math.cos(t * math.pi / 2), c.z + math.sin(a) * .22 * r))
    bf = [(j * 21 + k, j * 21 + k + 1, (j + 1) * 21 + k + 1, (j + 1) * 21 + k) for j in range(6) for k in range(20)]
    o = M.mesh('Refrator', bowl, bf, 'Lente_Luminaria', None, True)
    fix_normals(o, c + V(0, .2, 0), outward=True)
    lathe('Rele_base', tip + V(0, .1, .42), (0, 1, 0), [(.0, 0), (.045, 0), (.045, .02), (.0, .02)], 'Plastico_Preto', 16)
    lathe('Rele', tip + V(0, .12, .42), (0, 1, 0), [(.0, 0), (.038, 0), (.038, .05), (.03, .065), (.0, .068)], 'Plastico_Claro', 16)
    ANCHORS.setdefault(CURRENT, {})['luz'] = [[0, round(c.y - .06, 4), round(c.z, 4)]]

def led():
    tip = lamp_arm()
    z0, z1, y0 = tip.z - .04, tip.z + .62, tip.y - .02
    block('Corpo_LED', (-.13, y0 - .02, z0 + .06), (.13, y0 + .045, z1), 'Aluminio_Fundido', .012)
    block('Encaixe_braco', (-.045, y0 - .015, z0 - .02), (.045, y0 + .055, z0 + .1), 'Aluminio_Fundido', .01)
    for k in range(9):
        x = -.104 + k * .026
        block('Aleta_LED', (x - .003, y0 + .045, z0 + .14), (x + .003, y0 + .07, z1 - .04), 'Aluminio_Fundido')
    block('Lente_LED', (-.11, y0 - .026, z0 + .12), (.11, y0 - .018, z1 - .04), 'Lente_Luminaria', .004)
    lathe('Rele_base', V(0, y0 + .07, z0 + .1), (0, 1, 0), [(.0, 0), (.04, 0), (.04, .015), (.0, .015)], 'Plastico_Preto', 16)
    lathe('Rele', V(0, y0 + .085, z0 + .1), (0, 1, 0), [(.0, 0), (.034, 0), (.034, .045), (.026, .058), (.0, .06)], 'Plastico_Claro', 16)
    ANCHORS.setdefault(CURRENT, {})['luz'] = [[0, round(y0 - .06, 4), round((z0 + z1) / 2, 4)]]

def fix_normals(o, center, outward=True):
    import bmesh
    bm = bmesh.new(); bm.from_mesh(o.data); c = M.B(center)
    flip = [f for f in bm.faces if (f.calc_center_median() - c).dot(f.normal) < 0] if outward else []
    if flip: bmesh.ops.reverse_faces(bm, faces=flip)
    bm.to_mesh(o.data); bm.free()

# ------------------------------------------------------------------ telecom fittings (origin = point on the cable / pole face)

def splice_closure():
    M.tube('Emenda', [(-.23, -.14, 0), (.23, -.14, 0)], .075, 'Plastico_Preto', 20)
    for s in (-1, 1):
        lathe('Emenda_tampa', (s * .23, -.14, 0), (s, 0, 0), [(.075, 0), (.072, .02), (.05, .035), (.02, .05), (.0, .05)], 'Plastico_Preto', 20)
        M.tube('Prensa_cabo', [(s * .27, -.14, .02), (s * .31, -.12, .01)], .012, 'Plastico_Preto', 8, False)
        bar('Suporte_emenda', (s * .15, -.07, 0), (s * .15, .0, 0), .025, .004, 'Aco_Galvanizado')
        M.tube('Abracadeira', [(s * .15 - .01, -.064, -.078), (s * .15 - .01, -.064, .078)], .004, 'Plastico_Preto', 6, False)
    M.tube('Mensageiro_emenda', [(-.32, 0, 0), (.32, 0, 0)], .008, 'Cabo_Isolado', 8, False)

def reserve_coil():
    c = V(0, -.42, 0)
    for s in (-1, 1):
        bar('Cruz_reserva', tuple(c + V(-.3, -.3 * s, 0)), tuple(c + V(.3, .3 * s, 0)), .03, .006, 'Aco_Galvanizado')
    bar('Haste_reserva', (0, 0, 0), tuple(c + V(0, .3, 0)), .025, .005, 'Aco_Galvanizado')
    for turn in range(4):
        pts = []
        for k in range(49):
            a = 2 * math.pi * k / 48
            r = .29 + .008 * math.sin(a * 3 + turn)
            pts.append((c.x + math.cos(a) * r, c.y + math.sin(a) * r, (turn - 1.5) * .016 + .01 * math.sin(a * 2 + turn)))
        M.tube('Cabo_reserva', pts, .0075, 'Cabo_Isolado', 6, False)
    for a in (0, math.pi / 2, math.pi, 3 * math.pi / 2):
        p = c + V(math.cos(a + math.pi / 4) * .29, math.sin(a + math.pi / 4) * .29, 0)
        M.tube('Amarra', [tuple(p + V(0, 0, -.045)), tuple(p + V(0, 0, .045))], .006, 'Plastico_Preto', 6)
    M.tube('Mensageiro_reserva', [(-.3, 0, 0), (.3, 0, 0)], .008, 'Cabo_Isolado', 8, False)

def cto(y=4.8):
    """Fibre termination box on the road face; origin on the pole face, box toward +Z."""
    for dy in (-.11, .11):
        x, z = hx(y + dy) + .004, hz(y + dy) + .004
        for lo, hi in [((-x - .004, dy - .015, -2 * z - .004), (x + .004, dy + .015, -2 * z)), ((-x - .004, dy - .015, -2 * z), (-x, dy + .015, 0)),
                       ((x, dy - .015, -2 * z), (x + .004, dy + .015, 0))]:
            block('Cinta_CTO', lo, hi, 'Aco_Galvanizado')
    block('Suporte_CTO', (-.06, -.15, .0), (.06, .15, .02), 'Aco_Galvanizado')
    block('CTO', (-.12, -.19, .02), (.12, .19, .13), 'Plastico_Claro', .015)
    block('CTO_tampa', (-.105, -.17, .13), (.105, .17, .138), 'Plastico_Claro', .006)
    for k in range(6):
        x = -.075 + k * .03
        M.tube('Prensa_CTO', [(x, -.19, .075), (x, -.215, .075)], .007, 'Plastico_Preto', 8)
    for k, (x, l) in enumerate([(-.075, .5), (-.045, .35), (.015, .62), (.045, .28)]):
        M.tube('Drop_sobra', M.rounded_path([(x, -.215, .075), (x, -.3, .09), (x + .03 * k, -.1 - l, .12 + .02 * k)], .06, 5), .0035, 'Cabo_Isolado', 6, False)

# ------------------------------------------------------------------ assemblies

CURRENT = None

def pole_rede(): pole_body(); primary_crossarm(); spool_rack([6.9, 6.7, 6.5, 6.3]); common_fittings()

def pole_transformer():
    pole_body(); primary_crossarm(); spool_rack([6.9, 6.7, 6.5, 6.3]); common_fittings(); transformer()

def pole_low():
    pole_body(); spool_rack([7.0], 'multiplex'); common_fittings()
    # Top cap on poles without a primary arm.
    lathe('Chapeu', (0, H, 0), (0, 1, 0), [(.0, 0), (.09, 0), (.09, .015), (.0, .035)], 'Concreto_Cruzeta', 4)

def pole_service():
    """Small round concrete service post (padrao de entrada); +Z toward the street, -Z toward the building."""
    top = 6.05
    lathe('Poste_circular', (0, -.2, 0), (0, 1, 0), [(.0, 0), (.078, 0), (.06, top + .2), (.0, top + .22)], 'Concreto_Poste', 18)
    r = lambda y: .078 + (.06 - .078) * (y + .2) / (top + .2)
    quads('Base_argamassa', [[(-.16, .01, -.16), (.16, .01, -.16), (.16, .01, .16), (-.16, .01, .16)]], 'Argamassa', lambda i, f: V(0, 1, 0),
          lambda p, f: (p.x, p.z))
    y = 5.8; z0 = r(y) + .004
    for dy in (-.06, .06):
        lathe('Cinta_entrada', (0, y + dy - .015, 0), (0, 1, 0), [(r(y) + .004, 0), (r(y) + .004, .03)], 'Aco_Galvanizado', 18, False, False)
    for s, key in [(1, 'ramal_rua'), (-1, 'ramal_predio')]:
        zc = s * (z0 + .08)
        for d in (-1, 1):
            block('Estribo', (-.02, y + d * .042 - .003, min(s * z0, s * (z0 + .11))), (.02, y + d * .042 + .003, max(s * z0, s * (z0 + .11))), 'Aco_Galvanizado')
        lathe('Roldana', (0, y - .039, zc), (0, 1, 0), [(.010, 0), (.032, 0), (.035, .007), (.027, .022), (.025, .039), (.027, .056), (.035, .071),
                                                        (.032, .078), (.010, .078)], 'Porcelana_Branca', 16)
        ANCHORS.setdefault(CURRENT, {})[key] = [[0, y, round(zc + s * .025, 4)]]
    # Steel conduit with a weatherhead down the face opposite the meter pillar's side, into the ground.
    x = -(r(5.5) + .02)
    path = M.rounded_path([(x + .02, 5.72, .06), (x - .04, 5.78, .0), (x - .02, 5.62, -.0), (x, 5.45, 0), (r(.4) * -1 - .02, .35, 0),
                           (-.06 - .02, .05, 0), (-.06, -.15, 0)], .06, 8)
    M.tube('Eletroduto', path, .016, 'Aco_Galvanizado', 12, False)
    for yy in (4.6, 3.1, 1.6): block('Abracadeira', (x - .02, yy - .012, -.022), (x + .02, yy + .012, .022), 'Aco_Galvanizado')
    lathe('Cabecote', (x - .03, 5.70, 0), (0, -1, 0), [(.0, 0), (.026, .0), (.026, .05), (.018, .06)], 'Aco_Galvanizado', 12, True, False)
    for k in range(2):
        M.tube('Condutor_ramal', M.rounded_path([(x - .03 + .006 * k, 5.65, 0), (x - .05, 5.55, .03), (0, 5.62, z0 * -1 - .06),
                                                 (0, y + .0, -(z0 + .055))], .04, 5), .006, 'Cabo_Isolado', 6, False)
    block('Placa_identificacao', (-.04, 2.6, r(2.6)), (.04, 2.66, r(2.6) + .003), 'Placa_ID')

ASSETS = [('Poste_DT_Rede', pole_rede), ('Poste_DT_Transformador', pole_transformer), ('Poste_DT_Baixa', pole_low),
          ('Poste_Entrada', pole_service), ('Luminaria_Sodio', sodium), ('Luminaria_LED', led),
          ('Telecom_Caixa_Emenda', splice_closure), ('Telecom_Reserva', reserve_coil), ('Telecom_CTO', cto)]

def main(only=None):
    global CURRENT
    SOURCE.mkdir(parents=True, exist_ok=True); M.EXPORT.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    for o in list(scene.objects): bpy.data.objects.remove(o, do_unlink=True)
    M.SCENE = scene; scene.unit_settings.system = 'METRIC'; scene.unit_settings.scale_length = 1
    for name, fn in ASSETS:
        if only and name not in only: continue
        CURRENT = name; fn(); M.finish(name)
    for i, col in enumerate(c for c in scene.collection.children):
        for o in col.objects: o.location = M.B((i * 3.0, 0, 0))
    block_text = bpy.data.texts.get('postes.py') or bpy.data.texts.new('postes.py')
    block_text.clear(); block_text.write((SOURCE / 'scripts' / 'postes.py').read_text())
    (M.EXPORT / 'assets.json').write_text(json.dumps({'assets': M.META, 'materials': COLORS, 'anchors': ANCHORS}, indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / 'Postes.blend'))
    print(json.dumps({k: v['triangles'] for k, v in M.META.items()}, indent=1)); print(json.dumps(ANCHORS))

if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else None)

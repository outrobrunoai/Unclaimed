"""Neighbourhood vegetation kit for Unclaimed (Blender 5.2, headless, deterministic).

    python3 Blender/Entorno/scripts/vegetacao_texturas.py                     # atlases (Pillow)
    blender -b --factory-startup --python Blender/Entorno/scripts/vegetacao.py # meshes, impostors, FBX

    blender -b --factory-startup --python Blender/Entorno/scripts/vegetacao.py -- --only Arvore_Sibipiruna_A,Grama_Tufo_A
                                                                               # rebuild some plants, merge vegetacao.json

Game-style plants: tapered tubes for trunks and branches, foliage as textured cards (twig clusters with a
cut-out alpha, folded in a shallow V) whose normals lean away from the crown centre so the crown shades as a
volume. Trees are grown as branching skeletons (arvores.py); lawn grass and capim are real blades (opaque tapered
strips), so tufts keep their volume seen from the side. Each plant is one FBX with <Name>_LOD0/_LOD1 (+ _LOD2
impostor for trees) and material slots named after the atlases; Unity builds the materials (alpha clip, both faces),
the LODGroup and the colliders from vegetacao.json (UnclaimedEntornoAssets.cs). Blender +X becomes Unity -X on
export, which plants don't mind.

Every mesh carries the wind weights of Unclaimed/Vegetacao as a linear vertex colour (R branch flexibility, G phase,
B leaf flutter, A main bend); plants without explicit weights get a height-based fallback in Geo.to_object.
"""
import json
import math
import os
import random
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from vegetacao_texturas import CELLS, GRASS_VARIANTS, SIZE, pull_push  # noqa: E402
import arvores  # noqa: E402
from PIL import Image  # noqa: E402

ROOT = os.path.normpath(os.path.join(HERE, ".."))
UNITY = os.path.normpath(os.path.join(ROOT, "..", "..", "Assets", "Modelos", "Entorno", "Vegetacao"))
TEX = os.path.join(UNITY, "Texturas")
PREVIEWS = os.path.join(ROOT, "previews")
PREVIEW = "--no-preview" not in sys.argv
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ONLY = set(ARGS[ARGS.index("--only") + 1].split(",")) if "--only" in ARGS else None

# Trees: branching skeletons and species parameters live in arvores.py.
TREES = arvores.SPECIES
SHRUBS = {   # ellipsoid crown (rx, rz, centre z), cards
    "Arbusto_Redondo_A": dict(seed=41, leaf="Folhas_Largas", cells="moita", size=(0.75, 0.55, 0.6), cards=90, card=(0.45, 0.6)),
    "Arbusto_Redondo_B": dict(seed=42, leaf="Folhas_Largas", cells="ramo", size=(0.6, 0.45, 0.5), cards=70, card=(0.4, 0.55)),
    "Arbusto_Estreito": dict(seed=43, leaf="Folhas_Estreitas", cells="ramo", size=(0.42, 0.85, 0.95), cards=80, card=(0.45, 0.6)),
}
MATERIAL_ORDER = ["Casca", "Casca_Sibipiruna", "Casca_Mangueira", "Folhas_Largas", "Folhas_Compostas", "Folhas_Estreitas",
                  "Plantas_Procedurais", "Grama_Laminas"]
GRASS_U = {n: (i + 0.5) / len(GRASS_VARIANTS) for i, n in enumerate(GRASS_VARIANTS)}   # column centres
# Per-plant wind amplitudes (m) / frequency (Hz) written to vegetacao.json, overriding the material defaults in Unity.
# Trees keep the material defaults; small plants get amplitudes proportional to their size and stiffness.
WIND = {
    "grama": {"Grama_Laminas": dict(tronco=0.035, ramos=0.02, folhas=0.006, frequencia=0.9)},
    "capim": {"Grama_Laminas": dict(tronco=0.16, ramos=0.07, folhas=0.015, frequencia=0.6)},
    "erva": {"Plantas_Procedurais": dict(tronco=0.03, ramos=0.03, folhas=0.01, frequencia=0.8)},
    "espada": {"Plantas_Procedurais": dict(tronco=0.012, ramos=0.006, folhas=0.0, frequencia=0.55)},
    "banana": {"Plantas_Procedurais": dict(tronco=0.05, ramos=0.08, folhas=0.03, frequencia=0.45)},
    "arbusto": {m: dict(tronco=0.03, ramos=0.035, folhas=0.012, frequencia=0.55) for m in ("Folhas_Largas", "Folhas_Estreitas", "Casca")},
    "trepadeira": {"Folhas_Largas": dict(tronco=0.0, ramos=0.0, folhas=0.008, frequencia=0.5)},
    "parado": {"Plantas_Procedurais": dict(tronco=0.0, ramos=0.0, folhas=0.0, frequencia=0.0)},
}


# ---------------------------------------------------------------- mesh builder

class Geo:
    """Accumulates faces with per-loop UVs and custom normals, one material slot per face."""

    def __init__(self):
        self.v, self.f, self.uv, self.n, self.mat, self.c = [], [], [], [], [], []

    def face(self, pts, uvs, normals, mat, colors=None):
        base = len(self.v)
        self.v.extend(pts)
        self.f.append(tuple(range(base, base + len(pts))))
        self.uv.extend(uvs)
        self.n.extend(normals)
        self.mat.append(mat)
        self.c.extend(colors if colors is not None else [None] * len(pts))

    def wind_colors(self):
        """Explicit RGBA where given; elsewhere A = R = (z / height)^2, B = A on foliage, G = hash of a 10 cm cell."""
        top = max((p.z for p in self.v), default=1.0) or 1.0
        out = []
        for face, m in zip(self.f, self.mat):
            for i in face:
                c = self.c[i]
                if c is None:
                    p = self.v[i]
                    a = min(1.0, max(0.0, p.z / top)) ** 2
                    cell = (round(self.v[face[0]].x * 10), round(self.v[face[0]].y * 10), round(self.v[face[0]].z * 10))
                    c = (a, random.Random(hash(cell) & 0xffffffff).random(), 0.0 if m.startswith("Casca") else a, a)
                out.append(tuple(c))
        return out

    def tris(self):
        return sum(len(f) - 2 for f in self.f)

    def to_object(self, name):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([tuple(p) for p in self.v], [], self.f)
        layer = mesh.uv_layers.new(name="UVMap")
        for i, loop in enumerate(mesh.loops):
            layer.data[i].uv = self.uv[loop.vertex_index]
        names = sorted(set(self.mat), key=lambda m: MATERIAL_ORDER.index(m) if m in MATERIAL_ORDER else 99)
        for m in names:
            mesh.materials.append(material(m))
        for poly, m in zip(mesh.polygons, self.mat):
            poly.material_index = names.index(m)
        colors = self.wind_colors()
        attr = mesh.color_attributes.new("Vento", "FLOAT_COLOR", "CORNER")
        for i, loop in enumerate(mesh.loops):
            attr.data[i].color = colors[loop.vertex_index]
        mesh.color_attributes.active_color = attr
        mesh.normals_split_custom_set([tuple(self.n[l.vertex_index]) for l in mesh.loops])
        mesh.validate(clean_customdata=False)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        return obj


def cell_uv(atlas, cell):
    x0, y0, x1, y1 = cell
    return x0 / SIZE, 1 - y1 / SIZE, x1 / SIZE, 1 - y0 / SIZE   # u0, v0 (bottom), u1, v1 (top)


def tube(g, path, radii, sides, repeat=1.0, mat="Casca", uv_rect=None, tile=None, colors=None):
    """Tapered tube along `path` (parallel-transported frames). UV: u around, v = length / 1.5 m,
    or squeezed into uv_rect = (u0, v0, u1, v1) once along the whole length. With `tile` (scan size in metres) the
    bark keeps its real scale: v = length / tile and the texture wraps round(circumference / tile) times.
    `colors`: one wind RGBA per ring."""
    if tile:
        repeat = max(1, round(2 * math.pi * radii[0] / tile))
    tangents = [(path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)]).normalized() for i in range(len(path))]
    ref = Vector((1, 0, 0)) if abs(tangents[0].x) < 0.9 else Vector((0, 1, 0))
    normal = tangents[0].cross(ref).normalized()
    frames = []
    for t in tangents:
        normal = (normal - t * normal.dot(t)).normalized()
        frames.append((normal, t.cross(normal)))
    length = [0.0]
    for i in range(1, len(path)):
        length.append(length[-1] + (path[i] - path[i - 1]).length)
    rings = []
    for i, p in enumerate(path):
        n, b = frames[i]
        rings.append([(p + (n * math.cos(a) + b * math.sin(a)) * radii[i], (n * math.cos(a) + b * math.sin(a)))
                      for a in (2 * math.pi * k / sides for k in range(sides + 1))])
    for i in range(len(path) - 1):
        for k in range(sides):
            quad = [rings[i][k], rings[i][k + 1], rings[i + 1][k + 1], rings[i + 1][k]]
            if uv_rect:
                u0, v0, u1, v1 = uv_rect
                uvs = [(u0 + (u1 - u0) * kk / sides, v0 + (v1 - v0) * length[ii] / length[-1]) for ii, kk in
                       ((i, k), (i, k + 1), (i + 1, k + 1), (i + 1, k))]
            else:
                uvs = [(kk / sides * repeat, length[ii] / (tile or 1.5)) for ii, kk in ((i, k), (i, k + 1), (i + 1, k + 1), (i + 1, k))]
            cols = [colors[ii] for ii in (i, i, i + 1, i + 1)] if colors else None
            g.face([q[0] for q in quad], uvs, [q[1] for q in quad], mat, cols)


def card(g, base, up, right, w, h, atlas, cell, center=None, fold=0.25, bend=0.0, lean=0.6, colors=None, bend_fn=None):
    """Twig card: bottom-centre at `base`, growing along `up`, `right` across; folded in a V along its axis.
    Normals lean toward (vertex - center) when a crown centre is given. `colors` = wind (R, G, B, A); A = None takes
    the main bend from bend_fn(z) per vertex; the bottom edge flutters less so the card stays on its twig."""
    up = up.normalized()
    right = (right - up * right.dot(up)).normalized()
    face_n = right.cross(up).normalized()
    if center is not None and face_n.dot(base + up * h * 0.5 - center) < 0:
        right = -right
        face_n = -face_n
    u0, v0, u1, v1 = cell_uv(atlas, cell)
    um = (u0 + u1) / 2
    droop = -Vector((0, 0, 1)) * bend * h
    pts = {
        "bl": base - right * w / 2 + face_n * fold * w / 2, "bc": base, "br": base + right * w / 2 + face_n * fold * w / 2,
        "tl": base + up * h + droop - right * w / 2 + face_n * fold * w / 2, "tc": base + up * h + droop,
        "tr": base + up * h + droop + right * w / 2 + face_n * fold * w / 2,
    }
    uvs = {"bl": (u0, v0), "bc": (um, v0), "br": (u1, v0), "tl": (u0, v1), "tc": (um, v1), "tr": (u1, v1)}

    def nrm(p):
        if center is None:
            return face_n
        return (face_n * (1 - lean) + (p - center).normalized() * lean).normalized()

    def col(k):
        r, gg, b, a = colors
        return (r, gg, b * (0.3 if k[0] == "b" else 1.0), bend_fn(pts[k].z) if a is None else a)

    for keys in (("bl", "bc", "tc", "tl"), ("bc", "br", "tr", "tc")):
        g.face([pts[k] for k in keys], [uvs[k] for k in keys], [nrm(pts[k]) for k in keys], atlas,
               [col(k) for k in keys] if colors else None)


def flat_card(g, center, size, angle, atlas, cell, z):
    c, s = math.cos(angle), math.sin(angle)
    ax, ay = Vector((c, s, 0)) * size / 2, Vector((-s, c, 0)) * size / 2
    u0, v0, u1, v1 = cell_uv(atlas, cell)
    p = Vector((center.x, center.y, z))
    g.face([p - ax - ay, p + ax - ay, p + ax + ay, p - ax + ay], [(u0, v0), (u1, v0), (u1, v1), (u0, v1)],
           [Vector((0, 0, 1))] * 4, atlas)


def rand_perp(rng, axis):
    a = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1)))
    a -= axis * a.dot(axis)
    return a.normalized() if a.length > 1e-4 else axis.orthogonal().normalized()


def bezier(p0, p1, p2, n):
    return [p0 * (1 - t) ** 2 + p1 * 2 * (1 - t) * t + p2 * t * t for t in (i / (n - 1) for i in range(n))]


# ---------------------------------------------------------------- materials (Blender preview / impostor only)

_mats = {}


def material(name):
    if name in _mats:
        return _mats[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    path = os.path.join(TEX, name + "_BaseMap.png") if not name.startswith("Impostor_") else \
        os.path.join(TEX, name[len("Impostor_"):] + "_Impostor_BaseMap.png")
    if os.path.exists(path):
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(path, check_existing=True)
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if not name.startswith("Casca") and name != "Grama_Laminas":
            nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.8
    _mats[name] = m
    return m


# ---------------------------------------------------------------- plants

def tree(name, P, lod):
    g = Geo()
    _, cards = arvores.build_tree(P, lod, g, tube, card, CELLS[P["leaf"]]["ramo"])
    print(f"  {name} LOD{lod}: {cards} leaf cards")
    return g


def shrub(name, P, lod):
    rng = random.Random(P["seed"])
    g = Geo()
    rx, rz, cz = P["size"]
    center = Vector((0, 0, cz))
    for s in range(3 if lod == 0 else 0):
        a = rng.uniform(0, 2 * math.pi)
        tube(g, bezier(Vector((0, 0, -0.02)), Vector((math.cos(a) * 0.1, math.sin(a) * 0.1, cz * 0.6)),
                       Vector((math.cos(a) * rx * 0.4, math.sin(a) * rx * 0.4, cz)), 3), [0.025, 0.018, 0.01], 3)
    count = P["cards"] if lod == 0 else int(P["cards"] * 0.4)
    scale = 1.0 if lod == 0 else 1.4
    cells = CELLS[P["leaf"]][P["cells"]]
    for i in range(count):
        while True:
            q = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1)))
            if 0.35 ** 2 < q.length_squared <= 1 and q.z > -0.85:
                break
        anchor = center + Vector((q.x * rx, q.y * rx, q.z * rz))
        out = Vector((q.x, q.y, q.z * 0.8 + 0.25)).normalized()
        up = (out + rand_perp(rng, out) * 0.5).normalized()
        w = rng.uniform(*P["card"]) * scale
        card(g, anchor - up * w * 0.4, up, rand_perp(rng, up), w, w, P["leaf"], rng.choice(cells), center=center,
             fold=0.3, lean=0.65)
    return g


def banana(name, seed, lod):
    rng = random.Random(seed)
    g = Geo()
    cells = CELLS["Plantas_Procedurais"]
    stem_uv = cell_uv("Plantas_Procedurais", cells["pseudocaule"][0])
    stems = rng.randint(2, 3)
    for s in range(stems):
        a = 2 * math.pi * s / stems + rng.uniform(-0.4, 0.4)
        foot = Vector((math.cos(a), math.sin(a), 0)) * (0.0 if s == 0 else rng.uniform(0.3, 0.5))
        hgt = rng.uniform(1.7, 2.3) * (1 if s == 0 else rng.uniform(0.6, 0.85))
        lean = Vector((math.cos(a), math.sin(a), 0)) * rng.uniform(0.05, 0.2)
        top = foot + lean + Vector((0, 0, hgt))
        r = rng.uniform(0.09, 0.12) * (1 if s == 0 else 0.8)
        tube(g, bezier(foot - Vector((0, 0, 0.03)), foot + lean * 0.4 + Vector((0, 0, hgt * 0.5)), top, 4),
             [r * 1.15, r, r * 0.9, r * 0.75], 7 if lod == 0 else 5, mat="Plantas_Procedurais", uv_rect=stem_uv)
        leaves = rng.randint(5, 8) if lod == 0 else 3
        for k in range(leaves):
            az = 2 * math.pi * k / leaves + rng.uniform(-0.3, 0.3)
            out = Vector((math.cos(az), math.sin(az), 0))
            length = rng.uniform(1.5, 2.1) * (1 if s == 0 else 0.8)
            rise = rng.uniform(0.6, 1.2)
            p0 = top - Vector((0, 0, rng.uniform(0, 0.15)))
            p1 = p0 + out * length * 0.45 + Vector((0, 0, rise))
            p2 = p0 + out * length + Vector((0, 0, rise * 0.4 - rng.uniform(0.3, 0.8)))
            leaf_strip(g, bezier(p0, p1, p2, 8 if lod == 0 else 4), rng.uniform(0.42, 0.55), cells["bananeira"][0],
                       rng.uniform(0.1, 0.3), rng)
        for k in range(rng.randint(1, 3) if lod == 0 else 0):     # dry leaves hanging along the stem
            az = rng.uniform(0, 2 * math.pi)
            out = Vector((math.cos(az), math.sin(az), 0))
            p0 = top - Vector((0, 0, 0.1))
            p2 = p0 + out * 0.35 - Vector((0, 0, rng.uniform(1.0, 1.4)))
            leaf_strip(g, bezier(p0, p0 + out * 0.35, p2, 5), 0.35, cells["bananeira_seca"][0], 0.05, rng)
    return g


def leaf_strip(g, path, width, cell, fold, rng):
    """Long leaf along `path` (petiole first), folded along the midrib; texture runs along the length."""
    u0, v0, u1, v1 = cell_uv("Plantas_Procedurais", cell)
    length = [0.0]
    for i in range(1, len(path)):
        length.append(length[-1] + (path[i] - path[i - 1]).length)
    twist = rng.uniform(-0.4, 0.4)
    rows = []
    for i, p in enumerate(path):
        t = (path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)]).normalized()
        side = t.cross(Vector((0, 0, 1)))
        side = side.normalized() if side.length > 1e-3 else Vector((1, 0, 0))
        side = Matrix.Rotation(twist * i / len(path), 3, t) @ side
        up = side.cross(t).normalized()
        u = u0 + (u1 - u0) * length[i] / length[-1]
        rows.append((p - side * width / 2 + up * fold * width / 2, p, p + side * width / 2 + up * fold * width / 2, up, u))
    for i in range(len(rows) - 1):
        a, b = rows[i], rows[i + 1]
        g.face([a[0], a[1], b[1], b[0]], [(a[4], v0), (a[4], (v0 + v1) / 2), (b[4], (v0 + v1) / 2), (b[4], v0)], [a[3], a[3], b[3], b[3]], "Plantas_Procedurais")
        g.face([a[1], a[2], b[2], b[1]], [(a[4], (v0 + v1) / 2), (a[4], v1), (b[4], v1), (b[4], (v0 + v1) / 2)], [a[3], a[3], b[3], b[3]], "Plantas_Procedurais")


def grass(name, seed, lod, kind):
    """Crossed, slightly bent cards: tall capim, low grass tufts, broad-leaf weeds."""
    rng = random.Random(seed)
    g = Geo()
    cells = CELLS["Plantas_Procedurais"]
    if kind == "capim":
        clumps, per, wh, spread, cell = 1, (5, 7), ((0.55, 0.8), (0.75, 1.15)), 0.12, cells["capim"][0]
    elif kind == "grama":
        clumps, per, wh, spread, cell = rng.randint(4, 6), (2, 3), ((0.22, 0.32), (0.16, 0.28)), 0.3, cells["grama"][0]
    else:
        clumps, per, wh, spread, cell = 1, (3, 4), ((0.3, 0.42), (0.26, 0.4)), 0.05, cells["erva"][0]
    if lod == 1:
        per = (max(1, per[0] - 2), max(2, per[1] - 2))
    for c in range(clumps):
        foot = Vector((rng.uniform(-spread, spread), rng.uniform(-spread, spread), 0)) if c else Vector()
        n = rng.randint(*per)
        for k in range(n):
            a = math.pi * k / n + rng.uniform(-0.2, 0.2)
            right = Vector((math.cos(a), math.sin(a), 0))
            tilt = Vector((-math.sin(a), math.cos(a), 0)) * rng.uniform(-0.25, 0.25)
            up = (Vector((0, 0, 1)) + tilt + (Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)) * 0.25 if kind == "erva" else Vector())).normalized()
            card(g, foot + Vector((0, 0, -0.01)), up, right, rng.uniform(*wh[0]), rng.uniform(*wh[1]), "Plantas_Procedurais",
                 cell, center=foot + Vector((0, 0, -0.3)), fold=0.1, bend=0.05, lean=0.5)
    return g


def blade(g, root, lean_dir, face_dir, length, width, theta0, curl, u, phase, segs):
    """One opaque blade: root fixed, rising at theta0 from vertical and curling over by `curl` (rad) towards the tip,
    tapering to a point. Normals blend the blade normal with +Z (lawn-like shading on both faces).
    Wind: s = arc fraction -> R = s, B = 0.6 s, A = s^2, G = phase."""
    pts, p = [root], root.copy()
    for i in range(segs):
        th = theta0 + curl * ((i + 0.5) / segs) ** 1.5
        p = p + (lean_dir * math.sin(th) + Vector((0, 0, math.cos(th)))) * length / segs
        pts.append(p)
    side = Vector((-face_dir.y, face_dir.x, 0)).normalized()
    du = 0.32 / len(GRASS_VARIANTS)
    rows = []
    for i, q in enumerate(pts):
        sfrac = i / segs
        t = (pts[min(i + 1, segs)] - pts[max(i - 1, 0)]).normalized()
        sd = (side - t * side.dot(t)).normalized()
        n = (t.cross(sd).normalized() * 0.45 + Vector((0, 0, 1)) * 0.55).normalized()
        w = width * (1 - sfrac ** 1.6) / 2
        rows.append((q - sd * w, q + sd * w, n, sfrac, (sfrac, phase, 0.6 * sfrac, sfrac * sfrac)))
    for i in range(segs):
        a, b = rows[i], rows[i + 1]
        if i < segs - 1:
            g.face([a[0], a[1], b[1], b[0]], [(u - du, a[3]), (u + du, a[3]), (u + du, b[3]), (u - du, b[3])],
                   [a[2], a[2], b[2], b[2]], "Grama_Laminas", [a[4], a[4], b[4], b[4]])
        else:
            tip = pts[-1]
            g.face([a[0], a[1], tip], [(u - du, a[3]), (u + du, a[3]), (u, 1.0)], [a[2], a[2], b[2]], "Grama_Laminas",
                   [a[4], a[4], b[4]])


GRASS_KINDS = {
    # Grama-esmeralda / batatais left unmown for a while: fine blades 6-17 cm, mostly green, a few yellowed and dry.
    "grama": dict(blades=(38, 54), length=(0.07, 0.19), width=(0.004, 0.0065), radius=0.15, theta=(4, 34),
                  curl=(0.15, 0.9), segs=3, palette=[("verde", 3), ("verde_medio", 3), ("verde_escuro", 2.5),
                                                     ("verde_azulado", 1.2), ("amarelado", 0.35), ("seco", 0.2)]),
    # Capim (Urochloa/Panicum in a neglected corner): long arching blades, more straw.
    "capim": dict(blades=(18, 26), length=(0.4, 0.95), width=(0.008, 0.014), radius=0.07, theta=(4, 26),
                  curl=(0.4, 1.5), segs=5, palette=[("verde_medio", 3), ("verde", 2), ("amarelado", 2), ("seco", 1.2),
                                                    ("talo", 0.5)]),
}


def blade_tuft(name, seed, lod, kind):
    """Tuft of real blades on a disk (denser and taller in the middle). LOD1 keeps every third blade, wider and with
    fewer segments, at the same place, so the cross-fade does not shift the silhouette."""
    K = GRASS_KINDS[kind]
    rng = random.Random(seed)
    names, weights = zip(*K["palette"])
    bias = rng.choice(names[:3])          # each tuft leans to one green
    specs = []
    for _ in range(rng.randint(*K["blades"])):
        r = K["radius"] * min(1.0, abs(rng.gauss(0, 0.5)))
        a = rng.uniform(0, 2 * math.pi)
        root = Vector((math.cos(a) * r, math.sin(a) * r, -0.01))
        out = Vector((math.cos(a), math.sin(a), 0))
        lean = (out * (0.4 + r / K["radius"]) + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)) * 0.6).normalized()
        face = Matrix.Rotation(rng.uniform(-0.7, 0.7), 3, Vector((0, 0, 1))) @ lean
        length = rng.uniform(*K["length"]) * (1.0 - 0.35 * r / K["radius"])
        col = bias if rng.random() < 0.35 else rng.choices(names, weights)[0]
        specs.append((root, lean, face, length, rng.uniform(*K["width"]), math.radians(rng.uniform(*K["theta"])),
                      rng.uniform(*K["curl"]), GRASS_U[col], rng.random()))
    g = Geo()
    keep = specs if lod == 0 else specs[::3]
    for root, lean, face, length, width, th, curl, u, phase in keep:
        blade(g, root, lean, face, length, width * (1.0 if lod == 0 else 2.1), th, curl, u, phase,
              K["segs"] if lod == 0 else max(2, K["segs"] - 2))
    return g


def sansevieria(name, seed, lod):
    rng = random.Random(seed)
    g = Geo()
    cells = CELLS["Plantas_Procedurais"]["espada"]
    blades = rng.randint(14, 18) if lod == 0 else 7
    for k in range(blades):
        a = rng.uniform(0, 2 * math.pi)
        foot = Vector((math.cos(a), math.sin(a), 0)) * rng.uniform(0, 0.12)
        h = rng.uniform(0.5, 0.85)
        lean = Vector((math.cos(a), math.sin(a), 0)) * rng.uniform(0.02, 0.15)
        path = bezier(foot, foot + Vector((0, 0, h * 0.5)), foot + lean + Vector((0, 0, h)), 4 if lod == 0 else 2)
        w = rng.uniform(0.06, 0.09)
        u0, v0, u1, v1 = cell_uv("Plantas_Procedurais", rng.choice(cells))
        turn = a + rng.uniform(-1.2, 1.2)
        face_dir = Vector((math.cos(turn), math.sin(turn), 0))
        side = Vector((0, 0, 1)).cross(face_dir).normalized()   # winding (p-s, p+s, up) faces face_dir
        for i in range(len(path) - 1):
            t0, t1 = i / (len(path) - 1), (i + 1) / (len(path) - 1)
            p0, p1 = path[i], path[i + 1]
            s0 = side * w / 2 * (1 - 0.1 * t0)
            s1 = side * w / 2 * (1 - 0.1 * t1)
            n0 = (face_dir + Vector((0, 0, 0.35))).normalized()
            g.face([p0 - s0, p0 + s0, p1 + s1, p1 - s1], [(u0, v0 + (v1 - v0) * t0), (u1, v0 + (v1 - v0) * t0),
                   (u1, v0 + (v1 - v0) * t1), (u0, v0 + (v1 - v0) * t1)], [n0] * 4, "Plantas_Procedurais")
    return g


def vine(name, seed, lod):
    """Creeping fig on a wall: cards nearly in the local XZ plane (wall face), facing -Y, ragged outline."""
    rng = random.Random(seed)
    g = Geo()
    cells = CELLS["Folhas_Largas"]["moita"] + CELLS["Folhas_Largas"]["ramo"]
    count = 120 if lod == 0 else 40
    scale = 1.0 if lod == 0 else 1.5
    placed = 0
    while placed < count:
        x, z = rng.uniform(-1.0, 1.0), rng.uniform(0.0, 1.6)
        edge = 1.6 * (0.55 + 0.45 * math.sin(x * 2.3 + 1.1) * math.cos(x * 4.1))   # ragged top
        if z > edge or (abs(x) > 0.8 and rng.random() < 0.5):
            continue
        placed += 1
        w = rng.uniform(0.32, 0.45) * scale
        base = Vector((x, -rng.uniform(0.005, 0.06), max(0.0, z - w * 0.3)))
        up = Matrix.Rotation(rng.uniform(-2.6, 2.6), 3, Vector((0, 1, 0))) @ Vector((0, 0, 1))
        up = (up + Vector((0, -0.25, 0))).normalized()
        right = up.cross(Vector((0, -1, 0))).normalized()
        card(g, base, up, right, w, w, "Folhas_Largas", rng.choice(cells), center=base + Vector((0, 0.6, 0)),
             fold=0.12, lean=0.35)
    return g


def dry_leaves(name, seed):
    rng = random.Random(seed)
    g = Geo()
    cell = CELLS["Plantas_Procedurais"]["folhas_secas"][0]
    for k in range(rng.randint(5, 7)):
        r = rng.uniform(0, 0.6)
        a = rng.uniform(0, 2 * math.pi)
        flat_card(g, Vector((math.cos(a) * r, math.sin(a) * r, 0)), rng.uniform(0.35, 0.6), rng.uniform(0, 6.28),
                  "Plantas_Procedurais", cell, 0.006 + 0.002 * k)
    return g


# ---------------------------------------------------------------- impostors, previews, export

def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)


def setup_render(res_x, res_y, samples=24):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = False
    sc.render.film_transparent = True
    sc.render.resolution_x, sc.render.resolution_y = res_x, res_y
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"
    world = sc.world or bpy.data.worlds.new("World")
    sc.world = world
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (0.55, 0.55, 0.55, 1)
    bg.inputs["Strength"].default_value = 1.0


def light_and_camera(target_h, width, ortho, view=(0.0, -1.0, 0.0)):
    sun_data = bpy.data.lights.new("Sol", "SUN")
    sun_data.energy = 2.2
    sun = bpy.data.objects.new("Sol", sun_data)
    sun.rotation_euler = (math.radians(50), 0, math.radians(30))
    bpy.context.scene.collection.objects.link(sun)
    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    target = Vector((0, 0, target_h / 2))
    if ortho:
        cam_data.type = "ORTHO"
        cam_data.ortho_scale = max(target_h, width) * 1.04
        cam.location = target + Vector(view) * 30
    else:
        cam_data.lens = 50
        dist = max(target_h, width) * 1.9 + 0.5
        cam.location = target + Vector((0.7, -1.0, 0.45)).normalized() * dist
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam_data.clip_end = 200
    bpy.context.scene.camera = cam
    return [sun, cam]


def render_to(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def impostor(name, obj, h, width):
    """Orthographic side render of LOD0 into <Name>_Impostor_BaseMap.png (1024 px, colour pushed into alpha 0)."""
    setup_render(1024, 1024, samples=32)
    extra = light_and_camera(h, width, True)
    path = os.path.join(TEX, name + "_Impostor_BaseMap.png")
    render_to(path)
    pull_push(Image.open(path).convert("RGBA")).save(path)
    for o in extra:
        bpy.data.objects.remove(o, do_unlink=True)
    return max(h, width) * 1.04


def impostor_mesh(name, size):
    g = Geo()
    u0, v0, u1, v1 = 0.0, 0.0, 1.0, 1.0
    for a in (0.0, math.pi / 2):
        right = Vector((math.cos(a), math.sin(a), 0))
        n = Vector((-math.sin(a), math.cos(a), 0)) * -1
        p = [-right * size / 2, right * size / 2, right * size / 2 + Vector((0, 0, size)), -right * size / 2 + Vector((0, 0, size))]
        g.face(p, [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], [(n + Vector((0, 0, 0.6))).normalized()] * 4, "Impostor_" + name)
    return g


def export(name, objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(UNITY, name + ".fbx"), use_selection=True, object_types={"MESH"},
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, apply_scale_options="FBX_SCALE_UNITS",
                             mesh_smooth_type="OFF", colors_type="LINEAR", use_custom_props=False, bake_anim=False, add_leaf_bones=False,
                             path_mode="STRIP")


def measure(obj):
    zs = [(obj.matrix_world @ v.co).z for v in obj.data.vertices]
    xs = [abs((obj.matrix_world @ v.co).x) for v in obj.data.vertices] + [abs((obj.matrix_world @ v.co).y) for v in obj.data.vertices]
    return max(zs), max(xs)


def build(name, makers, kind, collider=None):
    clear_scene()
    objs = []
    for lod, make in enumerate(makers):
        objs.append(make(lod).to_object(f"{name}_LOD{lod}"))
    for o in objs[1:]:
        o.hide_render = True
    h, r = measure(objs[0])
    if kind == "tree":
        size = impostor(name, objs[0], h, 2 * r)
        objs.append(impostor_mesh(name, size).to_object(f"{name}_LOD2"))
        objs[-1].hide_render = True
    if PREVIEW:
        setup_render(420, 420, samples=24)
        extra = light_and_camera(h, 2 * r, False)
        render_to(os.path.join(PREVIEWS, name + ".png"))
        for o in extra:
            bpy.data.objects.remove(o, do_unlink=True)
    for o in objs:
        o.hide_render = False
    export(name, objs)
    tris = {str(i): sum(len(p.vertices) - 2 for p in o.data.polygons) for i, o in enumerate(objs)}
    mats = sorted({m.name for o in objs for m in o.data.materials})
    print(f"{name:22s} h={h:5.2f} r={r:4.2f} tris={tris} mats={mats}")
    return {"nome": name, "altura": round(h, 3), "raio_copa": round(r, 3), "tris": tris, "materiais": mats, "colisor": collider}


def contact_sheet(names):
    tiles = [Image.open(os.path.join(PREVIEWS, n + ".png")).convert("RGBA") for n in names if os.path.exists(os.path.join(PREVIEWS, n + ".png"))]
    cols = 5
    sheet = Image.new("RGBA", (cols * 420, ((len(tiles) + cols - 1) // cols) * 420), (60, 60, 60, 255))
    for i, t in enumerate(tiles):
        bg = Image.new("RGBA", t.size, (140, 140, 140, 255))
        bg.alpha_composite(t)
        sheet.paste(bg, ((i % cols) * 420, (i // cols) * 420))
    sheet.convert("RGB").save(os.path.join(PREVIEWS, "_todas.png"))


# Screen-height fractions for small plants (default Unity transitions would swap a 13 cm tuft under 1 m and cull it
# under 3 m): grass tufts LOD1 at ~7 m and cull at ~28 m, capim ~14 m / ~55 m.
LODS = {**{n: [0.015, 0.004] for n in ("Tufo_Grama_A", "Tufo_Grama_B", "Grama_Tufo_A", "Grama_Tufo_B", "Grama_Tufo_C", "Grama_Tufo_D")},
        **{n: [0.05, 0.012] for n in ("Capim_Alto_A", "Capim_Alto_B")}}


def plants():
    """(name, makers, kind, collider, wind) for the whole kit."""
    out = []
    for name, P in TREES.items():
        out.append((name, [lambda l, n=name, p=P: tree(n, p, l)] * 2, "tree",
                    {"tipo": "capsula", "raio": round(P["r0"] * 1.1, 3), "altura": round(P["fork"] + 0.3, 2)}, None))
    for name, P in SHRUBS.items():
        out.append((name, [lambda l, n=name, p=P: shrub(n, p, l)] * 2, "shrub", None, WIND["arbusto"]))
    for name, seed in (("Bananeira_A", 51), ("Bananeira_B", 52)):
        out.append((name, [lambda l, n=name, s=seed: banana(n, s, l)] * 2, "plant", {"tipo": "capsula", "raio": 0.14, "altura": 1.6},
                    WIND["banana"]))
    for name, seed, kind in (("Capim_Alto_A", 61, "capim"), ("Capim_Alto_B", 62, "capim"), ("Tufo_Grama_A", 63, "grama"),
                             ("Tufo_Grama_B", 64, "grama"), ("Grama_Tufo_A", 101, "grama"), ("Grama_Tufo_B", 102, "grama"),
                             ("Grama_Tufo_C", 103, "grama"), ("Grama_Tufo_D", 104, "grama")):
        out.append((name, [lambda l, n=name, s=seed, k=kind: blade_tuft(n, s, l, k)] * 2, "plant", None, WIND[kind]))
    for name, seed in (("Erva_Daninha_A", 65), ("Erva_Daninha_B", 66)):
        out.append((name, [lambda l, n=name, s=seed: grass(n, s, l, "erva")] * 2, "plant", None, WIND["erva"]))
    out.append(("Espada_Sao_Jorge", [lambda l: sansevieria("Espada_Sao_Jorge", 71, l)] * 2, "plant", None, WIND["espada"]))
    out.append(("Trepadeira_Muro", [lambda l: vine("Trepadeira_Muro", 81, l)] * 2, "plant", None, WIND["trepadeira"]))
    for name, seed in (("Folhas_Secas_A", 91), ("Folhas_Secas_B", 92)):
        out.append((name, [lambda l, n=name, s=seed: dry_leaves(n, s)], "plant", None, WIND["parado"]))
    return out


def main():
    os.makedirs(PREVIEWS, exist_ok=True)
    path = os.path.join(UNITY, "vegetacao.json")
    manifest = {}
    if ONLY and os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            manifest = json.load(f)
    for name, makers, kind, collider, wind in plants():
        if ONLY and name not in ONLY:
            continue
        entry = build(name, makers, kind, collider)
        entry["cor_vertice"] = True
        if wind:
            entry["vento"] = {m: v for m, v in wind.items() if m in entry["materiais"]}
        if name in LODS:
            entry["lods"] = LODS[name]
        manifest[name] = entry
    with open(path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=1)
    if PREVIEW:
        contact_sheet(list(manifest))
    print("vegetation kit:", len(manifest), "plants ->", UNITY)


if __name__ == "__main__":
    main()

"""Branching trees for the Unclaimed vegetation kit (imported by vegetacao.py, runs inside Blender).

Skeleton first, then foliage:
- a tapered trunk to the fork, scaffold limbs, secondary branches and twigs (pipe-model radii), each bending
  towards the light and away from the trunk, limited by an irregular crown envelope (a few lumps, not an ellipsoid);
- foliage cards are sprigs (sibipiruna) or leaf whorls (mango) of 0.4-0.7 m hung along the outer part of every twig and
  at its tip, facing along the twig, so the crown reads as clumps around real branches with gaps between them;
- card normals blend the card's own normal with the direction from its clump centre and the crown centre (soft volume);
- bark UVs use the scan's real tile size (Casca_<species>), so the texel scale is the same on trunk and twigs.

Vertex colour (linear RGBA) is the wind contract read by Unclaimed/Vegetacao:
R = branch flexibility (0 trunk/limbs -> 1 twig tips; leaves inherit their twig), G = phase per limb/twig,
B = leaf flutter (0 wood, 1 leaf), A = main bend (0 below the fork -> 1 at the top, quadratic).
"""
import math
import random

from mathutils import Matrix, Vector


def bezier3(p0, p1, p2, p3, n):
    out = []
    for i in range(n):
        t = i / (n - 1)
        u = 1 - t
        out.append(p0 * u ** 3 + p1 * 3 * u * u * t + p2 * 3 * u * t * t + p3 * t ** 3)
    return out


def perp(rng, axis):
    a = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1)))
    a -= axis * a.dot(axis)
    return a.normalized() if a.length > 1e-4 else axis.orthogonal().normalized()


class Envelope:
    """Irregular crown: an ellipsoid (radius rx, half height rz, centre cz) modulated by a few random lumps."""

    def __init__(self, rng, rx, rz, cz, lumps, flat_top=0.0):
        self.rx, self.rz, self.cz, self.flat = rx, rz, cz, flat_top
        self.lumps = []
        for _ in range(lumps):
            az = rng.uniform(0, 2 * math.pi)
            el = rng.uniform(-0.2, 0.9)
            d = Vector((math.cos(az) * math.cos(el), math.sin(az) * math.cos(el), math.sin(el)))
            self.lumps.append((d, rng.uniform(-0.22, 0.18), rng.uniform(0.5, 0.9)))

    def radius_scale(self, d):
        """Envelope distance multiplier along unit direction d from the crown centre."""
        s = 1.0
        for ld, amp, width in self.lumps:
            c = d.dot(ld)
            if c > 1 - width:
                s += amp * ((c - (1 - width)) / width) ** 2
        return s

    def inside(self, p, margin=1.0):
        q = Vector(((p.x) / self.rx, (p.y) / self.rx, (p.z - self.cz) / self.rz))
        if q.length < 1e-6:
            return True
        lim = self.radius_scale(q.normalized()) * margin
        if self.flat and q.z > 0:
            lim = min(lim, 1.0 - self.flat * q.z * q.z)
        return q.length <= lim

    def clamp(self, start, end, margin=1.0):
        """Shortens start->end so it ends inside the envelope (binary search)."""
        if self.inside(end, margin):
            return end
        lo, hi = 0.0, 1.0
        for _ in range(14):
            mid = (lo + hi) / 2
            if self.inside(start.lerp(end, mid), margin):
                lo = mid
            else:
                hi = mid
        return start.lerp(end, max(lo, 0.25))


class Tree:
    def __init__(self, P, lod):
        self.P, self.lod = P, lod
        self.rng = random.Random(P["seed"])
        self.branches = []     # (path, radii, level, flex0, flex1, phase)
        self.twigs = []        # (path, radius, flex0, flex1, phase, clump_id)
        rx, rz, cz = P["crown"]
        self.env = Envelope(self.rng, rx, rz, cz, P.get("lumps", 6), P.get("flat_top", 0.0))
        self.centre = Vector((0, 0, cz))

    # ------------------------------------------------------------ skeleton

    def grow(self, start, direction, length, radius, level, flex0, phase, clump):
        P, rng = self.P, self.rng
        levels = P["levels"]
        segs = max(3, int(length / (0.35 if level < 2 else 0.5)) + 1)
        if self.lod and level >= 2:
            segs = max(3, segs // 2)
        direction = direction.normalized()
        # Away from the trunk axis; scaffold limbs start at the fork (near the axis), so they use their own heading.
        outward = Vector((start.x, start.y, 0)) if level >= 1 else Vector((direction.x, direction.y, 0))
        outward = outward.normalized() if outward.length > 0.05 else perp(rng, Vector((0, 0, 1)))
        # Tropism: up towards light, out away from the trunk; outer/thin branches droop a little.
        up_bias = P["phototropism"] * (1.0 - 0.25 * level)
        droop = P.get("droop", 0.0) * level / max(1, levels - 1)
        bend_dir = (outward * P["spread"] + Vector((0, 0, up_bias - droop))).normalized()
        wander = perp(rng, direction) * rng.uniform(0.05, 0.2) * length
        p0 = start
        p1 = start + direction * length * 0.33 + wander * 0.5
        p2 = start + (direction * 0.55 + bend_dir * 0.45).normalized() * length * 0.7 + wander
        p3 = start + (direction * 0.45 + bend_dir * 0.55).normalized() * length
        if level >= 1:
            p3 = self.env.clamp(start, p3, 1.0 if level < levels - 1 else 1.05)
            ratio = min(1.0, (p3 - start).length / max(length, 1e-3))
            p1, p2 = start.lerp(p1, ratio), start.lerp(p2, ratio)   # shorten the whole curve: no hooks past the tip
        path = bezier3(p0, p1, p2, p3, segs)
        tip_radius = max(P["min_radius"], radius * 0.3)   # thin ends: no blunt open tubes
        radii = [radius + (tip_radius - radius) * (i / (segs - 1)) ** 0.9 for i in range(segs)]
        flex1 = min(1.0, flex0 + P["flex_step"][min(level, len(P["flex_step"]) - 1)])
        self.branches.append((path, radii, level, flex0, flex1, phase))
        if level == levels - 1:
            self.twigs.append((path, radius, flex0, flex1, phase, clump))
            return
        # Children along the branch (more towards the end), pipe-model radii.
        # Same skeleton for every LOD (LOD1 only drops thin wood and thins the cards), so cross-fades never swap shapes.
        n = rng.randint(*P["children"][level])
        total = (path[-1] - path[0]).length
        # Children spiral round the parent at the golden angle (phyllotaxis) instead of random sides, so siblings
        # spread out and rarely cross each other.
        ref = perp(rng, direction)
        spin = rng.uniform(0, 2 * math.pi)
        for k in range(n):
            t = P["child_t"][level][0] + (P["child_t"][level][1] - P["child_t"][level][0]) * (k + rng.uniform(0.1, 0.9)) / n
            i = min(segs - 2, int(t * (segs - 1)))
            base = path[i].lerp(path[i + 1], t * (segs - 1) - i)
            tangent = (path[i + 1] - path[i]).normalized()
            angle = math.radians(rng.uniform(*P["branch_angle"][level]))
            radial = Matrix.Rotation(spin + k * 2.39996, 3, tangent) @ (ref - tangent * ref.dot(tangent)).normalized()
            axis = tangent.cross(radial).normalized()
            d = (Matrix.Rotation(angle, 3, axis) @ tangent).normalized()
            child_len = total * rng.uniform(*P["length_ratio"][level]) * (1.15 - 0.4 * t)
            child_r = max(P["min_radius"], radii[i] * rng.uniform(0.5, 0.68))
            f0 = flex0 + (flex1 - flex0) * t
            child_phase = phase if level >= 1 else rng.random()
            self.grow(base, d, child_len, child_r, level + 1, f0, (child_phase + rng.uniform(0, 0.05)) % 1.0,
                      clump if level >= 1 else len(self.branches))

    def build_skeleton(self):
        P, rng = self.P, self.rng
        lean = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)) * P.get("lean", 0.12)
        fork = Vector((0, 0, P["fork"])) + lean
        trunk = bezier3(Vector((0, 0, -0.08)), Vector((0, 0, P["fork"] * 0.35)), Vector((0, 0, P["fork"] * 0.7)) + lean * 0.5,
                        fork, 7 if not self.lod else 5)
        r0 = P["r0"]
        radii = [r0 * (1.45 - 0.45 * min(1, i / 1.5)) * (1 - 0.18 * i / 6) for i in range(len(trunk))]
        # The trunk runs on past the fork and closes between the limbs, so no open ring shows at the fork.
        up = (fork - trunk[-2]).normalized()
        trunk += [fork + up * 0.25, fork + up * 0.5]
        radii += [radii[-1] * 0.7, radii[-1] * 0.25]
        self.branches.append((trunk, radii, -1, 0.0, 0.0, 0.0))
        mains = P["mains"]
        for m in range(mains):
            az = 2 * math.pi * (m + rng.uniform(-0.3, 0.3)) / mains
            rise = math.radians(rng.uniform(*P["rise"]))
            d = Vector((math.cos(az) * math.cos(rise), math.sin(az) * math.cos(rise), math.sin(rise)))
            # Limbs start inside the trunk at staggered heights below the fork and leave through its side.
            side = Vector((math.cos(az), math.sin(az), 0)) * radii[-3] * 0.35
            start = fork + side - up * rng.uniform(0.05, 0.45)
            length = P["main_length"] * rng.uniform(0.8, 1.15)
            self.grow(start, d, length, r0 * rng.uniform(0.55, 0.7), 0, 0.0, rng.random(), len(self.branches))
        if P.get("leader"):
            self.grow(fork, Vector((rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.1), 1)), P["main_length"] * 0.9, r0 * 0.6, 0, 0.0,
                      rng.random(), len(self.branches))

    # ------------------------------------------------------------ mesh

    def main_bend(self, z):
        P = self.P
        f = P["fork"] * 0.75
        top = P["crown"][2] + P["crown"][1]
        t = max(0.0, (z - f) / max(top - f, 0.1))
        return min(1.0, t) ** 2

    def wood(self, g, tube_fn, mat, tile):
        lod = self.lod
        for path, radii, level, f0, f1, phase in self.branches:
            if lod and level >= self.P["levels"] - 1 and radii[0] < 0.012:
                continue          # LOD1 keeps leaves on invisible twigs
            sides = {-1: 10, 0: 8, 1: 6, 2: 4, 3: 3}.get(level, 3)
            if lod:
                sides = max(3, sides - 3)
            n = len(path)
            colors = []
            for i, p in enumerate(path):
                t = i / (n - 1)
                colors.append((f0 + (f1 - f0) * t, phase, 0.0, self.main_bend(p.z)))
            tube_fn(g, path, radii, sides, mat=mat, tile=tile, colors=colors)

    def foliage(self, g, card_fn, atlas, cells):
        P, rng, lod = self.P, self.rng, self.lod
        cards = []
        per_m = P["cards_per_m"] * (0.42 if lod else 1.0)
        scale = 1.0 if not lod else 1.45
        for path, radius, f0, f1, phase, clump in self.twigs:
            length = sum((path[i + 1] - path[i]).length for i in range(len(path) - 1))
            count = max(1, int(round(length * per_m * rng.uniform(0.7, 1.3))))
            for k in range(count):
                t = 1.0 if k == 0 else rng.uniform(P["leaf_from"], 1.0)
                i = min(len(path) - 2, int(t * (len(path) - 1)))
                p = path[i].lerp(path[i + 1], t * (len(path) - 1) - i)
                tangent = (path[i + 1] - path[i]).normalized()
                side = perp(rng, tangent)
                if k == 0:
                    up = (tangent + Vector((0, 0, P.get("leaf_rise", 0.3)))).normalized()
                else:
                    up = (tangent * 0.45 + side * 0.75 + Vector((0, 0, P.get("leaf_rise", 0.3) - P.get("leaf_droop", 0.2)))).normalized()
                w = rng.uniform(*P["card"]) * scale
                cards.append((p, up, perp(rng, up), w, f0 + (f1 - f0) * t, phase, clump))
        # Clump centres for soft normals.
        sums = {}
        for p, up, right, w, flex, phase, clump in cards:
            c = p + up * w * 0.5
            s = sums.setdefault(clump, [Vector(), 0])
            s[0] += c
            s[1] += 1
        centres = {k: v[0] / v[1] for k, v in sums.items()}
        for p, up, right, w, flex, phase, clump in cards:
            centre = centres[clump].lerp(self.centre, 0.35)
            # Leaves follow their twig (same flexibility and phase at the attachment point); only B adds flutter.
            col = (min(1.0, flex + 0.04), (phase + rng.uniform(0, 0.02)) % 1.0, rng.uniform(0.7, 1.0), None)
            card_fn(g, p - up * w * 0.08, up, right, w, w, atlas, rng.choice(cells), center=centre,
                    fold=rng.uniform(0.15, 0.32), bend=rng.uniform(0.05, 0.2), lean=0.62, colors=col,
                    bend_fn=self.main_bend)
        return len(cards)


SPECIES = {
    # Sibipiruna (Cenostigma pluviosum): short trunk, 4-6 scaffold limbs spreading wide, flat-topped umbrella crown of
    # fine bipinnate foliage with visible gaps; mottled grey bark shedding in plates.
    "Arvore_Sibipiruna_A": dict(seed=21, fork=2.5, r0=0.22, crown=(4.6, 3.0, 6.0), lumps=7, flat_top=0.35, mains=6,
                                rise=(30, 70), main_length=4.2, leader=True, levels=4, children=[(5, 7), (3, 5), (3, 5)],
                                child_t=[(0.18, 1.0), (0.25, 1.0), (0.25, 1.0)], branch_angle=[(35, 65), (30, 55), (25, 55)],
                                length_ratio=[(0.5, 0.75), (0.45, 0.65), (0.5, 0.7)], min_radius=0.006, phototropism=0.55,
                                spread=0.75, droop=0.25, flex_step=[0.15, 0.3, 0.35, 0.3], cards_per_m=11.0, leaf_from=0.25,
                                card=(0.5, 0.8), leaf_rise=0.25, leaf_droop=0.25, leaf="Folhas_Compostas", bark="Casca_Sibipiruna",
                                tile=1.5),
    "Arvore_Sibipiruna_B": dict(seed=22, fork=2.2, r0=0.2, crown=(4.1, 2.7, 5.5), lumps=7, flat_top=0.35, mains=5,
                                rise=(30, 70), main_length=3.8, leader=True, levels=4, children=[(5, 7), (3, 5), (3, 5)],
                                child_t=[(0.18, 1.0), (0.25, 1.0), (0.25, 1.0)], branch_angle=[(35, 65), (30, 55), (25, 55)],
                                length_ratio=[(0.5, 0.75), (0.45, 0.65), (0.5, 0.7)], min_radius=0.006, phototropism=0.55,
                                spread=0.75, droop=0.25, flex_step=[0.15, 0.3, 0.35, 0.3], cards_per_m=11.0, leaf_from=0.25,
                                card=(0.5, 0.8), leaf_rise=0.25, leaf_droop=0.25, leaf="Folhas_Compostas", bark="Casca_Sibipiruna",
                                tile=1.5),
    # Mango (Mangifera indica): sturdy trunk, many ascending limbs, dense dome; leaves crowded at the branch ends,
    # outer twigs drooping; dark fissured bark.
    "Arvore_Mangueira_A": dict(seed=11, fork=1.9, r0=0.22, crown=(3.8, 3.1, 4.6), lumps=8, mains=7, rise=(35, 70),
                               main_length=3.4, leader=True, levels=4, children=[(5, 7), (3, 5), (3, 5)],
                               child_t=[(0.15, 1.0), (0.2, 1.0), (0.25, 1.0)], branch_angle=[(35, 65), (30, 60), (30, 60)],
                               length_ratio=[(0.5, 0.7), (0.45, 0.65), (0.45, 0.65)], min_radius=0.007, phototropism=0.7,
                               spread=0.55, droop=0.45, flex_step=[0.12, 0.28, 0.35, 0.35], cards_per_m=13.0, leaf_from=0.35,
                               card=(0.5, 0.72), leaf_rise=0.2, leaf_droop=0.35, leaf="Folhas_Largas", bark="Casca_Mangueira",
                               tile=1.0),
    "Arvore_Mangueira_B": dict(seed=12, fork=1.7, r0=0.2, crown=(3.5, 2.8, 4.3), lumps=7, mains=6, rise=(35, 70),
                               main_length=3.1, leader=True, levels=4, children=[(5, 7), (3, 5), (3, 5)],
                               child_t=[(0.15, 1.0), (0.2, 1.0), (0.25, 1.0)], branch_angle=[(35, 65), (30, 60), (30, 60)],
                               length_ratio=[(0.5, 0.7), (0.45, 0.65), (0.45, 0.65)], min_radius=0.007, phototropism=0.7,
                               spread=0.55, droop=0.45, flex_step=[0.12, 0.28, 0.35, 0.35], cards_per_m=13.0, leaf_from=0.35,
                               card=(0.5, 0.72), leaf_rise=0.2, leaf_droop=0.35, leaf="Folhas_Largas", bark="Casca_Mangueira",
                               tile=1.0),
    # Young street tree (a sibipiruna sapling): slender trunk with a leader, light open crown.
    "Arvore_Pequena": dict(seed=31, fork=1.6, r0=0.08, crown=(1.9, 1.8, 3.3), lumps=5, mains=4, rise=(42, 68),
                           main_length=1.9, levels=3, children=[(4, 6), (3, 5)], child_t=[(0.2, 1.0), (0.25, 1.0)],
                           branch_angle=[(28, 50), (28, 55)], length_ratio=[(0.5, 0.7), (0.5, 0.7)], min_radius=0.005,
                           phototropism=0.65, spread=0.6, droop=0.15, flex_step=[0.25, 0.4, 0.35], cards_per_m=10.0,
                           leaf_from=0.2, card=(0.42, 0.62), leaf_rise=0.3, leaf_droop=0.2, leader=True,
                           leaf="Folhas_Compostas", bark="Casca_Sibipiruna", tile=1.5),
}


def build_tree(P, lod, g, tube_fn, card_fn, cells):
    t = Tree(P, lod)
    t.build_skeleton()
    t.wood(g, tube_fn, P["bark"], P["tile"])
    n = t.foliage(g, card_fn, P["leaf"], cells)
    return t, n

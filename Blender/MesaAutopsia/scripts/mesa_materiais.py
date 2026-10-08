# Procedural materials for the autopsy table, used only to bake the Unity textures.
# Everything is in object space (the table's local frame), so the patterns survive any UV layout.
# Each material exposes its channels on reroutes CH_BASE / CH_ROUGH / CH_METAL so the baker can route
# one channel at a time into an emission shader.
import bpy, math

DRAIN = (1.045, 0.0)
BASIN = (1.40, 0.0)


class G:
    def __init__(self, mat):
        mat.use_nodes = True
        self.nt = mat.node_tree
        self.nt.nodes.clear()
        self.x = -1600

    def n(self, kind, **props):
        node = self.nt.nodes.new(kind)
        for k, v in props.items():
            if k.startswith("in_"):
                node.inputs[int(k[3:])].default_value = v
            else:
                setattr(node, k, v)
        node.location = (self.x, 0)
        self.x += 40
        return node

    def link(self, a, b):
        self.nt.links.new(a, b)

    # float helpers -------------------------------------------------------
    def math(self, op, a, b=None, clamp=False):
        m = self.n("ShaderNodeMath", operation=op, use_clamp=clamp)
        for i, v in enumerate((a, b)):
            if v is None:
                continue
            if isinstance(v, (int, float)):
                m.inputs[i].default_value = v
            else:
                self.link(v, m.inputs[i])
        return m.outputs[0]

    def mrange(self, v, a, b, c=0.0, d=1.0, smooth=False):
        m = self.n("ShaderNodeMapRange", interpolation_type="SMOOTHSTEP" if smooth else "LINEAR", clamp=True)
        self.link(v, m.inputs["Value"])
        m.inputs["From Min"].default_value = a
        m.inputs["From Max"].default_value = b
        m.inputs["To Min"].default_value = c
        m.inputs["To Max"].default_value = d
        return m.outputs["Result"]

    def coords(self, scale=(1, 1, 1), offset=(0, 0, 0), rot=(0, 0, 0)):
        if not hasattr(self, "_tc"):
            self._tc = self.n("ShaderNodeTexCoord")
        mp = self.n("ShaderNodeMapping")
        mp.inputs["Location"].default_value = offset
        mp.inputs["Rotation"].default_value = rot
        mp.inputs["Scale"].default_value = scale
        self.link(self._tc.outputs["Object"], mp.inputs["Vector"])
        return mp.outputs["Vector"]

    def obj(self):
        if not hasattr(self, "_tc"):
            self._tc = self.n("ShaderNodeTexCoord")
        return self._tc.outputs["Object"]

    def xyz(self):
        s = self.n("ShaderNodeSeparateXYZ")
        self.link(self.obj(), s.inputs[0])
        return s.outputs

    def noise(self, vec, scale, detail=2.0, rough=0.5, dims="3D", w=0.0):
        t = self.n("ShaderNodeTexNoise", noise_dimensions=dims)
        self.link(vec, t.inputs["Vector"])
        t.inputs["Scale"].default_value = scale
        t.inputs["Detail"].default_value = detail
        t.inputs["Roughness"].default_value = rough
        return t.outputs["Fac"]

    def voronoi(self, vec, scale, feature="F1", rand=1.0):
        t = self.n("ShaderNodeTexVoronoi", feature=feature)
        self.link(vec, t.inputs["Vector"])
        t.inputs["Scale"].default_value = scale
        t.inputs["Randomness"].default_value = rand
        return t.outputs["Distance"]

    def radial(self, cx, cy, r0, r1):
        """1 inside r0, fading to 0 at r1 (horizontal distance from (cx, cy))."""
        x, y, z = self.xyz()
        dx = self.math("SUBTRACT", x, cx)
        dy = self.math("SUBTRACT", y, cy)
        d = self.math("SQRT", self.math("ADD", self.math("MULTIPLY", dx, dx), self.math("MULTIPLY", dy, dy)))
        return self.mrange(d, r0, r1, 1.0, 0.0, smooth=True)

    def color(self, value_socket, rgb_lo, rgb_hi):
        """Lerp between two colours by a float (built from vector math so it works on every version)."""
        f = self.n("ShaderNodeCombineXYZ")
        for i in range(3):
            self.link(value_socket, f.inputs[i])
        lo = self.n("ShaderNodeCombineXYZ", in_0=rgb_lo[0], in_1=rgb_lo[1], in_2=rgb_lo[2])
        hi = self.n("ShaderNodeCombineXYZ", in_0=rgb_hi[0], in_1=rgb_hi[1], in_2=rgb_hi[2])
        diff = self.n("ShaderNodeVectorMath", operation="SUBTRACT")
        self.link(hi.outputs[0], diff.inputs[0]); self.link(lo.outputs[0], diff.inputs[1])
        mul = self.n("ShaderNodeVectorMath", operation="MULTIPLY")
        self.link(diff.outputs[0], mul.inputs[0]); self.link(f.outputs[0], mul.inputs[1])
        add = self.n("ShaderNodeVectorMath", operation="ADD")
        self.link(mul.outputs[0], add.inputs[0]); self.link(lo.outputs[0], add.inputs[1])
        return add.outputs[0]

    def finish(self, base, rough, metal, height, bump_strength=1.0, bump_dist=0.001):
        bsdf = self.n("ShaderNodeBsdfPrincipled")
        out = self.n("ShaderNodeOutputMaterial")
        emit = self.n("ShaderNodeEmission", name="BAKE_EMIT")
        img = self.n("ShaderNodeTexImage", name="BAKE_IMG")
        for nm, sock, target in (("CH_BASE", base, "Base Color"), ("CH_ROUGH", rough, "Roughness"), ("CH_METAL", metal, "Metallic")):
            rr = self.n("NodeReroute", name=nm)
            if isinstance(sock, (int, float)):
                v = self.n("ShaderNodeValue")
                v.outputs[0].default_value = sock
                sock = v.outputs[0]
            self.link(sock, rr.inputs[0])
            self.link(rr.outputs[0], bsdf.inputs[target])
        if height is not None:
            b = self.n("ShaderNodeBump")
            b.inputs["Strength"].default_value = bump_strength
            b.inputs["Distance"].default_value = bump_dist
            self.link(height, b.inputs["Height"])
            self.link(b.outputs["Normal"], bsdf.inputs["Normal"])
        self.link(bsdf.outputs[0], out.inputs["Surface"])
        self.nt.nodes.active = img
        return bsdf


def zone_masks(g):
    """Where the table gets wet: tray floor towards the outlet, basin, under the spout."""
    x, y, z = g.xyz()
    in_tray = g.math("MULTIPLY", g.mrange(z, 0.875, 0.885), g.mrange(z, 0.93, 0.92))          # floor height band
    foot = g.mrange(x, 0.2, 1.1)                                                                # wetter near the outlet
    centre = g.mrange(g.math("ABSOLUTE", y), 0.30, 0.05)
    tray_wet = g.math("MULTIPLY", in_tray, g.math("MULTIPLY", foot, centre))
    basin = g.math("MULTIPLY", g.math("MULTIPLY", g.mrange(x, 1.22, 1.26), g.mrange(x, 1.565, 1.555)),
                   g.math("MULTIPLY", g.mrange(g.math("ABSOLUTE", y), 0.362, 0.352),
                          g.math("MULTIPLY", g.mrange(z, 0.715, 0.725), g.mrange(z, 0.90, 0.86))))
    drain = g.radial(DRAIN[0], DRAIN[1], 0.06, 0.20)
    wet = g.math("MAXIMUM", g.math("MAXIMUM", tray_wet, basin), drain)
    return wet, in_tray


def make_steel():
    mat = bpy.data.materials.get("Aco inox - mesa") or bpy.data.materials.new("Aco inox - mesa")
    g = G(mat)
    tone = g.noise(g.obj(), 1.4, detail=3.0)                                # large, soft tonal drift
    streak = g.noise(g.coords(scale=(2.5, 70.0, 70.0)), 1.0, detail=2.0)    # brushing runs along the length
    smudge = g.mrange(g.noise(g.obj(), 5.0, detail=5.0, rough=0.6), 0.52, 0.72, smooth=True)
    smudge = g.math("MULTIPLY", smudge, g.mrange(g.noise(g.obj(), 1.7, detail=1.0), 0.45, 0.6))
    wet, in_tray = zone_masks(g)
    spots = g.mrange(g.voronoi(g.obj(), 55.0), 0.0, 0.22, 1.0, 0.0, smooth=True)
    spots = g.math("MULTIPLY", spots, g.mrange(g.noise(g.obj(), 9.0, detail=2.0), 0.45, 0.62))
    lime = g.math("MULTIPLY", wet, g.math("MAXIMUM", spots, g.math("MULTIPLY", g.mrange(g.noise(g.obj(), 14.0, 3.0), 0.55, 0.7), 0.5)))
    # Fine brushing and scratches are below the atlas resolution: they come from the tiled detail
    # normal map in Unity. Only broad, soft variation is baked here.

    v = g.math("ADD", 0.55, g.math("MULTIPLY", g.math("SUBTRACT", tone, 0.5), 0.10))
    v = g.math("SUBTRACT", v, g.math("MULTIPLY", smudge, 0.05))
    v = g.math("ADD", v, g.math("MULTIPLY", g.math("SUBTRACT", streak, 0.5), 0.025))
    steel = g.color(v, (0.0, 0.0, 0.0), (1.0, 0.985, 0.96))                  # very slightly warm grey
    limeamt = g.math("MULTIPLY", lime, 0.45)
    base = g.n("ShaderNodeVectorMath", operation="ADD")
    lime_tint = g.color(limeamt, (0, 0, 0), (0.08, 0.08, 0.076))       # neutral mineral film, not dirt
    g.link(steel, base.inputs[0]); g.link(lime_tint, base.inputs[1])

    rough = g.math("ADD", 0.30, g.math("MULTIPLY", g.math("SUBTRACT", streak, 0.5), 0.04))
    rough = g.math("ADD", rough, g.math("MULTIPLY", smudge, 0.14))
    rough = g.math("ADD", rough, g.math("MULTIPLY", lime, 0.22), clamp=True)

    oilcan = g.noise(g.obj(), 2.2, detail=1.0)                               # sheet-metal waviness
    dents = g.mrange(g.voronoi(g.obj(), 9.0), 0.0, 0.05, 1.0, 0.0, smooth=True)
    dents = g.math("MULTIPLY", dents, g.mrange(g.noise(g.obj(), 3.0, 1.0), 0.55, 0.65))
    h = g.math("ADD", g.math("MULTIPLY", oilcan, 0.6), g.math("MULTIPLY", dents, -0.25))
    g.finish(base.outputs[0], rough, 1.0, h, bump_strength=0.18, bump_dist=0.002)
    return mat


def holes_mask(g, cx, cy, pitch, r_hole, r_field):
    """Perforations on a square grid (object XY), limited to a disc of radius r_field."""
    x, y, z = g.xyz()
    def cell(c, origin):
        t = g.math("DIVIDE", g.math("SUBTRACT", c, origin), pitch)
        f = g.math("SUBTRACT", g.math("FRACT", g.math("ADD", t, 0.5)), 0.5)
        return g.math("MULTIPLY", f, pitch)
    dx, dy = cell(x, cx), cell(y, cy)
    d = g.math("SQRT", g.math("ADD", g.math("MULTIPLY", dx, dx), g.math("MULTIPLY", dy, dy)))
    hole = g.mrange(d, r_hole, r_hole - 0.0006, 0.0, 1.0)
    return g.math("MULTIPLY", hole, g.radial(cx, cy, r_field, r_field + 0.0005))


def make_perforated():
    mat = bpy.data.materials.get("Aco inox - ralo") or bpy.data.materials.new("Aco inox - ralo")
    g = G(mat)
    tray = holes_mask(g, DRAIN[0], DRAIN[1], 0.0085, 0.0022, 0.036)
    basin = holes_mask(g, BASIN[0], BASIN[1], 0.0065, 0.0016, 0.026)
    hole = g.math("MAXIMUM", tray, basin)
    keep = g.math("SUBTRACT", 1.0, hole)
    grime = g.mrange(g.noise(g.obj(), 40.0, 3.0), 0.45, 0.7)
    v = g.math("MULTIPLY", g.math("SUBTRACT", 0.50, g.math("MULTIPLY", grime, 0.08)), keep)
    base = g.color(g.math("ADD", v, g.math("MULTIPLY", hole, 0.02)), (0, 0, 0), (1.0, 0.985, 0.96))
    rough = g.math("ADD", g.math("MULTIPLY", keep, g.math("ADD", 0.40, g.math("MULTIPLY", grime, 0.2))), g.math("MULTIPLY", hole, 0.9))
    g.finish(base, rough, keep, g.math("MULTIPLY", keep, 1.0), bump_strength=0.6, bump_dist=0.0015)
    return mat


def make_slot():
    mat = bpy.data.materials.get("Aco inox - ladrao") or bpy.data.materials.new("Aco inox - ladrao")
    g = G(mat)
    x, y, z = g.xyz()
    t = g.math("FRACT", g.math("DIVIDE", g.math("SUBTRACT", z, 0.8575), 0.0085))
    band = g.math("MULTIPLY", g.mrange(t, 0.0, 0.04), g.mrange(t, 0.55, 0.51))
    rows = g.math("MULTIPLY", g.mrange(z, 0.857, 0.858), g.mrange(z, 0.8825, 0.8815))
    span = g.mrange(g.math("ABSOLUTE", y), 0.036, 0.035, 1.0, 0.0)
    slot = g.math("MULTIPLY", band, g.math("MULTIPLY", rows, span))
    keep = g.math("SUBTRACT", 1.0, slot)
    base = g.color(g.math("ADD", g.math("MULTIPLY", keep, 0.50), g.math("MULTIPLY", slot, 0.015)), (0, 0, 0), (1.0, 0.985, 0.96))
    rough = g.math("ADD", g.math("MULTIPLY", keep, 0.42), g.math("MULTIPLY", slot, 0.9))
    g.finish(base, rough, keep, keep, bump_strength=0.6, bump_dist=0.0015)
    return mat


def make_rubber():
    mat = bpy.data.materials.get("Borracha - apoio") or bpy.data.materials.new("Borracha - apoio")
    g = G(mat)
    n1 = g.noise(g.obj(), 60.0, detail=4.0)
    wear = g.mrange(g.noise(g.obj(), 8.0, detail=3.0), 0.5, 0.7)
    v = g.math("ADD", 0.032, g.math("MULTIPLY", wear, 0.018))
    base = g.color(v, (0, 0, 0), (1.0, 0.97, 0.94))
    rough = g.math("ADD", 0.62, g.math("MULTIPLY", g.math("SUBTRACT", n1, 0.5), 0.12))
    rough = g.math("SUBTRACT", rough, g.math("MULTIPLY", wear, 0.12))
    g.finish(base, rough, 0.0, n1, bump_strength=0.12, bump_dist=0.0008)
    return mat


def make_all():
    return {"steel": make_steel(), "perf": make_perforated(), "slot": make_slot(), "rubber": make_rubber()}

"""Print UVs of the storage-room packaging. A box is unfolded into a "net" of rectangles (metres, origin bottom-left);
the second UV layer 'Impressao' maps each face into its rectangle and the painter (impressos_deposito.py) draws the same
rectangles, so print, tape and wear land on the right faces. The bake reads the print through that layer and writes into
the asset's own atlas ('UVMap'), which is the only layer exported.
Face orientation as seen from outside: front -Y (right = +X), back +Y (right = -X), right +X (right = +Y),
left -X (right = -Y), top +Z (front edge at the bottom)."""
from mathutils import Vector

LAYER = 'Impressao'
PAD = 0.012


def net(L, W, H, open_box=False):
    """Rectangles of a box L (x) x W (y) x H (z): name -> (x, y, w, h); and the canvas (w, h). Square canvas."""
    r = {}
    x = PAD
    for name, w in (('esquerda', W), ('frente', L), ('direita', W), ('fundo', L)):
        r[name] = (x, PAD, w, H)
        x += w + PAD
    y = PAD * 2 + H
    if open_box:
        x = PAD
        for name, w in (('i_esquerda', W), ('i_frente', L), ('i_direita', W), ('i_fundo', L)):
            r[name] = (x, y, w, H)
            x += w + PAD
        y += H + PAD
        x = PAD
        for name in ('aba_frente', 'aba_fundo', 'aba_esquerda', 'aba_direita', 'i_base'):
            w, h = {'aba_frente': (L, W / 2), 'aba_fundo': (L, W / 2), 'aba_esquerda': (W, W / 2), 'aba_direita': (W, W / 2),
                    'i_base': (L, W)}[name]
            r[name] = (x, y, w, h)
            x += w + PAD
        top = y + W
    else:
        r['topo'] = (PAD, y, L, W)
        top = y + W
    size = max(x, top + PAD, PAD * 5 + 2 * (L + W))
    return r, size


def _layer(me):
    return (me.uv_layers.get(LAYER) or me.uv_layers.new(name=LAYER)).data


def _side(n, c, centre, inner):
    ax = max(range(3), key=lambda i: abs(n[i]))
    s = 1 if n[ax] > 0 else -1
    if inner is not None and (c - centre).dot(n) < 0:
        # an inner face looks into the box: the wall it belongs to is on the opposite side of its normal; it is read
        # against its normal like any face, so the same (axis, sign) formulas apply
        name = {(0, 1): 'i_esquerda', (0, -1): 'i_direita', (1, 1): 'i_frente', (1, -1): 'i_fundo', (2, 1): 'i_base', (2, -1): 'i_base'}[(ax, s)]
        return name, ax, s
    return {(0, 1): 'direita', (0, -1): 'esquerda', (1, 1): 'fundo', (1, -1): 'frente', (2, 1): 'topo', (2, -1): 'topo'}[(ax, s)], ax, s


def cube_uv(ob, rects, size, L, W, H, z0=0.0, inner=None, dx=0.0, dy=0.0):
    """Maps every face of ob by its dominant normal; inner=True sends faces looking into the box to the i_* rects.
    (dx, dy) is the box centre in the part's frame."""
    me = ob.data
    uv = _layer(me)
    centre = Vector((dx, dy, z0 + H / 2))
    for f in me.polygons:
        name, ax, s = _side(f.normal, f.center, centre, inner)
        if name not in rects:
            name = 'topo' if 'topo' in rects else 'i_base'
        rx, ry, rw, rh = rects[name]
        for li in f.loop_indices:
            p = me.vertices[me.loops[li].vertex_index].co
            x, y, z = p.x - dx, p.y - dy, p.z - z0
            if ax == 2:
                u, v = x + L / 2, y + W / 2
            elif ax == 1:
                u, v = (x + L / 2 if s < 0 else L / 2 - x), z
            else:
                u, v = (y + W / 2 if s > 0 else W / 2 - y), z
            u = min(max(u, 0.0), rw)
            v = min(max(v, 0.0), rh)
            uv[li].uv = ((rx + u) / size, (ry + v) / size)


def planar_uv(ob, rect, size, u0, v0, uaxis=0, vaxis=1, flip_u=False):
    """Projects ob along the remaining axis into one rect (flaps: built closed, projected, then folded open)."""
    me = ob.data
    uv = _layer(me)
    rx, ry, rw, rh = rect
    for f in me.polygons:
        for li in f.loop_indices:
            p = me.vertices[me.loops[li].vertex_index].co
            u, v = p[uaxis] - u0, p[vaxis] - v0
            if flip_u:
                u = rw - u
            uv[li].uv = ((rx + min(max(u, 0), rw)) / size, (ry + min(max(v, 0), rh)) / size)


def blank_uv(ob, size):
    """Parts without print: every corner on the canvas' unused bottom-left padding (painted plain)."""
    uv = _layer(ob.data)
    for lp in uv:
        lp.uv = (PAD * 0.5 / size, PAD * 0.5 / size)


def roll_uv(ob, rects, size, radius, length, cx=0.0, cy=0.0, cz=0.0):
    """Cylinder along X: the curved face unrolled into 'manto' (u = arc length from the top, v = along X); the two ends
    into 'ponta_e' / 'ponta_d' centred on the axis."""
    import math
    me = ob.data
    uv = _layer(me)
    for f in me.polygons:
        end = abs(f.normal.x) > 0.7
        for li in f.loop_indices:
            p = me.vertices[me.loops[li].vertex_index].co
            y, z = p.y - cy, p.z - cz
            if end:
                rx, ry, rw, rh = rects['ponta_d' if f.normal.x > 0 else 'ponta_e']
                s = 1 if f.normal.x > 0 else -1
                u, v = rw / 2 + s * y, rh / 2 + z
            else:
                rx, ry, rw, rh = rects['manto']
                a = math.atan2(y, z) % (2 * math.pi)
                u, v = a * radius, p.x - cx + length / 2
            uv[li].uv = ((rx + min(max(u, 0), rw)) / size, (ry + min(max(v, 0), rh)) / size)
    # the seam: faces straddling a = 0 would stretch over the whole manto; pull their low side over
    for f in me.polygons:
        if abs(f.normal.x) > 0.7:
            continue
        us = [uv[li].uv[0] for li in f.loop_indices]
        if max(us) - min(us) > 0.5 * rects['manto'][2] / size:
            for li in f.loop_indices:
                if uv[li].uv[0] < (rects['manto'][0] + rects['manto'][2] / 2) / size:
                    uv[li].uv = ((rects['manto'][0] + rects['manto'][2]) / size, uv[li].uv[1])


def roll_net(radius, length):
    c = 2 * 3.141592653589793 * radius
    r = {'manto': (PAD, PAD, c, length), 'ponta_e': (PAD, 2 * PAD + length, 2 * radius, 2 * radius),
         'ponta_d': (2 * PAD + 2 * radius, 2 * PAD + length, 2 * radius, 2 * radius)}
    return r, max(c, 4 * radius + PAD, length + 2 * radius + PAD) + 2 * PAD

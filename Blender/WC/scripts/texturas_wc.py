# Bathroom surfaces, drawn with numpy + Pillow (system Python):
#   python3 Blender/WC/scripts/texturas_wc.py
# Output: Assets/UnclaimedPrototype/WC/Texturas. All tile maps are laid out in metres (the map
# generator gives wall and floor finishes world-space UVs), so tiles keep one scale everywhere.
#   WC_Azulejo_*       wall tile 30 x 45 cm (width x height), white glaze, 2 mm grout; 1024 px over 0.9 m
#   WC_Piso_*          floor porcelain 45 x 45 cm, matt grey-beige, 3 mm grout; 1024 px over 1.35 m
#   WC_ForroPVC_*      PVC ceiling boards 10 cm wide, V joints; 512 px over 1 m
#   Decal_RejunteEncardido  darker grout around the toilet foot, 2 x 2 floor tiles (aligned to the grid)
#   WC_Espelho_MS      mirror: faint wipe streaks and dried splashes in the gloss
# Maps: _Base (sRGB), _Normal (tangent space, OpenGL / Unity), _MS (R metallic = 0, A smoothness).
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "..", "Assets", "UnclaimedPrototype", "WC", "Texturas")
RNG = np.random.default_rng(31)


def noise(h, w, scale, octaves=3):
    """Tileable value noise, 0..1 (scale in features per texture side)."""
    out = np.zeros((h, w))
    for o in range(octaves):
        fy, fx = np.fft.fftfreq(h)[:, None] * h, np.fft.fftfreq(w)[None, :] * w
        k = scale * 2 ** o
        spec = np.fft.fft2(RNG.standard_normal((h, w))) * np.exp(-(fx ** 2 + fy ** 2) / (k * k))
        layer = np.real(np.fft.ifft2(spec))
        out += layer / (layer.std() + 1e-9) / 2 ** o
    return (out - out.min()) / (out.max() - out.min())


def save(a, name, rgba=False):
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA" if rgba else "RGB").save(os.path.join(OUT, name))
    return name


def normal_from_height(h, strength):
    gy, gx = np.gradient(h)
    nrm = np.dstack((-gx * strength, gy * strength, np.ones_like(h)))
    nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
    return nrm * 0.5 + 0.5


def tiles(prefix, n, size_m, tile_w, tile_h, grout_mm, base, grout_rgb, gloss, grout_gloss, pillow_mm, shade=0.015,
          stagger=False):
    """Generic tile field: per-tile shade and gloss, rounded (pillowed) tile edges, recessed grout.
    Image row 0 is the top, so tile rows start at the bottom edge (v = 0 = floor level)."""
    px_mm = n / (size_m * 1000.0)
    yy, xx = np.mgrid[0:n, 0:n]
    ym = (n - 1 - yy) / px_mm                                 # mm from the bottom edge
    xm = xx / px_mm
    row = (ym // (tile_h * 1000)).astype(int)
    xs = xm + (row % 2) * tile_w * 500 if stagger else xm
    col = (xs // (tile_w * 1000)).astype(int)
    fx, fy = xs % (tile_w * 1000), ym % (tile_h * 1000)
    edge = np.minimum(np.minimum(fx, tile_w * 1000 - fx), np.minimum(fy, tile_h * 1000 - fy))   # mm to the edge
    grout = np.clip(1.0 - (edge - grout_mm / 2) / 0.5, 0, 1)
    pillow = np.clip(1.0 - (edge - grout_mm / 2) / pillow_mm, 0, 1) ** 2 * (1 - grout)
    ncol, nrow = int(np.ceil(size_m / tile_w)) + 1, int(np.ceil(size_m / tile_h)) + 1
    tshade = RNG.uniform(-shade, shade, (nrow + 1, ncol + 1))[row, col % (ncol + 1)]
    tgloss = RNG.uniform(-0.04, 0.04, (nrow + 1, ncol + 1))[row, col % (ncol + 1)]
    cloud = noise(n, n, 5)
    c = np.array(base)[None, None, :] * (1 + tshade[..., None] + 0.02 * (cloud[..., None] - 0.5))
    g = np.array(grout_rgb)[None, None, :] * (0.9 + 0.15 * noise(n, n, 50, 2)[..., None])
    c = c * (1 - grout[..., None]) + g * grout[..., None]
    c = c * (1 - 0.05 * pillow[..., None])
    save(c, prefix + "_Base.png")
    h = -0.6 * pillow - 1.5 * grout
    save(normal_from_height(h * px_mm, 0.8), prefix + "_Normal.png")
    ms = np.zeros((n, n, 4))
    s = gloss + tgloss - 0.04 * (noise(n, n, 3) - 0.5)
    ms[..., 3] = np.clip(s * (1 - grout) + grout_gloss * grout, 0, 1)
    return save(ms, prefix + "_MS.png", rgba=True)


def pvc_ceiling(n=512, size_m=1.0, board=0.1):
    """White PVC ceiling boards: V-joint every 10 cm, faint extrusion lines along the boards."""
    px = n / size_m
    yy, xx = np.mgrid[0:n, 0:n]
    f = (xx / px) % board
    d = np.minimum(f, board - f) * 1000                    # mm to the joint
    joint = np.clip(1 - d / 2.0, 0, 1)
    lines = np.repeat(noise(1, n, 120, 1), n, axis=0)        # constant along each board (extrusion)
    c = np.ones((n, n, 3)) * np.array([0.93, 0.93, 0.91])[None, None, :] * (1 - 0.25 * joint[..., None]) * (0.985 + 0.015 * lines[..., None])
    save(c, "WC_ForroPVC_Base.png")
    h = -joint * 1.0 + 0.02 * lines
    save(normal_from_height(h * px / 1000 * 4, 6.0), "WC_ForroPVC_Normal.png")
    ms = np.zeros((n, n, 4))
    ms[..., 3] = 0.45 - 0.2 * joint
    return save(ms, "WC_ForroPVC_MS.png", rgba=True)


def grime_decal(n=512, tiles_n=2, grout_mm=3.0, size_m=0.9):
    """Grout darkened around the toilet foot: joints of a 2 x 2 tile patch, strongest at the centre (where
    the foot stands) and fading out; a faint ring of splash marks. The quad must sit on the tile grid."""
    px_mm = n / (size_m * 1000.0)
    yy, xx = np.mgrid[0:n, 0:n]
    t = n / tiles_n
    d = np.minimum(np.minimum(xx % t, t - xx % t), np.minimum(yy % t, t - yy % t)) / px_mm
    joint = np.clip(1 - (d - grout_mm / 2) / 0.6, 0, 1)
    cx, cy = (xx / n - 0.5) * 2, (yy / n - 0.5) * 2
    r = np.sqrt(cx ** 2 + cy ** 2)
    fade = np.clip(1.1 - r, 0, 1) ** 1.5
    blot = noise(n, n, 8)
    rgba = np.zeros((n, n, 4))
    rgba[..., :3] = np.array([0.22, 0.20, 0.16])
    a = joint * fade * (0.55 + 0.45 * blot) + 0.10 * fade * np.clip((blot - 0.6) / 0.2, 0, 1)
    rgba[..., 3] = np.clip(a, 0, 0.85)
    return save(rgba, "Decal_RejunteEncardido.png", rgba=True)


def mirror_ms(n=512):
    """Mirror gloss: nearly perfect, with wipe arcs and a few dried splashes near the bottom edge."""
    yy, xx = np.mgrid[0:n, 0:n]
    s = np.full((n, n), 0.97)
    wipe = noise(n, n, 4)
    arcs = np.sin((np.hypot(xx - n * 0.3, yy - n * 1.4) / n) * 60 + wipe * 6) * 0.5 + 0.5
    s -= 0.03 * arcs * np.clip((wipe - 0.4) / 0.3, 0, 1)
    for _ in range(40):
        cx, cy = RNG.integers(0, n), RNG.integers(int(n * 0.65), n)
        rad = RNG.uniform(2, 6)
        s -= 0.12 * np.exp(-((xx - cx) ** 2 + (yy - cy) ** 2) / (2 * rad * rad))
    ms = np.zeros((n, n, 4))
    ms[..., 0] = 1.0                                       # metallic: it is a mirror
    ms[..., 3] = np.clip(s, 0, 1)
    return save(ms, "WC_Espelho_MS.png", rgba=True)


if __name__ == "__main__":
    print(tiles("WC_Azulejo", 1024, 0.9, 0.30, 0.45, 2.0, (0.93, 0.93, 0.91), (0.78, 0.78, 0.76), 0.78, 0.15, 1.6))
    print(tiles("WC_Piso", 1024, 1.35, 0.45, 0.45, 3.0, (0.55, 0.53, 0.50), (0.32, 0.31, 0.29), 0.34, 0.08, 0.8, shade=0.025))
    print(pvc_ceiling())
    print(grime_decal())
    print(mirror_ms())

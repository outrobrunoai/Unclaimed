# Reception floor and wall surfaces (second pass), drawn with numpy + Pillow (system Python):
#   python3 Blender/Recepcao/scripts/texturas_superficies.py
# Output: Assets/UnclaimedPrototype/Recepcao/Texturas. Colours stay those of the approved materials
# (the base maps are near-white multipliers for the paints and the old porcelain tone for the floor).
#   Recepcao_Porcelanato_*  floor, 2048 px over 2.4 m (4 x 4 tiles of 60 cm, 3 mm grout)
#   Recepcao_Pintura_*      wall paint (wainscot and upper wall), 1024 px over 2 m
#   Decal_RetoquePintura    a touched-up patch of paint
# Maps: _Base (sRGB), _Normal (tangent space, OpenGL / Unity), _MS (R metallic = 0, A smoothness).
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "..", "Assets", "UnclaimedPrototype", "Recepcao", "Texturas")
RNG = np.random.default_rng(23)


def noise(n, scale, octaves=3, rng=RNG, w=None):
    """Tileable value noise (frequency-domain filtered white noise), 0..1."""
    w = w or n
    out = np.zeros((n, w))
    for o in range(octaves):
        fy, fx = np.fft.fftfreq(n)[:, None] * n, np.fft.fftfreq(w)[None, :] * w
        k = scale * 2 ** o
        spec = np.fft.fft2(rng.standard_normal((n, w))) * np.exp(-(fx ** 2 + fy ** 2) / (k * k))
        layer = np.real(np.fft.ifft2(spec))
        out += layer / (layer.std() + 1e-9) / 2 ** o
    return (out - out.min()) / (out.max() - out.min())


def save_rgb(a, name):
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, name))
    return name


def save_rgba(a, name):
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(OUT, name))
    return name


def normal_from_height(h, strength):
    gy, gx = np.gradient(h)
    nrm = np.dstack((-gx * strength, gy * strength, np.ones_like(h)))   # +Y up in the image = OpenGL
    nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
    return nrm * 0.5 + 0.5


def porcelain(n=2048, tiles=4, grout_mm=3.0, size_m=2.4):
    """Polished beige porcelain. Each tile: its own small shade and gloss offset, faint cloudy veining,
    a tiny tilt (lippage) so reflections break from tile to tile, a 1 mm eased edge; recessed grout."""
    t = n // tiles
    px_mm = n / (size_m * 1000.0)
    yy, xx = np.mgrid[0:n, 0:n]
    ty, tx = yy // t, xx // t
    fy, fx = (yy % t) / t, (xx % t) / t                      # position inside the tile, 0..1
    edge = np.minimum(np.minimum(fx, 1 - fx), np.minimum(fy, 1 - fy)) * t / px_mm   # mm to the tile edge
    grout = np.clip(1.0 - (edge - grout_mm / 2) / 0.6, 0, 1)                         # 1 in the joint
    ease = np.clip(1.0 - (edge - grout_mm / 2) / 1.2, 0, 1) * (1 - grout)           # eased edge band

    base = np.array([0.80, 0.745, 0.655])
    shade = RNG.uniform(-0.02, 0.02, (tiles, tiles))[ty, tx]
    gloss = RNG.uniform(-0.035, 0.035, (tiles, tiles))[ty, tx]
    tiltx = RNG.uniform(-1, 1, (tiles, tiles))[ty, tx]
    tilty = RNG.uniform(-1, 1, (tiles, tiles))[ty, tx]
    cloud = noise(n, 6, 4)
    vein = noise(n, 14, 3)
    vein = np.clip(1 - np.abs(vein - 0.5) * 5, 0, 1) ** 1.5   # broad, very faint veining
    speck = noise(n, 400, 1)
    col = base[None, None, :] * (1 + shade[..., None] + 0.035 * (cloud[..., None] - 0.5) + 0.012 * (speck[..., None] - 0.5))
    col = col * (1 - 0.012 * vein[..., None])
    grout_col = np.array([0.50, 0.47, 0.42]) * (0.92 + 0.12 * noise(n, 60, 2)[..., None])
    col = col * (1 - grout[..., None]) + grout_col * grout[..., None]
    col = col * (1 - 0.06 * ease[..., None])
    save_rgb(col, "Recepcao_Porcelanato_Base.png")

    # Height in mm: tile surface 0 with a tiny per-tile tilt, eased edge drops 0.3 mm, joint 1.2 mm down.
    h = 0.04 * (tiltx * (fx - 0.5) + tilty * (fy - 0.5)) - 0.3 * ease - 1.2 * grout
    save_rgb(normal_from_height(h * px_mm, 0.9), "Recepcao_Porcelanato_Normal.png")

    wipe = noise(n, 3, 3)                                       # mop and traffic: broad, faint
    scratches = np.zeros((n, n))
    for _ in range(60):                                         # fine scuffs, a few cm long
        x0, y0 = RNG.integers(0, n, 2)
        ang, ln = RNG.uniform(0, np.pi), RNG.integers(20, 90)
        xs = (x0 + np.cos(ang) * np.arange(ln)).astype(int) % n
        ys = (y0 + np.sin(ang) * np.arange(ln)).astype(int) % n
        scratches[ys, xs] = RNG.uniform(0.3, 1.0)
    smooth = 0.62 + gloss - 0.05 * (wipe - 0.5) - 0.05 * scratches
    smooth = smooth * (1 - grout) + 0.12 * grout
    smooth = smooth * (1 - 0.25 * ease)
    ms = np.zeros((n, n, 4))
    ms[..., 3] = np.clip(smooth, 0, 1)
    return save_rgba(ms, "Recepcao_Porcelanato_MS.png")


def paint(n=1024, size_m=2.0):
    """Washable paint over plaster: a fine roller stipple (orange peel), faint roller laps and the
    odd trowel ripple underneath. Base is a near-white multiplier (the materials keep their colours);
    smoothness is a multiplier map around 1 (wainscot and upper wall set their own gloss)."""
    stipple = noise(n, 160, 2)
    laps = noise(n, 3, 2)
    ripple = noise(n, 12, 3)
    # Same mean tint as the approved paint map (Autopsia_Pintura_Base, ~0.80 / 0.79 / 0.74 sRGB), so the
    # wainscot and upper wall keep their colour; only the fine texture and the gloss map are new.
    tint = np.array([0.797, 0.787, 0.737]) / 0.965
    col = tint[None, None, :] * (0.965 + 0.025 * (laps[..., None] - 0.5) + 0.015 * (stipple[..., None] - 0.5))
    save_rgb(col, "Recepcao_Pintura_Base.png")
    h = 0.06 * stipple + 0.25 * ripple + 0.08 * laps
    save_rgb(normal_from_height(h, 2.2), "Recepcao_Pintura_Normal.png")
    ms = np.zeros((n, n, 4))
    ms[..., 3] = np.clip(0.88 + 0.12 * stipple - 0.06 * (laps - 0.5), 0, 1)
    return save_rgba(ms, "Recepcao_Pintura_MS.png")


def touch_up(w=512, h=384):
    """A rectangle of fresh paint over a scuff: the wall paint's own tone a shade lighter (fresher), roller
    texture, ragged edges, kept faint so it reads as maintenance, not as a patch of another colour."""
    yy, xx = np.mgrid[0:h, 0:w]
    nx, ny = (xx / w - 0.5) * 2, (yy / h - 0.5) * 2
    ragged = noise(512, 18, 3)[:h, :w]
    box = np.clip((1.0 - np.maximum(np.abs(nx) ** 6, np.abs(ny) ** 6) ** (1 / 6) - 0.08 + 0.12 * (ragged - 0.5)) / 0.06, 0, 1)
    rgba = np.zeros((h, w, 4))
    tint = np.array([0.797, 0.787, 0.737]) / 0.965 * 1.035                       # paint() mean, a shade lighter
    rgba[..., :3] = tint * (0.97 + 0.03 * noise(512, 140, 2)[:h, :w, None])
    rgba[..., 3] = box * (0.30 + 0.10 * ragged)
    return save_rgba(rgba, "Decal_RetoquePintura.png")


if __name__ == "__main__":
    import sys
    for name in sys.argv[1:] or ["porcelain", "paint", "touch_up"]:
        print(globals()[name]())

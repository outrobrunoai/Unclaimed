"""Helpers shared by the sign generators (letreiro.py, placas.py): live-text SVG matrices rendered with rsvg-convert,
restrained weathering fields and the extra maps read by UnclaimedExteriorFinishes (<name>_Mascara = URP metallic/
smoothness, <name>_Normal = OpenGL tangent normal). Images are float arrays in 0..1, row 0 at the top."""
from pathlib import Path
import subprocess
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[4]
FONTES = ROOT / 'Blender/Entorno/Acabamentos/fontes'
TEX = ROOT / 'Assets/Modelos/Entorno/Acabamentos/Texturas'


def svg_matrix(name, svg):
    """Write the editable matrix and return its rendering as RGBA floats."""
    FONTES.mkdir(parents=True, exist_ok=True)
    path = FONTES / (name + '.svg')
    path.write_text(svg)
    return render(path)


def render(path_or_svg):
    if isinstance(path_or_svg, Path):
        png = subprocess.check_output(['rsvg-convert', str(path_or_svg)])
    else:
        png = subprocess.check_output(['rsvg-convert'], input=path_or_svg.encode())
    import io
    return np.asarray(Image.open(io.BytesIO(png)).convert('RGBA'), float) / 255


def noise(shape, cells, rng, blur=0):
    """Smooth value noise in -0.5..0.5; cells = (rows, cols) of the random lattice."""
    h, w = shape
    im = Image.fromarray(np.uint8(rng.integers(0, 256, cells))).resize((w, h), Image.Resampling.BICUBIC)
    if blur:
        im = im.filter(ImageFilter.GaussianBlur(blur))
    a = np.asarray(im, float) / 255
    return (a - a.mean()) / (np.ptp(a) + 1e-6)


def streaks(shape, rng, sources, px_per_mm=1.0):
    """Rain run-off: each source (x, y, width_mm, length_mm, strength) gives a narrow, meandering trace that fades
    downwards. Returns intensity 0..1. Width and fibre come from vertically stretched noise, not a drawn stroke."""
    h, w = shape
    yy, xx = np.mgrid[:h, :w].astype(float)
    fibre = noise(shape, (max(3, h // 90), max(8, int(w / (4 * px_per_mm)))), rng, 1) + .5
    out = np.zeros(shape)
    for x0, y0, wid, length, k in sources:
        x0, y0, wid, length = x0 * px_per_mm, y0 * px_per_mm, wid * px_per_mm, length * px_per_mm
        drift = noise((h, 1), (6, 1), rng)[:, 0] * wid * 1.2           # water does not run in a ruler line
        d = np.abs(xx - x0 - drift[:, None]) / (wid * (1 + .5 * (yy - y0).clip(0) / max(length, 1)))
        across = np.exp(-d ** 2 * 2.2)
        t = (yy - y0) / length
        along = np.where(t < 0, 0, np.exp(-t * 2.3) * np.clip(t * 12, 0, 1))
        out = np.maximum(out, across * along * k * (.55 + .9 * fibre))
    return out.clip(0, 1)


def edge_band(shape, rng, side, depth_px, ragged_px):
    """Dirt held along one edge (water line at the bottom, settled dust at the top) with a ragged inner limit."""
    h, w = shape
    yy, xx = np.mgrid[:h, :w].astype(float)
    lim = depth_px + noise((1, w), (1, max(4, w // 40)), rng)[0] * 2 * ragged_px
    dist = (h - 1 - yy) if side == 'bottom' else yy
    return np.clip(1 - dist / np.maximum(lim, 1)[None, :], 0, 1) ** 1.6


def save_rgb(arr, name):
    TEX.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.uint8(np.clip(arr * 255 + .5, 0, 255)), 'RGB').save(TEX / (name + '.png'))


def save_mask(smooth, name, metal=None):
    """URP Lit metallic/smoothness map: R = metallic, A = smoothness (material _Smoothness stays 1)."""
    h, w = smooth.shape
    m = np.zeros((h, w, 4))
    if metal is not None:
        m[..., 0] = metal
    m[..., 3] = smooth
    Image.fromarray(np.uint8(np.clip(m * 255 + .5, 0, 255)), 'RGBA').save(TEX / (name + '_Mascara.png'))


def save_normal(height, name, strength):
    """height in arbitrary units; strength scales slopes. Green = +V (image up), Unity/OpenGL convention."""
    g_row, g_col = np.gradient(height)
    n = np.dstack((-g_col * strength, g_row * strength, np.ones_like(height)))
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    Image.fromarray(np.uint8(np.clip((n * .5 + .5) * 255 + .5, 0, 255)), 'RGB').save(TEX / (name + '_Normal.png'))


def mix(base, color, amount):
    a = np.clip(amount, 0, 1)[..., None]
    return base * (1 - a) + np.asarray(color, float)[None, None, :] / 255 * a

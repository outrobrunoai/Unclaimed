# Bathroom surfaces, drawn with numpy + Pillow (system Python):
#   python3 Blender/WC/scripts/texturas_wc.py
# Output: Assets/UnclaimedPrototype/WC/Texturas. All tile maps are laid out in metres (the map
# generator gives wall and floor finishes world-space UVs), so tiles keep one scale everywhere.
#   WC_Azulejo_*       wall tile 30 x 45 cm (width x height), white glaze, 2 mm grout; 1024 px over 0.9 m
#   WC_Piso_*          floor porcelain 45 x 45 cm, matt grey-beige, 3 mm grout; 1024 px over 1.35 m
#   WC_ForroPVC_*      PVC ceiling boards 10 cm wide, V joints; 512 px over 1 m
#   Decal_RejunteEncardido  darker grout around the toilet foot, 2 x 2 floor tiles (aligned to the grid)
#   WC_Espelho_MS      mirror: faint wipe streaks and dried splashes in the gloss
#   Placa_NaoJogarPapel  small acrylic sign over the toilet ("não jogue papel no vaso", common in Brazil)
#   Controle_Limpeza     cleaning log sheet in its holder by the door (printed grid, handwritten ticks)
#   Agua_Fluxo / Agua_Espuma / Gota   tap water: streaks along the stream, foam ring, splash droplet
# Maps: _Base (sRGB), _Normal (tangent space, OpenGL / Unity), _MS (R metallic = 0, A smoothness).
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

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


FONT, FONT_B = "/usr/share/fonts/noto/NotoSans-Regular.ttf", "/usr/share/fonts/noto/NotoSans-Bold.ttf"


def font(size, bold=False):
    return ImageFont.truetype(FONT_B if bold else FONT, size)


def save_img(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name))
    return name


def placa_papel(w=640, h=384):
    """White acrylic sign, red band and a crossed-out paper roll pictogram, short instruction below."""
    img = Image.new("RGB", (w, h), (244, 244, 240))
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, w, int(h * 0.2)), fill=(178, 34, 34))
    d.text((w / 2, h * 0.1), "ATENÇÃO", font=font(int(h * 0.12), True), fill="white", anchor="mm")
    cx, cy, r = w * 0.2, h * 0.58, h * 0.24
    d.rounded_rectangle((cx - r * 0.55, cy - r * 0.75, cx + r * 0.45, cy + r * 0.75), radius=int(r * 0.25), outline=(40, 40, 40), width=8)
    d.ellipse((cx - r * 0.2, cy - r * 0.2, cx + r * 0.2, cy + r * 0.2), outline=(40, 40, 40), width=6)
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=(178, 34, 34), width=12)
    d.line((cx - r * 0.7, cy + r * 0.7, cx + r * 0.7, cy - r * 0.7), fill=(178, 34, 34), width=12)
    for k, line in enumerate(("FAVOR NÃO JOGAR", "PAPEL NO VASO.", "USE A LIXEIRA.")):
        d.text((w * 0.66, h * (0.40 + 0.17 * k)), line, font=font(int(h * 0.105), k < 2), fill=(35, 35, 35), anchor="mm")
    return save_img(img, "Placa_NaoJogarPapel.png")


def controle_limpeza(w=512, h=720):
    """A4 cleaning log in a clear sleeve: header, a time/date grid with initials and ticks in blue pen."""
    rng = np.random.default_rng(5)
    img = Image.new("RGB", (w, h), (238, 236, 228))
    d = ImageDraw.Draw(img)
    d.text((w / 2, 34), "CONTROLE DE LIMPEZA", font=font(30, True), fill=(30, 30, 30), anchor="mm")
    d.text((w / 2, 66), "SANITÁRIO - RECEPÇÃO", font=font(18), fill=(60, 60, 60), anchor="mm")
    cols = ["DATA", "HORA", "RESP.", "VISTO"]
    x = [30, 140, 250, 380, 482]
    top, row = 92, 30
    rows = (h - top - 40) // row
    for i in range(rows + 1):
        d.line((x[0], top + i * row, x[-1], top + i * row), fill=(90, 90, 90), width=2)
    for xx in x:
        d.line((xx, top, xx, top + rows * row), fill=(90, 90, 90), width=2)
    for k, c in enumerate(cols):
        d.text(((x[k] + x[k + 1]) / 2, top + row / 2), c, font=font(16, True), fill=(30, 30, 30), anchor="mm")
    pen = (35, 55, 140)
    filled = 13
    for i in range(1, filled + 1):
        y = top + i * row + row / 2
        day = 3 + (i - 1) // 3
        hora = ("07:30", "12:15", "17:40")[(i - 1) % 3]
        jit = lambda: rng.uniform(-3, 3)
        d.text(((x[0] + x[1]) / 2 + jit(), y + jit()), f"{day:02d}/10", font=font(17), fill=pen, anchor="mm")
        d.text(((x[1] + x[2]) / 2 + jit(), y + jit()), hora, font=font(17), fill=pen, anchor="mm")
        d.text(((x[2] + x[3]) / 2 + jit(), y + jit()), ("M.S.", "J.R.", "A.P.")[i % 3], font=font(17), fill=pen, anchor="mm")
        cx, cy = (x[3] + x[4]) / 2 + jit(), y + jit()
        d.line((cx - 10, cy, cx - 2, cy + 8, cx + 14, cy - 10), fill=pen, width=3)
    img = img.filter(ImageFilter.GaussianBlur(0.6))
    return save_img(img, "Controle_Limpeza.png")


def agua_fluxo(w=64, h=256):
    """Stream texture: bright, mostly clear, with streaks along v (the flow) and soft bands; alpha carries it."""
    rng = np.random.default_rng(9)
    yy, xx = np.mgrid[0:h, 0:w]
    streak = np.zeros((h, w))
    for _ in range(14):
        x0, width, amp = rng.uniform(0, w), rng.uniform(1.0, 4.0), rng.uniform(0.3, 1.0)
        dx = np.minimum(np.abs(xx - x0), w - np.abs(xx - x0))
        streak += amp * np.exp(-(dx / width) ** 2) * (0.6 + 0.4 * np.sin(yy / h * 2 * np.pi * rng.integers(1, 4) + rng.uniform(0, 6)))
    bands = 0.5 + 0.5 * np.sin(yy / h * 2 * np.pi * 6 + np.sin(xx / w * 2 * np.pi) * 0.8)
    a = np.clip(0.18 + 0.32 * streak / streak.max() + 0.12 * bands, 0, 1)
    rgba = np.zeros((h, w, 4))
    rgba[..., :3] = np.array([0.86, 0.93, 0.97])[None, None, :] * (0.85 + 0.15 * bands[..., None])
    rgba[..., 3] = a
    return save(rgba, "Agua_Fluxo.png", rgba=True)


def agua_espuma(n=256):
    """Foam ring where the stream hits the basin: broken white ring with bubbles, transparent centre and edge."""
    rng = np.random.default_rng(12)
    yy, xx = np.mgrid[0:n, 0:n]
    cx = cy = (n - 1) / 2
    r = np.hypot(xx - cx, yy - cy) / (n / 2)
    ang = np.arctan2(yy - cy, xx - cx)
    ring = np.exp(-((r - 0.38) / 0.16) ** 2) * (0.6 + 0.4 * np.sin(ang * 7 + rng.uniform(0, 6)))
    bub = np.zeros((n, n))
    for _ in range(90):
        bx, by, br = rng.uniform(0.15, 0.85) * n, rng.uniform(0.15, 0.85) * n, rng.uniform(1.5, 4.5)
        d = np.hypot(xx - bx, yy - by)
        bub = np.maximum(bub, np.clip(1 - np.abs(d - br) / 1.2, 0, 1) * (r < 0.8))
    a = np.clip(ring * 0.8 + bub * 0.6, 0, 1) * np.clip((0.95 - r) / 0.2, 0, 1)
    rgba = np.zeros((n, n, 4))
    rgba[..., :3] = 0.95
    rgba[..., 3] = a
    return save(rgba, "Agua_Espuma.png", rgba=True)


def gota(n=32):
    """Soft droplet for the splash particles."""
    yy, xx = np.mgrid[0:n, 0:n]
    r = np.hypot(xx - (n - 1) / 2, yy - (n - 1) / 2) / (n / 2)
    rgba = np.zeros((n, n, 4))
    rgba[..., :3] = 0.92
    rgba[..., 3] = np.clip(1 - r, 0, 1) ** 1.5
    return save(rgba, "Gota.png", rgba=True)


if __name__ == "__main__" and len(__import__("sys").argv) > 1:
    for name in __import__("sys").argv[1:]:
        print(globals()[name]())
elif __name__ == "__main__":
    print(tiles("WC_Azulejo", 1024, 0.9, 0.30, 0.45, 2.0, (0.93, 0.93, 0.91), (0.78, 0.78, 0.76), 0.78, 0.15, 1.6))
    print(tiles("WC_Piso", 1024, 1.35, 0.45, 0.45, 3.0, (0.55, 0.53, 0.50), (0.32, 0.31, 0.29), 0.34, 0.08, 0.8, shade=0.025))
    print(pvc_ceiling())
    print(grime_decal())
    print(mirror_ms())
    for f in (placa_papel, controle_limpeza, agua_fluxo, agua_espuma, gota):
        print(f())

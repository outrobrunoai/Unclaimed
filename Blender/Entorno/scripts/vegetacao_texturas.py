"""Foliage atlases for the Unclaimed neighbourhood vegetation kit (python3 + Pillow + numpy, no Blender).

Leaf photos come from CC0 Poly Haven scans in Blender/Entorno/fontes/folhas (see fontes/LICENCAS.md).
Every atlas is 2048 px RGBA with a cut-out alpha; colour is pushed into the transparent texels
(pull-push fill) so mipmaps never pick up a dark or white halo. Run: python3 vegetacao_texturas.py
The cell layout (CELLS) is read by vegetacao.py to map cards onto the atlases.
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "..", "fontes", "folhas")
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Modelos", "Entorno", "Vegetacao", "Texturas"))
SIZE = 2048

# Atlas cells as (x0, y0, x1, y1) in pixels, top-left origin. Leaf atlases: 2 x 2 twig clusters.
QUAD = [(0, 0, 1024, 1024), (1024, 0, 2048, 1024), (0, 1024, 1024, 2048), (1024, 1024, 2048, 2048)]
CELLS = {
    "Folhas_Largas": {"ramo": QUAD[:3], "moita": [QUAD[3]]},
    "Folhas_Compostas": {"ramo": QUAD},
    "Folhas_Estreitas": {"ramo": QUAD},
    "Plantas_Procedurais": {
        "bananeira": [(0, 0, 2048, 512)],
        "bananeira_seca": [(0, 512, 2048, 1024)],
        "capim": [(0, 1024, 512, 2048)],
        "grama": [(512, 1024, 1024, 1536)],
        "erva": [(512, 1536, 1024, 2048)],
        "espada": [(1024, 1024, 1280, 2048), (1280, 1024, 1536, 2048)],
        "folhas_secas": [(1536, 1024, 2048, 1536)],
        "pseudocaule": [(1536, 1536, 2048, 2048)],
    },
}
# Grass blades (geometry): Grama_Laminas_BaseMap is GRASS_SIZE square, one vertical gradient per column (u = variant,
# v = base -> tip). GRASS_COLUMNS lists the column centres' u per family, so vegetacao.py can pick a colour per blade.
GRASS_SIZE = 256
GRASS_VARIANTS = ["verde", "verde_escuro", "verde_claro", "amarelado", "seco", "verde_medio", "talo", "verde_azulado"]
BARKS = {   # Unity material name -> Poly Haven scan (fontes/polyhaven) and real tile size in metres
    "Casca_Sibipiruna": ("bark_platanus", 1.5, (0.92, 0.94, 0.97), 0.45),   # mottled grey, not tan
    "Casca_Mangueira": ("bark_brown_01", 1.0, (0.72, 0.71, 0.72), 0.35),    # dark grey-brown, fissured
}


# ---------------------------------------------------------------- utilities

def load_rgba(diff, alpha):
    rgb = Image.open(os.path.join(SRC, diff)).convert("RGB")
    a = np.asarray(Image.open(os.path.join(SRC, alpha)))
    if a.ndim == 3:
        a = a[:, :, 0]   # the masks are grey images (the jacaranda one is RGBA with a near-opaque alpha)
    if a.dtype != np.uint8:
        a = (a.astype(np.float32) / a.max() * 255).astype(np.uint8)
    out = np.dstack([np.asarray(rgb), a])
    return Image.fromarray(out, "RGBA")


def components(alpha, scale=8, min_px=40):
    """Bounding boxes of the opaque blobs (4-connected) on a downsampled mask."""
    small = np.asarray(Image.fromarray(alpha).resize((alpha.shape[1] // scale, alpha.shape[0] // scale), Image.BILINEAR)) > 100
    h, w = small.shape
    seen = np.zeros_like(small)
    boxes = []
    for y in range(h):
        for x in range(w):
            if not small[y, x] or seen[y, x]:
                continue
            stack = [(y, x)]
            seen[y, x] = True
            y0 = y1 = y
            x0 = x1 = x
            n = 0
            while stack:
                cy, cx = stack.pop()
                n += 1
                y0, y1, x0, x1 = min(y0, cy), max(y1, cy), min(x0, cx), max(x1, cx)
                for ny, nx in ((cy - 1, cx), (cy + 1, cx), (cy, cx - 1), (cy, cx + 1)):
                    if 0 <= ny < h and 0 <= nx < w and small[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
            if n >= min_px:
                boxes.append((x0 * scale, y0 * scale, (x1 + 1) * scale, (y1 + 1) * scale, n))
    return boxes


def pull_push(img):
    """Fill transparent texels with the colour of the nearest opaque ones (pyramid fill), alpha untouched."""
    arr = np.asarray(img).astype(np.float32) / 255.0
    rgb, a = arr[:, :, :3], arr[:, :, 3:4]
    w = (a > 0.02).astype(np.float32)
    levels = [(rgb * w, w)]
    while levels[-1][1].shape[0] > 4:
        c, m = levels[-1]
        hh, ww = c.shape[0] // 2, c.shape[1] // 2
        c2 = c[:hh * 2, :ww * 2].reshape(hh, 2, ww, 2, 3).sum(axis=(1, 3))
        m2 = m[:hh * 2, :ww * 2].reshape(hh, 2, ww, 2, 1).sum(axis=(1, 3))
        levels.append((c2, m2))
    fill_c, fill_m = levels[-1]
    fill = fill_c / np.maximum(fill_m, 1e-6)
    for c, m in reversed(levels[:-1]):
        up = np.repeat(np.repeat(fill, 2, axis=0), 2, axis=1)[:c.shape[0], :c.shape[1]]
        own = c / np.maximum(m, 1e-6)
        fill = np.where(m > 1e-6, own, up)
    out = np.where(w > 0, rgb, fill)
    return Image.fromarray(np.dstack([out, a]).clip(0, 1).__mul__(255).astype(np.uint8), "RGBA")


def tint(img, factor=1.0, hue=(1.0, 1.0, 1.0), sat=1.0):
    rgb = img.convert("RGB")
    if sat != 1.0:
        rgb = ImageEnhance.Color(rgb).enhance(sat)
    arr = np.asarray(rgb).astype(np.float32) * factor * np.array(hue, np.float32)
    out = Image.fromarray(arr.clip(0, 255).astype(np.uint8), "RGB")
    out.putalpha(img.getchannel("A"))
    return out


def paste_rotated(canvas, leaf, anchor, length, angle_deg, pivot=(0.5, 1.0)):
    """Scale `leaf` to `length` px along its long axis, rotate about its pivot (petiole) and place the pivot at anchor.
    angle 0 = leaf pointing up; positive = clockwise."""
    s = length / max(leaf.size)
    lf = leaf.resize((max(2, int(leaf.width * s)), max(2, int(leaf.height * s))), Image.LANCZOS)
    pad = int(max(lf.size) * 1.5)
    big = Image.new("RGBA", (pad * 2, pad * 2), (0, 0, 0, 0))
    px, py = int(lf.width * pivot[0]), int(lf.height * pivot[1])
    big.alpha_composite(lf, (pad - px, pad - py))
    big = big.rotate(-angle_deg, resample=Image.BICUBIC, center=(pad, pad))
    canvas.alpha_composite(big, (int(anchor[0] - pad), int(anchor[1] - pad)))


def stem(draw, pts, width, color):
    for i in range(len(pts) - 1):
        w = max(1, int(width * (1 - i / len(pts)) + 1))
        draw.line([pts[i], pts[i + 1]], fill=color, width=w)


def curve(p0, p1, p2, n=24):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
             (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]) for t in np.linspace(0, 1, n)]


# ---------------------------------------------------------------- leaf sources

def broad_leaves():
    """Eight single broad leaves (island_tree_02): upright, petiole at the bottom."""
    src = load_rgba("island_tree_02_leaves_diff.png", "island_tree_02_leaves_alpha.png")
    a = np.asarray(src)[:, :, 3]
    leaves = []
    for x0, y0, x1, y1, n in sorted(components(a), key=lambda b: -b[4])[:8]:
        crop = src.crop((max(0, x0 - 8), max(0, y0 - 8), min(src.width, x1 + 8), min(src.height, y1 + 8)))
        crop = crop.crop(crop.getbbox())
        if crop.height > crop.width * 1.3:
            leaves.append(crop)
    return leaves


def compound_leaves():
    """Three compound leaves (jacaranda): turned so the petiole is at the bottom."""
    src = load_rgba("jacaranda_tree_leaves_diff.png", "jacaranda_tree_leaves_alpha.png")
    a = np.asarray(src)[:, :, 3]
    out = []
    for x0, y0, x1, y1, n in sorted(components(a, scale=8, min_px=200), key=lambda b: -b[4])[:3]:
        crop = src.crop((x0, y0, x1, y1))
        crop = crop.crop(crop.getbbox())
        # Rachis runs left -> right with the petiole on the left (or the right for the mirrored one).
        al = np.asarray(crop)[:, :, 3] > 100
        left = al[:, : crop.width // 6].sum()
        right = al[:, -crop.width // 6:].sum()
        crop = crop.rotate(90 if left < right else -90, expand=True)   # thin petiole end goes down
        out.append(crop)
    return out


def narrow_leaves():
    """Narrow leaves from shrub_02: whole, slender blobs only (the scan has a dirty strip on the left)."""
    src = load_rgba("shrub_02_diffuse.png", "shrub_02_alpha.png")
    a = np.asarray(src)[:, :, 3].copy()
    a[:, :200] = 0
    leaves = []
    for x0, y0, x1, y1, n in components(a, scale=4, min_px=60):
        w, h = x1 - x0, y1 - y0
        crop = src.crop((x0, y0, x1, y1))
        px = np.asarray(crop)
        opaque = px[:, :, 3] > 90
        mean = px[:, :, :3][opaque].mean(axis=0) if opaque.any() else (0, 0, 0)
        if h > 2.6 * w and h > 120 and mean[1] > mean[0] * 1.02:   # whole, green (the scan also has dry/dirty ones)
            crop = Image.fromarray(np.dstack([np.asarray(crop)[:, :, :3], np.asarray(crop)[:, :, 3] * (np.asarray(crop)[:, :, 3] > 90)]).astype(np.uint8), "RGBA")
            leaves.append(crop.crop(crop.getbbox()))
    return leaves


# ---------------------------------------------------------------- clusters

def mango_twig(leaves, rng, dense=False):
    """A twig ending in a whorl of leaves (mango-like); back leaves darker. 1024 px cell, twig base at bottom centre."""
    cell = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    d = ImageDraw.Draw(cell)
    base = (512 + rng.uniform(-40, 40), 1010)
    tip = (512 + rng.uniform(-120, 120), 1010 - rng.uniform(380, 470))
    mid = ((base[0] + tip[0]) / 2 + rng.uniform(-90, 90), (base[1] + tip[1]) / 2)
    pts = curve(base, mid, tip)
    stem(d, pts, 16, (88, 74, 52, 255))
    count = rng.randint(22, 28) if dense else rng.randint(12, 16)
    layers = []
    for i in range(count):
        if dense:
            t = rng.uniform(0.15, 1.0)
            p = pts[int(t * (len(pts) - 1))]
            ang = rng.uniform(-150, 150)
            length = rng.uniform(220, 340)
        elif i < count - 4:
            p = tip
            ang = rng.uniform(-115, 115)
            length = rng.uniform(330, 470)
        else:
            t = rng.uniform(0.35, 0.8)
            p = pts[int(t * (len(pts) - 1))]
            ang = rng.choice([-1, 1]) * rng.uniform(40, 95)
            length = rng.uniform(240, 330)
        layers.append((rng.random(), p, ang, length, rng.choice(leaves)))
    layers.sort(key=lambda L: L[0])
    for depth, p, ang, length, leaf in layers:
        shade = 0.55 + 0.45 * depth
        leaf = tint(leaf, shade, (rng.uniform(0.92, 1.05), rng.uniform(0.95, 1.05), rng.uniform(0.85, 1.0)), sat=rng.uniform(0.8, 1.0))
        paste_rotated(cell, leaf, p, length, ang)
    return cell


def frond_twig(leaves, rng):
    """Compound leaves fanning from a twig (sibipiruna-like)."""
    cell = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    d = ImageDraw.Draw(cell)
    base = (512, 1015)
    tip = (512 + rng.uniform(-80, 80), 600)
    pts = curve(base, (512 + rng.uniform(-60, 60), 820), tip)
    stem(d, pts, 12, (92, 80, 58, 255))
    fronds = []
    for i in range(rng.randint(6, 8)):
        t = 0.35 + 0.65 * i / 7
        p = pts[int(t * (len(pts) - 1))]
        ang = (-1) ** i * rng.uniform(25, 80) * (1.2 - t) + rng.uniform(-15, 15)
        fronds.append((rng.random(), p, ang, rng.uniform(470, 600), rng.choice(leaves)))
    fronds.append((1.0, tip, rng.uniform(-10, 10), 560, rng.choice(leaves)))
    fronds.sort(key=lambda f: f[0])
    for depth, p, ang, length, leaf in fronds:
        leaf = tint(leaf, 0.6 + 0.4 * depth, (rng.uniform(0.92, 1.03), 1.0, rng.uniform(0.85, 1.0)), sat=rng.uniform(0.8, 1.0))
        paste_rotated(cell, leaf, p, length, ang)
    return cell


def frond_sprig(leaves, rng, fronds):
    """Sibipiruna sprig: a short twig carrying 2-4 bipinnate leaves (each ~30-45 cm), not a fan of eight.
    Cell is 1024 px for ~0.6 m, base at bottom centre; back leaves darker for depth."""
    cell = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    d = ImageDraw.Draw(cell)
    base = (512 + rng.uniform(-30, 30), 1015)
    tip = (512 + rng.uniform(-140, 140), rng.uniform(560, 700))
    pts = curve(base, (512 + rng.uniform(-60, 60), 860), tip)
    stem(d, pts, 9, (82, 74, 56, 255))
    items = [(1.0, tip, rng.uniform(-12, 12), rng.uniform(520, 640), rng.choice(leaves))]
    for i in range(fronds - 1):
        t = rng.uniform(0.45, 0.85)
        p = pts[int(t * (len(pts) - 1))]
        ang = (-1) ** i * rng.uniform(30, 70) + rng.uniform(-10, 10)
        items.append((rng.uniform(0.2, 0.9), p, ang, rng.uniform(420, 560), rng.choice(leaves)))
    items.sort(key=lambda f: f[0])
    for depth, p, ang, length, leaf in items:
        leaf = tint(leaf, 0.62 + 0.38 * depth, (rng.uniform(0.80, 0.88), rng.uniform(0.97, 1.03), rng.uniform(0.86, 0.96)),
                    sat=rng.uniform(0.75, 0.9))
        paste_rotated(cell, leaf, p, length, ang)
    return cell


def mango_whorl(leaves, rng):
    """Mango twig end: 8-12 long lanceolate leaves (broad scans stretched 1.4x) in a loose whorl, darker and cooler."""
    cell = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    d = ImageDraw.Draw(cell)
    base = (512 + rng.uniform(-30, 30), 1015)
    tip = (512 + rng.uniform(-80, 80), 1015 - rng.uniform(300, 380))
    pts = curve(base, ((base[0] + tip[0]) / 2 + rng.uniform(-50, 50), 840), tip)
    stem(d, pts, 12, (78, 68, 52, 255))
    layers = []
    for i in range(rng.randint(8, 12)):
        ang = rng.uniform(-120, 120)
        layers.append((rng.random(), tip, ang, rng.uniform(380, 520) * (1 - 0.25 * abs(ang) / 120), rng.choice(leaves)))
    for i in range(rng.randint(2, 4)):     # an older leaf or two lower on the twig
        t = rng.uniform(0.3, 0.7)
        layers.append((rng.random() * 0.6, pts[int(t * (len(pts) - 1))], rng.choice([-1, 1]) * rng.uniform(45, 100),
                       rng.uniform(300, 400), rng.choice(leaves)))
    layers.sort(key=lambda L: L[0])
    for depth, p, ang, length, leaf in layers:
        leaf = leaf.resize((leaf.width, int(leaf.height * 1.4)), Image.LANCZOS)
        leaf = tint(leaf, 0.5 + 0.42 * depth, (rng.uniform(0.74, 0.84), rng.uniform(0.93, 1.0), rng.uniform(0.88, 0.98)),
                    sat=rng.uniform(0.75, 0.9))
        paste_rotated(cell, leaf, p, length, ang)
    return cell


def grass_blades():
    """Grama_Laminas_BaseMap: one column per colour family, dark base -> lighter tip, fine lengthwise streaks."""
    rng = np.random.default_rng(5)
    families = {   # base and tip sRGB
        # Kept cool and dark: under the warm garden lamps yellow-green tips turn orange.
        "verde": ((52, 74, 40), (92, 120, 66)), "verde_escuro": ((40, 60, 34), (72, 100, 54)),
        "verde_claro": ((62, 86, 46), (108, 136, 76)), "amarelado": ((72, 80, 48), (118, 124, 78)),
        "seco": ((84, 78, 58), (128, 118, 90)), "verde_medio": ((48, 70, 38), (84, 114, 62)),
        "talo": ((70, 76, 50), (112, 114, 80)), "verde_azulado": ((42, 66, 48), (78, 110, 78)),
    }
    W = GRASS_SIZE
    cw = W // len(GRASS_VARIANTS)
    img = np.zeros((W, W, 3), np.float32)
    v = 1 - (np.arange(W) + 0.5) / W                    # image row 0 = tip (v = 1)
    for i, name in enumerate(GRASS_VARIANTS):
        a, b = (np.array(c, np.float32) for c in families[name])
        t = v[:, None] ** 0.8
        col = a[None, :] * (1 - t) + b[None, :] * t
        streak = 1 + 0.06 * rng.standard_normal(cw)       # lengthwise streaks across the column
        img[:, i * cw:(i + 1) * cw] = col[:, None, :] * streak[None, :, None]
    Image.fromarray(img.clip(0, 255).astype(np.uint8)).save(os.path.join(OUT, "Grama_Laminas_BaseMap.png"))


def species_barks():
    """Casca_<species>: Poly Haven bark scans graded towards each species (night-friendly, not too warm)."""
    src = os.path.join(HERE, "..", "fontes", "polyhaven")
    for name, (scan, tile, gain, sat) in BARKS.items():
        img = Image.open(os.path.join(src, scan + "_diff_2k.png"))
        arr = np.asarray(img).astype(np.float32)
        if arr.max() > 255:
            arr = arr / 257.0
        rgb = Image.fromarray(arr[..., :3].clip(0, 255).astype(np.uint8)).resize((1024, 1024), Image.LANCZOS)
        rgb = ImageEnhance.Color(rgb).enhance(sat)
        out = np.asarray(rgb).astype(np.float32) * np.array(gain)
        Image.fromarray(out.clip(0, 255).astype(np.uint8)).save(os.path.join(OUT, name + "_BaseMap.png"))
        nrm = np.asarray(Image.open(os.path.join(src, scan + "_nor_gl_2k.png"))).astype(np.float32)
        if nrm.max() > 255:
            nrm = nrm / 257.0
        Image.fromarray(nrm[..., :3].clip(0, 255).astype(np.uint8)).resize((1024, 1024), Image.LANCZOS) \
            .save(os.path.join(OUT, name + "_Normal.png"))


def narrow_twig(leaves, rng):
    """A spray of slender leaves along two or three thin stems (murta/podocarpo-like shrubs)."""
    cell = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    d = ImageDraw.Draw(cell)
    for s in range(3):
        base = (512 + rng.uniform(-60, 60), 1015)
        tip = (512 + rng.uniform(-330, 330), rng.uniform(60, 220))
        pts = curve(base, (512 + rng.uniform(-150, 150), 560), tip)
        stem(d, pts, 7, (84, 78, 56, 255))
        items = []
        for i in range(rng.randint(14, 20)):
            t = rng.uniform(0.1, 1.0)
            p = pts[int(t * (len(pts) - 1))]
            ang = math.degrees(math.atan2(tip[0] - base[0], base[1] - tip[1])) + rng.choice([-1, 1]) * rng.uniform(20, 60)
            items.append((rng.random(), p, ang, rng.uniform(160, 240), rng.choice(leaves)))
        items.sort(key=lambda f: f[0])
        for depth, p, ang, length, leaf in items:
            paste_rotated(cell, tint(leaf, 0.6 + 0.4 * depth, (0.95, 1.0, 0.9), sat=0.85), p, length, ang)
    return cell


def leaf_atlas(name, makers):
    atlas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    for (x0, y0, x1, y1), make in zip(QUAD, makers):
        atlas.alpha_composite(make().resize((x1 - x0 - 16, y1 - y0 - 16), Image.LANCZOS), (x0 + 8, y0 + 8))
    pull_push(atlas).save(os.path.join(OUT, name + "_BaseMap.png"))


# ---------------------------------------------------------------- procedural plants

def banana_leaf(w, h, rng, dry=False):
    """Banana leaf lying along x (petiole at the left), drawn at 2x and filtered down."""
    W, H = w * 2, h * 2
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u = xx / W
    v = (yy - H / 2) / (H / 2)
    width = np.clip(np.sin(np.clip((u - 0.06) / 0.94, 0, 1) * math.pi) ** 0.55, 0, 1) * 0.9
    width *= 1 - 0.25 * np.clip((u - 0.8) / 0.2, 0, 1)
    side = np.abs(v)
    inside = side < width
    # Tears run from the edge to the midrib along the lateral veins (slanted toward the tip).
    tears = np.zeros_like(inside)
    for k in range(rng.randint(9 if not dry else 14, 14 if not dry else 22)):
        ut = rng.uniform(0.18, 0.95)
        sgn = rng.choice([-1, 1])
        depth = rng.uniform(0.35, 1.0)
        slope = 0.18
        line_u = ut + slope * (side - 0.05)
        tears |= (np.abs(u - line_u) < 0.0022 + 0.004 * side) & (np.sign(v) == sgn) & (side > 0.05 + (1 - depth) * width)
    inside &= ~tears
    if dry:
        base = np.array([116, 84, 48], np.float32)
        edge = np.array([70, 52, 34], np.float32)
    else:
        base = np.array([70, 106, 44], np.float32)
        edge = np.array([118, 104, 56], np.float32)
    shade = 0.85 + 0.15 * np.cos(side * math.pi * 0.5)
    veins = 0.965 + 0.035 * np.sin((u - 0.22 * side) * W * 0.16)
    col = base[None, None, :] * (shade * veins)[:, :, None]
    burn = np.clip((side - width * 0.86) / (width * 0.14 + 1e-3), 0, 1)
    col = col * (1 - burn[:, :, None]) + edge[None, None, :] * burn[:, :, None]
    midrib = np.abs(v) < 0.025 + 0.02 * (1 - u)
    col[midrib & inside] = [150, 150, 92] if not dry else [150, 112, 66]
    noise = np.asarray(Image.effect_noise((W, H), 18).filter(ImageFilter.GaussianBlur(3))).astype(np.float32) / 128.0
    col *= (0.9 + 0.1 * noise)[:, :, None]
    petiole = (u < 0.07) & (np.abs(v) < 0.05)
    col[petiole] = [96, 120, 62] if not dry else [110, 82, 50]
    alpha = ((inside | petiole) * 255).astype(np.uint8)
    img = Image.fromarray(np.dstack([col.clip(0, 255).astype(np.uint8), alpha]), "RGBA")
    return img.resize((w, h), Image.LANCZOS)


def grass_tuft(w, h, rng, blades, tall):
    img = Image.new("RGBA", (w * 2, h * 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    W, H = w * 2, h * 2
    for i in range(blades):
        bx = W / 2 + rng.gauss(0, W * 0.09)
        lean = rng.gauss(0, W * 0.22)
        top = H * rng.uniform(0.05 if tall else 0.15, 0.55)
        tip = (bx + lean, top)
        ctrl = (bx + lean * 0.2, (H + top) / 2)
        pts = curve((bx, H), ctrl, tip, 14)
        wb = rng.uniform(W * 0.012, W * 0.022)
        g = rng.uniform(0.75, 1.1)
        straw = rng.random() < (0.35 if tall else 0.15)
        left, right = [], []
        for k, (x, y) in enumerate(pts):
            t = k / (len(pts) - 1)
            ww = wb * (1 - t) ** 0.8
            left.append((x - ww, y))
            right.append((x + ww, y))
        c0 = (int(52 * g), int(84 * g), int(34 * g))
        c1 = (int(150 * g), int(140 * g), int(80 * g)) if straw else (int(98 * g), int(128 * g), int(58 * g))
        # two-tone blade: dark base half, lighter tip half
        n = len(pts) // 2
        d.polygon(left[: n + 1] + right[: n + 1][::-1], fill=c0 + (255,))
        d.polygon(left[n:] + right[n:][::-1], fill=c1 + (255,))
    return img.resize((w, h), Image.LANCZOS)


def sansevieria_blade(w, h, rng):
    W, H = w * 2, h * 2
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    t = 1 - yy / H                                   # 0 at the base, 1 at the tip
    half = (0.42 * np.sin(np.clip(t, 0, 1) * math.pi * 0.92 + 0.12)) * (1 - 0.65 * t ** 3)
    x = (xx - W / 2) / (W / 2)
    inside = np.abs(x) < half
    band = np.sin(t * 70 + np.sin(x * 9 + t * 13) * 1.4 + rng.uniform(0, 6))
    dark = np.array([34, 64, 40], np.float32)
    light = np.array([92, 128, 80], np.float32)
    col = dark[None, None] + (light - dark)[None, None] * ((band > 0.35) * 0.8 + 0.2 * (band + 1) / 2)[:, :, None]
    margin = np.abs(x) > half - 0.06
    col[margin & inside] = [190, 168, 82]
    img = Image.fromarray(np.dstack([col.clip(0, 255).astype(np.uint8), (inside * 255).astype(np.uint8)]), "RGBA")
    return img.resize((w, h), Image.LANCZOS)


def scatter(size, leaves, rng, count, tone, ground=False):
    """Leaves spread over a square (weeds: rosettes; ground: fallen and dried)."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    for i in range(count):
        leaf = rng.choice(leaves)
        f = rng.uniform(0.55, 1.0)
        hue = tone(rng)
        if ground:
            p = (rng.uniform(0.1, 0.9) * size, rng.uniform(0.1, 0.9) * size)
            paste_rotated(img, tint(leaf, f, hue, sat=0.6), p, rng.uniform(0.12, 0.2) * size, rng.uniform(0, 360), pivot=(0.5, 0.5))
        else:
            p = (size / 2 + rng.gauss(0, size * 0.04), size * 0.92)
            paste_rotated(img, tint(leaf, f, hue, sat=0.85), p, rng.uniform(0.3, 0.45) * size, rng.uniform(-85, 85))
    return img


def pseudostem(w, h, rng):
    """Banana pseudo-stem: green-brown fibrous sheaths with dry brown strips (tiles along its length)."""
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    fib = np.sin(xx / w * math.pi * 2 * 18 + np.sin(yy / h * math.pi * 2) * 2) * 0.5 + 0.5
    noise = np.asarray(Image.effect_noise((w, h), 30).filter(ImageFilter.GaussianBlur((6, 1)))).astype(np.float32) / 128
    green = np.array([86, 104, 58], np.float32)
    brown = np.array([110, 86, 56], np.float32)
    dry = (np.sin(xx / w * math.pi * 2 * 3 + 1.3) > 0.55)[:, :, None]
    col = np.where(dry, brown, green)[...] * (0.8 + 0.2 * fib)[:, :, None] * (0.85 + 0.15 * noise)[:, :, None]
    return Image.fromarray(np.dstack([col.clip(0, 255).astype(np.uint8), np.full((h, w), 255, np.uint8)]), "RGBA")


def procedural_atlas(broad, compound):
    rng = random.Random(77)
    atlas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    cells = CELLS["Plantas_Procedurais"]

    def put(key, img, i=0):
        x0, y0, x1, y1 = cells[key][i]
        atlas.alpha_composite(img.resize((x1 - x0 - 12, y1 - y0 - 12), Image.LANCZOS), (x0 + 6, y0 + 6))

    put("bananeira", banana_leaf(2048, 512, rng))
    put("bananeira_seca", banana_leaf(2048, 512, rng, dry=True))
    put("capim", grass_tuft(512, 1024, rng, 70, True))
    put("grama", grass_tuft(512, 512, rng, 60, False))
    put("erva", scatter(512, broad, rng, 16, lambda r: (r.uniform(0.95, 1.1), r.uniform(1.0, 1.15), 0.8)))
    put("espada", sansevieria_blade(256, 1024, rng), 0)
    put("espada", sansevieria_blade(256, 1024, rng), 1)
    put("folhas_secas", scatter(512, broad + compound[:1], rng, 34, lambda r: r.choice([(1.25, 0.85, 0.5), (1.1, 0.8, 0.55), (1.0, 0.95, 0.6), (1.3, 0.95, 0.45)]), ground=True))
    put("pseudocaule", pseudostem(512, 512, rng))
    pull_push(atlas).save(os.path.join(OUT, "Plantas_Procedurais_BaseMap.png"))


def bark():
    """Tileable grey-brown bark (island_tree_02 branches scan), warmed a little for mango/sibipiruna."""
    img = Image.open(os.path.join(SRC, "island_tree_02_branches_diff.png")).convert("RGB").resize((1024, 1024), Image.LANCZOS)
    arr = np.asarray(img).astype(np.float32) * np.array([0.92, 0.86, 0.78]) * 0.85
    Image.fromarray(arr.clip(0, 255).astype(np.uint8)).save(os.path.join(OUT, "Casca_BaseMap.png"))
    Image.open(os.path.join(SRC, "island_tree_02_branches_nor_gl.png")).convert("RGB").resize((1024, 1024), Image.LANCZOS) \
        .save(os.path.join(OUT, "Casca_Normal.png"))


def main():
    os.makedirs(OUT, exist_ok=True)
    broad, compound, narrow = broad_leaves(), compound_leaves(), narrow_leaves()
    print("leaves: broad %d compound %d narrow %d" % (len(broad), len(compound), len(narrow)))
    rng = random.Random(11)
    leaf_atlas("Folhas_Largas", [lambda: mango_whorl(broad, rng), lambda: mango_whorl(broad, rng),
                                 lambda: mango_whorl(broad, rng), lambda: mango_twig(broad, rng, dense=True)])
    rng = random.Random(23)
    leaf_atlas("Folhas_Compostas", [lambda: frond_sprig(compound, rng, 3), lambda: frond_sprig(compound, rng, 2),
                                    lambda: frond_sprig(compound, rng, 4), lambda: frond_sprig(compound, rng, 3)])
    rng = random.Random(31)
    leaf_atlas("Folhas_Estreitas", [lambda: narrow_twig(narrow, rng)] * 4)
    procedural_atlas(broad, compound)
    bark()
    species_barks()
    grass_blades()
    print("atlases written to", OUT)


if __name__ == "__main__":
    main()

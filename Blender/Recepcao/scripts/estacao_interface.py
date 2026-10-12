# Visual identity of the reception workstation ("Ipê Desktop", a fictional conservative institutional Linux
# desktop), drawn with Pillow (system Python):
#   python3 Blender/Recepcao/scripts/estacao_interface.py
# Output: Assets/UnclaimedPrototype/Computador/Interface/ — wallpaper, app and file-type icons (64 and 24 px),
# start-button logo and window-button glyphs. Everything is drawn at 4x and downsampled for clean edges.
# Colours are shared with Assets/Scripts/ComputadorRecepcao.Tema.cs (keep in step).
import math
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "..", "Assets", "UnclaimedPrototype", "Computador", "Interface")
SS = 4   # supersampling

ACCENT = (58, 110, 165)
ACCENT_D = (38, 78, 122)
OUTLINE = (60, 72, 88)


def canvas(n):
    return Image.new("RGBA", (n * SS, n * SS), (0, 0, 0, 0))


def finish(im, n, name):
    im = im.resize((n, n), Image.LANCZOS)
    im.save(os.path.join(OUT, f"{name}_{n}.png"))


def rr(d, box, r, fill, outline=None, w=0):
    d.rounded_rectangle([v * SS for v in box], radius=r * SS, fill=fill, outline=outline, width=int(w * SS))


def shadow(im, box, r, alpha=70, blur=3, dy=1.5):
    sh = Image.new("RGBA", im.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle([box[0] * SS, (box[1] + dy) * SS, box[2] * SS, (box[3] + dy) * SS],
                                         radius=r * SS, fill=(0, 0, 0, alpha))
    sh = sh.filter(ImageFilter.GaussianBlur(blur * SS))
    return Image.alpha_composite(im, sh)


def vgrad(size, top, bottom):
    w, h = size
    t = np.linspace(0, 1, h)[:, None, None]
    a = np.array(top, float)[None, None, :] * (1 - t) + np.array(bottom, float)[None, None, :] * t
    return Image.fromarray(np.repeat(a, w, axis=1).astype(np.uint8), "RGBA")


def paste_grad(im, box, r, top, bottom):
    """Rounded box filled with a vertical gradient (box in icon units)."""
    x0, y0, x1, y1 = [int(v * SS) for v in box]
    g = vgrad((x1 - x0, y1 - y0), top + (255,), bottom + (255,))
    m = Image.new("L", g.size, 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, g.size[0] - 1, g.size[1] - 1], radius=r * SS, fill=255)
    im.paste(g, (x0, y0), m)


# ------------------------------------------------------------------ icons (64-unit grid)

def page(im, fill_top=(255, 255, 255), fill_bot=(236, 239, 243), fold=True):
    im = shadow(im, (14, 6, 50, 60), 3)
    paste_grad(im, (14, 6, 50, 60), 3, fill_top, fill_bot)
    d = ImageDraw.Draw(im)
    rr(d, (14, 6, 50, 60), 3, None, OUTLINE + (170,), 1)
    if fold:
        d.polygon([(40 * SS, 6 * SS), (50 * SS, 16 * SS), (40 * SS, 16 * SS)], fill=(214, 220, 228, 255), outline=OUTLINE + (170,))
    return im, d


def icon_documento(n):
    im = canvas(64)
    im, d = page(im)
    for k, w in enumerate((22, 26, 26, 18, 26, 22)):
        y = 22 + k * 5.5
        rr(d, (19, y, 19 + w, y + 2.2), 1, ACCENT + (200,) if k == 0 else (130, 142, 158, 200))
    finish(im, n, "doc")


def icon_planilha(n):
    im = canvas(64)
    im, d = page(im)
    x0, y0, x1, y1 = 18, 20, 46, 54
    rr(d, (x0, y0, x1, y0 + 6), 1, (76, 140, 92, 230))
    for k in range(1, 5):
        y = y0 + 6 + k * 7
        d.line([(x0 * SS, y * SS), (x1 * SS, y * SS)], fill=(120, 160, 128, 200), width=SS)
    for x in (27, 36):
        d.line([(x * SS, y0 * SS), (x * SS, y1 * SS)], fill=(120, 160, 128, 200), width=SS)
    d.rectangle([x0 * SS, y0 * SS, x1 * SS, y1 * SS], outline=(76, 140, 92, 230), width=SS)
    finish(im, n, "planilha")


def icon_nota(n):
    im = canvas(64)
    im = shadow(im, (12, 8, 52, 60), 3)
    paste_grad(im, (12, 8, 52, 60), 3, (255, 248, 196), (246, 232, 160))
    d = ImageDraw.Draw(im)
    rr(d, (12, 8, 52, 60), 3, None, (150, 130, 70, 190), 1)
    rr(d, (12, 8, 52, 15), 3, (214, 98, 82, 255))
    for k in range(5):
        y = 23 + k * 6.5
        d.line([(17 * SS, y * SS), (47 * SS, y * SS)], fill=(150, 170, 200, 200), width=SS)
    for x in (20, 28, 36, 44):
        d.ellipse([(x - 1.6) * SS, 5.5 * SS, (x + 1.6) * SS, 11 * SS], fill=(120, 126, 136, 255))
    finish(im, n, "nota")


def icon_imagem(n):
    im = canvas(64)
    im = shadow(im, (8, 12, 56, 52), 3)
    paste_grad(im, (8, 12, 56, 52), 3, (255, 255, 255), (232, 236, 240))
    d = ImageDraw.Draw(im)
    rr(d, (8, 12, 56, 52), 3, None, OUTLINE + (170,), 1)
    sky = vgrad((int(42 * SS), int(34 * SS)), (150, 190, 226, 255), (206, 224, 240, 255))
    im.paste(sky, (int(11 * SS), int(15 * SS)))
    d = ImageDraw.Draw(im)
    d.ellipse([40 * SS, 19 * SS, 47 * SS, 26 * SS], fill=(245, 196, 70, 255))
    d.polygon([(11 * SS, 49 * SS), (25 * SS, 30 * SS), (35 * SS, 42 * SS), (41 * SS, 36 * SS), (53 * SS, 49 * SS)], fill=(84, 128, 96, 255))
    finish(im, n, "imagem")


def icon_pasta(n):
    im = canvas(64)
    im = shadow(im, (6, 14, 58, 54), 3)
    d = ImageDraw.Draw(im)
    rr(d, (6, 12, 28, 22), 3, (206, 160, 72, 255))
    paste_grad(im, (6, 17, 58, 54), 3, (238, 196, 104), (214, 164, 70))
    d = ImageDraw.Draw(im)
    rr(d, (6, 17, 58, 54), 3, None, (150, 108, 40, 200), 1)
    d.line([(8 * SS, 21 * SS), (56 * SS, 21 * SS)], fill=(255, 230, 170, 160), width=SS)
    finish(im, n, "pasta")


def icon_correio(n):
    im = canvas(64)
    im = shadow(im, (6, 14, 58, 52), 3)
    paste_grad(im, (6, 14, 58, 52), 3, (250, 251, 252), (226, 231, 237))
    d = ImageDraw.Draw(im)
    rr(d, (6, 14, 58, 52), 3, None, OUTLINE + (180,), 1)
    d.line([(8 * SS, 50 * SS), (26 * SS, 33 * SS)], fill=(150, 160, 174, 220), width=SS)
    d.line([(56 * SS, 50 * SS), (38 * SS, 33 * SS)], fill=(150, 160, 174, 220), width=SS)
    d.polygon([(7 * SS, 16 * SS), (32 * SS, 37 * SS), (57 * SS, 16 * SS)], fill=ACCENT + (255,))
    d.line([(7 * SS, 16 * SS), (32 * SS, 37 * SS), (57 * SS, 16 * SS)], fill=ACCENT_D + (255,), width=SS)
    finish(im, n, "correio")


def icon_agenda(n):
    im = canvas(64)
    im = shadow(im, (8, 10, 56, 56), 4)
    paste_grad(im, (8, 10, 56, 56), 4, (255, 255, 255), (236, 239, 243))
    d = ImageDraw.Draw(im)
    rr(d, (8, 10, 56, 56), 4, None, OUTLINE + (170,), 1)
    rr(d, (8, 10, 56, 22), 4, (196, 74, 64, 255))
    d.rectangle([8 * SS, 18 * SS, 56 * SS, 22 * SS], fill=(196, 74, 64, 255))
    for x in (19, 45):
        rr(d, (x - 2, 6, x + 2, 15), 2, (110, 118, 128, 255))
    for r in range(4):
        for c in range(5):
            x, y = 13 + c * 8, 27 + r * 7
            rr(d, (x, y, x + 5, y + 4), 1, ACCENT + (230,) if (r, c) == (2, 1) else (176, 186, 198, 255))
    finish(im, n, "agenda")


def icon_estacao(n):
    im = canvas(64)
    im = shadow(im, (6, 10, 58, 44), 3)
    d = ImageDraw.Draw(im)
    rr(d, (6, 10, 58, 44), 3, (64, 72, 84, 255))
    scr = vgrad((int(46 * SS), int(28 * SS)), (96, 140, 186, 255), (58, 96, 140, 255))
    im.paste(scr, (int(9 * SS), int(13 * SS)))
    d = ImageDraw.Draw(im)
    d.polygon([(26 * SS, 44 * SS), (38 * SS, 44 * SS), (40 * SS, 52 * SS), (24 * SS, 52 * SS)], fill=(120, 128, 140, 255))
    rr(d, (18, 51, 46, 55), 2, (96, 104, 116, 255))
    finish(im, n, "estacao")


def icon_anexo(n):
    im = canvas(64)
    d = ImageDraw.Draw(im)
    w = 4.5 * SS
    c = (96, 106, 120, 255)
    d.arc([22 * SS, 6 * SS, 42 * SS, 26 * SS], 180, 360, fill=c, width=int(w))
    d.line([(22 * SS, 16 * SS), (22 * SS, 46 * SS)], fill=c, width=int(w))
    d.line([(42 * SS, 16 * SS), (42 * SS, 44 * SS)], fill=c, width=int(w))
    d.arc([22 * SS, 36 * SS, 34 * SS, 56 * SS], 0, 180, fill=c, width=int(w))
    d.line([(34 * SS, 46 * SS), (34 * SS, 20 * SS)], fill=c, width=int(w))
    finish(im, n, "anexo")


def logo(n):
    """Start-button mark: a stylised ipê flower (five rounded petals), the distribution's emblem."""
    im = canvas(64)
    d = ImageDraw.Draw(im)
    cx, cy = 32, 32
    for k in range(5):
        a = math.radians(-90 + k * 72)
        px, py = cx + math.cos(a) * 13, cy + math.sin(a) * 13
        d.ellipse([(px - 11) * SS, (py - 11) * SS, (px + 11) * SS, (py + 11) * SS], fill=(240, 196, 64, 255))
    for k in range(5):
        a = math.radians(-90 + k * 72)
        px, py = cx + math.cos(a) * 13, cy + math.sin(a) * 13
        d.ellipse([(px - 8) * SS, (py - 8) * SS, (px + 8) * SS, (py + 8) * SS], fill=(250, 214, 92, 255))
    d.ellipse([(cx - 6) * SS, (cy - 6) * SS, (cx + 6) * SS, (cy + 6) * SS], fill=(196, 120, 40, 255))
    finish(im, n, "logo")


# ------------------------------------------------------------------ glyphs (white on transparent, tinted in Unity)

def glyph(name, n, draw):
    im = canvas(n)
    draw(ImageDraw.Draw(im), n * SS)
    im.resize((n, n), Image.LANCZOS).save(os.path.join(OUT, f"{name}.png"))


def g_min(d, N):
    d.line([(0.28 * N, 0.66 * N), (0.72 * N, 0.66 * N)], fill=(255, 255, 255, 255), width=int(0.085 * N))


def g_max(d, N):
    d.rectangle([0.28 * N, 0.3 * N, 0.72 * N, 0.7 * N], outline=(255, 255, 255, 255), width=int(0.075 * N))
    d.line([(0.28 * N, 0.34 * N), (0.72 * N, 0.34 * N)], fill=(255, 255, 255, 255), width=int(0.11 * N))


def g_restore(d, N):
    w = int(0.07 * N)
    d.rectangle([0.36 * N, 0.24 * N, 0.74 * N, 0.58 * N], outline=(255, 255, 255, 255), width=w)
    d.rectangle([0.24 * N, 0.40 * N, 0.62 * N, 0.76 * N], fill=(0, 0, 0, 0), outline=(255, 255, 255, 255), width=w)


def g_close(d, N):
    w = int(0.09 * N)
    d.line([(0.3 * N, 0.3 * N), (0.7 * N, 0.7 * N)], fill=(255, 255, 255, 255), width=w)
    d.line([(0.7 * N, 0.3 * N), (0.3 * N, 0.7 * N)], fill=(255, 255, 255, 255), width=w)


def g_back(d, N):
    w = int(0.09 * N)
    d.line([(0.25 * N, 0.5 * N), (0.75 * N, 0.5 * N)], fill=(255, 255, 255, 255), width=w)
    d.line([(0.25 * N, 0.5 * N), (0.47 * N, 0.28 * N)], fill=(255, 255, 255, 255), width=w)
    d.line([(0.25 * N, 0.5 * N), (0.47 * N, 0.72 * N)], fill=(255, 255, 255, 255), width=w)


def g_up(d, N):
    w = int(0.09 * N)
    d.line([(0.5 * N, 0.25 * N), (0.5 * N, 0.75 * N)], fill=(255, 255, 255, 255), width=w)
    d.line([(0.5 * N, 0.25 * N), (0.28 * N, 0.47 * N)], fill=(255, 255, 255, 255), width=w)
    d.line([(0.5 * N, 0.25 * N), (0.72 * N, 0.47 * N)], fill=(255, 255, 255, 255), width=w)


def g_chevron(d, N):
    w = int(0.09 * N)
    d.line([(0.4 * N, 0.28 * N), (0.62 * N, 0.5 * N), (0.4 * N, 0.72 * N)], fill=(255, 255, 255, 255), width=w, joint="curve")


def g_chevron_l(d, N):
    w = int(0.09 * N)
    d.line([(0.6 * N, 0.28 * N), (0.38 * N, 0.5 * N), (0.6 * N, 0.72 * N)], fill=(255, 255, 255, 255), width=w, joint="curve")


def g_power(d, N):
    w = int(0.08 * N)
    d.arc([0.24 * N, 0.26 * N, 0.76 * N, 0.78 * N], -60, 240, fill=(255, 255, 255, 255), width=w)
    d.line([(0.5 * N, 0.18 * N), (0.5 * N, 0.5 * N)], fill=(255, 255, 255, 255), width=w)


# ------------------------------------------------------------------ wallpaper

def wallpaper(w=1920, h=1080):
    """Muted dusk over layered hills with a small flowering ipê on the right: calm, low contrast, readable icons."""
    rng = np.random.default_rng(7)
    t = np.linspace(0, 1, h)[:, None]
    top, mid, bot = np.array([62, 84, 110]), np.array([118, 132, 148]), np.array([150, 146, 140])
    sky = np.where(t < 0.62, top * (1 - t / 0.62) + mid * (t / 0.62), mid * (1 - (t - 0.62) / 0.38) + bot * ((t - 0.62) / 0.38))
    img = np.repeat(sky[:, None, :], w, axis=1).astype(float)
    # Soft glow low on the horizon.
    yy, xx = np.mgrid[0:h, 0:w]
    glow = np.exp(-(((xx - w * 0.62) / (w * 0.45)) ** 2 + ((yy - h * 0.66) / (h * 0.22)) ** 2))
    img += glow[..., None] * np.array([40, 30, 14])
    im = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    d = ImageDraw.Draw(im)

    def hills(base, amp, freq, phase, color):
        xs = np.arange(0, w + 8, 8)
        ys = base + amp * np.sin(xs / w * freq * math.pi * 2 + phase) + amp * 0.45 * np.sin(xs / w * freq * 5.3 + phase * 2)
        d.polygon([(0, h)] + list(zip(xs.tolist(), ys.tolist())) + [(w, h)], fill=color)

    hills(h * 0.70, 22, 1.3, 0.4, (96, 108, 122, 255))
    hills(h * 0.77, 26, 0.9, 2.1, (78, 88, 100, 255))
    hills(h * 0.86, 18, 1.6, 4.0, (60, 68, 78, 255))
    # Small ipê: trunk, branches and a few hundred blossoms, kept faint.
    tree = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    td = ImageDraw.Draw(tree)
    bx, by = int(w * 0.80), int(h * 0.865)
    trunk = (52, 56, 62, 255)
    td.line([(bx, by), (bx + 6, by - 120)], fill=trunk, width=9)
    for ang, ln in ((-62, 90), (-115, 80), (-88, 70), (-40, 60), (-140, 55)):
        a = math.radians(ang)
        td.line([(bx + 6, by - 110), (bx + 6 + math.cos(a) * ln, by - 110 + math.sin(a) * ln)], fill=trunk, width=4)
    for _ in range(1400):
        r = rng.uniform(0, 1) ** 0.7 * 92
        a = rng.uniform(0, 2 * math.pi)
        x = bx + 6 + math.cos(a) * r * 1.3
        y = by - 168 + math.sin(a) * r * 0.55
        s = rng.uniform(1.2, 3.0)
        c = (214 + int(rng.uniform(-12, 16)), 170 + int(rng.uniform(-16, 16)), 70, int(rng.uniform(120, 210)))
        td.ellipse([x - s, y - s, x + s, y + s], fill=c)
    tree = tree.filter(ImageFilter.GaussianBlur(0.8))
    tree.putalpha(tree.getchannel("A").point(lambda v: int(v * 0.62)))
    im = Image.alpha_composite(im, tree)
    noise = (rng.normal(0, 2.2, (h, w, 1))).repeat(3, axis=2)
    arr = np.clip(np.asarray(im.convert("RGB")).astype(float) + noise, 0, 255).astype(np.uint8)
    Image.fromarray(arr, "RGB").save(os.path.join(OUT, "papel_de_parede.png"))


def main():
    os.makedirs(OUT, exist_ok=True)
    for n in (64, 24):
        for f in (icon_documento, icon_planilha, icon_nota, icon_imagem, icon_pasta, icon_correio, icon_agenda, icon_estacao, icon_anexo, logo):
            f(n)
    for name, fn in (("g_min", g_min), ("g_max", g_max), ("g_restore", g_restore), ("g_close", g_close), ("g_back", g_back),
                     ("g_up", g_up), ("g_chevron", g_chevron), ("g_chevron_l", g_chevron_l), ("g_power", g_power)):
        glyph(name, 32, fn)
    wallpaper()
    print("ok", OUT)


if __name__ == "__main__":
    main()

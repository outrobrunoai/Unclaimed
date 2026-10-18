# Notice board of the reception: renders every pinned document of QuadroAvisos.json to a PNG (the paper in
# the world, also read up close in the inspection; papers with torn parts also get a plain back), the cork
# panel with the contact shadows of that layout, pin holes and old staples, the paper relief (a tileable fibre
# detail shared by all papers, and per paper its waviness, crumples and folds), and a preview of the board.
#   python3 Blender/Recepcao/scripts/quadro_avisos.py
# Content (texts, sizes in metres, layout) lives only in the JSON; sizes inside a document are in mm.
import json
import math
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
DIR = os.path.join(ROOT, "Assets", "UnclaimedPrototype", "Recepcao", "QuadroDeAvisos")
DOCS = os.path.join(DIR, "Documentos")
NOTO = "/usr/share/fonts/noto/"
HAND = os.path.join(HERE, "..", "fontes", "Caveat-Variable.ttf")
PX = 6.8            # pixels per millimetre of paper
CORK_PX = 1.75      # pixels per millimetre of cork
RNG = np.random.default_rng(11)

FONTS = {
    ("sans", "r"): "NotoSans-Regular.ttf", ("sans", "b"): "NotoSans-Bold.ttf",
    ("sans", "i"): "NotoSans-Italic.ttf", ("sans", "bi"): "NotoSans-BoldItalic.ttf",
    ("serif", "r"): "NotoSerif-Regular.ttf", ("serif", "b"): "NotoSerif-Bold.ttf",
    ("serif", "i"): "NotoSerif-Italic.ttf", ("serif", "bi"): "NotoSerif-BoldItalic.ttf",
}
_cache = {}


TEXT_SCALE = [1.0]   # per document ("escalaTexto"), applied to every printed size


def font(family, style, mm):
    key = (family, style, round(mm * PX * (TEXT_SCALE[0] if family != "hand" else 1.0)))
    if key not in _cache:
        if family == "hand":
            f = ImageFont.truetype(HAND, key[2])
            try:
                f.set_variation_by_axes([560])
            except Exception:
                pass
            _cache[key] = f
        else:
            _cache[key] = ImageFont.truetype(NOTO + FONTS[(family, style)], key[2])
    return _cache[key]


def hexc(h, a=255):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (a,)


def mm(v):
    return v * PX


# ---------------------------------------------------------------- text layout

def wrap(text, f, width):
    lines = []
    for para in text.split("\n"):
        words, cur = para.split(" "), ""
        for w in words:
            t = (cur + " " + w).strip()
            if f.getlength(t) <= width or not cur:
                cur = t
            else:
                lines.append(cur)
                cur = w
        lines.append(cur)
    return lines


def draw_lines(d, lines, f, x0, y, width, color, align="esquerda", justify=False, leading=1.32):
    lh = f.size * leading
    for i, ln in enumerate(lines):
        if justify and i < len(lines) - 1 and " " in ln:
            words = ln.split(" ")
            gap = (width - sum(f.getlength(w) for w in words)) / (len(words) - 1)
            x = x0
            for w in words:
                d.text((x, y), w, font=f, fill=color)
                x += f.getlength(w) + gap
        else:
            lw = f.getlength(ln)
            x = x0 + (width - lw) / 2 if align == "centro" else x0 + width - lw if align == "direita" else x0
            d.text((x, y), ln, font=f, fill=color)
        y += lh
    return y


class Page:
    def __init__(self, doc):
        self.doc = doc
        w, h = doc["tamanho"]
        self.W, self.H = int(round(w * 1000 * PX)), int(round(h * 1000 * PX))
        self.img = Image.new("RGBA", (self.W, self.H), hexc(doc["papel"]))
        self.d = ImageDraw.Draw(self.img)
        self.m = mm(doc.get("margem", 10))
        self.x0, self.x1 = self.m, self.W - self.m
        self.y = self.m
        self.fam = doc.get("fonte", "sans")
        self.anchors = {}
        self.bottom = self.H - self.m
        self.ink = (28, 28, 30, 255)

    @property
    def cw(self):
        return self.x1 - self.x0

    def style(self, b):
        s = ("b" if b.get("negrito") else "") + ("i" if b.get("italico") else "")
        return s or "r"


def b_cabecalho(p, b):
    f = font("sans", "b", b.get("tamanho", 3.9))
    txt = " ".join(b["texto"])  # letter-spaced
    p.d.text((p.x0, p.y), txt, font=f, fill=(95, 98, 104, 255))
    p.y += f.size * 1.45
    p.d.line((p.x0, p.y, p.x1, p.y), fill=(150, 150, 150, 255), width=max(1, int(mm(0.25))))
    p.y += mm(2.6)


def b_titulo(p, b, sub=False):
    size = b.get("tamanho", 5.0)
    f = font(p.fam, "b", size)
    lines = wrap(b["texto"], f, p.cw)
    color = hexc(b["cor"]) if "cor" in b else p.ink
    align = b.get("alinhamento", "esquerda")
    if "titulo" not in p.anchors and not sub:
        ln = lines[0]
        lw = f.getlength(ln)
        x = p.x0 + (p.cw - lw) / 2 if align == "centro" else p.x0
        letters = [x + f.getlength(ln[:i]) + f.getlength(ln[i]) / 2 for i in range(len(ln))]
        bb = f.getbbox(ln)
        p.anchors["titulo"] = dict(box=(x, p.y + bb[1], x + lw, p.y + bb[3]), letters=letters, size=f.size)
    p.y = draw_lines(p.d, lines, f, p.x0, p.y, p.cw, color, align, leading=1.18)
    p.y += mm(1.6 if sub else 2.4)


def b_faixa(p, b):
    f = font("sans", "b", b.get("tamanho", 5.0))
    lines = wrap(b["texto"], f, p.cw - mm(6))
    pad = mm(2.2)
    h = len(lines) * f.size * 1.18 + pad * 2
    p.d.rectangle((p.x0, p.y, p.x1, p.y + h), fill=hexc(b.get("cor", "#1E3F7A")))
    draw_lines(p.d, lines, f, p.x0, p.y + pad - f.size * 0.06, p.cw, (255, 255, 255, 255), "centro", leading=1.18)
    p.y += h + mm(3)


def b_paragrafo(p, b):
    f = font(p.fam, p.style(b), b.get("tamanho", 3.0))
    lines = wrap(b["texto"], f, p.cw)
    p.y = draw_lines(p.d, lines, f, p.x0, p.y, p.cw, p.ink, b.get("alinhamento", "esquerda"), b.get("justificado", False))
    p.y += mm(1.8)


def b_lista(p, b):
    f = font(p.fam, "r", b.get("tamanho", 3.0))
    fb = font(p.fam, "b", b.get("tamanho", 3.0))
    ind = max(mm(5.2), fb.getlength("6.") + mm(2.0))
    for i, item in enumerate(b["itens"]):
        mark = f"{i + 1}." if b.get("numerada") else "•"
        p.d.text((p.x0 + mm(0.6), p.y), mark, font=fb, fill=p.ink)
        lines = wrap(item, f, p.cw - ind)
        p.y = draw_lines(p.d, lines, f, p.x0 + ind, p.y, p.cw - ind, p.ink)
        p.y += mm(1.0)
    p.y += mm(1.2)


def b_tabela(p, b):
    size = b.get("tamanho", 2.8)
    f, fb = font(p.fam, "r", size), font(p.fam, "b", size)
    widths = [w * p.cw for w in b["larguras"]]
    pad = mm(1.2)
    lw = max(1, int(mm(0.22)))
    grid = (120, 120, 120, 255)

    def row(cells, fnt, fill, idx=None, strike=False):
        wrapped = [wrap(c, fnt, w - 2 * pad) for c, w in zip(cells, widths)]
        h = max(len(x) for x in wrapped) * fnt.size * 1.25 + 2 * pad
        if fill:
            p.d.rectangle((p.x0, p.y, p.x1, p.y + h), fill=fill)
        x = p.x0
        for lines, w in zip(wrapped, widths):
            draw_lines(p.d, lines, fnt, x + pad, p.y + pad, w - 2 * pad, p.ink, leading=1.25)
            p.d.line((x, p.y, x, p.y + h), fill=grid, width=lw)
            x += w
        p.d.line((p.x1, p.y, p.x1, p.y + h), fill=grid, width=lw)
        p.d.line((p.x0, p.y, p.x1, p.y), fill=grid, width=lw)
        if idx is not None:
            p.anchors[f"linha:{idx}"] = dict(box=(p.x0, p.y, p.x1, p.y + h))
        p.y += h

    row(b["colunas"], fb, (226, 226, 222, 255))
    for i, r in enumerate(b["linhas"]):
        row(r["c"], f, None, i, r.get("riscada", False))
        if r.get("riscada"):
            x0, y0, x1, y1 = p.anchors[f"linha:{i}"]["box"]
            pen_line(p.img, x0 + mm(1), (y0 + y1) / 2 + mm(0.4), x1 - mm(1), (y0 + y1) / 2 - mm(0.5), (31, 62, 150, 235), mm(0.45))
    p.d.line((p.x0, p.y, p.x1, p.y), fill=grid, width=lw)
    p.y += mm(3)


def b_destaque(p, b):
    f = font(p.fam, "b", b.get("tamanho", 3.0))
    pad = mm(2.2)
    lines = wrap(b["texto"], f, p.cw - 2 * pad)
    h = len(lines) * f.size * 1.3 + 2 * pad
    paper = hexc(p.doc["papel"])
    tint = tuple(int(c * 0.93) for c in paper[:3]) + (255,)
    p.d.rectangle((p.x0, p.y, p.x1, p.y + h), fill=tint, outline=p.ink, width=max(2, int(mm(0.4))))
    draw_lines(p.d, lines, f, p.x0 + pad, p.y + pad, p.cw - 2 * pad, p.ink, b.get("alinhamento", "esquerda"), leading=1.3)
    p.y += h + mm(2.6)


def b_citacao(p, b):
    f = font("serif", "i", b.get("tamanho", 3.4))
    lines = wrap(b["texto"], f, p.cw - mm(6))
    p.y = draw_lines(p.d, lines, f, p.x0 + mm(3), p.y, p.cw - mm(6), p.ink, "centro")
    p.y += mm(1.2)


def b_assinatura(p, b):
    f = font(p.fam, "r", b.get("tamanho", 2.9))
    y = p.y + mm(1.5)
    first_name = next((i for i, ln in enumerate(b["assinatura"]) if ln.startswith("Dra.") or ln.startswith("H.")), None)
    for i, ln in enumerate(b["assinatura"]):
        if i == first_name and b.get("rubrica"):
            # signature over a short rule, in blue pen
            rx1 = p.x1
            rx0 = rx1 - max(f.getlength(ln), mm(38))
            y += mm(6.5)
            p.d.line((rx0, y - mm(0.6), rx1, y - mm(0.6)), fill=(60, 60, 60, 255), width=max(1, int(mm(0.2))))
            hand(p.img, b["rubrica"], (rx0 + rx1) / 2, y - mm(4.2), 5.6, "#1F3E96", -4)
        if ln:
            lw = f.getlength(ln)
            p.d.text((p.x1 - lw, y), ln, font=f, fill=p.ink)
        y += f.size * 1.3
    p.y = y + mm(2)


def b_rodape(p, b, last=False):
    f = font("sans", "r", b.get("tamanho", 3.5))
    lines = wrap(b["texto"], f, p.cw)
    h = len(lines) * f.size * 1.3
    y = max(p.y, p.bottom - h) if last else p.y
    draw_lines(p.d, lines, f, p.x0, y, p.cw, (100, 100, 104, 255), b.get("alinhamento", "esquerda"), leading=1.3)
    p.y = y + h + (0 if last else mm(1.5))


def b_icone(p, b):
    s = mm(b.get("tamanho", 30))
    cx, cy, r = p.W / 2, p.y + s / 2, s / 2
    d = p.d
    d.rectangle((cx - r * 0.72, cy - r * 0.11, cx + r * 0.42, cy + r * 0.11), fill=(70, 70, 70, 255))
    d.rectangle((cx + r * 0.42, cy - r * 0.11, cx + r * 0.66, cy + r * 0.11), fill=(205, 120, 45, 255))
    for k in range(3):
        d.arc((cx + r * 0.5 + k * r * 0.05, cy - r * 0.62 - k * r * 0.1, cx + r * 0.75 + k * r * 0.05, cy - r * 0.2 - k * r * 0.1),
              200, 340, fill=(150, 150, 150, 255), width=int(mm(0.6)))
    w = int(s * 0.12)
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=(200, 30, 30, 255), width=w)
    a = math.radians(45)
    d.line((cx - math.cos(a) * (r - w / 2), cy - math.sin(a) * (r - w / 2), cx + math.cos(a) * (r - w / 2), cy + math.sin(a) * (r - w / 2)),
           fill=(200, 30, 30, 255), width=w)
    p.y += s + mm(1.5)


def b_foto(p, b):
    """A photocopied snapshot: grey portrait of an elderly woman (shapes only), halftone and toner noise."""
    h = int(p.H * b.get("altura", 0.35))
    w = int(h * 0.8)
    x0 = int((p.W - w) / 2)
    y0 = int(p.y)
    ph = Image.new("L", (w, h), 0)
    g = np.linspace(150, 205, h)[:, None] * np.ones((1, w))
    ph = Image.fromarray(g.astype(np.uint8))
    d = ImageDraw.Draw(ph)
    cx = w / 2
    d.ellipse((cx - w * 0.47, h * 0.66, cx + w * 0.47, h * 1.35), fill=105)                  # blouse
    d.rectangle((cx - w * 0.09, h * 0.52, cx + w * 0.09, h * 0.72), fill=150)                 # neck
    d.ellipse((cx - w * 0.29, h * 0.10, cx + w * 0.29, h * 0.40), fill=212)                    # hair
    d.ellipse((cx - w * 0.12, h * 0.02, cx + w * 0.12, h * 0.17), fill=205)                    # bun
    d.ellipse((cx - w * 0.22, h * 0.20, cx + w * 0.22, h * 0.62), fill=170)                    # face
    d.ellipse((cx - w * 0.24, h * 0.18, cx + w * 0.24, h * 0.34), fill=214)                    # fringe over the face top
    d.ellipse((cx - w * 0.22, h * 0.24, cx + w * 0.22, h * 0.62), fill=170)
    for sx in (-1, 1):                                                                          # glasses
        ex = cx + sx * w * 0.095
        d.ellipse((ex - w * 0.08, h * 0.37, ex + w * 0.08, h * 0.45), outline=60, width=max(2, int(w * 0.012)))
        d.ellipse((ex - w * 0.022, h * 0.40, ex + w * 0.022, h * 0.42), fill=70)
    d.line((cx - w * 0.015, h * 0.41, cx + w * 0.015, h * 0.41), fill=60, width=max(2, int(w * 0.01)))
    d.arc((cx - w * 0.07, h * 0.49, cx + w * 0.07, h * 0.56), 20, 160, fill=90, width=max(2, int(w * 0.01)))   # smile
    d.line((cx, h * 0.43, cx - w * 0.015, h * 0.50), fill=135, width=max(2, int(w * 0.008)))                   # nose
    ph = ph.filter(ImageFilter.GaussianBlur(w * 0.012))
    arr = np.asarray(ph).astype(float)
    # photocopy: crushed contrast, halftone dots, toner speckle
    arr = np.clip((arr - 95) * 1.7, 0, 255)
    yy, xx = np.mgrid[0:h, 0:w]
    cell = max(4, int(mm(0.55)))
    dots = (np.sin(xx * 2 * np.pi / cell) * np.sin(yy * 2 * np.pi / cell)) * 22
    arr = np.clip(arr + dots + RNG.normal(0, 14, arr.shape), 0, 255)
    rgba = Image.fromarray(arr.astype(np.uint8)).convert("RGBA")
    p.img.paste(rgba, (x0, y0))
    p.d.rectangle((x0, y0, x0 + w, y0 + h), outline=(40, 40, 40, 255), width=max(1, int(mm(0.3))))
    p.y += h + mm(2.5)


def b_destacaveis(p, b):
    """Tear-off strips at the bottom of the page; torn ones become transparent (with a ragged edge)."""
    n, h = b["quantidade"], mm(b.get("altura", 45))
    y0 = p.H - h
    f = font("sans", "b", 3.4)
    w = p.W / n
    dash = mm(1.6)
    p.d.line((0, y0, p.W, y0), fill=(110, 110, 110, 255), width=max(1, int(mm(0.2))))
    for i in range(n):
        x = i * w
        if i:
            for yy in np.arange(y0, p.H, dash * 2):
                p.d.line((x, yy, x, min(p.H, yy + dash)), fill=(110, 110, 110, 255), width=max(1, int(mm(0.2))))
        strip = Image.new("RGBA", (int(h), int(w)), (0, 0, 0, 0))
        sd = ImageDraw.Draw(strip)
        tw = f.getlength(b["texto"])
        sd.text(((h - tw) / 2, (w - f.size) / 2 - f.size * 0.15), b["texto"], font=f, fill=p.ink)
        strip = strip.rotate(90, expand=True)
        p.img.alpha_composite(strip, (int(x + (w - strip.width) / 2), int(y0)))
    alpha = p.img.getchannel("A")
    ad = ImageDraw.Draw(alpha)
    for i in b.get("arrancados", []):
        x0, x1 = i * w, (i + 1) * w
        # ragged tear line a little below the cut
        pts = [(x0, p.H), (x0, y0 + mm(2))]
        for k in range(13):
            t = k / 12
            pts.append((x0 + (x1 - x0) * t, y0 + mm(1.2) + RNG.uniform(0, mm(2.2))))
        pts += [(x1, y0 + mm(2)), (x1, p.H)]
        ad.polygon(pts, fill=0)
    p.img.putalpha(alpha)
    p.bottom = y0 - mm(2)


BLOCKS = {
    "cabecalho": b_cabecalho, "titulo": b_titulo, "subtitulo": lambda p, b: b_titulo(p, b, sub=True),
    "faixa": b_faixa, "paragrafo": b_paragrafo, "lista": b_lista, "tabela": b_tabela,
    "destaque": b_destaque, "citacao": b_citacao, "assinatura": b_assinatura, "icone": b_icone,
    "foto": b_foto, "destacaveis": b_destacaveis,
}


# ---------------------------------------------------------------- pen

def hand(img, text, cx, cy, size_mm, color, rot):
    f = font("hand", "r", size_mm)
    tw, th = int(f.getlength(text)) + 20, int(f.size * 1.5)
    layer = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
    ImageDraw.Draw(layer).text((10, f.size * 0.12), text, font=f, fill=hexc(color, 235))
    layer = layer.rotate(rot, expand=True, resample=Image.BICUBIC)
    img.alpha_composite(layer, (int(cx - layer.width / 2), int(cy - layer.height / 2)))


def pen_line(img, x0, y0, x1, y1, color, width):
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    n = 24
    pts = [(x0 + (x1 - x0) * t / n, y0 + (y1 - y0) * t / n + math.sin(t * 0.9) * width * 0.4) for t in range(n + 1)]
    d.line(pts, fill=color, width=int(max(2, width)), joint="curve")
    img.alpha_composite(layer)


# ---------------------------------------------------------------- finishes

def paper_grain(img, amount=6):
    a = np.asarray(img).astype(float)
    n = RNG.normal(0, amount, a.shape[:2])
    n = np.asarray(Image.fromarray(np.clip(n + 128, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8))).astype(float) - 128
    a[..., :3] += n[..., None]
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


def finish(img, kind):
    a = np.asarray(img).astype(float)
    h, w = a.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w] / max(h, w)
    if kind == "fotocopia":
        lum = a[..., :3].mean(axis=2, keepdims=True)
        a[..., :3] = a[..., :3] * 0.35 + lum * 0.65                          # copies lose colour
        a[..., :3] = np.clip((a[..., :3] - 18) * 1.08, 0, 255)
        speck = RNG.random((h, w)) < 0.0009
        a[speck, :3] *= 0.25
        edge = np.clip(1 - np.minimum(np.minimum(xx, yy), np.minimum(w / max(h, w) - xx, h / max(h, w) - yy)) / 0.02, 0, 1)
        a[..., :3] *= (1 - 0.18 * edge[..., None])                            # grey copier border
    elif kind == "desbotado":
        a[..., :3] = a[..., :3] * 0.62 + np.array([236, 222, 170]) * 0.38      # sun and age
        stain = np.exp(-(((xx - 0.78) ** 2) + ((yy - 0.25) ** 2)) / 0.004)
        a[..., :3] -= stain[..., None] * np.array([18, 26, 48])
        a[..., :3] *= (0.95 + 0.05 * (1 - yy))[..., None]
    out = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")
    if kind == "fotocopia":
        rgb = out.convert("RGB").filter(ImageFilter.GaussianBlur(0.6))
        out = Image.merge("RGBA", (*rgb.split(), out.getchannel("A")))
    return paper_grain(out, 5 if kind != "desbotado" else 9)


def render(doc):
    TEXT_SCALE[0] = doc.get("escalaTexto", 1.0)
    p = Page(doc)
    blocks = doc["blocos"]
    # tear-off strips reserve the bottom of the page first
    for b in blocks:
        if b["tipo"] == "destacaveis":
            b_destacaveis(p, b)
    for i, b in enumerate(blocks):
        if b["tipo"] == "destacaveis":
            continue
        if b["tipo"] == "rodape":
            b_rodape(p, b, last=(i == len(blocks) - 1))
        else:
            BLOCKS[b["tipo"]](p, b)
    if p.y > p.bottom + 1:
        print(f"  ! {doc['id']}: content overflows by {(p.y - p.bottom) / PX:.1f} mm")
    for r in doc.get("riscos", []):
        an = p.anchors[r["ancora"]]
        x = an["letters"][r["letra"]]
        x0, y0, x1, y1 = an["box"]
        s = an["size"] * 0.32
        pen_line(p.img, x - s, y1 - s * 0.2, x + s, y0 + s * 0.2, (192, 40, 30, 235), mm(0.5))
    for m in doc.get("manuscritos", []):
        if "ancora" in m:
            an = p.anchors[m["ancora"]]
            x0, y0, x1, y1 = an["box"]
            if "letra" in m:
                cx, cy = an["letters"][m["letra"]], y0 - mm(m["tamanho"]) * 0.55
            else:
                cx = x0 + (x1 - x0) * m.get("x", 0.5)
                cy = (y0 + y1) / 2 + (y1 - y0) * m.get("dy", 0)
        else:
            cx, cy = p.W * m["x"], p.H * m["y"]
        if m.get("alinhamento") == "esquerda":
            cx += font("hand", "r", m["tamanho"]).getlength(m["texto"]) / 2
        hand(p.img, m["texto"], cx, cy, m["tamanho"], m["cor"], m.get("rot", 0))
    img = finish(p.img, doc.get("acabamento", "papel"))
    os.makedirs(DOCS, exist_ok=True)
    img.save(os.path.join(DOCS, doc["id"] + ".png"))
    # papers with torn parts get a plain back with the same cut (seen when the paper is turned over)
    alpha = img.getchannel("A")
    if alpha.getextrema()[0] < 255:
        back = paper_grain(Image.new("RGBA", img.size, tuple(int(c * 0.97) for c in hexc(doc["papel"])[:3]) + (255,)), 5)
        back.putalpha(alpha)
        back.save(os.path.join(DOCS, doc["id"] + "_verso.png"))
    return img


# ---------------------------------------------------------------- paper relief

def height_to_normal(h, strength):
    gy, gx = np.gradient(h)
    n = np.dstack((-gx * strength, gy * strength, np.ones_like(h)))   # +y up in the texture (v up in Unity)
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8))


def fibres(n=512):
    """Tileable paper fibre detail (4 cm square): short random fibres plus fine grain, as a normal map and
    a grey detail albedo (0.5 = no change), both repeating on every paper."""
    rng = np.random.default_rng(5)
    h = np.zeros((n, n))
    yy, xx = np.mgrid[0:n, 0:n]
    img = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(img)
    for _ in range(2600):   # fibres: short slightly curved strokes, drawn wrapped so the tile repeats
        x, y = rng.uniform(0, n), rng.uniform(0, n)
        a = rng.uniform(0, math.pi)
        L = rng.uniform(6, 22)
        w = int(rng.integers(1, 3))
        val = int(rng.integers(30, 90))
        for ox in (-n, 0, n):
            for oy in (-n, 0, n):
                d.line((x + ox, y + oy, x + ox + math.cos(a) * L, y + oy + math.sin(a) * L), fill=val, width=w)
    fib = np.asarray(img.filter(ImageFilter.GaussianBlur(0.7))).astype(float) / 255
    grain = value_noise_tile(n, 64, 7) * 0.5 + value_noise_tile(n, 8, 8) * 0.5
    h = fib * 0.7 + grain * 0.6
    height_to_normal(h, 3.2).save(os.path.join(DIR, "Papel_Fibras_Normal.png"))
    alb = 0.5 + (grain - 0.5) * 0.06 - fib * 0.035
    Image.fromarray((np.clip(alb, 0, 1) * 255).astype(np.uint8)).convert("RGB").save(os.path.join(DIR, "Papel_Fibras_Detalhe.png"))


def value_noise_tile(n, cells, seed):
    """Tileable smooth noise: a random grid wrapped around and upsampled."""
    rng = np.random.default_rng(seed)
    g = rng.random((cells, cells))
    big = np.tile(g, (3, 3))
    im = Image.fromarray((big * 255).astype(np.uint8)).resize((n * 3, n * 3), Image.BICUBIC)
    a = np.asarray(im).astype(float)[n:2 * n, n:2 * n] / 255
    return (a - a.min()) / (a.max() - a.min() + 1e-9)


def relief(doc):
    """Waviness, crumples and fold lines of one paper, as a normal map over the whole sheet."""
    w, h = doc["tamanho"]
    W = 512 if w >= h else int(512 * w / h)
    H = 512 if h >= w else int(512 * h / w)
    k = doc.get("amassado", 1.0)
    seed = sum(map(ord, doc["id"]))
    wave = value_noise(H, W, max(W, H) // 3, 2, seed)            # sheet not quite flat
    crumple = value_noise(H, W, max(W, H) // 14, 3, seed + 1)     # handled, folded in a bag...
    hgt = wave * 0.6 + crumple * 0.8 * k
    yy, xx = np.mgrid[0:H, 0:W]
    for v in doc.get("vincos", []):                              # folds: a sharp ridge with soft shoulders
        if v["eixo"] == "h":
            dist = np.abs(yy / (H - 1) - v["pos"]) * h * 1000
        else:
            dist = np.abs(xx / (W - 1) - v["pos"]) * w * 1000
        hgt += 0.9 * np.exp(-dist / 2.5) - 0.25 * np.exp(-dist / 12)
    height_to_normal(hgt, 3.0 * (0.4 + 0.6 * k)).save(os.path.join(DOCS, doc["id"] + "_relevo.png"))


# ---------------------------------------------------------------- cork

def value_noise(h, w, scale, octaves=3, seed=0):
    rng = np.random.default_rng(seed)
    out = np.zeros((h, w))
    for o in range(octaves):
        s = max(2, int(scale / 2 ** o))
        small = rng.random((h // s + 2, w // s + 2))
        layer = np.asarray(Image.fromarray((small * 255).astype(np.uint8)).resize((w + s, h + s), Image.BICUBIC)).astype(float)[:h, :w] / 255
        out += layer / 2 ** o
    return (out - out.min()) / (out.max() - out.min())


def doc_quad(doc, cw, ch, px):
    """Corners of a document on the cork image (pixels), from its board position, size and rotation."""
    w, h = doc["tamanho"]
    cx, cy = doc["posicao"]
    a = math.radians(-doc["rotacao"])   # image y points down
    pts = []
    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        x, y = sx * w / 2, sy * h / 2
        xr, yr = x * math.cos(a) - y * math.sin(a), x * math.sin(a) + y * math.cos(a)
        pts.append(((cx + xr + cw / 2) * 1000 * px, (ch / 2 - cy + yr) * 1000 * px))
    return pts


def cork(data):
    q = data["quadro"]
    cw, ch = q["largura"] - 2 * q["moldura"], q["altura"] - 2 * q["moldura"]
    W, H = int(cw * 1000 * CORK_PX), int(ch * 1000 * CORK_PX)
    coarse = value_noise(H, W, 40, 2, 1)
    grains = value_noise(H, W, 6, 2, 2)
    fine = RNG.random((H, W))
    height = 0.45 * grains + 0.25 * coarse + 0.3 * fine
    base = np.array([0.67, 0.50, 0.33])
    col = base[None, None, :] * (0.78 + 0.32 * height[..., None])
    dark = (value_noise(H, W, 3, 1, 3) > 0.86) & (fine > 0.5)                 # dark bits of bark in the agglomerate
    col[dark] *= 0.62
    col *= (0.93 + 0.07 * coarse)[..., None]
    # sun-bleached top (the board faces the ceiling lights; older cork lightens)
    yy = np.linspace(0, 1, H)[:, None]
    col *= (1.03 - 0.06 * yy)[..., None]
    img = Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8)).convert("RGBA")
    d = ImageDraw.Draw(img)
    # pin holes and a few old staples / torn paper corners from notices taken down
    for _ in range(140):
        x, y = RNG.uniform(8, W - 8), RNG.uniform(8, H - 8)
        r = RNG.uniform(0.6, 1.1)
        d.ellipse((x - r, y - r, x + r, y + r), fill=(48, 32, 20, 255))
    for _ in range(9):
        x, y = RNG.uniform(20, W - 20), RNG.uniform(20, H - 20)
        ang = RNG.uniform(-0.5, 0.5)
        L = 12 * CORK_PX / 1.75
        d.line((x - math.cos(ang) * L / 2, y - math.sin(ang) * L / 2, x + math.cos(ang) * L / 2, y + math.sin(ang) * L / 2),
               fill=(170, 170, 172, 255), width=2)
    for _ in range(5):
        x, y = RNG.uniform(30, W - 30), RNG.uniform(30, H - 30)
        s = RNG.uniform(6, 14)
        pts = [(x, y), (x + s, y + RNG.uniform(-2, 2)), (x + RNG.uniform(-2, 3), y + s)]
        d.polygon(pts, fill=(238, 236, 226, 255))
        d.ellipse((x + 2, y + 2, x + 4, y + 4), fill=(48, 32, 20, 255))
    # contact shadows of the pinned documents (light from the ceiling: shadow falls a little downwards)
    shadow = Image.new("L", (W, H), 0)
    sd = ImageDraw.Draw(shadow)
    for doc in data["documentos"]:
        pts = doc_quad(doc, cw, ch, CORK_PX)
        sd.polygon([(x, y + 2.5 * CORK_PX) for x, y in pts], fill=120)
    shadow = shadow.filter(ImageFilter.GaussianBlur(3.2 * CORK_PX))
    sh = np.asarray(shadow).astype(float) / 255
    arr = np.asarray(img).astype(float)
    arr[..., :3] *= (1 - 0.55 * sh)[..., None]
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")
    img.convert("RGB").save(os.path.join(DIR, "Cortica_Base.png"))
    # normal map from the granule height (holes pressed in)
    hgt = height.copy()
    holes = np.asarray(img.convert("L")).astype(float)
    gy, gx = np.gradient(hgt * 3.0)
    nrm = np.dstack((-gx, -gy, np.ones_like(hgt)))
    nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
    Image.fromarray(((nrm * 0.5 + 0.5) * 255).astype(np.uint8)).save(os.path.join(DIR, "Cortica_Normal.png"))
    return img, cw, ch


def preview(data, cork_img, cw, ch, docs):
    """The whole board as laid out (for checking the layout and as a reference image)."""
    s = 1.75
    W, H = cork_img.size
    board = cork_img.copy()
    for doc in sorted(data["documentos"], key=lambda d: d["ordem"]):
        img = docs[doc["id"]]
        w, h = doc["tamanho"]
        small = img.resize((int(w * 1000 * s), int(h * 1000 * s)), Image.LANCZOS)
        rot = small.rotate(doc["rotacao"], expand=True, resample=Image.BICUBIC)
        cx, cy = (doc["posicao"][0] + cw / 2) * 1000 * s, (ch / 2 - doc["posicao"][1]) * 1000 * s
        board.alpha_composite(rot, (int(cx - rot.width / 2), int(cy - rot.height / 2)))
        fx = doc.get("fixacao", {})
        if fx.get("tipo") in ("percevejo", "fita"):
            for pt in fx["pontos"]:
                u, v = pt["u"], pt["v"]
                x, y = (u - 0.5) * w, (0.5 - v) * h
                a = math.radians(doc["rotacao"])
                xr, yr = x * math.cos(a) - y * math.sin(a), x * math.sin(a) + y * math.cos(a)
                px_, py_ = (doc["posicao"][0] + xr + cw / 2) * 1000 * s, (ch / 2 - doc["posicao"][1] - yr) * 1000 * s
                d = ImageDraw.Draw(board)
                if fx["tipo"] == "fita":
                    d.rectangle((px_ - 18, py_ - 7, px_ + 18, py_ + 7), fill=(240, 236, 210, 170))
                else:
                    d.ellipse((px_ - 6, py_ - 6, px_ + 6, py_ + 6), fill=(200, 40, 40, 255))
    board.convert("RGB").save(os.path.join(DIR, "Quadro_Previa.png"))


def main():
    data = json.load(open(os.path.join(DIR, "QuadroAvisos.json"), encoding="utf-8"))
    docs = {}
    fibres()
    for doc in data["documentos"]:
        docs[doc["id"]] = render(doc)
        relief(doc)
        print(f"{doc['id']}: {docs[doc['id']].size}")
    cork_img, cw, ch = cork(data)
    preview(data, cork_img, cw, ch, docs)


if __name__ == "__main__":
    main()

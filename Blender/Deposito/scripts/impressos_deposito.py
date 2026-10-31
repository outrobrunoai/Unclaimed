"""Painted print of the storage-room packaging (Pillow + numpy, no generated imagery). Each asset's net (impressao.net) is
painted face by face: kraft or coated board, flexo print, packing tape over the seam, the stock sticker, and wear where
boxes are handled and dragged (edges, corners, the bottom band). Returns the colour image and a mask
(R: tape gloss, G: sticker paper, B: ink), both top-left origin over the whole canvas.
Text is short and factual (product, size, quantity, validity), as on the delivered material."""
import math, os, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

FONTS = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../Recepcao/fontes'))
SANS = '/usr/share/fonts/noto/NotoSans-Regular.ttf'
SANS_B = '/usr/share/fonts/noto/NotoSans-Bold.ttf'
SANS_C = '/usr/share/fonts/gsfonts/NimbusSansNarrow-Bold.otf'
MONO = '/usr/share/fonts/noto/NotoSansMono-Regular.ttf'
HELENA = os.path.join(FONTS, 'ReenieBeanie.ttf')
_fonts = {}


def font(path, px):
    if not os.path.exists(path):
        path = SANS_B
    key = (path, int(px))
    if key not in _fonts:
        _fonts[key] = ImageFont.truetype(path, max(6, int(px)))
    return _fonts[key]


def _noise(shape, cells, rng):
    """Smooth value noise in [0, 1]: random grid upscaled bicubically."""
    h, w = shape
    g = rng.random((max(2, int(h / cells) + 2), max(2, int(w / cells) + 2))).astype(np.float32)
    im = Image.fromarray((g * 255).astype(np.uint8)).resize((w + int(cells) * 2, h + int(cells) * 2), Image.BICUBIC)
    a = np.asarray(im, dtype=np.float32)[int(cells):int(cells) + h, int(cells):int(cells) + w] / 255.0
    return a


class Canvas:
    def __init__(self, rects, size, px, seed, bg=(150, 112, 74)):
        self.rects, self.size, self.px = rects, size, px
        self.k = px / size                       # pixels per metre
        self.rng = np.random.default_rng(seed)
        self.rnd = random.Random(seed)
        self.img = Image.new('RGB', (px, px), bg)   # the padding is what untextured parts read
        self.mask = Image.new('RGB', (px, px), (0, 0, 0))

    def box(self, name):
        """Pixel box (x0, y0, x1, y1) of a rect (top-left origin)."""
        x, y, w, h = self.rects[name]
        k = self.k
        return int(x * k), int(self.px - (y + h) * k), int((x + w) * k), int(self.px - y * k)

    def m(self, v):
        return v * self.k

    # ---------------------------------------------------------------- surfaces
    def board(self, name, rgb, kraft=True, flutes_vertical=True):
        x0, y0, x1, y1 = self.box(name)
        w, h = x1 - x0, y1 - y0
        if w <= 0 or h <= 0:
            return
        c = np.array(rgb, np.float32)
        big = _noise((h, w), self.m(0.12), self.rng)
        mid = _noise((h, w), self.m(0.02), self.rng)
        fine = self.rng.random((h, w)).astype(np.float32)
        shade = 1.0 + (big - 0.5) * (0.10 if kraft else 0.03) + (mid - 0.5) * (0.06 if kraft else 0.015) + (fine - 0.5) * (0.05 if kraft else 0.012)
        if kraft:   # faint flute ribs of the corrugated wall
            if flutes_vertical:
                xs = np.arange(w)[None, :] / self.m(0.0045)
            else:
                xs = np.arange(h)[:, None] / self.m(0.0045)
            shade = shade + 0.012 * np.sin(xs * 2 * math.pi)
        a = np.clip(c[None, None, :] * shade[..., None], 0, 255).astype(np.uint8)
        self.img.paste(Image.fromarray(a, 'RGB'), (x0, y0))

    def wear(self, name, edge=0.010, bottom=0.0, amount=1.0, sides='lrtb', rub=None):
        """Darkened, crushed edges (varying along them) and an optional scuffed band at the bottom of a face."""
        x0, y0, x1, y1 = self.box(name)
        w, h = x1 - x0, y1 - y0
        if w <= 0 or h <= 0:
            return
        reg = np.asarray(self.img.crop((x0, y0, x1, y1)), np.float32)
        yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
        d = np.full((h, w), 1e9, np.float32)
        if 'l' in sides: d = np.minimum(d, xx)
        if 'r' in sides: d = np.minimum(d, w - 1 - xx)
        if 't' in sides: d = np.minimum(d, yy)
        if 'b' in sides: d = np.minimum(d, h - 1 - yy)
        var = _noise((h, w), self.m(0.05), self.rng)
        e = np.clip(1 - d / (self.m(edge) * (0.5 + var)), 0, 1) ** 1.6
        dark = 1 - 0.20 * amount * e
        if bottom > 0:
            band = np.clip(1 - (h - 1 - yy) / self.m(bottom), 0, 1) ** 1.3
            streak = _noise((h, w), self.m(0.006), self.rng) * _noise((h, w), self.m(0.04), self.rng)
            dark = dark * (1 - 0.22 * amount * band * (0.4 + streak))
        out = reg * dark[..., None]
        if rub is not None:   # coated board rubbed through to its grey core on the edges
            core = np.array(rub, np.float32)
            t = np.clip(e * 1.4 - 0.5 * var, 0, 1)[..., None] * amount
            out = out * (1 - t) + core * t
        self.img.paste(Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), 'RGB'), (x0, y0))

    # ---------------------------------------------------------------- print
    def _face_layer(self, name):
        x0, y0, x1, y1 = self.box(name)
        return Image.new('L', (x1 - x0, y1 - y0), 0), (x0, y0)

    def ink(self, name, draw_fn, rgb=(32, 36, 50), fade=0.85, erode=0.35):
        """Draws with draw_fn(ImageDraw, w, h, k) on a coverage layer, then lays it as worn flexo/offset ink."""
        lay, (x0, y0) = self._face_layer(name)
        draw_fn(ImageDraw.Draw(lay), lay.width, lay.height, self.k)
        cov = np.asarray(lay, np.float32) / 255.0
        if erode > 0:
            n = _noise(cov.shape, self.m(0.0025), self.rng) * 0.6 + _noise(cov.shape, self.m(0.03), self.rng) * 0.4
            cov = cov * np.clip(1.3 - erode * n * 1.6, 0, 1)
        cov = cov * fade
        reg = np.asarray(self.img.crop((x0, y0, x0 + lay.width, y0 + lay.height)), np.float32)
        out = reg * (1 - cov[..., None]) + np.array(rgb, np.float32) * cov[..., None]
        self.img.paste(Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), 'RGB'), (x0, y0))
        mk = np.asarray(self.mask.crop((x0, y0, x0 + lay.width, y0 + lay.height)), np.float32)
        mk[..., 2] = np.maximum(mk[..., 2], cov * 255)
        self.mask.paste(Image.fromarray(mk.astype(np.uint8), 'RGB'), (x0, y0))

    def fill_rect(self, name, u0, v0, u1, v1, rgb, coverage=1.0):
        """Solid colour block in face metres (u right, v up from the face's bottom)."""
        def fn(d, w, h, k):
            d.rectangle((u0 * k, h - v1 * k, u1 * k, h - v0 * k), fill=int(255 * coverage))
        self.ink(name, fn, rgb, fade=1.0, erode=0.08)

    def text(self, name, lines, rgb=(32, 36, 50), erode=0.3, fade=0.9):
        """lines: (text, u_centre, v_centre, height_m, font_path[, max_width_m])."""
        def fn(d, w, h, k):
            for ln in lines:
                t, u, v, hm, fp = ln[:5]
                f = font(fp, hm * k)
                if len(ln) > 5:
                    while f.size > 6 and d.textlength(t, font=f) > ln[5] * k:
                        f = font(fp, f.size - 1)
                d.text((u * k, h - v * k), t, font=f, fill=255, anchor='mm')
        self.ink(name, fn, rgb, fade=fade, erode=erode)

    def symbols(self, name, u, v, s, rgb=(32, 36, 50)):
        """Handling marks: this way up (two arrows), keep dry (umbrella with drops), at (u, v) bottom-left, size s."""
        def fn(d, w, h, k):
            x, y, q = u * k, h - v * k, s * k
            lw = max(2, int(q * 0.07))
            d.rectangle((x, y - q, x + q, y), outline=255, width=lw)
            for ax in (x + q * 0.33, x + q * 0.67):
                d.line((ax, y - q * 0.18, ax, y - q * 0.70), fill=255, width=lw)
                d.polygon([(ax - q * 0.12, y - q * 0.62), (ax + q * 0.12, y - q * 0.62), (ax, y - q * 0.84)], fill=255)
            d.line((x + q * 0.2, y - q * 0.12, x + q * 0.8, y - q * 0.12), fill=255, width=lw)
            x2 = x + q * 1.25
            d.rectangle((x2, y - q, x2 + q, y), outline=255, width=lw)
            d.pieslice((x2 + q * 0.15, y - q * 0.80, x2 + q * 0.85, y - q * 0.25), 180, 360, fill=255)
            d.line((x2 + q * 0.5, y - q * 0.52, x2 + q * 0.5, y - q * 0.2), fill=255, width=lw)
            d.arc((x2 + q * 0.5, y - q * 0.30, x2 + q * 0.66, y - q * 0.14), 0, 180, fill=255, width=lw)
            for dx, dy in ((0.22, 0.12), (0.5, 0.06), (0.78, 0.12)):
                d.ellipse((x2 + q * dx - q * 0.035, y - q * (0.88 + dy) + q * 0.05, x2 + q * dx + q * 0.035, y - q * (0.88 + dy) + q * 0.12), fill=255)
        self.ink(name, fn, rgb, fade=0.85, erode=0.3)

    # ---------------------------------------------------------------- tape and stickers
    def tape(self, name, u0, v0, u1, v1, base=(176, 128, 62)):
        """Brown packing tape (glossy, translucent: the board shows through), slightly wavy, edges a little lifted."""
        x0, y0, x1, y1 = self.box(name)
        h = y1 - y0
        k = self.k
        px0, px1 = x0 + int(u0 * k), x0 + int(u1 * k)
        py0, py1 = y0 + int(h - v1 * k), y0 + int(h - v0 * k)
        px0, py0 = max(px0, x0), max(py0, y0)
        px1, py1 = min(px1, x1), min(py1, y1)
        if px1 <= px0 or py1 <= py0:
            return
        reg = np.asarray(self.img.crop((px0, py0, px1, py1)), np.float32)
        th, tw = reg.shape[:2]
        tone = 1 + (_noise((th, tw), self.m(0.03), self.rng) - 0.5) * 0.08
        out = reg * 0.55 + np.array(base, np.float32) * 0.45
        out = out * tone[..., None]
        edge = np.zeros((th, tw), np.float32)
        ew = max(1, int(self.m(0.0012)))
        if (u1 - u0) < (v1 - v0):
            edge[:, :ew] = 1; edge[:, -ew:] = 1
        else:
            edge[:ew, :] = 1; edge[-ew:, :] = 1
        out = out * (1 - 0.18 * edge[..., None])
        self.img.paste(Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), 'RGB'), (px0, py0))
        mk = np.asarray(self.mask.crop((px0, py0, px1, py1)), np.float32)
        mk[..., 0] = 255 * (1 - 0.5 * edge)
        self.mask.paste(Image.fromarray(mk.astype(np.uint8), 'RGB'), (px0, py0))

    def sticker(self, name, u, v, w_m, h_m, lines, tilt=0.0, paper=(232, 230, 220), soil=0.15):
        """Stock label: printed white paper sticker, lines (text, rel_v 0..1, rel_height, font) left aligned."""
        k = self.k
        W, Hh = int(w_m * k), int(h_m * k)
        st = Image.new('RGBA', (W, Hh), paper + (255,))
        d = ImageDraw.Draw(st)
        bw = max(1, int(self.m(0.0008)))
        d.rectangle((bw, bw, W - 1 - bw, Hh - 1 - bw), outline=(120, 118, 112, 255), width=bw)
        for t, rv, rh, fp in lines:
            f = font(fp, rh * Hh)
            while f.size > 6 and d.textlength(t, font=f) > W * 0.90:
                f = font(fp, f.size - 1)
            d.text((W * 0.05, Hh * (1 - rv)), t, font=f, fill=(28, 28, 30, 255), anchor='lm')
        a = np.asarray(st, np.float32)
        n = _noise((Hh, W), self.m(0.02), self.rng)
        a[..., :3] *= (1 - soil * n)[..., None]
        st = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA').rotate(tilt, expand=True, resample=Image.BICUBIC)
        x0, y0, x1, y1 = self.box(name)
        hface = y1 - y0
        cx, cy = x0 + int(u * k), y0 + int(hface - v * k)
        pos = (cx - st.width // 2, cy - st.height // 2)
        self.img.paste(st.convert('RGB'), pos, st)
        alpha = st.split()[3]
        ml = Image.new('RGB', st.size, (0, 255, 0))
        self.mask.paste(ml, pos, alpha)

    def hand(self, name, u, v, text, h_m, rgb=(25, 45, 130), tilt=-2.0, path=HELENA):
        """Ballpoint handwriting on a printed field (Helena's blue pen)."""
        k = self.k
        f = font(path, h_m * k)
        tmp = Image.new('L', (int(f.getlength(text)) + 20, int(h_m * k * 1.6) + 10), 0)
        ImageDraw.Draw(tmp).text((10, tmp.height // 2), text, font=f, fill=235, anchor='lm')
        tmp = tmp.rotate(tilt, expand=True, resample=Image.BICUBIC)
        x0, y0, x1, y1 = self.box(name)
        pos = (x0 + int(u * k), y0 + int((y1 - y0) - v * k) - tmp.height // 2)
        ink = Image.new('RGB', tmp.size, rgb)
        self.img.paste(ink, pos, tmp)

    def save(self, path):
        self.img.save(path + '.png')
        self.mask.save(path + '_mask.png')

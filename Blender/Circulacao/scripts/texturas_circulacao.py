"""Interior circulation textures, drawn with numpy + Pillow (system Python, offline).

    python3 -I Blender/Circulacao/scripts/texturas_circulacao.py

Floor: 2048 px / 2.4 m, four 1.20 m cast terrazzo panels, 3 mm dividing strips.
Maps: Base (sRGB), Normal (OpenGL / Unity), MS (linear, R = 0, A = smoothness).
Signs: 2.56 px/mm, Nimbus Sans, physical margins and 4x supersampling, following
docs/direcao-arte/comunicacao-visual.md, section 2, and Recepcao's functional_plate.
Every generator has its own fixed random stream; no external images are needed.
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
from PIL.PngImagePlugin import PngInfo


HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "..", "Assets",
                                   "UnclaimedPrototype", "Circulacao", "Texturas"))
NIMBUS = "/usr/share/fonts/gsfonts/NimbusSans-Regular.otf"
NIMBUS_B = "/usr/share/fonts/gsfonts/NimbusSans-Bold.otf"
PLATE, INK = "#e4e1d6", "#2b2d29"
PX_MM, SS = 2.56, 4


def save(img, name, srgb=True):
    os.makedirs(OUT, exist_ok=True)
    info = PngInfo()
    if srgb:
        info.add(b"sRGB", b"\x00")
    img.save(os.path.join(OUT, name), pnginfo=info)
    print(name, flush=True)
    return name


def save_array(a, name, srgb=True):
    return save(Image.fromarray(np.uint8(np.clip(a, 0, 1) * 255 + 0.5)), name, srgb)


def noise(h, w, scale, rng, octaves=2):
    """Periodic frequency-filtered value noise, as in texturas_superficies.py."""
    fy = np.fft.fftfreq(h)[:, None] * h
    fx = np.fft.fftfreq(w)[None, :] * w
    out = np.zeros((h, w), dtype=np.float32)
    for o in range(octaves):
        k = scale * 2 ** o
        spectrum = np.fft.fft2(rng.standard_normal((h, w)))
        layer = np.fft.ifft2(spectrum * np.exp(-(fx * fx + fy * fy) / k ** 2)).real
        out += (layer / (layer.std() + 1e-9) / 2 ** o).astype(np.float32)
    return (out - out.min()) / (out.max() - out.min())


def normal_from_height(h, strength):
    """Same tangent convention as the reference, with periodic central differences."""
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5
    nrm = np.dstack((-gx * strength, gy * strength, np.ones_like(h)))
    nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
    return nrm * 0.5 + 0.5


def granilite(variant, matrix, palette, strip, seed, n=2048):
    """Dense marble/quartz chips, shallow joints and local wheel wear; no dirt wash."""
    rng = np.random.default_rng(seed)
    px_mm, t = n / 2400.0, n // 2
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32) + 0.5
    # Shift the joint grid by half a panel: texture borders pass through panel centres.
    fx, fy = (xx + t / 2) % t, (yy + t / 2) % t
    edge = np.minimum(np.minimum(fx, t - fx), np.minimum(fy, t - fy)) / px_mm
    joint = np.clip((1.8 - edge) / 0.6, 0, 1)
    shoulder = np.exp(-np.maximum(edge - 1.5, 0) / 2.5) * (1 - joint)
    tx, ty = ((xx + t / 2) // t).astype(int) % 2, ((yy + t / 2) // t).astype(int) % 2
    shade = rng.uniform(-0.014, 0.014, (2, 2))[ty, tx]
    gloss = rng.uniform(-0.012, 0.012, (2, 2))[ty, tx]
    cloud = noise(n, n, 7, rng)
    micro = noise(n, n, 390, rng, 1)
    col = np.array(matrix, dtype=np.float32)[None, None, :] / 255
    col = col * (1 + shade[..., None] + 0.025 * (cloud[..., None] - 0.5)
                 + 0.012 * (micro[..., None] - 0.5))

    # Subpixel irregular polygons, 2--8 mm across, biased toward the small fractions.
    # Wrapping chips also preserves their antialiasing at both opposite borders.
    chip_img = Image.new("RGBA", (n * 2, n * 2))
    d = ImageDraw.Draw(chip_img)
    for _ in range(92000):
        cx, cy = rng.uniform(0, n, 2)
        diameter = (2 + 6 * rng.beta(1.1, 3.5)) * px_mm
        radius = diameter / 2
        angles = np.arange(6) * np.pi / 3 + rng.uniform(0, 2 * np.pi)
        radii = radius * rng.uniform(0.65, 1.0, 6)
        points = np.column_stack((np.cos(angles) * radii, np.sin(angles) * radii))
        tint = np.array(palette[rng.integers(len(palette))]) + rng.uniform(-7, 7)
        color = tuple(np.uint8(np.clip(tint, 0, 255))) + (255,)
        oxs = [0] + ([n] if cx < radius else []) + ([-n] if cx > n - radius else [])
        oys = [0] + ([n] if cy < radius else []) + ([-n] if cy > n - radius else [])
        for ox in oxs:
            for oy in oys:
                d.polygon([((cx + x + ox) * 2, (cy + y + oy) * 2) for x, y in points], fill=color)
    chips = np.asarray(chip_img.resize((n, n), Image.Resampling.LANCZOS), np.float32) / 255
    alpha = chips[..., 3]
    col = col * (1 - alpha[..., None]) + chips[..., :3] * alpha[..., None]
    strip_col = np.array(strip) / 255 * (0.985 + 0.03 * cloud[..., None])
    col = col * (1 - joint[..., None]) + strip_col * joint[..., None]

    # Two rare short shrinkage cracks start beside a dividing strip.
    cracks = Image.new("L", (n * 2, n * 2))
    cd = ImageDraw.Draw(cracks)
    for y0 in (n * 0.32, n * 0.69):
        x0 = n * 0.25 + 2.3 * px_mm
        points = [(2 * (x0 + k * 4), 2 * (y0 + k * 1.5 + rng.uniform(-2, 2)))
                  for k in range(10)]
        cd.line(points, fill=155, width=1)
    crack = np.asarray(cracks.resize((n, n), Image.Resampling.LANCZOS), np.float32) / 255
    col *= (1 - 0.30 * crack[..., None])

    # Thin wheel scratches stay within a few centimetres of a strip.
    scratches = Image.new("L", (n * 2, n * 2))
    sd = ImageDraw.Draw(scratches)
    for _ in range(140):
        axis = rng.integers(2)
        across = rng.choice([n * 0.25, n * 0.75]) + rng.uniform(-23, 23)
        along = rng.uniform(30, n - 100)
        length = rng.uniform(8, 65)
        p = [(across, along), (across + rng.uniform(-3, 3), along + length)]
        if axis:
            p = [(y, x) for x, y in p]
        sd.line([(x * 2, y * 2) for x, y in p], fill=int(rng.integers(60, 160)), width=1)
    scratch = np.asarray(scratches.resize((n, n), Image.Resampling.LANCZOS), np.float32) / 255
    wear = np.exp(-edge / 16) * (0.5 + 0.5 * cloud)
    col *= 1 - 0.018 * scratch[..., None]
    prefix = "Circulacao_Granilite_" + variant
    save_array(col, prefix + "_Base.png")
    height = (0.010 * (micro - 0.5) + 0.008 * alpha - 0.20 * joint
              - 0.025 * shoulder - 0.025 * crack - 0.012 * scratch)
    save_array(normal_from_height(height * px_mm, 0.9), prefix + "_Normal.png", False)
    smooth = (0.405 + gloss + 0.018 * (cloud - 0.5) + 0.015 * alpha
              - 0.085 * wear - 0.040 * scratch)
    smooth = smooth * (1 - joint) + 0.27 * joint
    ms = np.zeros((n, n, 4), dtype=np.float32)
    ms[..., 3] = np.clip(smooth, 0.25, 0.45)
    save_array(ms, prefix + "_MS.png", False)


def decals():
    rng = np.random.default_rng(641)

    def output(name, rgb, alpha):
        a = np.zeros((*alpha.shape, 4), dtype=np.float32)
        a[..., :3] = np.asarray(rgb) / 255
        a[..., 3] = alpha
        save_array(a, name)

    # A 2 m long by 1 m wide drawing: 0.55 m gauge = 281.6 px.
    y, x = np.mgrid[0:512, 0:1024]
    centre = 200 + 0.00039 * (x - 512) ** 2
    tracks = np.zeros_like(x, dtype=float)
    for axle in (-140.8, 140.8):
        for twin in (-3.0, 3.0):
            dist = y - centre - axle - twin
            tracks += np.exp(-(dist / 1.6) ** 2)
    fade = np.sin(np.pi * x / 1023) ** 1.1
    grain = noise(512, 1024, 110, rng)
    alpha = np.clip(tracks * fade * (0.14 + 0.34 * grain), 0, 0.53)
    output("Decal_RodasMaca.png", (64, 59, 53), alpha)

    # A half-metre cluster in a 2 m wall segment, with smooth subpixel scratches.
    im = Image.new("RGBA", (4096, 1024))
    d = ImageDraw.Draw(im)
    for i in range(34):
        x0, y0 = rng.uniform(360, 610), rng.uniform(90, 171)
        length = rng.uniform(12, 85)
        color = (218, 213, 197) if i % 2 else (68, 64, 57)
        d.line((x0 * 4, y0 * 4, (x0 + length) * 4, (y0 + rng.uniform(-1, 1)) * 4),
               fill=color + (int(rng.integers(35, 120)),), width=int(rng.integers(2, 5)))
    save(im.resize((1024, 256), Image.Resampling.LANCZOS), "Decal_RiscosParede.png")

    y, x = np.mgrid[0:512, 0:256]
    irregular = noise(512, 256, 9, rng)
    oval = np.exp(-(((x - 126) / 49) ** 2 + ((y - 274) / 103) ** 2) * 1.6)
    alpha = oval * (0.045 + 0.075 * irregular)
    # Broad palm/handle contact, with faint fingertip extensions rather than a stamped handprint.
    for cx, cy in ((102, 192), (122, 173), (144, 185)):
        alpha += 0.018 * np.exp(-((x - cx) / 12) ** 2 - ((y - cy) / 48) ** 2)
    alpha[:12] = alpha[-12:] = 0
    alpha[:, :8] = alpha[:, -8:] = 0
    output("Decal_MarcaMao.png", (97, 86, 66), np.clip(alpha, 0, 0.14))

    y, x = np.mgrid[0:128, 0:1024]
    uneven = noise(128, 1024, 35, rng)
    depth = 6 + 15 * uneven
    alpha = 0.34 * np.exp(-(127 - y) / depth) * (0.4 + 0.6 * uneven)
    alpha *= np.clip((y - 77) / 20, 0, 1) * np.sin(np.pi * x / 1023) ** 0.35
    output("Decal_PoeiraRodape.png", (76, 69, 57), alpha)

    rough = noise(128, 1024, 80, rng)
    edge = 18 + 3 * (rough - 0.5)
    band = (y > edge) & (y < 127 - edge)
    worn = np.zeros((128, 1024), dtype=float)
    for cx in (285, 565, 810):
        worn += np.exp(-((x - cx) / 34) ** 2) * (0.40 + rough)
    # White RGB stays pure even in holes, for clean Unity tinting. Intact paint alpha = 1.
    mask = Image.fromarray(np.uint8(band & (rough > 0.20) & (worn < 0.61)) * 255)
    alpha = np.asarray(mask.filter(ImageFilter.GaussianBlur(0.45)), dtype=float) / 255
    output("Decal_FaixaGasta.png", (255, 255, 255), alpha)


class Sign:
    """Physical-mm drawing surface; every mark and glyph is supersampled 4x."""

    def __init__(self, w, h, background=PLATE):
        self.w, self.h = w, h
        self.size = (round(w * PX_MM), round(h * PX_MM))
        self.scale = PX_MM * SS
        self.img = Image.new("RGB", (self.size[0] * SS, self.size[1] * SS), background)
        self.d = ImageDraw.Draw(self.img)
        self.margin = min(w, h) * 0.12

    def box(self, b):
        return tuple(round(v * self.scale) for v in b)

    def rectangle(self, box, fill=None, outline=None, width=1):
        self.d.rectangle(self.box(box), fill=fill, outline=outline, width=max(1, round(width * self.scale)))

    def ellipse(self, box, fill):
        self.d.ellipse(self.box(box), fill=fill)

    def line(self, points, fill=INK, width=1):
        self.d.line([(round(x * self.scale), round(y * self.scale)) for x, y in points],
                    fill=fill, width=max(1, round(width * self.scale)), joint="curve")

    def polygon(self, points, fill=INK):
        self.d.polygon([(round(x * self.scale), round(y * self.scale)) for x, y in points], fill=fill)

    def text(self, text, box, size, bold=False, color=INK, align="left"):
        """Fit by measured ink bounds, preserving aspect and explicit minimum clear margins.

        Size is in mm, never squeezed horizontally. A failed layout stops generation.
        """
        f = ImageFont.truetype(NIMBUS_B if bold else NIMBUS, round(size * self.scale))
        left, top, right, bottom = self.d.textbbox((0, 0), text, font=f)
        x0, y0, x1, y1 = (v * self.scale for v in box)
        width, height = right - left, bottom - top
        if width > x1 - x0 or height > y1 - y0:
            raise ValueError(f"Text does not fit: {text!r} ({width / self.scale:.1f} mm)")
        x = x0 - left if align == "left" else (x0 + x1 - width) / 2 - left
        y = (y0 + y1 - height) / 2 - top
        assert x + left >= self.margin * self.scale - 1
        assert x + right <= (self.w - self.margin) * self.scale + 1
        assert y + top >= self.margin * self.scale - 1
        assert y + bottom <= (self.h - self.margin) * self.scale + 1
        self.d.text((round(x), round(y)), text, font=f, fill=color)

    def arrow(self, cx, cy, length, direction="right", color=INK):
        # One closed filled path: no font glyphs, consistent arrowhead and stem.
        points = [(-0.5, -0.07), (0.12, -0.07), (0.12, -0.25), (0.5, 0),
                  (0.12, 0.25), (0.12, 0.07), (-0.5, 0.07)]
        transform = {"right": lambda x, y: (x, y), "left": lambda x, y: (-x, y),
                     "up": lambda x, y: (y, -x), "down": lambda x, y: (y, x)}[direction]
        pts = [transform(x * length, y * length) for x, y in points]
        self.polygon([(cx + x, cy + y) for x, y in pts], color)

    def finish(self, name):
        img = self.img.resize(self.size, Image.Resampling.LANCZOS)
        # Match functional_plate's subtle material variation and top-edge dust only.
        rng = np.random.default_rng(sum(map(ord, name)))
        grain = Image.fromarray(rng.integers(0, 256, (4, 12), dtype=np.uint8))
        grain = np.asarray(grain.resize(self.size, Image.Resampling.BICUBIC), np.float32) / 255
        a = np.asarray(img, np.float32) / 255
        yy = np.arange(self.size[1])[:, None]
        a *= (1 + 0.008 * (grain - 0.5))[..., None]
        a *= (1 - 0.025 * np.exp(-yy / (PX_MM * 1.2)))[..., None]
        return save_array(a, name)


def functional_plate(name, w, h, title, sub=None, restriction=False, centered=False):
    s = Sign(w, h)
    m = s.margin
    right = w - m - (120 if restriction else 0)
    if sub:
        s.text(title, (m, m + 8, right, h * 0.53), 46, True)
        # Long restriction caption has its own generous line, at a readable physical size.
        s.text(sub, (m, h * 0.63, right, h - m), 24)
    else:
        s.text(title, (m, m, right, h - m), 42, True,
               align="center" if centered else "left")
    if restriction:
        # Conventional barred-person pictogram in its own red restriction field.
        x0, y0, side = w - m - 88, (h - 88) / 2, 88
        s.rectangle((x0, y0, x0 + side, y0 + side), "#9a3b32")
        cx, cy = x0 + 44, y0 + 44
        s.ellipse((cx - 36, cy - 36, cx + 36, cy + 36), "white")
        s.ellipse((cx - 30, cy - 30, cx + 30, cy + 30), "#9a3b32")
        # Person within the ring; white slash denotes the barred person.
        s.ellipse((cx - 6, cy - 23, cx + 6, cy - 11), "white")
        s.line([(cx, cy - 7), (cx, cy + 9)], "white", 9)
        s.line([(cx - 16, cy + 1), (cx, cy - 6), (cx + 16, cy + 1)], "white", 5)
        s.line([(cx - 11, cy + 24), (cx, cy + 7), (cx + 11, cy + 24)], "white", 6)
        s.line([(cx - 25, cy - 25), (cx + 25, cy + 25)], "white", 7)
    s.finish(name)


def directory():
    s = Sign(1300, 760)
    m = s.margin
    s.text("Circulação interna", (m, m, 1100, 159), 44, True)
    s.line([(m, 195), (1300 - m, 195)], INK, 1.3)
    rows = [("Arquivo · Administração", 270, "right"),
            ("Descanso · Depósito", 400, "up"),
            ("Autópsia · Câmara fria · Recebimento", 530, "up")]
    for text, cy, direction in rows:
        s.text(text, (m, cy - 42, 1050, cy + 42), 54)
        s.arrow(1150, cy, 104, direction)
    s.text("Recepção", (m, 627, 400, 760 - m), 38)
    s.arrow(1150, 640, 52, "down")
    s.finish("Placa_Diretorio.png")


def running_person(s, cx, cy, size, facing_left=False):
    """Simple conventional running-person silhouette, articulated limbs and doorway."""
    def p(x, y):
        return cx + x * size * (-1 if facing_left else 1), cy + y * size
    hx, hy = p(0.18, -0.36)
    s.ellipse((hx - 0.10 * size, hy - 0.10 * size,
               hx + 0.10 * size, hy + 0.10 * size), "white")
    s.line([p(0.10, -0.20), p(-0.04, 0.07)], "white", size * 0.16)
    s.line([p(0.07, -0.17), p(0.26, -0.03), p(0.43, -0.03)], "white", size * 0.09)
    s.line([p(0.06, -0.18), p(-0.18, -0.26), p(-0.31, -0.06)], "white", size * 0.09)
    s.line([p(-0.04, 0.04), p(-0.23, 0.25), p(-0.47, 0.25)], "white", size * 0.12)
    s.line([p(-0.01, 0.05), p(0.17, 0.20), p(0.08, 0.47)], "white", size * 0.12)
    s.line([p(-0.53, 0.48), p(-0.53, -0.48), p(-0.18, -0.48)], "white", size * 0.045)


def safety_signs():
    s = Sign(250, 250, "#b23a2e")
    s.rectangle((7, 7, 243, 243), outline="white", width=2)
    # Extinguisher: cylinder, neck, valve, lever and hose with nozzle.
    s.rectangle((94, 87, 145, 168), "white")
    s.ellipse((94, 72, 145, 102), "white")
    s.ellipse((94, 151, 145, 180), "white")
    s.rectangle((113, 56, 126, 82), "white")
    s.line([(108, 56), (143, 48)], "white", 7)
    s.line([(109, 64), (142, 64)], "white", 5)
    s.line([(128, 66), (158, 72), (169, 93), (170, 137), (179, 153)], "white", 7)
    s.line([(177, 149), (184, 161)], "white", 10)
    s.arrow(125, 210, 36, "down", "white")
    s.finish("Placa_Extintor.png")

    s = Sign(120, 180, "#ece7d8")
    s.text("Extintor ABC · 6 kg", (s.margin, 20, 120 - s.margin, 38), 10, True)
    s.text("Recarga", (s.margin, 53, 120 - s.margin, 72), 13, True)
    for y in (87, 103):
        s.line([(s.margin, y), (120 - s.margin, y)], INK, 0.45)
    s.rectangle((s.margin, 120, 120 - s.margin, 161), outline=INK, width=0.5)
    s.finish("Placa_ExtintorEtiqueta.png")

    s = Sign(220, 300)
    s.polygon([(110, 28), (27, 177), (193, 177)], "#151612")
    s.polygon([(110, 42), (40, 169), (180, 169)], "#d8b21e")
    s.polygon([(111, 73), (86, 119), (108, 116), (98, 151),
               (137, 99), (115, 103), (123, 73)], "#151612")
    s.text("Risco elétrico", (s.margin, 190, 220 - s.margin, 228), 25, True)
    s.rectangle((s.margin, 243, 220 - s.margin, 274), "#d8b21e")
    s.text("Acesso restrito", (s.margin + 8, 246, 220 - s.margin - 8, 270), 20, True, "#151612")
    s.finish("Placa_RiscoEletrico.png")

    for suffix, direction in (("Direita", "right"), ("Esquerda", "left"), ("Frente", None)):
        s = Sign(400, 150, "#2f7a4d")
        s.rectangle((5, 5, 395, 145), outline="white", width=1.5)
        s.text("SAÍDA", (s.margin, s.margin, 190, 150 - s.margin), 45, True, "white")
        cx = 248 if direction == "left" else (255 if direction else 299)
        running_person(s, cx, 76, 94, facing_left=direction == "left")
        if direction:
            s.arrow(351, 75, 58, direction, "white")
        s.finish("Placa_Saida_" + suffix + ".png")

    s = Sign(90, 90, "#f5f2e9")
    red, m = "#9a3b32", s.margin
    s.text("Alarme de", (m, 13, 90 - m, 25), 12, True, red, "center")
    s.text("incêndio", (m, 28, 90 - m, 40), 12, True, red, "center")
    s.arrow(45, 53, 15, "down", red)
    s.text("Quebre o vidro", (m, 66, 90 - m, 78), 10, False, red, "center")
    s.finish("Placa_AcionadorAlarme.png")


def signs():
    directory()
    for name, w, title, center in (
            ("Arquivo", 620, "Arquivo e administração", True),
            ("Descanso", 620, "Descanso e vestiário", False),
            ("Deposito", 420, "Depósito", True),
            ("Recebimento", 620, "Recebimento de corpos", False),
            ("CamaraFria", 620, "Câmara fria", False)):
        functional_plate("Placa_" + name + ".png", w, 170, title, centered=center)
    functional_plate("Placa_Autopsia.png", 620, 200, "Autópsia",
                     "Acesso restrito a pessoal autorizado", restriction=True)
    functional_plate("Placa_SalaTecnica.png", 620, 200, "Sala técnica", "Pessoal autorizado")
    s = Sign(900, 220)
    s.text("Recebimento de corpos", (s.margin, 38, 710, 103), 50, True)
    s.text("Câmara fria", (s.margin, 121, 710, 181), 50)
    s.arrow(800, 110, 108)
    s.finish("Placa_Direcao_Recebimento.png")
    safety_signs()


if __name__ == "__main__":
    granilite("A", (141, 138, 130),
              [(192, 183, 162), (159, 154, 144), (106, 105, 100), (210, 204, 186), (131, 126, 115)],
              (111, 99, 67), 701)
    granilite("B", (133, 138, 140),
              [(192, 195, 193), (159, 164, 165), (96, 103, 106), (220, 219, 209), (117, 123, 126)],
              (75, 80, 81), 702)
    granilite("C", (143, 149, 141),
              [(208, 213, 200), (166, 179, 161), (110, 122, 112), (222, 222, 208), (139, 156, 140)],
              (77, 84, 79), 703)
    decals()
    signs()

# Flat textures for the reception finishes and fittings, drawn with numpy + Pillow (system Python):
#   python3 Blender/Recepcao/scripts/texturas_recepcao.py
# Output: Assets/UnclaimedPrototype/Recepcao/Texturas. Texts are neutral Portuguese signage only;
# body text on notices is drawn as grey lines (illegible on purpose).
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "..", "Assets", "UnclaimedPrototype", "Recepcao", "Texturas")
FONT = "/usr/share/fonts/noto/NotoSans-Regular.ttf"
FONT_B = "/usr/share/fonts/noto/NotoSans-Bold.ttf"
BLUE = (22, 52, 120)
RNG = np.random.default_rng(7)


def font(size, bold=False):
    return ImageFont.truetype(FONT_B if bold else FONT, size)


def periodic_noise(n, scale, octaves=3):
    """Tileable value noise by filtering white noise in the frequency domain."""
    out = np.zeros((n, n))
    for o in range(octaves):
        f = np.fft.fftfreq(n)[:, None] ** 2 + np.fft.fftfreq(n)[None, :] ** 2
        k = scale * 2 ** o
        spec = np.fft.fft2(RNG.standard_normal((n, n))) * np.exp(-f * (n / k) ** 2)
        layer = np.real(np.fft.ifft2(spec))
        out += layer / (layer.std() + 1e-9) / 2 ** o
    return (out - out.min()) / (out.max() - out.min())


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name))
    return name


def floor(n=1024, tiles=2, grout_px=4):
    """Beige polished porcelain, 60 x 60 cm tiles (texture covers 1.2 m), thin grout; normal map for joints."""
    base = np.array([0.80, 0.745, 0.655])
    cloud = periodic_noise(n, 3)
    fine = periodic_noise(n, 60, 2)
    img = base[None, None, :] * (0.94 + 0.08 * cloud[..., None] + 0.03 * (fine[..., None] - 0.5))
    t = n // tiles
    yy, xx = np.mgrid[0:n, 0:n]
    for i in range(tiles):            # each tile a slightly different shade (dye lot variation)
        for j in range(tiles):
            m = (yy // t == i) & (xx // t == j)
            img[m] *= 1.0 + RNG.uniform(-0.025, 0.025)
    dx, dy = np.minimum(xx % t, t - xx % t), np.minimum(yy % t, t - yy % t)
    joint = np.clip(1.0 - np.minimum(dx, dy) / grout_px, 0, 1)
    img = img * (1 - joint[..., None]) + np.array([0.52, 0.49, 0.44]) * joint[..., None]
    save(Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)), "Recepcao_Piso_Base.png")
    h = 1.0 - joint * 0.8
    gy, gx = np.gradient(h)
    nrm = np.dstack((-gx * 2.5, -gy * 2.5, np.ones_like(h)))
    nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
    return save(Image.fromarray(((nrm * 0.5 + 0.5) * 255).astype(np.uint8)), "Recepcao_Piso_Normal.png")


def plate(name, w, h, title, sub=None, band=BLUE, icon=None, title_rgb=BLUE, title_size=None):
    """Institutional PVC sign: white plate, coloured band with a pictogram, title and subtitle."""
    img = Image.new("RGB", (w, h), (244, 244, 240))
    d = ImageDraw.Draw(img)
    bw = h if icon else 0
    if icon:
        d.rectangle((0, 0, bw, h), fill=band)
        icon(d, bw / 2, h / 2, h * 0.36)
    size = title_size or int(h * (0.36 if sub else 0.46))
    ft = font(size, True)
    while d.textlength(title, font=ft) > 0.86 * (w - bw):   # shrink long titles to fit the plate
        size -= 4
        ft = font(size, True)
    tx = bw + (w - bw) / 2
    ty = h * (0.40 if sub else 0.5)
    d.text((tx, ty), title, font=ft, fill=title_rgb, anchor="mm")
    if sub:
        d.text((tx, h * 0.76), sub, font=font(int(h * 0.15)), fill=(70, 70, 70), anchor="mm")
    d.rectangle((0, 0, w - 1, h - 1), outline=(200, 200, 195), width=3)
    return save(img, name)


def icon_info(d, cx, cy, r):
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline="white", width=int(r * 0.16))
    d.text((cx, cy + r * 0.05), "i", font=font(int(r * 1.5), True), fill="white", anchor="mm")


def person(d, cx, cy, r, skirt):
    head = r * 0.2
    d.ellipse((cx - head, cy - r, cx + head, cy - r + 2 * head), fill="white")
    top = cy - r + 2.3 * head
    if skirt:
        d.polygon([(cx - r * 0.18, top), (cx + r * 0.18, top), (cx + r * 0.42, cy + r * 0.35), (cx - r * 0.42, cy + r * 0.35)], fill="white")
        for s in (-1, 1):
            d.rectangle((cx + s * r * 0.14 - r * 0.07, cy + r * 0.3, cx + s * r * 0.14 + r * 0.07, cy + r), fill="white")
    else:
        d.rounded_rectangle((cx - r * 0.3, top, cx + r * 0.3, cy + r * 0.25), radius=int(r * 0.08), fill="white")
        for s in (-1, 1):
            d.rectangle((cx + s * r * 0.15 - r * 0.1, cy + r * 0.2, cx + s * r * 0.15 + r * 0.1, cy + r), fill="white")


def icon_wc(d, cx, cy, r):
    person(d, cx - r * 0.45, cy, r * 0.9, False)
    d.line((cx, cy - r, cx, cy + r), fill="white", width=max(2, int(r * 0.06)))
    person(d, cx + r * 0.45, cy, r * 0.9, True)


def icon_stop(d, cx, cy, r):
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline="white", width=int(r * 0.2))
    d.line((cx - r * 0.7, cy + r * 0.7, cx + r * 0.7, cy - r * 0.7), fill="white", width=int(r * 0.2))


# International Symbol of Access (Rehabilitation International, 1968), the version used by NBR 9050.
# Paths copied unchanged from the public-domain SVG "File:International Symbol of Access.svg" on
# Wikimedia Commons (https://commons.wikimedia.org/wiki/File:International_Symbol_of_Access.svg);
# rendered with rsvg-convert so the proportions stay those of the reference.
ISA_PATHS = (
    "M 186.64897,65.480367 C 186.50176,83.078558 171.97518,97.221807 154.17772,97.094756 "
    "C 136.38026,96.967706 122.03,82.618314 122.10061,65.019576 C 122.17121,47.420839 136.63602,33.215826 "
    "154.43379,33.267127 C 172.23155,33.318428 186.64406,47.606678 186.65007,65.205636",
    "M 127.71647,81.536503 L 141.42615,239.55857 L 258.31919,238.83701 L 303.77759,347.79287 "
    "L 358.6163,330.47538 L 347.07131,303.05603 L 318.93039,310.27165 L 278.52292,210.6961 L 169.56706,210.6961 "
    "L 169.56706,188.32767 L 234.50764,188.32767 L 234.50764,159.4652 L 161.62988,159.4652 L 160.18676,96.689304 "
    "L 160.18676,96.689304",
    "M 124.21875,161.75 C -45.440292,275.71724 167.90561,487.74881 280.0625,327.59375 L 267.6875,285.03125 "
    "C 217.81425,422.65276 14.297265,311.90314 126.28125,199.15625 L 124.21875,161.75 z",
)


def isa_glyph(height, rgb=(255, 255, 255)):
    """The ISA pictogram alone (RGBA, transparent background), cropped to its own bounds, `height` px tall."""
    import subprocess
    svg = ('<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="1600" viewBox="0 0 430 430">'
           '<g fill="#fff">' + "".join(f'<path d="{p}"/>' for p in ISA_PATHS) + "</g></svg>")
    png = subprocess.run(["rsvg-convert", "-f", "png"], input=svg.encode(), capture_output=True, check=True).stdout
    import io
    img = Image.open(io.BytesIO(png)).convert("RGBA")
    img = img.crop(img.getchannel("A").getbbox())
    img = img.resize((round(img.width * height / img.height), height), Image.LANCZOS)
    solid = Image.new("RGBA", img.size, rgb + (255,))
    solid.putalpha(img.getchannel("A"))
    return solid


def paste_centered(img, glyph, cx, cy):
    img.alpha_composite(glyph, (round(cx - glyph.width / 2), round(cy - glyph.height / 2)))


def accessible_sign(n=512):
    """NBR 9050 style plate: white ISA on blue, centred in the square field above the caption."""
    w, h = n, int(n * 1.3)
    img = Image.new("RGBA", (w, h), BLUE + (255,))
    field = w                                   # square field on top; caption band below it
    paste_centered(img, isa_glyph(int(field * 0.72)), w / 2, field / 2 + n * 0.02)
    d = ImageDraw.Draw(img)
    d.text((w / 2, field + (h - field) / 2 - n * 0.01), "ESPAÇO RESERVADO", font=font(int(n * 0.085), True),
           fill="white", anchor="mm")
    d.rectangle((0, 0, w - 1, h - 1), outline=(230, 230, 230), width=8)
    return save(img.convert("RGB"), "Placa_EspacoReservado.png")


def accessible_floor(n=512):
    """Floor marking (RGBA) of the 0.80 x 1.20 m wheelchair space: white outline and, centred in it,
    a blue square with the white ISA; paint worn by foot traffic."""
    w, h = n, int(n * 1.5)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle((6, 6, w - 6, h - 6), outline=(240, 240, 240, 255), width=int(n * 0.03))
    s = int(n * 0.56)
    x0, y0 = (w - s) // 2, (h - s) // 2
    d.rectangle((x0, y0, x0 + s, y0 + s), fill=BLUE + (255,))
    paste_centered(img, isa_glyph(int(s * 0.78), (240, 240, 240)), w / 2, h / 2)
    a = np.asarray(img).astype(float)
    wear = periodic_noise(max(w, h), 12)[:h, :w]
    a[..., 3] *= np.clip(0.55 + 0.6 * (wear - 0.35), 0.15, 0.85)
    return save(Image.fromarray(a.astype(np.uint8)), "Decal_EspacoReservado.png")


def clock(n=512):
    """Opaque dial. The clock face in Unity is a disc whose UVs map onto the circle inscribed in this
    square, so the corners are never shown (they only repeat the dial colour to keep filtering clean)."""
    img = Image.new("RGB", (n, n), (238, 238, 234))
    d = ImageDraw.Draw(img)
    c, r = n / 2, n * 0.48
    for k in range(60):
        a = np.radians(k * 6)
        r0 = r * (0.84 if k % 5 == 0 else 0.9)
        d.line((c + np.sin(a) * r0, c - np.cos(a) * r0, c + np.sin(a) * r * 0.95, c - np.cos(a) * r * 0.95),
               fill=(20, 20, 20), width=8 if k % 5 == 0 else 3)
    for k in range(1, 13):
        a = np.radians(k * 30)
        d.text((c + np.sin(a) * r * 0.68, c - np.cos(a) * r * 0.68), str(k), font=font(int(n * 0.085), True),
               fill=(20, 20, 20), anchor="mm")
    return save(img, "Relogio_Mostrador.png")


def notice_board(w=1024, h=683):
    """Cork board with a few pinned notices: legible titles, body as grey lines."""
    cork = periodic_noise(1024, 90, 2)[:h, :w]
    spots = periodic_noise(1024, 25, 2)[:h, :w]
    base = np.array([0.66, 0.50, 0.33])
    img = base[None, None, :] * (0.85 + 0.25 * cork[..., None]) * (0.92 + 0.1 * spots[..., None])
    img = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    d = ImageDraw.Draw(img)
    notes = [((60, 50, 380, 300), "HORÁRIO DE\nATENDIMENTO", 7, (250, 250, 246), 2),
             ((430, 40, 700, 410), "DOCUMENTOS\nNECESSÁRIOS", 11, (250, 250, 246), -1.5),
             ((750, 70, 970, 330), "AVISO", 6, (246, 238, 170), 3),
             ((90, 360, 360, 630), "PROIBIDO\nFUMAR", 0, (250, 250, 246), -2),
             ((470, 460, 760, 640), "TELEFONES ÚTEIS", 4, (220, 236, 250), 1)]
    for (x0, y0, x1, y1), title, lines, paper, ang in notes:
        sheet = Image.new("RGBA", (x1 - x0, y1 - y0), paper + (255,))
        sd = ImageDraw.Draw(sheet)
        sd.multiline_text(((x1 - x0) / 2, 22), title, font=font(30, True), fill=(30, 30, 30), anchor="ma", align="center")
        if title.startswith("PROIBIDO"):
            cx, cy, r = (x1 - x0) / 2, (y1 - y0) * 0.62, (y1 - y0) * 0.25
            sd.rectangle((cx - r * 0.7, cy - r * 0.1, cx + r * 0.45, cy + r * 0.1), fill=(150, 150, 150))
            sd.rectangle((cx + r * 0.45, cy - r * 0.1, cx + r * 0.7, cy + r * 0.1), fill=(200, 120, 50))
            sd.ellipse((cx - r, cy - r, cx + r, cy + r), outline=(190, 30, 30), width=14)
            sd.line((cx - r * 0.7, cy - r * 0.7, cx + r * 0.7, cy + r * 0.7), fill=(190, 30, 30), width=14)
        ytop = 22 + 40 * (title.count("\n") + 1) + 10
        for k in range(lines):
            lw = (x1 - x0 - 50) * (0.55 + 0.45 * RNG.random())
            sd.rectangle((25, ytop + k * 26, 25 + lw, ytop + k * 26 + 9), fill=(120, 120, 120, 255))
        sheet = sheet.rotate(ang, expand=True, resample=Image.BICUBIC)
        shadow = Image.new("RGBA", sheet.size, (0, 0, 0, 0))
        shadow.paste((0, 0, 0, 90), mask=sheet.split()[3])
        img.paste(shadow.filter(ImageFilter.GaussianBlur(5)), (x0 + 5, y0 + 6), shadow.filter(ImageFilter.GaussianBlur(5)))
        img.paste(sheet, (x0, y0), sheet)
        px = (x0 + x1) // 2
        d.ellipse((px - 9, y0 + 2, px + 9, y0 + 20), fill=[(190, 30, 30), (30, 90, 190), (40, 150, 60)][len(title) % 3])
    d.rectangle((0, 0, w - 1, h - 1), outline=(170, 170, 170), width=14)
    return save(img, "Quadro_Avisos.png")


def screen(w=512, h=288):
    """Generic records system window (no text): title bar, form fields, a table."""
    img = Image.new("RGB", (w, h), (226, 232, 238))
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, w, 22), fill=(28, 70, 140))
    d.rectangle((0, 22, 90, h), fill=(205, 214, 224))
    for k in range(7):
        d.rectangle((10, 36 + k * 26, 80, 46 + k * 26), fill=(150, 160, 175))
    for k in range(4):
        d.rectangle((104, 34 + k * 24, 180, 42 + k * 24), fill=(120, 125, 135))
        d.rectangle((188, 30 + k * 24, 330, 46 + k * 24), fill="white", outline=(160, 170, 180))
    d.rectangle((104, 134, w - 12, h - 12), fill="white", outline=(160, 170, 180))
    d.rectangle((104, 134, w - 12, 150), fill=(200, 210, 222))
    for k in range(6):
        y = 156 + k * 20
        if k % 2:
            d.rectangle((105, y - 3, w - 13, y + 13), fill=(238, 242, 246))
        for x0, x1 in ((112, 170), (190, 300), (320, 400), (420, 480)):
            d.rectangle((x0, y + 2, x1 * (0.9 + 0.1 * RNG.random()), y + 8), fill=(110, 115, 125))
    d.rectangle((350, 34, 440, 54), fill=(40, 120, 70))
    return save(img, "Tela_Sistema.png")


def door_glass(w=512, h=1024):
    """Door leaf glass (RGBA + smoothness). UVs: u 0..0.96 across the leaf (pivot side at 0), v 0..1 up
    its 2.148 m; u >= 0.97 is the polished edge. Clear tempered glass with a slight green tint, a frosted
    film band at eye level (1.30-1.40 m above the floor, as NBR 9050 asks of glass doors), hand smudges
    around the pull (centre u 0.875, 1.10 m) and a little grime along the bottom."""
    z = lambda m: (m - 0.016) / 2.148                     # floor height (m) -> v
    col = np.zeros((h, w, 4))
    col[..., :3] = (0.80, 0.90, 0.87)
    col[..., 3] = 0.10
    smooth = np.full((h, w), 0.95)
    v = 1.0 - (np.arange(h)[:, None] + 0.5) / h
    u = (np.arange(w)[None, :] + 0.5) / w
    band = ((v > z(1.30)) & (v < z(1.40))) & (u < 0.96)
    grain = periodic_noise(1024, 200, 1)[:h, :w]
    col[band, :3] = (0.93, 0.945, 0.945)
    col[band, 3] = 0.72 + 0.04 * grain[band]
    smooth[band] = 0.35
    hand = np.zeros((h, w))
    for _ in range(34):                                    # oily hand marks near the pull
        cu, cv = RNG.normal(0.875, 0.035), RNG.normal(z(1.10), 0.11)
        ru, rv, a = RNG.uniform(0.008, 0.02), RNG.uniform(0.008, 0.02), RNG.uniform(0.3, 1.0)
        hand = np.maximum(hand, a * np.exp(-(((u - cu) / ru) ** 2 + ((v - cv) / rv) ** 2)))
    low = np.clip(1.0 - v / z(0.25), 0, 1) ** 2 * (0.5 + 0.5 * grain)   # kicks and mop splashes
    dirt = np.clip(hand + 0.6 * low, 0, 1) * (u < 0.96)
    col[..., :3] = col[..., :3] * (1 - 0.25 * dirt[..., None]) + 0.75 * 0.25 * dirt[..., None]
    col[..., 3] = np.clip(col[..., 3] + 0.10 * dirt, 0, 1)
    smooth -= 0.40 * dirt
    edge = u >= 0.97
    col[np.broadcast_to(edge, (h, w))] = (0.36, 0.58, 0.52, 0.85)
    smooth[np.broadcast_to(edge, (h, w))] = 0.8
    save(Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8), "RGBA"), "Vidro_Porta_Base.png")
    ms = np.zeros((h, w, 4))
    ms[..., 3] = np.clip(smooth, 0, 1)
    return save(Image.fromarray((ms * 255).astype(np.uint8), "RGBA"), "Vidro_Porta_MetallicSmoothness.png")


def prismatic_normal(n=128, cells=8):
    """Tile of the small square pyramids of a prismatic acrylic diffuser (normal map, tangent space)."""
    t = (np.arange(n) + 0.5) / n * cells
    fx, fy = np.meshgrid(t % 1.0 - 0.5, t % 1.0 - 0.5)
    slope = 0.55
    nx = np.where(np.abs(fx) > np.abs(fy), np.sign(fx) * slope, 0.0)
    ny = np.where(np.abs(fy) >= np.abs(fx), -np.sign(fy) * slope, 0.0)
    nrm = np.dstack((nx, ny, np.ones_like(nx)))
    nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
    return save(Image.fromarray(((nrm * 0.5 + 0.5) * 255).astype(np.uint8)), "Difusor_Prismatico_Normal.png")


if __name__ == "__main__":
    out = [floor()]
    out.append(plate("Placa_Recepcao.png", 1024, 256, "RECEPÇÃO", "Atendimento ao público", icon=icon_info))
    out.append(plate("Placa_Sanitarios.png", 768, 256, "SANITÁRIOS", icon=icon_wc))
    out.append(plate("Placa_AcessoRestrito.png", 1024, 256, "ACESSO RESTRITO", "Somente funcionários",
                     band=(170, 30, 30), icon=icon_stop, title_rgb=(150, 25, 25)))
    out.append(plate("Placa_Administracao.png", 1024, 256, "ADMINISTRAÇÃO", "Acesso restrito",
                     band=(170, 30, 30), icon=icon_stop, title_rgb=(150, 25, 25)))
    out += [accessible_sign(), accessible_floor(), clock(), notice_board(), screen(), door_glass(), prismatic_normal()]
    print("\n".join(out))

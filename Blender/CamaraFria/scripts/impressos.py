"""Printed items of the cold room (textures only; quads are placed by UnclaimedCamaraFriaAssets).
Run: python3 Blender/CamaraFria/scripts/impressos.py [out_dir]
Content comes only from Assets/Historia/Unclaimed_Puzzles_e_Storytelling_Demo.docx (P01, P07, D02):
  positions C-01..C-07; occupancy: C-01 081 (P24), C-04 082 (P25) occupied, C-03 interdicted and empty, C-02 marked
  free on the board (the real 083 was moved there without a record, so its door card stays blank).
No names, dates, case numbers or reasons are added. Handwriting: Mauro's hand (GochiHand, as in the rest area),
because he records the movements (D02, "M. Nunes"). Fonts: Noto Sans / Nimbus Sans / GochiHand (SIL OFL).
  Posicao_C-0N.png     engraved stainless position plate 120 x 50 mm (5 px/mm)
  Ficha_C-01/04.png    record card in the polycarbonate holder 164 x 104 mm: printed template + handwriting
  Ficha_vazia.png      same template, nothing written (C-02 and C-03)
  Interdicao_C-03.png  large interdiction label taped to the C-03 door, 300 x 200 mm
  FormularioConferencia.png blank seven-position checking form, A4 portrait at 4 px/mm
  RotuloEtiquetas.png dispenser label, 180 x 60 mm at 5 px/mm
  QuadroOcupacao.png   seven-row whiteboard 600 x 450 mm (2 px/mm): printed grid + marker handwriting
  VisorControlador.png controller LCD face 90 x 50 mm: set and actual temperature within the reference range (0/2 C)
  Pulseira_083.png     Leo's identification wristband (P07: "a pulseira de Leo continua com 083"), white vinyl
                       ~170 x 22 mm; the printed label sits at u = 0.5 (top of the wrist), the band UV runs around it.
"""
import os, sys, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
OUT = sys.argv[1] if len(sys.argv) > 1 else ROOT + "/Assets/Modelos/CamaraFria/Texturas/Etiquetas"
BOLD, REG = "/usr/share/fonts/noto/NotoSans-Bold.ttf", "/usr/share/fonts/noto/NotoSans-Regular.ttf"
HAND = ROOT + "/Blender/Recepcao/fontes/GochiHand-Regular.ttf"
rnd = random.Random(1103)

def noise(im, amount=8, blur=4, seed=0):
    w, h = im.size
    n = Image.effect_noise((max(1, w // 6), max(1, h // 6)), 64).resize((w, h)).filter(ImageFilter.GaussianBlur(blur))
    base = im.convert("RGB"); px = base.load(); npx = n.load()
    for y in range(h):
        for x in range(w):
            d = (npx[x, y] - 128) * amount // 128
            r, g, b = px[x, y]; px[x, y] = (max(0, min(255, r + d)), max(0, min(255, g + d)), max(0, min(255, b + d)))
    return base

def rounded_alpha(size, r):
    a = Image.new("L", size, 0); ImageDraw.Draw(a).rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), r, fill=255); return a

def hand(d, xy, text, size, fill=(28, 40, 70), angle=0.0, im=None):
    f = ImageFont.truetype(HAND, size)
    if angle and im is not None:
        bbox = d.textbbox((0, 0), text, font=f); w, h = bbox[2] + 20, bbox[3] + 20
        layer = Image.new("RGBA", (w, h), (0, 0, 0, 0)); ImageDraw.Draw(layer).text((10, 5), text, font=f, fill=fill + (235,))
        layer = layer.rotate(angle, expand=True, resample=Image.BICUBIC); im.paste(layer, xy, layer)
    else:
        d.text(xy, text, font=f, fill=fill)

def plate(pos):
    W, H = 600, 250
    im = Image.new("RGB", (W, H), (150, 154, 152))
    # Brushed grain along the plate.
    g = Image.effect_noise((W, 8), 40).resize((W, H)).filter(ImageFilter.GaussianBlur(1))
    im = Image.blend(im, Image.merge("RGB", (g, g, g)), .12); d = ImageDraw.Draw(im)
    d.rounded_rectangle((4, 4, W - 5, H - 5), 18, outline=(110, 114, 112), width=4)
    f = ImageFont.truetype(BOLD, 150); bb = d.textbbox((0, 0), pos, font=f)
    x, y = (W - bb[2]) // 2, (H - bb[3]) // 2 - 18
    d.text((x + 2, y + 3), pos, font=f, fill=(190, 194, 192)); d.text((x, y), pos, font=f, fill=(24, 26, 28))   # engraved, filled black
    for cx in (34, W - 34):
        d.ellipse((cx - 11, H // 2 - 11, cx + 11, H // 2 + 11), fill=(120, 124, 122), outline=(80, 84, 82), width=2)
    im = im.convert("RGBA"); im.putalpha(rounded_alpha((W, H), 20))
    p = OUT + "/Posicao_" + pos + ".png"; im.save(p); return p

def card(name, reg=None, bag=None):
    W, H = 820, 520
    im = noise(Image.new("RGB", (W, H), (238, 234, 222)), 7, 6); d = ImageDraw.Draw(im)
    d.rectangle((14, 14, W - 15, H - 15), outline=(90, 92, 96), width=3)
    d.text((40, 30), "IDENTIFICAÇÃO DO COMPARTIMENTO", font=ImageFont.truetype(BOLD, 34), fill=(40, 42, 46))
    d.line((40, 84, W - 40, 84), fill=(90, 92, 96), width=3)
    fl = ImageFont.truetype(REG, 30)
    for i, label in enumerate(("REGISTRO", "PERTENCES")):
        y = 160 + i * 150
        d.text((40, y), label, font=fl, fill=(60, 62, 66))
        d.line((260, y + 52, W - 50, y + 52), fill=(120, 120, 118), width=2)
    if reg: hand(d, (300, 112), reg, 96, angle=1.5, im=im)
    if bag: hand(d, (300, 262), bag, 96, angle=-1.0, im=im)
    p = OUT + "/" + name + ".png"; im.save(p); return p

def interdiction():
    W, H = 900, 600
    im = noise(Image.new("RGB", (W, H), (244, 241, 233)), 6, 8); d = ImageDraw.Draw(im)
    # Red/white hatched border, as on site barrier labels.
    stripe = Image.new("RGB", (W, H), (244, 241, 233)); sd = ImageDraw.Draw(stripe)
    for k in range(-H, W, 60): sd.polygon([(k, 0), (k + 30, 0), (k + 30 + H, H), (k + H, H)], fill=(178, 32, 34))
    mask = Image.new("L", (W, H), 255); ImageDraw.Draw(mask).rectangle((44, 44, W - 45, H - 45), fill=0)
    im.paste(stripe, (0, 0), mask); d = ImageDraw.Draw(im)
    def centered(text, font, y, fill):
        bb = d.textbbox((0, 0), text, font=font); d.text(((W - bb[2]) // 2, y), text, font=font, fill=fill)
    centered("INTERDITADA", ImageFont.truetype(BOLD, 104), 100, (170, 26, 28))
    centered("C-03", ImageFont.truetype(BOLD, 150), 236, (28, 28, 30))
    centered("NÃO UTILIZAR", ImageFont.truetype(BOLD, 64), 430, (40, 40, 44))
    # Two strips of yellowed clear tape across the top corners, slightly crooked.
    for x0, ang in ((-20, 14), (W - 230, -12)):
        tape = Image.new("RGBA", (260, 70), (232, 226, 190, 120)).rotate(ang, expand=True)
        im.paste(tape, (x0, -10), tape)
    p = OUT + "/Interdicao_C-03.png"; im.save(p); return p

def board():
    W, H = 1200, 900
    im = noise(Image.new("RGB", (W, H), (236, 238, 236)), 5, 12); d = ImageDraw.Draw(im)
    # Faint ghosting of erased marker (used board), kept unreadable on purpose.
    ghost = Image.new("RGBA", (W, H), (0, 0, 0, 0)); gd = ImageDraw.Draw(ghost)
    for _ in range(18):
        x, y = rnd.randint(380, 1100), rnd.randint(300, 820)
        gd.line((x, y, x + rnd.randint(60, 220), y + rnd.randint(-8, 8)), fill=(70, 80, 100, 18), width=rnd.randint(8, 16))
    im.paste(ghost, (0, 0), ghost.filter(ImageFilter.GaussianBlur(5))); d = ImageDraw.Draw(im)
    d.rectangle((30, 30, W - 31, 150), fill=(40, 70, 96))
    d.text((60, 52), "CÂMARA FRIA — OCUPAÇÃO", font=ImageFont.truetype(BOLD, 64), fill=(240, 242, 240))
    cols = (60, 340, 680, W - 60)
    for x, label in zip(cols, ("POSIÇÃO", "REGISTRO", "SITUAÇÃO")):
        d.text((x + 16, 172), label, font=ImageFont.truetype(BOLD, 40), fill=(40, 70, 96))
    for k in range(8):
        y = 236 + k * 86; d.line((60, y, W - 60, y), fill=(40, 70, 96), width=4)
    for x in cols: d.line((x, 166, x, 236 + 7 * 86), fill=(40, 70, 96), width=4)
    rows = (("C-01", "081", "ocupada"), ("C-02", "—", "livre"), ("C-03", "—", "interditada"), ("C-04", "082", "ocupada"),
            ("C-05", "—", "livre"), ("C-06", "—", "livre"), ("C-07", "—", "livre"))
    fp = ImageFont.truetype(BOLD, 48)
    for k, (pos, reg, sit) in enumerate(rows):
        y = 236 + k * 86
        d.text((cols[0] + 40, y + 17), pos, font=fp, fill=(28, 30, 34))
        hand(d, (cols[1] + 70, y + 8), reg, 67, fill=(24, 42, 110), angle=rnd.uniform(-2, 2), im=im)
        hand(d, (cols[2] + 30, y + 8), sit, 57 if len(sit) > 8 else 65, fill=(150, 30, 30) if sit == "interditada" else (24, 42, 110),
             angle=rnd.uniform(-2, 2), im=im)
    p = OUT + "/QuadroOcupacao.png"; im.save(p); return p

def display():
    W, H = 450, 250
    im = Image.new("RGB", (W, H), (14, 18, 16)); d = ImageDraw.Draw(im)
    # Seven-segment-like readout (bold condensed), actual temperature large, set point small; no alarm icons lit.
    f = ImageFont.truetype("/usr/share/fonts/noto/NotoSansMono-Bold.ttf", 130) if os.path.exists("/usr/share/fonts/noto/NotoSansMono-Bold.ttf") else ImageFont.truetype(BOLD, 130)
    d.text((40, 36), "1.8", font=f, fill=(120, 230, 160))
    d.text((300, 60), "°C", font=ImageFont.truetype(BOLD, 60), fill=(120, 230, 160))
    fs = ImageFont.truetype(BOLD, 30)
    d.text((40, 196), "SET 1.0", font=fs, fill=(70, 140, 100))
    for i, lab in enumerate(("COMP", "DEF", "FAN", "ALM")):
        on = lab in ("COMP", "FAN")
        d.text((196 + i * 62, 202), lab, font=ImageFont.truetype(BOLD, 17), fill=(110, 210, 150) if on else (30, 44, 36))
    p = OUT + "/VisorControlador.png"; im.save(p); return p

def wristband():
    W, H = 1536, 200                     # around x across (u = 0.5 is the top of the wrist)
    im = noise(Image.new("RGB", (W, H), (236, 236, 232)), 5, 3); d = ImageDraw.Draw(im)
    # Embossed rib along both edges of the band and the snap holes at one end (under the wrist).
    for y in (10, H - 12): d.line((0, y, W, y), fill=(214, 214, 210), width=4)
    for x in (70, 120, 170): d.ellipse((x - 9, H // 2 - 9, x + 9, H // 2 + 9), fill=(200, 200, 196), outline=(170, 170, 166), width=2)
    # Thermal-printed insert at the centre: number large, name small, nothing else invented.
    x0, x1 = W // 2 - 300, W // 2 + 300
    d.rectangle((x0, 26, x1, H - 28), fill=(250, 250, 247), outline=(190, 190, 186), width=2)
    f = ImageFont.truetype(BOLD, 112); bb = d.textbbox((0, 0), "083", font=f)
    d.text((x0 + 26, (H - bb[3]) // 2 - 12), "083", font=f, fill=(22, 22, 24))
    d.text((x0 + 268, 62), "LÉO MARTINS", font=ImageFont.truetype(BOLD, 40), fill=(30, 30, 32))
    d.line((x0 + 268, 118, x1 - 26, 118), fill=(120, 120, 118), width=2)
    im = im.filter(ImageFilter.GaussianBlur(.6))
    p = OUT + "/Pulseira_083.png"; im.save(p); return p


def conference_form():
    im = Image.new("RGB", (840, 1188), (248, 247, 242)); d = ImageDraw.Draw(im)
    ink = (40, 42, 46)
    title = "CONFERÊNCIA DE POSIÇÕES — CÂMARA FRIA"
    size = 34
    while d.textbbox((0, 0), title, font=ImageFont.truetype(BOLD, size))[2] > 748:
        size -= 1
    d.text((46, 60), title, font=ImageFont.truetype(BOLD, size), fill=ink)
    f = ImageFont.truetype(REG, 26)
    for x, label, end in ((46, "Data", 355), (440, "Turno", 794)):
        d.text((x, 138), label, font=f, fill=ink)
        d.line((x + 85, 173, end, 173), fill=ink, width=2)
    xs = (46, 210, 430, 794); top, row = 236, 100
    for x in xs:
        d.line((x, top, x, top + 8 * row), fill=ink, width=2)
    for k in range(9):
        d.line((xs[0], top + k * row, xs[-1], top + k * row), fill=ink, width=2)
    for x, label in zip(xs, ("Posição", "Registro", "Conferido por")):
        d.text((x + 16, top + 32), label, font=ImageFont.truetype(BOLD, 25), fill=ink)
    for i in range(7):
        d.text((62, top + (i + 1) * row + 32), "C-%02d" % (i + 1), font=f, fill=ink)
    path = OUT + "/FormularioConferencia.png"; im.save(path); return path


def label_dispenser():
    im = Image.new("RGB", (900, 300), (238, 239, 235)); d = ImageDraw.Draw(im)
    d.rectangle((12, 12, 887, 287), outline=(80, 84, 84), width=3)
    for text, y, size in (("ETIQUETAS DE IDENTIFICAÇÃO", 75, 45), ("em branco", 176, 36)):
        font = ImageFont.truetype(BOLD if y == 75 else REG, size)
        bb = d.textbbox((0, 0), text, font=font)
        d.text(((900 - bb[2]) / 2, y), text, font=font, fill=(40, 42, 46))
    path = OUT + "/RotuloEtiquetas.png"; im.save(path); return path


def wear_decals():
    folder = os.path.join(os.path.dirname(OUT), "Desgaste")
    os.makedirs(folder, exist_ok=True)
    rng = random.Random(705)
    im = Image.new("RGBA", (2048, 1024), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    # Longitudinal direction is U (2.4 m): parallel transfer scratches concentrated along the centre.
    for i in range(180):
        y = int(max(20, min(1004, rng.gauss(512, 135))))
        x = rng.randint(10, 1950); length = rng.randint(36, 460)
        d.line((x, y, min(2028, x + length), y + rng.randint(-2, 2)), fill=(52, 55, 54, rng.randint(28, 105)), width=rng.choice((1, 2, 2)))
    for cy in (380, 644):
        for _ in range(14):
            x, y = rng.randint(50, 1750), cy + rng.randint(-6, 6)
            d.line((x, y, min(2000, x + rng.randint(90, 300)), y + rng.randint(-1, 1)), fill=(58, 60, 59, rng.randint(14, 44)), width=rng.randint(3, 7))
    im = im.filter(ImageFilter.GaussianBlur(.7))
    path = folder + "/RiscosTransferencia.png"; im.save(path); print(path)
    alpha = Image.new("L", (512, 512), 0); ad = ImageDraw.Draw(alpha)
    for _ in range(38):
        x, y = rng.gauss(255, 43), rng.gauss(265, 54)
        ad.ellipse((x - 9, y - 24, x + 9, y + 24), fill=rng.randint(40, 150))
    alpha = alpha.filter(ImageFilter.GaussianBlur(12))
    im = Image.new("RGBA", (512, 512), (194, 199, 196, 0)); im.putalpha(alpha)
    path = folder + "/MarcasPuxador.png"; im.save(path); print(path)

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    # Existing noisy paper and brushed plates are approved bitmaps: never regenerate them on a routine run.
    for i in range(1, 8):
        pos = "C-%02d" % i
        if not os.path.exists(OUT + "/Posicao_" + pos + ".png"):
            print(plate(pos))
    for name, args in (("Ficha_C-01", ("081", "P24")), ("Ficha_C-04", ("082", "P25")), ("Ficha_vazia", ())):
        if not os.path.exists(OUT + "/" + name + ".png"):
            print(card(name, *args))
    for name, fn in (("Interdicao_C-03", interdiction), ("VisorControlador", display), ("Pulseira_083", wristband)):
        if not os.path.exists(OUT + "/" + name + ".png"):
            print(fn())
    print(board()); print(conference_form()); print(label_dispenser()); wear_decals()

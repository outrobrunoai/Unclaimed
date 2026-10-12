# Textures of the first puzzle (reception), drawn with Pillow (system Python):
#   python3 Blender/Recepcao/scripts/texturas_puzzle.py
# Padlock and key (Assets/UnclaimedPrototype/Recepcao/Texturas): digit strip of the dials, key tag label.
# The computer's interface art is made by estacao_interface.py; the monitor's idle screen
# (Computador/Tela_Area_de_Trabalho.png) is a capture of the live interface (see ComputadorRecepcao.cs).
import json
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "..", "..", "..")
PC = os.path.join(ROOT, "Assets", "UnclaimedPrototype", "Computador")
TEX = os.path.join(ROOT, "Assets", "UnclaimedPrototype", "Recepcao", "Texturas")
FONT, FONT_B = "/usr/share/fonts/noto/NotoSans-Regular.ttf", "/usr/share/fonts/noto/NotoSans-Bold.ttf"



def font(size, bold=False):
    return ImageFont.truetype(FONT_B if bold else FONT, size)


def digit_strip(cw=96, ch=64):
    """10 cells (0 at the top) of white digits on black knurled plastic, for the padlock dials."""
    im = Image.new("RGB", (cw, ch * 10), (18, 18, 20))
    d = ImageDraw.Draw(im)
    for k in range(10):
        y0 = k * ch
        for x in range(0, cw, 6):   # knurling across the rim
            d.line((x, y0, x, y0 + ch), fill=(28, 28, 31), width=2)
        d.line((0, y0, cw, y0), fill=(6, 6, 7), width=3)
        d.rectangle((cw * 0.22, y0 + 6, cw * 0.78, y0 + ch - 6), fill=(14, 14, 16))
        d.text((cw / 2, y0 + ch / 2 + 1), str(k), font=font(int(ch * 0.78), True), fill=(236, 236, 230), anchor="mm")
    im.save(os.path.join(TEX, "Cadeado_Digitos.png"))
    return im


def key_tag(w=512, h=216):
    """Label of the key tag: white plastic, red frame, 'CIRCULAÇÃO'. The bottom rows stay plain plastic
    (the tag's sides are mapped there)."""
    im = Image.new("RGB", (w, h), (232, 230, 222))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((14, 14, w - 70, h - 30), radius=14, outline=(170, 30, 30), width=8)
    d.text(((w - 56) / 2, h * 0.40), "CIRCULAÇÃO", font=font(54, True), fill=(30, 30, 34), anchor="mm")
    d.text(((w - 56) / 2, h * 0.68), "acesso restrito", font=font(30), fill=(90, 30, 30), anchor="mm")
    im = im.filter(ImageFilter.GaussianBlur(0.6))
    im.save(os.path.join(TEX, "Etiqueta_Circulacao.png"))
    return im


if __name__ == "__main__":
    digit_strip()
    key_tag()
    print("ok")

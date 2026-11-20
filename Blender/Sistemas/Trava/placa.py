"""Printed face of the cold-room emergency release sign (red, white letters, 2:1).
Run: python Blender/Sistemas/Trava/placa.py   (Pillow) -> Assets/Modelos/CamaraFria/Texturas/TravaPlaca.png"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
OUT = ROOT + "/Assets/Modelos/CamaraFria/Texturas/TravaPlaca.png"
FONTE = "/usr/share/fonts/liberation/LiberationSans-Bold.ttf"

W, H = 1024, 512
img = Image.new("RGB", (W, H), (176, 22, 20))
d = ImageDraw.Draw(img)
d.rectangle((14, 14, W - 15, H - 15), outline=(240, 236, 230), width=10)


def centro(texto, y, tamanho):
    f = ImageFont.truetype(FONTE, tamanho)
    w = d.textlength(texto, font=f)
    d.text(((W - w) / 2, y), texto, font=f, fill=(244, 240, 234))


centro("SAÍDA DE", 52, 118)
centro("EMERGÊNCIA", 176, 118)
# Arrow pointing down at the lever, then the instruction.
d.polygon([(W / 2 - 46, 330), (W / 2 + 46, 330), (W / 2, 384)], fill=(244, 240, 234))
centro("PUXE A ALAVANCA", 400, 70)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT)
print("placa", OUT)

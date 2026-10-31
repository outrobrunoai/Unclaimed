"""Printed pieces of the rest room: clock dial and locker name cards (Pillow, no generated imagery)."""
import math, os
from PIL import Image, ImageDraw, ImageFont
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../..'))
OUT = ROOT + '/Assets/UnclaimedPrototype/Descanso'
SANS = '/usr/share/fonts/noto/NotoSans-Bold.ttf'
HAND = None
for c in ('/usr/share/fonts/TTF/Caveat-Regular.ttf', '/usr/share/fonts/truetype/caveat/Caveat-Regular.ttf'):
    if os.path.exists(c): HAND = c


def font(p, size):
    for q in (p, '/usr/share/fonts/TTF/DejaVuSans-Bold.ttf'):
        try: return ImageFont.truetype(q, size)
        except Exception: pass
    return ImageFont.load_default()


def dial():
    S = 1024; c = S / 2
    im = Image.new('RGBA', (S, S), (236, 234, 226, 255)); d = ImageDraw.Draw(im)
    for i in range(60):
        a = math.radians(i * 6); big = i % 5 == 0
        r0, r1 = (0.80, 0.92) if big else (0.87, 0.92)
        w = 10 if big else 4
        d.line((c + math.sin(a) * r0 * c, c - math.cos(a) * r0 * c, c + math.sin(a) * r1 * c, c - math.cos(a) * r1 * c), fill=(25, 25, 25, 255), width=w)
    f = font(SANS, 110)
    for n in range(1, 13):
        a = math.radians(n * 30); x, y = c + math.sin(a) * 0.66 * c, c - math.cos(a) * 0.66 * c
        d.text((x, y), str(n), font=f, fill=(25, 25, 25, 255), anchor='mm')
    im.save(OUT + '/Relogio_Mostrador.png')


def card(name, hand=True):
    im = Image.new('RGBA', (624, 208), (240, 238, 228, 255)); d = ImageDraw.Draw(im)
    d.rectangle((0, 0, 623, 207), outline=(200, 198, 188, 255), width=3)
    f = font(HAND if hand and HAND else SANS, 92 if hand and HAND else 70)
    d.text((312, 108), name, font=f, fill=(32, 44, 84, 255), anchor='mm')
    im.save(OUT + '/Etiquetas/' + name.replace(' ', '_').replace('.', '') + '.png')


if __name__ == '__main__':
    dial()
    for n in ('H. DUARTE', 'M. NUNES', 'NINA', 'CELSO', 'R. MATOS', 'J. PRADO', 'P. SOUZA', 'ESTAGIÁRIO', 'LIVRE'):
        card(n)

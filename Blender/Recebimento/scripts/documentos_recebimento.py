"""Receiving-room papers of stage 2 (texts: docs/textos/etapa2-documentos.md): D02 Mauro's whiteboard, D05 Leo's
intake photo, D06 the note stapled to it, D08 the night-removal sign on the patio door.
Also the shared printed-photo helper used by the archive's D12.
  python3 Blender/Recebimento/scripts/documentos_recebimento.py     (then Tools > Unclaimed > Recebimento > Build Documentos)"""
import json
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '../../Recepcao/scripts')))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '../../Descanso/scripts')))
import quadro_avisos as qa
import documentos_descanso as dd

DIR = os.path.join(qa.ROOT, 'Assets/UnclaimedPrototype/Recebimento/Documentos')
FOTOS = os.path.join(HERE, '..', 'fotos')
mm, PX = qa.mm, qa.PX
INK = (28, 28, 30, 255)


# ---------------------------------------------------------------- shared: printed photo with a white margin

def photo_area(path, w, h):
    """The photo itself: the rendered image when it exists (cropped to fill), otherwise a neutral grey placeholder."""
    if os.path.exists(path):
        src = Image.open(path).convert('RGB')
        k = max(w / src.width, h / src.height)
        src = src.resize((max(w, int(src.width * k + .5)), max(h, int(src.height * k + .5))), Image.LANCZOS)
        x0, y0 = (src.width - w) // 2, (src.height - h) // 2
        return src.crop((x0, y0, x0 + w, y0 + h)).convert('RGBA')
    g = np.linspace(118, 92, h)[:, None] * np.ones((1, w))
    g = g + np.random.default_rng(5).normal(0, 4, g.shape)
    img = Image.fromarray(np.clip(g, 0, 255).astype(np.uint8)).convert('RGBA')
    d = ImageDraw.Draw(img)
    f = ImageFont.truetype(dd.SANS_B, int(h * .09))
    t = 'FOTO'
    d.text(((w - f.getlength(t)) / 2, h * .44), t, font=f, fill=(140, 140, 140, 255))
    return img


def photo_print(doc, image_path, margin_bottom_mm):
    """A glossy print: 5 mm white border, the picture, and a wider white strip at the bottom. Returns (img, strip top px)."""
    img, W, H = dd.new_page(doc)
    b = int(mm(5))
    ph_h = int(H - b - mm(margin_bottom_mm))
    pic = photo_area(image_path, W - 2 * b, ph_h - b)
    img.alpha_composite(pic, (b, b))
    ImageDraw.Draw(img).rectangle((b, b, W - b - 1, ph_h - 1), outline=(70, 70, 70, 255), width=1)
    return img, ph_h


# ---------------------------------------------------------------- D02 whiteboard (rendered at its own scale)

BOARD_PX = 2.2   # px per mm: a 900 mm board stays under 2048 px


def d02_quadro(doc):
    w, h = doc['tamanho']
    W, H = int(w * 1000 * BOARD_PX), int(h * 1000 * BOARD_PX)
    rng = np.random.default_rng(21)
    base = np.ones((H, W, 3)) * np.array([238, 240, 240])
    yy, xx = np.mgrid[0:H, 0:W]
    base -= (yy / H * 6)[..., None]                                    # light from the ceiling
    for _ in range(9):                                                 # old marker ghosts and eraser smears
        cx, cy, sx, sy = rng.uniform(0, W), rng.uniform(0, H), rng.uniform(60, 260), rng.uniform(20, 70)
        blob = np.exp(-(((xx - cx) / sx) ** 2 + ((yy - cy) / sy) ** 2))
        base -= blob[..., None] * np.array([10, 9, 4]) * rng.uniform(.5, 1.2)
    base += rng.normal(0, 1.3, base.shape)
    img = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8)).convert('RGBA')

    hand, ink = 'GochiHand-Regular.ttf', (26, 44, 120, 255)
    red = (150, 34, 34, 255)
    f_title = ImageFont.truetype(os.path.join(dd.FONTS, hand), int(52 * BOARD_PX))
    f_line = ImageFont.truetype(os.path.join(dd.FONTS, hand), int(34 * BOARD_PX))
    f_sig = ImageFont.truetype(os.path.join(dd.FONTS, hand), int(34 * BOARD_PX))
    layer = Image.new('RGBA', img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    x0, y = 60 * BOARD_PX, 40 * BOARD_PX
    d.text((x0, y), 'CORPOS DE HOJE — 18/11', font=f_title, fill=ink)
    tw = f_title.getlength('CORPOS DE HOJE — 18/11')
    d.line((x0, y + 66 * BOARD_PX, x0 + tw, y + 70 * BOARD_PX), fill=ink, width=int(3 * BOARD_PX))
    y += 105 * BOARD_PX
    rows = [('081 · João Ferraz', 'gaveta C-01'), ('082 · Célia Prado', 'gaveta C-04'),
            ('083 · Léo Martins', 'levado para a sala de autópsia'), ('', 'às 22h28.')]
    r = random.Random(4)
    col = x0 + f_line.getlength('082 · Célia Prado') + 64 * BOARD_PX   # second column, after the hand-drawn arrow
    for left, right in rows:
        jx = r.uniform(-6, 6)
        if left:
            d.text((x0 + jx, y), left, font=f_line, fill=ink)
            ax0, ax1, ay = x0 + jx + f_line.getlength(left) + 14 * BOARD_PX, col - 16 * BOARD_PX, y + 26 * BOARD_PX
            d.line((ax0, ay, ax1, ay - 2), fill=ink, width=int(3 * BOARD_PX))
            d.line((ax1 - 13 * BOARD_PX, ay - 10 * BOARD_PX, ax1, ay - 2, ax1 - 13 * BOARD_PX, ay + 8 * BOARD_PX), fill=ink, width=int(3 * BOARD_PX))
        d.text((col + jx, y), right, font=f_line, fill=ink)
        y += (62 if left else 70) * BOARD_PX
    y += 18 * BOARD_PX
    d.text((x0, y), 'A gaveta C-03 está com defeito. Não usar.', font=f_line, fill=red)
    ul = f_line.getlength('A gaveta C-03 está com defeito. Não usar.')
    d.line((x0, y + 52 * BOARD_PX, x0 + ul, y + 50 * BOARD_PX), fill=red, width=int(2.5 * BOARD_PX))
    d.text((W - 210 * BOARD_PX, H - 85 * BOARD_PX), '— Mauro', font=f_sig, fill=ink)
    layer = layer.rotate(-.6, resample=Image.BICUBIC, center=(0, 0))
    # Marker ink is not perfectly opaque: slight thinning in a few strokes.
    a = np.asarray(layer.getchannel('A')).astype(float)
    a *= np.clip(.86 + rng.normal(0, .06, a.shape), .7, 1)
    layer.putalpha(Image.fromarray(a.astype(np.uint8)))
    img.alpha_composite(layer)
    return img


# ---------------------------------------------------------------- D05 intake photo, D06 stapled note

def d05_foto_entrada(doc):
    img, strip = photo_print(doc, os.path.join(FOTOS, 'd05_tala.png'), 19)
    W, H = img.size
    d = ImageDraw.Draw(img)
    f_b, f = ImageFont.truetype(dd.SANS_B, int(mm(3.3))), ImageFont.truetype(dd.SANS, int(mm(3.1)))
    x, y = mm(6), strip + mm(3.2)
    d.text((x, y), 'Registro 083 · Léo Martins · entrada às 21h50.', font=f_b, fill=INK)
    y += f_b.size * 1.45
    for ln in qa.wrap('Tala no braço esquerdo. Bolsa de pertences P26, com costura remendada.', f, W - 2 * x):
        d.text((x, y), ln, font=f, fill=INK)
        y += f.size * 1.35
    return img


def staple(img, x, y, length_mm, rot):
    """A steel staple through the corner: two short bright strokes with a dark shadow."""
    t = Image.new('RGBA', (int(mm(length_mm)) + 8, 14), (0, 0, 0, 0))
    d = ImageDraw.Draw(t)
    d.line((4, 9, t.width - 4, 9), fill=(70, 70, 72, 150), width=4)
    d.line((4, 6, t.width - 4, 6), fill=(196, 198, 200, 255), width=4)
    d.line((4, 5, t.width - 4, 5), fill=(240, 240, 240, 255), width=1)
    t = t.rotate(rot, expand=True, resample=Image.BICUBIC)
    img.alpha_composite(t, (int(x - t.width / 2), int(y - t.height / 2)))


def d06_bilhete_tala(doc):
    img, W, H = dd.new_page(doc)
    dd.pen_text(img, 'A tala do braço esquerdo fica no corpo até o exame. Não retirar.', mm(5), mm(7), 'mauro', 5.0,
                rot=.8, width_mm=64, leading=1.12, seed=31)
    dd.pen_text(img, '— Mauro', W - mm(26), H - mm(11), 'mauro', 5.0, seed=32)
    staple(img, mm(8), mm(4), 9, 35)
    return img


# ---------------------------------------------------------------- D08 laminated sign on the patio door

def d08_aviso_retiradas(doc):
    img, W, H = dd.new_page(doc)
    d = ImageDraw.Draw(img)
    m = mm(16)
    band = (176, 32, 30, 255)
    d.rectangle((0, 0, W, mm(46)), fill=band)
    f_att = ImageFont.truetype(dd.SANS_B, int(mm(22)))
    d.text(((W - f_att.getlength('ATENÇÃO')) / 2, mm(9)), 'ATENÇÃO', font=f_att, fill=(255, 255, 255, 255))
    y = mm(64)
    f_b = ImageFont.truetype(dd.SANS_B, int(mm(12.5)))
    f = ImageFont.truetype(dd.SANS, int(mm(11)))
    for text, ft in (('Depois das 22h30, nenhum corpo ou equipamento sai do prédio sem um funcionário do necrotério presente.', f_b),
                     ('A empresa de transporte não pode entrar sozinha.', f)):
        for ln in qa.wrap(text, ft, W - 2 * m):
            d.text((m, y), ln, font=ft, fill=INK)
            y += ft.size * 1.28
        y += mm(10)
    f_s = ImageFont.truetype(dd.SANS, int(mm(6.4)))
    d.text((W - m - f_s.getlength('— Coordenação'), H - mm(22)), '— Coordenação', font=f_s, fill=(60, 60, 64, 255))
    d.rectangle((mm(3), mm(3), W - mm(3), H - mm(3)), outline=(176, 32, 30, 255), width=int(mm(1.2)))
    return img


RENDER = {'d02_quadro': d02_quadro, 'd05_foto_entrada': d05_foto_entrada, 'd06_bilhete_tala': d06_bilhete_tala,
          'd08_aviso_retiradas': d08_aviso_retiradas}


def main():
    qa.DOCS = DIR
    data = json.load(open(os.path.join(DIR, 'DocumentosRecebimento.json'), encoding='utf-8'))
    for doc in data['documentos']:
        img = qa.finish(RENDER[doc['id']](doc), doc.get('acabamento', 'papel'))
        img.save(os.path.join(DIR, doc['id'] + '.png'))
        qa.relief(doc)
        print(f"{doc['id']}: {img.size}")


if __name__ == '__main__':
    main()

"""Rest-room papers. Each paper is drawn here (support-specific layouts: notebook sheet, torn scrap, photocopied memo,
printed form with pen marks); the paper finish and relief come from the notice-board renderer.
Content sources: Assets/Historia (Puzzles e Storytelling Demo: "bilhete do cafe", "marmita vazia", D14 "Escala e encerramento").
  python3 Blender/Descanso/scripts/documentos_descanso.py     (then Tools > Unclaimed > Descanso > Build Documentos)"""
import json
import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '../../Recepcao/scripts')))
import quadro_avisos as qa

FONTS = os.path.normpath(os.path.join(HERE, '../../Recepcao/fontes'))
DIR = os.path.join(qa.ROOT, 'Assets/UnclaimedPrototype/Descanso/Documentos')
PX = qa.PX
mm = qa.mm
# One hand per person: Nina (round, tidy), Mauro (upright, heavy), Helena (small, fast), each with its own pen.
HANDS = {
    'nina': ('PatrickHand-Regular.ttf', (34, 36, 40, 255)),
    'mauro': ('GochiHand-Regular.ttf', (28, 58, 128, 255)),
    'helena': ('ReenieBeanie.ttf', (20, 24, 46, 255)),
    'cleaner': ('Kalam-Regular.ttf', (30, 60, 150, 255)),
    # Stage 2: Irene keeps the Caveat hand of her archive notes; Célia and João get their own.
    'irene': ('Caveat-Variable.ttf', (24, 26, 30, 255)),
    'celia': ('ShadowsIntoLight.ttf', (22, 22, 26, 255)),
    'joao': ('Kalam-Regular.ttf', (22, 38, 96, 255)),
}
SANS = '/usr/share/fonts/noto/NotoSans-Regular.ttf'
SANS_B = '/usr/share/fonts/noto/NotoSans-Bold.ttf'
SANS_C = '/usr/share/fonts/noto/NotoSansDisplay-Condensed.ttf'
_f = {}


def font(path, size_mm):
    key = (path, round(size_mm * PX))
    if key not in _f:
        _f[key] = ImageFont.truetype(path if os.path.isabs(path) else os.path.join(FONTS, path), key[1])
    return _f[key]


def wrap(text, f, width):
    return qa.wrap(text, f, width)


def pen_text(img, text, x, y, hand, size_mm, rot=0.0, width_mm=None, leading=1.18, jitter=0.0, seed=1):
    """Handwriting block, wrapped to width_mm, top-left at (x, y) px; rotated about its top-left corner. Returns height px."""
    path, ink = HANDS[hand]
    f = font(path, size_mm)
    lines = wrap(text, f, mm(width_mm)) if width_mm else text.split('\n')
    lh = f.size * leading
    w = int(max(f.getlength(l) for l in lines)) + 24
    layer = Image.new('RGBA', (w, int(lh * len(lines) + f.size)), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    rng = random.Random(seed)
    for i, line in enumerate(lines):
        d.text((8, i * lh + rng.uniform(-jitter, jitter)), line, font=f, fill=ink)
    if rot:
        layer = layer.rotate(rot, expand=True, resample=Image.BICUBIC, center=(0, 0))
    img.alpha_composite(layer, (int(x - 8), int(y)))
    return lh * len(lines)


def tick(img, x, y, size_mm, ink, seed):
    """A hand-drawn check mark in a table cell."""
    rng = random.Random(seed)
    s = mm(size_mm)
    pts = [(x - s * .45 + rng.uniform(-1, 1), y + s * .05), (x - s * .12, y + s * .42 + rng.uniform(-1, 1)), (x + s * .55 + rng.uniform(-1.5, 1.5), y - s * .48)]
    ImageDraw.Draw(img).line(pts, fill=ink, width=max(2, int(mm(.42))), joint='curve')


def scribble(img, x, y, w_mm, ink, seed):
    """Initials as a quick loop-and-stroke (rubrica)."""
    rng = random.Random(seed)
    pts, n, w = [], 22, mm(w_mm)
    for i in range(n):
        t = i / (n - 1)
        pts.append((x + t * w + math.sin(t * 17 + seed) * w * .08, y - abs(math.sin(t * 9 + seed)) * mm(2.4) + rng.uniform(-1, 1)))
    ImageDraw.Draw(img).line(pts, fill=ink, width=max(2, int(mm(.32))), joint='curve')


def torn_left(img, seed):
    """Sheet torn from a spiral notebook: ragged edge with the remains of the punched holes."""
    w, h = img.size
    a = Image.new('L', (w, h), 255)
    d = ImageDraw.Draw(a)
    rng = random.Random(seed)
    step = mm(7.0)
    for i in range(int(h / step) + 1):
        cy = i * step + step * .5
        r = mm(1.5) * rng.uniform(.8, 1.15)
        d.ellipse((mm(3.0) - r, cy - r, mm(3.0) + r, cy + r), fill=0)
        x0 = mm(3.0) - r
        d.rectangle((0, cy - r * (.2 + rng.random() * .45), x0 + r, cy + r * (.2 + rng.random() * .45)), fill=0)   # the tear bridges the hole
    img.putalpha(a)


def new_page(doc):
    w, h = doc['tamanho']
    W, H = int(round(w * 1000 * PX)), int(round(h * 1000 * PX))
    return Image.new('RGBA', (W, H), qa.hexc(doc['papel'])), W, H


# ---------------------------------------------------------------- bilhete do cafe (notebook sheet, three people)

def bilhete_cafe(doc):
    img, W, H = new_page(doc)
    d = ImageDraw.Draw(img)
    rule = mm(7.0)
    y0 = mm(9.5)
    for k in range(int((H - y0) / rule) + 1):
        y = y0 + k * rule
        d.line((0, y, W, y), fill=(150, 176, 210, 255), width=max(1, int(mm(.22))))
    d.line((mm(14), 0, mm(14), H), fill=(212, 120, 120, 255), width=max(1, int(mm(.25))))
    # Text sits on the rules: the baseline is about 0.78 of the font size under the draw origin.
    base = lambda line_no, size: y0 + line_no * rule - size * PX * 0.80
    pen_text(img, 'Mauro, deixei o café no pote azul. O do pote branco é descafeinado. Nina.', mm(17), base(1, 5.0), 'nina', 5.0, rot=-0.6, width_mm=88, leading=rule / (5.0 * PX), seed=2)
    pen_text(img, 'A doutora já tomou o branco e reclamou que o plantão está fraco. Não conta. M.', mm(19), base(4, 5.4) + mm(0.5), 'mauro', 5.2, rot=0.7, width_mm=82, leading=rule / (5.2 * PX), seed=3)
    pen_text(img, 'Leio os recados antes de tomar café. H.', mm(27), base(7, 5.0), 'helena', 6.6, rot=-1.4, width_mm=76, leading=rule / (6.6 * PX), seed=4)
    torn_left(img, 7)
    return img


# ---------------------------------------------------------------- bilhete da marmita (scrap, one hand)

def bilhete_marmita(doc):
    img, W, H = new_page(doc)
    pen_text(img, 'Helena, sua comida está na geladeira. A Nina disse que você só almoçou bolacha.', mm(6), mm(6.5), 'mauro', 5.2, rot=-1.2, width_mm=75, leading=1.12, seed=5)
    pen_text(img, 'Mauro', mm(48), mm(37.5), 'mauro', 6.0, rot=-2.0, seed=6)
    d = ImageDraw.Draw(img)
    # clean cut at the top, ragged tear along the bottom
    a = Image.new('L', img.size, 255)
    ad = ImageDraw.Draw(a)
    rng = random.Random(9)
    pts = [(0, H - mm(3))]
    x = 0
    while x < W:
        x += mm(rng.uniform(2.5, 6))
        pts.append((min(x, W), H - mm(rng.uniform(0.5, 3.2))))
    pts += [(W, H), (0, H)]
    ad.polygon(pts, fill=0)
    img.putalpha(a)
    return img


# ---------------------------------------------------------------- comunicado D14 (photocopied memo)

def aviso(doc):
    img, W, H = new_page(doc)
    d = ImageDraw.Draw(img)
    ink = (30, 30, 32, 255)
    m = mm(11)
    f_small, f_b = font(SANS, 3.0), font(SANS_B, 3.0)
    d.text((m, mm(9)), 'N E C R O T É R I O   M U N I C I P A L', font=font(SANS_B, 3.0), fill=(90, 92, 98, 255))
    d.line((m, mm(14.5), W - m, mm(14.5)), fill=(60, 60, 62, 255), width=int(mm(.5)))
    d.text((m, mm(18)), 'COMUNICADO INTERNO', font=font(SANS_B, 7.0), fill=ink)
    d.text((m, mm(29)), 'Nº 047/2026', font=font(SANS, 3.6), fill=ink)
    # header fields
    y = mm(38)
    for label, value in (('DATA', '18/11/2026'), ('DE', 'Coordenação técnica'), ('PARA', 'Equipe do plantão')):
        d.text((m, y), label, font=f_b, fill=(90, 92, 98, 255))
        d.text((m + mm(20), y - mm(.6)), value, font=font(SANS, 3.9), fill=ink)
        d.line((m, y + mm(5.6), W - m, y + mm(5.6)), fill=(150, 150, 150, 255), width=max(1, int(mm(.2))))
        y += mm(8.2)
    y += mm(4)
    d.text((m, y), 'ASSUNTO', font=f_b, fill=(90, 92, 98, 255))
    d.text((m + mm(20), y - mm(.6)), 'Encerramento do plantão', font=font(SANS_B, 3.9), fill=ink)
    y += mm(13)
    body = font(SANS, 4.4)
    for item in ('Equipe técnica encerra presença no bloco às 22h35.', 'Exames pendentes seguem para a manhã.', 'Vigilância com ronda entre blocos.'):
        d.ellipse((m + mm(.4), y + mm(2.1), m + mm(1.9), y + mm(3.6)), fill=ink)
        for ln in wrap(item, body, W - 2 * m - mm(6)):
            d.text((m + mm(5), y), ln, font=body, fill=ink)
            y += body.size * 1.3
        y += mm(3.2)
    # signature block
    y = H - mm(31)
    d.line((m, y, m + mm(52), y), fill=ink, width=max(1, int(mm(.25))))
    d.text((m, y + mm(1.4)), 'Coordenação técnica', font=font(SANS, 3.6), fill=ink)
    d.line((m, H - mm(11), W - m, H - mm(11)), fill=(150, 150, 150, 255), width=max(1, int(mm(.2))))
    d.text((m, H - mm(8.6)), 'Afixar no quadro de avisos.', font=font(SANS, 2.7), fill=(100, 100, 104, 255))
    return img


# ---------------------------------------------------------------- controle de limpeza (form with pen marks)

def limpeza(doc):
    img, W, H = new_page(doc)
    d = ImageDraw.Draw(img)
    ink = (36, 36, 38, 255)
    grey = (120, 122, 126, 255)
    line = (96, 98, 102, 255)
    m = mm(7)
    d.text((m, mm(5.5)), 'CONTROLE DE LIMPEZA', font=font(SANS_B, 5.4), fill=ink)
    d.text((m, mm(13.2)), 'Copa e vestiário', font=font(SANS, 3.8), fill=ink)
    d.text((W - m - font(SANS, 3.4).getlength('Semana de 16/11 a 22/11/2026'), mm(13.6)), 'Semana de 16/11 a 22/11/2026', font=font(SANS, 3.4), fill=ink)
    d.line((m, mm(19.2), W - m, mm(19.2)), fill=ink, width=int(mm(.55)))

    # Daily routine
    d.text((m, mm(21.5)), 'ROTINA DIÁRIA', font=font(SANS_B, 2.9), fill=grey)
    tasks = ('Varrer e passar pano no piso', 'Esvaziar as lixeiras', 'Limpar bancada, mesa e cadeiras', 'Higienizar puxadores e maçanetas')
    days = ('SEG', 'TER', 'QUA', 'QUI', 'SEX', 'SÁB', 'DOM')
    x_name = m
    col0 = mm(68)
    colw = (W - 2 * m - col0) / 7
    top = mm(26.5)
    rh = mm(6.4)
    d.rectangle((m, top, W - m, top + rh), fill=(228, 228, 224, 255))
    for k, dn in enumerate(days):
        f = font(SANS_B, 2.8)
        d.text((m + col0 + k * colw + (colw - f.getlength(dn)) / 2, top + mm(1.7)), dn, font=f, fill=ink)
    d.text((x_name + mm(1.5), top + mm(1.6)), 'Tarefa', font=font(SANS_B, 2.8), fill=ink)
    rows = len(tasks) + 1
    for r in range(rows + 1):
        y = top + rh * (r + 1)
        d.line((m, y, W - m, y), fill=line, width=max(1, int(mm(.22))))
    for k in range(8):
        x = m + col0 + k * colw
        d.line((x, top, x, top + rh * (rows + 1)), fill=line, width=max(1, int(mm(.22))))
    d.line((m, top, m, top + rh * (rows + 1)), fill=line, width=max(1, int(mm(.22))))
    d.line((m, top, W - m, top), fill=line, width=max(1, int(mm(.22))))
    for r, t in enumerate(tasks):
        d.text((m + mm(1.5), top + rh * (r + 1) + mm(1.6)), t, font=font(SANS, 3.1), fill=ink)
    d.text((m + mm(1.5), top + rh * (rows) + mm(1.6)), 'Rubrica', font=font(SANS, 3.1), fill=ink)
    # Marks: Monday and Tuesday complete, Wednesday only the morning round (floor, bins)
    pen = HANDS['cleaner'][1]
    done = {(0, 0), (0, 1), (0, 2), (0, 3), (1, 0), (1, 1), (1, 2), (1, 3), (2, 0), (2, 1)}
    for (dc, r) in done:
        tick(img, m + col0 + dc * colw + colw / 2, top + rh * (r + 1) + rh / 2, 3.4, pen, dc * 7 + r)
    for dc in (0, 1):
        scribble(img, m + col0 + dc * colw + mm(1.6), top + rh * rows + rh * .75, 4.2, pen, dc + 2)

    # Periodic tasks
    y2 = top + rh * (rows + 1) + mm(5.2)
    d.text((m, y2), 'TAREFAS PERIÓDICAS', font=font(SANS_B, 2.9), fill=grey)
    y2 += mm(4.8)
    heads = ('Tarefa', 'Frequência', 'Última', 'Próxima')
    cols = [m, m + mm(74), m + mm(100), m + mm(124), W - m]
    d.rectangle((m, y2, W - m, y2 + rh), fill=(228, 228, 224, 255))
    for t, x in zip(heads, cols):
        d.text((x + mm(1.5), y2 + mm(1.7)), t, font=font(SANS_B, 2.8), fill=ink)
    per = (('Higienizar a geladeira por dentro', 'quinzenal', '06/11', '20/11'),
           ('Lavagem completa do piso', 'semanal', '14/11', '21/11'),
           ('Limpeza interna dos armários vagos', 'mensal', '31/10', '30/11'))
    for r, row in enumerate(per):
        y = y2 + rh * (r + 1)
        for c, (txt, x) in enumerate(zip(row, cols)):
            d.text((x + mm(1.5), y + mm(1.6)), txt, font=font(SANS, 3.1), fill=ink)
    for r in range(len(per) + 2):
        y = y2 + rh * r
        d.line((m, y, W - m, y), fill=line, width=max(1, int(mm(.22))))
    for x in cols:
        d.line((x, y2, x, y2 + rh * (len(per) + 1)), fill=line, width=max(1, int(mm(.22))))
    d.text((m, H - mm(6.2)), 'Marcar com visto ao concluir a tarefa. Rubricar ao fim do turno.', font=font(SANS, 2.7), fill=grey)
    return img


# ---------------------------------------------------------------- stage 2, Mauro's locker: Samuel's work card, Mauro's note, Irene's letter

def h01_cartao_samuel(doc):
    img, W, H = new_page(doc)
    d = ImageDraw.Draw(img)
    ink = (30, 30, 34, 255)
    band = (196, 126, 34, 255)                                    # builder's orange, faded by the bag
    d.rectangle((0, 0, W, mm(12)), fill=band)
    d.text((mm(5), mm(2.6)), 'CONSTRUTORA BOM PASTOR', font=font(SANS_B, 4.2), fill=(255, 255, 255, 255))
    d.text((mm(5), mm(16)), 'CARTÃO DE TRABALHO', font=font(SANS_B, 2.8), fill=(110, 110, 114, 255))
    d.rectangle((mm(5), mm(22), mm(24), mm(46)), outline=(150, 150, 150, 255), width=max(1, int(mm(.3))))
    d.text((mm(8.5), mm(32)), 'FOTO', font=font(SANS, 2.6), fill=(170, 170, 170, 255))
    d.text((mm(29), mm(24)), 'Samuel Reis', font=font(SANS_B, 5.2), fill=ink)
    d.text((mm(29), mm(32.5)), 'Ajudante de obras', font=font(SANS, 3.8), fill=ink)
    d.line((mm(29), mm(44), W - mm(6), mm(44)), fill=(120, 120, 120, 255), width=max(1, int(mm(.25))))
    d.text((mm(29), mm(45.5)), 'Assinatura', font=font(SANS, 2.2), fill=(120, 120, 120, 255))
    # Wear: rounded dirty corners and a dark smear from the belongings bag.
    a = np.asarray(img).astype(float)
    yy, xx = np.mgrid[0:H, 0:W]
    smear = np.exp(-(((xx - W * .78) / (W * .22)) ** 2 + ((yy - H * .7) / (H * .3)) ** 2))
    a[..., :3] -= smear[..., None] * np.array([34, 38, 44])
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA')
    m = Image.new('L', (W, H), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, W - 1, H - 1), radius=int(mm(3)), fill=255)
    img.putalpha(m)
    return img


def h01_bilhete_mauro(doc):
    img, W, H = new_page(doc)
    pen_text(img, 'Achei este cartão na bolsa P17, que era do corpo 041. Guardei comigo depois que o corpo saiu.', mm(5), mm(9),
             'mauro', 4.6, rot=.6, width_mm=57, leading=1.1, seed=51)
    pen_text(img, '— Mauro', W - mm(25), H - mm(11), 'mauro', 4.6, seed=52)
    # Paper clip over the top edge, holding the card.
    w, h = int(mm(8)), int(mm(26))
    t = Image.new('RGBA', (w + 10, h + 10), (0, 0, 0, 0))
    dc = ImageDraw.Draw(t)
    for inset, col in ((0, (120, 122, 126, 255)), (int(mm(2)), (150, 152, 156, 255))):
        dc.rounded_rectangle((5 + inset, 5 + inset, 5 + w - inset, 5 + h - inset * 2), radius=int((w - 2 * inset) / 2), outline=col, width=max(3, int(mm(.7))))
    t = t.rotate(-5, expand=True, resample=Image.BICUBIC)
    img.alpha_composite(t, (int(W - mm(14)), -int(t.height * .3)))
    return img


def h05_carta_irene(doc):
    """Plain letter paper, folded in half; Irene writes large and slanted."""
    img, W, H = new_page(doc)
    size, x, w = 8.2, mm(14), 120
    y = mm(16)
    y += pen_text(img, 'Mauro,', x, y, 'irene', size, seed=61) + mm(3)
    for k, para in enumerate(('você estava lá quando eu entreguei o pedido para a Helena. Você viu. Eu esperei você falar, e você ficou quieto.',
                              'A mãe do Samuel continua me ligando.',
                              'Se um dia descobrir onde ele foi enterrado, me avisa.')):
        y += pen_text(img, para, x, y, 'irene', size, rot=-.3, width_mm=w, leading=1.08, seed=62 + k) + mm(5)
    pen_text(img, '— Irene', W - mm(44), y + mm(6), 'irene', size, seed=70)
    return img


RENDER = {'bilhete_cafe': bilhete_cafe, 'bilhete_marmita': bilhete_marmita, 'aviso_encerramento': aviso, 'escala_limpeza': limpeza,
          'h01_cartao_samuel': h01_cartao_samuel, 'h01_bilhete_mauro': h01_bilhete_mauro, 'h05_carta_irene': h05_carta_irene}


def main():
    qa.DOCS = DIR
    data = json.load(open(os.path.join(DIR, 'DocumentosDescanso.json'), encoding='utf-8'))
    for doc in data['documentos']:
        raw = RENDER[doc['id']](doc)
        img = qa.finish(raw, doc.get('acabamento', 'papel'))
        img.save(os.path.join(DIR, doc['id'] + '.png'))
        alpha = img.getchannel('A')
        if alpha.getextrema()[0] < 255:
            back = qa.paper_grain(Image.new('RGBA', img.size, tuple(int(c * .97) for c in qa.hexc(doc['papel'])[:3]) + (255,)), 5)
            back.putalpha(alpha)
            back.save(os.path.join(DIR, doc['id'] + '_verso.png'))
        qa.relief(doc)
        print(f"{doc['id']}: {img.size}")


if __name__ == '__main__':
    main()

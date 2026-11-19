# Office papers share the reception renderer; only the added blocks are registered here.
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '../../Recepcao/scripts')))
import documentos_recepcao as reception
qa = reception.qa
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '../../Recebimento/scripts')))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '../../Descanso/scripts')))
import documentos_recebimento as receb
import documentos_descanso as dd
from PIL import ImageDraw, ImageFont
DIR = os.path.join(qa.ROOT, 'Assets/UnclaimedPrototype/Administracao/Documentos')
FOTOS = os.path.normpath(os.path.join(HERE, '..', 'fotos'))
mm = qa.mm
INK = (28, 28, 30, 255)


def marked(p, b, strike=False):
    family = 'hand' if b['tipo'] == 'anotacao' else p.fam
    f = qa.font(family, 'r', b.get('tamanho', 5))
    margin = b.get('margem', False)
    lines = qa.wrap(b['texto'], f, qa.mm(58) if margin else p.cw - qa.mm(4))
    x = p.W - qa.mm(68) if margin else p.x0 + (qa.mm(4) if b['tipo'] == 'anotacao' else 0)
    if margin:
        p.y = qa.mm(60)
    elif b.get('rodape', False):
        p.y = p.H - qa.mm(38)
    color = (35, 56, 85, 255)
    for line in lines:
        p.d.text((x, p.y), line, font=f, fill=color)
        if strike:
            y = p.y + f.size * .62
            p.d.line((x, y, x + f.getlength(line), y + qa.mm(.5)), fill=color, width=max(1, int(qa.mm(.35))))
        p.y += f.size * 1.32
    p.y += qa.mm(2)


# ---------------------------------------------------------------- stage 2 papers in Leo's folder (drawn, not block-based)

def paper_clip(img, x, y, rot):
    """A steel paper clip over the top edge: two nested rounded loops."""
    from PIL import Image
    w, h = int(mm(9)), int(mm(30))
    t = Image.new('RGBA', (w + 10, h + 10), (0, 0, 0, 0))
    d = ImageDraw.Draw(t)
    lw = max(3, int(mm(.75)))
    for inset, col in ((0, (120, 122, 126, 255)), (int(mm(2.2)), (150, 152, 156, 255))):
        d.rounded_rectangle((5 + inset, 5 + inset, 5 + w - inset, 5 + h - inset * 2), radius=int((w - 2 * inset) / 2), outline=col, width=lw)
    t = t.rotate(rot, expand=True, resample=Image.BICUBIC)
    img.alpha_composite(t, (int(x - t.width / 2), int(y - t.height * .3)))


def d12_foto_galpao(doc):
    img, strip = receb.photo_print(doc, os.path.join(FOTOS, 'd12_galpao.png'), 26)
    W, _ = img.size
    dd.pen_text(img, 'Galpão da Limiar, 12/11. A caixa do "lote 784" tem o chaveiro azul da Vera, o do dente quebrado. — Célia',
                mm(6), strip + mm(1.5), 'celia', 4.6, rot=.4, width_mm=136, leading=1.05, seed=41)
    return img


def d12_bilhete_joao(doc):
    img, W, H = dd.new_page(doc)
    dd.pen_text(img, 'A saída da 784 está no meu nome, mas eu não fiz esse transporte.', mm(6), mm(8), 'joao', 4.6,
                rot=-.6, width_mm=66, leading=1.15, seed=42)
    dd.pen_text(img, '— João', W - mm(24), H - mm(12), 'joao', 4.6, seed=43)
    paper_clip(img, W - mm(9), 0, 4)
    return img


def d15_mensagens(doc):
    img, W, H = dd.new_page(doc)
    d = ImageDraw.Draw(img)
    dd.pen_text(img, 'Copiei do celular do Daniel quando ele esqueceu no caminhão. "R." é o comprador. — João', mm(10), mm(6),
                'joao', 5.4, rot=-.5, width_mm=128, leading=1.12, seed=44)
    # Printed phone screenshot, toner grey: contact bar, then the bubbles (Daniel's phone: his messages on the right).
    x0, y0, x1, y1 = mm(10), mm(34), W - mm(10), H - mm(14)
    d.rectangle((x0, y0, x1, y1), fill=(236, 236, 236, 255), outline=(90, 90, 90, 255), width=max(1, int(mm(.3))))
    d.rectangle((x0, y0, x1, y0 + mm(13)), fill=(196, 196, 196, 255))
    f_bar = ImageFont.truetype(dd.SANS_B, int(mm(4.4)))
    d.ellipse((x0 + mm(4), y0 + mm(2.5), x0 + mm(12), y0 + mm(10.5)), fill=(150, 150, 150, 255))
    d.text((x0 + mm(15), y0 + mm(3.2)), 'R.', font=f_bar, fill=INK)
    f_meta, f_msg = ImageFont.truetype(dd.SANS_B, int(mm(3.4))), ImageFont.truetype(dd.SANS, int(mm(4.9)))
    msgs = [('R.', '07/10, 23h12', 'Preciso de mais uma. Mulher, entre 50 e 65 anos. Até sexta.'),
            ('Daniel', '07/10, 23h40', 'O 784 sai amanhã. Mesmo valor de sempre.'),
            ('R.', '07/10, 23h41', 'Mesmo galpão?'),
            ('Daniel', '07/10, 23h41', 'Mesmo galpão. Ninguém vai procurar.')]
    y = y0 + mm(19)
    bw = (x1 - x0) * .72
    for who, when, text in msgs:
        lines = qa.wrap(text, f_msg, bw - mm(8))
        tw = max(max(f_msg.getlength(l) for l in lines), f_meta.getlength(who + ' · ' + when)) + mm(8)
        bh = mm(6.5) + len(lines) * f_msg.size * 1.3 + mm(3)
        right = who == 'Daniel'
        bx = x1 - mm(5) - tw if right else x0 + mm(5)
        d.rounded_rectangle((bx, y, bx + tw, y + bh), radius=int(mm(3)), fill=(170, 170, 170, 255) if right else (255, 255, 255, 255),
                            outline=(120, 120, 120, 255), width=1)
        d.text((bx + mm(4), y + mm(1.6)), who + ' · ' + when, font=f_meta, fill=(60, 60, 64, 255))
        ty = y + mm(6.4)
        for l in lines:
            d.text((bx + mm(4), ty), l, font=f_msg, fill=INK)
            ty += f_msg.size * 1.3
        y += bh + mm(7)
    return img


def h08_recibo_041(doc):
    img, W, H = dd.new_page(doc)
    d = ImageDraw.Draw(img)
    m = mm(12)
    f_logo, f_t = ImageFont.truetype(dd.SANS_B, int(mm(6.2))), ImageFont.truetype(dd.SANS_B, int(mm(4.6)))
    f_l, f_v = ImageFont.truetype(dd.SANS, int(mm(3.1))), ImageFont.truetype(dd.SANS, int(mm(4.8)))
    d.text((m, mm(11)), 'LIMIAR SERVIÇOS', font=f_logo, fill=INK)
    d.text((m, mm(20)), 'Transporte funerário e remoções', font=f_l, fill=(90, 90, 94, 255))
    d.line((m, mm(26), W - m, mm(26)), fill=INK, width=max(2, int(mm(.5))))
    d.text((m, mm(31)), 'RECIBO DE TRANSPORTE', font=f_t, fill=INK)
    y = mm(46)
    for label, value in (('Corpo', 'caso 041 (identidade não confirmada)'), ('Saída do necrotério', '15/08/2024, 06h40'),
                         ('Destino', 'Cemitério Santa Amélia'), ('Responsável pelo transporte', 'Daniel Vilar (código LV-12)'),
                         ('Conferido por funcionário do necrotério', '')):
        d.text((m, y), label.upper(), font=f_l, fill=(90, 90, 94, 255))
        d.text((m, y + mm(4.6)), value, font=f_v, fill=INK)
        d.line((m, y + mm(11.5), W - m, y + mm(11.5)), fill=(150, 150, 150, 255), width=max(1, int(mm(.25))))
        y += mm(18)
    y += mm(14)
    dd.scribble(img, m + mm(4), y, 30, (26, 40, 100, 255), 7)
    d.line((m, y + mm(3), m + mm(56), y + mm(3)), fill=INK, width=max(1, int(mm(.25))))
    d.text((m, y + mm(4.5)), 'Assinatura do responsável', font=f_l, fill=(90, 90, 94, 255))
    d.line((W - m - mm(56), y + mm(3), W - m, y + mm(3)), fill=INK, width=max(1, int(mm(.25))))
    d.text((W - m - mm(56), y + mm(4.5)), 'Assinatura do necrotério', font=f_l, fill=(90, 90, 94, 255))
    d.text((m, H - mm(12)), 'Via do necrotério', font=f_l, fill=(90, 90, 94, 255))
    return img


DRAWN = {'d12_foto_galpao': d12_foto_galpao, 'd12_bilhete_joao': d12_bilhete_joao, 'd15_mensagens': d15_mensagens,
         'h08_recibo_041': h08_recibo_041}


def main():
    qa.BLOCKS['riscado'] = lambda p, b: marked(p, b, True)
    qa.BLOCKS['anotacao'] = marked
    qa.DOCS = DIR
    data = json.load(open(os.path.join(DIR, 'DocumentosAdministracao.json'), encoding='utf-8'))
    for doc in data['documentos']:
        if doc['id'] in DRAWN:
            img = qa.finish(DRAWN[doc['id']](doc), doc.get('acabamento', 'papel'))
            img.save(os.path.join(DIR, doc['id'] + '.png'))
            qa.relief(doc)
            print(f"{doc['id']}: {img.size}")
            continue
        img = qa.render(doc)
        qa.relief(doc)
        print(f"{doc['id']}: {img.size}")


if __name__ == '__main__':
    main()

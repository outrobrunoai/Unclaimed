"""Technical-room paper D03 "Manutenção provisória" (Assets/História, Documentos do caso atual): Mauro's note on a
notebook sheet taped to the wall beside the boards, and Helena's answer under it. Only the document's own words.
  python3 Blender/Tecnica/scripts/documentos_tecnica.py     (then Tools > Unclaimed > Tecnica > Build Documentos)"""
import json, os, sys
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '../../Recepcao/scripts')))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '../../Descanso/scripts')))
import quadro_avisos as qa
import documentos_descanso as dd

DIR = os.path.join(qa.ROOT, 'Assets/UnclaimedPrototype/Tecnica/Documentos')
mm, PX = qa.mm, qa.PX


def tape(img, x, y, w_mm, h_mm, rot):
    """Masking tape over the sheet's top corners (the wall holds it): matt beige, translucent, ragged ends."""
    t = Image.new('RGBA', (int(mm(w_mm)), int(mm(h_mm))), (222, 206, 160, 205))
    d = ImageDraw.Draw(t)
    for k in range(0, t.height, 3):
        d.line((0, k, 2 + (k * 7) % 5, k), fill=(0, 0, 0, 0))
        d.line((t.width - 1 - (k * 5) % 4, k, t.width, k), fill=(0, 0, 0, 0))
    t = t.rotate(rot, expand=True, resample=Image.BICUBIC)
    img.alpha_composite(t, (int(x - t.width / 2), int(y - t.height / 2)))


def manutencao_provisoria(doc):
    img, W, H = dd.new_page(doc)
    d = ImageDraw.Draw(img)
    rule, y0 = mm(7.0), mm(11.0)
    for k in range(int((H - y0) / rule) + 1):
        d.line((0, y0 + k * rule, W, y0 + k * rule), fill=(150, 176, 210, 255), width=max(1, int(mm(.22))))
    d.line((mm(14), 0, mm(14), H), fill=(212, 120, 120, 255), width=max(1, int(mm(.25))))
    base = lambda line_no, size: y0 + line_no * rule - size * PX * 0.80
    dd.pen_text(img, 'A alimentação provisória não aguenta os dois circuitos de luz com a refrigeração ligada. Mauro.', mm(18), base(1, 5.6),
                'mauro', 5.6, rot=0.5, width_mm=118, leading=rule / (5.6 * PX), seed=11)
    dd.pen_text(img, 'Refrigeração permanece ligada. H.', mm(44), base(6, 6.8), 'helena', 6.8, rot=-1.2, width_mm=96, leading=rule / (6.8 * PX), seed=12)
    dd.torn_left(img, 13)
    tape(img, mm(22), mm(3), 26, 9, 4)
    tape(img, W - mm(20), mm(3), 26, 9, -3)
    return img


def main():
    qa.DOCS = DIR
    data = json.load(open(os.path.join(DIR, 'DocumentosTecnica.json'), encoding='utf-8'))
    for doc in data['documentos']:
        img = qa.finish(manutencao_provisoria(doc), doc.get('acabamento', 'papel'))
        img.save(os.path.join(DIR, doc['id'] + '.png'))
        qa.relief(doc)
        print(f"{doc['id']}: {img.size}")


if __name__ == '__main__':
    main()

# Narrative documents placed in the reception (ficha de entrada D01, routine notice for the circulation door,
# Nina's crossword photocopy): renders every document of DocumentosRecepcao.json to a PNG plus its relief map,
# with the same renderer as the notice board (quadro_avisos.py), and adds two block types the board has no use for:
#   campos  typed form fields (label above value, in a grid of columns)
#   dicas   two clue lists of a newspaper crossword (number, clue)
#   python3 Blender/Recepcao/scripts/documentos_recepcao.py
# The texts live only in the JSON (sizes inside a document are in mm). Unity side: Tools > Unclaimed > Build Documentos Recepcao.
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import quadro_avisos as qa
from quadro_avisos import font, mm, wrap, draw_lines

DIR = os.path.join(qa.ROOT, "Assets", "UnclaimedPrototype", "Recepcao", "Documentos")
qa.DOCS = DIR


def b_campos(p, b):
    cols = b.get("colunas", 2)
    fl, fv = font("sans", "r", b.get("tamanhoRotulo", 3.0)), font(p.fam, "b", b.get("tamanhoValor", 5.0))
    gap = mm(6)
    cw = (p.cw - gap * (cols - 1)) / cols
    grey = (96, 98, 104, 255)
    rule = (150, 150, 150, 255)
    row_h = fl.size * 1.5 + fv.size * 1.55 + mm(3.2)
    for i, it in enumerate(b["itens"]):
        c, r = i % cols, i // cols
        x = p.x0 + c * (cw + gap)
        y = p.y + r * row_h
        p.d.text((x, y), it["rotulo"], font=fl, fill=grey)
        p.d.text((x, y + fl.size * 1.5), it["valor"], font=fv, fill=p.ink)
        yl = y + fl.size * 1.5 + fv.size * 1.4
        p.d.line((x, yl, x + cw, yl), fill=rule, width=max(1, int(mm(0.25))))
    p.y += ((len(b["itens"]) + cols - 1) // cols) * row_h + mm(2)


def b_dicas(p, b):
    f, fb = font(p.fam, "r", b.get("tamanho", 4.0)), font(p.fam, "b", b.get("tamanho", 4.0))
    gap = mm(10)
    cw = (p.cw - gap) / 2
    y_top = p.y
    ends = []
    for k, (head, key) in enumerate((("HORIZONTAIS", "horizontais"), ("VERTICAIS", "verticais"))):
        x = p.x0 + k * (cw + gap)
        y = y_top
        p.d.text((x, y), head, font=fb, fill=p.ink)
        y += fb.size * 1.5
        p.d.line((x, y - mm(0.8), x + cw, y - mm(0.8)), fill=(60, 60, 60, 255), width=max(1, int(mm(0.3))))
        y += mm(1.2)
        for num, clue in b[key]:
            nw = fb.getlength(num + " ") + mm(0.5)
            p.d.text((x, y), num, font=fb, fill=p.ink)
            lines = wrap(clue, f, cw - nw)
            y = draw_lines(p.d, lines, f, x + nw, y, cw - nw, p.ink, leading=1.28)
            y += mm(1.3)
        ends.append(y)
    p.y = max(ends) + mm(2)


qa.BLOCKS["campos"] = b_campos
qa.BLOCKS["dicas"] = b_dicas


def main():
    data = json.load(open(os.path.join(DIR, "DocumentosRecepcao.json"), encoding="utf-8"))
    for doc in data["documentos"]:
        img = qa.render(doc)
        qa.relief(doc)
        print(f"{doc['id']}: {img.size}")


if __name__ == "__main__":
    main()

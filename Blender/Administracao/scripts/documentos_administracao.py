# Office papers share the reception renderer; only the added blocks are registered here.
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '../../Recepcao/scripts')))
import documentos_recepcao as reception
qa = reception.qa
DIR = os.path.join(qa.ROOT, 'Assets/UnclaimedPrototype/Administracao/Documentos')


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


def main():
    qa.BLOCKS['riscado'] = lambda p, b: marked(p, b, True)
    qa.BLOCKS['anotacao'] = marked
    qa.DOCS = DIR
    data = json.load(open(os.path.join(DIR, 'DocumentosAdministracao.json'), encoding='utf-8'))
    for doc in data['documentos']:
        img = qa.render(doc)
        qa.relief(doc)
        print(f"{doc['id']}: {img.size}")


if __name__ == '__main__':
    main()

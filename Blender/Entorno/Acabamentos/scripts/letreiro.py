"""Matriz SVG e impressão da proposta A aprovada. Escreve somente o letreiro principal.

Peça de empresa de comunicação visual: chapa de alumínio pintada (fosca) com letras de vinil recortado, instalada
na fachada sem beiral, exposta à chuva há alguns anos. O desgaste segue a água: poeira assentada no retorno superior
escorre em poucos fios, as fixações deixam marcas curtas abaixo delas e a sujeira seca na borda inferior. O vinil é
um pouco mais acetinado que a pintura e repele a água, por isso recebe menos marcas que o fundo."""
from pathlib import Path
import sys
import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent))
from placas_comum import FONTES, svg_matrix, render, noise, streaks, edge_band, save_rgb, save_mask, save_normal, mix

TEXTS = '''<text x="145" y="263" font-family="Nimbus Sans Narrow" font-weight="bold" font-size="184" fill="#292a27">NECROTÉRIO MUNICIPAL</text>
<text x="151" y="392" font-family="Nimbus Sans" font-size="76" fill="#292a27">Secretaria Municipal de Saúde</text>'''
HEAD = '<svg xmlns="http://www.w3.org/2000/svg" width="2400" height="520" viewBox="0 0 2400 520">\n'


def main():
    # Coordinates in millimetres at 1 px/mm; installed on the original 2400 x 520 mm envelope.
    # Screw heads are physical Blender geometry, never printed into this image.
    svg = (HEAD + '<title>NECROTÉRIO MUNICIPAL — chapa administrativa clara aprovada</title>\n'
           '<rect width="2400" height="520" fill="#e2ddcd"/>\n' + TEXTS + '\n</svg>')
    clean = svg_matrix('Placa_Fachada_A', svg)[..., :3]
    Image.fromarray(np.uint8(clean * 255 + .5)).save(FONTES / 'Placa_Fachada_A-plana.png')
    vinyl = render(HEAD + TEXTS + '</svg>')[..., 3]
    h, w = vinyl.shape
    rng = np.random.default_rng(171026)
    yy, xx = np.mgrid[:h, :w].astype(float)
    screws = [(38, 38), (2362, 38), (38, 482), (2362, 482)]

    # Paint: aged a few percent darker than the design colour, same hue; broad roller/curing variation.
    paint = clean * .955 * (1 + .018 * noise((h, w), (6, 26), rng, 20)[..., None] + .006 * noise((h, w), (40, 180), rng)[..., None])
    # Vinyl letters fade slightly towards grey and unevenly.
    letters = np.array([.205, .207, .19]) * (1 + .08 * noise((h, w), (8, 40), rng, 4))[..., None]
    arr = paint * (1 - vinyl[..., None]) + letters * vinyl[..., None]

    # Run-off: a few streaks from the top return (dust washed down), short marks below each fixing.
    sources = [(x, 1, rng.uniform(5, 14), rng.uniform(70, 260), rng.uniform(.25, .6)) for x in rng.uniform(60, 2340, 11)]
    sources += [(x + rng.uniform(-2, 2), y + 6, 4.5, rng.uniform(40, 90), .75) for x, y in screws]
    run = streaks((h, w), rng, sources)
    top = edge_band((h, w), rng, 'top', 9, 5) * .55
    bottom = edge_band((h, w), rng, 'bottom', 16, 9)
    corners = sum(np.exp(-((xx - x) / 16) ** 2 - ((yy - y) / 18) ** 2) for x, y in screws)
    lift = np.clip(np.asarray(Image.fromarray(np.uint8(vinyl * 255)).filter(ImageFilter.MaxFilter(3)), float) / 255 - vinyl, 0, 1)
    settle = np.clip((yy / h - .35) / .65, 0, 1) * .05 * (1 + noise((h, w), (2, 9), rng, 30))   # deposits lower down
    dirt = np.clip(run * (1 - .6 * vinyl) * .3 + top * .3 + bottom * .32 + corners * .14 + lift * .12 + settle, 0, .55)
    # Short edge losses on the lower fold, not a decorative border.
    chip = np.exp(-((yy - 517) / 2.5) ** 2) * (np.exp(-((xx - 210) / 40) ** 2) + np.exp(-((xx - 2090) / 55) ** 2))
    arr = mix(arr, (101, 96, 84), dirt)
    arr = mix(arr, (150, 146, 136), chip * .5)
    save_rgb(arr, 'Placa_Fachada')

    # Smoothness: chalky matte paint, satin vinyl, dirt duller still. Normal: slight oil-canning between the
    # standoffs, faint orange peel and the 0.1 mm vinyl step. No emission anywhere.
    smooth = .19 + .035 * noise((h, w), (5, 22), rng, 10)
    smooth = smooth * (1 - vinyl) + (.40 + .04 * noise((h, w), (6, 30), rng, 3)) * vinyl
    smooth = smooth - dirt * .22
    save_mask(np.clip(smooth, .04, .6), 'Placa_Fachada')
    can = noise((h, w), (3, 7), rng, 30) * 6 + noise((h, w), (90, 400), rng) * .25
    step = np.asarray(Image.fromarray(np.uint8(vinyl * 255)).filter(ImageFilter.GaussianBlur(.8)), float) / 255
    save_normal(can + step * .35, 'Placa_Fachada', 1.0)
    print('Letreiro A: matriz editável, prova plana, impressão envelhecida, máscara e normal atualizadas.')


if __name__ == '__main__':
    main()

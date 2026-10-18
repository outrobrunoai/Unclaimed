"""Sinalização externa além do letreiro A. Cada peça segue a origem de produção:
- Placa_Atendimento: aviso impresso pela equipe (editor de texto, Liberation Sans) numa folha A3 dentro de um porta-folha
  de acrílico parafusado; a textura cobre a face frontal do acrílico (fundo de PVC, folha e a própria superfície).
- Interfone_Identificacao: sino moldado pelo fabricante e uma etiqueta de rotuladora colada depois.
Matrizes SVG com texto vivo em Blender/Entorno/Acabamentos/fontes; texturas em Assets/Modelos/Entorno/Acabamentos/Texturas.
    python3 Blender/Entorno/Acabamentos/scripts/placas.py
"""
from pathlib import Path
import sys
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from placas_comum import svg_matrix, noise, streaks, edge_band, save_rgb, save_mask, save_normal, mix

PAPER, TONER = '#ece7d8', '#272725'


def atendimento():
    # Face 440 x 320 mm at 5 px/mm. The A3 sheet (420 x 297, landscape; the office copier prints A3) rests on the
    # bottom of the pocket, slightly askew. Word-like layout: 28 mm margins, 84 pt bold title and 64 pt body so it
    # reads from a couple of metres, left aligned.
    W, H, S = 440, 320, 5
    px, py = (W - 420) / 2 + 1.5, H - 8 - 297
    svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="{W * S}" height="{H * S}" viewBox="0 0 {W} {H}">
<title>Aviso da equipe em porta-folha de acrílico — atendimento a familiares</title>
<rect width="{W}" height="{H}" fill="#e1dfd7"/>
<g transform="translate({px} {py}) rotate(-0.4 210 148.5)">
<rect x="0.8" y="1" width="420" height="297" fill="#000" opacity="0.10"/>
<rect width="420" height="297" fill="{PAPER}"/>
<g font-family="Liberation Sans" fill="{TONER}">
<text x="28" y="55" font-size="29.7" font-weight="bold">Atendimento a familiares</text>
<text x="28" y="99" font-size="22.6">Horário comercial: na recepção.</text>
<text x="28" y="125" font-size="22.6">Outros horários: toque o interfone.</text>
<text x="28" y="151" font-size="22.6">Atendemos 24 horas.</text>
</g></g></svg>'''
    img = svg_matrix('Placa_Atendimento', svg)[..., :3]
    h, w = img.shape[:2]
    rng = np.random.default_rng(1810)
    yy, xx = np.mgrid[:h, :w].astype(float)
    sheet = (np.abs(img - np.array([0xec, 0xe7, 0xd8]) / 255).sum(2) < .02) | (img.mean(2) < .5)
    # Paper: gentle cockling from humidity, a faint tide mark where rain that entered the open top wicked up.
    cockle = noise((h, w), (5, 7), rng, 25) * .035
    img = img * (1 + cockle[..., None] * sheet[..., None])
    tide_top = (py + 297 - 18) * S + noise((1, w), (1, 9), rng)[0] * 30
    tide = np.exp(-((yy - tide_top[None, :]) / 5) ** 2) * .5 + (yy > tide_top[None, :]) * .25
    tide *= sheet * np.exp(-((xx - w * .63) / (w * .2)) ** 4)
    img = mix(img, (196, 178, 140), tide * .35)
    # Acrylic (outer surface): dust run-off from the top edge and screws, grime along the lower lip, dried drops.
    run = streaks((h, w), rng, [(x, 0, rng.uniform(1.5, 4), rng.uniform(25, 90), rng.uniform(.3, .7))
                                for x in rng.uniform(20, W - 20, 6)] + [(x, 18, 1.8, 30, .6) for x in (12, W - 12)], S)
    lip = edge_band((h, w), rng, 'bottom', 5 * S, 2 * S)
    drops = np.zeros((h, w))
    for x, y, r in zip(rng.uniform(0, w, 70), rng.uniform(h * .35, h, 70), rng.uniform(4, 14, 70)):
        d = np.hypot(xx - x, yy - y) / r
        drops = np.maximum(drops, np.exp(-((d - .9) / .18) ** 2) * (d < 1.4))
    img = mix(img, (120, 116, 104), run * .1 + lip * .22)
    img = mix(img, (236, 236, 230), drops * .12)
    save_rgb(img, 'Placa_Atendimento')
    smooth = .86 - drops * .35 - run * .25 - lip * .5 + noise((h, w), (6, 8), rng, 20) * .04
    save_mask(np.clip(smooth, .2, .9), 'Placa_Atendimento')


def interfone():
    # Identification strip of the intercom face, 86 x 20 mm at 16 px/mm. Grey ABS with the moulded bell, plus a
    # laminated 12 mm label-maker tape with the destination of the call.
    W, H, S = 86, 20, 16
    svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="{W * S}" height="{H * S}" viewBox="0 0 {W} {H}">
<title>Interfone externo — sino moldado e etiqueta de rotuladora</title>
<rect width="{W}" height="{H}" fill="#b4b8ad"/>
<g fill="none" stroke="#8e9289" stroke-width="0.9" stroke-linejoin="round">
<path d="M8 14.7 h11 M9.3 14.7 c0.8-1.2 1-2.4 1-4.2 c0-2.6 1.6-4.2 3.2-4.4 c1.6 0.2 3.2 1.8 3.2 4.4 c0 1.8 0.2 3 1 4.2"/>
<path d="M12.6 16.1 a1 1 0 0 0 2 0"/></g>
<g transform="rotate(-0.8 52 10)">
<rect x="27.4" y="4.3" width="47" height="12" fill="#000" opacity="0.18"/>
<rect x="27" y="4" width="47" height="12" rx="0.3" fill="#ecebe4"/>
<text x="50.5" y="12.6" text-anchor="middle" font-family="Nimbus Sans" font-weight="bold" font-size="7" fill="#1f201e" letter-spacing="0.25">RECEPÇÃO</text>
</g></svg>'''
    img = svg_matrix('Interfone_Identificacao', svg)[..., :3]
    h, w = img.shape[:2]
    rng = np.random.default_rng(97)
    yy, xx = np.mgrid[:h, :w].astype(float)
    tape = img.mean(2) > .85
    finger = np.exp(-((xx - w * .55) / (w * .12)) ** 2 - ((yy - h * .55) / (h * .45)) ** 2)
    img = img * (1 + .03 * noise((h, w), (4, 10), rng, 6))[..., None]
    img = mix(img, (150, 146, 134), finger * tape * .12 + edge_band((h, w), rng, 'top', 2 * S, S) * .2)
    # The strip's box UVs run upside down on the intercom face: store the image rotated by 180 degrees.
    save_rgb(img[::-1, ::-1], 'Interfone_Identificacao')
    save_mask((np.where(tape, .55, .32) - finger * .12)[::-1, ::-1], 'Interfone_Identificacao')


def main():
    atendimento()
    interfone()
    print('Placas externas: matrizes SVG e texturas atualizadas.')


if __name__ == '__main__':
    main()

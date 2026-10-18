"""Comparações de capturas reais arquivadas; não acessa nem modifica a Unity."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'integracao-a'
font=ImageFont.truetype('/usr/share/fonts/noto/NotoSans-Regular.ttf',28)
views=[('frente-player','Frente · altura do Player'),('angulo-player','Ângulo · altura do Player'),('perto-inspecao','Perto · câmera elevada de inspeção')]
sections=''
for key,title in views:
    before=Image.open(OUT/'antes'/f'{key}.png').convert('RGB')
    after=Image.open(OUT/'depois'/f'{key}.png').convert('RGB')
    pair=Image.new('RGB',(2880,1136),'#e8e3d7');pair.paste(before,(0,56));pair.paste(after,(1440,56))
    d=ImageDraw.Draw(pair);d.text((24,8),'ANTES · render Unity',font=font,fill='#282923');d.text((1464,8),'DEPOIS · proposta A integrada',font=font,fill='#282923')
    pair.save(OUT/f'comparacao-{key}.png')
    sections+=f'<section><h2>{title}</h2><div class="pair"><figure><a href="antes/{key}.png"><img src="antes/{key}.png" alt="Antes: {title}"></a><figcaption>Antes</figcaption></figure><figure><a href="depois/{key}.png"><img src="depois/{key}.png" alt="Depois: {title}"></a><figcaption>Depois · A integrada</figcaption></figure></div><a href="comparacao-{key}.png">Abrir comparação em resolução completa</a></section>'
page=f'''<!doctype html><html lang="pt-BR"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Unclaimed · letreiro A integrado</title><style>body{{margin:0;background:#ece8df;color:#272923;font:17px/1.5 system-ui,sans-serif}}main{{max-width:1600px;margin:auto;padding:32px 24px}}h1{{font-size:34px}}h2{{font-size:23px}}a{{color:#275c48}}section{{margin-top:42px}}.pair{{display:grid;grid-template-columns:1fr 1fr;gap:12px}}figure{{margin:0}}img{{display:block;width:100%;height:auto}}figcaption{{padding:8px 0}}.note{{padding:18px;background:#ded8ca}}@media(max-width:800px){{.pair{{grid-template-columns:1fr}}}}</style><main><h1>Letreiro principal · proposta A integrada</h1><p>17/10/2026 · <a href="README.md">Relatório e verificação</a> · <a href="../comunicacao-visual.md">Guia atualizado</a> · <a href="../../../Blender/Entorno/Acabamentos/fontes/Placa_Fachada_A.svg">Matriz SVG editável</a></p><p class="note">Renders reais do Editor Unity, mesma câmera em cada par, FOV 60°, 1440 × 1080. Iluminação noturna existente, sem alterar luzes, exposição ou pós-processamento. As duas primeiras vistas têm altura de olho de 1,65 m; a terceira é inspeção elevada. Clique nas imagens para avaliar no tamanho original. Não são as montagens 2D dos estudos.</p><p>Chapa fosca com retorno de 8 mm, afastadores de 8 mm e quatro pequenas fixações. Tipografia à esquerda, margens refinadas e desgaste localizado. Sem emissão, faixas azuis, régua divisória, brasão ou ornamentos. Somente a identificação principal foi substituída.</p>{sections}</main></html>'''
(OUT/'comparacao.html').write_text(page)
print('Comparações e galeria real gravadas em',OUT)

if __name__=='__main__':pass

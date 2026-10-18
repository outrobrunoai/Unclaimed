"""Propostas editoriais; não acessa Unity nem escreve em Assets. Requer Pillow e rsvg-convert."""
from pathlib import Path
import base64, io, subprocess, html
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT.parents[1]
OUT = ROOT / 'propostas'
OUT.mkdir(exist_ok=True)
TITLE = 'NECROTÉRIO MUNICIPAL'
SUB = 'Secretaria Municipal de Saúde'

def text(x,y,value,size,family='Nimbus Sans',weight='normal',fill='#292a27',extra=''):
    return f'<text x="{x}" y="{y}" font-family="{family}" font-size="{size}" font-weight="{weight}" fill="{fill}" {extra}>{html.escape(value)}</text>'

def svg(body,w=2400,h=520,defs=''):
    return f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="{w}" height="{h}" viewBox="0 0 {w} {h}"><title>Unclaimed — proposta de comunicação visual</title><defs>{defs}</defs>{body}</svg>'

def uri(im):
    b=io.BytesIO(); im.save(b,format='PNG')
    return 'data:image/png;base64,'+base64.b64encode(b.getvalue()).decode()

bodies={}
screws=''.join(f'<circle cx="{x}" cy="{y}" r="7" fill="#807c72"/><path d="M{x-4} {y}h8" stroke="#514e48" stroke-width="2"/>' for x,y in [(30,30),(2370,30),(30,490),(2370,490)])
bodies['a-chapa'] = '<rect x="2" y="2" width="2396" height="516" rx="7" fill="#e2ddcd" stroke="#989486" stroke-width="4"/>'+screws+text(110,270,TITLE,192,'Nimbus Sans Narrow','bold')+text(116,397,SUB,77)
# B: lettering on masonry, transparent background. The small affiliation is also applied lettering.
bodies['b-letras'] = text(95,205,'NECROTÉRIO',191,'Nimbus Sans','bold','#dfdace')+text(95,418,'MUNICIPAL',191,'Nimbus Sans','bold','#dfdace')+text(1530,355,'Secretaria Municipal',62,'Nimbus Sans','normal','#d4d0c5')+text(1530,430,'de Saúde',62,'Nimbus Sans','normal','#d4d0c5')
# C: mineral paint field, typographic hierarchy inverted, title in a sturdy serif.
bodies['c-pintura'] = '<rect width="2400" height="520" fill="#d7d0bc"/>'+text(118,112,SUB,67,'Nimbus Sans','normal','#514d43')+text(110,272,'NECROTÉRIO',167,'Nimbus Roman','bold','#443c31')+text(110,447,'MUNICIPAL',167,'Nimbus Roman','bold','#443c31')

base=Image.open(PROJECT/'Blender/Entorno/Acabamentos/revisao/depois/entrada-player.png').convert('RGB')
base.save(OUT/'base-fachada.png')
# Remove only the existing plate: smoothly interpolate masonry at its perimeter.
# This is a local compositing reconstruction, not an exported scene change.
wall=base.copy()
x,y,w,h=406,97,627,138
px,py,pw,ph=404,95,631,144
pixels=np.asarray(base).astype(float)
top=pixels[py-1,px:px+pw]; bottom=pixels[py+ph,px:px+pw]
patch=np.linspace(top,bottom,ph)
for iteration in range(1800):
    patch[0]=top; patch[-1]=bottom
    patch[:,0]=pixels[py:py+ph,px-1]; patch[:,-1]=pixels[py:py+ph,px+pw]
    patch[1:-1,1:-1]=(patch[:-2,1:-1]+patch[2:,1:-1]+patch[1:-1,:-2]+patch[1:-1,2:])*.25
wall.paste(Image.fromarray(patch.astype('uint8')),(px,py))
wall.save(OUT/'base-parede-reconstituida.png')
# Existing pale sign photograph supplies a spatial colour/shadow estimate.
# Median removes the previous black typography. There is no extra light source.
old=Image.open(PROJECT/'Blender/Entorno/Acabamentos/revisao/antes/entrada-player.png').convert('RGB')
illum=old.crop((x,y,x+w,y+h)).filter(ImageFilter.MedianFilter(45)).filter(ImageFilter.GaussianBlur(9))
arr=np.asarray(illum).astype(float)/255
arr=np.clip(arr / np.array([.82,.80,.75]), .15,.85)
light=Image.fromarray((arr*255).astype('uint8'))
light.save(OUT/'campo-luz-aproximado.png')

for key,body in bodies.items():
    flat=svg(body)
    (OUT/f'{key}.svg').write_text(flat)
    subprocess.run(['rsvg-convert',str(OUT/f'{key}.svg'),'-o',str(OUT/f'{key}.png')],check=True)
    # Editable text remains in facade SVG; colour shading uses a raster light field.
    filters=f'''<filter id="night" x="-2%" y="-2%" width="104%" height="104%" color-interpolation-filters="sRGB"><feImage x="0" y="0" width="2400" height="520" preserveAspectRatio="none" xlink:href="{uri(light)}" result="light"/><feComposite in="SourceGraphic" in2="light" operator="arithmetic" k1="1" k2="0" k3="0" k4="0"/></filter>'''
    facade=f'<image width="1440" height="1080" xlink:href="{uri(wall)}"/><g transform="translate({x} {y}) scale({w/2400} {h/520})"><g filter="url(#night)">{body}</g></g>'
    (OUT/f'{key}-fachada.svg').write_text(svg(facade,1440,1080,filters))
    subprocess.run(['rsvg-convert',str(OUT/f'{key}-fachada.svg'),'-o',str(OUT/f'{key}-fachada.png')],check=True)
    im=Image.open(OUT/f'{key}-fachada.png')
    im.crop((360,60,1080,330)).save(OUT/f'{key}-detalhe.png')

names=[('a-chapa','A · Chapa administrativa','Uma linha, alinhamento à esquerda, chapa clara fosca e fixações aparentes. O nome domina; a vinculação ocupa uma segunda escala.'),('b-letras','B · Letras na arquitetura','Nome em dois níveis, letras claras aplicadas e vinculação independente à direita. Sem placa de fundo, moldura ou régua divisória.'),('c-pintura','C · Identificação pintada','Campo mineral pintado, nome serifado em dois níveis e vinculação acima. Sem volume de placa ou parafusos; manutenção por repintura.')]
cards=''
for key,name,note in names:
    cards+=f'''<section id="{key}"><h2>{name}</h2><p>{note}</p><h3>Estudo plano · 2,40 × 0,52 m</h3><div class="flat {'wall' if key=='b-letras' else ''}"><img src="propostas/{key}.png" alt="{name}, versão plana"/></div><p><a href="propostas/{key}.svg">SVG plano editável</a> · <a href="propostas/{key}-fachada.svg">SVG da montagem com texto editável</a></p><h3>Mesma câmera do Player · captura noturna existente</h3><img class="facade" src="propostas/{key}-fachada.png" alt="{name} na fachada à noite"/><h3>Recorte da mesma montagem · sem nova exposição</h3><img class="detail" src="propostas/{key}-detalhe.png" alt="Recorte do letreiro"/></section>'''
refs=[('r01-iml-rio.jpg','R01 · IML Rio · fotografia'),('r02-posto-fortaleza.jpg','R02 · Posto Guiomar Arruda · fotografia municipal'),('r03-direcional-ebserh-p164.png','R03 · Ebserh · orientação, desenho de manual'),('r04-porta-folha-ebserh-p182.png','R04 · Ebserh · porta-folha, desenho de manual'),('r05-hospital-santo-amaro-impresso.jpg','R05 · Hospital Santo Amaro · placa + impresso, fotografia'),('r06-gec-ses-sp.png','R06 · SES-SP · formulário oficial em branco')]
board=''.join(f'<figure><a href="referencias/{f}"><img src="referencias/{f}" alt="{n}" loading="lazy"/></a><figcaption>{n}</figcaption></figure>' for f,n in refs)
page=f'''<!doctype html><html lang="pt-BR"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Unclaimed · comunicação visual em revisão</title><style>body{{margin:0;background:#eeeae2;color:#282923;font:17px/1.55 system-ui,sans-serif}}main{{max-width:1200px;margin:auto;padding:40px 24px}}h1{{font-size:36px}}h2{{font-size:29px}}h3{{font-size:17px}}a{{color:#285747}}nav{{display:flex;gap:24px;flex-wrap:wrap}}section{{margin:60px 0;padding-top:24px;border-top:1px solid #b9b6ab}}img{{max-width:100%;height:auto}}.flat{{padding:24px;background:#fff}}.wall{{background:#4b4944}}.facade{{display:block;width:100%;}}.detail{{display:block;width:720px}}.refs{{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:20px}}figure{{margin:0;background:white;padding:12px}}figure img{{width:100%;height:230px;object-fit:contain}}figcaption{{font-size:14px}}.note{{padding:18px;background:#dfdacb}}footer{{font-size:14px}}</style><main><h1>Unclaimed · comunicação visual</h1><p>Estudos originais de 17/10/2026 · ver acima a integração aprovada de A.</p><nav><a href="#a-chapa">A · Chapa</a><a href="#b-letras">B · Letras</a><a href="#c-pintura">C · Pintura</a><a href="comunicacao-visual.md">Guia para Codex e Claude</a><a href="referencias/README.md">Fontes e observações</a></nav><p class="note">Três soluções de composição e construção, não três trocas de cor. Todos os estudos preservam somente os textos existentes. As imagens noturnas são montagens 2D na captura arquivada do Player: mesma câmera, enquadramento e iluminação de base, sem luz adicional. O fundo do letreiro foi reconstruído localmente; resposta de material, sombras de relevo e leitura em movimento ainda exigem validação na Unity após escolha. Na proposta B, o cinza do estudo plano representa apenas a parede, não uma placa.</p>{cards}<section><h2>Referências examinadas visualmente</h2><p>Fotografias, desenhos técnicos e formulário têm naturezas diferentes. As notas registram essa distinção, os links originais e o que será aproveitado. Clique para abrir em tamanho maior.</p><div class="refs">{board}</div></section><footer>Textos em SVG permanecem editáveis. Fontes locais: Nimbus Sans, Nimbus Sans Narrow e Nimbus Roman. PNGs registram a aparência sem depender de fontes no computador de revisão. Reprodução: python3 docs/direcao-arte/scripts/gerar_propostas.py.</footer></main></html>'''
page=page.replace('<main><h1>Unclaimed · comunicação visual</h1>','<main><p class="note"><strong>Proposta A escolhida e integrada.</strong> <a href="integracao-a/comparacao.html">Ver comparação real na Unity</a>. A galeria abaixo preserva os três estudos originais.</p><h1>Unclaimed · comunicação visual</h1>')
(ROOT/'index.html').write_text(page)
print('3 estudos planos, 3 montagens editáveis e galeria gerados em',ROOT)

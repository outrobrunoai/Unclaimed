"""Precise typesetting and original foliage textures; no generated text/images or external artwork."""
from pathlib import Path
import math, random
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / 'Assets/Modelos/Entorno/Acabamentos/Texturas'
FONT = '/usr/share/fonts/noto/NotoSans-Regular.ttf'
BOLD = '/usr/share/fonts/noto/NotoSans-Bold.ttf'

def text(d, words, box, size, bold=False, color=(29,48,49)):
    x,y,w,h=box
    f=ImageFont.truetype(BOLD if bold else FONT,size)
    while d.textlength(words,font=f)>w:
        size-=1;f=ImageFont.truetype(BOLD if bold else FONT,size)
    d.text((x+w/2,y+h/2),words,font=f,fill=color,anchor='mm')

def signs():
    # The approved permanent identification has its own matrix/material language.
    import runpy
    runpy.run_path(str(Path(__file__).with_name('letreiro.py')),run_name='__main__')
    specs={
      'Placa_Atendimento':((1200,900), [('Atendimento a familiares',.20,92,True),('24 horas',.38,112,True),('Fora do horário comercial,',.66,76,False),('toque o interfone',.79,76,False)]),
      'Placa_Numero':((960,480),[('Nº 840',.5,230,True)]),
      'Placa_NaoEstacione':((1400,700),[('NÃO ESTACIONE',.22,130,True),('Entrada e saída de veículos',.55,80,False),('do serviço funerário',.76,80,False)]),
      'Placa_Guarita':((1500,570),[('PORTARIA',.5,200,True)])
    }
    for name,(size,lines) in specs.items():
        w,h=size;im=Image.new('RGB',size,(232,234,225));d=ImageDraw.Draw(im)
        if name=='Placa_NaoEstacione':d.rectangle((0,0,w,int(h*.37)),fill=(155,43,38))
        else:d.rectangle((0,0,int(w*.025),h),fill=(27,62,72))
        for words,yy,sz,bold in lines:
            color=(234,237,225) if name=='Placa_NaoEstacione' and yy<.3 else (29,48,49)
            text(d,words,(w*.065,h*yy-h*.1,w*.87,h*.2),sz,bold,color)
        im.save(OUT/(name+'.png'))
    im=Image.new('RGB',(512,512),(190,192,179));d=ImageDraw.Draw(im)
    text(d,'INTERFONE',(20,15,472,80),58,True)
    # Bell silhouette drawn by exact vector primitives, no invented narrative.
    d.arc((175,140,337,330),180,360,fill=(36,43,42),width=14)
    d.line((176,233,161,327,351,327,336,233),fill=(36,43,42),width=14)
    d.ellipse((242,335,270,360),fill=(36,43,42))
    im.save(OUT/'Interfone_Identificacao.png')

def foliage():
    # RGB-only textures: silhouette is closed geometry, therefore no alpha fringe.
    random.seed(720)
    for name in ['Espada_Folha','Filodendro_Folha']:
        im=Image.new('RGB',(256,1024));px=im.load()
        for y in range(1024):
            t=y/1023
            for x in range(256):
                u=x/255
                if name=='Espada_Folha':
                    band=math.sin(t*160+math.sin(u*13)*1.4)*math.sin(t*47+u*7)
                    edge=max(0,1-min(u,1-u)/.07)
                    v=0.8+0.12*band+0.06*math.sin(t*33+u*4)
                    c=((49*v+edge*34),(85*v+edge*32),(43*v-edge*4))
                else:
                    vein=math.exp(-((u-.5)/.012)**2)
                    rib=max(0,math.cos(t*95+abs(u-.5)*20))**18*.06
                    v=.78+math.sin(u*math.pi)*.2+rib
                    c=(42*v+vein*10,82*v+vein*15,40*v+vein*6)
                px[x,y]=tuple(int(max(0,min(255,v+random.uniform(-2,2)))) for v in c)
        im.save(OUT/(name+'.png'))

def main():
    OUT.mkdir(parents=True,exist_ok=True);signs();foliage();print('8 precise RGB textures written:',OUT)
if __name__=='__main__':main()

# Printed furniture labels. Rectangles and Unity-local surface frames live in Etiquetas.json.
import json
import os
import sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '../../..'))
DIR = os.path.join(ROOT, 'Assets/Modelos/Administracao')
sys.path.insert(0, os.path.join(ROOT, 'Blender/Recepcao/scripts'))
import quadro_avisos as qa


def main():
    data = json.load(open(os.path.join(DIR, 'Etiquetas.json'), encoding='utf-8'))
    atlas = Image.new('RGB', tuple(data['atlas']), '#eee8d8')
    for item in data['etiquetas']:
        x, y, w, h = item['rect']
        # Render at the physical label aspect ratio, then pack into a fixed atlas cell.
        ph = max(96, round(w * item['tamanho'][1] / item['tamanho'][0]))
        tile = Image.new('RGB', (w, ph), '#e5dfcf' if item['dano'] else '#eeebe0')
        d = ImageDraw.Draw(tile)
        text = item['texto']
        for size in range(min(ph//2, 140), 10, -1):
            f = ImageFont.truetype(qa.NOTO + 'NotoSans-Regular.ttf', size)
            lines = qa.wrap(text, f, w-36)
            if len(lines)*size*1.38 < ph-20 and max((f.getlength(line) for line in lines), default=0) <= w-36:break
        ty = (ph-len(lines)*size*1.38)/2
        for line in lines:
            d.text(((w-f.getlength(line))/2, ty), line, fill='#292d30', font=f)
            ty += size*1.38
        if item['dano']=='rasgada':d.polygon([(w*.62,0),(w*.49,ph*.32),(w*.66,ph*.57),(w*.53,ph),(w,ph),(w,0)], fill='#d7cfb7')
        if item['dano']=='borrada':
            tile = tile.filter(ImageFilter.GaussianBlur(5));d=ImageDraw.Draw(tile);d.line((w*.30,ph*.25,w*.65,ph*.8), fill='#a4a396',width=40)
        if item['dano']=='descolada':d.polygon([(0,ph*.6),(w*.4,ph),(0,ph)],fill='#bdb49e')
        atlas.paste(tile.resize((w-8,h-8),Image.Resampling.LANCZOS),(x+4,y+4))
    os.makedirs(os.path.join(DIR,'Texturas'),exist_ok=True)
    path=os.path.join(DIR,'Texturas/Etiquetas_Admin.png');atlas.save(path)
    print(f"{path}: {atlas.size}, {len(data['etiquetas'])} labels")


if __name__=='__main__':main()

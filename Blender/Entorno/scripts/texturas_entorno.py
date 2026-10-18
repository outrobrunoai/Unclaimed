# Deterministic exterior signage and window textures (Python 3 + Pillow).
# Run with: python3 Blender/Entorno/scripts/texturas_entorno.py
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "..", "Assets", "UnclaimedPrototype", "Entorno", "Texturas")
FONT = "/usr/share/fonts/noto/NotoSans-Regular.ttf"
FONT_B = "/usr/share/fonts/noto/NotoSans-Bold.ttf"
RNG = np.random.default_rng(2417)


def font(size, bold=False):
    return ImageFont.truetype(FONT_B if bold else FONT, size)


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name)
    img.save(path)
    return path


def fitted(draw, text, width, size, bold=True):
    f = font(size, bold)
    while draw.textlength(text, font=f) > width and size > 10:
        size -= 2
        f = font(size, bold)
    return f


def plate(name, size, lines, color=(226, 225, 218), accent=None):
    w, h = size
    img = Image.new("RGB", size, color)
    d = ImageDraw.Draw(img)
    # Fine brushed-metal grain and a restrained inset border.
    px = np.asarray(img).astype(np.int16)
    noise = RNG.normal(0, 2.0, (h, w, 1))
    stripe = (np.sin(np.arange(h)[:, None, None] * .42) * 1.1)
    px = np.clip(px + noise + stripe, 0, 255).astype(np.uint8)
    img = Image.fromarray(px, "RGB")
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((3, 3, w-4, h-4), radius=max(4, h//28), outline=(125, 127, 124), width=max(2, h//150))
    if accent:
        d.rectangle((0, 0, max(8, w//42), h), fill=accent)
    if len(lines) == 1:
        f = fitted(d, lines[0], w*.88, int(h*.34))
        d.text((w/2, h/2), lines[0], font=f, fill=(39, 43, 43), anchor="mm")
    else:
        f0 = fitted(d, lines[0], w*.88, int(h*.34))
        f1 = fitted(d, lines[1], w*.88, int(h*.16), False)
        d.text((w/2, h*.43), lines[0], font=f0, fill=(39, 43, 43), anchor="mm")
        d.text((w/2, h*.73), lines[1], font=f1, fill=(74, 76, 73), anchor="mm")
    return save(img, name)


def curtain(name, lit):
    n = 512
    y, x = np.mgrid[0:n, 0:n]
    folds = (np.cos(x * 2*np.pi/72) * .13 + np.cos(x * 2*np.pi/36) * .045)
    broad = .10 * np.cos(x * 2*np.pi/256)
    grain = RNG.normal(0, .012, (n, n))
    if lit:
        base = np.array([.72, .56, .37])[None, None, :]
        shade = np.clip(.84 + folds + broad + grain + .08*np.sin(y*2*np.pi/512), .45, 1.08)
    else:
        base = np.array([.075, .105, .14])[None, None, :]
        shade = np.clip(.78 + folds + broad + grain, .4, 1.05)
    rgb = np.clip(base * shade[..., None] * 255, 0, 255).astype(np.uint8)
    img = Image.fromarray(rgb, "RGB")
    d = ImageDraw.Draw(img)
    for xx in range(18, n, 72):
        d.line((xx, 0, xx, n), fill=(190, 155, 110) if lit else (36, 49, 65), width=2)
    return save(img, name)


def dark_interior():
    n = 512
    y, x = np.mgrid[0:n, 0:n]
    rng = np.random.default_rng(912)
    noise = rng.normal(0, 2.5, (n, n))
    glow = np.exp(-(((x-270)/170)**2 + ((y-270)/230)**2)) * 6
    base = np.zeros((n,n,3), dtype=np.float32)
    base[:] = (13, 17, 21)
    base += noise[...,None] + glow[...,None]
    img = Image.fromarray(np.clip(base,0,255).astype(np.uint8), "RGB").filter(ImageFilter.GaussianBlur(3))
    d = ImageDraw.Draw(img)
    d.rectangle((300, 250, 430, 360), fill=(22, 24, 25))
    d.rectangle((320, 225, 415, 255), fill=(25, 27, 28))
    d.line((315, 360, 315, 390), fill=(18,20,21), width=9)
    d.line((415, 360, 415, 390), fill=(18,20,21), width=9)
    return save(img, "Janela_Interior_Escuro.png")


def no_parking():
    w,h=512,256
    img=Image.new("RGB",(w,h),(245,242,232)); d=ImageDraw.Draw(img)
    d.rectangle((0,0,w-1,h-1),outline=(190,34,30),width=18)
    d.rectangle((0,0,w,54),fill=(190,34,30))
    f=fitted(d,"NÃO ESTACIONE",w-36,42)
    d.text((w/2,27),"NÃO ESTACIONE",font=f,fill="white",anchor="mm")
    f2=fitted(d,"Entrada e saída de veículos",w-32,28,False)
    f3=fitted(d,"do serviço funerário",w-32,28,False)
    d.text((w/2,116),"Entrada e saída de veículos",font=f2,fill=(50,50,48),anchor="mm")
    d.text((w/2,158),"do serviço funerário",font=f3,fill=(50,50,48),anchor="mm")
    return save(img,"Placa_NaoEstacione.png")


def main():
    plate("Placa_Fachada.png",(2048,448),["NECROTÉRIO MUNICIPAL","Secretaria Municipal de Saúde"])
    plate("Placa_Atendimento.png",(512,384),["Atendimento a familiares — 24 horas","Fora do horário comercial, toque o interfone"])
    plate("Placa_Numero.png",(256,128),["Nº 840"])
    no_parking()
    plate("Placa_Guarita.png",(256,96),["PORTARIA"])
    curtain("Janela_Cortina_Acesa.png",True)
    curtain("Janela_Cortina_Apagada.png",False)
    dark_interior()
    for name in ("Placa_Fachada.png", "Placa_Atendimento.png", "Placa_Numero.png", "Placa_NaoEstacione.png", "Placa_Guarita.png", "Janela_Cortina_Acesa.png", "Janela_Cortina_Apagada.png", "Janela_Interior_Escuro.png"):
        with Image.open(os.path.join(OUT, name)) as image:
            print(f"{name}: {image.size} {image.mode}")


if __name__ == "__main__":
    main()

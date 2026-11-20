# Desenha os ícones, o papel de parede e as formas da interface do celular (Pillow).
#   python icones.py
# Saída: Assets/Prefabs/Sistemas/Celular/Icones/*.png
# Tudo é desenhado 4x maior e reduzido, para as bordas ficarem lisas.
import math
import os

from PIL import Image, ImageDraw, ImageFilter

AQUI = os.path.dirname(os.path.abspath(__file__))
SAIDA = os.path.abspath(os.path.join(AQUI, "..", "..", "..", "Assets", "Prefabs", "Sistemas", "Celular", "Icones"))
SS = 4


def tela(l, a, fundo=(0, 0, 0, 0)):
    img = Image.new("RGBA", (l * SS, a * SS), fundo)
    return img, ImageDraw.Draw(img)


def salvar(img, nome, l, a):
    os.makedirs(SAIDA, exist_ok=True)
    img.resize((l, a), Image.LANCZOS).save(os.path.join(SAIDA, nome + ".png"))


def gradiente(l, a, cima, baixo):
    g = Image.new("RGBA", (l, a))
    px = g.load()
    for y in range(a):
        t = y / max(1, a - 1)
        c = tuple(int(cima[i] + (baixo[i] - cima[i]) * t) for i in range(3)) + (255,)
        for x in range(l):
            px[x, y] = c
    return g


def mascara_arredondada(l, a, raio):
    m = Image.new("L", (l, a), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, l - 1, a - 1), raio, fill=255)
    return m


def base_icone(cima, baixo, n=256):
    l = n * SS
    fundo = gradiente(l, l, cima, baixo)
    img = Image.new("RGBA", (l, l), (0, 0, 0, 0))
    img.paste(fundo, (0, 0), mascara_arredondada(l, l, int(l * 0.225)))
    return img, ImageDraw.Draw(img), l


BRANCO = (255, 255, 255, 255)


def icone_mensagens():
    img, d, l = base_icone((110, 230, 120), (30, 175, 60))
    cx, cy = l * 0.5, l * 0.47
    w, h = l * 0.62, l * 0.48
    d.ellipse((cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2), fill=BRANCO)
    # rabinho do balão
    d.polygon([(cx - w * 0.28, cy + h * 0.28), (cx - w * 0.42, cy + h * 0.62), (cx - w * 0.05, cy + h * 0.42)], fill=BRANCO)
    salvar(img, "app_mensagens", 256, 256)


def icone_camera():
    img, d, l = base_icone((200, 203, 210), (120, 124, 132))
    escuro = (40, 42, 48, 255)
    x0, y0, x1, y1 = l * 0.17, l * 0.33, l * 0.83, l * 0.75
    d.rounded_rectangle((l * 0.37, l * 0.25, l * 0.63, l * 0.36), int(l * 0.04), fill=escuro)
    d.rounded_rectangle((x0, y0, x1, y1), int(l * 0.08), fill=escuro)
    cx, cy, r = l * 0.5, l * 0.54, l * 0.15
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(200, 203, 210, 255))
    r2 = r * 0.72
    d.ellipse((cx - r2, cy - r2, cx + r2, cy + r2), fill=escuro)
    d.ellipse((l * 0.69, l * 0.39, l * 0.75, l * 0.45), fill=(255, 200, 90, 255))
    salvar(img, "app_camera", 256, 256)


def icone_galeria():
    img, d, l = base_icone((255, 255, 255), (232, 234, 240))
    cx, cy = l * 0.5, l * 0.5
    cores = [(255, 190, 40), (255, 120, 40), (240, 70, 90), (180, 80, 200), (70, 120, 240), (40, 180, 230), (60, 200, 120), (170, 210, 60)]
    for i, c in enumerate(cores):
        a = math.radians(i * 45)
        px, py = cx + math.cos(a) * l * 0.17, cy + math.sin(a) * l * 0.17
        rx, ry = l * 0.17, l * 0.09
        petala = Image.new("RGBA", img.size, (0, 0, 0, 0))
        dp = ImageDraw.Draw(petala)
        dp.ellipse((px - rx, py - ry, px + rx, py + ry), fill=c + (215,))
        petala = petala.rotate(-i * 45, center=(px, py))
        img.alpha_composite(petala)
    salvar(img, "app_galeria", 256, 256)


def icone_provas():
    img, d, l = base_icone((95, 165, 255), (35, 95, 220))
    # avião de papel
    p = [(l * 0.2, l * 0.52), (l * 0.8, l * 0.24), (l * 0.6, l * 0.8), (l * 0.47, l * 0.6)]
    d.polygon(p, fill=BRANCO)
    d.polygon([(l * 0.47, l * 0.6), (l * 0.8, l * 0.24), (l * 0.44, l * 0.74)], fill=(205, 222, 255, 255))
    salvar(img, "app_provas", 256, 256)


def glifo_lanterna():
    img, d = tela(128, 128)
    l = 128 * SS
    d.polygon([(l * 0.32, l * 0.12), (l * 0.68, l * 0.12), (l * 0.62, l * 0.36), (l * 0.38, l * 0.36)], fill=BRANCO)
    d.rounded_rectangle((l * 0.38, l * 0.38, l * 0.62, l * 0.88), int(l * 0.04), fill=BRANCO)
    d.ellipse((l * 0.46, l * 0.5, l * 0.54, l * 0.58), fill=(0, 0, 0, 0))
    salvar(img, "glifo_lanterna", 128, 128)


def glifo_raio():
    img, d = tela(128, 128)
    l = 128 * SS
    d.polygon([(l * 0.58, l * 0.06), (l * 0.22, l * 0.56), (l * 0.48, l * 0.56), (l * 0.4, l * 0.94), (l * 0.78, l * 0.42), (l * 0.52, l * 0.42)], fill=BRANCO)
    salvar(img, "glifo_raio", 128, 128)


def formas():
    img, d = tela(128, 128)
    l = 128 * SS
    d.ellipse((0, 0, l - 1, l - 1), fill=BRANCO)
    salvar(img, "circulo", 128, 128)
    img, d = tela(128, 128)
    d.ellipse((0, 0, l - 1, l - 1), outline=BRANCO, width=int(l * 0.07))
    salvar(img, "anel", 128, 128)
    img, d = tela(128, 128)
    d.rounded_rectangle((0, 0, l - 1, l - 1), int(l * 0.375), fill=BRANCO)
    salvar(img, "arredondado", 128, 128)


def papel_de_parede():
    l, a = 540, 1180
    img = gradiente(l, a, (18, 22, 40), (6, 7, 12)).convert("RGBA")
    manchas = Image.new("RGBA", (l, a), (0, 0, 0, 0))
    d = ImageDraw.Draw(manchas)
    for (x, y, r, c) in ((0.2, 0.25, 0.45, (60, 70, 140, 120)), (0.85, 0.55, 0.5, (90, 40, 110, 90)), (0.35, 0.9, 0.4, (30, 90, 120, 80))):
        d.ellipse(((x - r) * l, y * a - r * l, (x + r) * l, y * a + r * l), fill=c)
    manchas = manchas.filter(ImageFilter.GaussianBlur(90))
    img.alpha_composite(manchas)
    os.makedirs(SAIDA, exist_ok=True)
    img.convert("RGB").save(os.path.join(SAIDA, "papel_de_parede.png"))


icone_mensagens()
icone_camera()
icone_galeria()
icone_provas()
glifo_lanterna()
glifo_raio()
formas()
papel_de_parede()
print("Ícones em", SAIDA)

import sys, os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
src, out = sys.argv[1], sys.argv[2]

def lum(path):
    a = np.asarray(Image.open(path).convert("RGB").resize((1024, 1024), Image.LANCZOS), dtype=np.float32) / 255.0
    return a @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)

def var(L, k):
    # variação relativa à média, com contraste k (1 = original)
    return (L / L.mean() - 1.0) * k + 1.0

def salvar(nome, v, cor):
    rgb = np.clip(v[..., None] * np.array(cor, dtype=np.float32), 0, 1)
    Image.fromarray((rgb * 255 + 0.5).astype(np.uint8), "RGB").save(os.path.join(out, nome))
    print(nome, "media", np.round(rgb.reshape(-1, 3).mean(0), 3), "desvio", round(float(rgb.std()), 4))

P = lambda a, m="Color": os.path.join(src, a, f"{a}_1K-JPG_{m}.jpg")
fibra = lum(P("Paper001"))
amassado = lum(P("Paper003"))
pagina = var(fibra, 1.6) * var(amassado, 0.35)
salvar("papel_pagina.png", pagina, (0.93, 0.90, 0.81))
salvar("papel_indice.png", pagina[::-1, ::-1].copy(), (0.86, 0.82, 0.72))
salvar("papel_nota.png", var(fibra, 1.4) * var(amassado, 0.25), (0.97, 0.87, 0.47))
salvar("capa_papelao.png", var(lum(P("Paper006")), 1.3), (0.24, 0.18, 0.13))
salvar("divisoria_cartolina.png", var(lum(P("Paper005")), 1.5), (0.74, 0.66, 0.50))

# Uma volta da espiral (repete na vertical): dois furos no papel e o arame passando por cima.
SS = 4; W, H = 128, 48
img = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
def e(x0, y0, x1, y1, cor): d.ellipse([x0 * SS, y0 * SS, x1 * SS, y1 * SS], fill=cor)
for cx in (22, 106):  # furos
    e(cx - 7, 17, cx + 7, 31, (28, 24, 22, 235))
    e(cx - 7, 17, cx + 7, 22, (0, 0, 0, 120))
# arame: arco grosso com sombra, corpo e brilho
def arco(dx, dy, larg, cor):
    d.arc([(22 + dx) * SS, (6 + dy) * SS, (106 + dx) * SS, (44 + dy) * SS], 180, 360, fill=cor, width=larg * SS)
arco(2, 3, 7, (0, 0, 0, 90))
arco(0, 0, 7, (58, 60, 64, 255))
arco(0, 0, 4, (120, 124, 130, 255))
arco(0, -1, 1, (215, 220, 228, 255))
img = img.resize((W, H), Image.LANCZOS)
img.save(os.path.join(out, "espiral.png")); print("espiral.png", img.size)

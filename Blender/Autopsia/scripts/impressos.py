"""Printed items of the autopsy room (textures only; meshes come from corpo_base.py / the prefab builder).
Run: python3 Blender/Autopsia/scripts/impressos.py
  Etiqueta083.png     transfer tag 90 x 55 mm, 10 px/mm: number 083 (story), blank ruled fields, punched eyelet (alpha).
  AdesivoMao.png      blank sticker 22 x 15 mm, torn outline in alpha.
  TecidoLencol.png    tileable cotton weave of the sheets (0.25 m tile).
  AvisoProvisorio.png A5 landscape maintenance notice, 8 px/mm, taped to the wall above the exam-lamp switch.
                      Text approved by the user (2026-11-08); it does not explain the puzzle.
Fonts: Noto Sans (SIL OFL), as the other signage of the project."""
import os, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
OUT = ROOT + "/Assets/UnclaimedPrototype/Autopsia/Texturas"
BOLD, REG = "/usr/share/fonts/noto/NotoSans-Bold.ttf", "/usr/share/fonts/noto/NotoSans-Regular.ttf"
rnd = random.Random(83)

def grime(im, amount=10, blur=6):
    w, h = im.size; n = Image.effect_noise((w // 8, h // 8), 64).resize((w, h)).filter(ImageFilter.GaussianBlur(blur))
    base = im.convert("RGB"); px = base.load(); npx = n.load()
    for y in range(h):
        for x in range(w):
            d = (npx[x, y] - 128) * amount // 128
            r, g, b = px[x, y]; px[x, y] = (max(0, min(255, r + d)), max(0, min(255, g + d)), max(0, min(255, b + d - 1)))
    return base

def tag():
    W, H = 900, 550
    im = Image.new("RGB", (W, H), (236, 228, 205)); im = grime(im, 9)
    d = ImageDraw.Draw(im)
    d.rectangle((6, 6, W - 7, H - 7), outline=(120, 112, 95), width=3)
    d.text((150, 34), "MOVIMENTAÇÃO", font=ImageFont.truetype(BOLD, 44), fill=(45, 45, 48))
    d.line((150, 92, W - 40, 92), fill=(70, 70, 72), width=3)
    f = ImageFont.truetype(BOLD, 250)
    d.text((170, 95), "083", font=f, fill=(22, 22, 24))
    for y in (420, 480):                                    # blank fields, nothing filled in
        d.line((150, y, W - 40, y), fill=(120, 118, 110), width=2)
    # Punched eyelet with a reinforcement ring on the left; the hole is transparent (the sheet shows through).
    cx, cy = 75, H // 2
    d.ellipse((cx - 46, cy - 46, cx + 46, cy + 46), fill=(214, 206, 182), outline=(150, 140, 118), width=3)
    a = Image.new("L", (W, H), 255); ImageDraw.Draw(a).ellipse((cx - 22, cy - 22, cx + 22, cy + 22), fill=0)
    im = im.convert("RGBA"); im.putalpha(a)
    im.save(OUT + "/Etiqueta083.png"); return OUT + "/Etiqueta083.png"

def sticker():
    """Small blank sticker (22 x 15 mm, 16 px/mm) on the back of the right hand: worn paper, irregular torn
    outline as alpha. Nothing printed on it (no invented content)."""
    import math
    W, H = 352, 240
    im = Image.new("RGB", (W, H), (232, 228, 214)); im = grime(im, 12, 3)
    a = Image.new("L", (W * 2, H * 2), 0); r = random.Random(84)
    jag = [r.uniform(.84, 1.0) for _ in range(22)]; pts = []
    for k in range(220):
        t = k / 220 * 22; i = int(t) % 22; f = t - int(t); f = f * f * (3 - 2 * f)
        rr = (jag[i] * (1 - f) + jag[(i + 1) % 22] * f) * r.uniform(.985, 1.0)
        ang = k / 220 * math.tau
        pts.append((W + math.cos(ang) * rr * (W - 8), H + math.sin(ang) * rr * (H - 8)))
    ImageDraw.Draw(a).polygon(pts, fill=255)
    a = a.resize((W, H), Image.LANCZOS)
    im = im.convert("RGBA"); im.putalpha(a)
    im.save(OUT + "/AdesivoMao.png"); return OUT + "/AdesivoMao.png"

def linen():
    """Hospital cotton sheet, tileable 512 px for 0.25 m (0.5 mm/px): faint plain weave, slight grain and soft
    laundering mottle (+-2.5%). Material detail of the cloth; the drape and its normals come from the mesh."""
    import numpy as np
    N = 512; r = np.random.default_rng(83)
    def soft(scale):
        f = np.fft.fft2(r.standard_normal((N, N))); k = np.fft.fftfreq(N)[:, None] ** 2 + np.fft.fftfreq(N)[None, :] ** 2
        g = np.real(np.fft.ifft2(f * np.exp(-k * (N / scale) ** 2 * 2))); return g / (np.abs(g).max() + 1e-9)
    y, x = np.mgrid[0:N, 0:N]
    weave = ((x // 2 + y // 2) % 2) * 2 - 1.0                          # 1 mm plain weave
    lum = 1 + .025 * soft(6) + .012 * soft(60) + .008 * weave + .006 * r.standard_normal((N, N))
    base = np.array([219, 222, 219], float)                             # ~ (0.86, 0.87, 0.86)
    img = np.clip(base[None, None, :] * lum[..., None], 0, 255).astype(np.uint8)
    Image.fromarray(img, "RGB").save(OUT + "/TecidoLencol.png"); return OUT + "/TecidoLencol.png"

def notice():
    W, H = 1680, 1184
    im = Image.new("RGB", (W, H), (244, 242, 234)); im = grime(im, 6, 10)
    d = ImageDraw.Draw(im)
    d.text((110, 120), "MANUTENÇÃO", font=ImageFont.truetype(BOLD, 52), fill=(90, 90, 92))
    d.line((110, 190, W - 110, 190), fill=(90, 90, 92), width=4)
    ft = ImageFont.truetype(BOLD, 104)
    d.text((110, 270), "LUMINÁRIA DE EXAME EM", font=ft, fill=(20, 20, 22))
    d.text((110, 400), "ALIMENTAÇÃO PROVISÓRIA", font=ft, fill=(20, 20, 22))
    fb = ImageFont.truetype(REG, 64)
    d.text((110, 620), "Acionamento pelo painel de contingência", font=fb, fill=(30, 30, 32))
    d.text((110, 710), "(sala técnica).", font=fb, fill=(30, 30, 32))
    # Two strips of clear tape at the top corners (slightly glossy, a bit yellowed).
    for x0 in (60, W - 300):
        tape = Image.new("RGBA", (240, 70), (238, 232, 200, 120)).rotate(rnd.uniform(-6, 6), expand=True)
        im.paste(tape, (x0, 10), tape)
    im.save(OUT + "/AvisoProvisorio.png"); return OUT + "/AvisoProvisorio.png"

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    print(tag()); print(sticker()); print(linen()); print(notice())

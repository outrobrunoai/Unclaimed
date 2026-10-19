"""Pole concrete: Poly Haven granular_concrete re-graded to the light, slightly cool grey of spun/vibrated poles.
Keeps the grain (contrast scaled) and recentres the mean; output tiles like the source. Run: python3 textura_concreto.py"""
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[4]
SRC = ROOT / 'Assets/UnclaimedPrototype/Entorno/Texturas/granular_concrete_diff.jpg'
OUT = ROOT / 'Assets/Modelos/Entorno/Postes/Texturas/Concreto_Poste.png'
TARGET = np.array([.60, .60, .58])   # sRGB mean
CONTRAST = .75

a = np.asarray(Image.open(SRC).convert('RGB'), dtype=np.float64) / 255.
lum = a @ [.2126, .7152, .0722]
grain = (lum - lum.mean()) * CONTRAST
tint = (a - lum[..., None]) * .25          # keep a trace of the original colour variation
out = np.clip(TARGET + grain[..., None] + tint, 0, 1)
Image.fromarray((out * 255 + .5).astype(np.uint8)).save(OUT)
print(OUT, out.mean(axis=(0, 1)).round(3), (out ** 2.2).mean(axis=(0, 1)).round(3))

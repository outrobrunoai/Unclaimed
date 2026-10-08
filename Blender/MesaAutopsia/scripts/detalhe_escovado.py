# Tileable detail normal map for brushed stainless steel: fine brushing along U plus sparse micro-scratches.
import sys
import numpy as np
from PIL import Image

N = int(sys.argv[2]) if len(sys.argv) > 2 else 1024
rng = np.random.default_rng(7)

# Brushing: periodic white noise filtered in Fourier space, long along U (x), thin along V (y).
white = rng.standard_normal((N, N))
ky = np.fft.fftfreq(N)[:, None]
kx = np.fft.fftfreq(N)[None, :]
filt = np.exp(-(kx / 0.004) ** 2) * np.exp(-(ky / 0.35) ** 2)
brush = np.real(np.fft.ifft2(np.fft.fft2(white) * filt))
brush /= brush.std()
# a second, coarser layer so the grain is not uniform
white2 = rng.standard_normal((N, N))
filt2 = np.exp(-(kx / 0.0015) ** 2) * np.exp(-(ky / 0.06) ** 2)
brush2 = np.real(np.fft.ifft2(np.fft.fft2(white2) * filt2))
brush2 /= brush2.std()
h = 0.75 * brush + 0.35 * brush2

# Micro-scratches: thin wrapped segments, mostly near the brushing direction.
scr = np.zeros((N, N))
for _ in range(140):
    ang = rng.normal(0.0, 0.35) if rng.random() < 0.8 else rng.uniform(0, np.pi)
    length = rng.uniform(0.05, 0.35) * N
    x0, y0 = rng.uniform(0, N, 2)
    depth = rng.uniform(0.6, 1.6)
    t = np.linspace(0, 1, int(length * 2))
    xs = (x0 + np.cos(ang) * length * t) % N
    ys = (y0 + np.sin(ang) * length * t) % N
    fade = np.sin(np.pi * t) ** 0.5
    np.add.at(scr, (ys.astype(int), xs.astype(int)), -depth * fade)
# soften to ~1.5 px wide, periodic
g = np.exp(-(kx ** 2 + ky ** 2) / (2 * 0.12 ** 2))
scr = np.real(np.fft.ifft2(np.fft.fft2(scr) * g))
h = h + 2.2 * scr

def normal_from_height(h, strength):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5
    nx, ny, nz = -dx * strength, dy * strength, np.ones_like(h)   # image rows go down: flip Y for OpenGL (Y+)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([nx / l, ny / l, nz / l], -1)

n = normal_from_height(h, 0.09)
img = ((n * 0.5 + 0.5) * 255 + 0.5).clip(0, 255).astype(np.uint8)
Image.fromarray(img, "RGB").save(sys.argv[1])
print("tilt deg p50/p99:", np.degrees(np.arccos(n[..., 2])).round(2).__array__().flatten()[[0]], 
      np.percentile(np.degrees(np.arccos(n[..., 2])), [50, 99]).round(2))

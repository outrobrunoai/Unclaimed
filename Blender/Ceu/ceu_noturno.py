# Provisional night sky for PrototipoNecroterio: one equirectangular (latitude-longitude) image for
# Unity's built-in Skybox/Panoramic shader. Drawn procedurally (numpy + Pillow), no external images:
#   python3 Blender/Ceu/ceu_noturno.py
# Output: Assets/UnclaimedPrototype/Iluminacao/Ceu_Noturno.png (4096 x 2048, sRGB).
# Dark blue sky with faint stars, thin irregular clouds partly over them, the moon and a dark horizon.
# The moon is drawn in the direction opposite to the moonlight ("Luz da lua", MOON_EULER below, the
# same values UnclaimedMorgueGenerator.Iluminacao.cs gives the light), so shadows agree with the sky.
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "Assets", "UnclaimedPrototype", "Iluminacao", "Ceu_Noturno.png")
W, H = 4096, 2048
MOON_EULER = (20.0, 10.0)      # light pitch (down) and yaw, degrees, as in Unity's Transform.eulerAngles
MOON_DIAMETER = 1.6            # degrees (the real moon is 0.5; games show it larger to read at 60 fov)
RNG = np.random.default_rng(11)


def moon_direction():
    """Unit vector from the ground towards the moon (opposite of the light's forward), Unity axes."""
    pitch, yaw = np.radians(MOON_EULER)
    fwd = np.array([np.sin(yaw) * np.cos(pitch), -np.sin(pitch), np.cos(yaw) * np.cos(pitch)])
    return -fwd


def directions():
    """Per-pixel view direction for Skybox/Panoramic (lat-long): u = 0.5 - atan2(z, x) / 2pi,
    v = 1 - acos(y) / pi, image row 0 at the top (v = 1)."""
    u = (np.arange(W) + 0.5) / W
    v = 1.0 - (np.arange(H) + 0.5) / H
    lon = (0.5 - u) * 2 * np.pi                 # atan2(z, x)
    lat = (1.0 - v) * np.pi                     # acos(y), 0 at the zenith
    x = np.sin(lat)[:, None] * np.cos(lon)[None, :]
    z = np.sin(lat)[:, None] * np.sin(lon)[None, :]
    y = np.repeat(np.cos(lat)[:, None], W, axis=1)
    return x, y, z


def tileable_noise(scale, octaves, seed):
    """Value noise that wraps horizontally (the panorama's seam) built in the frequency domain."""
    rng = np.random.default_rng(seed)
    out = np.zeros((H // 2, W // 2))
    for o in range(octaves):
        fy = np.fft.fftfreq(H // 2)[:, None]
        fx = np.fft.fftfreq(W // 2)[None, :]
        k = scale * 2 ** o
        spec = np.fft.fft2(rng.standard_normal((H // 2, W // 2))) * np.exp(-((fx * W / 2) ** 2 + (fy * H / 2 * 2) ** 2) / k ** 2)
        layer = np.real(np.fft.ifft2(spec))
        out += layer / (layer.std() + 1e-9) / 1.9 ** o
    out = (out - out.min()) / (out.max() - out.min())
    return np.asarray(Image.fromarray((out * 65535).astype(np.uint16)).resize((W, H), Image.BICUBIC)) / 65535.0


def main():
    x, y, z = directions()
    alt = np.degrees(np.arcsin(np.clip(y, -1, 1)))   # altitude above the horizon
    m = moon_direction()
    cosang = np.clip(x * m[0] + y * m[1] + z * m[2], -1, 1)
    ang = np.degrees(np.arccos(cosang))               # angle from the moon

    # Sky gradient: deep blue at the zenith, slightly lighter and greyer low down, dark ground below.
    t = np.clip(alt / 60.0, 0, 1)[..., None]
    zenith = np.array([0.010, 0.016, 0.040])
    low = np.array([0.030, 0.038, 0.060])
    sky = low * (1 - t) + zenith * t
    sky += np.array([0.020, 0.026, 0.040]) * np.exp(-ang / 25.0)[..., None]        # glow around the moon
    ground = np.array([0.008, 0.009, 0.011])
    below = np.clip(-alt / 3.0, 0, 1)[..., None]                                      # soft dark horizon
    sky = sky * (1 - below) + ground * below
    sky *= (0.85 + 0.15 * np.clip(alt / 12.0, 0, 1))[..., None]                       # darker band at the horizon

    # Stars: uniform over the sphere, mostly faint, fewer near the horizon (haze) and near the moon.
    img = sky.copy()
    n = 4500
    zs = RNG.uniform(0.02, 1.0, n)                    # sin(altitude), uniform in area
    lon = RNG.uniform(-np.pi, np.pi, n)
    sx, sy, sz = np.sqrt(1 - zs ** 2) * np.cos(lon), zs, np.sqrt(1 - zs ** 2) * np.sin(lon)
    col = ((0.5 - np.arctan2(sz, sx) / (2 * np.pi)) % 1.0 * W).astype(int)
    row = ((np.arccos(sy) / np.pi) * H).astype(int)
    mag = RNG.power(4.0, n) ** 6                      # many faint, few bright
    haze = np.clip(np.degrees(np.arcsin(sy)) / 15.0, 0.15, 1.0)
    moonfade = np.clip((np.degrees(np.arccos(np.clip(sx * m[0] + sy * m[1] + sz * m[2], -1, 1))) - 4) / 20.0, 0, 1)
    bright = (0.06 + 0.6 * mag) * haze * moonfade
    tint = np.stack([0.85 + 0.15 * RNG.random(n), 0.9 + 0.1 * RNG.random(n), np.ones(n)], axis=1)
    for (r, c, b, tc) in zip(row, col, bright, tint):
        img[r % H, c % W] += tc * b
        if b > 0.45:                                  # the brighter ones get a small cross of light
            for dr, dc in ((0, 1), (0, -1), (1, 0), (-1, 0)):
                img[(r + dr) % H, (c + dc) % W] += tc * b * 0.35

    # Clouds: thin irregular banks, flattened towards the horizon; moonlit edges near the moon.
    base = tileable_noise(8, 5, 3)
    detail = tileable_noise(70, 3, 5)
    cover = np.clip((base * 0.65 + detail * 0.35 - 0.47) / 0.16, 0, 1) ** 1.3
    cover *= np.clip(alt / 8.0, 0, 1) * np.clip((80 - alt) / 30.0, 0.25, 1)          # none below the horizon
    cover *= np.clip((ang - MOON_DIAMETER * 0.6) / 6.0, 0.35, 1)                      # thinner over the moon
    cloud_col = np.array([0.030, 0.034, 0.045]) + np.array([0.10, 0.10, 0.11]) * np.exp(-ang / 12.0)[..., None]
    img = img * (1 - 0.85 * cover[..., None]) + cloud_col * (0.85 * cover[..., None])

    # Moon: disc with darker maria, soft limb, small halo.
    r = MOON_DIAMETER / 2
    disc = np.clip((r - ang) / (r * 0.12), 0, 1)
    maria = tileable_noise(140, 2, 9)
    face = np.array([0.86, 0.87, 0.82]) * (0.78 + 0.22 * maria)[..., None]
    limb = np.clip(1 - (ang / r) ** 2, 0, 1) ** 0.25
    img = img * (1 - disc[..., None]) + face * (limb * disc)[..., None]
    img += np.array([0.10, 0.11, 0.13]) * np.exp(-np.maximum(ang - r, 0) / 1.2)[..., None] * (1 - disc[..., None])

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    srgb = np.where(img <= 0.0031308, img * 12.92, 1.055 * np.power(np.clip(img, 0, None), 1 / 2.4) - 0.055)
    Image.fromarray((np.clip(srgb, 0, 1) * 255 + 0.5).astype(np.uint8)).save(OUT)
    print(OUT, "moon dir", np.round(m, 3))


if __name__ == "__main__":
    main()

# Procedural night sky for Unity Skybox/Panoramic, drawn with numpy + Pillow (no Blender).
# Every field is evaluated in unit-direction space; curved cloud shells provide perspective,
# spherical stars retain their angular size, and both poles and the longitude seam are continuous.
# python3 Blender/Ceu/ceu_noturno.py --preview --out Blender/Ceu/previews/Ceu_Noturno.png
# Default output: Assets/UnclaimedPrototype/Iluminacao/Ceu_Noturno.png (8192 x 4096, RGB sRGB).
# The moon remains opposite the forward direction of the generator's "Luz da lua" light.
import argparse
import os
import time
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "Assets", "UnclaimedPrototype", "Iluminacao", "Ceu_Noturno.png")
W, H = 8192, 4096             # native size; --width 4096 reproduces the previous, lower-detail panorama
MOON_EULER = (20.0, 10.0)      # light pitch (down) and yaw, degrees, as in Unity's Transform.eulerAngles
MOON_DIAMETER = 1.6            # degrees (the real moon is 0.5; games show it larger to read at 60 fov)
CLOUD_SCALE = 1.1              # km per noise unit of the low deck (lumps of a few hundred metres)
COVER_SCALE = 9.0              # km per unit of the slow coverage field (where the gaps are)
COVER_BASE = 0.37              # fbm threshold of the deck: lower = more overcast
COVER_SPREAD = 0.45            # how much the coverage field moves that threshold
SOFTNESS = 0.19                # edge softness of the lumps
WARP = 0.35                    # small domain warp: irregular lumps without marbled swirls
DETAIL_OCTAVES = 3             # finer cloud octaves added on top (zero-mean, same normalization: coverage unchanged)
DETAIL_GAIN = 3.0              # strength of those octaves relative to the fBm's own falloff


def moon_direction():
    """Unit vector from the ground towards the moon (opposite of the light's forward), Unity axes."""
    pitch, yaw = np.radians(MOON_EULER)
    fwd = np.array([np.sin(yaw) * np.cos(pitch), -np.sin(pitch), np.cos(yaw) * np.cos(pitch)])
    return -fwd


def directions(start=0, stop=H):
    """Panoramic directions: u = 0.5 - atan2(z,x)/2pi; row zero is the zenith."""
    lon = (0.5 - (np.arange(W, dtype=np.float32) + 0.5) / W) * (2 * np.pi)
    # Include the exact poles so clamp sampling cannot expose a ring of differing directions.
    colat = np.arange(start, stop, dtype=np.float64) * np.pi / (H - 1)
    sine = np.sin(colat).astype(np.float32)
    sine[(np.arange(start, stop) == 0) | (np.arange(start, stop) == H - 1)] = 0
    return np.stack(np.broadcast_arrays(sine[:, None] * np.cos(lon),
                    np.cos(colat).astype(np.float32)[:, None],
                    sine[:, None] * np.sin(lon)), axis=-1)


def smooth(a, b, x):
    """Cubic smoothstep with a clamped range."""
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def noise(p, seed):
    """Continuous 3D value noise using a deterministic integer lattice hash."""
    cell = np.floor(p).astype(np.int32)
    f = p - cell
    f = f * f * f * (f * (f * 6 - 15) + 10)
    result = np.zeros(p.shape[:-1], dtype=np.float32)
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                h = ((cell[..., 0] + dx).astype(np.uint32) * np.uint32(1597334677)
                     ^ (cell[..., 1] + dy).astype(np.uint32) * np.uint32(3812015801)
                     ^ (cell[..., 2] + dz).astype(np.uint32) * np.uint32(2798796415)
                     ^ np.uint32(seed))
                h ^= h >> 16
                h *= np.uint32(2246822519)
                h ^= h >> 13
                value = (h >> 8).astype(np.float32) / 16777215
                weight = ((f[..., 0] if dx else 1 - f[..., 0])
                          * (f[..., 1] if dy else 1 - f[..., 1])
                          * (f[..., 2] if dz else 1 - f[..., 2]))
                result += weight * value
    return result


def fbm(p, seed, octaves=4, footprint=None):
    """Normalized fractal noise in world space. With a footprint (noise units per pixel), DETAIL_OCTAVES
    finer octaves are added as zero-mean detail, each faded out where it would be smaller than ~2 pixels."""
    total = np.zeros(p.shape[:-1], dtype=np.float32)
    amplitude, norm = 1.0, 0.0
    extra = DETAIL_OCTAVES if footprint is not None else 0
    for i in range(octaves + extra):
        if i < octaves:
            total += amplitude * noise(p, seed + i * 101)
            norm += amplitude
        else:
            wavelength = 1.0 / 2.03 ** i
            total += DETAIL_GAIN * amplitude * (noise(p, seed + i * 101) - 0.5) * smooth(1.5, 3.0, wavelength / footprint)
        amplitude *= 0.48
        p = p * 2.03 + np.array([13.2, 7.1, -9.7], dtype=np.float32)
    return total / norm


def footprint(d, height, scale):
    """Noise units covered by one panorama texel on a cloud shell, along the stretched (radial) direction."""
    radius = 6371.0
    ry = radius * d[..., 1]
    distance = (2 * radius * height + height * height) / (
        np.sqrt(ry * ry + 2 * radius * height + height * height) + ry)
    texel = np.pi / (H - 1)
    return distance * texel / (scale * np.maximum(d[..., 1], 0.05))


def shell(d, height, scale):
    """Intersect rays from Earth's surface with a concentric cloud shell, in km."""
    radius = 6371.0
    ry = radius * d[..., 1]
    distance = (2 * radius * height + height * height) / (
        np.sqrt(ry * ry + 2 * radius * height + height * height) + ry)
    p = d * distance[..., None]
    return p / scale + np.array([23.4, 11.7, -38.2], dtype=np.float32)


def cloud_fields(d):
    """Low rain-cloud density, thin high veil, and subtle moon-facing relief."""
    m = moon_direction().astype(np.float32)
    angle = np.degrees(np.arccos(np.clip(d @ m, -1, 1)))
    # Broad coverage: a slow field decides where the deck is solid and where the few gaps open.
    broad = fbm(shell(d, 2.0, COVER_SCALE), 61, 3)
    p = shell(d, 2.0, CLOUD_SCALE)
    warp = np.stack([fbm(p * 0.9, s, 2) - 0.5 for s in (31, 47, 79)], axis=-1)
    q = p + warp * WARP
    foot = footprint(d, 2.0, CLOUD_SCALE)
    mass = fbm(q, 13, 6, foot)
    threshold = COVER_BASE + COVER_SPREAD * (broad - 0.5)
    density = smooth(threshold, threshold + SOFTNESS, mass)
    # Second, lower and finer broken layer: overlapping lumps give the deck depth.
    p2 = shell(d, 1.3, CLOUD_SCALE * 0.55)
    mass2 = fbm(p2 + warp * WARP * 0.5, 211, 5, footprint(d, 1.3, CLOUD_SCALE * 0.55))
    density = np.maximum(density, 0.8 * smooth(threshold + 0.06, threshold + 0.06 + SOFTNESS, mass2))
    # An irregular opening follows the 3D field rather than a circular cutout.
    opening_radius = 10.5 + 4.0 * (noise(d * 19, 233) - 0.5)
    opening = smooth(opening_radius * 0.50, opening_radius * 1.35, angle)
    density *= 0.035 + 0.965 * opening
    high = 0.36 + 0.28 * fbm(shell(d, 8.5, 14.0), 173, 3)
    # Project the moon direction onto the local shell tangent plane.
    normal = (p - np.array([23.4, 11.7, -38.2], dtype=np.float32)) * CLOUD_SCALE
    normal[..., 1] += 6371
    normal /= np.linalg.norm(normal, axis=-1)[..., None]
    tangent = m - normal * (normal @ m)[..., None]
    shifted = fbm(q + tangent * 0.18, 13, 6, foot)
    relief = np.clip((mass - shifted) * 4.0, -0.3, 0.3)
    return density, high, relief, angle


def render_block(d):
    """Compose linear radiance and cloud transmission for a block of directions."""
    alt = np.degrees(np.arcsin(np.clip(d[..., 1], -1, 1)))
    angle = np.degrees(np.arccos(np.clip(d @ moon_direction(), -1, 1)))
    t = np.clip(alt / 60, 0, 1)[..., None]
    sky = np.array([0.030, 0.038, 0.060]) * (1 - t) + np.array([0.010, 0.016, 0.040]) * t
    sky += np.array([0.020, 0.026, 0.040]) * np.exp(-angle / 25)[..., None]
    below = np.clip(-alt / 3, 0, 1)[..., None]
    sky = sky * (1 - below) + np.array([0.008, 0.009, 0.011]) * below
    sky *= (0.85 + 0.15 * np.clip(alt / 12, 0, 1))[..., None]
    transmission = np.zeros(alt.shape, dtype=np.float32)
    visible = alt > 0
    if np.any(visible):
        low, high, relief, ang = cloud_fields(d[visible])
        haze = smooth(0, 12, alt[visible])
        low_alpha = (1 - np.exp(-low * 7)) * haze
        high_alpha = (1 - np.exp(-high)) * haze
        near = np.exp(-ang / 17)
        # Thick cores are a little darker underneath, moon-facing flanks a little brighter: soft volume.
        thick = smooth(0.55, 1.0, low)
        cloud_color = ((np.array([0.029, 0.036, 0.056])
                        + relief[:, None] * np.array([0.030, 0.034, 0.042])
                        + near[:, None] * np.array([0.014, 0.017, 0.024]))
                       * (1 - 0.22 * thick)[:, None])
        # Cool diffuse city light reaches the lower cloud bases.
        cloud_color += (1 - smooth(10, 70, alt[visible]))[:, None] * np.array([0.002, 0.003, 0.004])
        patch = sky[visible]
        patch = patch * (1 - high_alpha[:, None]) + np.array([0.025, 0.032, 0.052]) * high_alpha[:, None]
        patch = patch * (1 - low_alpha[:, None]) + cloud_color * low_alpha[:, None]
        trans = np.exp(-low * 10 - high)
        transmission[visible] = trans * haze
        r = MOON_DIAMETER / 2
        disc = 1 - smooth(r * 0.88, r, ang)
        maria = fbm(d[visible] * 180, 907, 3)
        limb = np.clip(1 - (ang / r) ** 2, 0, 1) ** 0.25
        face = np.array([1.0, 1.0, 0.95]) * (0.69 + 0.31 * maria[:, None])
        # The disc shows through a thin streaky veil (finer than the high layer), so it reads as half-hidden.
        veil = 0.18 + 0.45 * smooth(0.35, 0.75, fbm(shell(d[visible], 8.5, 1.6), 401, 4))
        seen = np.exp(-low * 10 - veil)
        patch = patch * (1 - (disc * seen)[:, None]) + face * (limb * disc * seen)[:, None]
        # Discreet corona: a tight glow in the veil plus a wide faint aureole.
        out = np.maximum(ang - r, 0) * (1 - disc)
        halo = (0.05 * np.exp(-out / 1.4) + 0.012 * np.exp(-out / 7)) * (0.6 + 0.8 * veil)
        patch += (halo * np.exp(-low * 3))[:, None] * np.array([0.75, 0.85, 1.0])
        sky[visible] = patch
    return sky.astype(np.float32), transmission


def add_stars(img):
    """Rasterize spherical Gaussian stars in angular windows, including longitude wrap."""
    rng = np.random.default_rng(11)
    n = 4500
    y = rng.uniform(0.02, 1, n)
    lon = rng.uniform(-np.pi, np.pi, n)
    stars = np.stack([np.sqrt(1-y*y)*np.cos(lon), y, np.sqrt(1-y*y)*np.sin(lon)], axis=-1).astype(np.float32)
    low, high, _, angle = cloud_fields(stars)
    brightness = (0.06 + 0.6 * rng.power(4, n)**6) * np.clip(np.degrees(np.arcsin(y))/15, 0, 1)
    brightness *= np.clip((angle-4)/20, 0, 1) * np.exp(-low*10-high)
    widths = np.radians(rng.uniform(0.04, 0.08, n))
    tints = np.stack([rng.uniform(0.85, 1, n), rng.uniform(0.9, 1, n), np.ones(n)], axis=-1)
    for s, longitude, sigma, bright, tint in zip(stars, lon, widths, brightness, tints):
        theta = np.arccos(float(s[1]))
        radius = sigma * 3.5
        r0 = max(0, int(np.floor((theta-radius)/np.pi*(H-1))))
        r1 = min(H, int(np.ceil((theta+radius)/np.pi*(H-1)))+1)
        min_sine = min(np.sin(max(0, theta-radius)), np.sin(min(np.pi, theta+radius)))
        half = W//2 if min_sine < 1e-6 else min(W//2, int(np.ceil(radius/min_sine*W/(2*np.pi)))+1)
        center = int((0.5-longitude/(2*np.pi))*W-0.5)
        cols = np.arange(center-half, center+half+1) % W if half < W//2 else np.arange(W)
        colat = np.arange(r0, r1)*np.pi/(H-1)
        longitude_pixels = (0.5-(cols+0.5)/W)*2*np.pi
        dot = np.cos(colat)[:, None]*s[1] + np.sin(colat)[:, None]*(np.cos(longitude_pixels)*s[0]+np.sin(longitude_pixels)*s[2])
        distance = np.arccos(np.clip(dot, -1, 1))
        gaussian = np.exp(-0.5*(distance/sigma)**2)
        img[r0:r1, cols] += (gaussian*bright)[..., None]*tint


def to_linear(rgb):
    """Decode normalized sRGB."""
    return np.where(rgb <= 0.04045, rgb/12.92, ((rgb+0.055)/1.055)**2.4)


def encode(img, dither_lsb=0.0):
    """Encode linear radiance as RGB sRGB bytes; optional triangular dither (in 8-bit steps) against
    banding in the dark gradients (it makes the PNG ~10x larger)."""
    rng = np.random.default_rng(5)
    out = np.empty(img.shape, dtype=np.uint8)
    for start in range(0, img.shape[0], 256):
        block = img[start:start+256]
        rgb = np.where(block <= 0.0031308, block*12.92, 1.055*np.maximum(block, 0)**(1/2.4)-0.055)
        dither = (rng.random(block.shape, dtype=np.float32) - rng.random(block.shape, dtype=np.float32)) * dither_lsb
        out[start:start+256] = np.clip(np.clip(rgb, 0, 1)*255 + 0.5 + dither, 0, 255).astype(np.uint8)
    return out


def regions():
    """Solid-angle weights and the four reference altitude regions."""
    alt = 90 - np.arange(H)*180/(H-1)
    weights = np.maximum(0, np.cos(np.radians(alt)))
    return alt, weights, [alt > 0, alt > 60, (alt > 0) & (alt < 20), alt < 0]


def match_radiance(img):
    """Fit a smooth altitude-only exposure curve to the approved upper-sky means."""
    alt, weights, masks = regions()
    bases = np.stack([np.ones(H), 1-smooth(15, 45, alt), smooth(40, 70, alt)], axis=-1)
    means = img.mean(axis=1)
    target = np.array([[0.0244, 0.0311, 0.0524], [0.0139, 0.0201, 0.0434], [0.0296, 0.0367, 0.0569]])
    for channel in range(3):
        matrix = np.array([(bases[mask]* (means[mask, channel]*weights[mask])[:, None]).sum(axis=0)/weights[mask].sum() for mask in masks[:3]])
        coefficients = np.linalg.solve(matrix, target[:, channel])
        gain = bases @ coefficients
        img[alt > 0, :, channel] *= gain[alt > 0, None]


def report(rgb):
    """Print reference radiance means and pole/seam diagnostics from saved RGB bytes."""
    alt, weights, masks = regions()
    linear = to_linear(rgb.astype(np.float32)/255)
    means = linear.mean(axis=1, dtype=np.float64)
    for name, mask in zip(('upper hemisphere', 'altitude > 60', 'altitude 0-20', 'lower hemisphere'), masks):
        print(name, np.round(np.average(means[mask], axis=0, weights=weights[mask]), 7))
    print('Pole std RGB:', rgb[0].std(axis=0), rgb[-1].std(axis=0))
    print('Mean absolute seam / adjacent RGB:', np.abs(rgb[:, 0].astype(float)-rgb[:, -1]).mean(), np.abs(rgb[:, 0].astype(float)-rgb[:, 1]).mean())


def sample_panorama(rgb, d):
    """Bilinear lat-long sampling with horizontal wrap and vertical clamp."""
    height, width = rgb.shape[:2]
    x = ((0.5-np.arctan2(d[..., 2], d[..., 0])/(2*np.pi))*width-0.5) % width
    y = np.arccos(np.clip(d[..., 1], -1, 1))/np.pi*(height-1)
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = (x-x0)[..., None], (y-y0)[..., None]
    y1 = np.minimum(y0+1, height-1)
    return ((rgb[y0, x0]*(1-fx)+rgb[y0, (x0+1)%width]*fx)*(1-fy)
            +(rgb[y1, x0]*(1-fx)+rgb[y1, (x0+1)%width]*fx)*fy).astype(np.uint8)


def previews(rgb, folder, prefix):
    """Render a 120-degree zenith view and four 70-degree horizon perspectives."""
    os.makedirs(folder, exist_ok=True)
    views = [('zenite', None)] + list(zip(('N', 'L', 'S', 'O'), (0, 90, 180, 270)))
    for name, yaw in views:
        width, height = (700, 700) if yaw is None else (800, 500)
        if yaw is None:
            forward, right, up = np.array([0., 1., 0.]), np.array([1., 0., 0.]), np.array([0., 0., 1.])
            fov = 120
        else:
            a, pitch = np.radians(yaw), np.radians(15)
            forward = np.array([np.sin(a)*np.cos(pitch), np.sin(pitch), np.cos(a)*np.cos(pitch)])
            right = np.array([np.cos(a), 0., -np.sin(a)])
            up = np.cross(forward, right)
            fov = 70
        x = ((np.arange(width)+0.5)/width*2-1)*np.tan(np.radians(fov/2))
        y = (1-(np.arange(height)+0.5)/height*2)*np.tan(np.radians(fov/2))*height/width
        d = forward + x[None, :, None]*right + y[:, None, None]*up
        d /= np.linalg.norm(d, axis=-1)[..., None]
        Image.fromarray(sample_panorama(rgb, d)).save(os.path.join(folder, prefix+'_'+name+'.png'))


def main():
    """Generate the panorama, or preview an existing panorama without modifying it."""
    global W, H, DETAIL_OCTAVES
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', default=OUT, help='Output panorama PNG')
    parser.add_argument('--preview', action='store_true')
    parser.add_argument('--preview-dir', default=os.path.join(HERE, 'previews'))
    parser.add_argument('--preview-only', metavar='PNG')
    parser.add_argument('--width', type=int, default=W, help='Panorama width (height = width / 2)')
    parser.add_argument('--dither', type=float, default=0.0, help='Triangular dither amplitude in 8-bit steps')
    parser.add_argument('--detail', type=int, default=DETAIL_OCTAVES, help='Extra fine cloud octaves')
    args = parser.parse_args()
    W, H, DETAIL_OCTAVES = args.width, args.width // 2, args.detail
    started = time.perf_counter()
    if args.preview_only:
        with Image.open(args.preview_only) as source:
            rgb = np.asarray(source.convert('RGB'))
        previews(rgb, args.preview_dir, os.path.splitext(os.path.basename(args.preview_only))[0])
    else:
        img = np.empty((H, W, 3), dtype=np.float32)
        for start in range(0, H, 64):
            stop = min(start+64, H)
            img[start:stop], _ = render_block(directions(start, stop))
        add_stars(img)
        match_radiance(img)
        rgb = encode(img, args.dither)
        os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
        Image.fromarray(rgb).save(args.out)
        print(args.out, 'moon dir', np.round(moon_direction(), 3))
        report(rgb)
        if args.preview:
            previews(rgb, args.preview_dir, os.path.splitext(os.path.basename(args.out))[0])
    print('Elapsed: %.2f s' % (time.perf_counter()-started))


if __name__ == '__main__':
    main()

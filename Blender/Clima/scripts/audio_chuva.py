"""Synthesize a seamless light-rain loop (no thunder) for the morgue exterior.

Everything is generated here (filtered noise + small droplet ticks), so the clip carries no
third-party license. Filtering is done in the frequency domain over the whole buffer and droplets
wrap around, which makes the loop seamless.
Usage: python3 Blender/Clima/scripts/audio_chuva.py [output.wav]
"""
import sys, wave
import numpy as np

SR, SECONDS, SEED = 44100, 24, 7
OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/UnclaimedPrototype/Clima/Audio/Chuva_leve_loop.wav"
rng = np.random.default_rng(SEED)
n = SR * SECONDS
f = np.fft.rfftfreq(n, 1 / SR)

def shaped_noise(gain):
    spec = (rng.normal(size=f.size) + 1j * rng.normal(size=f.size)) * gain
    return np.fft.irfft(spec, n)

def band(lo, hi, slope=1.0):
    g = np.where(f > 0, 1 / np.maximum(f, 1) ** (0.5 * slope), 0)
    g *= 1 / (1 + (lo / np.maximum(f, 1)) ** 4)
    g *= 1 / (1 + (f / hi) ** 4)
    return g

def channel():
    # Soft hiss of distant rain on foliage and pavement, slowly breathing (no gusts).
    hiss = shaped_noise(band(400, 9000, 0.6))
    hiss /= np.abs(hiss).max()
    t = np.arange(n) / SR
    breathe = 1 + 0.08 * np.sin(2 * np.pi * t / SECONDS * 2 + rng.uniform(0, 6)) \
                + 0.05 * np.sin(2 * np.pi * t / SECONDS * 5 + rng.uniform(0, 6))
    hiss *= breathe
    # Very low, soft body (water on the ground), kept quiet.
    body = shaped_noise(band(60, 500, 1.2)); body /= np.abs(body).max()
    # Individual droplet ticks: short damped sines, wrapping around the loop end.
    ticks = np.zeros(n)
    count = int(SECONDS * 260)
    starts = rng.integers(0, n, count)
    for s in starts:
        freq = rng.uniform(1800, 7000)
        dur = int(SR * rng.uniform(0.004, 0.018))
        k = np.arange(dur)
        amp = rng.uniform(0.05, 1.0) ** 2.5
        tick = amp * np.sin(2 * np.pi * freq * k / SR) * np.exp(-k / (dur / 4))
        idx = (s + k) % n
        ticks[idx] += tick
    # A few closer, duller drops (gutter/leaves).
    for s in rng.integers(0, n, int(SECONDS * 6)):
        freq = rng.uniform(500, 1400)
        dur = int(SR * 0.04)
        k = np.arange(dur)
        tick = rng.uniform(0.2, 0.6) * np.sin(2 * np.pi * freq * k / SR) * np.exp(-k / (dur / 5))
        ticks[(s + k) % n] += tick
    ticks /= np.abs(ticks).max()
    return 0.55 * hiss + 0.12 * body + 0.33 * ticks

left, right = channel(), channel()
mix = np.stack([left, right], axis=1)
mix *= 0.5 / np.abs(mix).max()  # about -6 dBFS peak; the AudioSource sets the final level
pcm = (mix * 32767).astype("<i2")
with wave.open(OUT, "wb") as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes(pcm.tobytes())
print(OUT, SECONDS, "s")

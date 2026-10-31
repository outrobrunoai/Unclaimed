"""Corridor signage as vectors: SVG with live text (Nimbus Sans) -> PNG through rsvg-convert, plus specs.json.

    python3 -I Blender/Circulacao/scripts/sinais_vetoriais.py [Name ...]

Three kinds of sign, deliberately not one template (docs/direcao-arte/comunicacao-visual.md, section 2):
  ID  room identification: light plate, Nimbus Sans Narrow Bold, left aligned, no arrow.
  OR  orientation: dark desaturated green plate, one arrow module per direction group, destinations stacked.
  SEG safety: shape and colour of ABNT NBR 13434-2 as reproduced in CBMERJ NT 2-05:2020 (annexes), see
      docs/revisao-placas-equipamentos/referencias.md. Red square + white pictogram for equipment, yellow triangle for
      electrical risk, green rectangle for exits. No certification marks, no invented codes.
All sizes are millimetres of the real object; PNGs are PX_MM pixels per mm. Arrows are one parametric shape
(constant stroke, square ends, mitred tip), pictograms are paths, nothing is a font glyph or a raster.
"""
import html
import json
import os
import subprocess
import sys

from PIL import ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SVG_DIR = os.path.join(ROOT, "Blender", "Circulacao", "fontes_sinais")
PNG_DIR = os.path.join(ROOT, "Assets", "UnclaimedPrototype", "Circulacao", "Sinais")
FONT_DIR = "/usr/share/fonts/gsfonts/"
FONTS = {  # style -> (css family, weight, otf used for measuring)
    "reg": ("Nimbus Sans", 400, "NimbusSans-Regular.otf"),
    "bold": ("Nimbus Sans", 700, "NimbusSans-Bold.otf"),
    "nreg": ("Nimbus Sans Narrow", 400, "NimbusSansNarrow-Regular.otf"),
    "nbold": ("Nimbus Sans Narrow", 700, "NimbusSansNarrow-Bold.otf"),
}
CAP = 0.72            # cap height / em of Nimbus Sans (Helvetica metrics)
PX_MM = 3.2

PLATE, INK = "#e4e1d6", "#2b2d29"          # guide section 2
ORIENT, ORIENT_INK = "#4b6256", "#ebe8dc"  # guide: desaturated green for orientation
RED, YELLOW, BLACK, EXIT = "#df4420", "#f2c200", "#171815", "#0b5b34"
WHITE = "#f1eee4"                          # printed white, not pure


def measure(text, style, size):
    """Ink width in mm of a text set at `size` mm em."""
    f = ImageFont.truetype(FONT_DIR + FONTS[style][2], 1000)
    return f.getlength(text) * size / 1000.0


def txt(x, base, text, style, cap, fill, anchor="start"):
    """Text whose capital height is `cap` mm, baseline at `base`."""
    fam, weight, _ = FONTS[style]
    size = cap / CAP
    return (f'<text x="{x:.2f}" y="{base:.2f}" font-family="{fam}" font-weight="{weight}" font-size="{size:.2f}" '
            f'fill="{fill}" text-anchor="{anchor}">{html.escape(text)}</text>')


def fit_cap(text, style, max_w, cap_max):
    """Largest capital height <= cap_max for which `text` fits max_w mm."""
    return min(cap_max, cap_max * max_w / measure(text, style, cap_max / CAP))


def arrow(cx, cy, size, direction, fill):
    """The one arrow: stem + mitred chevron, constant stroke (0.17 of the module), square ends."""
    deg = {"right": 0, "down": 90, "left": 180, "up": 270}[direction]
    return (f'<g transform="translate({cx:.2f} {cy:.2f}) rotate({deg}) scale({size:.3f})" fill="none" stroke="{fill}" '
            f'stroke-width="0.17" stroke-linejoin="miter" stroke-miterlimit="4"><path d="M-0.46 0H0.46"/>'
            f'<path d="M0.04 -0.42L0.46 0L0.04 0.42"/></g>')


def doc(w, h, body, background):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}mm" height="{h}mm" viewBox="0 0 {w} {h}">'
            f'<rect width="{w}" height="{h}" fill="{background}"/>{body}</svg>')


# ------------------------------------------------------------------ pictograms (unit boxes, drawn at x, y, size)

def extinguisher(x, y, s, fill):
    """White cylinder, neck with valve and cap, hose handle: the NT 2-05 / NBR 13434-2 symbol."""
    return (f'<g transform="translate({x:.2f} {y:.2f}) scale({s / 100:.4f})" fill="{fill}">'
            '<path d="M34 93V44C34 34 39 29 44 26V17H62V26C67 29 68 34 68 44V93Z"/>'
            '<rect x="46" y="8" width="18" height="8" rx="2"/>'
            f'<path d="M47 20C37 20 29 25 27 36" fill="none" stroke="{fill}" stroke-width="6.5" stroke-linecap="round"/></g>')


def call_symbol(x, y, s, red, paper):
    """Manual call point symbol: red square, white field, red disc, white dot (NT 2-05 code 21)."""
    return (f'<g transform="translate({x:.2f} {y:.2f}) scale({s / 100:.4f})"><rect width="100" height="100" fill="{red}"/>'
            f'<rect x="9" y="9" width="82" height="82" fill="{paper}"/><circle cx="50" cy="50" r="33" fill="{red}"/>'
            f'<circle cx="50" cy="50" r="9" fill="{paper}"/></g>')


def bolt_triangle(x, y, s):
    """Yellow warning triangle, black band, lightning bolt (NT 2-05 code 9). s = side."""
    h = s * 0.866
    k = s / 100.0
    return (f'<g transform="translate({x:.2f} {y:.2f}) scale({k:.4f})" stroke-linejoin="round">'
            f'<path d="M50 5L96 85H4Z" fill="{YELLOW}" stroke="{BLACK}" stroke-width="9"/>'
            f'<path d="M46 31H60L53 50H64L45 78L47 58H37Z" fill="{BLACK}"/></g>'), h


def person_barred(x, y, s):
    """Prohibition circle (red ring, slash) over a black person: restricted access."""
    return (f'<g transform="translate({x:.2f} {y:.2f}) scale({s / 100:.4f})"><circle cx="50" cy="50" r="44" fill="{WHITE}"/>'
            f'<circle cx="50" cy="26" r="7.5" fill="{BLACK}"/><path d="M38 38H62L66 66H58L57 84H43L42 66H34Z" fill="{BLACK}"/>'
            f'<circle cx="50" cy="50" r="42" fill="none" stroke="{RED}" stroke-width="9"/>'
            f'<path d="M20 80L80 20" stroke="{RED}" stroke-width="9" stroke-linecap="butt"/></g>')


def runner(x, y, s, fill, flip=False):
    """Running figure in an open doorway (ISO 7010 / NT 2-05 code 17). Box of s x s, runner heads right (or left if flip)."""
    sx = -1 if flip else 1
    tx = x + (0.70 * s + 0.01 * s if flip else -0.01 * s)
    limb = f'fill="none" stroke="{fill}" stroke-linejoin="round" stroke-linecap="round"'
    return (f'<g transform="translate({tx:.2f} {y:.2f}) scale({sx * s / 100:.4f} {s / 100:.4f})">'
            f'<path d="M7 93V6H70V93" fill="none" stroke="{fill}" stroke-width="5.5" stroke-linejoin="miter"/>'
            f'<rect x="1" y="86" width="26" height="5.5" fill="{fill}"/>'
            f'<circle cx="47" cy="20" r="7.2" fill="{fill}"/>'
            f'<path d="M41 31H52L42 60H30Z" fill="{fill}"/>'
            f'<path d="M49 37L59 46L65 36" {limb} stroke-width="6.5"/>'
            f'<path d="M41 37L32 46L25 56" {limb} stroke-width="6.5"/>'
            f'<path d="M37 59L51 68L49 90" {limb} stroke-width="9"/>'
            f'<path d="M34 59L25 75L12 85" {limb} stroke-width="9"/></g>')


# ------------------------------------------------------------------ signs

def room_plate(title, second=None, restricted=False):
    cap, m = 42.0, 34.0
    h = 170.0 if not second else 215.0
    right = 0.0
    if restricted:
        right = 96.0 + 24.0
    w = max(measure(title, "nbold", cap / CAP), measure(second or "", "nreg", 21 / CAP)) + 2 * m + right
    w = round(w / 10 + 0.49) * 10.0
    body = ""
    if second:
        body += txt(m, h / 2 - 8, title, "nbold", cap, INK)
        body += txt(m, h / 2 + 8 + 21, second, "nreg", 21, INK)
    else:
        body += txt(m, h / 2 + cap / 2, title, "nbold", cap, INK)
    if restricted:
        body += person_barred(w - m - 96, (h - 96) / 2, 96)
    return w, h, body, PLATE


def directory():
    m, cap, step = 58.0, 40.0, 76.0
    rows = ["Arquivo · Administração", "Descanso · Depósito", "Autópsia · Câmara fria", "Recebimento de corpos"]
    text_w = max(measure(t, "bold", cap / CAP) for t in rows)
    col = 190.0
    w = round((2 * m + col + text_w) / 10 + 0.49) * 10.0
    top = m + 36 + 46
    h = round((top + step * (len(rows) - 1) + cap + m) / 10 + 0.49) * 10.0
    body = txt(m, m + 36, "Circulação interna", "bold", 30, ORIENT_INK)
    # One arrow for the whole group: every destination is reached by going east along this corridor.
    mid = top + (step * (len(rows) - 1) + cap) / 2
    body += arrow(m + 66, mid, 112, "right", ORIENT_INK)
    for i, row in enumerate(rows):
        body += txt(m + col, top + i * step + cap, row, "bold", cap, ORIENT_INK)
    return w, h, body, ORIENT


def direction_plate(lines, direction):
    cap, m = 36.0, 34.0
    step = 78.0
    w = max(measure(t, "bold", cap / CAP) for t in lines) + 2 * m + 100 + 28
    w = round(w / 10 + 0.49) * 10.0
    h = 2 * m + 36 + step * (len(lines) - 1) + 12
    body = arrow(m + 50, h / 2, 82, direction, ORIENT_INK)
    first = h / 2 - step * (len(lines) - 1) / 2 + cap / 2
    for i, t in enumerate(lines):
        body += txt(m + 128, first + i * step, t, "bold", cap, ORIENT_INK)
    return w, h, body, ORIENT


def extinguisher_sign():
    s = 200.0
    return s, s, extinguisher(s * 0.10, s * 0.06, s * 0.80, WHITE), RED


def alarm_sign():
    w = 170.0
    h = w + 98.0
    body = call_symbol(0, 0, w, RED, WHITE)
    for i, t in enumerate(("ALARME", "DE INCÊNDIO")):
        body += txt(w / 2, w + 18 + 21 + i * 32, t, "nbold", 21, RED, "middle")
    return w, h, body, WHITE


def electric_sign():
    w, h = 220.0, 340.0
    tri, th = bolt_triangle(15, 16, 190)
    body = tri
    body += txt(w / 2, 246, "RISCO DE CHOQUE", "nbold", fit_cap("RISCO DE CHOQUE", "nbold", w - 36, 21), INK, "middle")
    body += txt(w / 2, 276, "ELÉTRICO", "nbold", 21, INK, "middle")
    body += txt(w / 2, 311, "Acesso restrito", "nreg", 15, INK, "middle")
    return w, h, body, WHITE


def exit_sign(direction):
    h, m, cap, pic = 140.0, 16.0, 42.0, 100.0
    word = measure("SAÍDA", "bold", cap / CAP)
    gap = 20.0
    w = m + word + gap + 0.70 * pic + (gap + 50.0 if direction else 0) + m
    w = round(w / 10 + 0.49) * 10.0
    body = txt(m, h / 2 + cap / 2, "SAÍDA", "bold", cap, WHITE)
    px = m + word + gap
    body += runner(px, (h - pic) / 2, pic, WHITE, flip=direction == "left")
    if direction:
        body += arrow(px + 0.70 * pic + gap + 25, h / 2, 50, direction, WHITE)
    return w, h, body, EXIT


# ------------------------------------------------------------------ equipment artwork (not plates: no specs entry)

FLAME = "M52 6C56 20 74 32 72 54C70 72 60 84 48 84C36 84 28 72 30 58C31 50 35 44 40 38C41 46 44 50 47 50C50 40 48 22 52 6Z"   # two-tongued flame in a 100 box


def flame(x, y, s, fill):
    return f'<path transform="translate({x:.2f} {y:.2f}) scale({s / 100:.4f})" d="{FLAME}" fill="{fill}"/>'


def class_icon(x, y, s, kind):
    """Fire-class square, white pictogram on its class colour (A green, B red, C blue)."""
    col = {"A": "#2f7d4a", "B": "#c93a22", "C": "#2a5aa6"}[kind]
    g = f'<g transform="translate({x:.2f} {y:.2f}) scale({s / 100:.4f})"><rect width="100" height="100" fill="{col}"/>'
    if kind == "A":     # logs and a flame
        g += (f'<rect x="14" y="70" width="72" height="12" rx="6" fill="{WHITE}"/><rect x="24" y="55" width="52" height="12" rx="6" fill="{WHITE}"/>'
              + flame(31, 4, 52, WHITE))
    elif kind == "B":   # flame over a spilled liquid
        g += (flame(24, 2, 48, WHITE) + f'<path d="M14 70L52 60L62 78L22 90Z" fill="{WHITE}"/><path d="M62 72C72 72 84 72 88 78C82 84 68 84 62 72Z" fill="{WHITE}"/>')
    else:               # plug and cable
        g += (f'<path d="M36 18V40M62 18V40" stroke="{WHITE}" stroke-width="8" stroke-linecap="round"/>'
              f'<path d="M26 40H72V54C72 64 64 70 49 70C34 70 26 64 26 54Z" fill="{WHITE}"/>'
              f'<path d="M49 70V86" stroke="{WHITE}" stroke-width="9" stroke-linecap="round"/>')
    return g + '</g>'


def extinguisher_label():
    """Wrap-around label, 134 x 130 mm (about 107 degrees of a 144 mm cylinder): title, classes, how to use.
    No logos, approval seals or registration numbers."""
    w, h, m = 134.0, 130.0, 9.0
    b = f'<rect width="{w}" height="{h}" fill="#ecebe3"/>'
    b += f'<rect x="0" y="0" width="{w}" height="23" fill="{INK}"/>'
    b += txt(m, 15.6, "EXTINTOR PORTÁTIL", "nbold", 8.6, "#ecebe3")
    b += txt(m, 34.5, "Pó químico ABC · 4 kg", "nbold", 7.0, INK)
    for i, k in enumerate("ABC"):
        x = m + i * 39.5
        b += class_icon(x, 41.5, 27, k)
        b += txt(x + 13.5, 77.5, k, "bold", 5.8, INK, "middle")
    b += txt(m, 91, "MODO DE USAR", "nbold", 6.0, INK)
    steps = ["Retire o pino de segurança", "Aponte para a base do fogo", "Aperte o gatilho e varra"]
    for i, t in enumerate(steps):
        y = 101.5 + i * 9.6
        b += f'<circle cx="{m + 3.8}" cy="{y - 2.4}" r="3.8" fill="{INK}"/>'
        b += txt(m + 3.8, y - 0.5, str(i + 1), "bold", 4.0, "#ecebe3", "middle")
        b += txt(m + 12, y, t, "nreg", 4.9, INK)
    return w, h, b, "#ecebe3"


def gauge_dial():
    """Pressure gauge face, 36 mm: green working band, red zones, needle in the green."""
    d = 36.0
    c = d / 2
    import math
    def arc(a0, a1, r0, r1, col):
        a0, a1 = math.radians(a0), math.radians(a1)
        p = lambda a, r: (c + r * math.cos(a), c - r * math.sin(a))
        x0, y0 = p(a0, r1); x1, y1 = p(a1, r1); x2, y2 = p(a1, r0); x3, y3 = p(a0, r0)
        return f'<path d="M{x0:.2f} {y0:.2f}A{r1} {r1} 0 0 1 {x1:.2f} {y1:.2f}L{x2:.2f} {y2:.2f}A{r0} {r0} 0 0 0 {x3:.2f} {y3:.2f}Z" fill="{col}"/>'
    b = f'<circle cx="{c}" cy="{c}" r="{c}" fill="#e9e7df"/>'
    b += arc(225, 160, 11, 16, "#b8281e") + arc(160, 20, 11, 16, "#2f7d4a") + arc(20, -45, 11, 16, "#b8281e")
    b += f'<path d="M{c} {c}L{c + 12 * math.cos(math.radians(95)):.2f} {c - 12 * math.sin(math.radians(95)):.2f}" stroke="{INK}" stroke-width="1.3" stroke-linecap="round"/>'
    b += f'<circle cx="{c}" cy="{c}" r="1.8" fill="{INK}"/>'
    return d, d, b, "#e9e7df"


def callpoint_front():
    """Front of a break-glass manual call point, 84 x 84 mm: pictogram, window with the instruction, break mark."""
    w = h = 84.0
    red = "#b72a1f"
    b = f'<rect width="{w}" height="{h}" fill="{red}"/>'
    # house with flame (white line pictogram, as on the real devices)
    b += (f'<g transform="translate(29 5) scale(0.26)" fill="none" stroke="{WHITE}" stroke-width="6" stroke-linejoin="round" stroke-linecap="round">'
          '<path d="M6 44L50 6L94 44"/><path d="M18 36V94H82V36"/></g>')
    b += flame(33.0, 13.5, 18.0, WHITE)
    # window: instruction + the line-and-target mark of the glass
    wx, wy, ww, wh = 9.0, 31.0, 66.0, 36.0
    b += f'<rect x="{wx}" y="{wy}" width="{ww}" height="{wh}" fill="#dedcd4"/>'
    b += txt(w / 2, wy + 9.5, "EM CASO DE INCÊNDIO", "nbold", 4.6, INK, "middle")
    b += txt(w / 2, wy + 16.5, "QUEBRE O VIDRO", "nbold", 4.6, INK, "middle")
    b += (f'<g stroke="{INK}" stroke-width="0.8" fill="none"><path d="M{wx + 8} {wy + 27}H{wx + ww - 8}"/>'
          f'<circle cx="{w / 2}" cy="{wy + 27}" r="3.6"/><path d="M{w / 2} {wy + 22.4}V{wy + 31.6}"/></g>')
    return w, h, b, red


def build_all():
    specs = {}
    sheets = {
        "ID_Arquivo": ("room", room_plate("Arquivo e administração")),
        "ID_Descanso": ("room", room_plate("Descanso e vestiário")),
        "ID_Deposito": ("room", room_plate("Depósito")),
        "ID_Autopsia": ("room", room_plate("Autópsia", "Acesso restrito a pessoal autorizado", True)),
        "ID_SalaTecnica": ("room", room_plate("Sala técnica", "Pessoal autorizado")),
        "ID_Recebimento": ("room", room_plate("Recebimento de corpos")),
        "ID_CamaraFria": ("room", room_plate("Câmara fria")),
        "OR_Diretorio": ("orient", directory()),
        "OR_Recebimento": ("orient", direction_plate(["Recebimento de corpos", "Câmara fria"], "up")),
        "SEG_Extintor": ("safety", extinguisher_sign()),
        "SEG_Alarme": ("safety", alarm_sign()),
        "SEG_Risco": ("safety", electric_sign()),
        "SEG_Saida_Frente": ("exit", exit_sign(None)),
        "SEG_Saida_Direita": ("exit", exit_sign("right")),
    }
    for name, (kind, (w, h, body, bg)) in sheets.items():
        specs[name] = {"w": w, "h": h, "kind": kind}
        yield name, w, h, doc(w, h, body, bg)
    json.dump(specs, open(os.path.join(SVG_DIR, "specs.json"), "w"), indent=1)
    for name, fn in (("EQ_ExtintorRotulo", extinguisher_label), ("EQ_Manometro", gauge_dial), ("EQ_AcionadorFrente", callpoint_front)):
        w, h, body, bg = fn()
        yield name, w, h, doc(w, h, body, bg)


def main():
    only = set(sys.argv[1:])
    os.makedirs(SVG_DIR, exist_ok=True)
    os.makedirs(PNG_DIR, exist_ok=True)
    for name, w, h, svg in build_all():
        if only and name not in only:
            continue
        svg_path = os.path.join(SVG_DIR, name + ".svg")
        open(svg_path, "w", encoding="utf-8").write(svg)
        px = round(w * (12.0 if name == "EQ_Manometro" else 8.0 if name.startswith("EQ_") else PX_MM))
        subprocess.run(["rsvg-convert", "-w", str(px), "-o", os.path.join(PNG_DIR, name + ".png"), svg_path], check=True)
        print(name, f"{w:.0f}x{h:.0f} mm -> {px} px", flush=True)


if __name__ == "__main__":
    main()

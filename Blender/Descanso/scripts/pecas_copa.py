"""Coffee-counter objects: pump thermos, two glass jars (coffee, sugar), a stack of disposable cups, two mugs and a spoon.
Fewer objects than before, each built with wall thickness, open rims, handles and lids, arranged by use on the counter top:
preparation to the left, cups at hand in the middle, the last-used mug with its spoon on the right, free counter between them.
Origin: centre of the counter top. Loaded by gerar.py (R, F injected)."""
import math
from mathutils import Matrix


def group(fn, x, y, yaw=0.0, **kw):
    """Builds one object at the origin and moves all of its parts to (x, y) rotated by yaw (degrees)."""
    coll = R._coll(R._current['asset'])
    mark = set(coll.objects)
    fn(**kw)
    m = Matrix.Translation((x, y, 0.0)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z')
    for o in set(coll.objects) - mark:
        o.data.transform(m)


def circle(r, z, n=28, cx=0.0, cy=0.0):
    return [(cx + r * math.cos(2 * math.pi * i / n), cy + r * math.sin(2 * math.pi * i / n), z) for i in range(n + 1)]


def termica(tag='Garrafa termica'):
    # body: wall-thick shell, base ring, rounded shoulder to a short neck
    R.lathe(tag, 'termica', [(0.0, 0.012), (0.056, 0.012), (0.0665, 0.040), (0.0675, 0.185), (0.060, 0.222), (0.046, 0.240), (0.044, 0.248), (0.0, 0.248)], seg=32, r=0.0035)
    R.lathe(tag + ' base', 'preto', [(0.0, 0.0), (0.060, 0.0), (0.063, 0.006), (0.0655, 0.034), (0.0, 0.034)], seg=32, r=0.002)
    # pump head: collar, domed cap, press button, the spout swings out of its side
    R.lathe(tag + ' bomba', 'preto', [(0.0, 0.245), (0.050, 0.245), (0.052, 0.256), (0.048, 0.270), (0.034, 0.278), (0.0, 0.278)], seg=28, r=0.003)
    R.lathe(tag + ' botao', 'inox', [(0.0, 0.278), (0.022, 0.278), (0.021, 0.286), (0.0, 0.288)], seg=20, r=0.002)
    R.tube(tag + ' bico', 'preto', [(0.040, 0.0, 0.262), (0.066, 0.0, 0.264), (0.086, 0.0, 0.254), (0.098, 0.0, 0.238)], 0.0085, fillet=0.014)
    R.lathe(tag + ' ponta do bico', 'inox', [(0.0, 0.0), (0.0068, 0.0), (0.0068, 0.004), (0.0, 0.004)], seg=12, r=0.0008, matrix=Matrix.Translation((0.098, 0.0, 0.234)))
    # side handle on the back (+Y... turned by the caller): attached high on the shoulder and low on the body
    R.tube(tag + ' alca', 'preto', [(-0.062, 0.0, 0.205), (-0.104, 0.0, 0.196), (-0.114, 0.0, 0.130), (-0.100, 0.0, 0.074), (-0.066, 0.0, 0.066)], 0.0100, fillet=0.03)
    # level window strip
    R.box(tag + ' visor', 'preto', 0.0580, -0.008, 0.070, 0.0692, 0.008, 0.185, r=0.002, seg=1)


def pote(kind='cafe'):
    lid = 'azul' if kind == 'cafe' else 'branco'
    fill = 'cafe' if kind == 'cafe' else 'acucar'
    h = 0.126
    # glass: 3 mm wall, thick heavy base, open at the top so the content is seen from above
    R.lathe(f'Pote {kind} vidro', 'vidro', [(0.0, 0.0), (0.045, 0.0), (0.050, 0.006), (0.0505, 0.112), (0.047, 0.122), (0.0465, 0.1235),
                                           (0.0445, 0.1235), (0.0445, 0.012), (0.0, 0.012)], seg=32, r=0.0006)
    R.lathe(f'Pote {kind} conteudo', fill, [(0.0, 0.012), (0.0452, 0.012), (0.0452, 0.082), (0.030, 0.086), (0.0, 0.088)], seg=28, r=0.001)
    R.lathe(f'Pote {kind} etiqueta', 'etiqueta', [(0.0509, 0.034), (0.0518, 0.034), (0.0518, 0.086), (0.0509, 0.086)], seg=32, r=0.0003)
    # screw lid: rim, ridged side, domed top
    R.lathe(f'Pote {kind} tampa', lid, [(0.0, 0.140), (0.044, 0.140), (0.0545, 0.1385), (0.0555, 0.122), (0.0545, 0.1195), (0.0468, 0.1195), (0.0468, 0.126), (0.0, 0.126)], seg=32, r=0.0012)


def copos():
    n, hc, step = 9, 0.075, 0.0066
    top = hc + (n - 1) * step
    def rad(z):
        return 0.0255 + (0.0348 - 0.0255) * z / hc
    R.lathe('Copos empilhados', 'copo', [(0.0, 0.0), (rad(0) , 0.0), (rad(top) + 0.0011, top), (rad(top) - 0.0008, top + 0.0007), (rad(top) - 0.0016, top),
                                          (rad(top - hc) + 0.0001, top - hc + 0.002), (0.0, top - hc + 0.002)], seg=32, r=0.0004)
    for k in range(1, n):
        z = hc + (k - 1) * step
        R.cyl(f'Borda do copo {k}', 'copo', (0.0, 0.0, z), rad(z) + 0.0012, 0.0011, seg=32, r=0.0003)


def caneca(mat='caneca', cafe=False):
    h, r = 0.092, 0.0395
    R.lathe('Caneca ' + mat, mat, [(0.0, 0.0), (0.033, 0.0), (r - 0.001, 0.005), (r, 0.010), (r + 0.0012, 0.086), (r - 0.001, 0.0925), (r - 0.0045, 0.0925), (r - 0.0052, 0.087),
                                    (r - 0.0050, 0.012), (0.0, 0.0085)], seg=36, r=0.0006)
    R.tube('Alca da ' + mat, mat, [(r - 0.001, 0.0, 0.078), (r + 0.026, 0.0, 0.076), (r + 0.034, 0.0, 0.050), (r + 0.024, 0.0, 0.024), (r - 0.001, 0.0, 0.019)], 0.0060, fillet=0.014)
    if cafe:
        R.lathe('Cafe da caneca', 'cafe', [(0.0, 0.062), (r - 0.0053, 0.062), (r - 0.0053, 0.0635), (0.0, 0.0635)], seg=30, r=0.0002)


def colher():
    pts = [(0.000, -0.0062), (0.012, -0.0064), (0.020, -0.0040), (0.022, 0.0), (0.020, 0.0040), (0.012, 0.0064), (0.000, 0.0062), (-0.006, 0.0042), (-0.060, 0.0026), (-0.100, 0.0036),
           (-0.108, 0.0), (-0.100, -0.0036), (-0.060, -0.0026), (-0.006, -0.0042)]
    R.extrude('Colher', 'inox', pts, 'XY', 0.0, 0.0012, r=0.0003, seg=1)


def itens_copa():
    group(termica, -0.64, -0.12, 200.0)
    group(pote, -0.40, -0.06, 0.0, kind='cafe')
    group(pote, -0.27, -0.13, 25.0, kind='acucar')
    group(copos, -0.04, -0.10)
    group(caneca, 0.34, -0.15, 160.0, mat='caneca', cafe=True)
    group(colher, 0.46, -0.20, 21.0)
    group(caneca, 0.66, -0.04, 35.0, mat='caneca2')

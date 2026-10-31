"""Rest-room bench: four hardwood slats (grain along the length) screwed to three steel crossbars, two welded end frames
with square-tube legs and rubber glides, a lengthwise stretcher tying the frames. Origin: floor centre. Loaded by gerar.py (R, F injected)."""
import math

L, D, H = 0.80, 0.17, 0.45          # half length, half depth, height (BENCH)
T = 0.030                            # slat thickness
SW, GAP = 0.070, 0.0130              # slat width and gap
XF = 0.66                            # end-frame crossbar x
LEG = 0.014                          # half section of the legs


def weld_loop(name, cx, cy, z, hx, hy):
    """A weld bead round a square tube where it meets the crossbar."""
    pts = [(cx - hx, cy - hy, z), (cx + hx, cy - hy, z), (cx + hx, cy + hy, z), (cx - hx, cy + hy, z), (cx - hx, cy - hy, z)]
    R.tube(name, 'tubo', pts, 0.0032, fillet=0.004, res=2)


def banco():
    seat0 = H - T
    cb0 = seat0 - 0.032                                  # crossbar underside
    # slats: top edges eased, a hair different in length like cut stock
    y0 = -(4 * SW + 3 * GAP) / 2
    for i in range(4):
        ya = y0 + i * (SW + GAP)
        dl = (0.0012, -0.0016, 0.0009, -0.0007)[i]
        R.box(f'Ripa {i}', 'madeira', -L + dl, ya, seat0, L - dl, ya + SW, H, r=0.0055, seg=3)
    # crossbars (flat tube 40 x 32) under the slats
    for k, x in enumerate((-XF, 0.0, XF)):
        R.box(f'Travessa {k}', 'tubo', x - 0.020, -D + 0.020, cb0, x + 0.020, D - 0.020, seat0, r=0.0035, seg=2)
    # end frames: legs, lower rail, glides, weld beads
    for sx in (-1, 1):
        x = sx * XF
        for sy in (-1, 1):
            y = sy * (D - 0.040)
            R.box(f'Perna {sx:+d} {sy:+d}', 'tubo', x - LEG, y - LEG, 0.010, x + LEG, y + LEG, cb0, r=0.003, seg=2)
            R.box(f'Tampa da perna {sx:+d} {sy:+d}', 'aco', x - LEG - 0.0015, y - LEG - 0.0015, 0.0, x + LEG + 0.0015, y + LEG + 0.0015, 0.010, r=0.003, seg=2)
            R.cyl(f'Deslizador {sx:+d} {sy:+d}', 'borracha', (x, y, 0.0035), 0.0155, 0.007, seg=14, r=0.0012)
            weld_loop(f'Solda {sx:+d} {sy:+d}', x, y, cb0 - 0.0006, LEG + 0.0028, LEG + 0.0028)
            weld_loop(f'Solda baixa {sx:+d} {sy:+d}', x, y, 0.182, LEG + 0.0028, LEG + 0.0028)
        R.box(f'Travessa baixa {sx:+d}', 'tubo', x - 0.011, -D + 0.040 - LEG, 0.170, x + 0.011, D - 0.040 + LEG, 0.192, r=0.002, seg=1)
    # lengthwise stretcher under the middle crossbar, welded to the end crossbars
    R.box('Longarina', 'tubo', -XF - 0.020, -0.014, cb0 - 0.030, XF + 0.020, 0.014, cb0 + 0.0004, r=0.003, seg=2)
    for sx in (-1, 1):
        weld_loop(f'Solda longarina {sx:+d}', sx * XF, 0.0, cb0 - 0.0305, 0.0225, 0.0165)
    # screws: one per slat on every crossbar, countersunk heads with a slot
    for x in (-XF, 0.0, XF):
        for i in range(4):
            ya = y0 + i * (SW + GAP) + SW / 2
            R.cyl(f'Parafuso {x:+.2f} {i}', 'aco', (x, ya, H + 0.0002), 0.0052, 0.0016, seg=10, r=0.0004)
            R.box(f'Fenda {x:+.2f} {i}', 'tubo', x - 0.0036, ya - 0.0005, H + 0.0006, x + 0.0036, ya + 0.0005, H + 0.0012, r=0.0001, seg=1)

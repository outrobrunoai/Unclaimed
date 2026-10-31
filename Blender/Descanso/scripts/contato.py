import sys, glob
from PIL import Image, ImageDraw
pre = sys.argv[1]; files = sorted(glob.glob(pre + '_*.jpg')); n = len(files)
cols = 3 if n > 4 else 2; rows = (n + cols - 1) // cols; W = 640; H = 480
sh = Image.new('RGB', (cols * W, rows * H), 'white')
for i, f in enumerate(files):
    im = Image.open(f).convert('RGB'); im.thumbnail((W, H)); x, y = (i % cols) * W, (i // cols) * H
    sh.paste(im, (x, y)); ImageDraw.Draw(sh).text((x + 6, y + 4), f, fill='red')
sh.save(f'/tmp/claude-1000/-mnt-dados-Projetos-Unity-Unclaimed/d1ee5909-773b-4d20-8086-75eeec0f05a3/scratchpad/c_{pre}.png')

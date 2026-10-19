"""Labelled texture contact sheet: montage.py <out.png> <chapter>:<tex> ..."""
import os, sys
from PIL import Image, ImageDraw

EX = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "extracted")
T = 160
items = sys.argv[2:]
cols = 6
rows = (len(items) + cols - 1) // cols
sheet = Image.new("RGB", (cols * T, rows * (T + 14)), (255, 0, 255))
d = ImageDraw.Draw(sheet)
for i, it in enumerate(items):
    ch, tex = it.split(":", 1)
    folder = os.path.join(EX, ch, "texture")
    m = {f.lower(): f for f in os.listdir(folder)}
    f = m.get(os.path.splitext(tex)[0].lower() + ".png")
    x, y = (i % cols) * T, (i // cols) * (T + 14)
    if f:
        im = Image.open(os.path.join(folder, f)).convert("RGB")
        sheet.paste(im.resize((T, T), Image.NEAREST), (x, y + 14))
        label = "%s %s %dx%d" % (ch, tex, im.width, im.height)
    else:
        label = "%s %s MISSING" % (ch, tex)
    d.rectangle([x, y, x + T, y + 13], fill=(0, 0, 0))
    d.text((x + 2, y + 1), label, fill=(255, 255, 255))
sheet.save(sys.argv[1])

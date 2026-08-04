"""Print the top-tier needle.tif's per-row opaque-alpha extents — the pointer silhouette
that texture.zbd's RGB copy lacks. Run ExtractAssets.ps1 -Unzip first."""
from PIL import Image

PATH = r"..\..\extracted\C1\rtexture15\needle.png"

im = Image.open(PATH)
a = im.getchannel("A")
w, h = im.size
print(f"{PATH}  {w}x{h} {im.mode} — per-row opaque half-width (alpha>=128), row 0 = tip:")
for y in range(h):
    xs = [x for x in range(w) if a.getpixel((x, y)) >= 128]
    if xs:
        print(f"  row {y:3d}: x {min(xs):2d}..{max(xs):2d}  width {max(xs) - min(xs) + 1:2d}")
    else:
        print(f"  row {y:3d}: fully transparent")

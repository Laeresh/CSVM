"""C4 sky-sheet luminance means across every tier: where the tiers disagree with texture.zbd
they agree with each other, so texture.zbd is the outlier the shipped game never draws.
Run ExtractAssets.ps1 -Unzip first."""
from PIL import Image
import os

ROOT = r"..\..\extracted\C4"

for f in ["c4sky2.png", "sky1.png"]:
    for tier in ["texture", "rtexture2", "rtexture4", "rtexture6", "rtexture8", "rtexture14"]:
        p = os.path.join(ROOT, tier, f)
        if not os.path.exists(p):
            print(f"{f} {tier}: missing")
            continue
        im = Image.open(p)
        g = im.convert("L")
        d = list(g.getdata())
        print(f"{f} {tier:12s} {im.size} {im.mode} mean {sum(d) / len(d):.1f}")

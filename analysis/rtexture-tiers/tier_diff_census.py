"""Per-chapter content diff between texture.zbd and its top rtextureN tier: same names and
resolutions, different pixels. Run ExtractAssets.ps1 -Unzip first."""
from PIL import Image
import os

ROOT = r"..\..\extracted"
TOPS = {"C1": 15, "C1B": 11, "C1C": 10, "C2": 14, "C2B": 9, "C3": 12, "C4": 14, "C5": 14}

for ch, n in TOPS.items():
    base = os.path.join(ROOT, ch, "texture")
    rt = os.path.join(ROOT, ch, f"rtexture{n}")
    if not (os.path.isdir(base) and os.path.isdir(rt)):
        print(f"{ch}: missing dirs")
        continue
    same = diff = mode = 0
    for f in os.listdir(rt):
        if not f.endswith(".png"):
            continue
        bp = os.path.join(base, f)
        if not os.path.exists(bp):
            continue
        a = Image.open(bp)
        b = Image.open(os.path.join(rt, f))
        if a.mode != b.mode:
            mode += 1
        elif a.size != b.size or a.tobytes() != b.tobytes():
            diff += 1
        else:
            same += 1
    print(f"{ch} rtexture{n}: {same} identical, {diff} differ in pixels, {mode} differ in mode")

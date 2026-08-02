"""CBLOCK-LOD 1b, as a repeatable census: the authored `_1`/`_2` mip levels every chapter ships.

Answers, per chapter, read-only, from `extracted/**`:
  1. how many base textures ship an authored `_1` (half) / `_2` (quarter) sibling,
  2. whether each sibling really is half/quarter the base's dimensions (a name match is a lead,
     not a guarantee — the adoption code refuses a sibling whose size disagrees),
  3. whether any gamez material references an `_N` name in its own right,
  4. the luminance the artist kept versus the luminance a box filter keeps: mean, and the share of
     pixels above 128, per level.

(4) is the measurement the fix is judged on. It is the DATA side of it — the engine side (what our
loaded mip chain actually holds) is `--dump-mips`, which reports the same two columns off the
Image the texture archive installed.

Usage:  python mip_census.py [chapter ...]        (default: every chapter under extracted/)
"""
import json, os, sys
from PIL import Image
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import item9_lib

ROOT = item9_lib.ROOT if hasattr(item9_lib, "ROOT") else None
if ROOT is None:
    d = os.path.dirname(os.path.abspath(__file__))
    while not os.path.isdir(os.path.join(d, "extracted")):
        d = os.path.dirname(d)
    ROOT = d

CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]
# Rec.601 luma, the same weighting CBLOCK-LOD 1b used.
LUMA = np.array([0.299, 0.587, 0.114], dtype=np.float64)


def luminance(img):
    a = np.asarray(img.convert("RGB"), dtype=np.float64)
    return a @ LUMA


def box_half(img):
    """One box-filter halving — what `Image.GenerateMipmaps()` does to the base."""
    w, h = img.size
    return img.convert("RGB").resize((max(1, w // 2), max(1, h // 2)), Image.BOX)


def stats(lum):
    return lum.mean(), float((lum > 128).sum()) / lum.size * 100.0


def texture_dir(chapter):
    d = os.path.join(ROOT, "extracted", chapter, "texture")
    return d if os.path.isdir(d) else None


def material_names(chapter):
    """Every texture name a gamez material asks for, lowercased and extension-stripped.

    Materials carry a `texture_index` into the chapter's own `textures.json` name table, so both
    files are needed — a material alone names nothing.
    """
    mp = os.path.join(ROOT, "extracted", chapter, "gamez", "materials.json")
    tp = os.path.join(ROOT, "extracted", chapter, "gamez", "textures.json")
    if not (os.path.isfile(mp) and os.path.isfile(tp)):
        return set()
    with open(mp, "r", encoding="utf-8") as f:
        mats = json.load(f)
    with open(tp, "r", encoding="utf-8") as f:
        table = [t["name"] for t in json.load(f)]
    names = set()
    for m in mats:
        tex = m.get("Textured") if isinstance(m, dict) else None
        if not isinstance(tex, dict):
            continue
        i = tex.get("texture_index")
        if isinstance(i, int) and 0 <= i < len(table):
            names.add(os.path.splitext(table[i])[0].rstrip(".").lower())
    return names


def main(chapters):
    grand = []
    for ch in chapters:
        d = texture_dir(ch)
        if d is None:
            print(f"{ch}: no extracted/{ch}/texture — skipped")
            continue
        pngs = {os.path.splitext(f)[0]: os.path.join(d, f)
                for f in os.listdir(d) if f.lower().endswith(".png")}
        bases = sorted(n for n in pngs if not (n.endswith("_1") or n.endswith("_2")))
        mats = material_names(ch)

        with_1 = with_2 = 0
        size_ok = size_bad = 0
        bad = []
        referenced = sorted(n for n in pngs if (n.endswith("_1") or n.endswith("_2")) and n.lower() in mats)
        for b in bases:
            base_img = Image.open(pngs[b])
            bw, bh = base_img.size
            for level, suffix in ((1, "_1"), (2, "_2")):
                sib = pngs.get(b + suffix)
                if sib is None:
                    continue
                if level == 1:
                    with_1 += 1
                else:
                    with_2 += 1
                sw, sh = Image.open(sib).size
                want = (max(1, bw >> level), max(1, bh >> level))
                if (sw, sh) == want:
                    size_ok += 1
                else:
                    size_bad += 1
                    bad.append(f"{b}{suffix} {sw}x{sh} (base {bw}x{bh} wants {want[0]}x{want[1]})")

        print(f"\n=== {ch}: {len(bases)} base textures, {with_1} with _1, {with_2} with _2 "
              f"({size_ok} sized right, {size_bad} wrong)")
        if bad:
            print("    size mismatches: " + "; ".join(bad))
        print(f"    _N names referenced by a gamez material: "
              f"{len(referenced) if referenced else 0}{' ' + ', '.join(referenced) if referenced else ''}")

        # The luminance the fix is judged on, over every base that ships an authored level.
        rows = []
        for b in bases:
            for level, suffix in ((1, "_1"), (2, "_2")):
                sib = pngs.get(b + suffix)
                if sib is None:
                    continue
                base_img = Image.open(pngs[b])
                boxed = base_img
                for _ in range(level):
                    boxed = box_half(boxed)
                am, ap = stats(luminance(Image.open(sib)))
                bm, bp = stats(luminance(boxed))
                rows.append((b + suffix, am, ap, bm, bp))
        if rows:
            a_mean = sum(r[1] for r in rows) / len(rows)
            a_pct = sum(r[2] for r in rows) / len(rows)
            b_mean = sum(r[3] for r in rows) / len(rows)
            b_pct = sum(r[4] for r in rows) / len(rows)
            brighter = sum(1 for r in rows if r[2] > r[4])
            print(f"    {len(rows)} authored level(s): mean luma {a_mean:6.2f} vs box {b_mean:6.2f} | "
                  f"px>128 {a_pct:5.3f}% vs box {b_pct:5.3f}% | "
                  f"{brighter}/{len(rows)} keep more bright pixels than the box filter")
            for name in ("cblock1_1", "cblock1_2", "cblock2_1"):
                for r in rows:
                    if r[0] == name:
                        print(f"      {name:<14} authored mean {r[1]:6.2f} px>128 {r[2]:5.3f}%  |  "
                              f"box mean {r[3]:6.2f} px>128 {r[4]:5.3f}%")
            grand.append((ch, len(rows), a_pct, b_pct))
    if grand:
        print("\n--- all chapters ---")
        for ch, n, a, b in grand:
            print(f"{ch:<5} {n:4d} authored levels   px>128 authored {a:6.3f}%   box {b:6.3f}%")


if __name__ == "__main__":
    main(sys.argv[1:] or CHAPTERS)

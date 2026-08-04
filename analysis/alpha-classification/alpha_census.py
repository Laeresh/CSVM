"""Install-wide census of the blend-vs-scissor alpha classification.

The engine decides per texture whether alpha means BLEND (soft translucency, no depth
write) or SCISSOR (1-bit cutout at 0.5) — `TextureArchive.AlphaIsSoft`. This script
answers, read-only, from the same tier the engine loads (the chapter's top `rtextureN`
set, see `SessionPaths.ChapterTextures`):

  1. per texture: max alpha, opaque share (a>=200), partial share (32..199), ink share
     (a>=32), and scissor survival = the fraction of ink texels that pass the 0.5 cutoff,
  2. the verdict of the OLD rule (max<140, or opaque<30% of all texels and partial>35%)
     vs the LANDED rule (binary-ness: opaque/ink below a threshold — see FINDINGS.md for
     why survival alone is the wrong metric), swept over candidate thresholds,
  3. the flip lists — textures whose class changes — so the blast radius of a classifier
     change is an explicit, reviewable list before anything renders differently.

Usage:  python alpha_census.py [chapter ...]     (default: every chapter under extracted/)
Writes alpha_census.json next to itself; prints the summary + flip lists.
"""
import json
import os
import sys
import zipfile
from io import BytesIO

import numpy as np
from PIL import Image

d = os.path.dirname(os.path.abspath(__file__))
while not os.path.isdir(os.path.join(d, "extracted")):
    parent = os.path.dirname(d)
    if parent == d:
        sys.exit("no extracted/ directory above this script")
    d = parent
ROOT = d

CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]
THRESHOLDS = [0.40, 0.45, 0.50, 0.60]
CHOSEN = 0.45  # the landed threshold (TextureArchive.AlphaIsSoft); flip lists print here


def top_tier(chapter_dir):
    """The engine's texture source: the highest rtextureN, else texture (SessionPaths)."""
    best, best_n = "texture", -1
    for entry in os.listdir(chapter_dir):
        name = entry[:-4] if entry.endswith(".zip") else entry
        if name.startswith("rtexture"):
            try:
                n = int(name[len("rtexture"):])
            except ValueError:
                continue
            if n > best_n:
                best, best_n = name, n
    unzipped = os.path.join(chapter_dir, best)
    if os.path.isdir(unzipped):
        return unzipped
    z = unzipped + ".zip"
    return z if os.path.isfile(z) else None


def is_authored_mip(base):
    """`name_1`/`name_2` siblings are the base texture's authored mip levels, not textures."""
    return len(base) > 2 and base[-2] == "_" and base[-1] in "12"


def iter_pngs(tier):
    if os.path.isdir(tier):
        for f in sorted(os.listdir(tier)):
            if f.lower().endswith(".png"):
                with open(os.path.join(tier, f), "rb") as fh:
                    yield f[:-4], fh.read()
    else:
        with zipfile.ZipFile(tier) as z:
            for f in sorted(z.namelist()):
                if f.lower().endswith(".png"):
                    yield os.path.basename(f)[:-4], z.read(f)


def classify(alpha):
    """alpha: uint8 ndarray. Returns the stats dict + old/new verdicts."""
    total = alpha.size
    amax = int(alpha.max())
    opaque = int((alpha >= 200).sum())
    partial = int(((alpha >= 32) & (alpha <= 199)).sum())
    ink = int((alpha >= 32).sum())
    survive = int((alpha > 127).sum())
    old_soft = amax < 140 or (opaque < total * 0.30 and partial > total * 0.35)
    survival = survive / ink if ink else 1.0
    binary = opaque / ink if ink else None  # None = no ink: soft (draws nothing either way)
    return {
        "max": amax,
        "opaque_pct": round(100.0 * opaque / total, 1),
        "partial_pct": round(100.0 * partial / total, 1),
        "ink_pct": round(100.0 * ink / total, 1),
        "survival": round(survival, 3),
        "binary": round(binary, 3) if binary is not None else None,
        "old_soft": bool(old_soft),
        "new_soft": {str(t): bool(binary is None or binary < t) for t in THRESHOLDS},
    }


def main():
    chapters = sys.argv[1:] or CHAPTERS
    by_name = {}  # name -> {"chapters": [...], "stats": ..., "varies": bool}
    for ch in chapters:
        chapter_dir = os.path.join(ROOT, "extracted", ch)
        if not os.path.isdir(chapter_dir):
            print(f"{ch}: no extracted data, skipped")
            continue
        tier = top_tier(chapter_dir)
        if tier is None:
            print(f"{ch}: no texture tier found, skipped")
            continue
        n_alpha = 0
        for name, data in iter_pngs(tier):
            if is_authored_mip(name):
                continue
            img = Image.open(BytesIO(data))
            if img.mode not in ("RGBA", "LA", "PA"):
                continue
            alpha = np.asarray(img.convert("RGBA"))[:, :, 3]
            if int(alpha.min()) == 255:
                continue  # alpha channel present but fully opaque = no alpha (DetectAlpha)
            n_alpha += 1
            stats = classify(alpha)
            if name in by_name:
                prev = by_name[name]
                prev["chapters"].append(ch)
                if prev["stats"] != stats:
                    prev["varies"] = True
            else:
                by_name[name] = {"chapters": [ch], "stats": stats, "varies": False}
        print(f"{ch}: tier={os.path.basename(tier)} alpha_textures={n_alpha}")

    names = sorted(by_name)
    total = len(names)
    old_soft = [n for n in names if by_name[n]["stats"]["old_soft"]]
    print(f"\nunique alpha textures: {total}; OLD rule soft: {len(old_soft)}")
    for t in THRESHOLDS:
        new_soft = [n for n in names if by_name[n]["stats"]["new_soft"][str(t)]]
        print(f"NEW rule soft @ {t:.2f}: {len(new_soft)}")

    key = str(CHOSEN)
    def line(n):
        s = by_name[n]["stats"]
        var = " VARIES-BY-CHAPTER" if by_name[n]["varies"] else ""
        binary = "none" if s["binary"] is None else f"{s['binary']:.3f}"
        return (f"  {n:24s} max={s['max']:3d} opaque={s['opaque_pct']:5.1f}% "
                f"partial={s['partial_pct']:5.1f}% ink={s['ink_pct']:5.1f}% "
                f"binary={binary} survival={s['survival']:.3f} "
                f"[{','.join(by_name[n]['chapters'])}]{var}")

    hard_to_soft = [n for n in names
                    if not by_name[n]["stats"]["old_soft"] and by_name[n]["stats"]["new_soft"][key]]
    soft_to_hard = [n for n in names
                    if by_name[n]["stats"]["old_soft"] and not by_name[n]["stats"]["new_soft"][key]]
    print(f"\nFLIPS at {CHOSEN}: scissor->blend {len(hard_to_soft)}, blend->scissor {len(soft_to_hard)}")
    print("\nscissor -> blend (was cut, becomes translucent):")
    for n in hard_to_soft:
        print(line(n))
    print("\nblend -> scissor (was translucent, becomes cut):")
    for n in soft_to_hard:
        print(line(n))

    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "alpha_census.json")
    with open(out, "w") as f:
        json.dump({n: by_name[n] for n in names}, f, indent=1)
    print(f"\nwrote {out}")


if __name__ == "__main__":
    main()

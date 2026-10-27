"""Generates dougfirtree1, the look-alike of the tall open-grown fir card C1's forest is stamped
with: a stout trunk bare to about a quarter of its height, a few odd low branches, then flat drooping
branch tiers with the trunk showing between them, dark with yellow-green highlights on their tops.

C1B's card is C1's (same texture and size), so both take the same model. C4's is the same texture at
10 x 17 m against 10 x 22 m, so C4 takes its own CARDS entry, built to that card. Builder: veg_fir.py.
"""

import os
import sys

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import veg_fir  # noqa: E402

veg_fir.main({
    "model": "dougfirtree1.flt",
    # The card quad per chapter, width and height in metres: extracted/<C>/gamez, the model under
    # dougfirtree1.flt. --render checks the extracted card against this and stops on a mismatch.
    "cards": {"C1": (10.026, 22.473), "C1B": (10.026, 22.473), "C4": (10.026, 17.154)},
    "seed": 3,
    # Crown radius over card width at a height fraction, off the C1 card's alpha silhouette: the bare
    # trunk to 0.26, odd low branches to 0.38, a narrow waist, the widest tier at 0.5, then tapering
    # tiers. The card's grass tuft at the base is left to the ground.
    "profile": [(0.0, 0.05), (0.26, 0.04), (0.29, 0.14), (0.36, 0.19), (0.42, 0.13), (0.46, 0.30),
                (0.52, 0.31), (0.58, 0.25), (0.65, 0.25), (0.7, 0.19), (0.76, 0.155), (0.82, 0.12),
                (0.88, 0.095), (0.93, 0.05), (0.975, 0.0)],
    "apex": 0.975,
    # Needle colours (sRGB), set as firtree1's from this card's crown texels: the card's body
    # (16, 23, 12) and top tenth (63, 76, 48) are within a few levels of firtree1's card.
    "colours": ((0.016, 0.046, 0.01), (0.116, 0.166, 0.098), (0.369, 0.448, 0.289)),
    # Whorls: the clumps gather at these height fractions, a tenth of the share left between them.
    "tiers": {"at": (0.31, 0.36, 0.49, 0.58, 0.66, 0.74, 0.82, 0.9), "width": 0.015, "gap": 0.1},
    # The card's trunk is green with moss, not brown: it takes the needle patch.
    "trunk": {"r_base": 0.55, "r_top": 0.15, "top": 0.9, "moss": True},
    # A thin core up the tiered crown only, so the gaps between tiers show trunk and shade, not sky.
    "core": {"fs": (0.45, 0.6, 0.75, 0.87), "r": 0.2, "taper": 0.05},
    # Fewer, longer and flatter clumps than firtree1's, so a tier reads as a broad plate with gaps.
    "clumps": {"span": (0.28, 0.94), "floor": 0.08, "top": 0.92, "half_width": (0.5, 0.65), "reach": (0.9, 1.15), "droop": 0.75},
    # Strong shade across a clump, its top lit with yellow-green growth, as the card's tiers are.
    "needle": {"across": 0.8, "high": (0.04, 0.5)},
    "count": 90,
    # The clump tiles, as firtree1's: trees per tile, and per chapter the linear factor per channel that
    # puts the tiles' mean on the card's (sRGB 20.8, 28.7, 16.0 over solid texels on every chapter).
    "clump_trees": (3, 2),
    "clump_grade": {"C1": (0.262, 0.305, 0.162), "C1B": (0.262, 0.305, 0.162), "C4": (0.251, 0.291, 0.157)},
})

"""Generates bush1, the look-alike of the dense olive shrub card C1's river banks, grassland and
hedges are stamped with: a vase-shaped broadleaf bush, narrow at its stems and widening to a broad,
flat top, many small clusters of fine leaves with little contrast.

C1B's card is C1's (same texture and size), so both take the same model. Builder: veg_bush.py.
"""

import os
import sys

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import veg_bush  # noqa: E402

veg_bush.main({
    "model": "bush1.flt",
    # The card quad per chapter, width and height in metres: extracted/<C>/gamez, the model under
    # bush1.flt. --render checks the extracted card against this and stops on a mismatch.
    "cards": {"C1": (5.576, 5.058), "C1B": (5.576, 5.058)},
    "seed": 21,
    # Radius over card width at a height fraction, off the C1 card's alpha silhouette: narrow at the
    # stems, widest from 0.45 to 0.7, held wide to a flat top (the card's right side reaches further;
    # stamps turn, so the profile is the mean of both sides, pushed toward the wider).
    "profile": [(0.0, 0.06), (0.08, 0.15), (0.16, 0.20), (0.24, 0.25), (0.32, 0.30), (0.40, 0.36),
                (0.46, 0.44), (0.52, 0.46), (0.66, 0.47), (0.74, 0.46), (0.82, 0.44), (0.90, 0.42),
                (0.95, 0.34), (1.0, 0.15)],
    "centre": 0.55,
    # Leaf colours (sRGB): shade, body and highlight, off the card's darkest tenth (14, 22, 6),
    # body (36, 48, 22) and lightest tenth (57, 71, 43).
    "colours": ((0.06, 0.09, 0.03), (0.17, 0.22, 0.09), (0.34, 0.40, 0.21)),
    "leaves": {"count": 900, "size": (5, 9), "top": 0.3, "spread": 0.2, "high": 0.04},
    "cell_light": (0.08, 0.0, -0.12),
    "bark": ((0.10, 0.08, 0.05), (0.25, 0.20, 0.13)),
    "stems": {"count": 5, "r": 0.06, "top": (0.3, 0.5)},
    "core": {"fs": (0.08, 0.3, 0.55, 0.72), "r": 0.55, "apex": 0.82},
    # Many small clusters; those above `cap` spread over the whole top facing up, so it reads flat.
    "clusters": {"count": 150, "span": (0.05, 0.99), "size": (0.06, 0.09), "depth": (0.75, 1.0), "cap": 0.8},
})

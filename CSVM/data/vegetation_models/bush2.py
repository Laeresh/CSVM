"""Generates bush2, the look-alike of the small open shrub card C1's river banks, grassland and
hedges are stamped with: blue-green leaf clusters with holes between them on a stem, the sky showing
through, over a dark low tuft at its foot.

C1B's card is C1's (same texture and size), so both take the same model. Builder: veg_bush.py.
"""

import os
import sys

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import veg_bush  # noqa: E402

veg_bush.main({
    "model": "bush2.flt",
    # The card quad per chapter, width and height in metres: extracted/<C>/gamez, the model under
    # bush2.flt. --render checks the extracted card against this and stops on a mismatch.
    "cards": {"C1": (3.693, 3.350), "C1B": (3.693, 3.350)},
    "seed": 22,
    # Radius over card width at a height fraction, off the C1 card's alpha silhouette: a low tuft,
    # the bare stem near 0.25, then a crown widest at 0.64.
    "profile": [(0.0, 0.06), (0.2, 0.11), (0.26, 0.06), (0.32, 0.24), (0.40, 0.33), (0.48, 0.36),
                (0.56, 0.42), (0.64, 0.48), (0.72, 0.44), (0.80, 0.37), (0.88, 0.27), (0.96, 0.19),
                (1.0, 0.0)],
    "centre": 0.6,
    # Leaf colours (sRGB): shade, body and highlight, off the card's darkest tenth (18, 45, 39),
    # body (43, 71, 57) and lightest tenth (77, 104, 88).
    "colours": ((0.06, 0.16, 0.13), (0.18, 0.29, 0.22), (0.36, 0.46, 0.38)),
    # Leaves in rosettes with holes between them, as the card's open leafage.
    "leaves": {"count": 500, "size": (6, 11), "top": 0.3, "spread": 0.2, "high": 0.08, "rosettes": 7, "rosette_r": 0.16},
    "cell_light": (0.15, 0.0, -0.25),
    "bark": ((0.06, 0.08, 0.05), (0.18, 0.17, 0.11)),
    "stems": {"count": 4, "r": 0.04, "top": (0.3, 0.5)},
    "clusters": {"count": 40, "span": (0.05, 0.97), "size": (0.11, 0.16), "depth": (0.75, 1.0)},
})

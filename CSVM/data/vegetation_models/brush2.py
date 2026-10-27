"""Generates brush2, the look-alike of the small round shrub card C2's scrub is stamped with most: a
dense ball of fine olive leaves flecked red-brown, on a few short stems.

Builder: veg_bush.py.
"""

import os
import sys

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import veg_bush  # noqa: E402

veg_bush.main({
    "model": "brush2.flt",
    # The card quad per chapter, width and height in metres: extracted/<C>/gamez, the model under
    # brush2.flt. --render checks the extracted card against this and stops on a mismatch.
    "cards": {"C2": (2.358, 2.081)},
    "seed": 32,
    # Radius over card width at a height fraction, off the card's alpha silhouette (the mean of both
    # sides): short stems, then a round crown widest from 0.3 to 0.7.
    "profile": [(0.0, 0.08), (0.1, 0.12), (0.14, 0.2), (0.2, 0.35), (0.3, 0.43), (0.4, 0.45), (0.5, 0.44),
                (0.6, 0.45), (0.7, 0.45), (0.8, 0.40), (0.9, 0.30), (0.96, 0.17), (1.0, 0.0)],
    "centre": 0.55,
    # Leaf colours (sRGB): shade, body and highlight, the card's crown texels at each level at 1.5 times
    # its linear brightness (D33, Colour in game); the card's darkest level is its red-brown flecks.
    "colours": ((0.318, 0.303, 0.189), (0.368, 0.369, 0.237), (0.469, 0.52, 0.339)),
    # Leaves sparse enough to leave holes, and no core: in game the card shows the sand through its
    # leafage, which a dense ball would hide.
    "leaves": {"count": 1000, "size": (3, 6), "top": 0.3, "spread": 0.25, "high": 0.06},
    "cell_light": (0.1, 0.0, -0.15),
    "bark": ((0.10, 0.08, 0.06), (0.24, 0.20, 0.15)),
    "stems": {"count": 3, "r": 0.04, "top": (0.2, 0.4)},
    # Few clusters: C2 stamps this card more than any other, and every one inside the band draws.
    "clusters": {"count": 45, "span": (0.12, 0.99), "size": (0.10, 0.14), "depth": (0.75, 1.0)},
})

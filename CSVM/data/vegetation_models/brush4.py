"""Generates brush4, the look-alike of the large low shrub card C2's scrub is stamped with: a wide dome
of fine olive leaves to the ground, with little contrast.

Builder: veg_bush.py.
"""

import os
import sys

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import veg_bush  # noqa: E402

veg_bush.main({
    "model": "brush4.flt",
    # The card quad per chapter, width and height in metres: extracted/<C>/gamez, the model under
    # brush4.flt. --render checks the extracted card against this and stops on a mismatch.
    "cards": {"C2": (5.666, 5.002)},
    "seed": 34,
    # Radius over card width at a height fraction, off the card's alpha silhouette (the mean of both
    # sides): leaves to the ground, widest at 0.6, a narrower hump above 0.75.
    "profile": [(0.0, 0.22), (0.05, 0.29), (0.1, 0.34), (0.2, 0.37), (0.3, 0.37), (0.4, 0.39), (0.5, 0.43),
                (0.6, 0.44), (0.7, 0.40), (0.78, 0.27), (0.86, 0.24), (0.92, 0.12), (1.0, 0.0)],
    "centre": 0.45,
    # Leaf colours (sRGB): shade, body and highlight, the card's crown texels at each level at 1.5 times
    # its linear brightness (D33, Colour in game).
    "colours": ((0.197, 0.222, 0.097), (0.237, 0.257, 0.127), (0.301, 0.323, 0.188)),
    # Leaves sparse enough to leave holes, and no core: in game the card shows the sand through its
    # leafage, which a dense dome would hide.
    "leaves": {"count": 1000, "size": (3, 6), "top": 0.25, "spread": 0.15, "high": 0.03},
    "cell_light": (0.08, 0.0, -0.12),
    "bark": ((0.10, 0.08, 0.05), (0.25, 0.20, 0.13)),
    "stems": {"count": 4, "r": 0.06, "top": (0.2, 0.4)},
    "clusters": {"count": 75, "span": (0.03, 0.98), "size": (0.07, 0.10), "depth": (0.75, 1.0)},
})

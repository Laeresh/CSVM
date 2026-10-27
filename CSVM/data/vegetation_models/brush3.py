"""Generates brush3, the look-alike of the large grass tussock card C2's scrub is stamped with: a dense,
dark mound of long blades, widest at the ground where the outer blades fall, narrowing to a peak.

Builder: veg_tussock.py.
"""

import os
import sys

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import veg_tussock  # noqa: E402

veg_tussock.main({
    "model": "brush3.flt",
    # The card quad per chapter, width and height in metres: extracted/<C>/gamez, the model under
    # brush3.flt. --render checks the extracted card against this and stops on a mismatch.
    "cards": {"C2": (5.666, 5.002)},
    "seed": 33,
    # Radius over card width at a height fraction, off the card's alpha silhouette (the mean of both
    # sides): widest at the ground, where the outer blades fall, narrowing to a peak.
    "profile": [(0.0, 0.47), (0.2, 0.44), (0.4, 0.40), (0.55, 0.40), (0.65, 0.29), (0.8, 0.21),
                (0.9, 0.14), (0.97, 0.06), (1.0, 0.0)],
    "centre": 0.4,
    # Blade colours (sRGB): shade, body and highlight, the card's crown texels at each level at 1.5 times
    # its linear brightness (D33, Colour in game).
    "colours": ((0.14, 0.178, 0.077), (0.2, 0.245, 0.142), (0.314, 0.359, 0.244)),
    # The fan: blade count per cell (lit, plain, shaded; the lit cell is the outer tufts' and sparse), how
    # far past the outline a tip may run, the share whose tips reach the outline or past it, the base's
    # half spread, how far a blade's curve rises toward the top before falling to its tip, its width at
    # the foot (px), the gain toward its tip, the light spread and the odds of a pale blade.
    "blades": {"count": (110, 260, 220), "over": 1.15, "edge": 0.6, "base": 0.7, "arch": (0.0, 0.08), "width": (1.2, 2.4),
               "along": 0.35, "spread": 0.2, "high": 0.05},
    "cell_light": (0.12, 0.0, -0.18),
    "centre_fans": 3,
    "tufts": {"count": 16, "r": (0.15, 0.65), "scale": (0.5, 0.75)},
    # A small dark core at the foot, inside the blades.
    "core": {"fs": (0.0, 0.2, 0.35), "r": 0.45, "apex": 0.45},
})

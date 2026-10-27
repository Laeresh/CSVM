"""Generates brush1, the look-alike of the agave card C2's desert scrub is stamped with: a rosette of
stiff, broad, pointed blue-green leaves fanning up and out from the ground, their edges pale.

Builder: veg_rosette.py.
"""

import os
import sys

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import veg_rosette  # noqa: E402

veg_rosette.main({
    "model": "brush1.flt",
    # The card quad per chapter, width and height in metres: extracted/<C>/gamez, the model under
    # brush1.flt. --render checks the extracted card against this and stops on a mismatch.
    "cards": {"C2": (2.374, 2.095)},
    "seed": 31,
    # Leaf tips' radius over card width at a height fraction, off the card's alpha silhouette (the mean
    # of both sides): the outer leaves' tips widest at half the height, the inner ones upright to the top.
    "profile": [(0.0, 0.2), (0.3, 0.40), (0.5, 0.45), (0.7, 0.41), (0.85, 0.36), (0.92, 0.27), (1.0, 0.12)],
    "centre": 0.4,
    # Leaf colours (sRGB): shade, body and highlight, the card's crown texels at each level at 1.5 times
    # its linear brightness (D33, Colour in game).
    "colours": ((0.159, 0.262, 0.163), (0.224, 0.33, 0.211), (0.462, 0.533, 0.362)),
    # The leaf texture: its taper to the point, the pale margin's share of its half width, the darkening
    # down its fold, the gain toward its tip, lengthwise streaks and the spread between the three cells.
    "leaf": {"taper": 0.9, "margin": 0.12, "fold": 0.25, "along": 0.3, "streak": 0.06, "spread": 0.12},
    "cell_light": (0.12, 0.0, -0.15),
    # Stiff leaves: the peak is about the tip, so a leaf rises and leans out without arching over.
    "leaves": {"count": 24, "segments": 3, "span": (0.2, 0.98), "base_r": 0.25, "reach": (1.0, 1.15),
               "arch": (0.0, 0.08), "ctl_r": (0.3, 0.45), "width": (0.2, 0.26), "taper": 0.15, "twist": 0.3,
               "fold": 0.3},
})

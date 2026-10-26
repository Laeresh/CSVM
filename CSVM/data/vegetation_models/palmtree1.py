"""Generates palmtree1, the look-alike of the date palm card C2 and C3 stamp: a thick trunk with
diamond leaf-base scars, a dense dome of dark fronds with yellow-green highlights.

C2's and C3's cards are one species at one size (C3's texture is C2's at half the resolution plus
a base patch), so both chapters take the same model. Builder: veg_palm.py.
"""

import os
import sys

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import veg_palm  # noqa: E402

veg_palm.main({
    "model": "palmtree1.flt",
    # The card quad per chapter, width and height in metres: extracted/<C>/gamez, the model under
    # palmtree1.flt. --render checks the extracted card against this and stops on a mismatch.
    "cards": {"C2": (7.589, 13.189), "C3": (7.589, 13.189)},
    "seed": 11,
    "crown_z": 8.4,
    "core_r": 0.7,
    "trunk": {"r_base": 0.42, "r_top": 0.32, "swell": 0.3, "sides": 8, "segs": 6, "repeats": 3},
    # Colours are sRGB: shade, body and highlight, read off the card's crown and trunk texels.
    "fronds": {"count": 42, "length": (4.8, 3.6), "width": 0.17, "fold": 1.0, "elev": (88, -40), "bend": (0.2, 2.4)},
    "frond": {"cols": ((0.01, 0.04, 0.02), (0.12, 0.15, 0.07), (0.90, 0.90, 0.42)), "rachis": (0.40, 0.40, 0.24),
              "light": 0.2, "spread": 0.2, "high": 0.04, "gaps": 0.2, "angle": (35, 50), "step": 2.5, "thick": 2},
    # Every other frond is a lit one, its leaflets mostly in the highlight, as the card shows whole fronds catching the sun.
    "frond_b": {"cols": ((0.01, 0.04, 0.02), (0.12, 0.15, 0.07), (0.90, 0.90, 0.42)), "rachis": (0.40, 0.40, 0.24),
                "light": 0.6, "spread": 0.2, "high": 0.3, "gaps": 0.2, "angle": (35, 50), "step": 2.5, "thick": 2},
    "bark": {"pattern": "diamond", "dark": (0.14, 0.11, 0.09), "light": (0.48, 0.38, 0.24)},
    "core": ((0.10, 0.10, 0.07), (0.30, 0.27, 0.18)),
    "nut": ((0.30, 0.22, 0.10), (0.45, 0.35, 0.15)),
})

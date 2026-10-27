"""Generates palmtree3, the look-alike of the weeping palm card C2 and C3 stamp: a slim red-brown
trunk, a dense dome of grey-green fronds that weep low, a skirt of dead red-brown fronds under it.

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
    "model": "palmtree3.flt",
    # The card quad per chapter, width and height in metres: extracted/<C>/gamez, the model under
    # palmtree3.flt. --render checks the extracted card against this and stops on a mismatch.
    "cards": {"C2": (5.744, 9.983), "C3": (5.744, 9.983)},
    "seed": 13,
    "crown_z": 6.2,
    "core_r": 0.45,
    "trunk": {"r_base": 0.24, "r_top": 0.19, "swell": 0.2, "sides": 6, "segs": 6, "repeats": 3},
    # Colours are sRGB: shade, body and highlight. Trunk and bark off the card's trunk texels; fronds set
    # against the card's crown in game, greener and darker than its texels' mean (D33, Colour in game).
    "fronds": {"count": 44, "length": (3.6, 3.4), "width": 0.16, "fold": 1.0, "elev": (88, -50), "elev_pow": 1.5, "bend": (0.2, 2.4)},
    "frond": {"cols": ((0.08, 0.13, 0.06), (0.17, 0.27, 0.12), (0.38, 0.52, 0.28)), "rachis": (0.277, 0.326, 0.215),
              "light": 0.45, "spread": 0.15, "high": 0.16, "gaps": 0.08, "angle": (30, 45), "step": 2.5, "thick": 2},
    "skirt": {"count": 20, "dead_from": 0.65, "length": 2.2, "width": 0.12, "elev": (-88, -75),
              "frond": {"cols": ((0.18, 0.06, 0.04), (0.36, 0.13, 0.09), (0.50, 0.25, 0.15)), "rachis": (0.30, 0.18, 0.13),
                        "light": 0.45, "spread": 0.2, "high": 0.05, "gaps": 0.3, "angle": (20, 35), "step": 3, "thick": 2}},
    "bark": {"pattern": "rings", "ring_px": 5, "dark": (0.16, 0.09, 0.09), "light": (0.30, 0.20, 0.18)},
    "core": ((0.14, 0.09, 0.07), (0.28, 0.20, 0.15)),
    "nut": ((0.30, 0.15, 0.10), (0.40, 0.22, 0.12)),
})

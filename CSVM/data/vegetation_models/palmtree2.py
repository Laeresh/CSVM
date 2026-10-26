"""Generates palmtree2, the look-alike of the coconut palm card C2 and C3 stamp: a slender ringed
trunk leaning to its crown, an open crown of long arching olive fronds over a cluster of nuts.

C2's and C3's cards are one species at one size (C3's texture is C2's at half the resolution), so
both chapters take the same model. Builder: veg_palm.py.
"""

import os
import sys

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import veg_palm  # noqa: E402

veg_palm.main({
    "model": "palmtree2.flt",
    # The card quad per chapter, width and height in metres: extracted/<C>/gamez, the model under
    # palmtree2.flt. --render checks the extracted card against this and stops on a mismatch.
    "cards": {"C2": (5.744, 9.983), "C3": (5.744, 9.983)},
    "seed": 12,
    "crown_z": 6.5,
    "core_r": 0.35,
    # The card's trunk stands 0.5 m left of its centre and its crown on it: the lean puts the crown there.
    "trunk": {"r_base": 0.15, "r_top": 0.12, "swell": 0.5, "sides": 6, "segs": 8, "repeats": 4, "lean": 0.45},
    # Colours are sRGB: shade, body and highlight, read off the card's crown and trunk texels.
    "fronds": {"count": 18, "length": (4.0, 3.4), "width": 0.2, "fold": 0.9, "elev": (88, 10), "bend": (0.4, 2.6)},
    "frond": {"cols": ((0.06, 0.11, 0.0), (0.25, 0.30, 0.02), (0.50, 0.50, 0.14)), "rachis": (0.40, 0.38, 0.18),
              "light": 0.5, "spread": 0.15, "high": 0.15, "gaps": 0.06, "angle": (30, 45), "step": 2.5, "thick": 2},
    "bark": {"pattern": "rings", "ring_px": 6, "dark": (0.17, 0.17, 0.08), "light": (0.36, 0.34, 0.20)},
    "core": ((0.08, 0.09, 0.03), (0.16, 0.16, 0.06)),
    "nut": ((0.22, 0.24, 0.06), (0.38, 0.34, 0.12)),
    "nuts": {"count": 6, "r": 0.14},
})

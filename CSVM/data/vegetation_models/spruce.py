"""Generates spruce, the look-alike of the small spruce card C2 and C4 stamp: a narrow cone of drooping
branch tiers from the ground to a slim leader, each tier lit yellow-green on top and dark beneath.

C2's and C4's cards are one picture at one size, but C4's texels are about half as bright and a few
degrees bluer, so each chapter takes its own colours (and its own bytes). Both write clump tiles: with
clumps on (neither chapter's default), spruce takes three of C4's six slots and one of C2's.
Builder: veg_fir.py.
"""

import os
import sys

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import veg_fir  # noqa: E402

veg_fir.main({
    "model": "spruce.flt",
    # The card quad per chapter, width and height in metres: extracted/<C>/gamez, the model under
    # spruce.flt. --render checks the extracted card against this and stops on a mismatch.
    "cards": {"C2": (5.566, 8.997), "C4": (5.566, 8.997)},
    "seed": 4,
    # Crown radius over card width at a height fraction, off the card's alpha silhouette (the mean of
    # both sides): a short trunk, branches sweeping to the ground at 0.05, widest at 0.3, then a taper.
    "profile": [(0.0, 0.04), (0.03, 0.04), (0.05, 0.40), (0.15, 0.39), (0.3, 0.42), (0.45, 0.36),
                (0.55, 0.28), (0.65, 0.21), (0.75, 0.155), (0.85, 0.11), (0.93, 0.05), (0.98, 0.0)],
    "apex": 0.98,
    # Needle colours (sRGB) per chapter: shade, body and highlight, the card's crown texels at each level
    # (D33, Colour in game); C4's at twice the card's linear brightness, which the grade takes back.
    "colours": {"C2": ((0.114, 0.153, 0.055), (0.187, 0.243, 0.106), (0.369, 0.424, 0.238)),
                "C4": ((0.091, 0.123, 0.049), (0.156, 0.19, 0.102), (0.287, 0.33, 0.208))},
    # Whorls, as the card's branch tiers, with more of the share between them than dougfirtree1's.
    "tiers": {"at": (0.07, 0.16, 0.25, 0.35, 0.45, 0.55, 0.65, 0.75, 0.85), "width": 0.025, "gap": 0.2},
    "trunk": {"r_base": 0.12, "r_top": 0.05, "top": 0.6, "colour": (0.14, 0.10, 0.07)},
    "core": {"fs": (0.04, 0.2, 0.4, 0.6, 0.75, 0.87), "r": 0.3},
    "clumps": {"span": (0.03, 0.94), "floor": 0.2, "top": 0.85, "half_width": (0.35, 0.5), "reach": (0.85, 1.1), "droop": 0.8},
    # Tiers lit on top and dark beneath, with yellow-green growth at the tips; the thin core above lets
    # the shade between tiers show, as on the card.
    "needle": {"across": 0.9, "tip": 0.2, "high": (0.08, 0.6)},
    "leader": 0.4,
    "count": 110,
    # The clump tiles: trees per tile, and per chapter the linear factor per channel that puts the tiles'
    # mean on the card's (sRGB C2 53.5, 65.5, 29.0; C4 28.2, 34.8, 17.3 over solid texels).
    "clump_trees": (3, 2, 4),
    "clump_grade": {"C2": (0.965, 1.04, 0.361), "C4": (0.389, 0.424, 0.18)},
})

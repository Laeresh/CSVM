"""Generates firtree2, the look-alike of the full, broad fir card C1's forest stamps (and swaps in for
one firtree1 stamp in ten): a dense olive cone from a short bare trunk, its lower edge a near-level
skirt, fine-needled with no tiers, lighter and yellower than firtree1.

C4's card is the same tree photograph at 15 x 13 m against C1's 11 x 21 m (a squat cone; C4 swaps it
in for five firtree1 stamps in fourteen), so C4 takes its own CARDS entry and the crown is built to
that card's width and height. Builder: veg_fir.py.
"""

import os
import sys

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import veg_fir  # noqa: E402

veg_fir.main({
    "model": "firtree2.flt",
    # The card quad per chapter, width and height in metres: extracted/<C>/gamez, the model under
    # firtree2.flt. --render checks the extracted card against this and stops on a mismatch.
    "cards": {"C1": (10.719, 20.883), "C4": (15.139, 13.189)},
    "seed": 2,
    # Crown radius over card width at a height fraction, off the C1 card's alpha silhouette: the trunk
    # alone below 0.11, the widest skirt to 0.36, then a straight taper to a rounded top.
    "profile": [(0.0, 0.03), (0.10, 0.03), (0.11, 0.40), (0.2, 0.43), (0.34, 0.44), (0.42, 0.38),
                (0.52, 0.36), (0.6, 0.30), (0.7, 0.25), (0.8, 0.18), (0.88, 0.15), (0.93, 0.09),
                (0.975, 0.0)],
    "apex": 0.975,
    # Needle colours (sRGB): shade, body and highlight, olive like the card's (body 30, 34, 19;
    # top tenth 68, 75, 44), with more highlight specks than firtree1, for its finer, lighter grain.
    # Hue and saturation per level are the card's crown texels', as firtree1's; the levels sit low
    # enough that the brightness grade stays inside its range.
    "colours": ((0.022, 0.027, 0.018), (0.129, 0.141, 0.092), (0.394, 0.428, 0.279)),
    # Lit nearly evenly across a clump, so the crown reads as one fuzzy mass rather than in layers.
    "needle": {"base": 0.5, "across": 0.3, "high": (0.12, 0.4)},
    "trunk": {"r_base": 0.32, "r_top": 0.14, "top": 0.5, "colour": (0.12, 0.09, 0.06)},
    "core": {"fs": (0.11, 0.2, 0.4, 0.6, 0.75, 0.87), "r": 0.5},
    # Clumps droop less than firtree1's, so the skirt hangs near the card's level edge, and the raised
    # floor keeps the upper crown full to its rounded top.
    "clumps": {"span": (0.13, 0.94), "floor": 0.35, "half_width": (0.38, 0.5), "droop": 0.6, "top": 0.9},
    "count": 190,
    # The clump tiles, as firtree1's: trees per tile, and per chapter the linear factor per channel that
    # puts the tiles' mean on the card's (sRGB C1 30.9, 34.9, 19.9; C4 30.7, 34.6, 19.7 over solid texels).
    "clump_trees": (3, 2),
    "clump_grade": {"C1": (0.407, 0.404, 0.214), "C4": (0.351, 0.344, 0.187)},
})

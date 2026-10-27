"""Generates firtree1, the look-alike of the dense fir card C1's forest is stamped with: shaggy, not
tiered, a dark body with yellow-green specks of new growth toward the clump tips, no bare trunk up
the crown.

C4's card is the same tree photograph at 10 x 13 m against C1's 10 x 18 m, so C4 takes its own
CARDS entry: the crown is built to that card's width and height, its silhouette the card's, rather
than C1's model scaled down (which would stand 7.4 m wide against a 10 m card). Builder: veg_fir.py.
"""

import os
import sys

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import veg_fir  # noqa: E402

veg_fir.main({
    "model": "firtree1.flt",
    # The card quad per chapter, width and height in metres: extracted/<C>/gamez, the model under
    # firtree1.flt. --render checks the extracted card against this and stops on a mismatch.
    "cards": {"C1": (10.026, 17.834), "C4": (10.026, 13.189)},
    "seed": 1,
    # Crown radius as a fraction of the card width, at a height fraction of the card height, read off
    # the C1 card's alpha silhouette (half the opaque span per row); C4's matches it within 0.03.
    "profile": [(0.0, 0.30), (0.1, 0.35), (0.3, 0.35), (0.45, 0.29), (0.55, 0.26), (0.65, 0.21),
                (0.75, 0.14), (0.85, 0.09), (0.93, 0.04), (0.975, 0.0)],
    "apex": 0.975,
    # Needle colours (sRGB): the shade inside the crown, the clump body, and the highlight on the outer
    # foliage. Each takes the hue and saturation of the card's crown texels at its brightness (darkest,
    # middle and lightest fifth), since the game draws the model unlit; the levels sit low enough that
    # the brightness grade stays inside its range on both chapters.
    "colours": ((0.013, 0.054, 0.003), (0.12, 0.179, 0.108), (0.391, 0.477, 0.309)),
    "count": 165,
    # The clump tiles: trees per tile (CanopyClumps composes two to four), and per chapter the linear
    # factor per channel that puts the tiles' mean colour on the card's (sRGB C1 21.8, 30.1, 16.3; C4
    # 19.9, 27.5, 14.8 over solid texels). The cards draw to 1 km and hand over to the clumps past it,
    # so a lighter tile would show as a band.
    "clump_trees": (3, 2, 4),
    "clump_grade": {"C1": (0.265, 0.303, 0.157), "C4": (0.222, 0.253, 0.134)},
})

"""Where the instruments sit, per HUD. One table, so nothing else guesses.

The original draws the same instruments two ways, and they are not the same
measurement problem:

  COCKPIT - the dials are painted on a 3D panel model under a fixed camera, so
    they cluster in one strip across the bottom of the screen and reach the
    screen through a homography. That is why `fitdial` fits an *affine* (and
    recovers a real shear, mirror-symmetric about the screen centre), why the
    panel can translate with screen shake, and why auto head turn invalidates a
    clip - it rotates the camera, so the dials foreshorten in opposite
    directions and no translation model can undo it.

  CHASE - the same dials are screen-space sprites pinned to the left and right
    edges, in two columns, with the compass tape at top centre. Being 2-D
    overlays they are axis-aligned, unsheared and pixel-locked by construction:
    they cannot shear, shake or foreshorten, so `checkclip`'s head-turn gate has
    nothing to test and does not apply (it says so rather than passing quietly).
    What moves instead is the *world behind them*, which is why the chase
    reference frame is a temporal MEDIAN over the clip - the HUD is the only
    thing holding still, so it comes out crisp while the sea and the aircraft
    smear away.

Everything here is canonical game (1280x720) coords; `extract.py` normalises
every capture geometry into that frame before anything else runs, so no value
in this file depends on the recording's resolution.

Measured off the footage, not eyeballed: the chase centres and radius come from
a static-and-structured map (median gradient / (1 + temporal std)) over
`CAP-18`, which puts the two columns at x = 212.5 and 1068.0 and the rows at
y = 468.0 / 560.5 / 653.0, dials 83 px across. `fitdial` then locks them
against the dials' own textures - see FINDINGS.md for the NCC it reached.
"""

# --- regions -----------------------------------------------------------------
# Per HUD: named boxes (x0, y0, x1, y1) in game coords. "panel" is the default
# region every downstream stage reads; the others are extracted on request.
# A HUD's panel box must contain every dial it declares, with a little margin -
# a box that clips a dial makes `fitdial` fail at NCC ~0 instead of loudly.
REGIONS = {
    "cockpit": {
        # compass tape down to the frame bottom - the dials are one cluster
        "panel": (295, 355, 985, 720),
    },
    "chase": {
        # the two edge columns; the middle is sky, and carrying it costs little
        # next to keeping one rectangular ROI that every stage can share
        "panel": (165, 405, 1120, 710),
        # top-centre heading tape. NOT in "panel": it is 370 px above the dials
        # and including it would triple the ROI for one strip. Extract it
        # explicitly when a chase clip has to give heading.
        "compass": (540, 0, 740, 45),
    },
}

# --- dials -------------------------------------------------------------------
# Per HUD: dial name -> (centre, (u_axis, v_axis), search box, texture), all in
# PANEL-LOCAL coords (subtract the panel origin above). These are `fitdial`
# start points and its search window, not final geometry - the fit refines them
# and reports the NCC it reached.
#
# The dial's ROLE and its TEXTURE are different names and must stay separable:
# the rockets gauge is drawn from `missilegauge.png`, and assuming role == file
# is what made the first chase fit crash rather than mis-fit.
DIALS = {
    "cockpit": {
        "altimeter": ((78.0, 214.0), (46.0, 46.0), (20, 155, 140, 275), "altimeter"),
        "speedometer": ((612.0, 216.0), (46.0, 46.0), (550, 155, 675, 275), "speedometer"),
        "horizonindicator": ((348.0, 270.0), (46.0, 50.0), (285, 210, 410, 335),
                             "horizonindicator"),
    },
    "chase": {
        # left column, top to bottom.
        # ⚠ rockets is the one dial whose texture fit does NOT lock (NCC 0.10 -
        # the BOOM/ordnance readouts cover most of its face), so this centre is
        # not a start point but the ANSWER, measured two independent ways that
        # agree to 0.03 px: the left column's x from the altimeter fit, the row
        # spacing from the right column (gungauge->speedometer 92.85 px), and
        # separately a pivot solved from the arrow's own sweep lines (47.24,
        # 54.39). `NCC_FLOOR` below stops the failed fit overriding it.
        "rockets": ((47.21, 54.37), (47.1, 47.1), (0, 8, 100, 108), "missilegauge"),
        "altimeter": ((47.5, 155.5), (41.5, 41.5), (0, 108, 100, 208), "altimeter"),
        "horizonindicator": ((47.5, 248.0), (41.5, 41.5), (0, 200, 100, 300),
                             "horizonindicator"),
        # right column
        "gungauge": ((903.0, 63.0), (41.5, 41.5), (855, 15, 955, 115), "gungauge"),
        "speedometer": ((903.0, 155.5), (41.5, 41.5), (855, 108, 955, 208), "speedometer"),
    },
}

# The cockpit strip carries the weapon gauges too; they were never fitted
# because nothing needed them until BL-184. Start centres are the midpoints of
# the boxes `shake.py` already masks for these two dials, so they come from the
# same measurement the shake tracker was built on rather than from a fresh
# guess. See FINDINGS.md for the NCC each reached.
DIALS["cockpit"]["gungauge"] = ((277.5, 160.0), (46.0, 46.0), (215, 100, 340, 220),
                                "gungauge")
DIALS["cockpit"]["rockets"] = ((472.5, 162.5), (46.0, 46.0), (415, 105, 530, 220),
                               "missilegauge")

DEFAULT_HUD = "cockpit"


def region(hud, name="panel"):
    """Box (x0, y0, x1, y1) in game coords for one region of one HUD."""
    if hud not in REGIONS:
        raise SystemExit(f"unknown HUD {hud!r}; known: {sorted(REGIONS)}")
    if name not in REGIONS[hud]:
        raise SystemExit(
            f"HUD {hud!r} declares no region {name!r}; has {sorted(REGIONS[hud])}")
    return REGIONS[hud][name]


def dials(hud):
    """Dial name -> (centre, axes, box) in panel-local coords."""
    if hud not in DIALS:
        raise SystemExit(f"unknown HUD {hud!r}; known: {sorted(DIALS)}")
    return DIALS[hud]


def suffix(hud):
    """Cache-name suffix. The cockpit keeps the bare legacy names, so every
    artifact and every published number from the 2026-07/08 sessions stays
    valid and reproducible without a re-run."""
    return "" if hud == DEFAULT_HUD else f"_{hud}"


def med_path(hud, cache=".scratch/vidcal/cache"):
    return f"{cache}/pool_med{suffix(hud)}.npy"


def std_path(hud, cache=".scratch/vidcal/cache"):
    return f"{cache}/pool_std{suffix(hud)}.npy"


def affines_path(hud, cache=".scratch/vidcal/cache"):
    return f"{cache}/dial_affines{suffix(hud)}.npy"


def affine_order(hud):
    """The order dial affines are stored in, so an index means the same thing
    on both HUDs. Cockpit keeps its historical first three positions."""
    if hud == "cockpit":
        return ["altimeter", "speedometer", "horizonindicator", "gungauge", "rockets"]
    return ["altimeter", "speedometer", "horizonindicator", "gungauge", "rockets"]


# A fit below this NCC has not locked, and its centre can be wildly wrong while
# looking like a number. `load_affine` refuses to hand one back, so callers fall
# through to the measured centre in DIALS instead of silently using it.
#
# ⚠ This floor exists because a wrong dial centre NEVER FAILS LOUDLY. It skews
# every angle read off that dial smoothly, by an amount that varies around the
# face. The rockets fit (NCC 0.10, centre 9.5 px out) turned a clean 8-slot
# lattice into angles that fitted no lattice at all, and the only reason it was
# caught is that the moves it implied were geometrically impossible.
NCC_FLOOR = 0.45


def ncc_path(hud, cache=".scratch/vidcal/cache"):
    return f"{cache}/dial_ncc{suffix(hud)}.npy"


def load_affine(hud, dial, cache=".scratch/vidcal/cache"):
    """The fitted affine for one dial, by name rather than by index.

    Raises if the dial has no stored fit, or if its fit did not clear
    NCC_FLOOR - an unlocked fit is worse than no fit, because it is silently
    plausible."""
    import os

    import numpy as np
    P = np.load(affines_path(hud, cache))
    order = affine_order(hud)
    if dial not in order:
        raise SystemExit(f"{dial!r} is not in {hud}'s affine order {order}")
    i = order.index(dial)
    if i >= len(P):
        raise SystemExit(
            f"{affines_path(hud, cache)} holds {len(P)} affines but {dial!r} is "
            f"index {i}; re-run fitdial.py for HUD {hud!r}")
    p = ncc_path(hud, cache)
    if os.path.exists(p):
        n = np.load(p)
        if i < len(n) and n[i] < NCC_FLOOR:
            raise SystemExit(
                f"{hud}/{dial} fit did not lock (NCC {n[i]:.4f} < {NCC_FLOOR}); "
                f"use the measured centre in hud.DIALS instead")
    return P[i]

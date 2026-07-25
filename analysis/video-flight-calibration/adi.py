"""Read the artificial horizon: sky fraction -> horizon cut distance -> sin(pitch).

For a gyro ball the painted horizon is a great circle, so its projection is a
chord whose signed distance from the dial centre is R.sin(theta). The *area*
either side of that chord is therefore a known function of sin(theta) - and using
area rather than fitting the boundary line survives the ball's coarse
tessellation, the fixed aircraft symbol painted over it and the pitch ladder.

The one fact that makes this enough: sin(180 - theta) = sin(theta), so
sin(gamma) = sin(theta) whether the aircraft is upright or inverted. Vertical
speed needs only sin(gamma), so the ball's bank state never has to be untangled.
"""
import numpy as np

CACHE = ".scratch/vidcal/cache"

# ball aperture, ROI-local, from the max-minus-min dynamic range map
CX, CY = 344.5, 274.5
RX, RY = 50.0, 47.0
SKY_CUT = 90.0     # luma: black ground below, cream/grey sky above


def ball_mask(shape, rx=RX, ry=RY, rmax=0.94):
    ys, xs = np.mgrid[0:shape[0], 0:shape[1]]
    q = ((xs - CX) / rx) ** 2 + ((ys - CY) / ry) ** 2
    return q <= rmax ** 2


def seg_area_frac(s):
    """Fraction of a unit disc on the far side of a chord at signed distance -s.
    s = +1 -> whole disc, s = -1 -> nothing. Monotonic in s."""
    s = np.clip(s, -1, 1)
    return (np.arccos(-s) + s * np.sqrt(1 - s * s)) / np.pi


def invert_frac(F, n=4001):
    """Numeric inverse of seg_area_frac."""
    grid = np.linspace(-1, 1, n)
    tab = seg_area_frac(grid)
    return np.interp(np.clip(F, 0, 1), tab, grid)


def sky_fraction(clip, cut=SKY_CUT):
    a = np.load(f"{CACHE}/{clip}_lum.npy")
    m = ball_mask(a.shape[1:])
    px = a[:, m].astype(np.float32)
    return (px > cut).mean(axis=1)

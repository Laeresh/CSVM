"""Per-frame panel translation (screenshake) by sub-pixel phase correlation.

The cockpit panel is part of the 3D scene, so it moves with camera shake. Every
ROI we read has to move with it. We track a static, feature-rich part of the
panel (rivets + bezels, dials masked out) against a reference frame.
"""
import numpy as np

CACHE = ".scratch/vidcal/cache"

# ROI-local boxes of the *dynamic* instruments, masked out of the shake template.
# ROI origin is game (295, 355).
DIALS = {
    "compass": (235, 15, 455, 70),   # tape scrolls
    "guns": (220, 105, 335, 215),    # GUNS dial (ammo digits)
    "damage": (250, 60, 355, 160),   # damage silhouette
    "rockets": (420, 110, 525, 215),  # ROCKETS dial
    "alt": (20, 160, 135, 275),      # altimeter needles
    "mph": (555, 163, 670, 273),     # speedometer needle
    "adi": (280, 220, 415, 360),     # artificial horizon
}


def build_mask(shape):
    m = np.ones(shape, np.float32)
    for x0, y0, x1, y1 in DIALS.values():
        m[max(0, y0):y1, max(0, x0):x1] = 0.0
    return m


def hann2(shape):
    wy = np.hanning(shape[0])
    wx = np.hanning(shape[1])
    return np.outer(wy, wx).astype(np.float32)


def _parabolic(v_m1, v_0, v_p1):
    """Sub-pixel peak offset from three samples around the max."""
    d = v_m1 - 2 * v_0 + v_p1
    if abs(d) < 1e-12:
        return 0.0
    return float(np.clip(0.5 * (v_m1 - v_p1) / d, -1.0, 1.0))


def track(short, ref_frames=31, max_shift=24):
    a = np.load(f"{CACHE}/{short}_lum.npy").astype(np.float32)
    n, h, w = a.shape
    mask = build_mask((h, w))
    win = hann2((h, w)) * mask
    ref = np.median(a[:ref_frames], axis=0)

    def prep(img):
        x = (img - img.mean()) * win
        return np.fft.rfft2(x)

    F_ref = prep(ref)
    out = np.zeros((n, 2), np.float32)
    for i in range(n):
        F = prep(a[i])
        cps = F * np.conj(F_ref)
        mag = np.abs(cps)
        cps = np.where(mag > 1e-9, cps / np.maximum(mag, 1e-9), 0)  # phase-only
        c = np.fft.irfft2(cps, s=(h, w))
        c = np.fft.fftshift(c)
        cy, cx = h // 2, w // 2
        sub = c[cy - max_shift:cy + max_shift + 1, cx - max_shift:cx + max_shift + 1]
        j = np.unravel_index(np.argmax(sub), sub.shape)
        py, px = j[0], j[1]
        dy = py - max_shift
        dx = px - max_shift
        if 0 < py < sub.shape[0] - 1:
            dy += _parabolic(sub[py - 1, px], sub[py, px], sub[py + 1, px])
        if 0 < px < sub.shape[1] - 1:
            dx += _parabolic(sub[py, px - 1], sub[py, px], sub[py, px + 1])
        out[i] = (dx, dy)
    np.save(f"{CACHE}/{short}_shift.npy", out)
    np.save(f"{CACHE}/{short}_ref.npy", ref)
    return out


if __name__ == "__main__":
    for s in ["pitch", "roll", "yaw", "dive", "accel", "decel"]:
        sh = track(s)
        print(f"{s:6s} dx  min={sh[:,0].min():+7.2f} max={sh[:,0].max():+7.2f} "
              f"std={sh[:,0].std():.2f}   dy min={sh[:,1].min():+7.2f} "
              f"max={sh[:,1].max():+7.2f} std={sh[:,1].std():.2f}")

"""Gate a new clip before decoding it: is the cockpit view rigid?

The decoder registers each dial to the pooled median by TRANSLATION only. That
holds while the camera is fixed in the cockpit. The original's auto head turn
rotates it instead, so the panel's perspective changes and the altimeter and
speedometer foreshorten in opposite directions - a shift the translation model
cannot represent and will silently absorb into wrong needle angles.

Verdict rule:
  dx(ALT) - dx(MPH) ~ 0  and peaks healthy  -> rigid, safe to decode
  dy equal but dx opposite, peaks collapsed -> head turn, re-record

Usage (from the repo root, after extract.py has cached the clip):
  python -c "import sys;sys.path.insert(0,'analysis/video-flight-calibration');\
             import checkclip;checkclip.report('dive2')"
"""
import numpy as np

CACHE = ".scratch/vidcal/cache"


def _box(p, r=1.15):
    cx, cy, a, b, c, d = p
    xs = [cx + a * r * sx + b * (-r * sy) for sx in (-1, 1) for sy in (-1, 1)]
    ys = [cy + c * r * sx + d * (-r * sy) for sx in (-1, 1) for sy in (-1, 1)]
    return (int(min(xs)), int(min(ys)), int(max(xs)), int(max(ys)))


def _pcorr(img, ref, box, ms=60):
    x0, y0, x1, y1 = box
    H, W = y1 - y0, x1 - x0
    A = img[max(0, y0 - ms):y1 + ms, max(0, x0 - ms):x1 + ms].astype(np.float32)
    B = np.zeros_like(A)
    oy, ox = y0 - max(0, y0 - ms), x0 - max(0, x0 - ms)
    B[oy:oy + H, ox:ox + W] = ref[y0:y1, x0:x1]
    h, w = A.shape
    win = np.outer(np.hanning(h), np.hanning(w)).astype(np.float32)
    FA = np.fft.rfft2((A - A.mean()) * win)
    FB = np.fft.rfft2((B - B.mean()) * win)
    cps = FA * np.conj(FB)
    m = np.abs(cps)
    cps = np.where(m > 1e-9, cps / np.maximum(m, 1e-9), 0)
    c = np.fft.fftshift(np.fft.irfft2(cps, s=(h, w)))
    cy, cx = h // 2, w // 2
    s = c[cy - ms:cy + ms + 1, cx - ms:cx + ms + 1]
    j = np.unravel_index(np.argmax(s), s.shape)
    return int(j[1]) - ms, int(j[0]) - ms, float(s.max())


def report(clip, step=10, dx_tol=2.0, peak_floor=0.15):
    med = np.load(f"{CACHE}/pool_med.npy")
    P = np.load(f"{CACHE}/dial_affines.npy")
    a = np.load(f"{CACHE}/{clip}_lum.npy")
    bA, bS = _box(P[0]), _box(P[1])
    rows = []
    for i in range(0, len(a), step):
        ax, ay, ap = _pcorr(a[i], med, bA)
        sx, sy, sp = _pcorr(a[i], med, bS)
        rows.append((i, ax, ay, ap, sx, sy, sp, ax - sx, ay - sy))
    r = np.array(rows, float)
    dxspread = np.abs(r[:, 7])
    peaks = np.minimum(r[:, 3], r[:, 6])
    print(f"{clip}: {len(a)} frames, {len(r)} sampled")
    print(f"  dial translation range: ALT dy {r[:,2].min():+.0f}..{r[:,2].max():+.0f} px, "
          f"MPH dy {r[:,5].min():+.0f}..{r[:,5].max():+.0f} px")
    print(f"  |dx(ALT) - dx(MPH)|: mean {dxspread.mean():.2f} max {dxspread.max():.0f} px "
          f"(rigid view keeps this ~0)")
    print(f"  registration peak: min {peaks.min():.2f} median {np.median(peaks):.2f}")
    # The signature is the SIGN, not the size. Head turn foreshortens the two dials
    # in opposite directions, so their dx anti-correlates; screen shake translates
    # the whole panel, so dx correlates positively however large the excursion. The
    # spread alone cannot tell them apart - a hard dive trips a bare |dx| threshold
    # while staying perfectly decodable (shake.py exists for exactly that clip).
    dxcorr = float(np.corrcoef(r[:, 1], r[:, 4])[0, 1]) if r[:, 1].std() > 0 else 1.0
    print(f"  corr dx(ALT), dx(MPH): {dxcorr:+.2f} "
          f"(shake keeps this ~+1; head turn drives it negative)")
    bad_dx = dxspread.max() > dx_tol and dxcorr < 0.5
    bad_pk = np.median(peaks) < peak_floor
    if bad_dx or bad_pk:
        why = []
        if bad_dx:
            why.append("the two dials shear apart horizontally")
        if bad_pk:
            why.append("registration confidence collapsed")
        print(f"  VERDICT: REJECT — {', and '.join(why)}. "
              f"Consistent with auto head turn; re-record with it off.")
        return False
    if dxspread.max() > dx_tol:
        print(f"  VERDICT: OK — panel translates as one ({dxcorr:+.2f}), so the "
              f"{dxspread.max():.0f} px excursion is shake, not head turn. Run shake.py.")
        return True
    print("  VERDICT: OK — view is rigid, translation registration is valid.")
    return True


if __name__ == "__main__":
    import sys
    ok = all(report(c) for c in (sys.argv[1:] or ["dive2"]))
    raise SystemExit(0 if ok else 1)

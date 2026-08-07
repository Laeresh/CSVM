# Per-frame plane pose + firing indicator for "Gun Wobble and animation.mp4".
#
# Plane: red-dominance mask in the game region (chase view, red Bloodhawk against
# gray sky / teal sea); centroid + principal-axis angle + area per frame.
# Firing: frame-difference energy in the GUNS ammo-counter ROI (the count redraws
# when rounds leave) plus mean luminance there, and the same for a muzzle ROI at
# the spinner hub. Saves everything to ./.scratch/gun-wobble/series.npz.
import os

import imageio_ffmpeg as iio
import numpy as np

VIDEO = r"Z:\CSVM\OriginalScreenshots\Videos\Gun Wobble and animation.mp4"
OUT = os.path.join(".scratch", "gun-wobble")
X0, X1 = 640, 1920

AMMO = (slice(450, 478), slice(1030, 1100))   # y, x in game coords: the "2371" text
HUB = (slice(495, 545), slice(600, 690))      # spinner hub / muzzle neighbourhood


def main():
    os.makedirs(OUT, exist_ok=True)
    reader = iio.read_frames(VIDEO)
    meta = next(reader)
    w, h = meta["size"]
    fps = meta["fps"]

    cx, cy, ang, area = [], [], [], []
    ammo_diff, ammo_lum, hub_lum = [], [], []
    prev_ammo = None
    for raw in reader:
        g = np.frombuffer(raw, dtype=np.uint8).reshape(h, w, 3)[:, X0:X1].astype(np.int16)
        r, gr, b = g[..., 0], g[..., 1], g[..., 2]
        mask = (r > gr + 25) & (r > b + 25) & (r > 60)
        ys, xs = np.nonzero(mask)
        if len(xs) < 200:
            cx.append(np.nan); cy.append(np.nan); ang.append(np.nan); area.append(len(xs))
        else:
            mx, my = xs.mean(), ys.mean()
            dx, dy = xs - mx, ys - my
            cov_xx, cov_yy, cov_xy = (dx * dx).mean(), (dy * dy).mean(), (dx * dy).mean()
            # principal axis of the red blob: for a wing-spanning blob this is the
            # wing line, so its angle tracks visual roll
            ang.append(0.5 * np.degrees(np.arctan2(2 * cov_xy, cov_xx - cov_yy)))
            cx.append(mx); cy.append(my); area.append(len(xs))

        a = g[AMMO[0], AMMO[1]].astype(np.float32)
        ammo_lum.append(float(a.mean()))
        ammo_diff.append(0.0 if prev_ammo is None else float(np.abs(a - prev_ammo).mean()))
        prev_ammo = a
        hub_lum.append(float(g[HUB[0], HUB[1]].astype(np.float32).mean()))

    np.savez(os.path.join(OUT, "series.npz"),
             fps=fps, cx=cx, cy=cy, ang=ang, area=area,
             ammo_diff=ammo_diff, ammo_lum=ammo_lum, hub_lum=hub_lum)
    cx, cy, ang = map(np.asarray, (cx, cy, ang))
    ammo_diff = np.asarray(ammo_diff)
    print(f"frames {len(cx)} fps {fps}")
    print(f"area med {np.nanmedian(area):.0f}  cx {np.nanmean(cx):.1f}±{np.nanstd(cx):.2f}  "
          f"cy {np.nanmean(cy):.1f}±{np.nanstd(cy):.2f}  ang {np.nanmean(ang):.2f}±{np.nanstd(ang):.2f}")
    # candidate firing frames: counter redraw energy well above its quiet floor
    floor = np.median(ammo_diff)
    mad = np.median(np.abs(ammo_diff - floor)) + 1e-9
    fire = np.nonzero(ammo_diff > floor + 8 * mad)[0]
    print(f"ammo-diff floor {floor:.3f} mad {mad:.3f}; {len(fire)} redraw frames")
    if len(fire):
        # group into windows
        groups = np.split(fire, np.nonzero(np.diff(fire) > 3)[0] + 1)
        for grp in groups:
            print(f"  window f{grp[0]}..f{grp[-1]}  t={grp[0]/fps:.2f}..{grp[-1]/fps:.2f}s")


if __name__ == "__main__":
    main()

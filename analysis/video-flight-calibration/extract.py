"""Pull the instrument region out of each clip into a .npy cache.

The game always renders 16:9; only the *capture region* around it varies by
session, so every clip is normalised to canonical game (1280x720) coords here
and nothing downstream ever sees the capture geometry:

  2560x720  (32:9) - game pillarboxed into x 640..1919 at 1:1.  The 2026-07
                     FlightModel session and CAP-09/10/14.
  2560x1440 (16:9) - game fills the frame at 2x.  The 2026-08-03 CAP session.

The 2x case block-mean downscales to 1280x720, which lands the panel on the
*same pixels* as the 1:1 session - phase correlation of the two panel ROIs
peaks at dx=dy=0 (peak 0.64), so the pooled median, the ROI and the fitted dial
ellipses all carry over unchanged.  Re-run that check (see `checkclip.report`)
before trusting a third capture geometry; FINDINGS.md's "pooled median is only
usable because the panel is pixel-locked" trap applies to every new session.

WHICH HUD a clip was flown in is a separate axis from its capture geometry, and
it has to be declared per clip because it cannot be inferred from the frame
size: cockpit and chase put the instruments in completely different places.
`hud.py` owns those layouts; a clip names its HUD in CLIPS and everything
downstream reads the layout from there.  Cache names are unchanged for cockpit
clips, so every artifact and published number from the earlier sessions stays
valid.
"""
import os
import sys

import imageio_ffmpeg as iio
import numpy as np

import hud as hudlib

VID = "OriginalScreenshots/Videos"
OUT = ".scratch/vidcal/cache"

# capture (w, h) -> game rect origin + integer downscale to reach 1280x720.
# Unknown geometry is an error, not a guess: a wrong origin decodes silently.
LAYOUTS = {
    (2560, 720): (640, 0, 1),
    (2560, 1440): (0, 0, 2),
    # 2560x728: the 32:9 layout with EIGHT extra captured rows - six above the game
    # rect and two below. Measured, not assumed: registering the altimeter and the
    # speedometer independently against the chase pooled median walks dy 1:1 with the
    # trial origin and both dials land at dx = dy = 0 at gy = 6 (peaks 0.893/0.823 and
    # 0.896/0.897 on the two CAP-14 building clips, against 0.886/0.901 for a
    # known-geometry control). The whole-panel correlation is NOT usable here - it
    # peaks at 0.20, because the airframe is a Balmoral (so the damage silhouette and
    # weapon readouts are not the pooled median's) and an 8 s city pass does not smear
    # the world away.
    (2560, 728): (640, 6, 1),
}

CLIPS = {
    # 2026-07 session, 32:9 - what pinned the clock (FINDINGS.md)
    "pitch": "FlightModel/Bloodhawk Pitch.mp4",
    "roll": "FlightModel/Bloodhawk Roll.mp4",
    "yaw": "FlightModel/Bloodhawk Yaw.mp4",
    "dive": "FlightModel/Bloodhawk Dive, screenshake so no exakt hud positions.mp4",
    "accel": "FlightModel/Bloodhawk Accelate 1-8 to 8-8.mp4",
    "decel": "FlightModel/Bloodhawk Deccelarate 8-8 to 1-8.mp4",
    "ceiling": "FlightModel/Bloodhawk Ceiling.mp4",
    "dive2": "FlightModel/Bloodhawk Dive 2.mp4",
    # 2026-08-03 session, 16:9 - the owed captures (playtest.md 0)
    "cap01": "CAP-01.mp4",                              # banked max-pull turn
    "cap04": "CAP-04.mp4",                              # ~45 deg pitch trace
    "cap04b": "CAP-04 2.mp4",
    "cap05knife": "CAP-05 Knife Edge.mp4",
    "cap05knife2": "CAP-05 2 Knife Edge.mp4",
    "cap05stall0": "CAP-05 Stall 0% Thrust no input.mp4",
    "cap05stall50": "CAP-05  Stall 50% thrust climb.mp4",
    "cap06": "CAP-06.mp4",                              # stall-warning approach
    "cap06b": "CAP-06 2.mp4",
    "cap10": "CAP-10 2.mp4",                            # engine note through a dive
    # CAP-03: level full-throttle runs held to equilibrium, one clip per altitude.
    # 6800 ft has no clip - the aircraft auto-stalls at/above ~6600 ft, which is
    # itself the answer BL-094 was after.
    "cap03a": "CAP-03 5500ft.mp4",
    "cap03b": "CAP-03 6000ft.mp4",
    "cap03c": "CAP-03 6500ft.mp4",
    "cap03stall": "CAP-03 Stall at max Alt.mp4",         # the ~6600 ft forced stall
    # 2026-08-03 CAP-04 re-record: square-wave pitch cadence driven by
    # pitch_cadence.ahk, one clip per period, each with its edge log alongside.
    # tau comes from the ripple amplitude ACROSS these, not from any one of them.
    "pt230": "playtest/CAP-04/Pitch Test 230ms.mp4",
    "pt370": "playtest/CAP-04/Pitch Test 370ms.mp4",
    "pt570": "playtest/CAP-04/Pitch Test 570ms.mp4",
    "pt930": "playtest/CAP-04/Pitch Test 930ms.mp4",
    "pt700": "playtest/CAP-04/Pitch Test 700ms.mp4",
    "pt1300": "playtest/CAP-04/Pitch Test 1300ms.mp4",
    # duty-mode control: pulses the pull key alone at 50%, so it measures a MEAN
    # rate rather than a ripple - the test of whether short presses reach the game.
    "pt230duty": "playtest/CAP-04/Pitch Test 230ms duty.mp4",
    # --- CHASE view. A bare string above means cockpit; these say so explicitly
    # because the instruments are somewhere else entirely (hud.py).
    "cap18": ("CAP-18.mp4", "chase"),                   # weapon-switch arrow
    "cap10chase": ("CAP-10 3 3rd Person.mp4", "chase"),
    # The fourth CAP-10 take, named for its subject rather than the capture. Was once
    # believed to hold a pull-out; the decoded state says it does not (HISTORY (b)).
    "cap10dive": ("Bloodhawk Dive Sound.mp4", "chase"),
    # 2026-08-04 re-record, to separate elevator input from climb rate. `cap10bank` is
    # the discriminator: at 90 deg of bank the elevator swings the nose in azimuth, so
    # pitch input is large while climb rate stays near zero.
    "cap10var": ("CAP-10 Dive 100% Thrust variable climb rate.mp4", "chase"),
    "cap10bank": ("CAP-10 90° Banked Pith Up Down.mp4", "chase"),
    "cap10climb": ("CAP-10 Variable Climp Pitch up.mp4", "chase"),
    # 2026-08-04 SCRIPTED runs, driven by capture-rigs/ElevatorDutySweep.ahk, which
    # logs every key edge - so for these two the elevator input is known rather than
    # inferred. F13 = duty staircase at zero mean pitch rate; F14 = held-pull ladder.
    "cap10f13": ("CAP-10 F13 scripted run.mp4", "chase"),
    "cap10f14": ("CAP-10 F14.mp4", "chase"),
    # The pull-out. Nine takes in, no clip yet held a dive THROUGH the recovery, which
    # is the one thing BL-109's "~1.05 overshoot at pull-out" claim needs to be tested.
    "cap10rec": ("CAP-10 Dive Recovery.mp4", "chase"),
    # 2026-08-04 CAP-21, driven by capture-rigs/ThrottleSweep.ahk. Chase view, the
    # camera untouched throughout, so the aircraft's apparent size in frame is the
    # measurement and the speedometer is the abscissa (BL-248). The two `full` takes
    # are the CONTROL: same speeds, a completely different throttle history, so a
    # size that tracks speed must agree across the pair and one that tracks throttle
    # setting or acceleration cannot.
    "cap21up": ("CAP-21 0 to 100 Stepped.mp4", "chase"),
    "cap21down": ("CAP-21 100 to 0 Stepped.mp4", "chase"),
    "cap21upfull": ("CAP-21 0 to 100 Full.mp4", "chase"),
    "cap21downfull": ("CAP-21 100 to 0 Full.mp4", "chase"),
    # 2026-08-04 CAP-14, graze vs crash (BL-172). Chase view, so the compass tape
    # is available as well as alt/mph - the graze is against a near-VERTICAL cliff
    # face, where a normal-direction restitution shows up in heading, not altitude.
    # The graze clip also carries CAP-15 (the burn-down to a red wing) after 8 s.
    "cap14graze": ("CAP-14 Graze and CAP 15 wing to red.mp4", "chase"),
    "cap14crash": ("CAP-14 Crash.mp4", "chase"),
    # Named for their subject rather than a CAP id, but they are the same question:
    # both end in a fatal contact with near-FLAT ground, where the graze clip's
    # cliff face gives the altimeter no normal component to read.
    "c1crash1": ("C1 IA1 Crash.mp4", "chase"),
    "c1crash2": ("C1 IA1 Crash 2.mp4", "chase"),
    # 2026-08-07 CAP-12: the cloud-deck pass-through (BL-118). Chase view; the
    # altimeter correlates the visual deck state (below / inside / above) with
    # altitude, so the deck's base and top come off the same frames as the look.
    "cap12": ("CAP-12 Clouddeck and Cloud puffers.mp4", "chase"),
    # The C4 take of the same question (doubles as CAP-11 C4). Clear air, so the
    # deck sheet and the scattered puffers are visually separable — the clip that
    # raised the deck-vs-slab gap question.
    "cap12c4": ("CAP-11 C4 and CAP-12 Clouddeck.mp4", "chase"),
    # 2026-08-04 CAP-14 re-record: the contacts the first pair could not give. The
    # first CAP-14 graze was against a near-VERTICAL cliff, where the contact normal
    # is horizontal and the altimeter cannot see it; these are shallow contacts with
    # near-FLAT ground, which put the normal on the altimeter's own axis. Two
    # airframes on purpose - Bloodhawk, and a max-armour Balmoral.
    "cap14bhhard": ("CAP-14 Bloodhawk  Hard Graze.mp4", "chase"),
    "cap14bhgc": ("CAP-14 Bloodhawk Graze and Crash.mp4", "chase"),
    "cap14balnose": ("CAP-14 Balmoral nose Graze.mp4", "chase"),
    "cap14balwing": ("CAP-14 Balmoral Wing Graze x2.mp4", "chase"),
    # C5 downtown - the building-corner pair playtest.md's CAP-14 row has always
    # asked for. Recorded at 2560x728, a geometry that needed its own LAYOUTS row.
    "cap14bldgraze": ("CAP-14 Building Hard graze Balmoral.mp4", "chase"),
    "cap14bldcrash": ("CAP-14 Building crash Balmoral.mp4", "chase"),
}


def clip_hud(short):
    """A CLIPS entry is either a path (cockpit, the historical default) or a
    (path, hud) pair. Kept permissive on purpose: every pre-existing entry keeps
    working untouched."""
    v = CLIPS[short]
    return (v, hudlib.DEFAULT_HUD) if isinstance(v, str) else (v[0], v[1])


def layout(w, h):
    """Capture size -> (game x origin, game y origin, downscale factor)."""
    if (w, h) not in LAYOUTS:
        raise SystemExit(
            f"unknown capture geometry {w}x{h}; add it to LAYOUTS after checking "
            f"the panel still registers against the pooled median"
        )
    return LAYOUTS[(w, h)]


def extract(short, fname, hud=hudlib.DEFAULT_HUD, region="panel"):
    # Normally a path under VID. A clip still staged in playtest/<ID>/ (captures
    # owned by an open item, git-ignored and not swept) is given repo-relative
    # instead, so it can be decoded without being moved in among the user's own
    # game recordings.
    p = os.path.join(VID, fname)
    if not os.path.exists(p) and os.path.exists(fname):
        p = fname
    g = iio.read_frames(p)
    meta = next(g)
    w, h = meta["size"]
    gx, gy, k = layout(w, h)
    x0, y0, x1, y1 = hudlib.region(hud, region)
    # crop in capture coords, then block-mean down to canonical game coords
    cx0, cy0, cx1, cy1 = gx + x0 * k, gy + y0 * k, gx + x1 * k, gy + y1 * k
    frames = []
    for buf in g:
        fr = np.frombuffer(buf, np.uint8).reshape(h, w, 3)
        sub = fr[cy0:cy1, cx0:cx1]
        # Rec.601 luma, kept in uint8
        lum = (sub[:, :, 0] * 0.299 + sub[:, :, 1] * 0.587 + sub[:, :, 2] * 0.114)
        if k > 1:
            r, c = lum.shape
            lum = lum.reshape(r // k, k, c // k, k).mean(axis=(1, 3))
        frames.append(lum.astype(np.uint8))
    g.close()
    a = np.stack(frames)
    tag = "" if region == "panel" else f"_{region}"
    np.save(f"{OUT}/{short}{tag}_lum.npy", a)
    np.save(f"{OUT}/{short}{tag}_meta.npy",
            np.array([meta["fps"], meta["duration"], len(a)]))
    print(f"{short:12s} {a.shape} {w}x{h}/{k}x hud={hud}/{region} "
          f"fps={meta['fps']:.3f} dur={meta['duration']:.2f}")
    return a


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    # `--region=<name>` pulls a non-default region (e.g. chase's compass tape)
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    reg = next((a.split("=", 1)[1] for a in sys.argv[1:]
                if a.startswith("--region=")), "panel")
    want = args or list(CLIPS)
    for s in want:
        path, h = clip_hud(s)
        extract(s, path, h, reg)

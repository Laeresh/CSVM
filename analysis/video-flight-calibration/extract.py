"""Pull the cockpit-panel strip out of each FlightModel clip into a .npy cache.

The capture is 2560x720 with the game pillarboxed into x 640..1919 (1280x720).
Everything we read lives in the bottom-centre panel, so we keep one grayscale
box per frame and work from that.
"""
import os
import sys

import imageio_ffmpeg as iio
import numpy as np

VID = "OriginalScreenshots/Videos/FlightModel"
OUT = ".scratch/vidcal/cache"
GAME_X0 = 640  # pillarbox left edge

# panel strip in game (1280x720) coords: compass tape down to the frame bottom
ROI = (295, 355, 985, 720)  # x0, y0, x1, y1

CLIPS = {
    "pitch": "Bloodhawk Pitch.mp4",
    "roll": "Bloodhawk Roll.mp4",
    "yaw": "Bloodhawk Yaw.mp4",
    "dive": "Bloodhawk Dive, screenshake so no exakt hud positions.mp4",
    "accel": "Bloodhawk Accelate 1-8 to 8-8.mp4",
    "decel": "Bloodhawk Deccelarate 8-8 to 1-8.mp4",
    "ceiling": "Bloodhawk Ceiling.mp4",
    "dive2": "Bloodhawk Dive 2.mp4",
}


def extract(short, fname):
    p = os.path.join(VID, fname)
    g = iio.read_frames(p)
    meta = next(g)
    w, h = meta["size"]
    x0, y0, x1, y1 = ROI
    frames = []
    for buf in g:
        fr = np.frombuffer(buf, np.uint8).reshape(h, w, 3)
        sub = fr[y0:y1, GAME_X0 + x0:GAME_X0 + x1]
        # Rec.601 luma, kept in uint8
        lum = (sub[:, :, 0] * 0.299 + sub[:, :, 1] * 0.587 + sub[:, :, 2] * 0.114)
        frames.append(lum.astype(np.uint8))
    g.close()
    a = np.stack(frames)
    np.save(f"{OUT}/{short}_lum.npy", a)
    np.save(f"{OUT}/{short}_meta.npy", np.array([meta["fps"], meta["duration"], len(a)]))
    print(f"{short:6s} {a.shape} fps={meta['fps']:.3f} dur={meta['duration']:.2f}")
    return a


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    want = sys.argv[1:] or list(CLIPS)
    for s in want:
        extract(s, CLIPS[s])

# Probe "Gun Wobble and animation.mp4": metadata, brightness trace, sample frames.
# Frames land in ./.scratch/gun-wobble/ as contact-sheet PNGs for eyeballing.
import os

import imageio_ffmpeg as iio
import numpy as np

# OriginalScreenshots/ is git-ignored, so a worktree has none: CSVM_DATA_ROOT names the tree
# that does (the same env var the engine reads), defaulting to this checkout.
DATA_ROOT = os.environ.get("CSVM_DATA_ROOT") or os.path.dirname(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
VIDEO = os.path.join(DATA_ROOT, "OriginalScreenshots", "Videos", "Gun Wobble and animation.mp4")
OUT = os.path.join(".scratch", "gun-wobble")


def main():
    os.makedirs(OUT, exist_ok=True)
    reader = iio.read_frames(VIDEO)
    meta = next(reader)
    print("meta:", {k: meta[k] for k in ("size", "fps", "duration") if k in meta})
    w, h = meta["size"]
    fps = meta["fps"]

    lum = []
    samples = {}
    n = 0
    for raw in reader:
        frame = np.frombuffer(raw, dtype=np.uint8).reshape(h, w, 3)
        lum.append(float(frame.mean()))
        if n % int(round(fps)) == 0:  # one sample per second
            samples[n] = frame[::4, ::4].copy()  # quarter res for the sheet
        n += 1
    lum = np.array(lum)
    print(f"frames {n}, {n / fps:.1f} s")
    np.save(os.path.join(OUT, "lum.npy"), lum)

    # Brightness spikes flag muzzle flash / explosions: report top deviations.
    base = np.convolve(lum, np.ones(15) / 15, mode="same")
    dev = lum - base
    hot = np.argsort(dev)[-20:]
    print("brightest spike frames (frame, t, +dev):")
    for f in sorted(hot):
        print(f"  {f:5d}  t={f / fps:6.2f}s  +{dev[f]:.2f}")

    # Contact sheet: 1 fps thumbnails, 10 per row.
    keys = sorted(samples)
    th, tw = samples[keys[0]].shape[:2]
    cols = 10
    rows = (len(keys) + cols - 1) // cols
    sheet = np.zeros((rows * th, cols * tw, 3), dtype=np.uint8)
    for i, k in enumerate(keys):
        r, c = divmod(i, cols)
        sheet[r * th:(r + 1) * th, c * tw:(c + 1) * tw] = samples[k]
    import struct
    import zlib

    def write_png(path, img):
        hh, ww = img.shape[:2]
        raw = b"".join(b"\x00" + img[y].tobytes() for y in range(hh))

        def chunk(tag, data):
            c = struct.pack(">I", len(data)) + tag + data
            return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

        png = (b"\x89PNG\r\n\x1a\n"
               + chunk(b"IHDR", struct.pack(">IIBBBBB", ww, hh, 8, 2, 0, 0, 0))
               + chunk(b"IDAT", zlib.compress(raw, 6))
               + chunk(b"IEND", b""))
        with open(path, "wb") as fh:
            fh.write(png)

    write_png(os.path.join(OUT, "sheet.png"), sheet)
    print("wrote", os.path.join(OUT, "sheet.png"), f"({rows}x{cols} thumbs, 1/s)")


if __name__ == "__main__":
    main()

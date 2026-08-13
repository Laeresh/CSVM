# Dump chosen full-res frames (game region x 640..1919) to ./.scratch/gun-wobble/.
import os
import struct
import sys
import zlib

import imageio_ffmpeg as iio
import numpy as np

# OriginalScreenshots/ is git-ignored, so a worktree has none: CSVM_DATA_ROOT names the tree
# that does (the same env var the engine reads), defaulting to this checkout.
DATA_ROOT = os.environ.get("CSVM_DATA_ROOT") or os.path.dirname(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
VIDEO = os.path.join(DATA_ROOT, "OriginalScreenshots", "Videos", "Gun Wobble and animation.mp4")
OUT = os.path.join(".scratch", "gun-wobble")
X0, X1 = 640, 1920


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


def main():
    wanted = sorted(int(a) for a in sys.argv[1:])
    os.makedirs(OUT, exist_ok=True)
    reader = iio.read_frames(VIDEO)
    meta = next(reader)
    w, h = meta["size"]
    for n, raw in enumerate(reader):
        if not wanted:
            break
        if n == wanted[0]:
            wanted.pop(0)
            frame = np.frombuffer(raw, dtype=np.uint8).reshape(h, w, 3)
            path = os.path.join(OUT, f"f{n:04d}.png")
            write_png(path, frame[:, X0:X1])
            print("wrote", path)


if __name__ == "__main__":
    main()

"""Per-clip sidecar cache: decode a clip ONCE, read it cheaply forever after.

A CAP task's dominant cost is not CPU, it is context. Sessions burned ~20 contact
sheets and stills plus 50-250 line numeric dumps re-deriving what an earlier
session had already measured, because `.scratch/vidcal/cache/` is swept and
nothing decoded ever survived next to the footage.

This writes one sidecar per clip, at `videodata/<repo-relative clip path>.csv`:

    # stamp / digest / shot-index   <- `#` comment block, ~40-60 lines, READ THIS
    pts,alt_ft,mph,heading_deg,...  <- header row
    0.000,4182.3,211.4,...          <- one row per frame, NEVER read wholesale

Two halves with deliberately different standing:

  NUMBERS are measured, machine-produced, and always cover the WHOLE clip
    (`extract.py` reads every frame anyway, so full coverage is free). They may
    be cited as evidence.

  The SHOT-INDEX is prose a previous session wrote. It is NAVIGATIONAL ONLY: it
    says where to look, never what is true. ⚠ A finding in `backlog.md` must
    still cite a frame THIS session opened. The index exists to cut 20 contact
    sheets down to 2-3 targeted stills, not to replace looking.

⚠ Never `cat`/`Read` a whole sidecar. A 35 s VFR clip is 1,000-2,000 rows, which
costs more context than the dumps this replaces. `show` prints the header block;
`slice` and `where` return bounded row sets. That discipline IS the saving.

⚠ Only MEASURED quantities are stored. Climb rate, flight-path angle γ and
sim-seconds are derived on demand, because sim time depends on k = 1.390 ± 0.021
(FINDINGS.md) — itself a measurement. Baking k into 60 files makes every one of
them wrong the day k is revised.

⚠ PowerShell must never touch `videodata/`. These files are git-ignored, so the
repo's mojibake pre-commit hook cannot see them, and PS 5.1 round-trips BOM-less
UTF-8 into garbage (CLAUDE.md). Everything here reads and writes with an explicit
`encoding="utf-8"`, and `show` refuses a file that already looks double-encoded.

Usage (from anywhere in the repo or a worktree):

    python analysis/video-flight-calibration/clipdata.py show   cap14graze
    python analysis/video-flight-calibration/clipdata.py slice  cap14graze --from 7.5 --to 9.0
    python analysis/video-flight-calibration/clipdata.py where  cap14graze "climb_fpm < -3000"
    python analysis/video-flight-calibration/clipdata.py note   cap14graze 7.8 8.4 "wing contact, sparks"
    python analysis/video-flight-calibration/clipdata.py build  cap14graze
    python analysis/video-flight-calibration/clipdata.py prep --hud=chase     # once per HUD
    python analysis/video-flight-calibration/clipdata.py list
"""
import argparse
import hashlib
import os
import subprocess
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import extract as ex          # noqa: E402
import hud as hudlib          # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
SCHEMA = 1

# Sim clock: the original runs fast against wall clock. Applied on demand only.
K_SIM = 1.390

# --- staleness ---------------------------------------------------------------
# Column -> the project files whose CONTENT determines that column's value. A
# fingerprint mismatch marks THAT COLUMN stale and nothing else, so a compass.py
# fix does not invalidate 60 clips' altimeter traces. File contents are hashed
# rather than a git SHA on purpose: an uncommitted edit changes the numbers just
# as much as a committed one does.
#
# `pts` has no entry: presentation timestamps come straight from ffmpeg and no
# project code can make them stale.
_DIAL_CHAIN = ["extract.py", "hud.py", "pool.py", "fitdial.py", "polar.py",
               "reader2.py", "decode.py"]
PRODUCERS = {
    "pts": [],
    "alt_ft": _DIAL_CHAIN + ["run2.py", "run2chase.py", "anchor.py", "shake.py"],
    "mph": _DIAL_CHAIN + ["run2.py", "run2chase.py", "shake.py"],
    "heading_deg": ["extract.py", "hud.py", "compass.py"],
    "alt_q": _DIAL_CHAIN + ["run2.py", "run2chase.py"],
    "mph_q": _DIAL_CHAIN + ["run2.py", "run2chase.py"],
    "shake_dx": ["extract.py", "hud.py", "shake.py", "reader2.py"],
    "shake_dy": ["extract.py", "hud.py", "shake.py", "reader2.py"],
}
# Columns a future session may add to the same file (chasesize.py, ammoarrow.py,
# the CAP-10 audio work). Unknown columns are carried through untouched and
# reported as "producer unknown" rather than dropped or silently trusted.
DERIVED = ("climb_fpm", "gamma_deg", "sim_t")


def _sha(paths):
    h = hashlib.sha1()
    for name in sorted(paths):
        p = os.path.join(HERE, name)
        h.update(name.encode())
        if os.path.exists(p):
            with open(p, "rb") as f:
                h.update(f.read())
        else:
            h.update(b"<missing>")
    return h.hexdigest()[:12]


def fingerprints():
    return {c: _sha(v) for c, v in PRODUCERS.items()}


# Columns whose values depend on the pooled reference and the fitted dial
# geometry, not just on code.
DIAL_COLS = ("alt_ft", "mph", "alt_q", "mph_q")


def artifact_fp(hud):
    """Fingerprint of the CACHE ARTIFACTS every dial reading is differenced
    against: the pooled median and the fitted dial affines.

    ⚠ These determine the numbers as much as the scripts do, and they are NOT
    source files, so `_sha` over PRODUCERS cannot see them. Rebuild the pool from
    a different clip set and every stored `alt_ft` becomes inconsistent with it —
    silently, which is the one failure mode this file exists to prevent.

    Returns None when the artifacts are absent: `.scratch/` is swept, so after a
    sweep the hash cannot be recomputed and the honest answer is "cannot verify",
    not "fresh"."""
    sfx = hudlib.suffix(hud)
    paths = [f".scratch/vidcal/cache/pool_med{sfx}.npy",
             f".scratch/vidcal/cache/dial_affines{sfx}.npy"]
    if not all(os.path.exists(p) for p in paths):
        return None
    h = hashlib.sha1()
    for p in paths:
        with open(p, "rb") as f:
            h.update(f.read())
    return h.hexdigest()[:12]


# --- where things live -------------------------------------------------------
def repo_root():
    """The MAIN checkout's root, from anywhere including a git worktree.

    `--git-common-dir` resolves to the main checkout's `.git` even inside a
    worktree, which is what makes one shared store work from every branch with
    no junction (CLAUDE.md forbids those), no env var, and nothing to unset."""
    d = subprocess.run(["git", "rev-parse", "--git-common-dir"],
                       capture_output=True, text=True, check=True).stdout.strip()
    return os.path.dirname(os.path.abspath(d))


def store():
    return os.path.join(repo_root(), "videodata")


def clip_relpath(name):
    """Clip key or path -> repo-relative video path.

    Mirrors `extract.extract`'s own resolution: a CLIPS value is normally under
    `OriginalScreenshots/Videos`, but a clip still staged in `playtest/<ID>/` is
    given repo-relative instead."""
    if name in ex.CLIPS:
        path, _ = ex.clip_hud(name)
    else:
        path = name
    root = repo_root()
    cand = os.path.join(ex.VID, path)
    if os.path.exists(os.path.join(root, cand)):
        return cand.replace("\\", "/")
    if os.path.exists(os.path.join(root, path)):
        return path.replace("\\", "/")
    if os.path.exists(path):
        return os.path.relpath(os.path.abspath(path), root).replace("\\", "/")
    raise SystemExit(f"no video found for {name!r} (tried {cand!r} and {path!r})")


def sidecar_path(name):
    return os.path.join(store(), *clip_relpath(name).split("/")) + ".csv"


def clip_hud_of(name):
    return ex.clip_hud(name)[1] if name in ex.CLIPS else hudlib.DEFAULT_HUD


# --- the file ----------------------------------------------------------------
MOJI = ("â", "Ã¤", "Ã¶", "Ã¼", "â")


def _check_mojibake(text, path):
    for i, line in enumerate(text.splitlines(), 1):
        if any(m in line for m in MOJI):
            raise SystemExit(
                f"{path}:{i} looks double-encoded (mojibake). Something wrote this "
                f"file without explicit UTF-8 — almost certainly PowerShell. "
                f"Regenerate it with `clipdata.py build`; do not trust its text.")


def read_sidecar(name):
    """-> (header_lines, column_names, rows ndarray). Rows are float, NaN-filled."""
    p = sidecar_path(name)
    if not os.path.exists(p):
        raise SystemExit(f"no sidecar for {name!r} at {p}\n"
                         f"  build it: python {os.path.relpath(__file__)} build {name}")
    with open(p, encoding="utf-8") as f:
        text = f.read()
    _check_mojibake(text, p)
    head, cols, data = [], None, []
    for line in text.splitlines():
        if line.startswith("#"):
            head.append(line)
        elif cols is None:
            cols = line.split(",")
        elif line.strip():
            data.append([float(v) if v else np.nan for v in line.split(",")])
    return head, cols or [], np.array(data, float) if data else np.zeros((0, 0))


def _hdr_get(head, prefix):
    return [h for h in head if h.startswith(prefix)]


def gate_verdict(head):
    for h in _hdr_get(head, "# gate "):
        return h[len("# gate "):].strip()
    return "unknown"


def refuse_if_rejected(head, clip, force):
    """⚠ A REJECT clip's numbers are wrong, not merely noisy. Refusing here rather
    than warning is deliberate: a warning printed above a clean-looking table is
    exactly what gets scrolled past and then cited."""
    g = gate_verdict(head)
    if g.startswith("REJECT") and not force:
        raise SystemExit(
            f"{clip}: REFUSING — this clip failed the checkclip gate.\n"
            f"  {g}\n"
            f"  Auto head turn rotates the camera, so the dials foreshorten in "
            f"opposite directions and translation-only registration absorbs it "
            f"into WRONG needle angles. No downstream care fixes it (FINDINGS.md).\n"
            f"  The clip must be re-recorded with head turn off. Pass --force only "
            f"to inspect the invalid series knowingly; never to quote it.")


def stale_columns(head, cols):
    """Columns that can no longer be trusted at face value.

    Two independent causes, both checked: the producing SCRIPTS changed, or the
    cache ARTIFACTS the dials are read against (pooled median, dial affines)
    changed. The second is not visible in any source file — see `artifact_fp`."""
    now = fingerprints()
    stored = {}
    for h in _hdr_get(head, "# col "):
        parts = h[len("# col "):].split()
        if len(parts) >= 2:
            stored[parts[0]] = parts[-1].replace("fp=", "")
    out = {}
    for c in cols:
        if c not in now:
            out[c] = "producer unknown"
        elif c in stored and stored[c] != now[c]:
            out[c] = f"STALE (written {stored[c]}, now {now[c]})"

    # --- the pooled reference / dial geometry
    hud = next((h.split("hud=")[1].strip() for h in _hdr_get(head, "# clipdata ")
                if "hud=" in h), hudlib.DEFAULT_HUD)
    was = next((h.split("fp=")[1].strip() for h in _hdr_get(head, "# artifacts ")
                if "fp=" in h), None)
    isnow = artifact_fp(hud)
    if was is None:
        note = ("pool/dial fingerprint not recorded (sidecar pre-dates artifact "
                "stamping) — rebuild to make it checkable")
    elif isnow is None:
        note = f"pool/dial artifacts absent (.scratch swept) — cannot verify {was}"
    elif was != isnow:
        note = (f"STALE POOL — the pooled median or dial fit changed "
                f"(written {was}, now {isnow}); re-run build before quoting")
    else:
        note = None
    if note:
        for c in cols:
            if c in DIAL_COLS:
                out[c] = f"{out[c]}; {note}" if c in out else note
    return out


# --- derived, on demand ------------------------------------------------------
def derive(cols, rows, what):
    """Compute a derived series from measured columns. Never stored."""
    idx = {c: i for i, c in enumerate(cols)}
    t = rows[:, idx["pts"]]
    if what == "sim_t":
        return t * K_SIM
    if what == "climb_fpm":
        if "alt_ft" not in idx:
            return np.full(len(rows), np.nan)
        alt = rows[:, idx["alt_ft"]]
        out = np.full(len(rows), np.nan)
        for i in range(len(t)):
            j0 = int(np.searchsorted(t, t[i] - 0.5))
            j1 = int(min(len(t) - 1, np.searchsorted(t, t[i] + 0.5)))
            dt = t[j1] - t[j0]
            if dt > 1e-6:
                out[i] = (alt[j1] - alt[j0]) / dt * 60.0
        return out
    if what == "gamma_deg":
        if "mph" not in idx:
            return np.full(len(rows), np.nan)
        climb = derive(cols, rows, "climb_fpm") / 60.0          # ft/s
        v = rows[:, idx["mph"]] * 5280.0 / 3600.0               # ft/s
        r = np.divide(climb, v, out=np.full(len(v), np.nan), where=np.abs(v) > 1e-3)
        # ⚠ γ saturates at ±90° in a vertical dive (descent rate reaches airspeed);
        # recovery.py gates on |climb/v| < 0.98 for exactly this reason.
        r = np.where(np.abs(r) < 0.98, r, np.nan)
        return np.degrees(np.arcsin(np.clip(r, -1, 1)))
    raise SystemExit(f"unknown derived column {what!r}; have {DERIVED}")


def column(cols, rows, name):
    return rows[:, cols.index(name)] if name in cols else derive(cols, rows, name)


# --- writing -----------------------------------------------------------------
def _branch():
    r = subprocess.run(["git", "rev-parse", "--abbrev-ref", "HEAD"],
                       capture_output=True, text=True)
    return r.stdout.strip() or "?"


def _digest_lines(cols, rows):
    out = []
    t = rows[:, cols.index("pts")]
    for c in cols:
        if c == "pts":
            continue
        v = rows[:, cols.index(c)]
        ok = np.isfinite(v)
        if not ok.any():
            out.append(f"#   {c:12s} all-NaN")
            continue
        i0, i1 = int(np.nanargmin(v)), int(np.nanargmax(v))
        out.append(f"#   {c:12s} min {v[i0]:10.2f} @ {t[i0]:6.2f}s   "
                   f"max {v[i1]:10.2f} @ {t[i1]:6.2f}s   "
                   f"first {v[ok][0]:.2f}  last {v[ok][-1]:.2f}")
    return out


def duration(cols, rows):
    """Wall-clock length, NaN-safe: trailing frames can lack a PTS (see `_pts`)."""
    if not len(rows):
        return 0.0
    t = rows[:, cols.index("pts")]
    return float(np.nanmax(t)) if np.isfinite(t).any() else 0.0


def write_sidecar(name, cols, rows, gate, notes=None, extra=None, pts_short=0):
    p = sidecar_path(name)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    rel = clip_relpath(name)
    src = os.path.join(repo_root(), *rel.split("/"))
    st = os.stat(src)
    fp = fingerprints()
    t = rows[:, cols.index("pts")]
    dur = duration(cols, rows)

    L = [
        f"# clipdata schema={SCHEMA}  clip={name}  hud={clip_hud_of(name)}",
        f"# source {rel}",
        f"# stamp src_bytes={st.st_size} src_mtime={int(st.st_mtime)} "
        f"branch={_branch()}",
        f"# gate {gate}",
    ]
    if pts_short:
        dt = float(np.nanmedian(np.diff(t[np.isfinite(t)]))) if np.isfinite(t).sum() > 2 else 0.0
        L.append(
            f"# ⚠ pts_short={pts_short} frames — ffmpeg showinfo returned "
            f"{len(rows) - pts_short} timestamps for {len(rows)} decoded frames. "
            f"Rows are aligned at the HEAD and the tail is NaN; worst-case timing "
            f"error ~{pts_short * dt:.2f}s. Do not quote a time to better than that.")
    L += [
        "#",
        f"# DIGEST  {len(rows)} frames, {dur:.2f}s wall "
        f"(= {dur * K_SIM:.2f}s sim at k={K_SIM})",
    ]
    L += _digest_lines(cols, rows)
    L += [
        "#",
        "# Derived on demand (NOT stored — see module docstring): "
        + ", ".join(DERIVED),
    ]
    for c in cols:
        if c in fp:
            L.append(f"# col {c} producers={len(PRODUCERS[c])} fp={fp[c]}")
        else:
            L.append(f"# col {c} producers=? fp=unknown")
    afp = artifact_fp(clip_hud_of(name))
    L.append(f"# artifacts pool_med+dial_affines fp={afp or 'absent-at-write'}"
             f"   (governs {', '.join(DIAL_COLS)})")
    L += [
        "#",
        "# SHOT-INDEX — NAVIGATIONAL ONLY. Says where to look, never what is true.",
        "#   A finding must cite a frame opened in the CURRENT session.",
        f"#   Numeric coverage is the whole clip (0.00-{dur:.2f}s), always.",
    ]
    L += (notes if notes is not None else [f"# idx 0.00-{dur:.2f} UNINDEXED"])
    L += ["#"]
    if extra:
        L += extra

    with open(p, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(L) + "\n")
        f.write(",".join(cols) + "\n")
        for r in rows:
            f.write(",".join("" if not np.isfinite(v) else f"{v:.4f}"
                             for v in r) + "\n")
    return p


# --- coverage ----------------------------------------------------------------
def index_lines(head):
    out = []
    for h in _hdr_get(head, "# idx "):
        body = h[len("# idx "):].strip()
        span, _, text = body.partition(" ")
        try:
            a, b = span.split("-")
            out.append((float(a), float(b), text.strip()))
        except ValueError:
            continue
    return sorted(out)


def gaps(head, dur):
    """Spans nobody has looked at. ⚠ A gap is 'nobody looked', NOT 'nothing
    happens' — the whole reason gaps are written down explicitly."""
    covered = [(a, b) for a, b, txt in index_lines(head) if txt != "UNINDEXED"]
    out, cur = [], 0.0
    for a, b in sorted(covered):
        if a > cur + 0.05:
            out.append((cur, a))
        cur = max(cur, b)
    if cur < dur - 0.05:
        out.append((cur, dur))
    return out


# --- decode ------------------------------------------------------------------
def _ensure_cache(short):
    cache = ".scratch/vidcal/cache"
    if not os.path.exists(f"{cache}/{short}_lum.npy"):
        os.makedirs(cache, exist_ok=True)
        path, h = ex.clip_hud(short)
        ex.extract(short, path, h)
    return cache


def _pts(short, n):
    """Presentation timestamps, wall-clock seconds, one per decoded frame.

    ⚠ PTS, never frame/fps — the captures are VFR and individual intervals are
    wrong by up to ±50% (FINDINGS.md's VFR trap).

    ⚠ `showinfo` and `read_frames` do not always agree on the frame count. On
    cap12c4 showinfo reports 3,422 against 3,427 decoded, with NO interior gap
    (largest dt is 1.5x the median) and only ~1 frame of tail — so the shortfall
    is not a dropped span that could be located and repaired. Rather than invent
    an alignment, this aligns at the HEAD, pads the tail with NaN, and returns
    the shortfall so `write_sidecar` can stamp it: a reader is told the worst-case
    timing error instead of being handed a silently shifted clock."""
    import imageio_ffmpeg as iio
    rel = clip_relpath(short)
    out = subprocess.run([iio.get_ffmpeg_exe(), "-i",
                          os.path.join(repo_root(), *rel.split("/")),
                          "-vf", "showinfo", "-f", "null", "-"],
                         stderr=subprocess.PIPE, stdout=subprocess.DEVNULL,
                         text=True).stderr
    t = np.array([float(l.split("pts_time:")[1].split()[0])
                  for l in out.splitlines() if "pts_time:" in l])
    short_by = n - len(t)
    if short_by > 0:
        t = np.concatenate([t, np.full(short_by, np.nan)])
    return t[:n], max(0, short_by)


def build(short, reindex=True):
    """Decode one clip end-to-end and write its sidecar.

    Preserves any existing shot-index (the prose is the expensive half and is
    never machine-reproducible), while every number is re-measured."""
    hud = clip_hud_of(short)
    _ensure_cache(short)
    cols, series, gate = ["pts"], [], "not run"

    if hud == "chase":
        import run2chase
        _, alt, mph = run2chase.decode(short)
        # hud.py: the chase HUD is a screen-space sprite, pinned by construction,
        # so checkclip's head-turn gate has nothing to test. Say so; do not pass quietly.
        gate = "N/A — chase HUD is a pinned sprite, no head-turn test applies"
        n = len(alt)
        cols += ["alt_ft", "mph"]
        series += [alt, mph]
    else:
        import checkclip
        import shake
        import run2
        import anchor
        from reader2 import Dial, LKRegistrar, circ_centroid, DEG
        cache = ".scratch/vidcal/cache"
        if not os.path.exists(f"{cache}/{short}_shift.npy"):
            shake.track(short)
        ok = checkclip.report(short)
        gate = ("OK — rigid, translation registration valid" if ok else
                "REJECT — consistent with auto head turn; DO NOT DECODE, re-record")
        if not ok:
            # ⚠ Write NO dial columns for a rejected clip. FINDINGS.md: a REJECT on
            # anti-correlated dx means the camera rotated, the dials foreshorten in
            # opposite directions, and the translation-only registration silently
            # absorbs that into wrong needle angles - "no amount of care downstream
            # fixes it". Storing those numbers anyway would leave a file full of
            # plausible, citable, wrong altitudes behind a header line someone can
            # skip. The gate verdict IS the finding; it is what the sidecar keeps.
            print(f"  gate REJECT -> storing verdict only, no dial columns")
            t, pts_short = _pts(short, len(np.load(f"{cache}/{short}_lum.npy",
                                                   mmap_mode="r")))
            rows = np.column_stack([t])
            p = write_sidecar(short, ["pts"], rows, gate, None, pts_short=pts_short)
            print(f"  wrote {os.path.relpath(p, repo_root())}  gate: REJECT")
            return p
        P = np.load(f"{cache}/dial_affines.npy")
        dA, dS = Dial(P[0], run2.ALT_BANDS), Dial(P[1], run2.SPD_BANDS)
        rA, rS = LKRegistrar(P[0]), LKRegistrar(P[1])
        a = np.load(f"{cache}/{short}_lum.npy")
        g0 = np.load(f"{cache}/{short}_shift.npy")
        n = len(a)
        rec = np.zeros((n, 8), np.float32)
        for i in range(n):
            dxa, dya = rA.shift(a[i], float(g0[i, 0]), float(g0[i, 1]))
            dxs, dys = rS.shift(a[i], float(g0[i, 0]), float(g0[i, 1]))
            pa, ps = dA.profiles(a[i], dxa, dya), dS.profiles(a[i], dxs, dys)
            pl = pa["long"]
            cl, sl = circ_centroid(pl, int(np.argmax(pl)))
            psh = pa["short"].copy()
            psh[np.arange(int(round(cl)) - 16, int(round(cl)) + 17) % len(psh)] = -1e9
            cs, ss = circ_centroid(psh, int(np.argmax(psh)), half=12)
            pn = ps["needle"]
            cn, sn = circ_centroid(pn, int(np.argmax(pn)))
            rec[i] = (cl * DEG, cs * DEG, cn * DEG, sl, ss, sn, dxa, dya)
        np.save(f"{cache}/{short}_dials2.npy", rec)
        mph = run2.unwrap(rec[:, 2]) / 0.72
        alt = anchor.resolve(short, verbose=False)
        cols += ["alt_ft", "mph", "alt_q", "mph_q", "shake_dx", "shake_dy"]
        series += [alt, mph, rec[:, 3], rec[:, 5], rec[:, 6], rec[:, 7]]
        try:
            import compass
            compass.track(short, verbose=False)
            cols.append("heading_deg")
            series.append(compass.heading(short))
        except Exception as e:                       # noqa: BLE001
            print(f"  heading: not available ({type(e).__name__}: {e})")

    t, pts_short = _pts(short, n)
    rows = np.column_stack([t] + [np.asarray(s, float)[:n] for s in series])

    notes = None
    if reindex and os.path.exists(sidecar_path(short)):
        old, _, _ = read_sidecar(short)
        # Real observations survive a re-decode (prose is the expensive half and
        # cannot be re-derived); the UNINDEXED placeholder must not, or a stale
        # span from an earlier duration is carried forward as if it were coverage.
        keep = [h for h in _hdr_get(old, "# idx ") if "UNINDEXED" not in h]
        notes = keep or None
    p = write_sidecar(short, cols, rows, gate, notes, pts_short=pts_short)
    print(f"  wrote {os.path.relpath(p, repo_root())}  "
          f"{len(rows)} rows x {len(cols)} cols   gate: {gate.split(chr(8212))[0].strip()}")
    return p


# --- commands ----------------------------------------------------------------
def cmd_show(a):
    head, cols, rows = read_sidecar(a.clip)
    print("\n".join(head))
    bad = stale_columns(head, cols)
    if bad:
        print("# ⚠ STALENESS")
        for c, why in sorted(bad.items()):
            print(f"#   {c}: {why}")
    dur = duration(cols, rows)
    g = gaps(head, dur)
    if g:
        print("# ⚠ UNINDEXED spans (nobody looked — NOT 'nothing happens'):")
        for x, y in g:
            print(f"#   {x:6.2f}-{y:6.2f}s")


def cmd_slice(a):
    head, cols, rows = read_sidecar(a.clip)
    refuse_if_rejected(head, a.clip, a.force)
    t = rows[:, cols.index("pts")]
    m = (t >= a.start) & (t <= a.end)
    want = a.cols.split(",") if a.cols else cols + ["climb_fpm"]
    data = {c: column(cols, rows, c) for c in want}
    idx = np.where(m)[0][:: max(1, a.every)]
    if len(idx) > a.limit:
        print(f"# {len(idx)} rows exceeds --limit {a.limit}; raise --every")
        idx = idx[: a.limit]
    print(",".join(want))
    for i in idx:
        print(",".join("" if not np.isfinite(data[c][i]) else f"{data[c][i]:.3f}"
                       for c in want))


def cmd_where(a):
    head, cols, rows = read_sidecar(a.clip)
    refuse_if_rejected(head, a.clip, a.force)
    env = {c: column(cols, rows, c) for c in set(cols) | set(DERIVED)}
    env["np"] = np
    m = np.asarray(eval(a.expr, {"__builtins__": {}}, env), bool)  # noqa: S307
    idx = np.where(m)[0]
    print(f"# {a.expr!r}: {len(idx)}/{len(rows)} frames")
    want = a.cols.split(",") if a.cols else ["pts", "alt_ft", "mph", "climb_fpm"]
    want = [c for c in want if c in env]
    print(",".join(want))
    for i in idx[: a.limit]:
        print(",".join("" if not np.isfinite(env[c][i]) else f"{env[c][i]:.3f}"
                       for c in want))
    if len(idx) > a.limit:
        print(f"# ... {len(idx) - a.limit} more (raise --limit)")


def cmd_note(a):
    head, cols, rows = read_sidecar(a.clip)
    p = sidecar_path(a.clip)
    with open(p, encoding="utf-8") as f:
        text = f.read()
    line = f"# idx {a.start:.2f}-{a.end:.2f} [{_branch()}] {a.text}"
    out, placed = [], False
    for ln in text.splitlines():
        if ln.startswith("# idx ") and "UNINDEXED" in ln and not placed:
            out.append(line)
            placed = True
            continue
        if ln.startswith("# idx ") and not placed:
            out.append(ln)
            continue
        if not placed and not ln.startswith("#"):
            out.append(line)
            placed = True
        out.append(ln)
    with open(p, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out) + "\n")
    print(f"noted: {line}")
    dur = duration(cols, rows)
    g = gaps([h for h in out if h.startswith("#")], dur)
    print(f"still unindexed: {', '.join(f'{x:.1f}-{y:.1f}s' for x, y in g) or 'nothing'}")


def prune(short):
    """Drop the big regenerable intermediates for one clip.

    `<clip>_lum.npy` runs ~10x the source video (1.33 GB for a 126 MB clip), and
    `.scratch/` is swept anyway — the sidecar is the durable artifact. Pruning as
    we go keeps a 60-clip backfill to roughly one clip's footprint at a time.
    ⚠ Never prune a clip in `pool.POOLS`: the pooled median is rebuilt from those."""
    import pool
    if any(short in v for v in pool.POOLS.values()):
        return 0
    freed = 0
    for suffix in ("_lum.npy", "_shortprof.npy", "_ref.npy"):
        p = f".scratch/vidcal/cache/{short}{suffix}"
        if os.path.exists(p):
            freed += os.path.getsize(p)
            os.remove(p)
    return freed


def cmd_build(a):
    names = a.clips or sorted(ex.CLIPS)
    ok = fail = 0
    for s in names:
        print(f"--- {s}", flush=True)
        try:
            build(s)
            ok += 1
        except SystemExit as e:
            print(f"  SKIPPED: {e}")
            fail += 1
        except Exception as e:                       # noqa: BLE001
            print(f"  FAILED: {type(e).__name__}: {e}")
            fail += 1
        if a.prune:
            freed = prune(s)
            if freed:
                print(f"  pruned {freed / 1e9:.2f} GB of intermediates")
    print(f"\n{ok} built, {fail} skipped/failed, of {len(names)}")


def cmd_prep(a):
    import pool
    import fitdial                                    # noqa: F401
    print(f"pooling {a.hud} ...")
    pool.build(a.hud)
    print(f"fit dials for {a.hud}: run\n"
          f"  python {os.path.relpath(__file__)} --hud={a.hud}  (see fitdial.py)")


def cmd_list(a):
    root = store()
    if not os.path.isdir(root):
        print(f"no store yet at {root}")
        return
    for dirpath, _, files in os.walk(root):
        for fn in sorted(files):
            if fn.endswith(".csv"):
                p = os.path.join(dirpath, fn)
                print(os.path.relpath(p, root).replace("\\", "/"))


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)

    s = sub.add_parser("show", help="print the header block (the cheap path)")
    s.add_argument("clip")
    s.set_defaults(fn=cmd_show)

    s = sub.add_parser("slice", help="bounded rows over a time window")
    s.add_argument("clip")
    s.add_argument("--from", dest="start", type=float, default=0.0)
    s.add_argument("--to", dest="end", type=float, default=1e9)
    s.add_argument("--every", type=int, default=1)
    s.add_argument("--cols", default="")
    s.add_argument("--limit", type=int, default=60)
    s.add_argument("--force", action="store_true",
                   help="read a gate-REJECTed clip's invalid series anyway")
    s.set_defaults(fn=cmd_slice)

    s = sub.add_parser("where", help="frames matching an expression over columns")
    s.add_argument("clip")
    s.add_argument("expr")
    s.add_argument("--cols", default="")
    s.add_argument("--limit", type=int, default=40)
    s.add_argument("--force", action="store_true",
                   help="read a gate-REJECTed clip's invalid series anyway")
    s.set_defaults(fn=cmd_where)

    s = sub.add_parser("note", help="append a NAVIGATIONAL shot-index line")
    s.add_argument("clip")
    s.add_argument("start", type=float)
    s.add_argument("end", type=float)
    s.add_argument("text")
    s.set_defaults(fn=cmd_note)

    s = sub.add_parser("build", help="decode clip(s) and write sidecar(s)")
    s.add_argument("clips", nargs="*")
    s.add_argument("--prune", action="store_true",
                   help="delete the big regenerable .npy intermediates after each "
                        "clip (for a backfill; pool clips are never pruned)")
    s.set_defaults(fn=cmd_build)

    s = sub.add_parser("prep", help="pooled median for a HUD (once per HUD)")
    s.add_argument("--hud", default=hudlib.DEFAULT_HUD)
    s.set_defaults(fn=cmd_prep)

    s = sub.add_parser("list", help="clips that have a sidecar")
    s.set_defaults(fn=cmd_list)

    a = ap.parse_args()
    a.fn(a)


if __name__ == "__main__":
    main()

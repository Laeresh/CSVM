"""Reads a host and a guest --debug-net-trace log and prints three measurements of a real link:
the guest clock slew, the match-state tick arrivals at the guest, and the position error measured
the net-soak way (owner's own path against the far copy, a fitted lag removed).
Usage: analyze.py HOST_LOG GUEST_LOG [--wall-offset=S] [--skip=S]

--wall-offset is guest wall minus host wall in seconds (the two machines' system clocks), used
only for the guest-clock truth check; the position fit absorbs any constant offset in its lag.
--skip drops that many seconds after the match start (the guest's opening alignment).

Rig: run-pair.ps1 (a Linux host headless on its LAN address through deck-host.sh, this PC joining
on the hidden desktop), clock-offset.ps1 before and after for --wall-offset. The game has no delay
or loss shaping on a real socket, so a run measures the link as it is.

Method and floor: each machine's steps are placed on its own wall clock by the lower envelope of
(wall - step/60) over a 4 s window, since steps run late in catch-up bursts and never early. What
is left of that lateness reads as position error, so a pair of processes on one PC over 127.0.0.1
is the control: on a quiet LAN it reads the same windowed error as the link. The position error is
fitted per 240-step window, as the soak fits one lag per cell; two machines' system clocks drift
apart by a millisecond or more a minute, which the per-window fit absorbs and the whole-run fit
does not. Placements (a respawn) are cut out with three seconds either side.
"""
import re
import sys
import numpy as np

DT = 1.0 / 60.0
TRACE = re.compile(r"net trace (host|guest) step (\d+) wall ([\d.]+) sim ([\d.]+) remain (-?[\d.]+)(.*)$")
CLOCK = re.compile(r"offset (-?[\d.]+) target (-?[\d.]+) snaps (\d+) rtt ([\d.]+) trips (\d+) asked (\d+)")
SEAT = re.compile(r"seat (\d+) (own|copy) (-?[\d.]+) (-?[\d.]+) (-?[\d.]+)")
READOUT = re.compile(r"net (host|guest) seat \d+ peers \d+: sent (\d+) recv (\d+).*?dropped state (\d+) fire (\d+).*?poses interp (\d+) extrap (\d+) starved (\d+) jumps (\d+)")


def load(path):
    rows = []
    readouts = []
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            m = TRACE.search(line)
            if m:
                rest = m.group(6)
                c = CLOCK.search(rest)
                seats = {int(s[0]): (s[1], float(s[2]), float(s[3]), float(s[4])) for s in SEAT.findall(rest)}
                rows.append(dict(step=int(m.group(2)), wall=float(m.group(3)), sim=float(m.group(4)),
                                 remain=float(m.group(5)),
                                 clock=tuple(float(x) for x in c.groups()) if c else None, seats=seats))
                continue
            r = READOUT.search(line)
            if r:
                readouts.append(tuple(int(x) for x in r.groups()[1:]))
    return rows, readouts


def ideal_times(rows, window=120):
    """Each step's ideal wall time: steps run late in catch-up bursts, never early, so the lower
    envelope of wall - step*dt over a sliding window is the schedule the steps stand for."""
    steps = np.array([r["step"] for r in rows], dtype=float)
    wall = np.array([r["wall"] for r in rows])
    e = wall - steps * DT
    n = len(e)
    env = np.empty(n)
    for i in range(n):
        lo, hi = max(0, i - window), min(n, i + window + 1)
        env[i] = e[lo:hi].min()
    return steps, wall, env + steps * DT, e - env


def segments_ok(pos, jump=40.0):
    """A mask clearing three seconds either side of any one-step move longer than `jump` metres
    (a respawn or a crash placement), which is a placement the wire carried, not tracking."""
    d = np.linalg.norm(np.diff(pos, axis=0), axis=1)
    bad = np.zeros(len(pos), dtype=bool)
    for i in np.nonzero(d > jump)[0]:
        bad[max(0, i - 180): i + 181] = True
    return ~bad


def track(own_t, own_p, shown_t, shown_p, lags):
    """The soak's measure: the lag that minimises the mean distance between the shown path and
    the owner's path read that much earlier, and the mean and worst distance at it."""
    best = (None, np.inf, np.inf)
    for lag in lags:
        q = shown_t - lag
        ok = (q >= own_t[0]) & (q <= own_t[-1])
        if ok.sum() < 30:
            continue
        ref = np.stack([np.interp(q[ok], own_t, own_p[:, k]) for k in range(3)], axis=1)
        d = np.linalg.norm(shown_p[ok] - ref, axis=1)
        if d.mean() < best[1]:
            best = (lag, d.mean(), d.max())
    return best


def path(rows, t, seat, kind):
    sel = [i for i, r in enumerate(rows) if seat in r["seats"] and r["seats"][seat][0] == kind]
    p = np.array([rows[i]["seats"][seat][1:] for i in sel])
    return t[sel], p


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    opts = dict(a[2:].split("=", 1) for a in sys.argv[1:] if a.startswith("--"))
    wall_offset = float(opts.get("wall-offset", "0"))
    skip = float(opts.get("skip", "5"))
    host, host_ro = load(args[0])
    guest, guest_ro = load(args[1])
    hs, hw, ht, hlate = ideal_times(host)
    gs, gw, gt, glate = ideal_times(guest)
    gt_host = gt - wall_offset  # guest ideal times on the host's wall axis

    print(f"host steps {len(host)} over {hw[-1]-hw[0]:.1f} s wall, pace {len(host)*DT/(hw[-1]-hw[0]):.4f} sim s per wall s, "
          f"steps late (median/p99/max) {np.median(hlate)*1000:.1f}/{np.percentile(hlate,99)*1000:.1f}/{hlate.max()*1000:.1f} ms")
    print(f"guest steps {len(guest)} over {gw[-1]-gw[0]:.1f} s wall, pace {len(guest)*DT/(gw[-1]-gw[0]):.4f} sim s per wall s, "
          f"steps late (median/p99/max) {np.median(glate)*1000:.1f}/{np.percentile(glate,99)*1000:.1f}/{glate.max()*1000:.1f} ms")
    for name, ro in (("host", host_ro), ("guest", guest_ro)):
        if ro:
            last = ro[-1]
            print(f"{name} readout (last): sent {last[0]} recv {last[1]} dropped state {last[2]} fire {last[3]} "
                  f"interp {last[4]} extrap {last[5]} starved {last[6]} jumps {last[7]}")

    # ---- BL-1018: the guest clock slew -------------------------------------------------------
    print("\n== BL-1018 guest clock slew")
    clk = np.array([r["clock"] for r in guest])
    off, tgt, snaps, rtt, trips, asked = clk.T
    t0 = gw[0]
    print(f"snaps {int(snaps[-1])} (first step {int(snaps[0])}), round trips taken {int(trips[-1])} of {int(asked[-1])} asked "
          f"-> unanswered {int(asked[-1]-trips[-1])} ({(asked[-1]-trips[-1])/max(1,asked[-1])*100:.1f} %)")
    rtts = []
    for i in range(1, len(trips)):
        if trips[i] != trips[i - 1]:
            rtts.append(rtt[i])
    rtts = np.array(rtts)
    if len(rtts):
        print(f"round trip ms: n {len(rtts)} min {rtts.min()*1000:.1f} median {np.median(rtts)*1000:.1f} "
              f"p95 {np.percentile(rtts,95)*1000:.1f} max {rtts.max()*1000:.1f}")
    # Every reading: the target changes. Its size is the correction asked for.
    jumps, walks = [], []
    idx = [i for i in range(1, len(tgt)) if tgt[i] != tgt[i - 1]]
    for k, i in enumerate(idx):
        if gw[i] - t0 < skip:
            continue
        err = tgt[i] - off[i - 1]
        jumps.append(err)
        end = idx[k + 1] if k + 1 < len(idx) else len(tgt)
        settled = next((j for j in range(i, end) if abs(tgt[j] - off[j]) <= 0.001), None)
        walks.append((gw[settled] - gw[i]) if settled is not None else np.nan)
    jumps = np.array(jumps)
    walks = np.array(walks)
    if len(jumps):
        print(f"readings after {skip:.0f} s: {len(jumps)}; correction asked |target - offset| ms: median {np.median(np.abs(jumps))*1000:.2f} "
              f"p95 {np.percentile(np.abs(jumps),95)*1000:.2f} max {np.abs(jumps).max()*1000:.2f}; over 5 s: {(np.abs(jumps) > 5).sum()}")
        w = walks[~np.isnan(walks)]
        print(f"walk to settled (wall s): settled before the next reading {len(w)} of {len(walks)}; median {np.median(w) if len(w) else float('nan'):.3f} "
              f"max {w.max() if len(w) else float('nan'):.3f}")
    after = gw - t0 >= skip
    print(f"target spread after {skip:.0f} s (ms): std {np.std(tgt[after])*1000:.2f} min..max {(tgt[after].min()-np.median(tgt[after]))*1000:.2f}..{(tgt[after].max()-np.median(tgt[after]))*1000:.2f} about median {np.median(tgt[after]):.4f}")
    print(f"offset in use after {skip:.0f} s (ms about its median): std {np.std(off[after])*1000:.2f} range {(off[after].max()-off[after].min())*1000:.2f}")
    # Truth: the host's sim clock at the guest step's wall moment, against guest sim + offset.
    hsim = np.array([r["sim"] for r in host])
    gsim = np.array([r["sim"] for r in guest])
    truth = np.interp(gw - wall_offset, hw, hsim, left=np.nan, right=np.nan)
    err = gsim + off - truth
    ok = after & ~np.isnan(err)
    if ok.any():
        print(f"guest host-time estimate minus host sim clock at the same wall instant (ms; includes both machines' frame-clock "
              f"phase and the system clock offset {wall_offset*1000:.1f} ms): median {np.median(err[ok])*1000:.1f} "
              f"p5..p95 {np.percentile(err[ok],5)*1000:.1f}..{np.percentile(err[ok],95)*1000:.1f}")

    # ---- BL-1025: the match-state tick at the guest -------------------------------------------
    print("\n== BL-1025 match-state ticks at the guest")
    remain = np.array([r["remain"] for r in guest])
    arr = [i for i in range(1, len(remain)) if remain[i] != remain[i - 1] and gw[i] - t0 >= skip]
    aw = gw[arr]
    sp = np.diff(aw)
    if len(sp):
        print(f"arrivals {len(arr)}; spacing s: mean {sp.mean():.4f} std {sp.std()*1000:.1f} ms min {sp.min():.3f} max {sp.max():.3f}; "
              f"p1 {np.percentile(sp,1):.3f} p99 {np.percentile(sp,99):.3f}; under 0.5 s {(sp < 0.5).sum()}, over 1.5 s {(sp > 1.5).sum()}")
        drops = -np.diff(remain[arr])
        print(f"remaining per arrival: mean drop {drops.mean():.4f} s, drops not 1.0 s: {(np.abs(drops - 1.0) > 0.01).sum()}")
        shown = np.ceil(remain[arr])
        steps = -np.diff(shown)
        print(f"HUD whole seconds per arrival (ceil): steps of 1 {(steps == 1).sum()}, of 2+ {(steps >= 2).sum()}, of 0 {(steps == 0).sum()}")
        # The HUD readout's own change times: a change held longer than 1.5 s is a stall, two
        # changes under 0.5 s apart read as a jump.
        print(f"readout held over 1.5 s: {(sp > 1.5).sum()}, two changes within 0.5 s: {(sp < 0.5).sum()}; longest hold {sp.max():.3f} s")
    hremain = np.array([r["remain"] for r in host])

    # ---- BL-1041: position error, the soak's way ---------------------------------------------
    print("\n== BL-1041 position error (lag fitted, error after it)")
    lags = np.arange(-0.5, 1.0, DT / 20)
    host_own_seat = next(s for s, v in host[0]["seats"].items() if v[0] == "own")
    guest_own_seat = next(s for s, v in guest[0]["seats"].items() if v[0] == "own")
    pairs = (("guest-shown host aeroplane", ht, host, host_own_seat, gt_host, guest),
             ("host-shown guest aeroplane", gt_host, guest, guest_own_seat, ht, host))
    for name, own_t_all, own_rows, seat, shown_t_all, shown_rows in pairs:
        own_t, own_p = path(own_rows, own_t_all, seat, "own")
        sh_t, sh_p = path(shown_rows, shown_t_all, seat, "copy")
        m_own = segments_ok(own_p)
        m_sh = segments_ok(sh_p)
        # Drop the shown samples whose own-path neighbourhood holds a placement.
        bad_t = own_t[~m_own]
        keep = m_sh & (sh_t - sh_t[0] >= skip)
        if len(bad_t):
            near = np.min(np.abs(sh_t[:, None] - bad_t[None, :]), axis=1) < 1.0
            keep &= ~near
        lag, mean, worst = track(own_t, own_p, sh_t[keep], sh_p[keep], lags)
        speed = np.median(np.linalg.norm(np.diff(own_p, axis=0), axis=1)) / DT
        print(f"{name}: whole run, {keep.sum()} steps: lag {lag*1000:.0f} ms, mean {mean:.2f} m, worst {worst:.2f} m (median speed {speed:.0f} m/s)")
        # The soak's own window: 240 steps, its own fitted lag each.
        win = []
        for s0 in range(0, keep.sum() - 240, 240):
            ids = np.nonzero(keep)[0][s0:s0 + 240]
            if sh_t[ids[-1]] - sh_t[ids[0]] > 240 * DT * 1.5:
                continue
            lag_w, mean_w, worst_w = track(own_t, own_p, sh_t[ids], sh_p[ids], lags)
            if lag_w is not None:
                win.append((lag_w, mean_w, worst_w))
        if win:
            a = np.array(win)
            print(f"   240-step windows {len(a)}: mean error median {np.median(a[:,1]):.2f} p95 {np.percentile(a[:,1],95):.2f} max {a[:,1].max():.2f} m; "
                  f"worst error median {np.median(a[:,2]):.2f} p95 {np.percentile(a[:,2],95):.2f} max {a[:,2].max():.2f} m; "
                  f"lag median {np.median(a[:,0])*1000:.0f} ms range {a[:,0].min()*1000:.0f}..{a[:,0].max()*1000:.0f} ms")


if __name__ == "__main__":
    main()

# The 4P cost of a heavy campaign mission (D33)

Part of `PLAN-campaign-coop.md`'s D33: the frame cost and hitch behaviour of four panes over a
full campaign mission, measured rather than guessed. `BL-434` already flagged per-viewport
cockpit interior cost at four players as unprofiled; this is that measurement, for the mission
data itself picks out as the heaviest one shipped.

## Which mission

`roster_census.py` reads every one of the 24 campaign missions' `aiv`/`egen`/`zeppelins.zrd.json`
files (`extracted/<chapter>/<mission>/zrdr/`) and counts the at-mission-start roster (enabled,
non-`player` `aiv` blocks), generator count and zeppelin count per mission. Top of 24 by
roster + generators + zeppelins: `CM10` (C1/M05, 23+0+1=24), `CM22` (C5/M02, 23+0+1=24), `CM24`
(C5/M04, 22+1+1=24), `CM18` (C4/M03, 21+1+1=23), `CM09` (C1/M04, 19+1+1=21).

Draw-call weight is not mission-specific (it is mostly the chapter's built world, clutter and
animation, which `CM18` shares with every other C4 mission), but it is already measured:
`analysis/perf/scenarios.json`'s `c4-terrain` scenario is the heaviest draw-call pose of the six
scenarios that suite runs (~2180 calls, against C1/C2B/C5's poses), so C4 is the heaviest chapter
world on the record. `CM18` (C4/M03, "Deceit at Devil's Horn", `seq` 17) is the largest roster of
the five C4 missions (21 roster + 1 generator + 1 zeppelin = 23, one shy of the campaign-wide max
of 24), so it is this item's pick: the heaviest chapter world carrying the heaviest roster that
chapter ships. **Confidence: lead-only, same as D33's own Evidence line.** No other chapter's
draw-call pose was measured against C4's at a matching pose, so this is the best data available,
not a chapter-by-chapter draw census.

## Method

A `--campaign=` launch with no profile on disk flies without a mission director at all (no
roster, no objectives, no generators; `CampaignDirector.TryCreate` warns `no such profile --
flying without a mission` and returns null), unlike `D32`'s goldens, which deliberately use that
path to get a plain `StartGrid` placement with no director. Measuring the actual mission load
needs a real profile, so `measure.ps1` writes one to `user://Profiles/d33-perf/` (this machine's
own Godot userdata directory, never a repo file) with `missionsCompleted: 17` so `CM18` (`seq`
17) is reachable, and leaves the user's own profiles there untouched.

Four configs, each 3 launches of 600 sim frames (`--det --perf --no-vsync --mute --frames=600`),
following the perf stage's own protocol (`docs/tooling.md` "The perf stage"): the first launch is
a discarded cold-cache warmup (PERF-7), each kept launch's first `[perf] window` is dropped
(shader compilation), and the report is the median over the remaining 18 windows (2 launches x
9 windows):

```
--campaign=d33-perf:17 --players=1 --plane=player_bhawk --det --perf --no-vsync --mute --frames=600 --screenshot=<path>
--campaign=d33-perf:17 --players=4 --plane=player_bhawk,player_bhawk,player_bhawk,player_bhawk --det --perf --no-vsync --mute --frames=600 --screenshot=<path>
```

Each pair is also run with `--view=cockpit` appended for the cockpit-view configs. Natural
(non-injected) hitches are read from `HitchMonitor`'s own always-on sidecar
(`<log>.hitches.jsonl`, recovered from each run's `[core] log file=...` line), not from
`--hitch-inject=`: that flag proves the monitor's own wiring (`RunTests.ps1 -Hitch`'s job), it
does not simulate this mission's real load.

Run: `$env:CSVM_DATA_ROOT = "Z:\CSVM"; .\analysis\campaign-coop-4p-perf\measure.ps1` from the
repo root (a worktree has no `tools/godot` of its own, per `docs/verification.md` LOG-17).

## Results

Median over 18 kept windows per config (render_cpu/gpu/frame in ms; draws/nodes are exact counts,
PERF's sharp instrument):

| config | render_cpu_ms | gpu_ms | draws | nodes | frame_ms | max_ms | p95_ms |
|---|---:|---:|---:|---:|---:|---:|---:|
| 1p-external | 0.710 | 0.995 | 697 | 39,673 | 11.89 | 12.28 | 12.02 |
| 4p-external | 0.685 | 1.005 | 3,972 | 45,949 | 19.27 | 30.70 | 20.22 |
| 1p-cockpit  | 0.580 | 1.135 | 733 | 39,673 | 12.39 | 12.92 | 12.50 |
| 4p-cockpit  | 0.685 | 0.725 | 4,169 | 45,949 | 21.23 | 26.92 | 21.97 |

**1P to 4P (external view).** Draws grow 5.7x (697 to 3,972) against a 4x pane count, nodes grow
1.16x, `frame_ms` grows 1.6x, and `render_cpu_ms`/`gpu_ms` stay flat, both sitting under the
scenarios.json same-build noise floor (0.25 ms / 15%, `docs/tooling.md`), so neither reads as a
real per-pane render-thread cost at this pose. The draws figure is the one that survives: it is an
exact count, not a noisy millisecond term, and it grows faster than the pane count, meaning
whatever the four panes draw between them is not simply "the same scene four times", worth
isolating (folded into `BL-434`'s still-open per-viewport question) rather than asserted here as a
cause.

**Cockpit vs. external, same player count.** +5% draws at both 1P (697 to 733) and 4P (3,972 to
4,169), and 0.4-2 ms of `frame_ms` movement that is inside or just past the same noise floor. The
per-pilot interior subtree is a small, roughly per-pane-proportional add at this pose, not the
dominant term next to the 1P-to-4P jump above.

**`max_ms`/`p95_ms` at 4P (external) is the standout.** 30.70 ms / 20.22 ms against 12.28 ms /
12.02 ms at 1P, the worst single frame in a window and the 95th percentile, both roughly double
to 2.5x. Read with `docs/verification.md`'s own caveat (a median of per-window extremes, small
population, no meaningful ratio) rather than as a precise multiplier, but the direction is
consistent across both external and cockpit view and both kept launches.

## The natural hitch: `ai_spawn`, ~300 ms, independent of player count

Every one of the four configs' kept launches tripped `HitchMonitor` at the same two `--det` sim
frames, 241 and 482, for 288-330 ms each time, with `HitchSidecar`'s sample attribution assigning
essentially the whole frame to one `ai_spawn` site (`attributed_ms` within ~20 ms of `frame_ms`
both times, well past the ~46 ms trip threshold). The magnitude and the frame numbers do not move
between 1P and 4P: this is a single mission-script/generator spawn cost on the main thread, not
something splitscreen's pane count multiplies. Filed as `BL-641` with the full per-hitch numbers,
since this is squarely what D33's "if the readings are bad, the outcome is a new backlog.md
entry" approach calls for, and it is not what BL-434 or this item set out to measure.

## Verdict

The measured cost of going from one pane to four on the heaviest shipped campaign mission is
real but modest at the render level (draws grow faster than the pane count, worst-frame time
roughly doubles), and the single largest frame-time event in this mission by a wide margin, the
~300 ms `ai_spawn` stall, is not a splitscreen cost at all. Nothing here rises to "fix inside
this plan" per D33's own approach; `BL-434` keeps the still-open per-viewport isolation question
and `BL-641` carries the new hitch finding forward.

## Evidence & limits

Machine-specific (this dev box, one GPU, one measurement session), the same caveat every reading
in `analysis/perf/scenarios.json` carries. `gpu_ms`/`render_cpu_ms` deltas reported here are below
or at the edge of that same-build noise floor and are stated as such, not as verdicts. The
chapter-vs-chapter draw-count claim (C4 heaviest) rests on the existing `c4-terrain` scenario
measurement against C1/C2B/C5, not a fresh 8-chapter sweep: C1B, C1C and C3 were never perf-tested
at a comparable pose, so "heaviest shipped campaign mission" is the best mission this data
supports, not a global maximum. Raw logs, per-launch hitch sidecars and `results.json` are under
`.scratch/` (git-ignored); this file and `roster_census.py`/`measure.ps1` are the durable record.

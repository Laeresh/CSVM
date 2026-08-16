# AI aircraft flying into each other: what actually causes it

**Question.** Enemy and wingman AI collide with each other repeatedly in a squadron dogfight.
Which part of the avoid-crash chain is failing: does the probe not see other aeroplanes, is its
geometry too thin, or is the response wrong?

**Verdict.** The response. The probe sees other aeroplanes perfectly well and widening it changes
nothing; what costs the collisions is that both parties answer a detection with the *same*
straight-ahead climb and so pull up along converging tracks. Displacing that climb-out to the
right of each aeroplane's own ground track cut mid-airs **41 %**, p about 0.014.

The decode this sits on is [`docs/org/aiPilot.md`](../../docs/org/aiPilot.md), which had already
named all three candidate bounds; this directory is which of them binds.

## The instrument

`dogfight-trials.ps1` runs `dogfight-5v5.json` N times and tallies the collisions. One run is a
C1 Instant Action squadron dogfight, five Bloodhawk wingmen against five Bloodhawk wave enemies,
every human pinned inert by `--debug-spectate`, gunnery on at skill 5, 9000 sim frames (about
150 s) ended by the screenshot save.

It reads two log lines that exist for this purpose and were added alongside it:

- the avoid-crash transition now names what the probe struck, so
  `obstacle inside 380 m (ai4_player_bhawk/airframe)` is distinguishable from `(a5/col)`;
- a crash into another aircraft prints `midair aspect`, carrying the angle between the two
  velocity vectors, the line of sight off own track, and both speeds.

## The three arms

Each row is a multi-run mean. Arms two and three differ from their controls in one constant, so
nothing else about the build could confound them.

| # | Change under test | runs | mid-airs / run | arms / run | arms naming an aircraft / run |
|---|---|---|---|---|---|
| 1 | the decoded zero-width ray (control) | 8 | 3.63 | 31.0 | 13.8 |
| 1 | a swept 10 m sphere | 6 | 3.50 | 79.7 | 66.7 |
| 2 | climb-out straight up (the original's) | 8 | 3.75 | | |
| 2 | climb-out 1000 m up **and 1000 m right of own track** | 14 | **2.21** | | |

**Arm 1, the probe geometry: no effect.** MEASURED. The sweep finds another aeroplane 4.8 times
as often and the collision rate does not move (3.50 against 3.63, inside a run-to-run spread of 3
to 4). Reverted: two extra physics queries per probe and 2.6 times the avoid-crash thrash, for
nothing.

**Arm 1 also retired a theory.** MEASURED. The plain ray arms on another Bloodhawk's airframe 13.8
times per run against about 17 on terrain, so it was never effectively terrain-only, which is what
the first reading of a single user log had suggested.

**Arm 2, the climb-out's symmetry: the binding one.** MEASURED. 3.75 to 2.21 mid-airs per run,
Welch t = 2.8 on 13 degrees of freedom, p about 0.014. Right rather than a coin flip is the
mechanism: two aeroplanes meeting head-on that each break right diverge every time, where a random
side leaves them on the same one half the time. Taken off the ground track and not the airframe's
right axis, so a rolled or inverted pilot breaks the same way as a level one.

**The collisions are head-on.** MEASURED. At impact the two velocity vectors are 175 to 178
degrees apart on most of them, with a minority of converging crosses at 45 degrees and below.

**Not a cure.** 2.2 mid-airs per 150 s of a ten-plane furball is still a lot. The next lever, and
a much larger invention than a displaced aim point, is a real separation term over the aircraft
roster: predicted miss distance at closest approach, pushing the aim point laterally under a
threshold.

## Instrument bugs and limits, hit on the way

- **ASSUMED-1: `--det` does not pin this scenario across processes.** Two launches on identical
  flags gave 4 and 6 mid-airs. It pins the clock, the master seed and the spawn index, and
  something downstream of those still moves. A single run therefore proves nothing and every
  figure above is a multi-run mean. Whatever the residual source is, it was not chased.
- **ASSUMED-2: the mid-air count is FATAL collisions only.** `midair aspect` prints from the crash
  path, so a contact under the 25 m/s crash threshold is a survivable graze and is not counted.
  The true contact rate is at least this and possibly higher. Both arms of every comparison are
  counted the same way, so the ratios stand even though the absolute rate is a lower bound.
- **INFERRED-1: the head-on threshold of 135 degrees is the instrument's, not the game's.** The
  measured distribution is strongly bimodal (a 175 to 178 cluster and a below-50 one), so the
  exact cut does not matter here, but it is a choice and not a decoded quantity.
- **MEASURED-1: a run that times out must be dropped, not counted.** The first baseline batch had
  one launch never save its shot; its log carried 19 avoid-crash arms against a normal 31, i.e. a
  short sample that would have read as a quiet run. The script now excludes and reports these.
- **MEASURED-2: a `--ia` file names planes by DISPLAY name.** `"player_plane": "player_bhawk"`
  parses without error and then builds no aircraft at all; `"Bloodhawk"` is what works. The first
  two trial runs were silently empty because of this, and the giveaway is the `ia:` log line
  echoing back the name it resolved.
- **MEASURED-3: relative paths in `--screenshot=` and `--ia=` resolve against the engine's own
  working directory, not the shell's.** Both silently miss. The script passes absolute paths.

## Reproducing

```powershell
.\analysis\ai-midair-collisions\dogfight-trials.ps1 -Runs 8 -Label control
```

From a git worktree, point `CSVM_DATA_ROOT` at the primary tree first so `RunGame.ps1` can find
`tools/godot` and `extracted/`. Screenshots land in the git-ignored `.scratch/ai-midair/`; the
per-run stdout logs are the `.scratch/logs/game-*.out` the script names in its table.

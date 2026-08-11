# What the 13 goldens actually cover, for `OBJECT_MOTION` debris

`PLAN-object-motion-decode` A1. Answers "which goldens exercise ballistic debris, which of them
build world colliders, and does any capture window contain a piece actually coming to rest" —
by reading `analysis/goldens/manifest.json` against `SessionSpec.BuildsCollision`
(`CSVM/src/SessionSpec.cs:183`) and by running each candidate shot's own args through
`RunProbe.ps1` with `--debug-anim` added, at the shot's own pinned `frame` (`--frames=N
--screenshot=…`, matching the runner's own reproduction rule, GOLD-4). `MotionSet` exposes
`LaunchCount`/`ContactLandings`/`ClockEndings` (`AnimRuntime.LogMotions`, once per sim-second);
that print is what is read here, not pixels.

## The 13, by mode

| shot | mode | `BuildsCollision` | can reach `WorldEffectsFactory`'s crash/destroy code at all |
|---|---|---|---|
| c1-waterfall, c1b-night-sea, c1c-rain, c2-city, c2b-rain, c3-island, c4-snow, c5-city-night | Freecam | false | no |
| viewer-bhawk | Viewer | false | no |
| empty-stage | **Fly** (`--stage=empty` is a content arg, `SessionSpec.cs:1152`) | **true** | no chapter gamez (`WorldMode` false) — nothing to destroy |
| c1-flight | Fly | true | yes, but fires nothing (`--hold` only) |
| c1-destroy-effects | Fly | true | yes — `--destroy=radiotwr.flt` |
| c1-crash | Fly | true | yes — `--crash=5` |

Only **4 of 13** goldens build colliders, not all and not none. Nine (freecam + viewer) never
touch `WorldEffectsFactory.BuildFlightCrashRuntime`/`EnsureWorldEffects` at all — they build no
world, so there is no destructible or crash rig to launch anything from — and this needed no probe
to establish, since `MotionRuntime`'s own class remark already states nothing ambient fires
`OBJECT_MOTION`; it was spot-checked anyway (`c1-waterfall` below) rather than taken on faith.
`empty-stage` is Fly and does build colliders (a generated ground plane), but plays no destructible
or crash data, so it is inert for this question regardless.

That leaves three — the plan's "goldens 11-13" — as the ones that can possibly exercise
`OBJECT_MOTION`.

## The three Fly-mode goldens, probed

Reproduced with `RunProbe.ps1 <shot's args> --debug-anim --frames=<shot's frame>
--screenshot=<path>` (no `--log-file` override, so streams land in `.scratch\logs\probe-*.out`).
Each run's `[core] shot pixmd5=…` line matched the manifest hash, confirming the probe reproduced
the exact pinned pose (GOLD-4) before its `anim/debug` lines were read.

### `c1-flight` (frame 120 = 2.000 s)

`--chapter=C1 --plane=player_bhawk --hold=0.2,0.1,0,1 --det --mute`. Two `anim/debug` lines print
(one per sim-second, one `AnimRuntime` — the world runtime; the plane carries its own
prop/control-surface animator, no `MotionSet`):

```
anim/debug: 14 live motion(s), 0 ballistic launch(es) so far      (t≈1s)
anim/debug: 6 live motion(s), 0 ballistic launch(es) so far       (t≈2s, at capture)
```

**0 `OBJECT_MOTION` launches in the whole 2 s window.** Nothing in this shot fires a weapon or
triggers a destructible — `--hold` only — so this is expected, not a finding. Confirms this shot
covers the flight HUD/prop/exhaust-trail systems the manifest's `exercises` field already claims,
and nothing about debris.

### `c1-destroy-effects` (frame 120 = 2.000 s)

`--chapter=C1 --plane=player_bhawk --destroy=radiotwr.flt --pos=… --direction=… --hold=0,0,0,0.5
--det --mute`. Three `anim/debug` lines print, one per `AnimRuntime` instance (world, world-effects,
the flight/prop animator), each on its own one-second clock from its own construction time:

```
anim/debug: 15 live motion(s), 1 ballistic launch(es) so far   (world runtime)
anim/debug: 1 live motion(s), 1 ballistic launch(es) so far    (world-effects runtime — moving_fire_ball_01)
anim/debug: 6 live motion(s), 0 ballistic launch(es) so far    (flight/prop runtime)
```

**1 `OBJECT_MOTION` launch, on the world runtime — the killed `ap_radiotwr`'s debris.** No
"contact-tested bodies ended" line prints anywhere in the run, so `ContactLandings + ClockEndings
== 0`: the piece has neither landed nor run its clock out by capture time. Checked against the
extracted data it actually launches from
(`extracted/C1/cam_anim/ap_radiotwr-radiotwr_destruction-healthy.json:240-283`):
`"do_intersections": false`, `"translation_range"` (not `"translation"`), `"run_time": 6.0`. With
`do_intersections` false, `MotionRuntime.TestsContact` is false regardless of whether a mask is
wired — this piece is **structurally** never contact-tested today, not merely "not yet" inside a
2 s window. It runs its authored 6 s clock and holds its final pose, wherever that lands it.

**This shot covers a `translation_range` LAUNCH. It does not cover ground contact and cannot,
without a code change** — `do_intersections: false` takes the untouched path regardless of capture
length (Decision 2/3's territory: this is exactly the "reachable but currently untestable" case C6
is meant to open up, not close).

### `c1-crash` (frame 20 = 0.333 s)

`--chapter=C1 --plane=player_bhawk --crash=5 --hold=0,0,0,0.6 --det --mute`. **No `anim/debug` line
prints at all** — `LogMotions` fires once per sim-second per runtime and the whole capture window is
a third of a second, too short for even the first tick. The `MotionSet` counters cannot be read
directly at this shot's own frame; the question is answered from the log's other lines and the data
instead:

- The log's last ~15 lines before the screenshot are `anim: retarget '<name>' onto '<state>' […
  caller player_crash_dirt]` — `flydirt_plane`, `large_black_smokeball`, `large_10sec_fire`,
  `call_crash_trails`, `large_fireball` — i.e. `player_crash_dirt` is already mid-dispatch by
  frame 20. Checked against `extracted/C1/cam_anim/player-player_crash_dirt.json`: the event that
  registers `piece1`'s motion (`"do_intersections": true`, `"run_time": 6.0`, lines 555/590) has
  `"start": null` — a null-start event, which this project's own convention (`BL-257`'s census,
  `SequenceRunner.cs`) fires on the SAME tick its sequence starts, not after a delay. So `piece1`–`4`
  launch at essentially crash-onset, well inside this shot's 0.333 s window.
- `BuildsCollision` is true here (Fly) and `WorldEffectsFactory.cs:373-375` wires
  `crashRuntime.ContactMask = CollisionLayers.World` whenever it is, so `piece1`–`4`'s sweep IS
  armed in this golden — unlike `c1-destroy-effects`'s piece, this one is not structurally exempt.
- But `run_time` is 6.0 s, and `MotionRuntime`'s own arming guard (`ArmSeconds`/`ArmDistance`,
  `MotionRuntime.cs:84-105`) holds the sweep off for the first ~0.1 s or 2 m of flight specifically
  so a piece starting inside its own wreck doesn't register a same-frame contact. 0.333 s is not
  enough time for a piece launched at ~t=0 to clear that guard, travel, and land, whatever the
  eventual contact tier turns out to be.

**This is exactly the situation A1's own Evidence section predicted**: the sweep is armed in this
golden, and the golden's capture window ends before any piece can reach the ground. `c1-crash`
covers the crash rig standing up and its debris LAUNCHING; it does not, and today cannot, cover a
landing.

## The `--freecam` spot check

One freecam shot was run the same way, as an able-to-fail control rather than trusting the "nothing
ambient fires `OBJECT_MOTION`" claim on the class remark alone (`docs/verification.md`'s standing
rule that an unchanged/zero reading is not evidence until it has been shown able to move):

```
c1-waterfall  --freecam --chapter=C1 --pos=… --lookat=… --det --mute --debug-anim --frames=120
  → anim/debug: 14 live motion(s), 0 ballistic launch(es) so far   (t≈2s, at capture)
```

0 launches, as expected. The other 7 chapter freecam shots and `viewer-bhawk` were not
individually re-run — none of them passes `--destroy=`/`--crash=`/a weapon flag, and freecam/viewer
build no world colliders regardless (`BuildsCollision` false), so there is nothing for a probe to
discover that isn't already structural.

## Proving the instrument can move at all

`ContactLandings` is not a counter that only ever reads zero: `.\RunTests.ps1`'s `engine` stage
(this run, pre-change baseline below) already exercises it and reports a live, nonzero result —
`ground-contact` suite: *"contact 3.18s ending 0.00 m from the surface; unmasked 20.02s ending
-1965.00 m from it"*. The counter can register a landing; none of the 13 goldens currently drive one
to.

## The answer

**No golden capture, today, contains a piece actually coming to rest.** Two of the thirteen
(`c1-destroy-effects`, `c1-crash`) do launch ballistic `OBJECT_MOTION` debris inside their capture
window — one `translation_range` launch each — but neither shows a landing: one is structurally
untestable (`do_intersections: false`), the other is armed but its window is far too short for its
6 s run time. This is the gap Decision 3 / D11 exists to close, and it is unresolved by this item on
purpose — D11 pins the golden that shows debris coming to rest; A1 only establishes that none of the
current 13 already does.

## Pre-change baseline (2026-08-10)

`.\RunTests.ps1` (build clean, 949/949 units, 37/37 engine suites, engine errors clean): **13/13
goldens hash-identical to the committed manifest** —

| shot | hash |
|---|---|
| c1-waterfall | `873fe6e045e805f71573bafff1a30640` |
| c1b-night-sea | `322b8c77c64a0724f169e0c266d163f8` |
| c1c-rain | `7adedc7062834e7ff5acb3feaffccad5` |
| c2-city | `0af0605b56a172f51b06ef096822759f` |
| c2b-rain | `0d6bbf3cbcdddfc32cfc6f89c64fae69` |
| c3-island | `321e9a5ea523395b0aa400eb3c41e9c5` |
| c4-snow | `e270860b89d077597c00caffb7abe2b2` |
| c5-city-night | `59ce2730871aa632ceb402e99caa19fb` |
| viewer-bhawk | `d952186fbb529953ce2eb38d72dd7488` |
| empty-stage | `6edc1b0b92c6003e84dcac15ac96559e` |
| c1-flight | `7be47c666ac71748788cb66e8d7a0b8c` |
| c1-destroy-effects | `5c8a15f7b4d4143edb1873da98c4850d` |
| c1-crash | `1c6843422a489e013c37f246a0331451` |

No golden is re-pinned by this item. These 13 hashes are the baseline every later item's golden
movement is measured against.

## Postscript: what "covers a launch" is worth in pixels (B4, 2026-08-11)

A1 established that `c1-destroy-effects` and `c1-crash` each launch one ballistic
`translation_range` body inside their window. B4 changed the launch direction for every such body
install-wide and only **one of the two hashes moved** — so "covers a launch" turns out to be
necessary and not sufficient, and the gap is amplitude.

`c1-destroy-effects` has a measured **sensitivity floor between 0.85× and 2×** of the authored
launch. Sweeping `--debris-launch` on the shot's own args at its own pinned frame:

| `--debris-launch` | hash |
|---|---|
| (default 0.65) | `5c8a15f7b4d4143edb1873da98c4850d` — pinned |
| 0.5 | `5c8a15f7b4d4143edb1873da98c4850d` — unchanged |
| 0.85 | `5c8a15f7b4d4143edb1873da98c4850d` — unchanged |
| 2 | `a241843fb25935b67e804af400277ae6` — moved |
| 10 | `c55e90212b94100b8460839badef72c8` — moved |

Its one body (`ap_radiotwr`'s `upper`: elevation 30–70°, speed 2.5–4.5 m/s, gravity −2, starting
0.2 s in) is still inside the fireball column that fills this framing at frame 120; only a throw
large enough to carry it clear of that column reaches the pixels. B4's own magnitude ratio is
0.745–0.81, inside the dead band — which is why its unchanged hash is an explained result and not an
unnoticed regression. **⚠ Read the same way round in future: an unchanged hash here bounds the
change at under ~2×, and says nothing at all below that.**

`c1-crash`'s movement also needed re-attributing. Its `player_crash_dirt` pieces are all the
**vector** `translation` form — B4 cannot reach them. What moved is
`carnage_trails-call_crash_trails`' five `fly_trailN`, the only `translation_range` bodies in that
shot, whose manifest `exercises` now says so.

## Loose end for D12

`docs/formats/destructibles.md:330-331` carries the same wrong claim as the two comments this item
corrects ("A session that builds no colliders … which is why every golden capture is unchanged") —
out of scope here (A1's own approach names only `MotionRuntime.cs:212` and
`WorldEffectsFactory.cs:372`), left for D12's sweep.

## Related

`analysis/object-motion-ground-rest/FINDINGS.md` (do_intersections/RUN_TIME census this reads
`ap_radiotwr` and `player_crash_dirt` against) · `docs/verification.md` GOLD-4, GOLD-9, LOG-17 ·
`analysis/goldens/manifest.json` · `docs/PLAN-object-motion-decode.md` A1/D11.

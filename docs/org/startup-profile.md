# The startup profile line: grammar, phases and terms

This page is the reference for CSVM's own startup timing report,
`CSVM/src/Utils/StartupProfile.cs`. It holds the line's grammar, the rules a phase obeys, and what
each term means. The module's purpose is its entry in
[`../architecture/Utils.md`](../architecture/Utils.md); how `boot` and `rest` mislead is
`docs/verification.md` PERF-16.

## The line

One line per session build, in the `perf` log category:

```
[perf] startup mode=<mode> <subject> total=<ms> boot=<ms> <phase>=<ms> … rest=<ms> first_frame=<ms>
```

`mode` is the session shape (`fly`, `freecam`, and so on), the same token the log file is named
after. `subject` is the scenario identity as ready-formatted `key=value` fragments
(`chapter=C1 plane=player_bhawk`), and is absent when the caller set none. Every millisecond value
carries one decimal and is formatted invariantly, so the line is identical on any machine's locale.

The report only reports. It holds no thresholds, no verdicts and no comparisons, because a
comparison needs a warm-up protocol this module deliberately does not own.

## The terms

| Term | Meaning |
|---|---|
| `boot` | engine start to the profile's construction |
| `<phase>` | one instrumented span of the session build, in the order first seen |
| `rest` | `build - sum(phases)`, real uninstrumented build work |
| `first_frame` | build end to the frame that proves the first has been drawn, or `none` |
| `total` | `boot + build + first_frame` |

The line asserts `total = boot + sum(phases) + rest + first_frame`. Every phase must therefore be
a leaf, never nested inside another, or the sum silently double-counts.

`first_frame` is measured on the second rendered frame after the build, since that is the frame
which proves the first was drawn. A run that quits inside the build never renders one, and the
teardown path emits the line with `first_frame=none`.

## Phases

`StartupProfile.Mark()` opens a span and `Record(phase, mark)` closes it into the session being
timed. Both are ambient statics over `Current`, so the shared build code carries the calls
unconditionally and records blind; with no session under measurement they are no-ops.

Repeated calls with the same phase name accumulate, which is why the three `GameZ.Load`s of a
flight session appear as one `gamez` figure. A phase nothing recorded is absent from the line
rather than printed as zero.

`Phases` is a read-only view of the same accumulator for a caller that wants to aggregate or
categorize the spans without emitting the line, and it never closes the build itself, so a caller
timing the build with its own stopwatch can read the phases mid-build. That is what
`CSVM/src/Testing/PhaseAttribution.cs` reads. The in-engine harness builds worlds through the very
same code, so it installs a private profile as `Current` for the length of one world build and
restores the previous value afterwards; outside such a build `Current` stays null there, which is
what keeps the harness's several census worlds from accumulating into one nonsense line. A suite's
phases and a session's `[perf] startup` line therefore never mix.

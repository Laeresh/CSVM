# Dispatch-axis families for AnimRuntime

`AnimRuntime.cs` is decomposed on the event-kind axis, not the mode axis. Three internal families
in `Anim/` (pose/visual, sound, light) each own their event kinds' dispatch bodies, state, tick and
teardown; the `AnimRuntime` facade is unchanged and remains the sole entry point, keeping the
explicit `switch (ev.Kind)` whose case labels mirror the original's dispatch table. Families are
plain internal classes constructed with exactly the dependencies they need; there is no host
interface and no kind-to-family registry. `_rest` (the authored pose of every touched node) stays
on the runtime as shared substrate, reached through a narrow recorder seam, because the pose
handlers and the death flow both depend on it.

## Considered options

A split on the **mode** axis (ambient / effects / crash interfaces) was examined by a
design-it-twice session in August 2026 and declined: a per-caller census showed production callers
already hold narrow slices, mostly through delegate sinks (`DamageSink`, `EffectSink`, the crash
trio) that narrow harder than an interface would. That session's deliverable was a "do not
re-propose" warning in `architecture.md`, later deleted and deliberately not restored: a
never-do-this is too strong a record for a module this size. This ADR is the re-openable record
that replaces it. The census was re-run on 2026-08-21 before the dispatch-axis decision; it
confirmed that no caller reaches toward any family's internals, so the mode-axis evidence still
holds and does not conflict with the event-kind cut.

## Consequences

Callers and tests see no change; the seam can be revisited without unwinding public API. A future
session proposing a mode-axis split should start from this record and a fresh caller census, not
from a prohibition.

# The diagnostic log: categories, levels, sinks and line grammar

This page is the reference for CSVM's own log, `CSVM/src/Utils/Log.cs`. It describes the call
shape, the fixed vocabularies a caller picks from, what each sink writes, and how a line is
filtered. The module's purpose and its place in the session is its entry in
[`../architecture/Utils.md`](../architecture/Utils.md).

## The call shape

`Log.Info("world", $"…")`, and its `Warn`, `Error` and `Debug` siblings, take a category and one
interpolated message. The message is rendered with `CultureInfo.InvariantCulture`, so a float
reads `16.667` on every machine and never `16,667`. Pass one interpolated string rather than a
concatenation of two, since only the whole `FormattableString` is rendered invariantly.

`Log.Error(cat, message, exception)` adds the exception: the console gets its type and message,
the file also gets the stack. `Log.Block(text)` writes an already-formatted multi-line block
verbatim to both sinks.

## Categories

Nine, and the vocabulary is closed. A category is a subsystem an investigator would want to turn
up on its own.

| Category | Covers |
|---|---|
| `anim` | the animation runtime and authored motion |
| `world` | world and scene building |
| `flight` | the flying aircraft |
| `weapons` | weapons, projectiles and impacts |
| `sound` | audio |
| `perf` | the instrument lines (`[perf] startup`, `[perf] hitch`) |
| `test` | the in-engine assertion harness |
| `ui` | the launchscreen, the screens and the inspection labs |
| `core` | the session spine |

`ui` is separate from `core` because a user reads the labs' state dumps on purpose and has to be
able to silence them without silencing the session spine.

## Levels

`Error` (0), `Warn` (1), `Info` (2), `Debug` (3). The console threshold defaults to `Info`.
Errors and warnings ignore the threshold entirely and are not suppressible; only `Info` and
`Debug` can be filtered out of the console. A per-frame diagnostic still gates itself at the call
site behind its own `--debug-*` flag, because the file sink takes everything regardless.

## The two sinks

The console is the human's view and stays quiet by default. The file sink is the machine's view
and always takes everything, at every level, in every category, line-flushed so a crash still
leaves what was written.

The file is `<repo>/.scratch/logs/<mode>-<stamp>.log`, where `mode` is the session shape (`fly`,
`freecam`, and so on) and `stamp` is `yyyyMMdd-HHmmss`. Two sessions starting in the same second
collide, so the process id is appended in that case. `Log.SinkPath` is the open file's absolute
path, and `HitchSidecar` derives its own `.hitches.jsonl` name from that same stem. Lines logged
before the sink opens are held in a bounded prelude (512 lines) and written the moment it does.

A file line is `<TAG> [<category>] <message>`, with `TAG` one of `ERROR`, `WARN `, `INFO `,
`DEBUG` padded to a common width. There is deliberately no timestamp column: a `--det` run has to
produce a byte-identical log, so a line that needs the time carries it as an explicit `key=value`.

## Where a console line goes

A console line resolves through three tiers, in order: the scoped sink for this execution flow,
then the process-wide `Log.ConsoleSink`, then the engine's `GD.Print` / `GD.PrintErr`.

`Log.ConsoleSink` is a process-wide default a test host installs once, so that a plain
(non-`Node`) class that logs is callable without an engine. `Log.PushConsoleSink(sink)` returns an
`IDisposable`, nests, and is per execution flow, which is what a test asserting on console lines
takes: a class running in parallel can neither steal its lines nor add its own. A thread spawned
inside a scope does not inherit it unless it captures the execution context.

Warnings are written plainly rather than through `GD.PushWarning`, because Godot .NET appends a
managed stack trace to every pushed warning and buries the message it is meant to surface.

## The `--log=` filter

`--log=` takes comma-separated tokens, each one of `cat`, `cat:level`, `*`, `*:level`, or a bare
level. A bare category turns that category up to `debug`; a bare level sets every category, so
`--log=debug` and `--log=*:debug` are the same filter. Parsing touches no Godot API, so it is
callable from a test host. An unknown category is kept rather than dropped and is reported when
the sink opens. The filter affects the console only.

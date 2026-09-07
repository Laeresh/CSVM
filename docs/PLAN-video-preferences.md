# Video preferences — the VIDEO page and the display settings behind it

**ACTIVE PLAN** (written 2026-09-06). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

This plan delivers `BL-768`: Preferences' VIDEO door opens a real page composed over its own
authored artwork, Enhanced Graphics moves on to it from Game Options where it never belonged, and
four display settings the port has never had stand beside it. `BL-768` was re-verified still-open in
this session against both the record and the code: the only commit naming it is `b48355ed`, the plan
closure that filed it, and `PF_B_VIDEO` is still the third entry of the disabled-doors array
(`CSVM/src/UI/Menu/Original/OriginalShell.cs:192-195`) while Enhanced Graphics is still the third
row of the Game Options table (`CSVM/src/UI/Menu/Original/OriginalGameOptions.cs:79-82`). No other
backlog item is drawn into this plan.

The plan splits at the wave boundary, and the split is the point. Wave A is a page build over an
existing pattern and touches only the files `BL-768` names. Wave B is a mechanism the port does not
have: nothing anywhere sets a window size, mode or screen at runtime, and `options.json` has never
carried a value that was not a vocabulary word. Wave A can land alone and be worth having;
Wave B cannot start before it.

## Milestone goal

- Preferences' VIDEO door opens a page instead of drawing greyed, the second of the three unbuilt
  doors to be answered after `BL-696`'s CONTROLS.
- Enhanced Graphics stands on the video page, not as a remake-only row on an authored Game Options
  page.
- A player picks their monitor, resolution, display mode and V-Sync at the controls, with no
  `config.json` and no command-line flag.
- Every one of those choices survives a restart, saved by the options file's one writer.

**The five authored quality rows do not come back.** Viewing Range, Effects Level, Objects Detail,
Lighting Quality and Texture Quality were tiers for 2000-era hardware. The knobs nearest to them
here are either fidelity values the mission data owns or settings that cost nothing on a modern GPU,
and a row that changes nothing is worse than an absent one.

## Decisions (2026-09-06)

| # | Question | Decision |
|---|---|---|
| 1 | Which of the nine authored rows can this port honour at all? | **Five rows, not nine** — Graphics as the monitor pick, Resolution, Display Mode, V-Sync, and Enhanced Graphics moved off Game Options. This is the decision `BL-768`'s own Trap named as wanted first. |
| 2 | Viewing Range, Effects Level, Objects Detail, Lighting Quality, Texture Quality? | **Left out.** Viewing range would expose the mission's own fog and fade distances as a player preference; effects level would expose pool sizing that is a fidelity and four-player question (`BL-537`); there is no object LOD system to tier; lighting quality duplicates Enhanced Graphics; anisotropy already sits at 16x and costs nothing. |
| 3 | Clutter Detail and Shadows? | **Left out.** Clutter off is a less faithful world for no gain on hardware that renders it free, and sun shadows are already gated on Enhanced Graphics (`Launcher.EnableSunShadows`), so a Shadows row would be a second switch on one gate. |
| 4 | Does the Graphics row keep its authored description? | **Rewritten.** "Select a 3D card or Software mode" does not describe a monitor pick, and there is no software renderer nor a game-level card choice to honour instead. The row takes the same licence the Enhanced Graphics row already takes on an authored page. |
| 5 | Do we need a frame-rate limit? | **Yes, but not as its own row.** It rides V-Sync's off state. In `Realtime` the clock's `ParentDriven` is false, so `GameSession` steps the simulation from its physics callback rather than once per rendered frame (`CSVM/src/Utils/GameClock.cs:88-91,141-142`): the render rate never reaches the flight model. The limit is a courtesy to a GPU rendering unseen frames, not a correctness fix. |

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + the module's entry in `docs/architecture/<Namespace>.md` (plus its index
  bullet in `docs/architecture.md`) / `docs/formats/` are updated in the same turn** as each landed
  item; a landed item gets its record in the landing commit's message and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture/<Namespace>.md` (found through the index in
  `docs/architecture.md`) before modifying it,** then the comments on the members you touch; dead
  ends are in the landing commits (`git log --grep=<ID>`), so search those before re-chasing one.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — the page

1. ☑ The VIDEO page opens from Preferences, carrying Enhanced Graphics moved off Game Options

### Wave B — the display settings

2. ☑ The options carrier takes a setting that is not a vocabulary word
3. ☑ V-Sync, with the frame limit on its off state
4. ☑ Display Mode
5. ☑ Resolution
6. ☐ Graphics, as the monitor pick
7. ☐ The precedence ladder and the golden guard

## Dependency and parallelism notes

A1 blocks everything: every Wave B item adds a row to the page A1 builds. B2 blocks B3 through B6,
which all read and write fields B2 adds to `OptionsDef`, validate through `OptionsStore` and ride
`OptionsApplyExit`. B7 verifies the four together and runs last.

**Do not run B3 through B6 in parallel.** They contend on the same three files: the page's row table
in the new `OriginalVideo` partial, `Launcher.ApplyOptions`, and `OptionsStore`'s validation. Run
them as a chain in one worktree. If any item does run in a parallel worktree, remember that
`git stash` is repo-global and shared across worktrees here; use a local commit or a file copy.

---

# Wave A — the page

## A1 ☑ The VIDEO page opens from Preferences, carrying Enhanced Graphics moved off Game Options

**Goal.** Pressing VIDEO on Preferences opens a page composed over the `Video` section's own
artwork, showing Enhanced Graphics as its one row. ACCEPT CHANGES saves and returns to Preferences;
CANCEL CHANGES returns with the choice dropped. Game Options is down to its two remaining rows.

**Evidence (confidence: traced).** `PF_B_VIDEO` is the third entry of `PreferencesPageKeys` and
draws disabled because no shared option stands behind it
(`CSVM/src/UI/Menu/Original/OriginalShell.cs:192-195`). The `Video` section of
`extracted/rof/menu_layout.json` carries 31 widgets: `VP_BACKGROUND`, `VP_B_ACCEPTCHANGES` and
`VP_B_CANCELCHANGES`, the checkbox buttons `VP_B_CLUTTER` and `VP_B_SHADOWS`, seven dropdowns
(`VP_D_Device`, `VP_D_Display`, `VP_D_View`, `VP_D_Effects`, `VP_D_Objects`, `VP_D_DLight`,
`VP_D_Texture`), and a title plus a description string per row. `OriginalScreenshots/Preferences
Video.png` shows the page live with all nine rows. The Enhanced Graphics row is `GameOptions[2]`
(`CSVM/src/UI/Menu/Original/OriginalGameOptions.cs:79-82`), a `GraphicsWords` radio reading and
writing `_graphics`, with a description that reports whether a restart is still owed (`:101-112`).

**Approach.** Mirror `OriginalGameOptions.cs` rather than inventing a second page shape: a
`VideoSection` constant, the door key, a row table of the same record shape (title, description
selector, control kind, words, read, write) and a `BuildVideoRows` carrying the same no-layout
fallback to text buttons, so the page stays walkable in an install without the section. Enable
`PF_B_VIDEO` in `PreferencesPageKeys`. Move the `GraphicsKey` entry out of the `GameOptions` array
into the new one, taking `GraphicsDescription()` with it. Do not widen `OptionsApplyExit` here; the
graphics word already rides it, so this item changes which page writes it and nothing else.

**Model recommendation.** medium. The page pattern exists in full and the move is mechanical; the
judgement is composition against the authored geometry, not design.

**Verify.** Add the page's `--menu=` name (`LaunchMenu.cs` holds the names, `docs/cli.md`'s
`--menu=` bullet) and take a `--screenshot` of it to compare against
`OriginalScreenshots/Preferences Video.png` for row placement and the description column. Drive the
door and both plaques through the Original menu suites the way `MenuOriginalSuites` already drives
Game Options, asserting one `OptionsApplyExit` on Accept and none on Cancel. `.\RunTests.ps1` green.

**⚠ Traps.** The page has its own `VP_B_ACCEPTCHANGES` / `VP_B_CANCELCHANGES` pair; do not route it
through `GameOptionsAcceptKey`, or Cancel on one page will read as Accept on the other. Enhanced
Graphics resolves once at launch and its description says so, so the description selector has to
move with the row rather than being rewritten as a static string. Game Options must still open on
its first row with `_goOpen` cleared (`OpenGameOptions`, `:94-99`); the new page needs the same
open-on-first-row treatment, since a form is not a list.

**Landed.** `OriginalVideo.cs` is the page, a shell partial mirroring `OriginalGameOptions.cs` with
one column more in its row record: each setting names the authored title, control and description
widgets it stands on, since the `Video` section gives every row its own line, control column and
width and a shared pitch would put no row where the artwork draws it. Enhanced Graphics stands on
the authored Shadows row, whose gate it owns and whose title box is wide enough for the name. Two
numbers the section does not state are derived and commented at the member: a title box stops at the
control beside it, and a description the section gives no width wraps at the plaque column. Both
option pages read all three saved words on entry and each one's ACCEPT CHANGES carries all three, so
`OptionsApplyExit` is unchanged and the store keeps its one writer. The `--menu=` name is `video`,
with `video:checked` for the ticked pose; `game-options:checked` is gone with the row.

**Verified.** Full `RunTests.ps1` on the plan tree: build 0 warnings, units 3476 passed / 0 failed,
engine 258 passed / 0 failed with engine errors clean over 4 shards, goldens 18 shots hash-identical
on the RTX 5080 / 1.4.351, exit 0. `--menu=video` composes the page over the `Video` section's own
artwork, and against `OriginalScreenshots/Preferences Video.png` the plate, the VIDEO tab, the
plaque column and the description column all land on the original's. The door and both plaques are
driven through `MenuOriginalSuites`, asserting one `OptionsApplyExit` on Accept and none on Cancel.

# Wave B — the display settings

## B2 ☑ The options carrier takes a setting that is not a vocabulary word

**Goal.** `OptionsDef`, `OptionsStore` and `OptionsApplyExit` can carry a monitor index, a
resolution, a display mode and a V-Sync choice, with the same guarantees the three existing options
have: one writer, validation that drops an unknown value rather than failing the file, and a missing
field that reads as never set.

**Evidence (confidence: traced).** `OptionsDef` carries three nullable strings and nothing else
(`CSVM/src/Utils/OptionsStore.cs:12-21`). Every field is validated against a fixed `HashSet<string>`
and an unknown value is dropped like a missing one, with only the version gate rejecting a whole
file (`:120-147`). `OptionsApplyExit` is documented as one of exactly four members of a closed
hierarchy and carries three values, with the note that all three ride the exit rather than being
saved by the screen so the options file keeps one writer
(`CSVM/src/UI/Menu/MenuExit.cs:17-40`). `Launcher.ApplyOptions` is that writer
(`CSVM/src/Session/Launcher.cs:1336-1362`). The store's `Version` comment states that adding a field
does not bump the schema version, because a missing field already reads as never set (`:33-39`).

**Approach.** Keep the word-valued shape wherever it fits: display mode and V-Sync are vocabularies
like the three existing options and cost nothing but a new `HashSet`. Resolution and the monitor
index are not words. Prefer a canonical string form (`"1920x1080"`, and the monitor as its index
rendered decimal) validated by shape rather than by membership, so `Read`'s "drop an unknown value"
contract still holds and the file stays readable by eye. Widen `OptionsApplyExit` with the four
values in one edit, since the record is deliberately closed and a second widening is a second
review. Do not bump `OptionsStore.Version`.

**Model recommendation.** high. The record is closed on purpose and the store's drop-on-unknown
contract is what keeps a bad file from bricking the options; widening both is the highest
blast-radius edit in the plan.

**Verify.** Extend `CSVM.Tests/OptionsStoreTests.cs`, the xunit suite that already asserts this
contract per field and needs no engine, so the new fields are proven as units. The round-trip
properties to assert are the ones the class already
promises: a serialized def deserializes equal, a file missing the new fields loads with the old ones
intact, an out-of-shape resolution or monitor index reads as null rather than invalidating the file,
and a file at a wrong version is rejected whole. `.\RunTests.ps1` green.

**⚠ Traps.** A saved monitor index can name a screen that is no longer plugged in, so validation
proves the shape and the apply proves the screen exists; those are two different checks and the
second cannot live in the store, which has no engine. Do not add a save call to the new page: the
single-writer rule is the reason the exit carries values at all.

**Landed.** `OptionsDef` carries four more nullable strings. `MonitorIndex` and `Resolution` are
canonical text validated by shape: `OptionsStore.FormatResolution` writes the one spelling a size
has and `TryParseResolution` / `TryParseMonitorIndex` are both the validation and the reader's way
back, so the writing side and the validating side cannot drift apart, and a malformed one is
dropped exactly as an unknown word is. `DisplayMode` and `VSync` are vocabularies in the new
`DisplayWords`, whose V-Sync list holds the frame caps as words, so one field carries both the
choice and the cap the decision table put on its off state. `OptionsApplyExit` widened to seven
values in one edit, the four new ones required rather than defaulted so the compiler named every
construction site. Both Original pages now leave through one `AppliedOptions()` on the shell and
Built-in's Options screen reads the four on entry, because a screen that shows a setting it does
not own must still hand it back or `Launcher.ApplyOptions`, still the file's one writer, would
clear it. No row was added and nothing is applied to the engine; `OptionsStore.Version` does not
move, for the reason its own comment gives.

**Verified.** Full `RunTests.ps1` on the plan tree: build 0 warnings, units 3484 passed / 0 failed
(the 3476 standing plus 8 new in `OptionsStoreTests`), engine 258 passed / 0 failed with engine
errors clean over 4 shards, goldens 18 shots hash-identical, exit 0. Each drop-on-bad-value test
ends by loading a well-shaped value through the same field and asserting it survives, so a field the
reader never looks at fails the test rather than passing it (`docs/verification.md` METHOD-10), and
the `menuPresentation` beside it in each fixture is the control saying the file itself was read.

## B3 ☑ V-Sync, with the frame limit on its off state

**Goal.** A V-Sync row on the VIDEO page takes effect immediately, survives a restart, and offers a
frame cap that is meaningful only while V-Sync is off.

**Evidence (confidence: traced).** The mechanism exists and only the UI is missing.
`Launcher` reads `Config.GetBool("display.vsync", true)`, ORs it with `_spec.NoVsync`, and on the
off path calls `DisplayServer.WindowSetVsyncMode(Disabled)` with `Engine.MaxFps = 0`, logging which
of the two sources won (`CSVM/src/Session/Launcher.cs:454-469`). `docs/cli.md:169-174` documents
`--no-vsync` as a measurement flag that beats the config key. `Engine.MaxFps` is assigned nowhere
else in the tree. The frame limit does not touch simulation: `GameClock.ParentDriven` is false in
`Realtime`, so the sim steps from the physics callback on `PhysicsDt(godotPhysicsDelta)`
(`CSVM/src/Utils/GameClock.cs:88-91,141-142`).

**Approach.** One row whose words are the V-Sync choice plus the caps (a single dropdown reading
V-Sync / Unlimited / 60 / 120 / 144 is one control instead of two where one greys the other; the
decision table settled the behaviour, not the control shape). Apply live in `ApplyOptions` through
the same two calls `Launcher` already makes at startup, and read the saved value at startup as a
third source below the flag. Default is V-Sync on, which reproduces today's behaviour with no file.

**Model recommendation.** medium. The engine calls exist and are one line each; the care goes into
the precedence, which is B7's to prove.

**Verify.** The cap is observable in-engine rather than at the controls alone: `Engine.MaxFps` and
`DisplayServer.WindowGetVsyncMode()` read back what the apply set, so a `--run-tests` suite asserts
both after driving the row. At the controls: `--debug-fps` shows the rate pinned at the refresh with V-Sync
on and at the chosen cap with it off. `.\RunTests.ps1` green.

**⚠ Traps.** `--no-vsync` must keep beating the saved choice, exactly as it beats `display.vsync`
today, and the log line naming the winning source has to learn the third source rather than
silently reporting one of the two it knows. The startup path already logs whether vsync is on
before the session builds, so the saved value has to be read early enough to reach it.

**Landed.** `Utils/VSyncSetting.cs` is the setting: `Resolve` layers the three sources and `Apply`
makes the two engine calls and logs which layer won, so the startup read and an Options apply
cannot drift apart and the frame cap has one owner. `Launcher` calls it in both places, the startup
one reading the saved word through `SavedWord`, which holds the `--det` guard the way
`Launcher.cs:593` holds the graphics one. The `--run-tests` store redirect moved above the vsync
block rather than the read moving down: the redirect's own rule is that it precedes the first
`UserOptions()` call, and moving the block instead would have put the config read after `--det`
dropped its overrides and changed what a deterministic run does with `display.vsync`. The page's
row is a dropdown on the authored Effects Level line, leaving the three rows above it for the
monitor, the resolution and the display mode in the order those read in; the table is now in
authored row order, since the cursor walks it, and a dropdown opens its list through the Game
Options page's own mechanism, whose overlay is now shared as `ComposeOptionList`. The
`--menu=video:checked` aid names the graphics row rather than pressing the page's first.

**Verified.** Full `RunTests.ps1` on the plan tree: build 0 warnings, units 3485 passed / 0 failed,
engine 259 passed / 0 failed with engine errors clean over 4 shards, goldens 18 shots
hash-identical, exit 0. The new `display-vsync` engine suite reads back `Engine.MaxFps == 144` and
`WindowGetVsyncMode() == Disabled` after applying what ACCEPT CHANGES carried, then `Enabled` and 0
for the On word, restoring the entry pacing in a `finally`. It proves the ladder and the `--det`
guard with the non-det read as its control, so a guard that always returned null fails rather than
passes (`docs/verification.md` METHOD-10).

## B4 ☑ Display Mode

**Goal.** A Display Mode row offering windowed, borderless fullscreen and exclusive fullscreen,
applied immediately and restored at the next launch.

**Evidence (confidence: traced).** Nothing in the tree calls `DisplayServer.WindowSetMode`; the only
window calls are the focus pair at `CSVM/src/Session/Launcher.cs:437-443`. The project ships a
windowed 1280x720 (`CSVM/project.godot:17-21`), and `window/size/no_focus=true` is what the
interactive path clears at startup so a scripted run does not steal focus (see
`docs/verification.md`'s SHELL-13). **A second claim this item started from is wrong and is
corrected here:** a mode change is not the first time the menu meets a viewport other than
1280x720. `project.godot` sets no `window/size/resizable` and no `display/window/stretch/*`, so
Godot's defaults stand and the window has always been resizable; `CSVM/src/UI/BoardFit.cs:16-34`
maps every board into a fixed 800x600 authored space at a uniform
`scale = min(vw/800, vh/600)`, centred with the remainder letterboxed, and
`CSVM/src/UI/LaunchMenu.cs:852-854` marks the board dirty when `GetVisibleRect().Size` changes, so a
live resize already recomposes. What this item adds is setting the mode explicitly and persisting
it, not new size handling.

**Approach.** Three words, `DisplayServer.WindowSetMode` in `ApplyOptions` and again at startup from
the saved value.

**Model recommendation.** medium.

**Verify.** The golden harness carries the size surface already: `analysis/goldens/manifest.json`
pins `"size": "1280x720"` and `RunTests.ps1` fails any shot whose rendered size differs, naming both
(`:1153`). The manifest holds one size for every shot, so a menu shot at a second viewport is a
`--screenshot` probe compared by eye, not a pinned golden. Since the uniform fit is proven at 16:9
already, the probe's subject is an **aspect ratio** the fit has not been looked at: 32:9, which is
what both fullscreen words resolve to on the development machine. At the controls: switch modes on
the page and confirm the page redraws correctly in each, then leave and re-enter to confirm the
choice persisted. `.\RunTests.ps1` green.

**⚠ Traps.** Do not clear `no_focus` twice or re-request foreground on a mode change; the startup
path owns focus and a scripted run depends on it not being handed back (SHELL-13). Exclusive
fullscreen on a multi-monitor machine interacts with B6's monitor pick, so land B6 after this and
apply the screen before the mode.

**Landed.** `Utils/DisplayModeSetting.cs` is the setting, `VSyncSetting`'s shape with one source
fewer: `Resolve` layers the saved word over the windowed default (there is no flag and no config
key above it), `SavedWord` holds the `--det` guard, and `Apply` is the only place
`DisplayServer.WindowSetMode` is called. It skips the engine call when the window already stands in
the resolved mode, so a launch that changes nothing leaves the window alone, and it makes no focus
call of its own. Godot's names invert the reading and the module says so at the parse: `Fullscreen`
is the borderless window filling the screen and `ExclusiveFullscreen` is the exclusive mode.
`Launcher` calls it in both places, and the startup one **only for a session someone is at**: a
scripted run's window is hidden off screen and its capture is compared against the viewport
`project.godot` pins, so a saved mode must not reach one, which is a second guard beside `--det` for
a `--screenshot` probe that is not deterministic. `ApplyOptions` now applies the display settings in
the order the window needs them, the mode ahead of the pacing and the screen ahead of both once B6
adds it. The page's row is a dropdown on the authored Viewing Range line, first in the table because
that line is above the V-Sync one; `VSyncIndex` became `WordIndex` over any vocabulary, since both
word rows want the same lookup and the same fall back to the first value.

**Verified.** Full `RunTests.ps1` on the plan tree: build 0 warnings, units 3486 passed / 0 failed,
engine 260 passed / 0 failed with engine errors clean over 4 shards, goldens 18 shots
hash-identical, exit 0. The new `display-mode` suite proves the ladder, the `--det` guard, the page
row and the word-to-enum mapping against the engine, asserting `DisplayServer.WindowGetMode()` on
this run's own windowed window equals what `windowed` resolves to, so a mapping that drifted fails.
Probes at 5120x1440, 1680x1050 and 1280x720 compose correctly: at 32:9 `BoardFit` is height-limited
(`scale = min(5120/800, 1440/600) = 2.4`), so the board renders 1920x1440 over 37.5% of the width,
centred, with every row and both plaques where they stand at 720p. The mode change itself, its
persistence across a restart, and exclusive fullscreen on a second monitor are owed at the controls:
a scripted run's window is hidden and its startup apply is skipped, so no headless run can prove
them.

## B5 ☑ Resolution

**Goal.** A Resolution row listing the modes the chosen monitor supports, applied immediately in
windowed and borderless modes and restored at the next launch.

**Evidence (confidence: traced).** The window size is the project setting alone
(`CSVM/project.godot:19-20`) and no code changes it. The authored row and its description string
exist (`VP_D_Display`, "Select the screen resolution.").

**Approach.** Enumerate from the engine rather than shipping a fixed list, so the row cannot offer a
mode the monitor refuses. Apply through the window size, and read the saved value at startup.

**Model recommendation.** medium.

**Verify.** The baseline is the standing 18-shot manifest, and what catches a leak is the size
assertion above rather than the hashes alone: a resolution that reached a scripted run fails its
shot as "rendered WxH, manifest hashes are 1280x720" before a hash is compared. At the controls:
pick a mode, confirm the window resizes and the menu recomposes, restart and confirm it came back.

**⚠ Traps.** This is the item that can move every golden, and B7 is where that gets proved rather
than assumed. A resolution list is per monitor, so the row's contents change when B6's row changes;
decide whether an unsupported saved mode falls back to the nearest or to the project default, and
say which in the code.

**Landed.** `Utils/ResolutionSetting.cs` is the setting, `DisplayModeSetting`'s shape over sizes
instead of words. **One claim this item started from needed correcting:** Godot 4.7's `DisplayServer`
exposes no video-mode list at all (`ScreenGetSize`, `ScreenGetUsableRect` and `ScreenGetRefreshRate`
are the whole screen surface), so "enumerate from the engine" is `Sizes`, which filters the standard
desktop sizes by the screen's own reported size and adds that size and the project default, both
always offerable. **An unsupported saved size falls back to the project default, never to the nearest
offered one**, and the module says so at `Resolve`: every other option here falls through to its own
default when the saved value is one the reader does not know, a nearest match would make this the one
setting where a size nobody picked reaches the window, and it would need a distance over sizes with
no right answer, since matching by area hands the player an aspect ratio they did not choose. The
page's row agrees with that rule rather than restating it, `ResolutionIndex` showing the default where
`WordIndex` would show the first value, since the first size a screen offers is its smallest and not
the size a launch with no options file runs at. `Apply` is the only place `DisplayServer.WindowSetSize`
is called; it skips a window the mode owns the size of, and re-centres one it did resize, a resize
otherwise growing off the screen's bottom-right and taking the plaques with it. `Launcher` calls it
after `DisplayModeSetting.Apply` in both places, the startup one inside the same
`if (!_spec.IsScripted)` block. The row is the authored Resolution line, its own, keeping the authored
description because that line describes what the row does here; it is first in the table, so
`VideoOption.Words` became a reader off the shell, this being the one row whose words are enumerated
rather than a vocabulary. The shell takes them through a `screenSizes` reader beside its `options`
one, null offering every candidate size, which is what a shell composed without an engine wants.

**Verified.** Full `RunTests.ps1` on the plan tree: build 0 warnings, units 3486 passed / 0 failed,
engine 261 passed / 0 failed with engine errors clean over 4 shards, goldens 18 shots
hash-identical, exit 0. The guard was proved by running rather than by argument, with the player's
own options file backed up and restored byte-identical around it. A saved `"resolution":
"1920x1080"` in the real options file leaves the goldens 18 hash-identical. The decisive isolation
saved a resolution and a V-Sync cap together and took a scripted but non-deterministic shot: the
launcher obeyed the saved V-Sync (`vsync off source=options.json max_fps=144`) while the saved
resolution produced no line and the shot rendered `size=1280x720`. That separates the two guards and
shows `!_spec.IsScripted` stops a size on its own, independently of `--det`, which the suite proves
separately with `SavedWord(det:false)` as the control against `SavedWord(det:true)`.

## B6 ☐ Graphics, as the monitor pick

**Goal.** The authored Graphics row picks which monitor the window opens on, with a rewritten
description, applied immediately and restored at the next launch.

**Evidence (confidence: traced).** The authored row is `VP_D_Device` with the description "Select a
3D card or Software mode", which decision 4 rewrites. Nothing in the tree reads or sets the current
screen; `Launcher` calls `DisplayServer.ScreenGetRefreshRate()` for the startup log line
(`CSVM/src/Session/Launcher.cs:452`) and that is the only screen query anywhere.

**Approach.** Enumerate the screens, label them by index and size, apply through the current-screen
call and re-apply B4's mode and B5's size after the move. Read at startup from the saved index,
falling back to the primary screen when the index names no screen.

**Model recommendation.** medium.

**Verify.** The development machine has one screen, so the item proves the enumerated screen list
and the applied-screen log line here, and the monitor move itself is checked at the controls on a
second machine. A single-screen pass is not a claim that the move works. `.\RunTests.ps1` green.

**⚠ Traps.** The saved index is the one setting that can silently name something absent, so the
fallback is part of the feature and not an error path. Apply the screen before the mode and the
size, or an exclusive fullscreen re-applies on the old monitor.

## B7 ☐ The precedence ladder and the golden guard

**Goal.** The four new settings sit in the same precedence ladder the existing options do, are
ignored under `--det`, and are proven not to move a single golden.

**Evidence (confidence: traced).** The ladder already exists and is documented per option:
`--no-vsync` beats `display.vsync` (`docs/cli.md:174`), and `--difficulty=` beats the saved word
which beats the default, with the note that `--det` reads no saved option at all
(`docs/cli.md:216`). `--det` pins a fixed-dt clock and drops `config.json` overrides so pixel output
is a function of frame count (`docs/cli.md:165`). A saved resolution or display mode that reached a
scripted run would change the rendered viewport, which is the one input every golden shares. The
guard to copy is already written one option down: the saved graphics word is read as
`_spec.Det ? null : ...` (`CSVM/src/Session/Launcher.cs:593`). It has to be copied rather than
assumed, because a golden shot is a `--det` run against the player's real options directory. Only
`--run-tests` redirects the store to a scratch directory (`:570-582`), so `--det` is the whole of
what stands between a saved display setting and every golden.

**Approach.** Assert the ladder rather than describe it: a suite per setting proving the flag beats
the file, the file beats the default, and `--det` reads none of them. Then take the golden run
before and after with a display setting saved, and confirm the shots are unchanged. Update
`docs/cli.md`'s `--no-vsync` bullet to name the third source, and each touched module's entry in
`docs/architecture/`.

**Model recommendation.** high. The regression surface is every golden in the repo, and an unchanged
number here is only evidence if the check has been seen able to fail.

**Verify.** Save a non-default resolution, display mode, monitor and V-Sync choice into the options
file, then run the goldens and confirm byte-identical shots. Take the baseline first, and prove the
check can fail by deliberately letting one setting through under `--det` once and seeing the goldens
move. Full 8-chapter `--freecam --chapter=<X>` regression with zero errors. `.\RunTests.ps1` green.

**⚠ Traps.** "The goldens did not move" proves nothing until the same harness has been seen to move
them, which is why the deliberate failure is part of the verify and not optional. `--det` dropping
the saved options is the guard, so test the guard rather than the symptom: a future setting added
without reading this ladder is the failure this item exists to prevent.

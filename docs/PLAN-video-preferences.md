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

1. ☐ The VIDEO page opens from Preferences, carrying Enhanced Graphics moved off Game Options

### Wave B — the display settings

2. ☐ The options carrier takes a setting that is not a vocabulary word
3. ☐ V-Sync, with the frame limit on its off state
4. ☐ Display Mode
5. ☐ Resolution
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

## A1 ☐ The VIDEO page opens from Preferences, carrying Enhanced Graphics moved off Game Options

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

# Wave B — the display settings

## B2 ☐ The options carrier takes a setting that is not a vocabulary word

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

**Verify.** `<TODO: name the existing OptionsStore suite to extend, and whether it lives in
CSVM/src/Testing or CSVM.Tests>`. The round-trip properties to assert are the ones the class already
promises: a serialized def deserializes equal, a file missing the new fields loads with the old ones
intact, an out-of-shape resolution or monitor index reads as null rather than invalidating the file,
and a file at a wrong version is rejected whole. `.\RunTests.ps1` green.

**⚠ Traps.** A saved monitor index can name a screen that is no longer plugged in, so validation
proves the shape and the apply proves the screen exists; those are two different checks and the
second cannot live in the store, which has no engine. Do not add a save call to the new page: the
single-writer rule is the reason the exit carries values at all.

## B3 ☐ V-Sync, with the frame limit on its off state

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

**Verify.** `<TODO: settle whether the frame cap is observable in a suite, or only at the controls
with --debug-fps>`. At the controls: `--debug-fps` shows the rate pinned at the refresh with V-Sync
on and at the chosen cap with it off. `.\RunTests.ps1` green.

**⚠ Traps.** `--no-vsync` must keep beating the saved choice, exactly as it beats `display.vsync`
today, and the log line naming the winning source has to learn the third source rather than
silently reporting one of the two it knows. The startup path already logs whether vsync is on
before the session builds, so the saved value has to be read early enough to reach it.

## B4 ☐ Display Mode

**Goal.** A Display Mode row offering windowed, borderless fullscreen and exclusive fullscreen,
applied immediately and restored at the next launch.

**Evidence (confidence: traced).** Nothing in the tree calls `DisplayServer.WindowSetMode`; the only
window calls are the focus pair at `CSVM/src/Session/Launcher.cs:437-443`. The project ships a
windowed 1280x720 (`CSVM/project.godot:17-21`), and `window/size/no_focus=true` is what the
interactive path clears at startup so a scripted run does not steal focus (see
`docs/verification.md`'s SHELL-13).

**Approach.** Three words, `DisplayServer.WindowSetMode` in `ApplyOptions` and again at startup from
the saved value. Fullscreen changes the viewport size, so this item is where the menu's composition
over its authored geometry gets its first test at a size nobody has run it at.

**Model recommendation.** medium.

**Verify.** `<TODO: name the golden or screenshot surface that proves the menu composes correctly at
a non-720p viewport>`. At the controls: switch modes on the page and confirm the page redraws
correctly in each, then leave and re-enter to confirm the choice persisted. `.\RunTests.ps1` green.

**⚠ Traps.** Do not clear `no_focus` twice or re-request foreground on a mode change; the startup
path owns focus and a scripted run depends on it not being handed back (SHELL-13). Exclusive
fullscreen on a multi-monitor machine interacts with B6's monitor pick, so land B6 after this and
apply the screen before the mode.

## B5 ☐ Resolution

**Goal.** A Resolution row listing the modes the chosen monitor supports, applied immediately in
windowed and borderless modes and restored at the next launch.

**Evidence (confidence: traced).** The window size is the project setting alone
(`CSVM/project.godot:19-20`) and no code changes it. The authored row and its description string
exist (`VP_D_Display`, "Select the screen resolution.").

**Approach.** Enumerate from the engine rather than shipping a fixed list, so the row cannot offer a
mode the monitor refuses. Apply through the window size, and read the saved value at startup.

**Model recommendation.** medium.

**Verify.** `<TODO: settle the golden baseline. A resolution that leaked into a scripted run would
move every golden, so this needs a before-and-after golden run, not an assertion.>` At the controls:
pick a mode, confirm the window resizes and the menu recomposes, restart and confirm it came back.

**⚠ Traps.** This is the item that can move every golden, and B7 is where that gets proved rather
than assumed. A resolution list is per monitor, so the row's contents change when B6's row changes;
decide whether an unsupported saved mode falls back to the nearest or to the project default, and
say which in the code.

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

**Verify.** `<TODO: this needs a second monitor to verify at the controls; state whether the author
has one, or the item verifies by log line alone on a single-screen machine.>` `.\RunTests.ps1` green.

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
scripted run would change the rendered viewport, which is the one input every golden shares.

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
